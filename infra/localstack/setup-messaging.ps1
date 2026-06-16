param(
    [string]$Endpoint = "http://localhost:4566",
    [string]$InternalEndpoint = "http://cashflow-localstack:4566",
    [string]$DockerNetwork = "localstack_default",
    [string]$Region = "us-east-1",
    [string]$TopicName = "cashflow-transaction-recorded",
    [string]$QueueName = "cashflow-transaction-recorded",
    [string]$DlqName = "cashflow-transaction-recorded-dlq",
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

if (-not $OutputFile) {
    $OutputFile = Join-Path $PSScriptRoot "generated\messaging.env"
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
        [switch]$AllowFailure,
        [string]$AttributeFile
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
        "--network", $DockerNetwork
    )

    if (-not [string]::IsNullOrWhiteSpace($AttributeFile)) {
        $dockerArgs += @("-v", "${AttributeFile}:/tmp/queue-attributes.json:ro")
    }

    $dockerArgs += @(
        "-e", "AWS_ACCESS_KEY_ID=test",
        "-e", "AWS_SECRET_ACCESS_KEY=test",
        "-e", "AWS_DEFAULT_REGION=$Region",
        "-e", "AWS_PAGER=",
        "amazon/aws-cli"
    ) + $Arguments + @("--endpoint-url", $InternalEndpoint, "--region", $Region)

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

function New-QueueAttributeFile {
    param([hashtable]$Attributes)

    $path = Join-Path $env:TEMP ("cashflow-sqs-attrs-{0}.json" -f [guid]::NewGuid().ToString("N"))
    $json = $Attributes | ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
    return $path
}

function Ensure-QueueUrl {
    param([string]$Name)

    $createdUrl = Invoke-AwsCli -Arguments @(
        "sqs", "create-queue",
        "--queue-name", $Name,
        "--query", "QueueUrl",
        "--output", "text"
    ) -AllowFailure

    if (-not [string]::IsNullOrWhiteSpace($createdUrl) -and $createdUrl -ne "None") {
        return $createdUrl
    }

    return Invoke-AwsCli -Arguments @(
        "sqs", "get-queue-url",
        "--queue-name", $Name,
        "--query", "QueueUrl",
        "--output", "text"
    )
}

function Set-QueueAttributesFromFile {
    param(
        [string]$QueueUrl,
        [hashtable]$Attributes,
        [switch]$AllowFailure
    )

    $attributeFile = New-QueueAttributeFile -Attributes $Attributes
    try {
        Invoke-AwsCli -Arguments @(
            "sqs", "set-queue-attributes",
            "--queue-url", $QueueUrl,
            "--attributes", "file:///tmp/queue-attributes.json"
        ) -AllowFailure:$AllowFailure -AttributeFile $attributeFile | Out-Null
    }
    finally {
        Remove-Item -Path $attributeFile -Force -ErrorAction SilentlyContinue
    }
}

function Get-LocalStackQueueUrl {
    param(
        [string]$ServiceEndpoint,
        [string]$QueueName
    )

    return "$($ServiceEndpoint.TrimEnd('/'))/000000000000/$QueueName"
}

Write-Host "Provisioning LocalStack SNS/SQS at $Endpoint..." -ForegroundColor Cyan

$dlqUrl = Ensure-QueueUrl -Name $DlqName

$dlqArn = Invoke-AwsCli -Arguments @(
    "sqs", "get-queue-attributes",
    "--queue-url", $dlqUrl,
    "--attribute-names", "QueueArn",
    "--query", "Attributes.QueueArn",
    "--output", "text"
)

$queueUrl = Ensure-QueueUrl -Name $QueueName

Set-QueueAttributesFromFile -QueueUrl $queueUrl -Attributes @{
    VisibilityTimeout = "30"
    RedrivePolicy = "{`"deadLetterTargetArn`":`"$dlqArn`",`"maxReceiveCount`":`"5`"}"
}

$queueArn = Invoke-AwsCli -Arguments @(
    "sqs", "get-queue-attributes",
    "--queue-url", $queueUrl,
    "--attribute-names", "QueueArn",
    "--query", "Attributes.QueueArn",
    "--output", "text"
)

$topicArn = Invoke-AwsCli -Arguments @(
    "sns", "create-topic",
    "--name", $TopicName,
    "--query", "TopicArn",
    "--output", "text"
) -AllowFailure

if ([string]::IsNullOrWhiteSpace($topicArn) -or $topicArn -eq "None") {
    $topicArn = Invoke-AwsCli -Arguments @(
        "sns", "list-topics",
        "--query", "Topics[?contains(TopicArn, '$TopicName')].TopicArn | [0]",
        "--output", "text"
    )
}

Invoke-AwsCli -Arguments @(
    "sns", "subscribe",
    "--topic-arn", $topicArn,
    "--protocol", "sqs",
    "--notification-endpoint", $queueArn,
    "--attributes", "RawMessageDelivery=true"
) -AllowFailure | Out-Null

$policy = "{`"Version`":`"2012-10-17`",`"Statement`":[{`"Effect`":`"Allow`",`"Principal`":{`"Service`":`"sns.amazonaws.com`"},`"Action`":`"sqs:SendMessage`",`"Resource`":`"$queueArn`",`"Condition`":{`"ArnEquals`":{`"aws:SourceArn`":`"$topicArn`"}}}]}"
Set-QueueAttributesFromFile -QueueUrl $queueUrl -Attributes @{
    Policy = $policy
} -AllowFailure

$connectionString = "Server=127.0.0.1,1433;Database=reporting-db;User Id=sa;Password=CashFlow@Dev123!;TrustServerCertificate=True;Encrypt=False"
$reportingConnectionString = $connectionString
$eventStoreConnection = "esdb://127.0.0.1:2113?tls=false"
$clientQueueUrl = Get-LocalStackQueueUrl -ServiceEndpoint $Endpoint -QueueName $QueueName
$clientDlqUrl = Get-LocalStackQueueUrl -ServiceEndpoint $Endpoint -QueueName $DlqName

$content = @"
MESSAGING__SNSTOPICARN=$topicArn
MESSAGING__SQSQUEUEURL=$clientQueueUrl
MESSAGING__DLQQUEUEURL=$clientDlqUrl
AWS__SERVICEURL=$Endpoint
AWS__REGION=$Region
ConnectionStrings__reporting-db=$reportingConnectionString
EventStore__ConnectionString=$eventStoreConnection
EventStore__HttpEndpoint=http://127.0.0.1:2113
"@

$directory = Split-Path -Parent $OutputFile
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputFile, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "LocalStack messaging ready." -ForegroundColor Green
Write-Host "  SNS Topic: $topicArn"
Write-Host "  SQS Queue: $clientQueueUrl"
Write-Host "  SQS DLQ:   $clientDlqUrl"
