$ErrorActionPreference = 'Stop'
$cer = Join-Path (Split-Path $PSScriptRoot -Parent) 'package\dev.cer'

Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

$key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
if (-not (Test-Path $key)) { New-Item -Path $key -Force | Out-Null }
Set-ItemProperty -Path $key -Name 'AllowDevelopmentWithoutDevLicense' -Value 1 -Type DWord
Set-ItemProperty -Path $key -Name 'AllowAllTrustedApps' -Value 1 -Type DWord

'DONE'
