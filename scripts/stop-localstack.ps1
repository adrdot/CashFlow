$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$stopScript = Join-Path $repoRoot "infra\cognito-local\stop-cognito.ps1"

& $stopScript
