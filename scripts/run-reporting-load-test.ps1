param(
    [string]$ReportingUrl = $env:CASHFLOW_REPORTING_URL,
    [string]$AuthUrl = $env:CASHFLOW_AUTH_URL,
    [int]$Rate = 50,
    [int]$DurationSeconds = 30,
    [string]$ReportDate = "",
    [switch]$SkipPreflight,
    [int]$WaitTimeoutSeconds = 0
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "tests\CashFlow.Reporting.Benchmarks\CashFlow.Reporting.Benchmarks.csproj"
$reportsDir = Join-Path $repoRoot "tests\CashFlow.Reporting.Benchmarks\reports"

if ([string]::IsNullOrWhiteSpace($ReportingUrl)) {
    $ReportingUrl = "https://localhost:7090"
}

if ([string]::IsNullOrWhiteSpace($AuthUrl)) {
    $AuthUrl = "https://localhost:7204"
}

function Get-UrlPort {
    param([string]$Url)

    if (-not [Uri]::TryCreate($Url, [UriKind]::Absolute, [ref]$null)) {
        throw "URL invalida: $Url"
    }

    $uri = [Uri]$Url
    if ($uri.Port -gt 0) {
        return $uri.Port
    }

    return if ($uri.Scheme -eq "https") { 443 } else { 80 }
}

function Test-TcpEndpoint {
    param(
        [string]$Url,
        [int]$TimeoutMs = 2000
    )

    $uri = [Uri]$Url
    $hostName = $uri.Host
    if ($hostName -eq "localhost") {
        $hostName = "127.0.0.1"
    }

    $port = Get-UrlPort $Url
    $client = New-Object System.Net.Sockets.TcpClient

    try {
        $connect = $client.BeginConnect($hostName, $port, $null, $null)
        $connected = $connect.AsyncWaitHandle.WaitOne($TimeoutMs, $false)
        if (-not $connected) {
            return $false
        }

        $client.EndConnect($connect)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Wait-ForEndpoints {
    param(
        [string[]]$Urls,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    Write-Host "Aguardando endpoints (timeout ${TimeoutSeconds}s)..." -ForegroundColor Yellow

    while ((Get-Date) -lt $deadline) {
        $allReady = $true
        foreach ($url in $Urls) {
            if (-not (Test-TcpEndpoint -Url $url)) {
                $allReady = $false
                break
            }
        }

        if ($allReady) {
            Write-Host "Endpoints disponiveis." -ForegroundColor Green
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Timeout aguardando servicos. Verifique se run-full-local.ps1 ainda esta em execucao."
}

function Assert-StackRunning {
    param(
        [string]$Auth,
        [string]$Reporting
    )

    $authUp = Test-TcpEndpoint -Url $Auth
    $reportingUp = Test-TcpEndpoint -Url $Reporting

    if ($authUp -and $reportingUp) {
        return
    }

    Write-Host ""
    Write-Host "ERRO: stack local nao esta acessivel." -ForegroundColor Red
    if (-not $authUp) {
        Write-Host "  Auth API inacessivel: $Auth" -ForegroundColor Red
    }
    if (-not $reportingUp) {
        Write-Host "  Reporting API inacessivel: $Reporting" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "O load test exige Auth + Reporting API em execucao." -ForegroundColor Yellow
    Write-Host "  1. Em outro terminal: .\scripts\run-full-local.ps1" -ForegroundColor Yellow
    Write-Host "  2. Aguarde 'Distributed application started' no Aspire Dashboard" -ForegroundColor Yellow
    Write-Host "  3. Rode este script SEM encerrar a stack (nao pressione Ctrl+C antes)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "URLs alternativas (se Aspire usar portas diferentes):" -ForegroundColor Yellow
    Write-Host "  `$env:CASHFLOW_AUTH_URL='https://localhost:PORTA'; `$env:CASHFLOW_REPORTING_URL='https://localhost:PORTA'; .\scripts\run-reporting-load-test.ps1" -ForegroundColor Gray
    Write-Host ""

    exit 1
}

$benchmarkExe = Join-Path $repoRoot "tests\CashFlow.Reporting.Benchmarks\bin\Debug\net10.0\CashFlow.Reporting.Benchmarks.exe"

if (-not (Test-Path $benchmarkExe)) {
    Write-Host "Benchmark binary not found; building (project references skipped while stack is running)..." -ForegroundColor Yellow
    dotnet build $benchmarkProject -p:BuildProjectReferences=false -v q
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$dotnetArgs = @(
    "run", "--project", $benchmarkProject, "--no-build", "--",
    "load",
    "--url", $ReportingUrl,
    "--auth-url", $AuthUrl,
    "--rate", $Rate,
    "--duration", $DurationSeconds
)

if (-not [string]::IsNullOrWhiteSpace($ReportDate)) {
    $dotnetArgs += @("--report-date", $ReportDate)
}

Write-Host "Reporting consolidation load test (SLO gate)" -ForegroundColor Cyan
Write-Host "  Reporting API: $ReportingUrl"
Write-Host "  Auth API:      $AuthUrl"
Write-Host "  Target:        $Rate RPS for ${DurationSeconds}s (max 5% fail, p50 < 200ms, p95 < 2000ms)"
Write-Host "  Reports dir:   $reportsDir"
Write-Host "  Note: uses --no-build so the running reporting-api is not recompiled." -ForegroundColor DarkGray
Write-Host ""

if (-not $SkipPreflight) {
    if ($WaitTimeoutSeconds -gt 0) {
        Wait-ForEndpoints -Urls @($AuthUrl, $ReportingUrl) -TimeoutSeconds $WaitTimeoutSeconds
    }
    else {
        Assert-StackRunning -Auth $AuthUrl -Reporting $ReportingUrl
    }
}

New-Item -ItemType Directory -Force -Path $reportsDir | Out-Null

$timestamp = Get-Date -Format "yyyy-MM-dd_HH.mm.ss"
$logFile = Join-Path $reportsDir "load_${Rate}rps_${timestamp}.log"

& dotnet @dotnetArgs 2>&1 | Tee-Object -FilePath $logFile
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "Load test passed. Log: $logFile" -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "Load test failed (exit $exitCode). See: $logFile" -ForegroundColor Red
}

exit $exitCode
