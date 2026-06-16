param(
    [string]$TransactionsMetricsUrl = $env:CASHFLOW_TRANSACTIONS_METRICS_URL,
    [string]$ReportingMetricsUrl = $env:CASHFLOW_REPORTING_METRICS_URL,
    [string]$PrometheusUrl = $env:CASHFLOW_PROMETHEUS_URL,
    [switch]$UsePrometheus,
    [int]$MaxCounterDelta = 0,
    [long]$MaxRelayLag = 100,
    [long]$MaxSqsVisible = 0,
    [int]$WaitSeconds = 0,
    [int]$PollIntervalSeconds = 5,
    [switch]$SkipCertificateCheck
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($TransactionsMetricsUrl)) {
    $TransactionsMetricsUrl = "https://localhost:7093/metrics"
}
if ([string]::IsNullOrWhiteSpace($ReportingMetricsUrl)) {
    $ReportingMetricsUrl = "https://localhost:7090/metrics"
}
if ([string]::IsNullOrWhiteSpace($PrometheusUrl)) {
    $PrometheusUrl = "http://localhost:9090"
}

function Get-PrometheusQueryValue {
    param([string]$Query)

    $encoded = [uri]::EscapeDataString($Query)
    $url = "$PrometheusUrl/api/v1/query?query=$encoded"
    $response = Invoke-RestMethod -Uri $url -TimeoutSec 15
    if ($response.status -ne "success") {
        throw "Prometheus query failed: $Query"
    }

    if (-not $response.data.result -or $response.data.result.Count -eq 0) {
        return 0
    }

    $total = 0.0
    foreach ($series in $response.data.result) {
        $total += [double]$series.value[1]
    }

    return $total
}

function Read-PipelineSnapshotFromPrometheus {
    [PSCustomObject]@{
        Created            = [long](Get-PrometheusQueryValue -Query "sum(transactions_created_total)")
        Published          = [long](Get-PrometheusQueryValue -Query "sum(transactions_events_published_total)")
        PublishFailures    = [long](Get-PrometheusQueryValue -Query "sum(transactions_events_publish_failures_total)")
        RelayLag           = [long](Get-PrometheusQueryValue -Query 'max({__name__=~"transactions_relay_subscription_lag(_events)?"})')
        RelayInFlight      = [long](Get-PrometheusQueryValue -Query "max(transactions_relay_subscription_in_flight)")
        Consumed           = [long](Get-PrometheusQueryValue -Query "sum(reporting_messages_consumed_total)")
        ProjectionFailures = [long](Get-PrometheusQueryValue -Query "sum(reporting_messages_failures_total)")
        SqsVisible         = [long](Get-PrometheusQueryValue -Query 'max(aws_sqs_approximate_number_of_messages_visible_average{dimension_QueueName=~"cashflow-transaction-recorded.*"})')
        SqsInFlight        = [long](Get-PrometheusQueryValue -Query 'max(aws_sqs_approximate_number_of_messages_not_visible_average{dimension_QueueName=~"cashflow-transaction-recorded.*"})')
    }
}

function Get-MetricsBody {
    param([string]$Url)

    $invokeParams = @{
        Uri             = $Url
        UseBasicParsing = $true
        TimeoutSec      = 15
    }
    if ($SkipCertificateCheck) {
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $invokeParams.SkipCertificateCheck = $true
        }
        else {
            add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate,
        WebRequest request, int certificateProblem) { return true; }
}
"@
            [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
        }
    }

    try {
        return (Invoke-WebRequest @invokeParams).Content
    }
    catch {
        throw "Failed to fetch metrics from $Url : $($_.Exception.Message)"
    }
}

function Get-PrometheusSamples {
    param([string]$Body)

    $samples = [System.Collections.Generic.List[object]]::new()
    foreach ($line in ($Body -split "`n")) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#")) {
            continue
        }

        if ($trimmed -notmatch '^(?<name>[a-zA-Z_:][a-zA-Z0-9_:]*)(\{(?<labels>[^}]*)\})?\s+(?<value>-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)(?:\s+\d+)?$') {
            continue
        }

        $samples.Add([PSCustomObject]@{
                Name   = $Matches.name
                Labels = $Matches.labels
                Value  = [double]$Matches.value
            })
    }

    return $samples
}

function Get-CounterTotal {
    param(
        [object[]]$Samples,
        [string]$NamePrefix
    )

    ($Samples | Where-Object { $_.Name -eq $NamePrefix -or $_.Name -like "$NamePrefix*" } | Measure-Object -Property Value -Sum).Sum
}

function Get-GaugeMax {
    param(
        [object[]]$Samples,
        [string[]]$NamePrefixes
    )

    $matches = $Samples | Where-Object {
        foreach ($prefix in $NamePrefixes) {
            if ($_.Name -eq $prefix -or $_.Name -like "$prefix*") { return $true }
        }
        return $false
    }
    if (-not $matches) { return $null }
    ($matches | Measure-Object -Property Value -Maximum).Maximum
}

function Read-PipelineSnapshot {
    $txBody = Get-MetricsBody -Url $TransactionsMetricsUrl
    $tx = Get-PrometheusSamples -Body $txBody

    [PSCustomObject]@{
        Created           = [long](Get-CounterTotal -Samples $tx -NamePrefix "transactions_created_total")
        Published         = $null
        PublishFailures   = $null
        RelayLag          = $null
        RelayInFlight     = $null
        Consumed          = $null
        ProjectionFailures = $null
        SqsVisible        = $null
        SqsInFlight       = $null
    }
}

function Test-PipelineSnapshot {
    param($Snapshot)

    $issues = [System.Collections.Generic.List[string]]::new()

    if ($null -ne $Snapshot.Published) {
        $createdPublishedDelta = [math]::Abs($Snapshot.Created - $Snapshot.Published)
        if ($createdPublishedDelta -gt $MaxCounterDelta) {
            $issues.Add("created ($($Snapshot.Created)) vs published ($($Snapshot.Published)) delta=$createdPublishedDelta (max $MaxCounterDelta)")
        }
    }

    if ($null -ne $Snapshot.Published -and $null -ne $Snapshot.Consumed) {
        $publishedConsumedDelta = [math]::Abs($Snapshot.Published - $Snapshot.Consumed)
        if ($publishedConsumedDelta -gt $MaxCounterDelta) {
            $issues.Add("published ($($Snapshot.Published)) vs consumed ($($Snapshot.Consumed)) delta=$publishedConsumedDelta (max $MaxCounterDelta)")
        }
    }

    if ($null -ne $Snapshot.RelayLag -and $Snapshot.RelayLag -gt $MaxRelayLag) {
        $issues.Add("relay lag $($Snapshot.RelayLag) > $MaxRelayLag")
    }

    if ($null -ne $Snapshot.SqsVisible -and $Snapshot.SqsVisible -gt $MaxSqsVisible) {
        $issues.Add("SQS visible $($Snapshot.SqsVisible) > $MaxSqsVisible")
    }

    if ($null -ne $Snapshot.PublishFailures -and $Snapshot.PublishFailures -gt 0) {
        $issues.Add("SNS publish failures total=$($Snapshot.PublishFailures)")
    }

    if ($null -ne $Snapshot.ProjectionFailures -and $Snapshot.ProjectionFailures -gt 0) {
        $issues.Add("reporting projection failures total=$($Snapshot.ProjectionFailures)")
    }

    return $issues
}

function Write-SnapshotTable {
    param($Snapshot)

    Write-Host ""
    Write-Host "Pipeline snapshot" -ForegroundColor Cyan
    Write-Host ("  transactions_created_total     : {0}" -f $Snapshot.Created)
    Write-Host ("  transactions_events_published  : {0}" -f ($(if ($null -eq $Snapshot.Published) { "n/a" } else { $Snapshot.Published })))
    Write-Host ("  reporting_messages_consumed    : {0}" -f ($(if ($null -eq $Snapshot.Consumed) { "n/a" } else { $Snapshot.Consumed })))
    Write-Host ("  relay_lag                      : {0}" -f ($(if ($null -eq $Snapshot.RelayLag) { "n/a" } else { $Snapshot.RelayLag })))
    Write-Host ("  relay_in_flight                : {0}" -f ($(if ($null -eq $Snapshot.RelayInFlight) { "n/a" } else { $Snapshot.RelayInFlight })))
    Write-Host ("  sqs_visible                    : {0}" -f ($(if ($null -eq $Snapshot.SqsVisible) { "n/a" } else { $Snapshot.SqsVisible })))
    Write-Host ("  sqs_in_flight                  : {0}" -f ($(if ($null -eq $Snapshot.SqsInFlight) { "n/a" } else { $Snapshot.SqsInFlight })))
    Write-Host ("  publish_failures               : {0}" -f ($(if ($null -eq $Snapshot.PublishFailures) { "n/a" } else { $Snapshot.PublishFailures })))
    Write-Host ("  projection_failures            : {0}" -f ($(if ($null -eq $Snapshot.ProjectionFailures) { "n/a" } else { $Snapshot.ProjectionFailures })))
    Write-Host ""
    if ($null -ne $Snapshot.Published -and $null -ne $Snapshot.Consumed) {
        Write-Host ("  created - published          : {0}" -f ($Snapshot.Created - $Snapshot.Published))
        Write-Host ("  published - consumed           : {0}" -f ($Snapshot.Published - $Snapshot.Consumed))
    }
}

Write-Host "Validating messaging pipeline (EventStore -> SNS -> SQS -> Reporting)" -ForegroundColor Cyan
if ($UsePrometheus) {
    Write-Host "  Source: Prometheus $PrometheusUrl (worker metrics via OTLP in Aspire Dashboard)"
}
else {
    Write-Host "  Transactions API metrics: $TransactionsMetricsUrl"
    Write-Host "  Tip: use -UsePrometheus after wiring an OTLP collector, or inspect workers in Aspire Dashboard"
}
if ($WaitSeconds -gt 0) {
    Write-Host "  Wait up to ${WaitSeconds}s for drain (poll every ${PollIntervalSeconds}s)"
}

$deadline = (Get-Date).AddSeconds($WaitSeconds)
$attempt = 0
do {
    $attempt++
    if ($attempt -gt 1) {
        Write-Host ""
        Write-Host "Poll attempt $attempt..." -ForegroundColor DarkGray
    }

    $snapshot = if ($UsePrometheus) { Read-PipelineSnapshotFromPrometheus } else { Read-PipelineSnapshot }
    Write-SnapshotTable -Snapshot $snapshot
    $issues = Test-PipelineSnapshot -Snapshot $snapshot

    if ($issues.Count -eq 0) {
        Write-Host "PASS - pipeline counters and backlogs within thresholds." -ForegroundColor Green
        exit 0
    }

    if ($WaitSeconds -le 0 -or (Get-Date) -ge $deadline) {
        break
    }

    Write-Host "Not ready yet; waiting ${PollIntervalSeconds}s..." -ForegroundColor Yellow
    Start-Sleep -Seconds $PollIntervalSeconds
} while ((Get-Date) -lt $deadline)

Write-Host "FAIL - pipeline validation issues:" -ForegroundColor Red
foreach ($issue in $issues) {
    Write-Host "  - $issue" -ForegroundColor Red
}

Write-Host ""
Write-Host "Hints:" -ForegroundColor Yellow
Write-Host "  - After load test, retry with -WaitSeconds 120 to allow SQS drain"
Write-Host "  - Use -MaxCounterDelta N if catch-up/replay is in progress"
Write-Host "  - Worker metrics (published/consumed/lag): Aspire Dashboard OTLP (no HTTP /metrics)"
Write-Host "  - Use -UsePrometheus when an OTLP->Prometheus bridge is configured"

exit 1
