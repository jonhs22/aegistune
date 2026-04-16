# Production MSIX Signing

## Overview

The repo now supports two MSIX signing lanes:

- local development lane: self-signed `CN=ichiphost` certificate for controlled local testing
- production lane: trusted PFX certificate for public `MSIX` publishing through GitHub Actions

The production lane is activated only when the repository has both secrets below:

- `MSIX_CERT_PFX_BASE64`
- `MSIX_CERT_PASSWORD`

Optional repository variable:

- `MSIX_TIMESTAMP_URL`

If a tag publish runs without those secrets, the workflow now publishes the portable lane only and skips hosted `MSIX`.

If you manually run the workflow with `include_msix=true` and the secrets are missing, the workflow fails immediately with a clear error.

## Critical requirement

Your trusted signing certificate subject must exactly match the package manifest publisher in:

- `src\AegisTune.App\Package.appxmanifest`

Today the manifest still declares:

- `CN=ichiphost`

If the purchased code-signing certificate uses a different subject, update the manifest publisher before trying to publish a public `MSIX`.

## Prepare the GitHub secret bundle

Use the helper script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-msix-signing-secrets.ps1 `
  -PfxPath C:\secure\aegistune-signing-cert.pfx `
  -PfxPassword "your-pfx-password" `
  -CopyToClipboard
```

It will:

- validate that the PFX opens
- print the certificate subject and thumbprint
- copy the base64 payload to the clipboard if requested
- show the exact GitHub secret names to set

## GitHub repository configuration

Repository secrets:

- `MSIX_CERT_PFX_BASE64` = base64 contents of the `.pfx`
- `MSIX_CERT_PASSWORD` = password for the `.pfx`

Optional repository variable:

- `MSIX_TIMESTAMP_URL` = trusted RFC 3161 timestamp endpoint

## Workflow behavior

`.github\workflows\publish-update-feed-pages.yml` now does this:

1. build portable
2. resolve whether `MSIX` can be published in this run
3. materialize the trusted PFX on the GitHub runner only when secrets exist
4. run `build-msix.ps1` with:
   - `-CodeSigningPfxPath`
   - `-CodeSigningPfxPassword`
   - `-RequireTrustedCertificate`
   - optional `-TimestampUrl`
5. stage the Pages site with `MSIX` included only when the trusted signing lane actually ran

## Local verification of the trusted-PFX lane

If you want to test the trusted-certificate code path locally, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msix.ps1 `
  -Platform x64 `
  -Configuration Release `
  -CodeSigningPfxPath C:\secure\aegistune-signing-cert.pfx `
  -CodeSigningPfxPassword "your-pfx-password" `
  -RequireTrustedCertificate
```

That path validates:

- the PFX opens
- the certificate subject matches the package manifest publisher
- `signtool` can sign the `MSIX`
- the final package validates after signing
