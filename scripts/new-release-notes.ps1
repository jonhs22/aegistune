param(
    [string]$Channel = "stable",
    [string]$Version = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$manifestPath = Join-Path $repoRoot "src\AegisTune.App\Package.appxmanifest"

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the repo root: $fullPath"
    }

    return $fullPath
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (-not (Test-Path $manifestPath)) {
        throw "Package manifest not found at $manifestPath"
    }

    [xml]$manifest = Get-Content -Path $manifestPath
    $Version = $manifest.Package.Identity.Version
}

$resolvedOutputPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Resolve-RepoPath -Path (Join-Path $repoRoot "docs\releases\$Channel\$Version.md")
}
else {
    Resolve-RepoPath -Path $OutputPath
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

if (Test-Path $resolvedOutputPath) {
    throw "Release notes file already exists at $resolvedOutputPath"
}

@(
    "# AegisTune $Version",
    "",
    "## Highlights",
    "",
    "- Describe the user-facing improvements in this release.",
    "",
    "## Guided workflow changes",
    "",
    "- Note new review flows, buttons, or default behaviors.",
    "",
    "## Fixes",
    "",
    "- Note important bugs or regressions that were fixed.",
    "",
    "## Operator notes",
    "",
    "- Call out any restore-point, signing, deployment, or trust considerations.",
    "",
    "## Artifacts",
    "",
    "- Channel: $Channel",
    "- Version: $Version"
) | Set-Content -Path $resolvedOutputPath -Encoding UTF8

Write-Host "Created release notes template: $resolvedOutputPath"
