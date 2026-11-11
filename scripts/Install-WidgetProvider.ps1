#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the Persian Calendar Widgets Board provider and registers it for the current user.

.DESCRIPTION
    Uses loose-file registration (the same thing Visual Studio's F5 does), which needs
    Windows "Developer Mode" (Settings → System → For developers). No certificate required.
    After installing: Win+W → "+" (Add widgets) → "تقویم شمسی".

.EXAMPLE
    ./scripts/Install-WidgetProvider.ps1
    ./scripts/Install-WidgetProvider.ps1 -Configuration Release -Platform ARM64
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')] [string] $Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' })
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/PersianCalendar.WidgetProvider/PersianCalendar.WidgetProvider.csproj'

$devMode = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
if ($devMode -ne 1) {
    throw 'Developer Mode is off. Enable it in Settings → System → For developers, then run this script again.'
}

# Register from a dedicated copy (not bin/) so Windows never locks build outputs.
$deploy = Join-Path $env:LOCALAPPDATA 'PersianCalendarWidget\WidgetProvider'
Get-Process PersianCalendar.WidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force
$existing = Get-AppxPackage -Name 'PersianCalendar.WidgetProvider'
if ($existing -and $existing.InstallLocation -ne $deploy) { $existing | Remove-AppxPackage }

Write-Host "Building widget provider ($Configuration | $Platform)…" -ForegroundColor Cyan
dotnet build $project -c $Configuration -p:Platform=$Platform -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$rid = "win-$($Platform.ToLowerInvariant())"
$manifest = Get-ChildItem (Join-Path $root "src/PersianCalendar.WidgetProvider/bin/$Platform/$Configuration") -Recurse -Filter AppxManifest.xml |
    Where-Object { $_.DirectoryName -like "*$rid" } | Select-Object -First 1
if (-not $manifest) { throw 'AppxManifest.xml was not generated.' }

Get-Process PersianCalendar.WidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force
robocopy $manifest.DirectoryName $deploy /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the package layout failed (robocopy $LASTEXITCODE)." }

Write-Host "Registering $deploy" -ForegroundColor Cyan
Add-AppxPackage -Register (Join-Path $deploy 'AppxManifest.xml') -ForceApplicationShutdown -ForceUpdateFromAnyVersion

# Restart the Widgets host so the new provider shows up in the picker immediately.
Get-Process Widgets, WidgetService -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host '✔ Installed. Open the Widgets Board (Win+W) → "+" → "تقویم شمسی" and pin it.' -ForegroundColor Green
exit 0
