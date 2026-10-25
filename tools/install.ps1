param(
    [switch]$Signed
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$layout = Join-Path $root 'package\layout'

if (-not $Signed) {
    # Requires Developer Mode (loose registration).
    Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml') -ForceUpdateFromAnyVersion
    Write-Host 'Installed (loose / Developer Mode).'
    exit 0
}

$makeappx = Join-Path $root 'tools\buildtools\bin\10.0.26100.0\x64\makeappx.exe'
$signtool = Join-Path $root 'tools\buildtools\bin\10.0.26100.0\x64\signtool.exe'
$msix = Join-Path $root 'package\EverythingCmdPal.msix'

if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed' }

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=EverythingCmdPal Dev' } | Select-Object -First 1
if (-not $cert) { throw 'Development certificate not found. Run tools\make-cert.ps1 first.' }

& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'signtool failed' }

Add-AppxPackage -Path $msix -ForceUpdateFromAnyVersion
Write-Host 'Installed (signed MSIX).'
