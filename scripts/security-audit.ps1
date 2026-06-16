param(
    [ValidateSet('low', 'moderate', 'high', 'critical')]
    [string]$FailOnPackageSeverity = 'moderate',

    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $repoRoot 'Aspire.CashFlow.slnx'

function Get-VulnerabilityPattern {
    return '(?i)(pacotes vulner[aá]veis|vulnerable packages)'
}

function Get-NuGetAuditWarnings {
    param([string]$Severity)

    switch ($Severity) {
        'low' { return @('NU1901', 'NU1902', 'NU1903', 'NU1904') }
        'moderate' { return @('NU1902', 'NU1903', 'NU1904') }
        'high' { return @('NU1903', 'NU1904') }
        'critical' { return @('NU1904') }
    }
}

Push-Location $repoRoot
try {
    Write-Host '>> dotnet restore...' -ForegroundColor Cyan
    dotnet restore $sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host '>> Pacotes vulneráveis (diretos e transitivos)...' -ForegroundColor Cyan
    $vulnerableOutput = dotnet list $sln package --vulnerable --include-transitive 2>&1 | Out-String
    Write-Host $vulnerableOutput

    $hasVulnerablePackages = $vulnerableOutput -match (Get-VulnerabilityPattern)
    if ($hasVulnerablePackages) {
        Write-Host 'Foram encontrados pacotes com vulnerabilidades conhecidas.' -ForegroundColor Yellow
    }
    else {
        Write-Host 'Nenhum pacote vulnerável encontrado.' -ForegroundColor Green
    }

    $buildArgs = @(
        'build', $sln,
        '-c', 'Release',
        '--no-restore'
    )

    if (-not $ReportOnly) {
        $auditWarnings = Get-NuGetAuditWarnings -Severity $FailOnPackageSeverity
        if ($auditWarnings.Count -gt 0) {
            $buildArgs += "-warnaserror:$($auditWarnings -join ';')"
        }
    }

    Write-Host '>> Build com analisadores de segurança (NetAnalyzers + SecurityCodeScan)...' -ForegroundColor Cyan
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $ReportOnly -and $hasVulnerablePackages) {
        Write-Host "Auditoria falhou: há vulnerabilidades em pacotes (limiar: $FailOnPackageSeverity)." -ForegroundColor Red
        exit 1
    }

    Write-Host 'Auditoria de segurança concluída.' -ForegroundColor Green
}
finally {
    Pop-Location
}
