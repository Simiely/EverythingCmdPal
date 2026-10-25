param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

$project = Join-Path $root 'src\EverythingCmdPal\EverythingCmdPal.csproj'
& $dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$src = Join-Path $root "src\EverythingCmdPal\bin\$Configuration\net10.0-windows10.0.26100.0\win-x64"
$layout = Join-Path $root 'package\layout'
New-Item -ItemType Directory -Force -Path $layout | Out-Null
Copy-Item (Join-Path $src '*') $layout -Recurse -Force
Copy-Item (Join-Path $root 'src\EverythingCmdPal\Package.appxmanifest') (Join-Path $layout 'AppxManifest.xml') -Force

Write-Host "Layout ready: $layout"
