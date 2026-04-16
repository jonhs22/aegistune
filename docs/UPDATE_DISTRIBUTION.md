# Update Distribution

## Canonical feed

The current live public stable channel is:

`https://jonhs22.github.io/aegistune/aegistune/stable/`

The app reads:

- `stable.json` for update checks
- the portable `zip` for portable updates
- `AegisTune.appinstaller` only when a packaged `MSIX` publish is included in that channel run

If you later move to a custom domain, keep the same layout under something like:

`https://updates.ichiphost.com/aegistune/stable/`

- then update the feed URL in app settings or your release automation.

## Build the update folder

1. Build the portable and packaged artifacts first.
2. Generate the host-ready update folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-update-channel.ps1 `
  -Channel stable `
  -PublicBaseUrl https://jonhs22.github.io/aegistune/aegistune/stable
```

This writes:

- `artifacts\updates\stable\stable.json`
- `artifacts\updates\stable\AegisTune.appinstaller`
- copied `MSIX` package
- copied portable `zip`
- `RELEASE-NOTES.md`

By default the script now looks for a checked-in release notes source in:

- `docs\releases\stable\<version>.md`
- or `docs\releases\stable\latest.md`

You can also scaffold a new notes file with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-release-notes.ps1 `
  -Channel stable
```

Or point the packaging step at a specific notes file:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-update-channel.ps1 `
  -Channel stable `
  -PublicBaseUrl https://jonhs22.github.io/aegistune/aegistune/stable `
  -ReleaseNotesPath .\docs\releases\stable\1.0.25.0.md
```

## Build a GitHub Pages site locally

Generate a ready-to-upload static site that contains:

- landing page
- `aegistune/stable/stable.json`
- `aegistune/stable/AegisTune.appinstaller`
- portable `zip`
- `MSIX`
- release notes

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\stage-update-feed-for-pages.ps1 `
  -Channel stable `
  -ProductPath aegistune `
  -SiteRoot .\artifacts\pages-site `
  -PublicBaseUrl https://jonhs22.github.io/aegistune/aegistune/stable
```

This writes a static site under:

- `artifacts\pages-site\index.html`
- `artifacts\pages-site\aegistune\index.html`
- `artifacts\pages-site\aegistune\stable\...`

## Free hosting options

### GitHub Pages

This repo now includes a ready workflow:

- `.github/workflows/publish-update-feed-pages.yml`

What it does:

1. restores and optionally tests the solution
2. builds the portable bundle
3. optionally builds the `MSIX`
4. stages the Pages site with `stage-update-feed-for-pages.ps1`
5. deploys the site to GitHub Pages

Operational note:

- tag pushes like `v1.0.25.0` still run the full test lane automatically
- manual `Run workflow` publishes default to `run_tests=false` and `include_msix=false` so you can bring the first update feed online even if the GitHub runner shows a CI-only test failure or the hosted runner hangs on the MSIX packaging lane
- when manual or tag-based tests are enabled, the workflow now uploads a `pages-test-diagnostics` artifact with `trx` and `vstest` logs for exact failure triage
- manual portable-first publishes still generate the full `stable.json`, portable zip, and release notes site; they simply omit `AegisTune.appinstaller` and the hosted `MSIX` package for that run

What you should configure in the repository:

- `Pages` enabled with `GitHub Actions` as the source
- optional repo variable `UPDATE_PUBLIC_BASE_URL`
- optional repo variable `UPDATE_CNAME`
- for public `MSIX` publishing, also see `docs\PRODUCTION_MSIX_SIGNING.md`

If you do not set `UPDATE_PUBLIC_BASE_URL`, the workflow defaults to:

- `https://OWNER.github.io/REPO/aegistune/stable`

For this repo today, that resolves to:

- `https://jonhs22.github.io/aegistune/aegistune/stable`

If you use a custom domain, set:

- `UPDATE_PUBLIC_BASE_URL=https://updates.ichiphost.com/aegistune/stable`
- `UPDATE_CNAME=updates.ichiphost.com`

### Cloudflare R2

This repo now includes:

- `scripts\publish-update-feed-r2.ps1`

First authenticate Wrangler:

```powershell
npx wrangler whoami
```

If needed:

```powershell
npx wrangler login
```

Preview the upload plan without pushing anything:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-update-feed-r2.ps1 `
  -BucketName aegistune-updates `
  -PublicBaseUrl https://updates.ichiphost.com/aegistune/stable `
  -PortableOnly `
  -DryRun
```

Then publish for real:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-update-feed-r2.ps1 `
  -BucketName aegistune-updates `
  -PublicBaseUrl https://updates.ichiphost.com/aegistune/stable `
  -PortableOnly
```

This stages the same static site layout as GitHub Pages and uploads every file to R2 with cache rules:

- `stable.json`, `.appinstaller`, notes, and HTML: low-cache / no-cache
- `zip` and `MSIX`: long-cache immutable

Use `-PortableOnly` for the same first-publish strategy as GitHub Pages when you want the public feed, release notes, and portable download live before you solve public MSIX signing.

Put a public custom domain like `updates.ichiphost.com` in front of the bucket and keep the app feed URL on:

- `https://updates.ichiphost.com/aegistune/stable/stable.json`

## Runtime behavior

- `Home` checks the configured feed on launch when app-update checks are enabled.
- `Settings` lets the operator set the feed URL and run a manual check.
- `About` shows the current distribution lane and latest feed result.
- `MSIX` installs prefer `AegisTune.appinstaller`.
- `Portable` installs prefer the portable `zip`.

## Important limitation

Public `MSIX` distribution still needs a trusted signing certificate for smooth installation on other machines. The local self-signed `CN=ichiphost` certificate is fine for controlled testing but not for normal public rollout.

The GitHub Actions workflow now supports that trusted-certificate lane through repository secrets, but you still need to supply a real code-signing certificate and make sure the manifest publisher matches its subject. See:

- `docs\PRODUCTION_MSIX_SIGNING.md`
