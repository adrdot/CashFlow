$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$localstackRoot = Join-Path $repoRoot "infra\localstack"
$setupScript = Join-Path $localstackRoot "setup-secrets.ps1"
$generatedEnv = Join-Path $localstackRoot "generated\localstack.env"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required. Install Docker Desktop and retry."
}

Write-Host "Starting LocalStack (Secrets + KMS + SNS/SQS + CloudWatch + API Gateway)..." -ForegroundColor Cyan
Push-Location $localstackRoot
try {
    docker compose down --remove-orphans 2>$null
    docker compose up -d --wait --force-recreate
}
finally {
    Pop-Location
}

& $setupScript -OutputFile $generatedEnv

Write-Host ""
Write-Host "LocalStack is ready for Secrets Manager." -ForegroundColor Green
Write-Host "  Endpoint: http://localhost:4566"
Write-Host "  Env file: $generatedEnv"
Write-Host ""
Write-Host "Set SecretsManager__ServiceUrl and Kms__ServiceUrl to http://localhost:4566, or use scripts/run-full-local.ps1 for everything."
