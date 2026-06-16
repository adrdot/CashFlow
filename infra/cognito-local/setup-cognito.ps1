param(
    [string]$Endpoint = "http://localhost:9229",
    [string]$InternalEndpoint = "http://cashflow-cognito-local:9229",
    [string]$DockerNetwork = "cognito-local_default",
    [string]$PoolName = "cashflow-local",
    [string]$ClientName = "cashflow-web",
    [string]$Username = "admin@cashflow.docker",
    [string]$Password = "Pass@word1",
    [string]$MfaCode = "123456",
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

if (-not $OutputFile) {
    $OutputFile = Join-Path $PSScriptRoot "generated\cognito.env"
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

function Invoke-AwsCognito {
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
        "-e", "AWS_ACCESS_KEY_ID=local",
        "-e", "AWS_SECRET_ACCESS_KEY=local",
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

function Remove-LegacyCognitoUsers {
    param(
        [string]$PoolId,
        [string]$KeepUsername
    )

    $usersJson = Invoke-AwsCognito @(
        "cognito-idp", "list-users", "--user-pool-id", $PoolId, "--output", "json"
    )

    $users = ($usersJson | ConvertFrom-Json).Users
    if (-not $users) {
        return
    }

    foreach ($user in $users) {
        $email = ($user.Attributes | Where-Object { $_.Name -eq "email" } | Select-Object -First 1).Value
        $deleteUsername = if (-not [string]::IsNullOrWhiteSpace($email)) { $email } else { $user.Username }

        if ($deleteUsername -eq $KeepUsername) {
            continue
        }

        Write-Host "Removing legacy Cognito user: $deleteUsername" -ForegroundColor Yellow
        Invoke-AwsCognito @(
            "cognito-idp", "admin-delete-user",
            "--user-pool-id", $PoolId,
            "--username", $deleteUsername
        ) -AllowFailure | Out-Null
    }
}

Write-Host "Provisioning Cognito Local at $Endpoint..." -ForegroundColor Cyan

$poolId = Invoke-AwsCognito @(
    "cognito-idp", "list-user-pools", "--max-results", "60",
    "--query", "UserPools[?Name=='$PoolName'].Id | [0]", "--output", "text"
)

if ($poolId -eq "None" -or [string]::IsNullOrWhiteSpace($poolId)) {
    $poolId = Invoke-AwsCognito @(
        "cognito-idp", "create-user-pool", "--pool-name", $PoolName,
        "--query", "UserPool.Id", "--output", "text"
    )
}

Invoke-AwsCognito @(
    "cognito-idp", "update-user-pool",
    "--user-pool-id", $poolId,
    "--mfa-configuration", "ON",
    "--software-token-mfa-configuration", "Enabled=true"
) -AllowFailure | Out-Null

$clientId = Invoke-AwsCognito @(
    "cognito-idp", "list-user-pool-clients", "--user-pool-id", $poolId, "--max-results", "60",
    "--query", "UserPoolClients[?ClientName=='$ClientName'].ClientId | [0]", "--output", "text"
)

if ($clientId -eq "None" -or [string]::IsNullOrWhiteSpace($clientId)) {
    $clientId = Invoke-AwsCognito @(
        "cognito-idp", "create-user-pool-client",
        "--user-pool-id", $poolId,
        "--client-name", $ClientName,
        "--explicit-auth-flows", "ALLOW_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH",
        "--allowed-o-auth-flows", "code",
        "--allowed-o-auth-scopes", "openid", "email", "profile",
        "--callback-urls", "https://localhost:7262/auth/callback",
        "--supported-identity-providers", "COGNITO",
        "--query", "UserPoolClient.ClientId", "--output", "text"
    )
}
else {
    Invoke-AwsCognito @(
        "cognito-idp", "update-user-pool-client",
        "--user-pool-id", $poolId,
        "--client-id", $clientId,
        "--explicit-auth-flows", "ALLOW_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH",
        "--allowed-o-auth-flows", "code",
        "--allowed-o-auth-scopes", "openid", "email", "profile",
        "--callback-urls", "https://localhost:7262/auth/callback",
        "--supported-identity-providers", "COGNITO"
    ) -AllowFailure | Out-Null
}

Remove-LegacyCognitoUsers -PoolId $poolId -KeepUsername $Username

$userLookup = Invoke-AwsCognito @(
    "cognito-idp", "admin-get-user", "--user-pool-id", $poolId, "--username", $Username
) -AllowFailure

if ([string]::IsNullOrWhiteSpace($userLookup) -or $userLookup -match "ResourceNotFoundException|UserNotFoundException|could not be found") {
    Invoke-AwsCognito @(
        "cognito-idp", "admin-create-user",
        "--user-pool-id", $poolId,
        "--username", $Username,
        "--user-attributes", "Name=email,Value=$Username", "Name=email_verified,Value=true",
        "--message-action", "SUPPRESS"
    ) | Out-Null
}

Invoke-AwsCognito @(
    "cognito-idp", "admin-set-user-password",
    "--user-pool-id", $poolId,
    "--username", $Username,
    "--password", $Password,
    "--permanent"
) | Out-Null

Invoke-AwsCognito @(
    "cognito-idp", "admin-set-user-mfa-preference",
    "--user-pool-id", $poolId,
    "--username", $Username,
    "--software-token-mfa-settings", "Enabled=true,PreferredMfa=true"
) -AllowFailure | Out-Null

$content = @"
COGNITO_USER_POOL_ID=$poolId
COGNITO_CLIENT_ID=$clientId
COGNITO_SERVICE_URL=$Endpoint
COGNITO_REGION=us-east-1
COGNITO_USERNAME=$Username
COGNITO_PASSWORD=$Password
COGNITO_MFA_CODE=$MfaCode
CASHFLOW_COGNITO_ENABLED=true
"@

$directory = Split-Path -Parent $OutputFile
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputFile, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "Cognito Local ready: pool=$poolId client=$clientId user=$Username" -ForegroundColor Green
