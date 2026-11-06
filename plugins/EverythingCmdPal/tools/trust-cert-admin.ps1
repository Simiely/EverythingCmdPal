$ErrorActionPreference = 'Stop'
$cer = Join-Path (Split-Path $PSScriptRoot -Parent) 'package\dev.cer'

if (-not (Test-Path $cer)) { throw "Certificate not found: $cer" }

Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

Write-Host 'Certificate installed to LocalMachine\Root and LocalMachine\TrustedPeople.'
