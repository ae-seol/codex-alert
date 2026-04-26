param(
    [string]$Title = "Codex test alert",
    [string]$Body = "Injected locally through adb. This does not use LAN or FCM.",
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
    Write-Host "Connect a USB-debugging-enabled Android phone or start an emulator first."
    exit 1
}

$toastId = "adb-test-$([Guid]::NewGuid().ToString("N"))"

function ConvertTo-AdbShellLiteral {
    param([string]$Value)
    return "'" + ($Value -replace "'", "'\\''") + "'"
}

$receivedAtUtc = [DateTimeOffset]::UtcNow.UtcDateTime.ToString("o")
$shellCommand = @(
    "am start",
    "-n com.codexalert/.MainActivity",
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

& adb shell $shellCommand

Write-Host "Injected test alert: $toastId"
