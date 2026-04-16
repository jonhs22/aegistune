# AegisTune Product Reset Scope

Date: 2026-04-16

## Market Reality

- Microsoft already covers the basic free lane through PC Manager, Storage Sense, Startup Apps, Installed Apps, Windows Update, and Device Manager.
- CCleaner, Glary, IObit, and similar suites compete on packaging, one-click language, breadth, scheduling, and perceived power.
- Broad registry cleaning is not a trustworthy wedge. Microsoft explicitly warns that registry cleaning utilities can cause instability and may require Windows reinstallation.

## Product Position

Do not position AegisTune as another cleaner or a blind driver updater.

Position it as:

`A guided Windows maintenance and repair cockpit with evidence-first actions, official-source routing, and technician-grade exports.`

## What We Should Build

### Core promise

- show what matters first
- guide the safest next action
- prove what changed

### Product pillars

1. Guided maintenance runs
   - Scan -> Review -> Fix -> Verify -> Export
2. Drivers and firmware control plane
   - Windows Update vs OEM utility vs OEM support vs local package evidence
3. Deep app removal and repair
   - broken uninstallers, leftovers, linked startup/tasks/services, repair routing
4. Windows health and recovery
   - crashes, Windows Update failures, SFC/DISM posture, storage pressure, service/task issues
5. Technician evidence layer
   - before/after proof, action journal, rollback hints, reports

## What We Should Not Build

- aggressive registry cleaner
- blind update-all-drivers engine from third-party feeds
- BIOS flashing automation
- fake health scores or vague "PC boost" promises
- destructive browser cleanup that logs users out or deletes profile state by default

## Safe Registry Strategy

If registry value is added, it should be framed as:

- Registry Residue Review
- Broken References
- Uninstall leftovers
- Orphaned startup/service/task links

Not as:

- Registry Cleaner
- Fix thousands of registry errors

## UX Reset

Every page should answer these immediately:

1. What did we find?
2. What should I do first?
3. What happens if I click this?

### UI rules

- one primary CTA per page
- one short result line near the CTA
- compact metrics, not empty whitespace
- review-first sections before long inventory lists
- status, source, risk, and rollback language near the action

## Next Release Scope

### v1.0.26

- shell and dashboard reset
- one primary CTA per module
- better empty states and review-first sections
- partial-failure tolerant app inventory
- stronger Apps, Cleanup, Startup operator flow

### v1.0.27

- Windows Health module
- Reliability Monitor, WER, Windows Update failure posture
- Services and Scheduled Tasks review

### v1.0.28

- Registry Residue Review
- uninstall leftovers linked to tasks/services/startup/shell references
- repair-safe rollback artifacts

### v1.0.29

- driver backup and rollback journal
- post-action verification
- baseline diff and session history

### v1.0.30

- deeper app uninstall workflows
- trusted software updater lanes where source transparency is strong
- technician and workshop export pack

## Pricing Direction

- Free: scan, preview, limited reports
- Home Pro: 1 PC, full guided workflows
- Pro Plus: 3 PCs
- Technician: 5-10 PCs, exports, rollback packs, service workflows

The paid wedge is not cleanup alone. It is lower risk, less guesswork, and stronger technician workflow.

## Source Notes

- Microsoft PC Manager: https://pcmanager.microsoft.com/en-us
- Storage Sense: https://support.microsoft.com/en-us/windows/manage-drive-space-with-storage-sense-654f6ada-7bfc-45e5-966b-e24aded96ad5
- Startup apps: https://support.microsoft.com/en-us/windows/configure-startup-applications-in-windows-115a420a-0bff-4a6f-90e0-1934c844e473
- Uninstall apps: https://support.microsoft.com/en-us/windows/uninstall-or-remove-apps-and-programs-in-windows-4b55f974-2cc6-2d2b-d092-5905080eaf98
- Driver delivery rules: https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/understanding-windows-update-automatic-and-optional-rules-for-driver-distribution
- Microsoft registry cleaner policy: https://support.microsoft.com/en-us/topic/microsoft-support-policy-for-the-use-of-registry-cleaning-utilities-0485f4df-9520-3691-2461-7b0fd54e8b3a
- CCleaner plans: https://www.ccleaner.com/ccleaner/plans
- Glary Utilities Pro: https://www.glary-utilities.com/
- IObit Driver Booster Pro: https://www.iobit.com/en/driver-booster-pro.php
