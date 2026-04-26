param(
    [string]$ConfigPath = ".\config\pc.config.json",
    [string]$ProjectId = "",
    [int]$TimeoutSeconds = 10,
    [switch]$SkipConfig
)

$ErrorActionPreference = "Stop"

function New-Check {
    param(
        [string]$Area,
        [string]$Target,
        [bool]$Passed,
        [string]$Detail
    )

    [pscustomobject]@{
        Area = $Area
        Target = $Target
        Passed = $Passed
        Detail = $Detail
    }
}

function Resolve-ConfiguredPath {
    param([string]$Path)

    if (-not $Path) {
        return ""
    }

    [Environment]::ExpandEnvironmentVariables($Path)
}

function Test-DnsNameSimple {
    param([string]$HostName)

    try {
        $addresses = [System.Net.Dns]::GetHostAddresses($HostName) |
            ForEach-Object { $_.IPAddressToString }
        if ($addresses.Count -gt 0) {
            return New-Check "DNS" $HostName $true ($addresses -join ", ")
        }

        return New-Check "DNS" $HostName $false "No addresses returned"
    } catch {
        return New-Check "DNS" $HostName $false $_.Exception.Message
    }
}

function Test-TcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutMs
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $asyncResult = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
            $client.Close()
            return New-Check "TCP" "$HostName`:$Port" $false "Connection timed out"
        }

        $client.EndConnect($asyncResult)
        return New-Check "TCP" "$HostName`:$Port" $true "Connected"
    } catch {
        return New-Check "TCP" "$HostName`:$Port" $false $_.Exception.Message
    } finally {
        $client.Dispose()
    }
}

function Test-HttpsEndpoint {
    param(
        [string]$Uri,
        [int]$TimeoutMs
    )

    try {
        $request = [System.Net.HttpWebRequest]::Create($Uri)
        $request.Method = "GET"
        $request.AllowAutoRedirect = $false
        $request.Timeout = $TimeoutMs
        $request.ReadWriteTimeout = $TimeoutMs
        $response = $request.GetResponse()
        $statusCode = [int]$response.StatusCode
        $response.Close()
        return New-Check "HTTPS" $Uri $true "HTTP $statusCode"
    } catch [System.Net.WebException] {
        $response = $_.Exception.Response
        if ($null -ne $response) {
            $statusCode = [int]$response.StatusCode
            $statusDescription = $response.StatusDescription
            $response.Close()
            return New-Check "HTTPS" $Uri $true "HTTP $statusCode $statusDescription"
        }

        return New-Check "HTTPS" $Uri $false $_.Exception.Message
    } catch {
        return New-Check "HTTPS" $Uri $false $_.Exception.Message
    }
}

$timeoutMs = [Math]::Max(1000, $TimeoutSeconds * 1000)
$checks = New-Object System.Collections.Generic.List[object]
$config = $null

if (-not $SkipConfig) {
    if (Test-Path -LiteralPath $ConfigPath) {
        try {
            $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
            $checks.Add((New-Check "Config" $ConfigPath $true "Loaded"))

            if (-not $ProjectId -and $config.firebase.projectId) {
                $ProjectId = [string]$config.firebase.projectId
            }

            $serviceAccountPath = Resolve-ConfiguredPath ([string]$config.firebase.serviceAccountPath)
            if ($serviceAccountPath -and (Test-Path -LiteralPath $serviceAccountPath)) {
                $serviceAccount = Get-Content -LiteralPath $serviceAccountPath -Raw | ConvertFrom-Json
                $requiredFields = @("project_id", "client_email", "private_key")
                $missing = @($requiredFields | Where-Object { -not $serviceAccount.$_ })
                if ($missing.Count -eq 0) {
                    $detail = "Found service account for $($serviceAccount.client_email)"
                    if ($ProjectId -and $serviceAccount.project_id -and $serviceAccount.project_id -ne $ProjectId) {
                        $checks.Add((New-Check "Config" "serviceAccountPath" $false "service account project_id '$($serviceAccount.project_id)' does not match '$ProjectId'"))
                    } else {
                        $checks.Add((New-Check "Config" "serviceAccountPath" $true $detail))
                    }
                } else {
                    $checks.Add((New-Check "Config" "serviceAccountPath" $false ("Missing JSON fields: " + ($missing -join ", "))))
                }
            } else {
                $checks.Add((New-Check "Config" "serviceAccountPath" $false "File not found: $serviceAccountPath"))
            }

            $targetValues = New-Object System.Collections.Generic.List[string]
            if ($config.firebase.targetToken) {
                $targetValues.Add(([string]$config.firebase.targetToken).Trim())
            }
            if ($config.firebase.targetTokens) {
                foreach ($token in @($config.firebase.targetTokens)) {
                    if (-not [string]::IsNullOrWhiteSpace([string]$token)) {
                        $targetValues.Add(([string]$token).Trim())
                    }
                }
            }
            $targetCount = @($targetValues | Select-Object -Unique).Count

            if ($targetCount -gt 0) {
                $checks.Add((New-Check "Config" "targetToken(s)" $true "Configured: $targetCount"))
            } else {
                $checks.Add((New-Check "Config" "targetToken(s)" $false "Missing Android FCM registration token"))
            }
        } catch {
            $checks.Add((New-Check "Config" $ConfigPath $false $_.Exception.Message))
        }
    } else {
        $checks.Add((New-Check "Config" $ConfigPath $false "Config file not found. Use -SkipConfig for network-only checks."))
    }
}

$hosts = @("oauth2.googleapis.com", "fcm.googleapis.com")
foreach ($hostName in $hosts) {
    $checks.Add((Test-DnsNameSimple $hostName))
    $checks.Add((Test-TcpPort $hostName 443 $timeoutMs))
}

$checks.Add((Test-HttpsEndpoint "https://oauth2.googleapis.com/token" $timeoutMs))
$checks.Add((Test-HttpsEndpoint "https://fcm.googleapis.com/" $timeoutMs))

if ($ProjectId) {
    $encodedProjectId = [System.Uri]::EscapeDataString($ProjectId)
    $checks.Add((Test-HttpsEndpoint "https://fcm.googleapis.com/v1/projects/$encodedProjectId/messages:send" $timeoutMs))
} elseif ($SkipConfig) {
    $checks.Add((New-Check "Config" "projectId" $true "Skipped for network-only check"))
} else {
    $checks.Add((New-Check "Config" "projectId" $false "Missing project id; pass -ProjectId or set firebase.projectId"))
}

Write-Host "Codex Alert FCM network check"
Write-Host ""
$checks | Format-Table Area, Target, Passed, Detail -AutoSize
Write-Host ""
Write-Host "Note: HTTP 401, 403, 404, or 405 from Google still means DNS/TCP/TLS reached Google. Auth is verified by send-test.ps1." -ForegroundColor DarkGray

$failed = @($checks | Where-Object { -not $_.Passed })
if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "One or more checks failed." -ForegroundColor Yellow
    exit 1
}

Write-Host "All FCM network/config checks passed." -ForegroundColor Green
