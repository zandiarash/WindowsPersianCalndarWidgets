#Requires -Version 7.0
# Removes the Persian Calendar Widgets Board provider for the current user.
$ErrorActionPreference = 'Stop'
Get-Process PersianCalendar.WidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force
Get-AppxPackage -Name 'PersianCalendar.WidgetProvider' | Remove-AppxPackage
Remove-Item (Join-Path $env:LOCALAPPDATA 'PersianCalendarWidget\WidgetProvider') -Recurse -Force -ErrorAction SilentlyContinue
Write-Host '✔ Widget provider removed.' -ForegroundColor Green
