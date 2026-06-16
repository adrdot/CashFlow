param(
    [string]$MetricsTarget = "host.docker.internal:5100",
    [string]$ReportingMetricsTarget = "host.docker.internal:5292",
    [string]$OtelCollectorTarget = "host.docker.internal:8889",
    [string]$AspireOtlpEndpoint = "https://host.docker.internal:21119",
    [ValidateSet('auto', 'http', 'https')]
    [string]$MetricsScheme = 'auto',
    [ValidateSet('auto', 'http', 'https')]
    [string]$ReportingMetricsScheme = 'http',
    [bool]$UseOtelCollector = $true,
    [switch]$InsecureSkipTlsVerify,
    [switch]$StrictTlsVerify
)

$ErrorActionPreference = "Stop"

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

function Resolve-TransactionsMetricsScrapeSettings {
    param(
        [string]$MetricsTarget,
        [string]$MetricsScheme,
        [bool]$InsecureSkipTlsVerify,
        [bool]$StrictTlsVerify
    )

    $defaultHttpTarget = "host.docker.internal:5100"
    $defaultHttpsTarget = "host.docker.internal:7093"
    $inputWasEmpty = [string]::IsNullOrWhiteSpace($MetricsTarget)

    $hostPort = if ($inputWasEmpty) { $defaultHttpTarget } else { $MetricsTarget.Trim().TrimEnd('/') }
    $inputSchemeHint = $null

    if ($hostPort -match '^(https?)://(.+)$') {
        $inputSchemeHint = $Matches[1].ToLowerInvariant()
        $hostPort = $Matches[2]
    }

    if ($hostPort -match '^127\.0\.0\.1:') {
        $hostPort = $hostPort -replace '^127\.0\.0\.1:', 'host.docker.internal:'
        Write-Host "  Mapped 127.0.0.1 to host.docker.internal for Docker-based Prometheus scrape." -ForegroundColor Yellow
    }
    elseif ($hostPort -match '^localhost:') {
        $hostPort = $hostPort -replace '^localhost:', 'host.docker.internal:'
        Write-Host "  Mapped localhost to host.docker.internal for Docker-based Prometheus scrape." -ForegroundColor Yellow
    }

    if ($hostPort -notmatch '^[^:]+:\d+$') {
        throw "Invalid metrics target '$MetricsTarget'. Expected host:port (e.g. host.docker.internal:5100)."
    }

    $port = ($hostPort -split ':', 2)[1]
    $resolvedScheme = switch ($MetricsScheme) {
        'https' { 'https' }
        'http' { 'http' }
        default {
            if ($inputSchemeHint -eq 'https' -or $port -eq '7093' -or $port -eq '7090') {
                'https'
            }
            else {
                'http'
            }
        }
    }

    if ($resolvedScheme -eq 'http' -and $port -eq '7093') {
        Write-Warning "Port 7093 is HTTPS-only in local dev. Remapping to HTTP port 5100. Use -MetricsScheme https to scrape via TLS."
        $hostPort = $hostPort -replace ':7093$', ':5100'
    }

    if ($resolvedScheme -eq 'http' -and $port -eq '7090') {
        Write-Warning "Port 7090 is HTTPS-only in local dev. Remapping to HTTP port 5292. Use -ReportingMetricsScheme https to scrape via TLS."
        $hostPort = $hostPort -replace ':7090$', ':5292'
    }

    if ($resolvedScheme -eq 'https' -and $port -eq '5100' -and $inputWasEmpty) {
        $hostPort = $defaultHttpsTarget
    }

    if ($resolvedScheme -eq 'https' -and $port -eq '5100' -and -not $inputWasEmpty) {
        Write-Warning "Port 5100 is the HTTP endpoint. Consider host.docker.internal:7093 for HTTPS scrape."
    }

    $skipTlsVerify = $false
    if ($resolvedScheme -eq 'https') {
        if ($StrictTlsVerify) {
            $skipTlsVerify = $false
            Write-Host "  HTTPS scrape with strict TLS verification (dev certs will fail unless a CA is mounted)." -ForegroundColor Yellow
        }
        elseif ($InsecureSkipTlsVerify) {
            $skipTlsVerify = $true
        }
        else {
            $skipTlsVerify = $true
            Write-Host "  HTTPS scrape uses insecure_skip_verify for the ASP.NET dev certificate." -ForegroundColor Yellow
        }
    }

    $tlsBlock = ''
    if ($resolvedScheme -eq 'https' -and $skipTlsVerify) {
        $tlsBlock = "`n    tls_config:`n      insecure_skip_verify: true"
    }

    return [PSCustomObject]@{
        Target = $hostPort
        Scheme = $resolvedScheme
        TlsBlock = $tlsBlock
        SkipTlsVerify = $skipTlsVerify
    }
}

function Build-PrometheusScrapeConfigs {
    param(
        [bool]$UseOtelCollector,
        [string]$OtelCollectorTarget,
        [string]$MetricsTarget,
        [string]$MetricsScheme,
        [string]$MetricsTlsBlock,
        [string]$ReportingMetricsTarget,
        [string]$ReportingMetricsScheme,
        [string]$ReportingMetricsTlsBlock
    )

    if ($UseOtelCollector) {
        return @"
  - job_name: otel-collector
    metrics_path: /metrics
    scheme: http
    static_configs:
      - targets: ['$OtelCollectorTarget']
        labels:
          service: otel-collector
"@
    }

    return @"
  - job_name: transactions-api
    metrics_path: /metrics
    scheme: $MetricsScheme
$MetricsTlsBlock
    static_configs:
      - targets: ['$MetricsTarget']
        labels:
          service: transactions-api

  - job_name: reporting-api
    metrics_path: /metrics
    scheme: $ReportingMetricsScheme
$ReportingMetricsTlsBlock
    static_configs:
      - targets: ['$ReportingMetricsTarget']
        labels:
          service: reporting-api
"@
}

function Start-TransactionsObservabilityStack {
    param(
        [string]$MetricsTarget = "host.docker.internal:5100",
        [string]$ReportingMetricsTarget = "host.docker.internal:5292",
        [string]$OtelCollectorTarget = "host.docker.internal:8889",
        [string]$AspireOtlpEndpoint = "https://host.docker.internal:21119",
        [ValidateSet('auto', 'http', 'https')]
        [string]$MetricsScheme = 'auto',
        [ValidateSet('auto', 'http', 'https')]
        [string]$ReportingMetricsScheme = 'http',
        [bool]$UseOtelCollector = $true,
        [switch]$InsecureSkipTlsVerify,
        [switch]$StrictTlsVerify
    )

    if ($StrictTlsVerify -and $InsecureSkipTlsVerify) {
        throw "Use either -StrictTlsVerify or -InsecureSkipTlsVerify, not both."
    }

    $settings = Resolve-TransactionsMetricsScrapeSettings -MetricsTarget $MetricsTarget -MetricsScheme $MetricsScheme -InsecureSkipTlsVerify:$InsecureSkipTlsVerify -StrictTlsVerify:$StrictTlsVerify
    $reportingSettings = Resolve-TransactionsMetricsScrapeSettings -MetricsTarget $ReportingMetricsTarget -MetricsScheme $ReportingMetricsScheme -InsecureSkipTlsVerify:$InsecureSkipTlsVerify -StrictTlsVerify:$StrictTlsVerify

    $stackDir = $PSScriptRoot
    $composeFile = Join-Path $stackDir "docker-compose.yml"
    $prometheusTemplate = Join-Path $stackDir "prometheus\prometheus.yml"
    $generatedDir = Join-Path $stackDir "generated"
    $prometheusConfig = Join-Path $generatedDir "prometheus.yml"

    if (-not (Test-Path $composeFile)) {
        throw "docker-compose.yml not found at $composeFile"
    }

    if (-not (Test-Path $generatedDir)) {
        New-Item -ItemType Directory -Path $generatedDir | Out-Null
    }

    $content = Get-Content $prometheusTemplate -Raw
    if ($content -notmatch '__SCRAPE_CONFIGS__') {
        throw "Prometheus template is missing __SCRAPE_CONFIGS__ placeholder."
    }

    $scrapeConfigs = Build-PrometheusScrapeConfigs `
        -UseOtelCollector:$UseOtelCollector `
        -OtelCollectorTarget $OtelCollectorTarget `
        -MetricsTarget $settings.Target `
        -MetricsScheme $settings.Scheme `
        -MetricsTlsBlock $settings.TlsBlock `
        -ReportingMetricsTarget $reportingSettings.Target `
        -ReportingMetricsScheme $reportingSettings.Scheme `
        -ReportingMetricsTlsBlock $reportingSettings.TlsBlock

    $updated = $content.Replace('__SCRAPE_CONFIGS__', $scrapeConfigs)
    [System.IO.File]::WriteAllText($prometheusConfig, $updated)

    Write-Host "Starting Prometheus + Grafana + OTEL Collector..." -ForegroundColor Cyan
    if ($UseOtelCollector) {
        Write-Host "  Pipeline metrics: OTLP -> http://127.0.0.1:4318 -> Prometheus http://127.0.0.1:8889/metrics"
        Write-Host "  Aspire forward: $AspireOtlpEndpoint"
        Write-Host "  Set CASHFLOW_OTEL_COLLECTOR_ENDPOINT=http://127.0.0.1:4318 before starting AppHost."
    }
    else {
        Write-Host "  Transactions API metrics: $($settings.Scheme)://$($settings.Target)/metrics"
        Write-Host "  Reporting API metrics:    $($reportingSettings.Scheme)://$($reportingSettings.Target)/metrics"
        Write-Host "  Worker metrics are not scraped (UseOtelCollector recommended)."
    }
    if ($settings.Scheme -eq 'https' -and $settings.SkipTlsVerify) {
        Write-Host "  TLS: insecure_skip_verify (local dev certificate)"
    }
    Write-Host "  Prometheus: http://localhost:9090"
    Write-Host "  Grafana:    http://localhost:3000 (admin / admin)"
    Write-Host "  Dashboard:  CashFlow / Messaging Pipeline (EventStore -> SQS)"
    Write-Host ""

    $previousPrometheusConfig = $env:PROMETHEUS_CONFIG
    $previousAspireOtlpEndpoint = $env:ASPIRE_OTLP_ENDPOINT
    $env:PROMETHEUS_CONFIG = $prometheusConfig
    $env:ASPIRE_OTLP_ENDPOINT = $AspireOtlpEndpoint
    try {
        Push-Location $stackDir
        try {
            if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
                throw "Docker is required to start the observability stack."
            }

            if ($UseOtelCollector) {
                Invoke-NativeDockerCommand -ThrowOnFailure -SuppressOutput { docker compose -f $composeFile up -d --force-recreate otel-collector }
            }

            Invoke-NativeDockerCommand -ThrowOnFailure -SuppressOutput { docker compose -f $composeFile up -d --force-recreate prometheus }
            Invoke-NativeDockerCommand -ThrowOnFailure -SuppressOutput { docker compose -f $composeFile up -d }
        }
        finally {
            Pop-Location
        }
    }
    finally {
        if ($null -eq $previousPrometheusConfig) {
            Remove-Item Env:PROMETHEUS_CONFIG -ErrorAction SilentlyContinue
        }
        else {
            $env:PROMETHEUS_CONFIG = $previousPrometheusConfig
        }

        if ($null -eq $previousAspireOtlpEndpoint) {
            Remove-Item Env:ASPIRE_OTLP_ENDPOINT -ErrorAction SilentlyContinue
        }
        else {
            $env:ASPIRE_OTLP_ENDPOINT = $previousAspireOtlpEndpoint
        }
    }

    Write-Host "Observability stack is up." -ForegroundColor Green
}

if ($MyInvocation.InvocationName -ne '.') {
    Start-TransactionsObservabilityStack `
        -MetricsTarget $MetricsTarget `
        -ReportingMetricsTarget $ReportingMetricsTarget `
        -OtelCollectorTarget $OtelCollectorTarget `
        -AspireOtlpEndpoint $AspireOtlpEndpoint `
        -MetricsScheme $MetricsScheme `
        -ReportingMetricsScheme $ReportingMetricsScheme `
        -UseOtelCollector:$UseOtelCollector `
        -InsecureSkipTlsVerify:$InsecureSkipTlsVerify `
        -StrictTlsVerify:$StrictTlsVerify
}
