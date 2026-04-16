param(
    [Parameter(Mandatory = $true)]
    [string]$PfxPath,
    [string]$PfxPassword = "",
    [string]$OutputPath = "",
    [switch]$CopyToClipboard
)

$ErrorActionPreference = "Stop"
$fullPfxPath = [System.IO.Path]::GetFullPath($PfxPath)

if (-not (Test-Path $fullPfxPath)) {
    throw "PFX file was not found at $fullPfxPath"
}

try {
    $flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable -bor
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet

    if ([string]::IsNullOrWhiteSpace($PfxPassword)) {
        $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($fullPfxPath, $null, $flags)
    }
    else {
        $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($fullPfxPath, $PfxPassword, $flags)
    }
}
catch {
    throw "Failed to load the PFX. $($_.Exception.Message)"
}

$base64 = [System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes($fullPfxPath))

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $outputDirectory = Split-Path -Parent $fullOutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }

    $base64 | Set-Content -Path $fullOutputPath -Encoding UTF8
    Write-Host "Wrote base64 certificate bundle to: $fullOutputPath"
}

if ($CopyToClipboard) {
    Set-Clipboard -Value $base64
    Write-Host "Copied the base64 certificate bundle to the clipboard."
}

Write-Host "Trusted MSIX signing certificate"
Write-Host "==============================="
Write-Host "PFX path: $fullPfxPath"
Write-Host "Subject: $($certificate.Subject)"
Write-Host "Thumbprint: $($certificate.Thumbprint)"
Write-Host ""
Write-Host "GitHub repository secrets:"
Write-Host "- MSIX_CERT_PFX_BASE64"
Write-Host "- MSIX_CERT_PASSWORD"
Write-Host ""
Write-Host "Package.appxmanifest Publisher must exactly match:"
Write-Host "$($certificate.Subject)"
