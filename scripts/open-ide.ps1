param(
    [ValidateSet("vscode", "android-studio", "both")]
    [string]$Ide = "vscode"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent

if ($Ide -eq "vscode" -or $Ide -eq "both") {
    $code = Get-Command code -ErrorAction SilentlyContinue
    if ($code) {
        & $code.Source $repoRoot
    } else {
        Write-Host "VS Code command 'code' was not found." -ForegroundColor Yellow
    }
}

if ($Ide -eq "android-studio" -or $Ide -eq "both") {
    $studio = "C:\Program Files\Android\Android Studio\bin\studio64.exe"
    if (Test-Path $studio) {
        Start-Process -FilePath $studio -ArgumentList (Join-Path $repoRoot "android")
    } else {
        Write-Host "Android Studio was not found at $studio." -ForegroundColor Yellow
    }
}
