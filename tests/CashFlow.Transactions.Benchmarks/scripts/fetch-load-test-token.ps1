param(
    [string]$AuthBaseUrl = "https://localhost:7204",
    [string]$Email = "admin@cashflow.docker",
    [string]$Password = "Pass@word1",
    [string]$MfaCode = "123456",
    [string]$OutputFile = "token.txt"
)

$ErrorActionPreference = "Stop"

if (-not $PSBoundParameters.ContainsKey("AuthBaseUrl")) {
    $aspireAuthUrl = $env:CASHFLOW_AUTH_URL
    if (-not [string]::IsNullOrWhiteSpace($aspireAuthUrl)) {
        $AuthBaseUrl = $aspireAuthUrl
    }
}

$loginUri = "$($AuthBaseUrl.TrimEnd('/'))/api/auth/login"
$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json

Write-Host "Requesting MFA challenge from $loginUri ..."
$challenge = Invoke-RestMethod -Method Post -Uri $loginUri -ContentType "application/json" -Body $loginBody -SkipCertificateCheck

if (-not $challenge.requiresMfa) {
    if ($challenge.token) {
        $challenge.token | Set-Content -Path $OutputFile -NoNewline -Encoding utf8
        Write-Host "Token saved to $OutputFile" -ForegroundColor Green
        exit 0
    }

    throw "Login did not return MFA challenge or token."
}

$mfaBody = @{
    email = $Email
    password = $Password
    mfaCode = $MfaCode
    challengeSession = $challenge.challengeSession
    challengeName = $challenge.challengeName
} | ConvertTo-Json

Write-Host "Completing MFA ..."
$result = Invoke-RestMethod -Method Post -Uri $loginUri -ContentType "application/json" -Body $mfaBody -SkipCertificateCheck

if ([string]::IsNullOrWhiteSpace($result.token)) {
    throw "MFA login did not return a token."
}

$result.token | Set-Content -Path $OutputFile -NoNewline -Encoding utf8
Write-Host "Token saved to $OutputFile" -ForegroundColor Green
