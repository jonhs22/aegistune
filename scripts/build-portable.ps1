param(
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64",
    [string]$Project = ".\src\AegisTune.App\AegisTune.App.csproj"
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Project))
$projectDirectory = Split-Path -Parent $projectPath
$manifestPath = Join-Path $projectDirectory "Package.appxmanifest"
$runtimeIdentifier = "win-$($Platform.ToLowerInvariant())"
$artifactsRoot = Join-Path $repoRoot "artifacts\portable"

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

if (-not (Test-Path $projectPath)) {
    throw "Project not found at $projectPath"
}

if (-not (Test-Path $manifestPath)) {
    throw "Package manifest not found at $manifestPath"
}

[xml]$manifest = Get-Content -Path $manifestPath
$appVersion = $manifest.Package.Identity.Version
$portableName = "AegisTune-$appVersion-$runtimeIdentifier-portable"
$portableDirectory = Resolve-RepoPath -Path (Join-Path $artifactsRoot $portableName)
$zipPath = Resolve-RepoPath -Path (Join-Path $artifactsRoot "$portableName.zip")
$readmePath = Join-Path $portableDirectory "README-PORTABLE.txt"

function Stop-PortableProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PortableRoot
    )

    $runningPortableProcesses = Get-Process AegisTune.App -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -and $_.Path.StartsWith($PortableRoot, [System.StringComparison]::OrdinalIgnoreCase)
        }

    foreach ($process in $runningPortableProcesses) {
        Write-Host "Stopping running portable process: $($process.Path)"
        Stop-Process -Id $process.Id -Force
        try {
            Wait-Process -Id $process.Id -Timeout 10 -ErrorAction Stop
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }
}

function Remove-PortableDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force
            return
        }
        catch {
            if ($attempt -eq 5) {
                throw
            }

            Start-Sleep -Seconds 2
        }
    }
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
if (Test-Path $portableDirectory) {
    Stop-PortableProcesses -PortableRoot $portableDirectory
    Remove-PortableDirectory -Path $portableDirectory
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Write-Host "[1/4] Publishing unpackaged portable output..."
$publishArgs = @(
    "publish"
    $projectPath
    "-c"
    $Configuration
    "-r"
    $runtimeIdentifier
    "-p:Platform=$Platform"
    "-p:PublishProfile=$runtimeIdentifier.pubxml"
    "-p:WindowsPackageType=None"
    "-p:WindowsAppSDKSelfContained=true"
    "-p:WindowsAppSdkDeploymentManagerInitialize=false"
    "-p:PublishSingleFile=false"
    "-p:PublishDir=$portableDirectory"
)

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $portableDirectory "AegisTune.App.exe"
if (-not (Test-Path $exePath)) {
    throw "Portable publish completed without producing $exePath"
}

Write-Host "[2/4] Writing portable readme..."
$readmeLines = @(
    "AegisTune Portable",
    "=================",
    "",
    "Version: $appVersion",
    "Architecture: $runtimeIdentifier",
    "",
    "Run AegisTune.App.exe to start the portable build.",
    "Keep all files from this folder together.",
    "Admin-only maintenance actions still require elevation when the app asks for them.",
    "For installed distribution, keep using the MSIX package."
)
$readmeLines | Set-Content -Path $readmePath -Encoding UTF8

Write-Host "[3/4] Creating portable zip..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $portableDirectory,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true)

Write-Host "[4/4] Done."
Write-Host "Portable directory: $portableDirectory"
Write-Host "Portable executable: $exePath"
Write-Host "Portable zip: $zipPath"
