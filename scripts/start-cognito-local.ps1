$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cognitoRoot = Join-Path $repoRoot "infra\cognito-local"
$generatedEnv = Join-Path $cognitoRoot "generated\cognito.env"
$setupScript = Join-Path $cognitoRoot "setup-cognito.ps1"

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

Write-Host ""
Write-Host "Cognito Local is ready for F5 (profile: https-cognito)." -ForegroundColor Green
Write-Host "  Env file: $generatedEnv"
Write-Host "  Run Aspire with profile 'https-cognito' or use scripts/run-full-local.ps1 for full local infra."
