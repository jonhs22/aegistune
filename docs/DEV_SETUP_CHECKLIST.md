# Dev Setup Checklist

## Recommended baseline
Build the first production track with:
- Windows 11 development machine
- Visual Studio 2026
- WinUI application development workload
- Developer Mode enabled
- .NET 8 SDK
- Windows App SDK stable 1.8.x
- Git
- PowerShell 7
- Windows Terminal

## Why this stack
It gives you the most standard and supported path for a new Windows desktop app with WinUI 3 and the Windows App SDK.

## Install order
1. Update Windows 11 fully
2. Enable Developer Mode
3. Install Visual Studio 2026
4. In Visual Studio Installer, enable **WinUI application development**
5. Install latest .NET 8 SDK
6. Install Git
7. Install PowerShell 7
8. Install Windows Terminal
9. Confirm WinUI templates appear in Visual Studio
10. Create a tiny packaged WinUI test project and run it once

## Must-have tools
- Visual Studio 2026
- WinUI application development workload
- .NET 8 SDK
- Windows SDK (normally brought in through Visual Studio / Windows tooling)
- SignTool from the Windows SDK for signed release packages
- App Installer on target machines for MSIX direct download scenarios

## Recommended extra tools
- GitHub Desktop or Git CLI
- Sysinternals suite for diagnosis
- Process Monitor for debugging file/registry access
- Device Manager familiarity for driver validation
- Event Viewer familiarity for package signing/deployment issues

## Validation checklist
- [ ] `dotnet --info` works
- [ ] Visual Studio shows **WinUI Blank App (Packaged)** template
- [ ] A sample app runs with F5
- [ ] A Release x64 build succeeds
- [ ] MSIX packaging succeeds
- [ ] A locally signed package installs on your test machine

## Suggested branching
- `main` -> stable release branch
- `develop` -> integration branch
- `feature/*` -> module development
- `release/*` -> hardening and packaging

## Notes for commercial release
For public distribution, plan for:
- real code-signing certificate
- release signing
- privacy policy URL
- EULA
- support contact
- crash/diagnostics opt-in wording
