param(
    [string]$Channel = "stable",
    [string]$ProductPath = "aegistune",
    [string]$SiteRoot = "",
    [string]$PublicBaseUrl = "https://updates.ichiphost.com/aegistune/stable",
    [string]$CName = "",
    [string]$PortableZipPath = "",
    [string]$MsixPath = "",
    [string]$ReleaseNotesUrl = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$prepareScriptPath = Join-Path $PSScriptRoot "prepare-update-channel.ps1"

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

function Get-SafeSiteSegment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $normalized = $Value.Trim().Trim('/').Trim('\')
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        throw "A non-empty site path segment is required."
    }

    return ($normalized -replace '[\\/]+', '\')
}

function Write-TextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $Content | Set-Content -Path $Path -Encoding UTF8
}

if (-not (Test-Path $prepareScriptPath)) {
    throw "prepare-update-channel.ps1 was not found at $prepareScriptPath"
}

$siteRootFullPath = if ([string]::IsNullOrWhiteSpace($SiteRoot)) {
    Resolve-RepoPath -Path (Join-Path $repoRoot "artifacts\pages-site")
}
else {
    Resolve-RepoPath -Path $SiteRoot
}

$normalizedProductPath = Get-SafeSiteSegment -Value $ProductPath
$normalizedBaseUrl = $PublicBaseUrl.Trim().TrimEnd('/')
if ([string]::IsNullOrWhiteSpace($normalizedBaseUrl)) {
    throw "PublicBaseUrl cannot be empty."
}

if (Test-Path $siteRootFullPath) {
    Remove-Item -LiteralPath $siteRootFullPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $siteRootFullPath | Out-Null

$channelOutputDirectory = Resolve-RepoPath -Path (Join-Path $siteRootFullPath (Join-Path $normalizedProductPath $Channel))

$prepareArgs = @(
    "-NoProfile"
    "-ExecutionPolicy"
    "Bypass"
    "-File"
    $prepareScriptPath
    "-Channel"
    $Channel
    "-PublicBaseUrl"
    $normalizedBaseUrl
    "-OutputDirectory"
    $channelOutputDirectory
)

if (-not [string]::IsNullOrWhiteSpace($PortableZipPath)) {
    $prepareArgs += @("-PortableZipPath", $PortableZipPath)
}

if (-not [string]::IsNullOrWhiteSpace($MsixPath)) {
    $prepareArgs += @("-MsixPath", $MsixPath)
}

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesUrl)) {
    $prepareArgs += @("-ReleaseNotesUrl", $ReleaseNotesUrl)
}

& powershell @prepareArgs
if ($LASTEXITCODE -ne 0) {
    throw "prepare-update-channel.ps1 failed with exit code $LASTEXITCODE."
}

$stableManifestPath = Join-Path $channelOutputDirectory "stable.json"
if (-not (Test-Path $stableManifestPath)) {
    throw "Expected stable.json at $stableManifestPath after staging."
}

$stableManifest = Get-Content -Path $stableManifestPath -Raw | ConvertFrom-Json
$productLandingDirectory = Resolve-RepoPath -Path (Join-Path $siteRootFullPath $normalizedProductPath)
$relativeChannelPath = (($normalizedProductPath -replace '\\', '/') + "/$Channel").TrimStart('/')

$rootIndexContent = @"
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta http-equiv="refresh" content="0; url=./$($normalizedProductPath -replace '\\', '/')/" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>AegisTune update feed</title>
  </head>
  <body>
    <p>Redirecting to <a href="./$($normalizedProductPath -replace '\\', '/')/">AegisTune update feed</a>.</p>
  </body>
</html>
"@
Write-TextFile -Path (Join-Path $siteRootFullPath "index.html") -Content $rootIndexContent

$productIndexContent = @"
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>AegisTune update channel</title>
    <style>
      :root {
        color-scheme: light dark;
        --bg: #f3f6fb;
        --panel: rgba(255, 255, 255, 0.92);
        --text: #132238;
        --muted: #50627b;
        --accent: #0a6c8e;
        --accent-strong: #0d8f79;
        --border: rgba(19, 34, 56, 0.12);
      }
      @media (prefers-color-scheme: dark) {
        :root {
          --bg: #0f141d;
          --panel: rgba(22, 30, 43, 0.94);
          --text: #e7eef7;
          --muted: #a8b7c9;
          --border: rgba(231, 238, 247, 0.12);
        }
      }
      * { box-sizing: border-box; }
      body {
        margin: 0;
        font-family: "Segoe UI", "Inter", sans-serif;
        background:
          radial-gradient(circle at top left, rgba(13, 143, 121, 0.12), transparent 36%),
          radial-gradient(circle at top right, rgba(10, 108, 142, 0.14), transparent 30%),
          var(--bg);
        color: var(--text);
      }
      main {
        max-width: 980px;
        margin: 0 auto;
        padding: 48px 24px 72px;
      }
      .hero, .panel {
        background: var(--panel);
        border: 1px solid var(--border);
        border-radius: 24px;
        box-shadow: 0 22px 60px rgba(16, 22, 34, 0.08);
      }
      .hero {
        padding: 32px;
        margin-bottom: 24px;
      }
      .eyebrow {
        font-size: 12px;
        letter-spacing: 0.14em;
        text-transform: uppercase;
        color: var(--accent);
        font-weight: 700;
      }
      h1 {
        margin: 12px 0 8px;
        font-size: clamp(2rem, 5vw, 3rem);
        line-height: 1.08;
      }
      p {
        margin: 0;
        color: var(--muted);
        line-height: 1.65;
      }
      .cta-row, .meta-grid {
        display: grid;
        gap: 16px;
      }
      .cta-row {
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        margin-top: 24px;
      }
      .cta {
        display: block;
        padding: 18px 18px 16px;
        border-radius: 18px;
        text-decoration: none;
        color: inherit;
        border: 1px solid var(--border);
        background: rgba(255, 255, 255, 0.5);
      }
      .cta strong {
        display: block;
        margin-bottom: 6px;
        font-size: 1rem;
      }
      .cta span {
        color: var(--muted);
        font-size: 0.95rem;
      }
      .cta.primary {
        border-color: rgba(13, 143, 121, 0.28);
        background: linear-gradient(135deg, rgba(13, 143, 121, 0.12), rgba(10, 108, 142, 0.1));
      }
      .panel {
        padding: 24px;
      }
      .meta-grid {
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        margin-top: 18px;
      }
      .meta-label {
        font-size: 0.82rem;
        color: var(--muted);
        margin-bottom: 4px;
      }
      .mono {
        font-family: "Cascadia Mono", "Consolas", monospace;
        word-break: break-word;
      }
    </style>
  </head>
  <body>
    <main>
      <section class="hero">
        <div class="eyebrow">Update Channel</div>
        <h1>AegisTune stable updates</h1>
        <p>Use this page as the single public update point for packaged installs, portable downloads, release notes, and machine-readable update manifests.</p>
        <div class="cta-row">
          <a class="cta primary" href="./$Channel/AegisTune.appinstaller">
            <strong>Install or update packaged build</strong>
            <span>Launches the MSIX App Installer flow for the current stable release.</span>
          </a>
          <a class="cta" href="./$Channel/$(Split-Path -Leaf $stableManifest.portable.url)">
            <strong>Download portable zip</strong>
            <span>Gets the current portable bundle without going through Microsoft App Installer.</span>
          </a>
          <a class="cta" href="./$Channel/RELEASE-NOTES.md">
            <strong>Read release notes</strong>
            <span>Open the public notes shipped with this stable channel publish.</span>
          </a>
        </div>
      </section>
      <section class="panel">
        <div class="eyebrow">Current Stable</div>
        <h2 style="margin: 10px 0 8px;">Version $($stableManifest.version)</h2>
        <p>The app can poll <span class="mono">$normalizedBaseUrl/stable.json</span> directly. Humans can use the buttons above.</p>
        <div class="meta-grid">
          <div>
            <div class="meta-label">Stable manifest</div>
            <div class="mono"><a href="./$Channel/stable.json">./$relativeChannelPath/stable.json</a></div>
          </div>
          <div>
            <div class="meta-label">MSIX App Installer</div>
            <div class="mono"><a href="./$Channel/AegisTune.appinstaller">./$relativeChannelPath/AegisTune.appinstaller</a></div>
          </div>
          <div>
            <div class="meta-label">Published at</div>
            <div class="mono">$($stableManifest.publishedAt)</div>
          </div>
          <div>
            <div class="meta-label">Feed base URL</div>
            <div class="mono">$normalizedBaseUrl</div>
          </div>
        </div>
      </section>
    </main>
  </body>
</html>
"@
Write-TextFile -Path (Join-Path $productLandingDirectory "index.html") -Content $productIndexContent

$noJekyllPath = Join-Path $siteRootFullPath ".nojekyll"
New-Item -ItemType File -Force -Path $noJekyllPath | Out-Null

if (-not [string]::IsNullOrWhiteSpace($CName)) {
    Write-TextFile -Path (Join-Path $siteRootFullPath "CNAME") -Content ($CName.Trim())
}

$siteMetrics = Get-ChildItem -Path $siteRootFullPath -File -Recurse | Measure-Object -Property Length -Sum
$siteSizeMb = [Math]::Round(($siteMetrics.Sum / 1MB), 2)

Write-Host "Pages site staged at: $siteRootFullPath"
Write-Host "Channel folder: $channelOutputDirectory"
Write-Host "Product landing page: $(Join-Path $productLandingDirectory 'index.html')"
Write-Host "Total staged site size: $siteSizeMb MB"
