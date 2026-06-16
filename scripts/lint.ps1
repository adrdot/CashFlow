param(
    [switch]$Fix
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $repoRoot "Aspire.CashFlow.slnx"

Push-Location $repoRoot
try {
    Write-Host ">> dotnet tool restore..." -ForegroundColor Cyan
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if ($Fix) {
        Write-Host ">> dotnet format analyzers (correções automáticas)..." -ForegroundColor Cyan
        dotnet format $sln analyzers --severity warn

        Write-Host ">> CSharpier (formatação C#)..." -ForegroundColor Cyan
        dotnet csharpier .
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host ">> build Release..." -ForegroundColor Cyan
        dotnet build $sln -c Release --no-restore 2>$null
        if ($LASTEXITCODE -ne 0) {
            dotnet restore $sln
            dotnet build $sln -c Release
        }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host "Lint com correção concluído." -ForegroundColor Green
    }
    else {
        Write-Host ">> CSharpier (verificação)..." -ForegroundColor Cyan
        dotnet csharpier --check .
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Formatação divergente. Execute: ./scripts/lint.ps1 -Fix" -ForegroundColor Yellow
            exit $LASTEXITCODE
        }

        Write-Host ">> Build com analisadores..." -ForegroundColor Cyan
        dotnet build $sln -c Release
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host "Lint OK." -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
