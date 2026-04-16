# Product Implementation Blueprint

## Product working title
AegisTune for Windows

## Positioning
A Windows 11 maintenance suite focused on:
- cleanup
- driver recovery and guided updates
- startup/app hygiene
- targeted repair
- rollback and reporting

## Do not build
- generic registry cleaner
- fake performance score inflation
- “repair all” black-box behavior
- risky silent driver installs

## Core pillars

### 1. Cleaner
Feature set:
- user temp
- system temp
- recycle bin
- stale logs
- browser cleanup abstraction
- exclusions
- scan preview
- dry-run
- reclaimed size estimate

### 2. Driver Center
Feature set:
- device inventory
- problem device detection
- driver metadata
- hardware ID collection
- recommendation engine
- source abstraction
- install workflow with verification
- reboot guidance
- logs

Driver source model:
- Windows Update
- local driver folder / INF import
- future OEM connectors

### 3. Startup / Apps
Feature set:
- startup inventory
- startup impact view
- enable/disable startup items
- orphaned startup detection
- uninstall leftovers detection
- app inventory
- future app update workflows

### 4. Repair Center
Safe, evidence-based fixes:
- orphaned startup entries
- uninstall leftovers
- broken shell/context menu remnants
- scheduled task leftovers
- missing path/service/task references where the evidence is clear

### 5. Reporting / Trust
- scan report
- action report
- before/after evidence
- exports
- rollback support
- exclusions
- logs

## Information architecture
Navigation:
- Dashboard
- Cleaner
- Drivers
- Startup
- Apps
- Repair
- Reports
- Settings
- About

## Severity model
- Safe
- Review
- Risky

## Recommended first MVP
Ship only:
- dashboard
- temp cleaner
- recycle bin cleaner
- startup inventory
- orphaned startup fixes
- device inventory
- driver audit
- logs and reports

Leave for later:
- OEM driver connectors
- advanced privacy controls
- duplicate finder
- large file analyzer
- full app updater
- technician mode

## Release-track decision
- finish the core packaged product first
- keep portable / unpackaged edition as a follow-up release track after the core program is stable
- portable edition must reuse the same trust model, reports, and safety constraints as the main app
- do not split engineering focus into packaged + portable before the core modules and release hardening are complete

## Suggested domain entities
- ScanSession
- ScanIssue
- FixPlan
- FixExecution
- FixEvidence
- DriverDevice
- DriverPackageCandidate
- CleanupRule
- StartupEntry
- InstalledApplication
- ReportDocument
- UserExclusion
- RestoreCheckpoint

## Suggested services
- ICleanupScanner
- ICleanupExecutor
- IDriverInventoryService
- IDriverRecommendationService
- IDriverInstallService
- IStartupInventoryService
- IRepairScanner
- IRepairExecutor
- IReportGenerator
- IRestorePointService
- IPrivilegedTaskRunner
- ISettingsStore

## Detection principles
- evidence first
- no inference without traceable data
- no silent deletion of ambiguous entries
- keep raw evidence for each finding

## UI principles
- show what was found
- show why it matters
- show what will change
- show whether rollback exists
- show whether admin rights are required
- never hide risky implications behind a one-click action

## Commercial features to plan after MVP
- scheduled maintenance
- premium reports
- advanced driver workflow
- app updater
- duplicate finder
- disk analyzer
- branded dark/light themes
- multiple language support
- paid license handling
- in-app upsell for premium modules
- portable edition for direct-download distribution after the main app is complete

## Monetization-ready tiers
### Free
- dashboard
- basic cleaner
- startup inventory
- driver audit

### Pro
- guided driver installs
- advanced cleanup
- reports export
- rollback center
- scheduled scans
- repair center

### Technician
- offline diagnostic bundle
- exportable support package
- multi-PC use
- advanced logging
