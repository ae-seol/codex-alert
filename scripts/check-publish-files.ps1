param(
    [switch]$Strict
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$commitRoots = @(
    ".gitattributes",
    ".gitignore",
    ".vscode\extensions.json",
    ".vscode\tasks.json",
    ".codex",
    "README.md",
    "android",
    "assets",
    "config\pc.example.json",
    "docs",
    "scripts",
    "windows"
)

$localOnly = @(
    "config\pc.config.json",
    "android\app\google-services.json",
    "android\local.properties",
    "android\.kotlin",
    ".tools",
    ".tmp",
    "dist",
    "history",
    "logs"
)

$secretPatterns = @(
    "*.keystore",
    "*.jks",
    "*.p12",
    "*.pfx",
    "*.pem",
    "*.key",
    "key.properties",
    "service-account*.json",
    "*service-account*.json",
    "firebase-adminsdk*.json",
    "*firebase-adminsdk*.json",
    "*-firebase-adminsdk-*.json",
    "*.env",
    ".env.*"
)

$scanRoots = @(
    ".codex",
    ".vscode",
    "android",
    "assets",
    "config",
    "docs",
    "scripts",
    "windows"
)

$skipPathRegex = '\\(\.gradle|\.kotlin|build|bin|obj|AppPackages|BundleArtifacts|publish)\\'

function Get-Git {
    $command = Get-Command git -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        return ""
    }
    return $command.Source
}

function Test-GitIgnored {
    param(
        [string]$Git,
        [string]$Path
    )

    if (-not $Git) {
        return $null
    }

    & $Git check-ignore -q -- $Path
    return ($LASTEXITCODE -eq 0)
}

Write-Host "Codex Alert publish file check"
Write-Host "Root: $root"
Write-Host ""

Write-Host "Commit candidates:"
foreach ($path in $commitRoots) {
    $exists = Test-Path -LiteralPath $path
    Write-Host ("  [{0}] {1}" -f ($(if ($exists) { "ok" } else { "missing" })), $path)
}
Write-Host ""

$git = Get-Git
if (-not $git) {
    Write-Host "Git was not found in PATH. Install Git before running check-ignore or pushing." -ForegroundColor Yellow
} else {
    Write-Host "Git: $git"
}
Write-Host ""

$failures = @()
Write-Host "Local-only paths:"
foreach ($path in $localOnly) {
    $exists = Test-Path -LiteralPath $path
    $ignored = Test-GitIgnored -Git $git -Path $path
    $ignoredText = if ($null -eq $ignored) { "not checked" } elseif ($ignored) { "ignored" } else { "NOT ignored" }
    Write-Host ("  [{0}] {1} - {2}" -f ($(if ($exists) { "exists" } else { "absent" })), $path, $ignoredText)
    if ($exists -and $false -eq $ignored) {
        $failures += $path
    }
}
Write-Host ""

Write-Host "Secret pattern matches:"
$rootFiles = Get-ChildItem -Force -File -ErrorAction SilentlyContinue
$scanFiles = @($rootFiles)
foreach ($scanRoot in $scanRoots) {
    if (-not (Test-Path -LiteralPath $scanRoot)) {
        continue
    }

    $scanFiles += Get-ChildItem -LiteralPath $scanRoot -Recurse -Force -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch $skipPathRegex }
}

foreach ($pattern in $secretPatterns) {
    $matches = $scanFiles | Where-Object { $_.Name -like $pattern }

    if (-not $matches) {
        Write-Host "  [none] $pattern"
        continue
    }

    foreach ($match in $matches) {
        $relative = Resolve-Path -LiteralPath $match.FullName -Relative
        $ignored = Test-GitIgnored -Git $git -Path $relative
        $ignoredText = if ($null -eq $ignored) { "not checked" } elseif ($ignored) { "ignored" } else { "NOT ignored" }
        Write-Host "  [found] $relative - $ignoredText"
        if ($false -eq $ignored) {
            $failures += $relative
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Publish check failed. These sensitive/local files are not ignored:" -ForegroundColor Red
    $failures | Sort-Object -Unique | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

if ($Strict -and -not $git) {
    Write-Host ""
    Write-Host "Strict mode failed because Git is unavailable." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Publish check completed." -ForegroundColor Green
