# Repo Structure and Milestones

## Proposed folder layout
```text
/src
  /AegisTune.App
  /AegisTune.Core
  /AegisTune.CleanupEngine
  /AegisTune.DriverEngine
  /AegisTune.RepairEngine
  /AegisTune.SystemIntegration
  /AegisTune.Storage
  /AegisTune.Reporting
/tests
  /AegisTune.Core.Tests
  /AegisTune.CleanupEngine.Tests
  /AegisTune.DriverEngine.Tests
  /AegisTune.RepairEngine.Tests
/docs
/scripts
/artifacts
```

## Milestone 1 — Compile and run
- Solution scaffolding
- WinUI shell
- DI container
- logging
- settings
- navigation
- dashboard stubs

## Milestone 2 — Core scan engines
- temp cleanup scan
- recycle bin scan
- startup inventory
- device inventory
- issue models
- result pages

## Milestone 3 — First real fixes
- temp cleanup execute
- recycle bin execute
- orphaned startup removal
- report generation
- exclusions
- dry-run mode

## Milestone 4 — Driver center
- driver metadata
- recommendation confidence
- package source abstractions
- guided install UI
- verification and reboot handling

## Milestone 5 — Release hardening
- tests
- packaging scripts
- app metadata
- privacy/EULA templates
- support docs
- release checklist

## Milestone 6 — Portable edition
- create an unpackaged / portable build of the same completed product
- keep feature parity with the packaged app
- verify direct `.exe` launch without MSIX install flow
- prepare portable publish script and distribution folder layout
- confirm that settings, logging, and reports behave correctly outside package identity
