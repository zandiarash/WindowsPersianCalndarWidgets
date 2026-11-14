#Requires -Version 7.0
<#
.SYNOPSIS
    Builds release artifacts for the version set in Directory.Build.props (<AppVersion>).

.DESCRIPTION
    Produces artifacts/<version>/:
      PersianCalendarWidget-<version>-<rid>.zip   desktop widget (single-file exe)
      Widget/                                     Windows 11 Widgets Board package:
        PersianCalendar.WidgetProvider_<version>_<arch>.msix
        PersianCalendarWidget.cer                 signing certificate (public part)
        Install.ps1                               trusts the certificate + installs the MSIX

    The MSIX must be signed. Pass -CertificatePath/-CertificatePassword to use your own .pfx
    (its subject must equal Publisher in Package.appxmanifest: CN=PersianCalendarWidget).
    Otherwise a self-signed certificate with that subject is created once in Cert:\CurrentUser\My
    and reused.

.EXAMPLE
    ./scripts/Publish.ps1
    ./scripts/Publish.ps1 -FrameworkDependent            # smaller exe; needs the .NET 11 Desktop Runtime
    ./scripts/Publish.ps1 -CertificatePath my.pfx -CertificatePassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')] [string] $Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }),
    [switch] $FrameworkDependent,
    [switch] $SkipDesktop,
    [switch] $SkipWidget,
    [string] $CertificatePath,
    [securestring] $CertificatePassword
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$rid = "win-$($Platform.ToLowerInvariant())"

# ---- Version: single source of truth ------------------------------------------------------
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$version = @($props.Project.PropertyGroup | ForEach-Object { $_.AppVersion } | Where-Object { $_ })[0]
if ($version -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$') { throw "AppVersion '$version' must have four numeric parts." }
$out = Join-Path $root "artifacts/$version"
New-Item -ItemType Directory -Force $out | Out-Null
# Clear only the parts being rebuilt (so -SkipDesktop / -SkipWidget keep the other artifacts).
if (-not $SkipDesktop) { Remove-Item (Join-Path $out 'Desktop'), (Join-Path $out "PersianCalendarWidget-$version-$rid.zip") -Recurse -Force -ErrorAction SilentlyContinue }
if (-not $SkipWidget) { Remove-Item (Join-Path $out 'Widget'), (Join-Path $out 'msix-build'), (Join-Path $out 'PersianCalendarWidget-WidgetsBoard-*.zip') -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "Publishing Persian Calendar Widget v$version ($rid)" -ForegroundColor Cyan

# ---- 1. Desktop widget ---------------------------------------------------------------------
if (-not $SkipDesktop) {
    Write-Host "`n[1/2] Desktop widget…" -ForegroundColor Cyan
    $desktopOut = Join-Path $out 'Desktop'
    # WPF's DirectWriteForwarder.dll (mixed-mode) can't load from inside a single-file bundle,
    # so self-contained builds extract their content to %TEMP%\.net on first start.
    dotnet publish (Join-Path $root 'src/PersianCalendar.Desktop/PersianCalendar.Desktop.csproj') `
        -c Release -r $rid --self-contained:$(-not $FrameworkDependent) `
        -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=$(-not $FrameworkDependent) -p:EnableCompressionInSingleFile=$(-not $FrameworkDependent) -p:DebugType=none `
        -o $desktopOut -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
    $zip = Join-Path $out "PersianCalendarWidget-$version-$rid.zip"
    Compress-Archive -Path (Join-Path $desktopOut '*') -DestinationPath $zip
    Write-Host "  → $zip" -ForegroundColor Green
}

# ---- 2. Windows 11 Widgets Board package (signed MSIX) -------------------------------------
if (-not $SkipWidget) {
    Write-Host "`n[2/2] Widgets Board package…" -ForegroundColor Cyan
    $subject = 'CN=PersianCalendarWidget'
    $widgetOut = Join-Path $out 'Widget'
    New-Item -ItemType Directory -Force $widgetOut | Out-Null

    if ($CertificatePath) {
        $certArgs = @("-p:PackageCertificateKeyFile=$((Resolve-Path $CertificatePath).Path)")
        if ($CertificatePassword) { $certArgs += "-p:PackageCertificatePassword=$(ConvertFrom-SecureString $CertificatePassword -AsPlainText)" }
        $pfx = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Resolve-Path $CertificatePath).Path,
            $(if ($CertificatePassword) { ConvertFrom-SecureString $CertificatePassword -AsPlainText } else { '' }))
        [IO.File]::WriteAllBytes((Join-Path $widgetOut 'PersianCalendarWidget.cer'), $pfx.Export('Cert'))
    }
    else {
        $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) -and $_.HasPrivateKey } |
            Sort-Object NotAfter -Descending | Select-Object -First 1
        if (-not $cert) {
            Write-Host "  Creating self-signed code-signing certificate $subject" -ForegroundColor Yellow
            $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature -FriendlyName 'Persian Calendar Widget' `
                -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) `
                -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
        }
        Export-Certificate -Cert $cert -FilePath (Join-Path $widgetOut 'PersianCalendarWidget.cer') | Out-Null
        $certArgs = @("-p:PackageCertificateThumbprint=$($cert.Thumbprint)")
    }

    $pkgDir = Join-Path $out 'msix-build'
    dotnet build (Join-Path $root 'src/PersianCalendar.WidgetProvider/PersianCalendar.WidgetProvider.csproj') `
        -c Release -p:Platform=$Platform -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=true `
        -p:AppxPackageDir="$pkgDir\" -p:AppxPackageTestDir="$pkgDir\" -p:AppxSymbolPackageEnabled=false @certArgs -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Widget package build failed.' }

    $msix = Get-ChildItem $pkgDir -Recurse -Filter *.msix | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $msix) { throw 'No .msix was produced.' }
    Copy-Item $msix.FullName $widgetOut
    Remove-Item $pkgDir -Recurse -Force

    # End-user installer: trust the certificate (admin, once) and install the package.
    @'
#Requires -RunAsAdministrator
# Installs the Persian Calendar widget for Windows 11's Widgets Board (Win+W).
$here = $PSScriptRoot
Import-Certificate -FilePath (Join-Path $here 'PersianCalendarWidget.cer') -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Get-ChildItem $here -Filter *.msix | ForEach-Object { Add-AppxPackage -Path $_.FullName -ForceApplicationShutdown }
Write-Host 'Installed. Open the Widgets Board (Win+W) → + → «تقویم شمسی».' -ForegroundColor Green
'@ | Set-Content (Join-Path $widgetOut 'Install.ps1') -Encoding utf8BOM

    $widgetZip = Join-Path $out "PersianCalendarWidget-WidgetsBoard-$version-$($Platform.ToLowerInvariant()).zip"
    Compress-Archive -Path (Join-Path $widgetOut '*') -DestinationPath $widgetZip
    Write-Host "  → $widgetZip" -ForegroundColor Green
}

Write-Host "`n✔ Release v$version ready in $out" -ForegroundColor Green
exit 0
