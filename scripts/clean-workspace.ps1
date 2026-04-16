param(
    [ValidateRange(1, 20)]
    [int]$KeepPackageCount = 1
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

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

function Remove-RepoDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $fullPath = Resolve-RepoPath -Path $Path
    Write-Host "Removing directory: $fullPath"
    Remove-Item -LiteralPath $fullPath -Recurse -Force
}

function Remove-RepoFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $fullPath = Resolve-RepoPath -Path $Path
    Write-Host "Removing file: $fullPath"
    Remove-Item -LiteralPath $fullPath -Force
}

Write-Host "Cleaning transient build outputs under $repoRoot"

$transientDirectories = Get-ChildItem -Path $repoRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @("bin", "obj") }

foreach ($directory in $transientDirectories) {
    Remove-RepoDirectory -Path $directory.FullName
}

$appPackagesRoot = Join-Path $repoRoot "src\AegisTune.App\AppPackages"
if (Test-Path $appPackagesRoot) {
    $packageDirectories = Get-ChildItem -Path $appPackagesRoot -Directory |
        Sort-Object LastWriteTime -Descending

    $stalePackages = $packageDirectories | Select-Object -Skip $KeepPackageCount
    foreach ($directory in $stalePackages) {
        Remove-RepoDirectory -Path $directory.FullName
    }
}

Remove-RepoFile -Path (Join-Path $repoRoot "artifacts\xaml-build-diag.log")

Write-Host "Workspace cleanup completed. Preserved certificates and the newest $KeepPackageCount AppPackages folder(s)."
