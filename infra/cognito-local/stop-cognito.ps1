$ErrorActionPreference = "Continue"

$cognitoContainer = "cashflow-cognito-local"

Push-Location $PSScriptRoot
try {
    $previousNative = $PSNativeCommandUseErrorActionPreference
    $PSNativeCommandUseErrorActionPreference = $false
    try {
        docker compose down --remove-orphans 2>$null | Out-Null
    }
    finally {
        $PSNativeCommandUseErrorActionPreference = $previousNative
    }

    docker rm -f $cognitoContainer 2>$null | Out-Null
    Write-Host "Cognito Local container stopped." -ForegroundColor Green
}
finally {
    Pop-Location
}
