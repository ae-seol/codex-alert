param(
    [string]$AndroidHome = (Join-Path (Split-Path $PSScriptRoot -Parent) ".tools\android-sdk")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$env:ANDROID_HOME = (Resolve-Path $AndroidHome).Path
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot"
$env:Path = "$env:JAVA_HOME\bin;$env:ANDROID_HOME\platform-tools;$env:Path"

$devices = & adb devices | Select-Object -Skip 1 | Where-Object { $_ -match "\bdevice\b" }
if (-not $devices) {
    Write-Host "No authorized Android device found." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "USB test setup:"
    Write-Host "  1. Connect the phone with a USB-C data cable."
    Write-Host "  2. Enable Developer options on the phone."
    Write-Host "  3. Enable USB debugging."
    Write-Host "  4. Accept the RSA debugging prompt on the phone."
    Write-Host "  5. Run: adb devices -l"
    exit 1
}

& (Join-Path $repoRoot "android\gradlew.bat") -p (Join-Path $repoRoot "android") :app:installDebug
