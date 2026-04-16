# Release and Packaging Plan

## Recommended release strategy
Use two release tracks over time:

### Track A — MSIX
Best for:
- clean packaged installs
- easier update path
- optional Store readiness
- early beta / controlled distribution

### Track B — signed EXE/MSI
Best for:
- broad public web distribution
- full installer control
- easier future support for helper executables / services
- commercial utility scenarios with deeper system integration

## Important packaging note
Single-project MSIX supports only **one executable**.
That means:
- if you keep the product as one executable in v1, MSIX is straightforward
- if you later add a helper executable/service, prefer either:
  - a separate Windows Application Packaging Project, or
  - a direct EXE/MSI installer release track

## Recommended v1
- Build the app as a packaged WinUI 3 desktop app
- Keep privileged work in the same executable via elevated relaunch
- Generate Release x64 MSIX
- Test signed local installs
- Keep the architecture open for a later EXE/MSI commercial installer

## Release artifacts
Prepare:
- signed package
- release notes
- changelog
- privacy policy
- EULA
- support contact
- screenshots
- Store description draft
- crash diagnostics policy text

## Code signing
For public release:
- use a valid code-signing certificate
- make sure the package publisher identity and signing certificate subject align for packaged app signing
- timestamp releases
- verify signatures during release QA

## Direct download path
For web distribution, prepare:
- product download page
- versioned artifacts
- SHA256 checksums
- release notes page
- privacy policy page
- EULA page
- support page / contact
- update policy

## Microsoft Store path
You can submit:
- MSIX
- EXE/MSI

Store submission prep:
- reserve name
- app description
- screenshots
- logo
- pricing/availability
- category
- privacy policy URL
- support details
- upload package or provide installer URL depending on package type

## QA matrix
Test:
- clean Windows 11 machine
- machine with missing driver
- machine with old startup clutter
- machine with low disk space
- standard user vs admin flows
- reboot-required flows
- signed install and update path

## Signing and release sanity checks
- package builds in Release x64
- signatures verify
- installer/package installs cleanly
- uninstall works cleanly
- logs are written
- app can detect pending reboot after driver/system changes
- privacy links and support links resolve
