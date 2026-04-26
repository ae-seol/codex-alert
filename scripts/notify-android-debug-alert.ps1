param(
    [string]$Title = "Codex completed",
    [string]$Body = "Debug broadcast notification from PC.",
    [string]$PcName = $env:COMPUTERNAME,
    [string]$AndroidHome = (Join-Path (Split-Path $PSScriptRoot -Parent) ".tools\android-sdk")
)

$ErrorActionPreference = "Stop"
$env:ANDROID_HOME = (Resolve-Path $AndroidHome).Path
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:Path = "$env:ANDROID_HOME\platform-tools;$env:Path"

$devices = & adb devices | Select-Object -Skip 1 | Where-Object { $_ -match "\bdevice\b" }
if (-not $devices) {
    Write-Host "No authorized Android device found." -ForegroundColor Yellow
    exit 1
}

function ConvertTo-AdbShellLiteral {
    param([string]$Value)
    return "'" + ($Value -replace "'", "'\\''") + "'"
}

$toastId = "debug-notification-$([Guid]::NewGuid().ToString("N"))"
$receivedAtUtc = [DateTimeOffset]::UtcNow.UtcDateTime.ToString("o")
$shellCommand = @(
    "am broadcast",
    "-a com.codexalert.DEBUG_ALERT",
    "-p com.codexalert",
    "--es type $(ConvertTo-AdbShellLiteral 'codex_notification')",
    "--es version $(ConvertTo-AdbShellLiteral '1')",
    "--es pcId $(ConvertTo-AdbShellLiteral 'desktop-test')",
    "--es pcName $(ConvertTo-AdbShellLiteral $PcName)",
    "--es sourceAppId $(ConvertTo-AdbShellLiteral 'OpenAI.Codex_2p2nqsd0c76g0!App')",
    "--es sourceAppName $(ConvertTo-AdbShellLiteral 'Codex')",
    "--es toastId $(ConvertTo-AdbShellLiteral $toastId)",
    "--es title $(ConvertTo-AdbShellLiteral $Title)",
    "--es body $(ConvertTo-AdbShellLiteral $Body)",
    "--es receivedAtUtc $(ConvertTo-AdbShellLiteral $receivedAtUtc)"
) -join " "

& adb shell input keyevent HOME | Out-Null
Start-Sleep -Milliseconds 500
& adb shell $shellCommand

Write-Host "Posted debug notification request: $toastId"
