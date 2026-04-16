param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Project = ".\src\AegisTune.App\AegisTune.App.csproj",
    [ValidateRange(1, 20)]
    [int]$KeepPackageCount = 2,
    [string]$CodeSigningPfxPath = "",
    [string]$CodeSigningPfxPassword = "",
    [string]$TimestampUrl = "",
    [switch]$RequireTrustedCertificate
)

$ErrorActionPreference = "Stop"
$projectDirectory = Split-Path -Parent $Project
$manifestPath = Join-Path $projectDirectory "Package.appxmanifest"
$sdkResolverPath = Join-Path $PSScriptRoot "Resolve-WindowsSdkTools.ps1"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$runtimeIdentifier = "win-$($Platform.ToLowerInvariant())"

if (-not (Test-Path $manifestPath)) {
    throw "Package manifest not found at $manifestPath"
}

if (Test-Path $sdkResolverPath) {
    . $sdkResolverPath
}

[xml]$manifest = Get-Content -Path $manifestPath
$publisherSubject = $manifest.Package.Identity.Publisher

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

function Prune-StalePackageFolders {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagesRoot,
        [Parameter(Mandatory = $true)]
        [int]$KeepCount
    )

    if (-not (Test-Path $PackagesRoot)) {
        return
    }

    $packageDirectories = Get-ChildItem -Path $PackagesRoot -Directory |
        Sort-Object LastWriteTime -Descending

    $stalePackages = $packageDirectories | Select-Object -Skip $KeepCount
    foreach ($directory in $stalePackages) {
        $fullPath = Resolve-RepoPath -Path $directory.FullName
        Write-Host "Pruning stale package folder: $fullPath"
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

function Remove-TransientDiagnosticLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArtifactsDirectory
    )

    $diagnosticLog = Join-Path $ArtifactsDirectory "xaml-build-diag.log"
    if (-not (Test-Path $diagnosticLog)) {
        return
    }

    $fullPath = Resolve-RepoPath -Path $diagnosticLog
    Write-Host "Removing stale diagnostic log: $fullPath"
    Remove-Item -LiteralPath $fullPath -Force
}

function Ensure-CodeSigningCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Subject
    )

    $certificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Subject } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($certificate) {
        return $certificate
    }

    Write-Host "Creating local code-signing certificate for $Subject"
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Subject `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature `
        -TextExtension @(
            "2.5.29.19={critical}{text}ca=false",
            "2.5.29.37={critical}{text}1.3.6.1.5.5.7.3.3"
        )

    $artifactsDirectory = Join-Path (Resolve-Path ".") "artifacts"
    New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null

    $certificateFileName = ($Subject -replace '[^A-Za-z0-9]+', '-') + ".cer"
    $certificatePath = Join-Path $artifactsDirectory $certificateFileName
    Export-Certificate -Cert $certificate -FilePath $certificatePath -Force | Out-Null
    Import-Certificate -FilePath $certificatePath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
    Import-Certificate -FilePath $certificatePath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null

    try {
        Import-Certificate -FilePath $certificatePath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
        Import-Certificate -FilePath $certificatePath -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
    }
    catch {
        Write-Warning "Machine-wide certificate trust could not be updated automatically. Current-user trust was installed."
    }

    return $certificate
}

function Load-CodeSigningPfxCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Password
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path $fullPath)) {
        throw "Trusted code-signing PFX was not found at $fullPath"
    }

    try {
        $flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable -bor
            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet

        if ([string]::IsNullOrWhiteSpace($Password)) {
            return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($fullPath, $null, $flags)
        }

        return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($fullPath, $Password, $flags)
    }
    catch {
        throw "Failed to load trusted code-signing certificate from $fullPath. $($_.Exception.Message)"
    }
}

function Resolve-CodeSigningMaterial {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPublisher,
        [string]$TrustedPfxPath,
        [string]$TrustedPfxPassword,
        [switch]$RequireTrustedCertificate
    )

    if (-not [string]::IsNullOrWhiteSpace($TrustedPfxPath)) {
        $trustedCertificate = Load-CodeSigningPfxCertificate -Path $TrustedPfxPath -Password $TrustedPfxPassword
        if (-not [string]::Equals($trustedCertificate.Subject, $ManifestPublisher, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Trusted code-signing certificate subject '$($trustedCertificate.Subject)' does not match the package manifest publisher '$ManifestPublisher'. Update Package.appxmanifest to the real publisher subject before producing public MSIX releases."
        }

        return @{
            Mode = "TrustedPfx"
            Subject = $trustedCertificate.Subject
            Thumbprint = $trustedCertificate.Thumbprint
            PfxPath = [System.IO.Path]::GetFullPath($TrustedPfxPath)
            Password = $TrustedPfxPassword
        }
    }

    if ($RequireTrustedCertificate) {
        throw "Trusted MSIX signing was required, but no code-signing PFX path was provided."
    }

    $certificate = Ensure-CodeSigningCertificate -Subject $ManifestPublisher
    return @{
        Mode = "LocalSelfSigned"
        Subject = $certificate.Subject
        Thumbprint = $certificate.Thumbprint
        Certificate = $certificate
    }
}

Prune-StalePackageFolders -PackagesRoot (Join-Path $projectDirectory "AppPackages") -KeepCount $KeepPackageCount
Remove-TransientDiagnosticLog -ArtifactsDirectory (Join-Path $repoRoot "artifacts")

Write-Host "[1/4] Restoring packages..."
& dotnet restore $Project -r $runtimeIdentifier -p:Platform=$Platform -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

Write-Host "[2/4] Building solution..."
& dotnet build $Project -c $Configuration -p:Platform=$Platform -r $runtimeIdentifier
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

Write-Host "[3/4] Publishing packaged artifact..."
$packageBuildStartedAtUtc = [DateTime]::UtcNow
& dotnet msbuild $Project `
  /t:Restore,Build `
  /p:Configuration=$Configuration `
  /p:Platform=$Platform `
  /p:RuntimeIdentifier=$runtimeIdentifier `
  /p:GenerateAppxPackageOnBuild=true `
  /p:AppxBundle=Never `
  /p:UapAppxPackageBuildMode=SideloadOnly `
  /p:AppxSymbolPackageEnabled=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet msbuild packaging failed with exit code $LASTEXITCODE."
}

$packagePath = Get-ChildItem (Join-Path $projectDirectory "AppPackages") -Recurse -Filter *.msix |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1 -ExpandProperty FullName

if (-not $packagePath) {
    throw "Packaging completed without producing an MSIX package."
}

$packageItem = Get-Item -LiteralPath $packagePath
if ($packageItem.LastWriteTimeUtc -lt $packageBuildStartedAtUtc.AddSeconds(-5)) {
    throw "Refusing to continue with a stale MSIX artifact: $packagePath"
}

$signingMaterial = Resolve-CodeSigningMaterial `
    -ManifestPublisher $publisherSubject `
    -TrustedPfxPath $CodeSigningPfxPath `
    -TrustedPfxPassword $CodeSigningPfxPassword `
    -RequireTrustedCertificate:$RequireTrustedCertificate

Write-Host "[4/5] Signing packaged artifact for $publisherSubject..."
$signature = if ($packagePath) { Get-AuthenticodeSignature $packagePath } else { $null }
$toolchain = if (Get-Command Resolve-WindowsSdkTools -ErrorAction SilentlyContinue) {
  Resolve-WindowsSdkTools -RequiredTools @("signtool")
}
else {
  $null
}

$signTool = if ($toolchain) { $toolchain.SignTool } else {
  Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe |
    Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
}

if (-not $packagePath) {
  Write-Warning "No MSIX package was found under AppPackages. Skipping signing."
}
elseif ($signature.Status -eq "Valid" -and
    $signature.SignerCertificate -and
    [string]::Equals($signature.SignerCertificate.Subject, $publisherSubject, [System.StringComparison]::OrdinalIgnoreCase)) {
  Write-Host "Package is already signed: $packagePath"
}
elseif ($signingMaterial.Mode -eq "TrustedPfx" -and $signTool) {
  Write-Host "Using trusted code-signing certificate: $($signingMaterial.Subject)"
  Write-Host "Using SignTool: $signTool"

  $signArguments = @(
    "sign"
    "/fd"
    "SHA256"
    "/f"
    $signingMaterial.PfxPath
  )

  if (-not [string]::IsNullOrWhiteSpace($signingMaterial.Password)) {
    $signArguments += @("/p", $signingMaterial.Password)
  }

  if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
    $signArguments += @("/tr", $TimestampUrl, "/td", "SHA256")
  }
  else {
    Write-Warning "No timestamp URL was supplied. The trusted MSIX will be signed without a timestamp."
  }

  $signArguments += $packagePath

  & $signTool @signArguments
  if ($LASTEXITCODE -ne 0) {
    throw "SignTool failed while signing $packagePath with the trusted PFX."
  }

  Write-Host "Signed package with trusted PFX: $packagePath"
}
elseif ($signingMaterial.Mode -eq "LocalSelfSigned" -and $signTool) {
  Write-Host "Using SignTool: $signTool"
  & $signTool sign /fd SHA256 /sha1 $signingMaterial.Thumbprint /s My $packagePath
  if ($LASTEXITCODE -ne 0) {
    throw "SignTool failed while signing $packagePath."
  }

  Write-Host "Signed package with local self-signed certificate: $packagePath"
}
else {
  if ($RequireTrustedCertificate -or $signingMaterial.Mode -eq "TrustedPfx") {
    throw "Trusted MSIX signing was requested, but SignTool was not available."
  }

  Write-Warning "No CurrentUser\\My publisher certificate or SignTool binary was found. Package remains unsigned."
}

$signature = if ($packagePath) { Get-AuthenticodeSignature $packagePath } else { $null }
if ($packagePath -and $signature.Status -ne "Valid") {
    throw "The signed MSIX did not validate successfully. Signature status: $($signature.Status)"
}

if ($packagePath -and $signature.SignerCertificate -and
    -not [string]::Equals($signature.SignerCertificate.Subject, $publisherSubject, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The signed MSIX uses signer subject '$($signature.SignerCertificate.Subject)', but Package.appxmanifest still declares '$publisherSubject'."
}

Prune-StalePackageFolders -PackagesRoot (Join-Path $projectDirectory "AppPackages") -KeepCount $KeepPackageCount

Write-Host "[5/5] Done. Check bin\$Platform\$Configuration and the newest AppPackages output."
