param(
    [switch]$Json
)

$ErrorActionPreference = "SilentlyContinue"

function New-Candidate {
    param(
        [string]$AppId,
        [string]$Name,
        [string]$Source,
        [string]$Confidence,
        [string]$Path = ""
    )

    [pscustomobject]@{
        AppId = $AppId
        Name = $Name
        Source = $Source
        Confidence = $Confidence
        Path = $Path
    }
}

$candidates = New-Object System.Collections.Generic.List[object]

try {
    Get-StartApps |
        Where-Object {
            $_.Name -match "Codex|OpenAI" -or $_.AppID -match "Codex|OpenAI"
        } |
        ForEach-Object {
            $confidence = if ($_.Name -eq "Codex" -or $_.AppID -match "OpenAI\.Codex") { "high" } else { "medium" }
            $candidates.Add((New-Candidate -AppId $_.AppID -Name $_.Name -Source "Start menu app registration" -Confidence $confidence))
        }
} catch {
}

try {
    Get-Process |
        Where-Object {
            $_.ProcessName -match "^Codex$|^codex$" -or $_.Path -match "OpenAI\\Codex|OpenAI\.Codex|\\Codex\\|\\Codex\.exe"
        } |
        ForEach-Object {
            $path = $_.Path
            $packageFolder = $null
            if ($path -match "WindowsApps\\([^\\]+)__([^\\]+)\\") {
                $prefixParts = $matches[1].Split("_")
                $prefix = if ($prefixParts.Count -gt 0) { $prefixParts[0] } else { "" }
                $publisher = $matches[2]
                if ($prefix -and $publisher) {
                    $packageFamily = "$prefix`_$publisher"
                    $derivedAppId = "$packageFamily!App"
                    $candidates.Add((New-Candidate -AppId $derivedAppId -Name "Codex" -Source "Running process package path" -Confidence "medium" -Path $path))
                }
            }
            $candidates.Add((New-Candidate -AppId "" -Name $_.ProcessName -Source "Running process" -Confidence "low" -Path $path))
        }
} catch {
}

$deduped = $candidates |
    Where-Object { $_.AppId -or $_.Path } |
    Sort-Object AppId, Source -Unique |
    Sort-Object @{ Expression = { switch ($_.Confidence) { "high" { 0 } "medium" { 1 } default { 2 } } } }, AppId

if ($Json) {
    $deduped | ConvertTo-Json -Depth 4
    exit 0
}

if (-not $deduped -or $deduped.Count -eq 0) {
    Write-Host "No Codex AppID candidates found."
    Write-Host ""
    Write-Host "Start Codex and run this script again. If detection still fails, use the Windows relay Observe notification sources action while triggering a Codex toast."
    exit 1
}

Write-Host "Found Codex candidates:"
Write-Host ""
$index = 1
foreach ($candidate in $deduped) {
    $appId = if ($candidate.AppId) { $candidate.AppId } else { "(no AppID derived)" }
    Write-Host "[$index] $appId"
    Write-Host "    Name: $($candidate.Name)"
    Write-Host "    Source: $($candidate.Source)"
    Write-Host "    Confidence: $($candidate.Confidence)"
    if ($candidate.Path) {
        Write-Host "    Path: $($candidate.Path)"
    }
    Write-Host ""
    $index++
}

$best = $deduped | Where-Object { $_.AppId } | Select-Object -First 1
if ($best) {
    Write-Host "Suggested config:"
    Write-Host "  allowedAppIds = [`"$($best.AppId)`"]"
}
