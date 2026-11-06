<#
.SYNOPSIS
    参数化打包：构建 → 布局 → makeappx → signtool。
.DESCRIPTION
    插件名、清单、工程、证书、版本全部从 plugins/<Plugin>/plugin.psd1 推导，
    脚本本身不含任何插件名或 PFN。不安装、不卸载。
.PARAMETER Plugin
    plugins/ 下的插件目录名（= MSIX Identity Name）。
.PARAMETER Bump
    打包前把 <Identity Version> 递增一个 revision（安装流程必须递增）。
.PARAMETER NoSign
    只打包不签名（产物不能直接安装，仅用于检查 layout）。
.PARAMETER ResultFile
    结果 JSON 路径；install.ps1 通过它拿到产物路径与版本。
#>
param(
    [Parameter(Mandatory = $true)][string]$Plugin,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$Bump,
    [switch]$NoSign,
    [string]$ResultFile = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$pluginRoot = Join-Path $repoRoot (Join-Path 'plugins' $Plugin)
if (-not (Test-Path $pluginRoot)) { throw "plugin not found: $pluginRoot" }

$metaFile = Join-Path $pluginRoot 'plugin.psd1'
if (-not (Test-Path $metaFile)) { throw "plugin.psd1 not found: $metaFile" }
$meta = Import-PowerShellDataFile $metaFile

$manifest = Join-Path $pluginRoot $meta.Manifest
$project = Join-Path $pluginRoot $meta.Project
if (-not (Test-Path $manifest)) { throw "manifest not found: $manifest" }
if (-not (Test-Path $project)) { throw "project not found: $project" }

# PowerShell 5.1 会把无 BOM 的 UTF-8 文件按 ANSI(GBK) 读，清单里的中文会把后面的 "<" 吞掉，
# [xml] 于是报 tag mismatch。读写清单一律显式 UTF-8。
function Get-ManifestVersion {
    ([xml]([IO.File]::ReadAllText($manifest, [Text.Encoding]::UTF8))).Package.Identity.Version
}

# 只改 <Identity> 内的 Version。全局 Version="..." 正则会误伤 XML 声明
# 的 version="1.0" 和 TargetDeviceFamily 的 MinVersion，makeappx 报 0xC00CEE40。
function Set-ManifestVersion([string]$version) {
    $raw = [IO.File]::ReadAllText($manifest, [Text.Encoding]::UTF8)
    $raw = $raw -replace '(<Identity[^>]*?Version=")[^"]+(")', ('${1}' + $version + '${2}')
    [IO.File]::WriteAllText($manifest, $raw, (New-Object Text.UTF8Encoding($false)))
}

$version = Get-ManifestVersion

if ($Bump) {
    $installed = Get-AppxPackage $meta.Name -ErrorAction SilentlyContinue
    $current = [version]$version
    if ($installed -and ([version]$installed.Version -ge $current)) {
        $version = '{0}.{1}.{2}.0' -f $current.Major, $current.Minor, ($current.Build + 1)
        Set-ManifestVersion $version
        Write-Host "版本递增：$($installed.Version) -> $version"
    }
}

# 1) 构建
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
Write-Host "build: $project ($Configuration)"
& $dotnet build $project -c $Configuration -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

$projDir = Split-Path $project -Parent
$bin = Get-ChildItem (Join-Path $projDir "bin\$Configuration") -Directory -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'win-x64' } | Select-Object -First 1 -ExpandProperty FullName
if (-not $bin) { throw "build output not found under $(Join-Path $projDir "bin\$Configuration")" }

# 2) 布局
$pkgDir = Join-Path $pluginRoot 'package'
$layout = Join-Path $pkgDir 'layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force -Path $layout | Out-Null
Copy-Item (Join-Path $bin '*') $layout -Recurse -Force
Copy-Item $manifest (Join-Path $layout 'AppxManifest.xml') -Force

# 3) 打包工具（来自 NuGet 的 Microsoft.Windows.SDK.BuildTools）
$btRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
$btDir = Get-ChildItem $btRoot -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $btDir) { throw 'Microsoft.Windows.SDK.BuildTools not found in NuGet cache' }
$makeappx = Get-ChildItem (Join-Path $btDir.FullName 'bin') -Recurse -Filter makeappx.exe |
    Where-Object { $_.DirectoryName -like '*x64' } | Select-Object -First 1 -ExpandProperty FullName
$signtool = Get-ChildItem (Join-Path $btDir.FullName 'bin') -Recurse -Filter signtool.exe |
    Where-Object { $_.DirectoryName -like '*x64' } | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeappx) { throw 'makeappx.exe not found in BuildTools package' }

$msix = Join-Path $pkgDir ("{0}-{1}.msix" -f $meta.Name, $version)
if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed (0xC00CEE40 / XML invalid?)' }

# 4) 签名
if (-not $NoSign) {
    if (-not $signtool) { throw 'signtool.exe not found in BuildTools package' }
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $meta.CertSubject } | Select-Object -First 1
    if (-not $cert) { throw "development certificate not found ($($meta.CertSubject)); run install.ps1 once to create it" }
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'signtool failed' }
}

$result = [pscustomobject]@{
    Plugin   = $meta.Name
    Version  = $version
    Msix     = $msix
    Layout   = $layout
    Manifest = $manifest
}
if ($ResultFile) { $result | ConvertTo-Json | Set-Content -Path $ResultFile -Encoding utf8 }
$result
