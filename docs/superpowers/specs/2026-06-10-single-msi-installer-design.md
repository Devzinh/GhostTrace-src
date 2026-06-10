# Single English MSI Installer

**Date:** 2026-06-10
**Status:** Approved

## Problem

The release pipeline builds three culture-specific MSIs (pt-BR, en-US, es-ES) whose only
difference is the language of the install wizard. The app itself detects the Windows UI
language at runtime, so the per-culture installers triple the build/verify/upload steps
without changing what gets installed.

A runtime language dropdown inside a pure `.msi` is not possible: Windows Installer does
not auto-apply embedded language transforms — that always requires a Burn bootstrapper
(`.exe`), which was considered and rejected.

## Decision

Ship a single MSI with the wizard in English (universal language). The installed app keeps
auto-detecting the OS language (en, pt-BR, es) exactly as it does today.

## Changes

1. **`src/GhostTrace.Installer/GhostTrace.Installer.wixproj`**
   - Remove the `MsiCulture` property and its default.
   - Fix `<Cultures>` to `en-US`.
   - `<OutputName>` becomes `GhostTrace-$(GhostTraceVersion)-x64` (no culture suffix).

2. **`.github/workflows/release.yml`**
   - Single `Build MSI` step (no `-p:MsiCulture`).
   - Verify/upload paths use the new name: `bin/x64/Release/en-US/GhostTrace-<version>-x64.msi`.
   - Release notes: single download link, note that the app auto-detects the OS language.

3. **Delete** `Package.pt-BR.wxl` and `Package.es-ES.wxl` — never compiled again.
   `Package.en-US.wxl` stays as the only localization file.

## Result

One release asset: `GhostTrace-<version>-x64.msi`.

## Testing

- Local: `dotnet build src/GhostTrace.Installer/GhostTrace.Installer.wixproj -p:Platform=x64 --configuration Release` produces `GhostTrace-1.5.0-x64.msi` under `bin/x64/Release/en-US/`.
- CI: next tag push produces a release with exactly one MSI asset.
