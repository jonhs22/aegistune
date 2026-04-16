param(
    [Parameter(Mandatory = $true)]
    [string]$BucketName,
    [string]$Channel = "stable",
    [string]$ProductPath = "aegistune",
    [string]$SiteRoot = "",
    [string]$PublicBaseUrl = "https://updates.ichiphost.com/aegistune/stable",
    [string]$PortableZipPath = "",
    [string]$MsixPath = "",
    [string]$ReleaseNotesUrl = "",
    [string]$WranglerBin = "",
    [switch]$DryRun,
    [switch]$SkipStage
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$stageScriptPath = Join-Path $PSScriptRoot "stage-update-feed-for-pages.ps1"

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

function Resolve-WranglerCommand {
    param(
        [string]$RequestedPath,
        [switch]$AllowPlaceholder
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return @($RequestedPath)
    }

    $candidates = @(
        (Get-Command wrangler.cmd -ErrorAction SilentlyContinue),
        (Get-Command wrangler -ErrorAction SilentlyContinue),
        (Get-Command npx.cmd -ErrorAction SilentlyContinue),
        (Get-Command npx -ErrorAction SilentlyContinue)
    ) | Where-Object { $_ }

    foreach ($candidate in $candidates) {
        if ($candidate.Name -like 'npx*') {
            return @($candidate.Source, "wrangler")
        }

        return @($candidate.Source)
    }

    if ($AllowPlaceholder) {
        return @("wrangler")
    }

    throw 'Wrangler was not found. Install it with `npm install -g wrangler` or pass -WranglerBin.'
}

function Get-ContentType {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    switch ([System.IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        ".json" { return "application/json" }
        ".html" { return "text/html; charset=utf-8" }
        ".txt" { return "text/plain; charset=utf-8" }
        ".md" { return "text/markdown; charset=utf-8" }
        ".appinstaller" { return "application/xml; charset=utf-8" }
        ".msix" { return "application/msix" }
        ".zip" { return "application/zip" }
        default { return "application/octet-stream" }
    }
}

function Get-CacheControl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    switch ([System.IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        ".msix" { return "public, max-age=31536000, immutable" }
        ".zip" { return "public, max-age=31536000, immutable" }
        ".html" { return "public, max-age=300" }
        default { return "no-cache" }
    }
}

function Get-RelativeFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    $normalizedRoot = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\') + '\'
    $normalizedFile = [System.IO.Path]::GetFullPath($FilePath)

    $rootUri = [System.Uri]::new($normalizedRoot)
    $fileUri = [System.Uri]::new($normalizedFile)
    return $rootUri.MakeRelativeUri($fileUri).ToString()
}

$siteRootFullPath = if ([string]::IsNullOrWhiteSpace($SiteRoot)) {
    Resolve-RepoPath -Path (Join-Path $repoRoot "artifacts\r2-site")
}
else {
    Resolve-RepoPath -Path $SiteRoot
}

if (-not $SkipStage) {
    $stageArgs = @(
        "-NoProfile"
        "-ExecutionPolicy"
        "Bypass"
        "-File"
        $stageScriptPath
        "-Channel"
        $Channel
        "-ProductPath"
        $ProductPath
        "-SiteRoot"
        $siteRootFullPath
        "-PublicBaseUrl"
        $PublicBaseUrl
    )

    if (-not [string]::IsNullOrWhiteSpace($PortableZipPath)) {
        $stageArgs += @("-PortableZipPath", $PortableZipPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($MsixPath)) {
        $stageArgs += @("-MsixPath", $MsixPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesUrl)) {
        $stageArgs += @("-ReleaseNotesUrl", $ReleaseNotesUrl)
    }

    & powershell @stageArgs
    if ($LASTEXITCODE -ne 0) {
        throw "stage-update-feed-for-pages.ps1 failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path $siteRootFullPath)) {
    throw "The staged site root was not found at $siteRootFullPath"
}

$wranglerCommand = Resolve-WranglerCommand -RequestedPath $WranglerBin -AllowPlaceholder:$DryRun
$siteFiles = Get-ChildItem -Path $siteRootFullPath -File -Recurse | Sort-Object FullName
if ($siteFiles.Count -eq 0) {
    throw "No staged files were found under $siteRootFullPath"
}

foreach ($file in $siteFiles) {
    $relativePath = Get-RelativeFilePath -RootPath $siteRootFullPath -FilePath $file.FullName
    $objectTarget = "$BucketName/$relativePath"
    $contentType = Get-ContentType -Path $file.FullName
    $cacheControl = Get-CacheControl -Path $file.FullName
    $commandLine = @()
    $commandLine += $wranglerCommand
    $commandLine += @(
        "r2"
        "object"
        "put"
        $objectTarget
        "--file"
        $file.FullName
        "--content-type"
        $contentType
        "--cache-control"
        $cacheControl
    )

    if ($DryRun) {
        Write-Host "[dry-run] $($commandLine -join ' ')"
        continue
    }

    $command = $commandLine[0]
    $arguments = @()
    if ($commandLine.Length -gt 1) {
        $arguments = $commandLine[1..($commandLine.Length - 1)]
    }

    & $command @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Wrangler upload failed for $relativePath with exit code $LASTEXITCODE."
    }
}

Write-Host "Uploaded update site from $siteRootFullPath to R2 bucket $BucketName"
