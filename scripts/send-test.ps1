param(
    [string]$ConfigPath = ".\config\pc.config.json",
    [string]$Title = "Codex Alert test",
    [string]$Body = "FCM test message from Windows relay config."
)

$ErrorActionPreference = "Stop"

function ConvertTo-Base64Url {
    param([byte[]]$Bytes)
    [Convert]::ToBase64String($Bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

function Convert-StringToBase64Url {
    param([string]$Text)
    ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($Text))
}

if (-not (Test-Path $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}

$config = Get-Content -Raw $ConfigPath | ConvertFrom-Json
if (-not $config.firebase.projectId -or -not $config.firebase.serviceAccountPath) {
    throw "Config must include firebase.projectId and firebase.serviceAccountPath."
}
if (-not (Test-Path $config.firebase.serviceAccountPath)) {
    throw "Service account JSON not found: $($config.firebase.serviceAccountPath)"
}

$serviceAccount = Get-Content -Raw $config.firebase.serviceAccountPath | ConvertFrom-Json
$tokenUri = if ($serviceAccount.token_uri) { $serviceAccount.token_uri } else { "https://oauth2.googleapis.com/token" }
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$jwtHeader = @{ alg = "RS256"; typ = "JWT" } | ConvertTo-Json -Compress
$jwtPayload = @{
    iss = $serviceAccount.client_email
    scope = "https://www.googleapis.com/auth/firebase.messaging"
    aud = $tokenUri
    iat = $now
    exp = $now + 3600
} | ConvertTo-Json -Compress

$encodedHeader = Convert-StringToBase64Url $jwtHeader
$encodedPayload = Convert-StringToBase64Url $jwtPayload
$signingInput = "$encodedHeader.$encodedPayload"

$rsa = [System.Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem($serviceAccount.private_key)
} catch {
    throw "Could not import service account private key. Use PowerShell 7+ or a runtime that supports RSA.ImportFromPem. $($_.Exception.Message)"
}

$signature = $rsa.SignData(
    [Text.Encoding]::UTF8.GetBytes($signingInput),
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
)
$assertion = "$signingInput.$(ConvertTo-Base64Url $signature)"

$tokenResponse = Invoke-RestMethod -Method Post -Uri $tokenUri -ContentType "application/x-www-form-urlencoded" -Body @{
    grant_type = "urn:ietf:params:oauth:grant-type:jwt-bearer"
    assertion = $assertion
}

function Get-ConfiguredTargetTokens {
    $tokens = New-Object System.Collections.Generic.List[string]
    if ($config.firebase.targetTokens) {
        foreach ($token in @($config.firebase.targetTokens)) {
            if (-not [string]::IsNullOrWhiteSpace([string]$token)) {
                $tokens.Add(([string]$token).Trim())
            }
        }
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$config.firebase.targetToken)) {
        $tokens.Add(([string]$config.firebase.targetToken).Trim())
    }
    $tokens | Select-Object -Unique
}

$targetTokens = @(Get-ConfiguredTargetTokens)
if ($targetTokens.Count -eq 0) {
    throw "No Android FCM target tokens configured."
}

foreach ($targetToken in $targetTokens) {
    $message = @{
        message = @{
            token = $targetToken
        android = @{
            priority = "HIGH"
        }
        data = @{
            type = "codex_notification"
            version = "1"
            pcId = [string]$config.pcId
            pcName = [string]$config.pcName
            sourceAppId = "manual-test"
            sourceAppName = "Codex Alert Relay"
            toastId = "manual-test-$([Guid]::NewGuid().ToString("N"))"
            title = $Title
            body = $Body
            receivedAtUtc = [DateTimeOffset]::UtcNow.UtcDateTime.ToString("o")
        }
    }
    } | ConvertTo-Json -Depth 8 -Compress

    $sendUri = "https://fcm.googleapis.com/v1/projects/$($config.firebase.projectId)/messages:send"
    $result = Invoke-RestMethod -Method Post -Uri $sendUri -ContentType "application/json; charset=utf-8" -Headers @{
        Authorization = "Bearer $($tokenResponse.access_token)"
    } -Body $message

    Write-Host "FCM send succeeded for token $($targetToken.Substring(0, [Math]::Min(12, $targetToken.Length)))...:"
    $result | ConvertTo-Json -Depth 8
}
