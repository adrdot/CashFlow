param(
    [switch]$SkipObservability,
    [switch]$ObservabilityHttps
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cognitoRoot = Join-Path $repoRoot "infra\cognito-local"
$localstackRoot = Join-Path $repoRoot "infra\localstack"
$transactionsStackRoot = Join-Path $repoRoot "infra\transactions-stack"
$observabilityRoot = Join-Path $repoRoot "infra\observability"
$observabilityStartScript = Join-Path $observabilityRoot "start-observability.ps1"
$cognitoEnvFile = Join-Path $cognitoRoot "generated\cognito.env"
$localstackEnvFile = Join-Path $localstackRoot "generated\localstack.env"
$messagingEnvFile = Join-Path $localstackRoot "generated\messaging.env"
$monitoringEnvFile = Join-Path $localstackRoot "generated\monitoring.env"
$apiGatewayEnvFile = Join-Path $localstackRoot "generated\api-gateway.env"
$transactionsStackEnvFile = Join-Path $transactionsStackRoot "generated\transactions-stack.env"
$cognitoSetupScript = Join-Path $cognitoRoot "setup-cognito.ps1"
$localstackSetupScript = Join-Path $localstackRoot "setup-secrets.ps1"
$messagingSetupScript = Join-Path $localstackRoot "setup-messaging.ps1"
$monitoringSetupScript = Join-Path $localstackRoot "setup-monitoring.ps1"
$apiGatewaySetupScript = Join-Path $localstackRoot "setup-api-gateway.ps1"
$transactionsStackSetupScript = Join-Path $transactionsStackRoot "setup-transactions-stack.ps1"
$appHost = Join-Path $repoRoot "src\Aspire.CashFlow.AppHost"
$localstackEndpoint = "http://localhost:4566"

$localstackContainer = "cashflow-localstack"
$cognitoContainer = "cashflow-cognito-local"
$sqlServerContainer = "cashflow-sqlserver"
$eventStoreContainer = "cashflow-eventstore"
$prometheusContainer = "cashflow-prometheus"
$grafanaContainer = "cashflow-grafana"

$script:cleanupDone = $false

function Invoke-NativeDockerCommand {
    param(
        [scriptblock]$Command,
        [switch]$ThrowOnFailure,
        [switch]$SuppressOutput
    )

    $previousErrorAction = $ErrorActionPreference
    $previousNative = $null
    $nativePreference = Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue
    if ($null -ne $nativePreference) {
        $previousNative = $nativePreference.Value
        $PSNativeCommandUseErrorActionPreference = $false
    }

    $ErrorActionPreference = "Continue"
    try {
        if ($SuppressOutput) {
            & $Command 2>$null | Out-Null
        }
        else {
            & $Command
        }

        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
        if ($null -ne $previousNative) {
            $PSNativeCommandUseErrorActionPreference = $previousNative
        }
    }

    if ($ThrowOnFailure -and $exitCode -ne 0) {
        throw "Docker command failed (exit $exitCode)."
    }
}

function Stop-DockerService {
    param(
        [string]$ComposeRoot,
        [string]$ContainerName,
        [string]$Label
    )

    Write-Host "Stopping $Label..." -ForegroundColor Cyan

    Push-Location $ComposeRoot
    try {
        Invoke-NativeDockerCommand -SuppressOutput { docker compose down --remove-orphans }
    }
    finally {
        Pop-Location
    }

    Invoke-NativeDockerCommand -SuppressOutput { docker rm -f $ContainerName }
}

function Import-EnvFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return
    }

    Get-Content $Path | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -match '^\s*$') {
            return
        }

        $parts = $_ -split '=', 2
        if ($parts.Count -eq 2) {
            Set-Item -Path "Env:$($parts[0].Trim())" -Value $parts[1].Trim()
        }
    }
}

function Set-CashFlowCognitoEnvironment {
    Set-Item -Path "Env:CASHFLOW_COGNITO_ENABLED" -Value "true"
    Set-Item -Path "Env:CASHFLOW_COGNITO_SERVICE_URL" -Value $env:COGNITO_SERVICE_URL
    Set-Item -Path "Env:CASHFLOW_COGNITO_USER_POOL_ID" -Value $env:COGNITO_USER_POOL_ID
    Set-Item -Path "Env:CASHFLOW_COGNITO_CLIENT_ID" -Value $env:COGNITO_CLIENT_ID
    Set-Item -Path "Env:CASHFLOW_COGNITO_REGION" -Value $env:COGNITO_REGION
    Set-Item -Path "Env:CASHFLOW_COGNITO_AUTHENTICATION_SOURCE" -Value "CognitoLocal"

    Set-Item -Path "Env:Parameters__cognito-enabled" -Value "true"
    Set-Item -Path "Env:Parameters__cognito-region" -Value $env:COGNITO_REGION
    Set-Item -Path "Env:Parameters__cognito-service-url" -Value $env:COGNITO_SERVICE_URL
    Set-Item -Path "Env:Parameters__cognito-user-pool-id" -Value $env:COGNITO_USER_POOL_ID
    Set-Item -Path "Env:Parameters__cognito-client-id" -Value $env:COGNITO_CLIENT_ID
    Set-Item -Path "Env:Parameters__cognito-authentication-source" -Value "CognitoLocal"

    $mfaCode = if ([string]::IsNullOrWhiteSpace($env:COGNITO_MFA_CODE)) { "123456" } else { $env:COGNITO_MFA_CODE }
    Set-Item -Path "Env:COGNITO_MFA_CODE" -Value $mfaCode
    Set-Item -Path "Env:DemoAccount__Email" -Value $env:COGNITO_USERNAME
    Set-Item -Path "Env:DemoAccount__Password" -Value $env:COGNITO_PASSWORD
    Set-Item -Path "Env:DemoAccount__MfaCode" -Value $mfaCode
    Set-Item -Path "Env:DemoAccount__Description" -Value "Cognito Local demo account"
}

function Set-CashFlowMessagingEnvironment {
    Set-Item -Path "Env:Messaging__SnsTopicArn" -Value $env:MESSAGING__SNSTOPICARN
    Set-Item -Path "Env:Messaging__SqsQueueUrl" -Value $env:MESSAGING__SQSQUEUEURL
    Set-Item -Path "Env:Messaging__DlqQueueUrl" -Value $env:MESSAGING__DLQQUEUEURL
    Set-Item -Path "Env:AWS__ServiceURL" -Value $env:AWS__SERVICEURL
    $awsRegion = if ([string]::IsNullOrWhiteSpace($env:AWS__REGION)) { "us-east-1" } else { $env:AWS__REGION }
    Set-Item -Path "Env:AWS__Region" -Value $awsRegion
    Set-Item -Path "Env:ConnectionStrings__reporting-db" -Value ${env:ConnectionStrings__reporting-db}
    Set-Item -Path "Env:EventStore__ConnectionString" -Value $env:EventStore__ConnectionString
    $eventStoreHttpEndpoint = if ([string]::IsNullOrWhiteSpace($env:EventStore__HttpEndpoint)) { "http://127.0.0.1:2113" } else { $env:EventStore__HttpEndpoint }
    Set-Item -Path "Env:EventStore__HttpEndpoint" -Value $eventStoreHttpEndpoint
}

function Set-CashFlowTransactionsStackEnvironment {
    Import-EnvFile -Path $transactionsStackEnvFile
    Set-Item -Path "Env:EventStore__ConnectionString" -Value $env:EventStore__ConnectionString
    Set-Item -Path "Env:EventStore__HttpEndpoint" -Value $env:EventStore__HttpEndpoint
}

function Set-CashFlowMonitoringEnvironment {
    Set-Item -Path "Env:CloudWatch__Enabled" -Value "true"
    Set-Item -Path "Env:CloudWatch__LogGroupPrefix" -Value $env:CLOUDWATCH__LOGGROUPPREFIX
    Set-Item -Path "Env:CloudWatch__AlarmTopicArn" -Value $env:CLOUDWATCH__ALARMTOPICARN
    Set-Item -Path "Env:CloudWatch__Namespace" -Value $env:CLOUDWATCH__NAMESPACE
    Set-Item -Path "Env:CloudWatch__ServiceUrl" -Value $env:AWS__LOGSSERVICEURL
    Set-Item -Path "Env:CloudWatch__Region" -Value "us-east-1"
    Set-Item -Path "Env:AWS__CloudWatchServiceUrl" -Value $env:AWS__CLOUDWATCHSERVICEURL
    Set-Item -Path "Env:AWS__LogsServiceUrl" -Value $env:AWS__LOGSSERVICEURL
}

function Set-CashFlowApiGatewayEnvironment {
    Set-Item -Path "Env:ApiGateway__Enabled" -Value $env:APIGATEWAY__ENABLED
    Set-Item -Path "Env:ApiGateway__Skipped" -Value $env:APIGATEWAY__SKIPPED
    Set-Item -Path "Env:ApiGateway__ApiId" -Value $env:APIGATEWAY__APIID
    Set-Item -Path "Env:ApiGateway__InvokeUrl" -Value $env:APIGATEWAY__INVOKEURL
    Set-Item -Path "Env:ApiGateway__AuthBasePath" -Value $env:APIGATEWAY__AUTHBASEPATH
    Set-Item -Path "Env:ApiGateway__TransactionsBasePath" -Value $env:APIGATEWAY__TRANSACTIONSBASEPATH
    Set-Item -Path "Env:ApiGateway__ReportingBasePath" -Value $env:APIGATEWAY__REPORTINGBASEPATH
}

function Set-CashFlowLocalStackEnvironment {
    param([string]$Endpoint)

    Set-Item -Path "Env:CASHFLOW_LOCALSTACK_ENABLED" -Value "true"
    Set-Item -Path "Env:LOCALSTACK_SERVICE_URL" -Value $Endpoint
    Set-Item -Path "Env:SECRETS_MANAGER_SERVICE_URL" -Value $Endpoint
    Set-Item -Path "Env:KMS_SERVICE_URL" -Value $Endpoint
    Set-Item -Path "Env:Parameters__secrets-service-url" -Value $Endpoint
    Set-Item -Path "Env:Parameters__kms-service-url" -Value $Endpoint
    Set-Item -Path "Env:SecretsManager__PreferConfiguration" -Value "false"
}

function Start-DockerCompose {
    param(
        [string]$Root,
        [string]$Label
    )

    Write-Host "Starting $Label..." -ForegroundColor Cyan
    Push-Location $Root
    try {
        Invoke-NativeDockerCommand -SuppressOutput { docker compose down --remove-orphans }
        Invoke-NativeDockerCommand -ThrowOnFailure { docker compose up -d --wait --force-recreate }
    }
    finally {
        Pop-Location
    }
}

function Invoke-FullLocalCleanup {
    if ($script:cleanupDone) {
        return
    }

    $script:cleanupDone = $true
    Write-Host ""
    Write-Host "Shutting down local infrastructure..." -ForegroundColor Cyan

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        Stop-DockerService -ComposeRoot $observabilityRoot -ContainerName $prometheusContainer -Label "Observability stack"
        Stop-DockerService -ComposeRoot $transactionsStackRoot -ContainerName $eventStoreContainer -Label "Transactions Stack"
        Stop-DockerService -ComposeRoot $localstackRoot -ContainerName $localstackContainer -Label "LocalStack"
        Stop-DockerService -ComposeRoot $cognitoRoot -ContainerName $cognitoContainer -Label "Cognito Local"
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }
}

Register-EngineEvent -SourceIdentifier PowerShell.Exiting -MessageData @{
    CognitoRoot = $cognitoRoot
    LocalstackRoot = $localstackRoot
    TransactionsStackRoot = $transactionsStackRoot
    ObservabilityRoot = $observabilityRoot
    CognitoContainer = $cognitoContainer
    LocalstackContainer = $localstackContainer
    SqlServerContainer = $sqlServerContainer
    EventStoreContainer = $eventStoreContainer
    PrometheusContainer = $prometheusContainer
    GrafanaContainer = $grafanaContainer
} -Action {
    $data = $Event.MessageData
    $previousErrorAction = $ErrorActionPreference
    $previousNative = $null
    $nativePreference = Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue
    if ($null -ne $nativePreference) {
        $previousNative = $nativePreference.Value
        $PSNativeCommandUseErrorActionPreference = $false
    }

    $ErrorActionPreference = "Continue"
    try {
        Push-Location $data.ObservabilityRoot
        try {
            docker compose down --remove-orphans 2>$null | Out-Null
        }
        finally {
            Pop-Location
        }

        docker rm -f $data.PrometheusContainer 2>$null | Out-Null
        docker rm -f $data.GrafanaContainer 2>$null | Out-Null

        Push-Location $data.TransactionsStackRoot
        try {
            docker compose down --remove-orphans 2>$null | Out-Null
        }
        finally {
            Pop-Location
        }

        docker rm -f $data.SqlServerContainer 2>$null | Out-Null
        docker rm -f $data.EventStoreContainer 2>$null | Out-Null

        Push-Location $data.LocalstackRoot
        try {
            docker compose down --remove-orphans 2>$null | Out-Null
        }
        finally {
            Pop-Location
        }

        docker rm -f $data.LocalstackContainer 2>$null | Out-Null

        Push-Location $data.CognitoRoot
        try {
            docker compose down --remove-orphans 2>$null | Out-Null
        }
        finally {
            Pop-Location
        }

        docker rm -f $data.CognitoContainer 2>$null | Out-Null
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
        if ($null -ne $previousNative) {
            $PSNativeCommandUseErrorActionPreference = $previousNative
        }
    }
} | Out-Null

function Register-CleanupOnCancelKey {
    if ($null -eq [Console]::CancelKeyPress) {
        return $false
    }

    $script:cancelKeyHandler = {
        param([object]$Sender, [System.ConsoleCancelEventArgs]$EventArgs)
        $EventArgs.Cancel = $true
        Invoke-FullLocalCleanup
    }

    [Console]::CancelKeyPress.Add($script:cancelKeyHandler)
    return $true
}

function Unregister-CleanupOnCancelKey {
    if ($null -eq [Console]::CancelKeyPress -or $null -eq $script:cancelKeyHandler) {
        return
    }

    [Console]::CancelKeyPress.Remove($script:cancelKeyHandler)
    $script:cancelKeyHandler = $null
}

$script:cancelKeyHandlerRegistered = Register-CleanupOnCancelKey

trap {
    Invoke-FullLocalCleanup
    break
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required. Install Docker Desktop and retry."
}

Start-DockerCompose -Root $localstackRoot -Label "LocalStack (Secrets + KMS + SNS/SQS + CloudWatch)"
& $localstackSetupScript -OutputFile $localstackEnvFile
Import-EnvFile -Path $localstackEnvFile
Set-CashFlowLocalStackEnvironment -Endpoint $localstackEndpoint
& $messagingSetupScript -OutputFile $messagingEnvFile
Import-EnvFile -Path $messagingEnvFile
Set-CashFlowMessagingEnvironment
& $monitoringSetupScript -OutputFile $monitoringEnvFile -SnsTopicArn $env:MESSAGING__SNSTOPICARN
Import-EnvFile -Path $monitoringEnvFile
Set-CashFlowMonitoringEnvironment
& $apiGatewaySetupScript -OutputFile $apiGatewayEnvFile
Import-EnvFile -Path $apiGatewayEnvFile
Set-CashFlowApiGatewayEnvironment

if ([string]::IsNullOrWhiteSpace($env:CASHFLOW_API_REPLICAS)) {
    Set-Item -Path Env:CASHFLOW_API_REPLICAS -Value "1"
}
if ([string]::IsNullOrWhiteSpace($env:CASHFLOW_RELAY_REPLICAS)) {
    Set-Item -Path Env:CASHFLOW_RELAY_REPLICAS -Value "3"
}
if ([string]::IsNullOrWhiteSpace($env:CASHFLOW_REPORTING_WORKER_REPLICAS)) {
    Set-Item -Path Env:CASHFLOW_REPORTING_WORKER_REPLICAS -Value "3"
}

Start-DockerCompose -Root $transactionsStackRoot -Label "Transactions Stack (SQL Server + EventStoreDB)"
& $transactionsStackSetupScript -OutputFile $transactionsStackEnvFile
Set-CashFlowTransactionsStackEnvironment

Start-DockerCompose -Root $cognitoRoot -Label "Cognito Local"
& $cognitoSetupScript -OutputFile $cognitoEnvFile
Import-EnvFile -Path $cognitoEnvFile
Set-CashFlowCognitoEnvironment

if (-not $SkipObservability) {
    . $observabilityStartScript
    $aspireOtlpEndpoint = $env:ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL
    if ([string]::IsNullOrWhiteSpace($aspireOtlpEndpoint)) {
        $aspireOtlpEndpoint = "https://host.docker.internal:21119"
    }
    else {
        $aspireOtlpEndpoint = $aspireOtlpEndpoint -replace '^https?://localhost:', 'https://host.docker.internal:'
        $aspireOtlpEndpoint = $aspireOtlpEndpoint -replace '^https?://127\.0\.0\.1:', 'https://host.docker.internal:'
    }

    if ($ObservabilityHttps) {
        Start-TransactionsObservabilityStack `
            -MetricsTarget "host.docker.internal:7093" `
            -ReportingMetricsTarget "host.docker.internal:7090" `
            -MetricsScheme https `
            -ReportingMetricsScheme https `
            -AspireOtlpEndpoint $aspireOtlpEndpoint `
            -InsecureSkipTlsVerify
    }
    else {
        Start-TransactionsObservabilityStack `
            -MetricsTarget "host.docker.internal:5100" `
            -ReportingMetricsTarget "host.docker.internal:5292" `
            -MetricsScheme http `
            -ReportingMetricsScheme http `
            -AspireOtlpEndpoint $aspireOtlpEndpoint
    }

    $env:CASHFLOW_OTEL_COLLECTOR_ENDPOINT = "http://127.0.0.1:4318"
}

Write-Host ""
Write-Host "Local infrastructure is ready." -ForegroundColor Green
Write-Host ""
Write-Host "  LocalStack:  $localstackEndpoint (Secrets + KMS + SNS/SQS + CloudWatch)"
Write-Host "  CloudWatch:  $($env:CloudWatch__LogGroupPrefix) (alarms -> $($env:CloudWatch__AlarmTopicArn))"
if ($env:APIGATEWAY__SKIPPED -eq "true") {
    Write-Host "  API Gateway: skipped (LocalStack Pro required; use Aspire dashboard for API URLs)" -ForegroundColor Yellow
}
else {
    Write-Host "  API Gateway: $($env:ApiGateway__InvokeUrl)"
}
Write-Host "  SQL Server:  127.0.0.1:1433 (reporting-db)"
Write-Host "  EventStore:  http://127.0.0.1:2113"
Write-Host "  SNS Topic:   $($env:Messaging__SnsTopicArn)"
Write-Host "  SQS Queue:   $($env:Messaging__SqsQueueUrl)"
Write-Host "  Cognito:     $($env:COGNITO_SERVICE_URL)"
Write-Host "  Pool:        $($env:COGNITO_USER_POOL_ID)"
Write-Host "  Client:      $($env:COGNITO_CLIENT_ID)"
Write-Host "  User:        $($env:COGNITO_USERNAME) / $($env:COGNITO_PASSWORD)"
Write-Host "  MFA code:    $($env:COGNITO_MFA_CODE)"
if (-not $SkipObservability) {
    Write-Host "  Prometheus:  http://localhost:9090"
    Write-Host "  Grafana:     http://localhost:3000 (admin / admin) -> Messaging Pipeline dashboard"
    Write-Host "  OTEL bridge: http://127.0.0.1:4318 -> Prometheus :8889 (relay + reporting-worker metrics)"
    Write-Host "  YACE:        http://localhost:5000/metrics (SQS depth via LocalStack CloudWatch)"
}
Write-Host ""
Write-Host "  Press Ctrl+C to stop Aspire and remove local containers."
Write-Host ""

$apiReplicasLabel = if ([string]::IsNullOrWhiteSpace($env:CASHFLOW_API_REPLICAS)) { "1" } else { $env:CASHFLOW_API_REPLICAS }
$relayReplicasLabel = if ([string]::IsNullOrWhiteSpace($env:CASHFLOW_RELAY_REPLICAS)) { "3" } else { $env:CASHFLOW_RELAY_REPLICAS }
$reportingWorkerReplicasLabel = if ([string]::IsNullOrWhiteSpace($env:CASHFLOW_REPORTING_WORKER_REPLICAS)) { "3" } else { $env:CASHFLOW_REPORTING_WORKER_REPLICAS }
Write-Host "Starting Aspire AppHost (transactions-api x$apiReplicasLabel, transactions-relay x$relayReplicasLabel, reporting-api x$apiReplicasLabel, reporting-worker x$reportingWorkerReplicasLabel)..." -ForegroundColor Cyan

try {
    dotnet run --project $appHost
}
finally {
    Unregister-CleanupOnCancelKey
    Invoke-FullLocalCleanup
}
