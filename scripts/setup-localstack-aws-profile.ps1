$ErrorActionPreference = "Stop"

$awsDir = Join-Path $HOME ".aws"
$credentialsFile = Join-Path $awsDir "credentials"
$configFile = Join-Path $awsDir "config"
$profileName = "localstack"
$endpoint = "http://127.0.0.1:4566"
$region = "us-east-1"

if (-not (Test-Path $awsDir)) {
    New-Item -ItemType Directory -Path $awsDir | Out-Null
}

function Update-AwsIniFile {
    param(
        [string]$Path,
        [string]$SectionHeader,
        [string[]]$Lines
    )

    $existing = @()
    if (Test-Path $Path) {
        $existing = Get-Content -Path $Path
    }

    $sectionIndex = -1
    for ($i = 0; $i -lt $existing.Count; $i++) {
        if ($existing[$i].Trim() -eq $SectionHeader) {
            $sectionIndex = $i
            break
        }
    }

    $newSection = @($SectionHeader) + $Lines

    if ($sectionIndex -ge 0) {
        $endIndex = $sectionIndex + 1
        while ($endIndex -lt $existing.Count -and $existing[$endIndex] -notmatch '^\s*\[') {
            $endIndex++
        }

        $before = @()
        if ($sectionIndex -gt 0) {
            $before = $existing[0..($sectionIndex - 1)]
        }

        $after = @()
        if ($endIndex -lt $existing.Count) {
            $after = $existing[$endIndex..($existing.Count - 1)]
        }

        $content = $before + $newSection + $after
    }
    else {
        $content = $existing
        if ($content.Count -gt 0) {
            $content += ""
        }

        $content += $newSection
    }

    $text = ($content -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine
    [System.IO.File]::WriteAllText($Path, $text, [System.Text.UTF8Encoding]::new($false))
}

Update-AwsIniFile -Path $credentialsFile -SectionHeader "[$profileName]" -Lines @(
    "aws_access_key_id = test",
    "aws_secret_access_key = test"
)

Update-AwsIniFile -Path $configFile -SectionHeader "[profile $profileName]" -Lines @(
    "region = $region",
    "output = json",
    "endpoint_url = $endpoint"
)

Write-Host "Perfil AWS '$profileName' configurado." -ForegroundColor Green
Write-Host "  Credentials: $credentialsFile"
Write-Host "  Config:      $configFile"
Write-Host "  Endpoint:    $endpoint"
Write-Host "  Region:      $region"
Write-Host ""
Write-Host "No VS Code: Ctrl+Shift+P -> AWS: Select Profile -> localstack"
