# CODEX MASTER PROMPT — Build the full Windows 11 maintenance suite

You are building a production-grade Windows 11 desktop utility suite for commercial release.

## Product name
Use working title: **AegisTune for Windows**.
You may keep the code namespace neutral (for example `AegisTune.*`).

## Mission
Build a **safe, commercial-grade Windows 11 maintenance suite** that focuses on:
1. Junk cleanup
2. Driver audit and guided driver installation workflows
3. Startup and app hygiene
4. Targeted repair workflows
5. Rollback, logs, and user trust

This is **not** scareware.
This is **not** a fake “boost your PC 300%” registry cleaner.
This is a transparent desktop maintenance product.

## Non-negotiable product rules
- Never implement a generic “delete lots of registry keys” cleaner.
- Never show inflated issue counts.
- Never claim guaranteed performance gains.
- Every destructive or risky action must have:
  - preview
  - severity label
  - confirmation
  - logging
- Risky actions must create a restore point or an equivalent rollback checkpoint when possible.
- Prefer “recommendation + explicit user choice” over silent automation.
- Keep a full action history and before/after reports.
- All scan engines must support dry-run mode.

## Technology stack
Build with:
- C#
- .NET 8
- WinUI 3
- Windows App SDK stable 1.8.x
- MVVM architecture
- Dependency injection
- Async-first design
- Structured logging
- Unit tests + integration tests

## Packaging constraints
Single-project MSIX only supports a single executable.
Therefore, in v1:
- avoid introducing a second helper executable
- do privileged workflows by relaunching the same executable with elevation and a constrained task argument such as:
  - `--run-task cleanup-temp`
  - `--run-task driver-install`
  - `--run-task repair-startup`
- isolate privileged logic behind an interface such as `IPrivilegedTaskRunner`
- make the architecture easy to swap later for a helper executable or service if a future EXE/MSI installer track is chosen

## Windows version target
Primary target: Windows 11 only.
Design should be modern and polished for Windows 11.
Do not spend effort on Windows 10-specific UI optimization in the first release.

## High-level solution structure
Create a solution with projects similar to:
- `src/AegisTune.App` — WinUI 3 packaged desktop app
- `src/AegisTune.Core` — domain models, contracts, shared types
- `src/AegisTune.CleanupEngine` — temp cleanup, recycle bin, cache rules, browser cleanup contracts
- `src/AegisTune.DriverEngine` — device inventory, driver scoring, driver install workflows
- `src/AegisTune.RepairEngine` — startup repair, broken references, uninstall leftovers, shell repair
- `src/AegisTune.SystemIntegration` — wrappers for PowerShell, WMI/CIM, DISM, pnputil, winget, registry, services, scheduled tasks
- `src/AegisTune.Reporting` — HTML/PDF/text report generation abstractions
- `src/AegisTune.Storage` — persistence, settings, exclusions, scan history
- `tests/AegisTune.Core.Tests`
- `tests/AegisTune.CleanupEngine.Tests`
- `tests/AegisTune.DriverEngine.Tests`
- `tests/AegisTune.RepairEngine.Tests`

## Required product areas

### 1) Dashboard / Health Check
Create a dashboard with:
- health score
- disk pressure
- startup load
- missing/problematic drivers
- outdated apps count
- broken startup entries
- recommended actions
- last scan time

The dashboard must clearly separate:
- Safe to fix now
- Review recommended
- Risky / advanced

### 2) Junk Cleanup
Implement:
- temp folder scan
- Windows temp
- user temp
- recycle bin estimation and cleanup
- stale logs where safe
- browser cache cleanup abstraction layer
- cleanup rules engine with exclusions
- preview of files and estimated reclaimed size
- dry run
- summary report

Add categories:
- Safe cleanup
- Optional cleanup
- Advanced cleanup

### 3) Driver Center
Implement a strong driver workflow:
- enumerate devices
- identify missing/problem devices
- read hardware IDs
- detect current provider/version/date/signer
- rank recommendation confidence
- separate:
  - critical missing drivers
  - problematic drivers
  - outdated drivers
  - generic Microsoft driver where OEM may be preferable

Driver pipeline:
1. inventory
2. diagnose
3. recommend
4. explicit user selection
5. validate package/signature/source
6. install
7. verify result
8. log and offer reboot if required

Important:
- do not fetch random drivers from shady sources
- create provider abstractions for sources
- support these source types in architecture:
  - Windows Update
  - local package / folder import
  - OEM connector placeholder
- build OEM connector interfaces, but do not hardcode fragile scraping in v1
- create clear UI showing current vs proposed driver

### 4) Startup and App Hygiene
Implement:
- startup app inventory
- startup impact scoring
- enable/disable startup entries
- orphaned startup entry detection
- installed app inventory
- uninstall leftovers detection
- app updater abstraction
- WinGet integration abstraction for application update workflows

### 5) Repair Center
Implement **targeted repair only**:
- broken startup references
- orphaned uninstall entries
- invalid shell/context menu leftovers
- missing file path references for app hooks where safe
- scheduled task leftovers
- service path validation where safe

Never implement:
- broad registry “optimize everything”
- mass COM cleanup without proof
- destructive shell extension deletion without preview

### 6) Privacy and Cleanup Controls
Implement a section for:
- browser privacy cleanup
- recent items cleanup
- optional telemetry/privacy setting guidance
But do this as explicit user-controlled options, not silent changes.

### 7) Reports / Undo / Audit
Implement:
- scan reports
- action reports
- before/after diffs
- export to HTML and plain text
- rollback-friendly history log
- exclusions list
- quarantine-like safety for repair artifacts where practical

## UI and UX
Use WinUI 3 with a clean commercial UI.
Create:
- sidebar navigation
- modern dashboard cards
- progress screens for scan/fix workflows
- issue details panels
- confirmation dialogs with impact text
- color-coded severity badges
- empty states
- searchable results grids

Pages:
- Dashboard
- Cleaner
- Drivers
- Startup
- Apps
- Repair
- Reports
- Settings
- About / License

## Architecture requirements
- MVVM
- cancellation tokens on scans
- background task orchestration with progress events
- interfaces for OS operations
- testable scan engines
- rule-based detection
- structured result models:
  - issue id
  - title
  - severity
  - category
  - evidence
  - estimated impact
  - fix availability
  - rollback support
- do not let UI talk directly to PowerShell or process execution

## OS integration requirements
Create wrappers for:
- PowerShell
- pnputil
- DISM
- WinGet
- registry reads/writes
- WMI/CIM queries
- service control manager access
- scheduled tasks
- file system scan with safe filtering

## Safety model
Every fixable issue must declare:
- safety level
- whether admin is required
- whether reboot may be required
- whether rollback is supported
- exact evidence collected

Create these enums:
- `IssueSeverity`
- `SafetyLevel`
- `FixResultStatus`
- `DriverRecommendationConfidence`

## Persistence
Store:
- settings
- exclusions
- accepted recommendations
- completed scans
- action logs
- restore point metadata
- reboot-pending state

## Testing
Add:
- unit tests for scan rules
- parser tests for driver outputs
- tests for cleanup size calculations
- tests for issue classification
- tests for report generation
- smoke tests for view models where practical

## Deliverables to generate in the repository
1. Solution and project files
2. Buildable WinUI 3 app shell
3. Core domain models
4. Placeholder services and interfaces
5. First implementation of:
   - temp cleaner
   - recycle bin estimate/cleanup
   - startup inventory
   - orphaned startup repair
   - device inventory
   - driver recommendation models
6. Release notes template
7. README
8. CI workflow
9. Packaging scripts for MSIX
10. Store listing starter text
11. Privacy policy starter template
12. EULA starter template
13. Support policy starter template

## Development plan to follow
Phase 1:
- scaffold solution
- create architecture
- build app shell and navigation
- implement dashboard models
- implement cleanup engine foundations
- implement startup inventory
- implement driver inventory
- make project compile

Phase 2:
- implement cleanup workflows
- implement targeted repair workflows
- implement driver recommendation pipeline
- implement reporting
- implement logs and settings

Phase 3:
- polish UI
- add tests
- add packaging scripts
- add release docs
- add Store listing assets checklist

## Build and packaging
Add scripts that can:
- restore packages
- build Release x64
- generate MSIX package
- emit release artifacts to `artifacts/`

Assume command-line build for MSIX includes:
`/p:GenerateAppxPackageOnBuild=true`

## Commercial-readiness requirements
Create documentation and placeholders for:
- versioning strategy
- code signing
- release checklist
- privacy policy
- EULA
- website download page requirements
- support email placeholders
- crash log opt-in

## Important product positioning
This app must be marketed as:
- safe
- transparent
- guided
- trustworthy
- Windows 11-focused

Do not position it as:
- miracle speed booster
- registry magic cleaner
- aggressive optimizer

## Output expectations
Do not just describe the architecture.
Actually create the repository content:
- code
- project files
- scripts
- docs
- tests
- placeholders
- basic implementation

After generating files:
1. summarize what was created
2. list what still needs manual work
3. give exact build/run commands
4. list risky assumptions
5. propose the next 5 implementation tasks
