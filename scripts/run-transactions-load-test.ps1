param(
    [string]$TransactionsUrl = $env:CASHFLOW_TRANSACTIONS_URL,
    [string]$AuthUrl = $env:CASHFLOW_AUTH_URL,
    [int]$Rate = 10,
    [int]$DurationSeconds = 30,
    [switch]$StressOnly,
    [switch]$LoadOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "tests\CashFlow.Transactions.Benchmarks\CashFlow.Transactions.Benchmarks.csproj"
$defaultTransactionsUrl = "https://localhost:7206"

if ([string]::IsNullOrWhiteSpace($TransactionsUrl)) {
    $TransactionsUrl = $defaultTransactionsUrl
}

if ([string]::IsNullOrWhiteSpace($AuthUrl)) {
    $AuthUrl = "https://localhost:7204"
}

$commonArgs = @(
    "--url", $TransactionsUrl,
    "--auth-url", $AuthUrl
)

Write-Host "Transactions exploratory load" -ForegroundColor Cyan
Write-Host "  API: $TransactionsUrl"
Write-Host "  Auth: $AuthUrl"
Write-Host "  Write-path checks: persistence mean latency < 200 ms (see docs/transactions-slo.md)"
Write-Host "  Consolidation SLO (50 RPS / 5% loss): reporting only — see docs/reporting-slo.md"
Write-Host ""

function Invoke-LoadGate {
    param(
        [string[]]$ExtraArgs,
        [string]$Label
    )

    Write-Host "=== $Label ===" -ForegroundColor Yellow
    dotnet run --project $benchmarkProject -- @ExtraArgs
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed (exit $LASTEXITCODE)."
    }
}

if (-not $StressOnly) {
    $loadArgs = @("load") + $commonArgs + @("--rate", $Rate, "--duration", $DurationSeconds)
    Invoke-LoadGate -ExtraArgs $loadArgs -Label "HTTP load test"
}

if (-not $LoadOnly) {
    $stressArgs = @(
        "stress"
    ) + $commonArgs + @(
        "--start-rate", "10",
        "--step", "10",
        "--max-rate", "100",
        "--step-duration", "15",
        "--failure-threshold", "5",
        "--max-mean-latency", "200"
    )
    Invoke-LoadGate -ExtraArgs $stressArgs -Label "HTTP stress test (capacity probe)"
}

Write-Host ""
Write-Host "Transaction load probes completed." -ForegroundColor Green
