$checks = @(
    @{ Name = "Git"; Command = "git --version" },
    @{ Name = ".NET"; Command = "dotnet --info" },
    @{ Name = "PowerShell"; Command = "pwsh --version" }
)

Write-Host "Release preflight checks"
Write-Host "======================"

foreach ($check in $checks) {
    try {
        Write-Host "`n[$($check.Name)]"
        Invoke-Expression $check.Command
    }
    catch {
        Write-Warning "Failed: $($check.Name)"
    }
}

if (Test-Path (Join-Path $PSScriptRoot 'Resolve-WindowsSdkTools.ps1')) {
    Write-Host "`n[Windows SDK Toolchain]"
    . (Join-Path $PSScriptRoot 'Resolve-WindowsSdkTools.ps1')
    try {
        Resolve-WindowsSdkTools -RequiredTools @('signtool', 'makeappx', 'makepri') | Format-List
    }
    catch {
        Write-Warning $_.Exception.Message
    }
}

Write-Host "`nManual checks:"
Write-Host "- Visual Studio installed"
Write-Host "- WinUI / Windows App SDK development workload installed"
Write-Host "- Developer Mode enabled"
Write-Host "- Trusted MSIX signing certificate available for release signing"
Write-Host "- Package.appxmanifest Publisher matches the trusted certificate subject"
Write-Host "- GitHub secrets MSIX_CERT_PFX_BASE64 and MSIX_CERT_PASSWORD configured for CI MSIX publishing"
Write-Host "- Optional repo variable MSIX_TIMESTAMP_URL configured for trusted timestamps"
Write-Host "- Privacy policy URL prepared"
Write-Host "- EULA prepared"
Write-Host "- Support email/contact prepared"
Write-Host "- Store images/screenshots prepared"
