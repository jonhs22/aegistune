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
Write-Host "- Signing certificate available for release signing"
Write-Host "- Privacy policy URL prepared"
Write-Host "- EULA prepared"
Write-Host "- Support email/contact prepared"
Write-Host "- Store images/screenshots prepared"
