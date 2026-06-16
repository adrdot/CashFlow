param(
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

if (-not $OutputFile) {
    $OutputFile = Join-Path $PSScriptRoot "generated\transactions-stack.env"
}

$eventStoreConnection = "esdb://127.0.0.1:2113?tls=false"

$content = @"
EventStore__ConnectionString=$eventStoreConnection
EventStore__HttpEndpoint=http://127.0.0.1:2113
"@

$directory = Split-Path -Parent $OutputFile
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputFile, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "Transactions stack env written to $OutputFile" -ForegroundColor Green
