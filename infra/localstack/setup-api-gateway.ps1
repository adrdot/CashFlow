param(
    [string]$Endpoint = "http://localhost:4566",
    [string]$InternalEndpoint = "http://cashflow-localstack:4566",
    [string]$DockerNetwork = "localstack_default",
    [string]$Region = "us-east-1",
    [string]$ApiName = "cashflow-local",
    [string]$HostGateway = "host.docker.internal",
    [int]$AuthApiPort = 5154,
    [int]$TransactionsApiPort = 5100,
    [int]$ReportingApiPort = 5292,
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

if (-not $OutputFile) {
    $OutputFile = Join-Path $PSScriptRoot "generated\api-gateway.env"
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
            return $result
        }

        return $result
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

    return $result
}

function Test-LocalStackApiGatewayAvailable {
    $probe = Invoke-AwsCli -Arguments @(
        "apigatewayv2", "get-apis",
        "--max-items", "1"
    ) -AllowFailure

    if ($null -eq $probe) {
        return $false
    }

    if ($probe.Output -match "not included within your LocalStack license|requires LocalStack Pro|not yet implemented") {
        return $false
    }

    return $probe.ExitCode -eq 0
}

function Write-SkippedApiGatewayEnv {
    param([string]$Reason)

    $content = @"
APIGATEWAY__ENABLED=false
APIGATEWAY__SKIPPED=true
APIGATEWAY__SKIPREASON=$Reason
"@

    $directory = Split-Path -Parent $OutputFile
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    [System.IO.File]::WriteAllText($OutputFile, $content, [System.Text.UTF8Encoding]::new($false))
}

function Remove-ExistingApi {
    param([string]$Name)

    $existingApiId = (Invoke-AwsCli -Arguments @(
        "apigatewayv2", "get-apis",
        "--query", "Items[?Name=='$Name'].ApiId | [0]",
        "--output", "text"
    ) -AllowFailure).Output

    if ([string]::IsNullOrWhiteSpace($existingApiId) -or $existingApiId -eq "None") {
        return
    }

    Invoke-AwsCli -Arguments @(
        "apigatewayv2", "delete-api",
        "--api-id", $existingApiId
    ) -AllowFailure | Out-Null

    Write-Host "Removed existing API Gateway: $Name ($existingApiId)" -ForegroundColor Yellow
}

function New-HttpProxyRoute {
    param(
        [string]$ApiId,
        [string]$RouteKey,
        [string]$IntegrationUri
    )

    $integrationId = (Invoke-AwsCli -Arguments @(
        "apigatewayv2", "create-integration",
        "--api-id", $ApiId,
        "--integration-type", "HTTP_PROXY",
        "--integration-method", "ANY",
        "--integration-uri", $IntegrationUri,
        "--payload-format-version", "1.0",
        "--query", "IntegrationId",
        "--output", "text"
    )).Output

    Invoke-AwsCli -Arguments @(
        "apigatewayv2", "create-route",
        "--api-id", $ApiId,
        "--route-key", $RouteKey,
        "--target", "integrations/$integrationId"
    ) -AllowFailure | Out-Null

    Write-Host "Route ready: $RouteKey -> $IntegrationUri" -ForegroundColor Green
}

Write-Host "Checking LocalStack API Gateway availability at $Endpoint..." -ForegroundColor Cyan

if (-not (Test-LocalStackApiGatewayAvailable)) {
    $reason = "API Gateway v2 requires LocalStack Pro. Local dev uses Aspire service discovery instead."
    Write-Host ""
    Write-Host "API Gateway skipped (Community edition)." -ForegroundColor Yellow
    Write-Host "  $reason" -ForegroundColor Yellow
    Write-Host "  Auth API:         http://localhost:$AuthApiPort" -ForegroundColor DarkYellow
    Write-Host "  Transactions API: http://localhost:$TransactionsApiPort" -ForegroundColor DarkYellow
    Write-Host "  Reporting API:    http://localhost:$ReportingApiPort" -ForegroundColor DarkYellow
    Write-Host ""
    Write-SkippedApiGatewayEnv -Reason $reason
    return
}

Write-Host "Provisioning LocalStack API Gateway at $Endpoint..." -ForegroundColor Cyan

Remove-ExistingApi -Name $ApiName

$createApiResult = Invoke-AwsCli -Arguments @(
    "apigatewayv2", "create-api",
    "--name", $ApiName,
    "--protocol-type", "HTTP",
    "--query", "ApiId",
    "--output", "text"
) -AllowFailure

$apiId = $createApiResult.Output
if ($createApiResult.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($apiId) -or $apiId -eq "None") {
    $reason = "Unable to create API Gateway in LocalStack."
    Write-Host "API Gateway setup failed. Continuing without gateway." -ForegroundColor Yellow
    Write-Host "  $($createApiResult.Output)" -ForegroundColor DarkYellow
    Write-SkippedApiGatewayEnv -Reason $reason
    return
}

$routes = @(
    @{
        RouteKey = "ANY /auth/{proxy+}"
        IntegrationUri = "http://${HostGateway}:${AuthApiPort}/{proxy}"
    },
    @{
        RouteKey = "ANY /auth"
        IntegrationUri = "http://${HostGateway}:${AuthApiPort}/"
    },
    @{
        RouteKey = "ANY /transactions/{proxy+}"
        IntegrationUri = "http://${HostGateway}:${TransactionsApiPort}/{proxy}"
    },
    @{
        RouteKey = "ANY /transactions"
        IntegrationUri = "http://${HostGateway}:${TransactionsApiPort}/"
    },
    @{
        RouteKey = "ANY /reporting/{proxy+}"
        IntegrationUri = "http://${HostGateway}:${ReportingApiPort}/{proxy}"
    },
    @{
        RouteKey = "ANY /reporting"
        IntegrationUri = "http://${HostGateway}:${ReportingApiPort}/"
    }
)

foreach ($route in $routes) {
    New-HttpProxyRoute -ApiId $apiId -RouteKey $route.RouteKey -IntegrationUri $route.IntegrationUri
}

Invoke-AwsCli -Arguments @(
    "apigatewayv2", "create-stage",
    "--api-id", $apiId,
    "--stage-name", "`$default",
    "--auto-deploy"
) -AllowFailure | Out-Null

$invokeUrl = "http://${apiId}.execute-api.localhost.localstack.cloud:4566"
$legacyInvokeUrl = "$($Endpoint.TrimEnd('/'))/_aws/execute-api/$apiId/`$default"

$content = @"
APIGATEWAY__ENABLED=true
APIGATEWAY__SKIPPED=false
APIGATEWAY__APIID=$apiId
APIGATEWAY__INVOKEURL=$invokeUrl
APIGATEWAY__LEGACYINVOKEURL=$legacyInvokeUrl
APIGATEWAY__AUTHBASEPATH=$invokeUrl/auth
APIGATEWAY__TRANSACTIONSBASEPATH=$invokeUrl/transactions
APIGATEWAY__REPORTINGBASEPATH=$invokeUrl/reporting
"@

$directory = Split-Path -Parent $OutputFile
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputFile, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "LocalStack API Gateway ready." -ForegroundColor Green
Write-Host "  API ID:      $apiId"
Write-Host "  Invoke URL:  $invokeUrl"
Write-Host "  Auth:        $invokeUrl/auth"
Write-Host "  Transactions:$invokeUrl/transactions"
Write-Host "  Reporting:   $invokeUrl/reporting"
Write-Host "  View in:     https://app.localstack.cloud/resources/apigateway"
