$ErrorActionPreference = "Continue"

$repoRoot = Split-Path -Parent $PSScriptRoot
$localstackRoot = Join-Path $repoRoot "infra\localstack"
$cognitoRoot = Join-Path $repoRoot "infra\cognito-local"
$localstackContainer = "cashflow-localstack"
$cognitoContainer = "cashflow-cognito-local"

function Stop-DockerService {
    param(
        [string]$ComposeRoot,
        [string]$ContainerName,
        [string]$Label
    )

    Write-Host "Stopping $Label..." -ForegroundColor Cyan

    Push-Location $ComposeRoot
    try {
        $previousNative = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
        try {
            docker compose down --remove-orphans 2>$null | Out-Null
        }
        finally {
            $PSNativeCommandUseErrorActionPreference = $previousNative
        }
    }
    finally {
        Pop-Location
    }

    docker rm -f $ContainerName 2>$null | Out-Null
}

Stop-DockerService -ComposeRoot $localstackRoot -ContainerName $localstackContainer -Label "LocalStack"
Stop-DockerService -ComposeRoot $cognitoRoot -ContainerName $cognitoContainer -Label "Cognito Local"

Write-Host "Local infrastructure stopped." -ForegroundColor Green
