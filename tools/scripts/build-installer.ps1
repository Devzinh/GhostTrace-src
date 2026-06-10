#Requires -Version 7
<#
.SYNOPSIS
    Publishes GhostTrace.CLI and builds the English MSI installer.

.DESCRIPTION
    End-to-end installer build:
      1. dotnet publish of GhostTrace.CLI (win-x64, self-contained) into
         artifacts/publish/GhostTrace.CLI/win-x64 — the folder Package.wxs harvests.
      2. Builds the MSI installer (en-US) via the WiX SDK project.
         The installer auto-detects the OS language at runtime.
      3. Copies the resulting MSI into artifacts/installers.

.EXAMPLE
    pwsh tools/scripts/build-installer.ps1
    pwsh tools/scripts/build-installer.ps1 -Configuration Release
#>
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot     = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$publishDir   = Join-Path $repoRoot 'artifacts\publish\GhostTrace.CLI\win-x64'
$installerDir = Join-Path $repoRoot 'artifacts\installers'

Write-Host "==> Publishing GhostTrace.CLI ($Configuration, win-x64, self-contained)" -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot 'src\GhostTrace.CLI\GhostTrace.CLI.csproj') `
    -c $Configuration -r win-x64 --self-contained true -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

New-Item -ItemType Directory -Force $installerDir | Out-Null

Write-Host "==> Building MSI (en-US)" -ForegroundColor Cyan
dotnet build (Join-Path $repoRoot 'src\GhostTrace.Installer\GhostTrace.Installer.wixproj') `
    -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) { throw 'MSI build failed.' }

Copy-Item (Join-Path $repoRoot "src\GhostTrace.Installer\bin\x64\$Configuration\en-US\*.msi") `
    $installerDir -Force

Write-Host "==> Done. Installers in $installerDir" -ForegroundColor Green
Get-ChildItem $installerDir -Filter *.msi |
    Select-Object Name, @{n = 'SizeMB'; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
