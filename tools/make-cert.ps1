$ErrorActionPreference = 'Stop'

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq 'CN=EverythingCmdPal Dev' } |
    Select-Object -First 1

if (-not $cert) {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=EverythingCmdPal Dev' `
        -KeyUsage DigitalSignature `
        -FriendlyName 'EverythingCmdPal Dev' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(5)
}

$root = Split-Path $PSScriptRoot -Parent
$cer = Join-Path $root 'package\dev.cer'
Export-Certificate -Cert $cert -FilePath $cer -Force | Out-Null

Write-Host "Certificate: $($cert.Thumbprint)"
Write-Host "Exported:    $cer"
Write-Host ''
Write-Host 'For signed MSIX installs, the certificate must be trusted machine-wide:'
Write-Host "  (admin) Import-Certificate -FilePath `"$cer`" -CertStoreLocation Cert:\LocalMachine\Root"
