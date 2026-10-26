# One-shot build+install for EverythingCmdPal.
# Works on a fresh machine: installs .NET SDK 10 (per-user), creates/trusts the
# development certificate (one UAC prompt), then builds, signs and installs the MSIX.
# ASCII-only output so it is safe under any codepage.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path $PSScriptRoot -Parent

function Write-Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

# --- 1. .NET SDK (per-user) ---------------------------------------------------
Write-Step '1/8 .NET SDK'
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $zip = Join-Path $env:TEMP 'dotnet-sdk-10.0.100-win-x64.zip'
    $url = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-win-x64.zip'
    Write-Host 'downloading .NET SDK 10.0.100 (~290MB)...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    New-Item -ItemType Directory -Force -Path $dotnetDir | Out-Null
    Expand-Archive -Path $zip -DestinationPath $dotnetDir -Force
    Remove-Item $zip -Force
}
Write-Host "dotnet: $dotnet"

# --- 2. Build (restores SDK BuildTools into the NuGet cache) -------------------
Write-Step '2/8 Build'
$project = Join-Path $root 'src\EverythingCmdPal\EverythingCmdPal.csproj'
& $dotnet build $project -c Debug -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

# --- 3. Locate makeappx/signtool from the NuGet cache --------------------------
Write-Step '3/8 Packaging tools (from NuGet cache)'
$btRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
$btDir = Get-ChildItem $btRoot -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $btDir) { throw 'Microsoft.Windows.SDK.BuildTools not found in NuGet cache' }

$makeappx = Get-ChildItem (Join-Path $btDir.FullName 'bin') -Recurse -Filter makeappx.exe |
    Where-Object { $_.DirectoryName -like '*x64' } | Select-Object -First 1 -ExpandProperty FullName
$signtool = Get-ChildItem (Join-Path $btDir.FullName 'bin') -Recurse -Filter signtool.exe |
    Where-Object { $_.DirectoryName -like '*x64' } | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeappx -or -not $signtool) { throw 'makeappx/signtool not found in BuildTools package' }
Write-Host "makeappx: $makeappx"

# --- 4. Development certificate + machine trust (one UAC prompt) ---------------
Write-Step '4/8 Development certificate'
$subject = 'CN=EverythingCmdPal Dev'
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
        -KeyUsage DigitalSignature -FriendlyName 'EverythingCmdPal Dev' `
        -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(5)
    Write-Host "certificate created: $($cert.Thumbprint)"
}

$pkgDir = Join-Path $root 'package'
New-Item -ItemType Directory -Force -Path $pkgDir | Out-Null
$cer = Join-Path $pkgDir 'dev.cer'
Export-Certificate -Cert $cert -FilePath $cer -Force | Out-Null

$trusted = Get-ChildItem Cert:\LocalMachine\Root -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
if (-not $trusted) {
    Write-Host 'trusting certificate in LocalMachine\Root (UAC prompt)...'
    $cmd = "certutil -addstore -f Root `"$cer`"; certutil -addstore -f TrustedPeople `"$cer`""
    Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', $cmd
}

# --- 5. Close Command Palette + old extension, remove old package --------------
Write-Step '5/8 Close and remove old package'
Get-Process Microsoft.CmdPal.UI, EverythingCmdPal -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$old = Get-AppxPackage EverythingCmdPal -ErrorAction SilentlyContinue
if ($old) {
    Remove-AppxPackage -Package $old.PackageFullName
    Write-Host "removed $($old.Version)"
}

# --- 6. Stage layout ------------------------------------------------------------
Write-Step '6/8 Stage layout'
$src = Join-Path $root 'src\EverythingCmdPal\bin\Debug\net10.0-windows10.0.26100.0\win-x64'
$layout = Join-Path $root 'package\layout'
New-Item -ItemType Directory -Force -Path $layout | Out-Null
Copy-Item (Join-Path $src '*') $layout -Recurse -Force
Copy-Item (Join-Path $root 'src\EverythingCmdPal\Package.appxmanifest') (Join-Path $layout 'AppxManifest.xml') -Force

# --- 7. Pack, sign, install ------------------------------------------------------
Write-Step '7/8 Pack, sign, install'
$msix = Join-Path $root 'package\EverythingCmdPal.msix'
if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'pack failed' }
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'sign failed' }

Add-AppxPackage -Path $msix
$installed = Get-AppxPackage EverythingCmdPal
Write-Host "installed: $($installed.Version)"

# --- 8. Start Command Palette -----------------------------------------------------
Write-Step '8/8 Start Command Palette'
& explorer.exe 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'

Write-Host "`nDONE. Version: $($installed.Version)" -ForegroundColor Green
