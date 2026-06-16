param(
    [string]$Endpoint = "http://localhost:4566",
    [string]$InternalEndpoint = "http://cashflow-localstack:4566",
    [string]$DockerNetwork = "localstack_default",
    [string]$Prefix = "cashflow",
    [string]$JwtSigningKey = "localstack-jwt-signing-key-change-me-1234567890",
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

if (-not $OutputFile) {
    $OutputFile = Join-Path $PSScriptRoot "generated\localstack.env"
}

function Invoke-NativeCommand {
    param(
        [scriptblock]$Command,
        [switch]$AllowFailure
    )

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Command 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "Command failed (exit $exitCode):`n$($output | Out-String)"
    }

    return [pscustomobject]@{
        Output = ($output | Out-String).Trim()
        ExitCode = $exitCode
    }
}

function Invoke-AwsSecretsManager {
    param(
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    if (Get-Command aws -ErrorAction SilentlyContinue) {
        $result = Invoke-NativeCommand -AllowFailure:$AllowFailure -Command {
            aws @Arguments --endpoint-url $Endpoint
        }

        if (-not $AllowFailure -and $result.ExitCode -ne 0) {
            throw "AWS CLI failed: aws $($Arguments -join ' ') --endpoint-url $Endpoint`n$($result.Output)"
        }

        return $result.Output
    }

    $dockerArgs = @(
        "run", "--rm",
        "--network", $DockerNetwork,
        "-e", "AWS_ACCESS_KEY_ID=test",
        "-e", "AWS_SECRET_ACCESS_KEY=test",
        "-e", "AWS_DEFAULT_REGION=us-east-1",
        "-e", "AWS_PAGER=",
        "amazon/aws-cli"
    ) + $Arguments + @("--endpoint-url", $InternalEndpoint)

    $result = Invoke-NativeCommand -AllowFailure:$AllowFailure -Command {
        docker @dockerArgs
    }

    if (-not $AllowFailure -and $result.ExitCode -ne 0) {
        throw "AWS CLI (docker) failed: docker $($dockerArgs -join ' ')`n$($result.Output)"
    }

    return $result.Output
}

Write-Host "Provisioning LocalStack Secrets Manager at $Endpoint..." -ForegroundColor Cyan

$secretName = "$Prefix/Auth/JwtSigningKey"
Invoke-AwsSecretsManager @(
    "secretsmanager", "create-secret",
    "--name", $secretName,
    "--secret-string", $JwtSigningKey
) -AllowFailure | Out-Null

Invoke-AwsSecretsManager @(
    "secretsmanager", "put-secret-value",
    "--secret-id", $secretName,
    "--secret-string", $JwtSigningKey
) -AllowFailure | Out-Null

Write-Host "Provisioning LocalStack KMS keys at $Endpoint..." -ForegroundColor Cyan

function Invoke-AwsKms {
    param(
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    if (Get-Command aws -ErrorAction SilentlyContinue) {
        $result = Invoke-NativeCommand -AllowFailure:$AllowFailure -Command {
            aws @Arguments --endpoint-url $Endpoint
        }

        if (-not $AllowFailure -and $result.ExitCode -ne 0) {
            throw "AWS CLI failed: aws $($Arguments -join ' ') --endpoint-url $Endpoint`n$($result.Output)"
        }

        return $result.Output
    }

    $dockerArgs = @(
        "run", "--rm",
        "--network", $DockerNetwork,
        "-e", "AWS_ACCESS_KEY_ID=test",
        "-e", "AWS_SECRET_ACCESS_KEY=test",
        "-e", "AWS_DEFAULT_REGION=us-east-1",
        "-e", "AWS_PAGER=",
        "amazon/aws-cli"
    ) + $Arguments + @("--endpoint-url", $InternalEndpoint)

    $result = Invoke-NativeCommand -AllowFailure:$AllowFailure -Command {
        docker @dockerArgs
    }

    if (-not $AllowFailure -and $result.ExitCode -ne 0) {
        throw "AWS CLI (docker) failed: docker $($dockerArgs -join ' ')`n$($result.Output)"
    }

    return $result.Output
}

function Ensure-KmsAlias {
    param(
        [string]$AliasName,
        [string]$Description
    )

    $existingAlias = Invoke-AwsKms @(
        "kms", "list-aliases",
        "--query", "Aliases[?AliasName=='$AliasName'].TargetKeyId | [0]",
        "--output", "text"
    ) -AllowFailure

    if (-not [string]::IsNullOrWhiteSpace($existingAlias) -and $existingAlias -ne "None") {
        Write-Host "KMS alias already exists: $AliasName" -ForegroundColor Yellow
        return
    }

    $keyId = Invoke-AwsKms @(
        "kms", "create-key",
        "--description", $Description,
        "--query", "KeyMetadata.KeyId",
        "--output", "text"
    )

    Invoke-AwsKms @(
        "kms", "create-alias",
        "--alias-name", $AliasName,
        "--target-key-id", $keyId
    ) -AllowFailure | Out-Null

    Write-Host "Created KMS alias $AliasName -> $keyId" -ForegroundColor Green
}

Ensure-KmsAlias -AliasName "alias/cashflow-default" -Description "CashFlow default encryption key"
Ensure-KmsAlias -AliasName "alias/cashflow-secrets" -Description "CashFlow secrets encryption key"
Ensure-KmsAlias -AliasName "alias/cashflow-storage" -Description "CashFlow storage encryption key"

$content = @"
LOCALSTACK_SERVICE_URL=$Endpoint
SECRETS_MANAGER_SERVICE_URL=$Endpoint
KMS_SERVICE_URL=$Endpoint
SECRETS_MANAGER_PREFIX=$Prefix/
SECRETS_MANAGER_PREFER_CONFIGURATION=false
"@

$directory = Split-Path -Parent $OutputFile
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputFile, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "LocalStack secrets ready: $secretName" -ForegroundColor Green
