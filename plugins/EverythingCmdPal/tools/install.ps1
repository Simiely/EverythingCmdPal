param(
    [switch]$Signed
)

# 薄封装：真正的流程在 shared/build/install.ps1（按 plugin.psd1 参数化）。
$ErrorActionPreference = 'Stop'
$pluginRoot = Split-Path $PSScriptRoot -Parent

if (-not $Signed) {
    # 松散注册：需要开发者模式（docs/install.md §2）。
    $layout = Join-Path $pluginRoot 'package\layout'
    if (-not (Test-Path $layout)) { throw 'layout not found. Run tools\build.ps1 first.' }
    Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')
    Write-Host 'Installed (loose / Developer Mode).'
    exit 0
}

& (Join-Path $PSScriptRoot '..\..\..\shared\build\install.ps1') -Plugin EverythingCmdPal
