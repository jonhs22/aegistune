param(
    [string]$Channel = "stable",
    [string]$PublicBaseUrl = "https://updates.ichiphost.com/aegistune/stable",
    [string]$PortableZipPath = "",
    [string]$MsixPath = "",
    [string]$OutputDirectory = "",
    [string]$ReleaseNotesUrl = "",
    [string]$ReleaseNotesPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$appProjectRoot = Join-Path $repoRoot "src\AegisTune.App"
$manifestPath = Join-Path $appProjectRoot "Package.appxmanifest"

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

function Resolve-LatestFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SearchRoot,
        [Parameter(Mandatory = $true)]
        [string]$Filter
    )

    $match = Get-ChildItem -Path $SearchRoot -Filter $Filter -File -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $match) {
        throw "Could not locate a file matching $Filter under $SearchRoot"
    }

    return $match.FullName
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Resolve-ReleaseNotesSource {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$Channel,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [string]$RequestedPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return Resolve-RepoPath -Path $RequestedPath
    }

    $candidatePaths = @(
        (Join-Path $RepoRoot "docs\releases\$Channel\$Version.md"),
        (Join-Path $RepoRoot "docs\releases\$Channel\latest.md")
    )

    foreach ($candidate in $candidatePaths) {
        if (Test-Path $candidate) {
            return Resolve-RepoPath -Path $candidate
        }
    }

    return $null
}

if (-not (Test-Path $manifestPath)) {
    throw "Package manifest not found at $manifestPath"
}

[xml]$manifest = Get-Content -Path $manifestPath
$identity = $manifest.Package.Identity
$displayName = $manifest.Package.Properties.DisplayName
$publisher = $identity.Publisher
$version = $identity.Version
$appId = $manifest.Package.Applications.Application.Id

$portableZipFullPath = if ([string]::IsNullOrWhiteSpace($PortableZipPath)) {
    Resolve-LatestFile -SearchRoot (Join-Path $repoRoot "artifacts\portable") -Filter "*.zip"
}
else {
    Resolve-RepoPath -Path $PortableZipPath
}

$msixFullPath = if ([string]::IsNullOrWhiteSpace($MsixPath)) {
    Resolve-LatestFile -SearchRoot (Join-Path $repoRoot "src\AegisTune.App\AppPackages") -Filter "*.msix"
}
else {
    Resolve-RepoPath -Path $MsixPath
}

$channelDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Resolve-RepoPath -Path (Join-Path $repoRoot "artifacts\updates\$Channel")
}
else {
    Resolve-RepoPath -Path $OutputDirectory
}

if (Test-Path $channelDirectory) {
    Remove-Item -LiteralPath $channelDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $channelDirectory -Force | Out-Null

$portableFileName = [System.IO.Path]::GetFileName($portableZipFullPath)
$msixFileName = [System.IO.Path]::GetFileName($msixFullPath)
$appInstallerFileName = "AegisTune.appinstaller"
$releaseNotesFileName = "RELEASE-NOTES.md"

Copy-Item -LiteralPath $portableZipFullPath -Destination (Join-Path $channelDirectory $portableFileName)
Copy-Item -LiteralPath $msixFullPath -Destination (Join-Path $channelDirectory $msixFileName)

$normalizedBaseUrl = $PublicBaseUrl.TrimEnd('/')
$portableUrl = "$normalizedBaseUrl/$portableFileName"
$msixUrl = "$normalizedBaseUrl/$msixFileName"
$appInstallerUrl = "$normalizedBaseUrl/$appInstallerFileName"
$effectiveReleaseNotesUrl = if ([string]::IsNullOrWhiteSpace($ReleaseNotesUrl)) {
    "$normalizedBaseUrl/$releaseNotesFileName"
}
else {
    $ReleaseNotesUrl
}

$portableSha = Get-Sha256 -Path $portableZipFullPath
$msixSha = Get-Sha256 -Path $msixFullPath

$generatedReleaseNotesPath = Join-Path $channelDirectory $releaseNotesFileName
$releaseNotesSourcePath = Resolve-ReleaseNotesSource -RepoRoot $repoRoot -Channel $Channel -Version $version -RequestedPath $ReleaseNotesPath
if ($releaseNotesSourcePath) {
    Copy-Item -LiteralPath $releaseNotesSourcePath -Destination $generatedReleaseNotesPath -Force
}
else {
    @(
        "# AegisTune $version",
        "",
        "## Publish checklist",
        "",
        "Fill in the public release notes for this build before you publish the update channel.",
        "",
        "## Distribution artifacts",
        "",
        "- Version: $version",
        "- Channel: $Channel",
        "- Portable zip: $portableFileName",
        "- MSIX: $msixFileName",
        "",
        "## Highlights",
        "",
        "- Add the operator-facing product changes here.",
        "",
        "## Fixes and rollout notes",
        "",
        "- Add important compatibility, undo, or deployment notes here."
    ) | Set-Content -Path $generatedReleaseNotesPath -Encoding UTF8
}

$stableManifestPath = Join-Path $channelDirectory "stable.json"
$stableManifest = [ordered]@{
    channel = $Channel
    version = $version
    publishedAt = [DateTimeOffset]::UtcNow.ToString("o")
    notesUrl = $effectiveReleaseNotesUrl
    portable = [ordered]@{
        url = $portableUrl
        sha256 = $portableSha
    }
    msix = [ordered]@{
        url = $msixUrl
        appInstallerUrl = $appInstallerUrl
        sha256 = $msixSha
    }
}

$stableManifest | ConvertTo-Json -Depth 6 | Set-Content -Path $stableManifestPath -Encoding UTF8

$appInstallerPath = Join-Path $channelDirectory $appInstallerFileName
$appInstallerXml = @"
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller
    xmlns="http://schemas.microsoft.com/appx/appinstaller/2021"
    Uri="$appInstallerUrl"
    Version="$version">
  <MainPackage
      Name="$($identity.Name)"
      Publisher="$publisher"
      Version="$version"
      ProcessorArchitecture="x64"
      Uri="$msixUrl" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="12" ShowPrompt="true" UpdateBlocksActivation="false" />
  </UpdateSettings>
</AppInstaller>
"@

$appInstallerXml | Set-Content -Path $appInstallerPath -Encoding UTF8

$readmePath = Join-Path $channelDirectory "DEPLOY-UPDATE-FEED.txt"
@(
    "AegisTune update channel",
    "=======================",
    "",
    "Host every file from this folder at:",
    $normalizedBaseUrl,
    "",
    "Then point the app update feed URL to:",
    "$normalizedBaseUrl/stable.json",
    "",
    "For packaged installs, distribute:",
    "$normalizedBaseUrl/$appInstallerFileName",
    "",
    "Application Id:",
    $appId
) | Set-Content -Path $readmePath -Encoding UTF8

Write-Host "Prepared update channel folder: $channelDirectory"
Write-Host "stable.json: $stableManifestPath"
Write-Host "App Installer file: $appInstallerPath"
Write-Host "Portable zip copied as: $portableFileName"
Write-Host "MSIX copied as: $msixFileName"
if ($releaseNotesSourcePath) {
    Write-Host "Release notes source: $releaseNotesSourcePath"
}
