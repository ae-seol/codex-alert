param(
    [string]$RepoUrl = "https://github.com/ae-seol/codex-alert.git",
    [string]$Destination = "",
    [string]$Ref = "main",
    [string]$ExpectedCommitSha = "",
    [switch]$AllowOtherOwner,
    [switch]$AllowDirtyExistingCheckout
)

$ErrorActionPreference = "Stop"

function Get-Git {
    $command = Get-Command git -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Git was not found in PATH. Install Git first, then restart PowerShell."
    }
    return $command.Source
}

function Test-AuthorizedRepoUrl {
    param([string]$Url)

    if ($AllowOtherOwner) {
        return $true
    }

    return (
        $Url -like "https://github.com/ae-seol/*" -or
        $Url -like "git@github.com:ae-seol/*"
    )
}

function Invoke-Git {
    param(
        [string]$Git,
        [string[]]$Arguments
    )

    & $Git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-AuthorizedRepoUrl -Url $RepoUrl)) {
    throw "Refusing to download a repository outside ae-seol. Pass -AllowOtherOwner only for an explicitly authorized repository."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path (Split-Path $repoRoot -Parent) "codex-alert-review"
}

$git = Get-Git
$destinationFullPath = [System.IO.Path]::GetFullPath($Destination)
$parent = Split-Path $destinationFullPath -Parent
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
}

$gitDirectory = Join-Path $destinationFullPath ".git"
if (Test-Path -LiteralPath $destinationFullPath) {
    $children = Get-ChildItem -LiteralPath $destinationFullPath -Force -ErrorAction SilentlyContinue
    if ($children.Count -gt 0 -and -not (Test-Path -LiteralPath $gitDirectory)) {
        throw "Destination exists and is not an empty Git checkout: $destinationFullPath"
    }

    $status = & $git -C $destinationFullPath status --porcelain 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Destination exists but Git status failed: $destinationFullPath"
    }
    if ($status -and -not $AllowDirtyExistingCheckout) {
        throw "Destination has local changes. Review them or pass -AllowDirtyExistingCheckout: $destinationFullPath"
    }

    $remoteUrl = (& $git -C $destinationFullPath remote get-url origin 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw "Existing checkout has no origin remote: $destinationFullPath"
    }
    if ($remoteUrl -ne $RepoUrl) {
        throw "Existing checkout origin differs. Expected '$RepoUrl' but found '$remoteUrl'."
    }

    Invoke-Git -Git $git -Arguments @("-C", $destinationFullPath, "fetch", "--prune", "origin")
} else {
    Invoke-Git -Git $git -Arguments @("clone", "--no-checkout", $RepoUrl, $destinationFullPath)
    Invoke-Git -Git $git -Arguments @("-C", $destinationFullPath, "fetch", "--prune", "origin")
}

$target = $Ref
& $git -C $destinationFullPath rev-parse --verify "$Ref^{commit}" *> $null
if ($LASTEXITCODE -ne 0) {
    Invoke-Git -Git $git -Arguments @("-C", $destinationFullPath, "fetch", "origin", $Ref)
    $target = "FETCH_HEAD"
}

Invoke-Git -Git $git -Arguments @("-C", $destinationFullPath, "checkout", $target)

$commit = (& $git -C $destinationFullPath rev-parse HEAD).Trim()
$branch = (& $git -C $destinationFullPath branch --show-current).Trim()
$remote = (& $git -C $destinationFullPath remote get-url origin).Trim()

if ($ExpectedCommitSha) {
    if (-not $commit.StartsWith($ExpectedCommitSha, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Checked out commit $commit, which does not match expected prefix $ExpectedCommitSha."
    }
}

Write-Host "Repository downloaded for review."
Write-Host "Destination: $destinationFullPath"
Write-Host "Remote: $remote"
Write-Host "Branch: $(if ($branch) { $branch } else { '(detached)' })"
Write-Host "Commit: $commit"
Write-Host ""
Write-Host "Stopped before running repository scripts. Inspect before build/test execution."
