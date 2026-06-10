# Single English MSI Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship one English-only MSI (`GhostTrace-<version>-x64.msi`) instead of three culture-specific MSIs.

**Architecture:** The WiX project drops the `MsiCulture` parameter and hardcodes `en-US`; the output name loses the culture suffix. The release workflow builds, verifies, and uploads that single artifact. The unused pt-BR/es-ES `.wxl` localization files are deleted. The app's runtime language auto-detection is untouched.

**Tech Stack:** WiX Toolset 5 (SDK-style `.wixproj`), GitHub Actions, PowerShell.

**Spec:** `docs/superpowers/specs/2026-06-10-single-msi-installer-design.md`

---

### Task 1: Simplify the WiX project to a single en-US culture

**Files:**
- Modify: `src/GhostTrace.Installer/GhostTrace.Installer.wixproj`
- Delete: `src/GhostTrace.Installer/Package.pt-BR.wxl`
- Delete: `src/GhostTrace.Installer/Package.es-ES.wxl`

There are no unit tests for the installer project; verification is a local WiX build producing the expected MSI filename.

- [ ] **Step 1: Replace the culture properties in the wixproj**

In `src/GhostTrace.Installer/GhostTrace.Installer.wixproj`, replace:

```xml
    <!-- Localized installer: pass -p:MsiCulture=pt-BR|es-ES|en-US to build each MSI. -->
    <MsiCulture Condition="'$(MsiCulture)' == ''">en-US</MsiCulture>
    <Cultures>$(MsiCulture)</Cultures>

    <OutputName>GhostTrace-$(GhostTraceVersion)-$(MsiCulture)-x64</OutputName>
```

with:

```xml
    <!-- Single English installer; the app itself auto-detects the OS language at runtime. -->
    <Cultures>en-US</Cultures>

    <OutputName>GhostTrace-$(GhostTraceVersion)-x64</OutputName>
```

- [ ] **Step 2: Delete the unused localization files**

```powershell
git rm src/GhostTrace.Installer/Package.pt-BR.wxl src/GhostTrace.Installer/Package.es-ES.wxl
```

`Package.en-US.wxl` stays — it is the only `.wxl` the build compiles.

- [ ] **Step 3: Build the installer locally to verify**

Run (from the repo root):

```powershell
dotnet build src/GhostTrace.Installer/GhostTrace.Installer.wixproj --configuration Release -p:Platform=x64
```

Expected: build succeeds and produces `src/GhostTrace.Installer/bin/x64/Release/en-US/GhostTrace-1.5.0-x64.msi` (note: the `en-US` output subfolder remains — WiX always nests output under the culture folder; only the filename loses the suffix).

Verify:

```powershell
Test-Path src/GhostTrace.Installer/bin/x64/Release/en-US/GhostTrace-1.5.0-x64.msi
```

Expected: `True`

Note: this build needs the published CLI at `artifacts/publish/GhostTrace.CLI/win-x64/`. If the folder is missing, publish first:

```powershell
dotnet publish src/GhostTrace.CLI/GhostTrace.CLI.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/publish/GhostTrace.CLI/win-x64
```

- [ ] **Step 4: Commit**

```powershell
git add src/GhostTrace.Installer/GhostTrace.Installer.wixproj
git commit -m "Build a single en-US MSI without culture suffix"
```

(The `git rm` from Step 2 is already staged and lands in this commit.)

### Task 2: Point the release workflow at the new artifact name

**Files:**
- Modify: `.github/workflows/release.yml`

The workflow already has a single `Build MSI` step (no `-p:MsiCulture`), so only the artifact paths and release body change: every occurrence of `GhostTrace-${{ env.PROJECT_VERSION }}-en-US-x64.msi` becomes `GhostTrace-${{ env.PROJECT_VERSION }}-x64.msi`. The `bin/x64/Release/en-US/` directory part stays.

- [ ] **Step 1: Update the verify step**

Replace:

```yaml
      - name: Verify release assets
        shell: pwsh
        run: |
          $file = Join-Path "${{ github.workspace }}" "src/GhostTrace.Installer/bin/x64/Release/en-US/GhostTrace-${{ env.PROJECT_VERSION }}-en-US-x64.msi"
          if (-not (Test-Path $file)) {
            throw "Arquivo não encontrado: $file"
          }
          Write-Host "OK: $file"
```

with:

```yaml
      - name: Verify release assets
        shell: pwsh
        run: |
          $file = Join-Path "${{ github.workspace }}" "src/GhostTrace.Installer/bin/x64/Release/en-US/GhostTrace-${{ env.PROJECT_VERSION }}-x64.msi"
          if (-not (Test-Path $file)) {
            throw "Arquivo não encontrado: $file"
          }
          Write-Host "OK: $file"
```

- [ ] **Step 2: Update the release body download link**

Replace:

```yaml
            ### Download
            [`GhostTrace-${{ env.PROJECT_VERSION }}-x64.msi`](https://github.com/${{ github.repository }}/releases/download/${{ github.ref_name }}/GhostTrace-${{ env.PROJECT_VERSION }}-en-US-x64.msi)
```

with:

```yaml
            ### Download
            [`GhostTrace-${{ env.PROJECT_VERSION }}-x64.msi`](https://github.com/${{ github.repository }}/releases/download/${{ github.ref_name }}/GhostTrace-${{ env.PROJECT_VERSION }}-x64.msi)
```

- [ ] **Step 3: Update the uploaded files list**

Replace:

```yaml
          files: |
            ${{ github.workspace }}/src/GhostTrace.Installer/bin/x64/Release/en-US/GhostTrace-${{ env.PROJECT_VERSION }}-en-US-x64.msi
```

with:

```yaml
          files: |
            ${{ github.workspace }}/src/GhostTrace.Installer/bin/x64/Release/en-US/GhostTrace-${{ env.PROJECT_VERSION }}-x64.msi
```

- [ ] **Step 4: Sanity-check the workflow YAML**

Run:

```powershell
Get-Content .github/workflows/release.yml | Select-String "en-US-x64"
```

Expected: no output (no stale references to the old filename).

- [ ] **Step 5: Commit**

```powershell
git add .github/workflows/release.yml
git commit -m "Release a single MSI asset without culture suffix"
```

### Task 3: Update README installer reference

**Files:**
- Modify: `README.md` (only if it mentions the per-culture installer naming)

- [ ] **Step 1: Find stale references**

Run:

```powershell
Get-Content README.md | Select-String -SimpleMatch "<culture>"
```

The README is known to mention `GhostTrace-<version>-<culture>-x64.msi`. If found, replace that naming with `GhostTrace-<version>-x64.msi` and drop any wording about per-language installers (the wizard is English; the app auto-detects the OS language).

- [ ] **Step 2: Commit (skip if Step 1 found nothing)**

```powershell
git add README.md
git commit -m "Update README for single MSI installer"
```
