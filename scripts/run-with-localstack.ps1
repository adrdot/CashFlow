$ErrorActionPreference = "Stop"

# Cognito Local + Aspire only. For Cognito + LocalStack + Aspire use scripts/run-full-local.ps1.

$repoRoot = Split-Path -Parent $PSScriptRoot
$cognitoRoot = Join-Path $repoRoot "infra\cognito-local"
$generatedEnv = Join-Path $cognitoRoot "generated\cognito.env"
$setupScript = Join-Path $cognitoRoot "setup-cognito.ps1"
$stopScript = Join-Path $cognitoRoot "stop-cognito.ps1"
$appHost = Join-Path $repoRoot "AspireApp1.AppHost"

$script:cleanupDone = $false

function Import-EnvFile {
    param([string]$Path)

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

    # Aspire resolve Parameters__* antes do default do AddParameter (dashboard + injeção).
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

function Invoke-CognitoLocalCleanup {
    if ($script:cleanupDone) {
        return
    }

    $script:cleanupDone = $true
    Write-Host ""
    Write-Host "Shutting down Cognito Local..." -ForegroundColor Cyan
    & $stopScript
}

Register-EngineEvent -SourceIdentifier PowerShell.Exiting -MessageData $cognitoRoot -Action {
    $root = $Event.MessageData
    Push-Location $root
    try {
        docker compose down --remove-orphans 2>$null
    }
    finally {
        Pop-Location
    }
} | Out-Null

trap {
    Invoke-CognitoLocalCleanup
    break
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required. Install Docker Desktop and retry."
}

Write-Host "Starting Cognito Local..." -ForegroundColor Cyan
Push-Location $cognitoRoot
try {
    docker compose down --remove-orphans 2>$null
    docker compose up -d --wait --force-recreate
}
finally {
    Pop-Location
}

& $setupScript -OutputFile $generatedEnv
Import-EnvFile -Path $generatedEnv
Set-CashFlowCognitoEnvironment

Write-Host ""
Write-Host "Cognito Local is ready." -ForegroundColor Green
Write-Host "  Mode:     CognitoLocal (CASHFLOW_COGNITO_ENABLED=true)"
Write-Host "  Endpoint: $($env:COGNITO_SERVICE_URL)"
Write-Host "  Pool:     $($env:COGNITO_USER_POOL_ID)"
Write-Host "  Client:   $($env:COGNITO_CLIENT_ID)"
Write-Host "  User:     $($env:COGNITO_USERNAME) / $($env:COGNITO_PASSWORD)"
Write-Host "  MFA code: $($env:COGNITO_MFA_CODE)"
Write-Host ""
Write-Host "  F5: use launch profile 'https-cognito' after running scripts/start-cognito-local.ps1"
Write-Host "  Default F5 uses in-memory auth with MFA code 123456."
Write-Host "  Press Ctrl+C to stop Aspire and remove the Cognito container."
Write-Host ""

Write-Host "Starting Aspire AppHost..." -ForegroundColor Cyan

try {
    dotnet run --project $appHost
}
finally {
    Invoke-CognitoLocalCleanup
}
