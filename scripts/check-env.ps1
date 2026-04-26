param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

function Find-Executable {
    param([string]$Name, [string[]]$FallbackPaths)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    foreach ($path in $FallbackPaths) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    return ""
}

function Test-CommandExists {
    param([string]$Name, [string[]]$FallbackPaths = @())
    $source = Find-Executable $Name $FallbackPaths
    if (-not $source) {
        return [pscustomobject]@{ Name = $Name; Found = $false; Source = ""; Version = "" }
    }

    $version = ""
    try {
        switch ($Name) {
            "java" { $version = (& $source -version 2>&1 | Select-Object -First 1) -replace '"', "" }
            "javac" { $version = (& $source -version 2>&1 | Select-Object -First 1) }
            "dotnet" { $version = (& $source --version 2>$null) }
            "adb" { $version = (& $source version 2>$null | Select-Object -First 1) }
            "node" { $version = (& $source --version 2>$null) }
            "npm" { $version = (& $source --version 2>$null) }
            default { $version = "" }
        }
    } catch {
        $version = ""
    }

    [pscustomobject]@{
        Name = $Name
        Found = $true
        Source = $source
        Version = $version
    }
}

function Test-PathHint {
    param([string]$Name, [string[]]$Paths)
    $existing = $Paths | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    [pscustomobject]@{
        Name = $Name
        Found = [bool]$existing
        Source = if ($existing) { $existing } else { "" }
        Version = ""
    }
}

$repoAndroidSdk = Join-Path (Get-Location) ".tools\android-sdk"
$platformToolsAdb = Join-Path $repoAndroidSdk "platform-tools\adb.exe"
$wingetAdb = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\Google.PlatformTools_Microsoft.Winget.Source_8wekyb3d8bbwe\platform-tools\adb.exe"
$javaHomeCandidate = "C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot"

$commands = @(
    (Test-CommandExists "java" @((Join-Path $javaHomeCandidate "bin\java.exe"), "C:\Program Files\Android\Android Studio\jbr\bin\java.exe")),
    (Test-CommandExists "javac" @((Join-Path $javaHomeCandidate "bin\javac.exe"))),
    (Test-CommandExists "dotnet" @("C:\Program Files\dotnet\dotnet.exe")),
    (Test-CommandExists "adb" @($platformToolsAdb, $wingetAdb)),
    (Test-CommandExists "node"),
    (Test-CommandExists "npm")
)
$envChecks = @(
    [pscustomobject]@{ Name = "ANDROID_HOME"; Found = [bool]$env:ANDROID_HOME; Source = $env:ANDROID_HOME; Version = "" },
    [pscustomobject]@{ Name = "ANDROID_SDK_ROOT"; Found = [bool]$env:ANDROID_SDK_ROOT; Source = $env:ANDROID_SDK_ROOT; Version = "" },
    [pscustomobject]@{ Name = "JAVA_HOME"; Found = [bool]$env:JAVA_HOME; Source = $env:JAVA_HOME; Version = "" }
)
$pathHints = @(
    (Test-PathHint "Android Studio" @(
        (Join-Path $env:ProgramFiles "Android\Android Studio"),
        (Join-Path ${env:ProgramFiles(x86)} "Android\Android Studio")
    )),
    (Test-PathHint "Android SDK default" @(
        "$env:LOCALAPPDATA\Android\Sdk",
        $repoAndroidSdk
    )),
    (Test-PathHint "Repo Android platform-tools" @(
        $platformToolsAdb
    ))
)

$all = @($commands + $envChecks + $pathHints)

if (-not $Quiet) {
    Write-Host "Codex Alert environment check"
    Write-Host ""
    $all | Format-Table Name, Found, Version, Source -AutoSize
    Write-Host ""
}

$missingImportant = @()
if (-not ($commands | Where-Object { $_.Name -eq "java" -and $_.Found })) { $missingImportant += "JDK 17 (java)" }
if (-not ($commands | Where-Object { $_.Name -eq "javac" -and $_.Found })) { $missingImportant += "JDK 17 (javac)" }
if (-not ($commands | Where-Object { $_.Name -eq "dotnet" -and $_.Found })) { $missingImportant += ".NET SDK 8+" }
if (-not (($env:ANDROID_HOME -and (Test-Path $env:ANDROID_HOME)) -or ($env:ANDROID_SDK_ROOT -and (Test-Path $env:ANDROID_SDK_ROOT)) -or (Test-Path "$env:LOCALAPPDATA\Android\Sdk") -or (Test-Path (Join-Path (Get-Location) ".tools\android-sdk")))) {
    $missingImportant += "Android SDK"
}

if ($missingImportant.Count -gt 0) {
    Write-Host "Missing required tooling:" -ForegroundColor Yellow
    $missingImportant | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "Suggested installs:"
    Write-Host "  winget install EclipseAdoptium.Temurin.17.JDK"
    Write-Host "  winget install Microsoft.DotNet.SDK.8"
    Write-Host "  winget install Google.AndroidStudio"
    exit 1
}

Write-Host "All required baseline tools were found." -ForegroundColor Green
