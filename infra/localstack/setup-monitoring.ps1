# LocalStack CloudWatch bootstrap (log groups + placeholder alarms).
# Canonical metrics/alerts: Prometheus — see docs/observability-prometheus-canonical.md.
# Custom namespaces CashFlow/* below are NOT published by the apps (logs only).

param(
    [string]$Endpoint = "http://localhost:4566",
    [string]$InternalEndpoint = "http://cashflow-localstack:4566",
    [string]$DockerNetwork = "localstack_default",
    [string]$Region = "us-east-1",
    [string]$LogGroupPrefix = "/cashflow",
    [string]$AlarmTopicName = "cashflow-monitoring-alerts",
    [string]$SnsTopicArn = "",
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

if (-not $OutputFile) {
    $OutputFile = Join-Path $PSScriptRoot "generated\monitoring.env"
}

function Invoke-NativeCommand {
    param(
        [scriptblock]$Command,
        [switch]$AllowFailure
    )

    $previousErrorAction = $ErrorActionPreference
    $previousNative = $PSNativeCommandUseErrorActionPreference
    $ErrorActionPreference = "Continue"
    $PSNativeCommandUseErrorActionPreference = $false
    try {
        $output = & $Command 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
        $PSNativeCommandUseErrorActionPreference = $previousNative
    }

    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "Command failed (exit $exitCode):`n$($output | Out-String)"
    }

    return [pscustomobject]@{
        Output = ($output | Out-String).Trim()
        ExitCode = $exitCode
    }
}

function Invoke-AwsCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    if (Get-Command aws -ErrorAction SilentlyContinue) {
        $result = Invoke-NativeCommand -AllowFailure:$AllowFailure -Command {
            aws @Arguments --endpoint-url $Endpoint --region $Region
        }

        if (-not $AllowFailure -and $result.ExitCode -ne 0) {
            throw "AWS CLI failed: aws $($Arguments -join ' ') --endpoint-url $Endpoint`n$($result.Output)"
        }

        if ($AllowFailure -and $result.ExitCode -ne 0) {
            return $null
        }

        return $result.Output
    }

    $dockerArgs = @(
        "run", "--rm",
        "--network", $DockerNetwork,
        "-e", "AWS_ACCESS_KEY_ID=test",
        "-e", "AWS_SECRET_ACCESS_KEY=test",
        "-e", "AWS_DEFAULT_REGION=$Region",
        "-e", "AWS_PAGER=",
        "amazon/aws-cli"
    ) + $Arguments + @("--endpoint-url", $InternalEndpoint)

    $result = Invoke-NativeCommand -AllowFailure:$AllowFailure -Command {
        docker @dockerArgs
    }

    if (-not $AllowFailure -and $result.ExitCode -ne 0) {
        throw "AWS CLI (docker) failed: docker $($dockerArgs -join ' ')`n$($result.Output)"
    }

    if ($AllowFailure -and $result.ExitCode -ne 0) {
        return $null
    }

    return $result.Output
}

function Ensure-LogGroup {
    param([string]$Name)

    Invoke-AwsCli -Arguments @(
        "logs", "create-log-group",
        "--log-group-name", $Name
    ) -AllowFailure | Out-Null

    Invoke-AwsCli -Arguments @(
        "logs", "put-retention-policy",
        "--log-group-name", $Name,
        "--retention-in-days", "7"
    ) -AllowFailure | Out-Null

    Write-Host "Log group ready: $Name" -ForegroundColor Green
}

function Ensure-AlarmTopicArn {
    if (-not [string]::IsNullOrWhiteSpace($SnsTopicArn) -and $SnsTopicArn -ne "None") {
        Write-Host "Using existing SNS topic for alarms: $SnsTopicArn" -ForegroundColor Yellow
        return $SnsTopicArn
    }

    $topicArn = Invoke-AwsCli -Arguments @(
        "sns", "create-topic",
        "--name", $AlarmTopicName,
        "--query", "TopicArn",
        "--output", "text"
    ) -AllowFailure

    if ([string]::IsNullOrWhiteSpace($topicArn) -or $topicArn -eq "None") {
        $topicArn = Invoke-AwsCli -Arguments @(
            "sns", "list-topics",
            "--query", "Topics[?contains(TopicArn, '$AlarmTopicName')].TopicArn | [0]",
            "--output", "text"
        )
    }

    Write-Host "Alarm SNS topic: $topicArn" -ForegroundColor Green
    return $topicArn
}

function Ensure-MetricAlarm {
    param(
        [string]$AlarmName,
        [string]$MetricName,
        [string]$Namespace,
        [double]$Threshold,
        [string]$ComparisonOperator,
        [string]$AlarmDescription,
        [string]$AlarmTopicArn
    )

    Invoke-AwsCli -Arguments @(
        "cloudwatch", "put-metric-alarm",
        "--alarm-name", $AlarmName,
        "--alarm-description", $AlarmDescription,
        "--metric-name", $MetricName,
        "--namespace", $Namespace,
        "--statistic", "Sum",
        "--period", "60",
        "--evaluation-periods", "1",
        "--threshold", "$Threshold",
        "--comparison-operator", $ComparisonOperator,
        "--treat-missing-data", "notBreaching",
        "--alarm-actions", $AlarmTopicArn
    ) -AllowFailure | Out-Null

    Write-Host "Alarm ready: $AlarmName" -ForegroundColor Green
}

Write-Host "Provisioning LocalStack CloudWatch at $Endpoint..." -ForegroundColor Cyan

$logGroups = @(
    "$LogGroupPrefix/auth-api",
    "$LogGroupPrefix/transactions-api",
    "$LogGroupPrefix/transactions-relay",
    "$LogGroupPrefix/reporting-api",
    "$LogGroupPrefix/reporting-worker",
    "$LogGroupPrefix/web",
    "$LogGroupPrefix/alarms"
)

foreach ($logGroup in $logGroups) {
    Ensure-LogGroup -Name $logGroup
}

$alarmTopicArn = Ensure-AlarmTopicArn

Ensure-MetricAlarm -AlarmName "cashflow-auth-failures-high" `
    -MetricName "AuthFailures" `
    -Namespace "CashFlow/Security" `
    -Threshold 5 `
    -ComparisonOperator "GreaterThanThreshold" `
    -AlarmDescription "Authentication failures exceeded threshold" `
    -AlarmTopicArn $alarmTopicArn

Ensure-MetricAlarm -AlarmName "cashflow-api-5xx-high" `
    -MetricName "Http5xxCount" `
    -Namespace "CashFlow/Http" `
    -Threshold 10 `
    -ComparisonOperator "GreaterThanThreshold" `
    -AlarmDescription "HTTP 5xx responses exceeded threshold" `
    -AlarmTopicArn $alarmTopicArn

Ensure-MetricAlarm -AlarmName "cashflow-dlq-messages-visible" `
    -MetricName "ApproximateNumberOfMessagesVisible" `
    -Namespace "AWS/SQS" `
    -Threshold 1 `
    -ComparisonOperator "GreaterThanOrEqualToThreshold" `
    -AlarmDescription "Messages are visible in a dead-letter queue" `
    -AlarmTopicArn $alarmTopicArn

Ensure-MetricAlarm -AlarmName "cashflow-reporting-projection-failures" `
    -MetricName "ReportingProjectionFailures" `
    -Namespace "CashFlow/Reporting" `
    -Threshold 1 `
    -ComparisonOperator "GreaterThanOrEqualToThreshold" `
    -AlarmDescription "Reporting worker SQS projection failures detected" `
    -AlarmTopicArn $alarmTopicArn

Ensure-MetricAlarm -AlarmName "cashflow-reporting-export-failures" `
    -MetricName "ReportingExportFailures" `
    -Namespace "CashFlow/Reporting" `
    -Threshold 1 `
    -ComparisonOperator "GreaterThanOrEqualToThreshold" `
    -AlarmDescription "Report CSV/PDF export failures detected" `
    -AlarmTopicArn $alarmTopicArn

Ensure-MetricAlarm -AlarmName "cashflow-reporting-read-latency-high" `
    -MetricName "ReportingCachedReadP95Ms" `
    -Namespace "CashFlow/Reporting" `
    -Threshold 2000 `
    -ComparisonOperator "GreaterThanThreshold" `
    -AlarmDescription "Cached consolidated report read p95 exceeded 2 seconds" `
    -AlarmTopicArn $alarmTopicArn

Invoke-AwsCli -Arguments @(
    "cloudwatch", "put-metric-data",
    "--namespace", "CashFlow/Setup",
    "--metric-data", "MetricName=MonitoringBootstrap,Value=1,Unit=Count"
) -AllowFailure | Out-Null

$content = @"
CLOUDWATCH__LOGGROUPPREFIX=$LogGroupPrefix
CLOUDWATCH__ALARMTOPICARN=$alarmTopicArn
CLOUDWATCH__NAMESPACE=CashFlow
AWS__CLOUDWATCHSERVICEURL=$Endpoint
AWS__LOGSSERVICEURL=$Endpoint
"@

$directory = Split-Path -Parent $OutputFile
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputFile, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "LocalStack monitoring ready." -ForegroundColor Green
Write-Host "  Log groups: $($logGroups -join ', ')"
Write-Host "  Alarm topic: $alarmTopicArn"
Write-Host "  View in: https://app.localstack.cloud/resources/cloudwatch/groups"
