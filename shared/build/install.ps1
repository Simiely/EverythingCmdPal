<#
.SYNOPSIS
    参数化零残留安装/更新：构建 → 打包签名 → 快照 LocalState → 停进程 → 卸载 → 安装 → 恢复 → 校验 → 重开。
.DESCRIPTION
    对应 docs/install.md 的 9 步。插件名、PFN、证书、进程名来自 plugin.psd1。
    禁止项已固化：不用 -ForceUpdateFromAnyVersion、不在面板运行中安装、同版本必须递增。
.PARAMETER Plugin
    plugins/ 下的插件目录名。
.PARAMETER NoBump
    跳过版本递增（版本已手工改好时用）。
.PARAMETER NoRestart
    装完不重开 CmdPal（自动化测试用）。
.PARAMETER SkipBuild
    跳过构建与打包，直接安装已有产物（仅调试用）。
#>
param(
    [Parameter(Mandatory = $true)][string]$Plugin,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$NoBump,
    [switch]$NoRestart,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$pluginRoot = Join-Path $repoRoot (Join-Path 'plugins' $Plugin)
$meta = Import-PowerShellDataFile (Join-Path $pluginRoot 'plugin.psd1')

function Write-Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

# --- 1. .NET SDK（仅用户目录，新机器也能跑） ---------------------------------
Write-Step '1/9 .NET SDK'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $zip = Join-Path $env:TEMP 'dotnet-sdk-10.0.100-win-x64.zip'
    $url = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-win-x64.zip'
    Write-Host 'downloading .NET SDK 10.0.100 (~290MB)...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    New-Item -ItemType Directory -Force -Path (Split-Path $dotnet -Parent) | Out-Null
    Expand-Archive -Path $zip -DestinationPath (Split-Path $dotnet -Parent) -Force
    Remove-Item $zip -Force
}

# --- 2. 构建 + 打包 + 签名 ----------------------------------------------------
Write-Step '2/9 Build / pack / sign'
$resultFile = Join-Path $env:TEMP ("cmdpal-package-{0}.json" -f $meta.Name)
if ($SkipBuild) {
    $msix = Get-ChildItem (Join-Path $pluginRoot 'package') -Filter "$($meta.Name)-*.msix" |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $msix) { throw 'no packaged msix found (run without -SkipBuild first)' }
    $version = ([xml]([IO.File]::ReadAllText((Join-Path $pluginRoot $meta.Manifest), [Text.Encoding]::UTF8))).Package.Identity.Version
} else {
    $pkgArgs = @{ Plugin = $Plugin; Configuration = $Configuration; ResultFile = $resultFile }
    if (-not $NoBump) { $pkgArgs.Bump = $true }
    & (Join-Path $PSScriptRoot 'package.ps1') @pkgArgs | Out-Null
    if (-not (Test-Path $resultFile)) { throw 'package.ps1 did not produce a result file' }
    $pkg = Get-Content $resultFile -Raw | ConvertFrom-Json
    $msix = $pkg.Msix
    $version = $pkg.Version
}
Write-Host "msix: $msix (version $version)"

# --- 3. 开发证书（首次创建 + 本机信任，一次 UAC） -------------------------------
Write-Step '3/9 Development certificate'
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $meta.CertSubject } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $meta.CertSubject `
        -KeyUsage DigitalSignature -FriendlyName $meta.CertSubject `
        -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(5)
    Write-Host "certificate created: $($cert.Thumbprint)"
    # 证书是安装之后才用到 signtool 的，重新打包一次
    & (Join-Path $PSScriptRoot 'package.ps1') -Plugin $Plugin -Configuration $Configuration -ResultFile $resultFile | Out-Null
    $pkg = Get-Content $resultFile -Raw | ConvertFrom-Json
    $msix = $pkg.Msix
    $version = $pkg.Version
}
$trusted = Get-ChildItem Cert:\LocalMachine\Root -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
if (-not $trusted) {
    $cerDir = Join-Path $pluginRoot 'package'
    New-Item -ItemType Directory -Force -Path $cerDir | Out-Null
    $cer = Join-Path $cerDir 'dev.cer'
    Export-Certificate -Cert $cert -FilePath $cer -Force | Out-Null
    Write-Host 'trusting certificate in LocalMachine\Root (UAC prompt)...'
    $cmd = "certutil -addstore -f Root `"$cer`"; certutil -addstore -f TrustedPeople `"$cer`""
    Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', $cmd
}

# Copy-Item 把目录复制到“已存在的目录”时会再嵌一层同名目录，LocalState 会被污染成
# LocalState\<快照名>\...。改为逐文件复制，目标保持相对路径。
function Copy-Tree([string]$source, [string]$destination) {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem $source -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($source.Length).TrimStart('\')
        $target = Join-Path $destination $relative
        $parent = Split-Path $target -Parent
        if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}

# --- 4. 用户数据快照（卸载会删除 LocalState，0x80073CFA 又救不回来） -------------
Write-Step '4/9 Snapshot LocalState'
$userData = Join-Path $env:LOCALAPPDATA ("Packages\{0}\LocalState" -f $meta.PackageFamilyName)
$snapshot = Join-Path $env:TEMP ("cmdpal-{0}-userdata-snapshot" -f $meta.Name)
if (Test-Path $snapshot) { Remove-Item $snapshot -Recurse -Force }
$hasData = Test-Path $userData
if ($hasData) {
    Copy-Tree $userData $snapshot
    $before = Get-ChildItem $snapshot -Recurse -File | Sort-Object FullName
    Write-Host "snapshot: $($before.Count) file(s) -> $snapshot"
} else {
    Write-Host 'no LocalState yet (first install)'
}

function Get-Hashes([string]$dir) {
    if (-not (Test-Path $dir)) { return @() }
    Get-ChildItem $dir -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($dir.Length); Sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    }
}


# --- 5. 停宿主与扩展，等 2 秒并断言 -----------------------------------------------
Write-Step '5/9 Stop host and extension'
$paletteRunning = Get-Process $meta.HostProcessName -ErrorAction SilentlyContinue
Get-Process $meta.HostProcessName, $meta.ProcessName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$left = Get-Process $meta.HostProcessName, $meta.ProcessName -ErrorAction SilentlyContinue
if ($left) { throw "processes still running: $($left.ProcessName -join ', ')" }

# --- 6. 卸载旧包 ----------------------------------------------------------------
Write-Step '6/9 Remove old package'
$old = Get-AppxPackage $meta.Name -ErrorAction SilentlyContinue
if ($old) {
    $old | ForEach-Object {
        Remove-AppxPackage -Package $_.PackageFullName
        Write-Host "removed $($_.Version)"
    }
}

# --- 7. 安装新包 ----------------------------------------------------------------
Write-Step '7/9 Add new package'
Add-AppxPackage -Path $msix
$installed = Get-AppxPackage $meta.Name
if (-not $installed) { throw 'package not registered after Add-AppxPackage' }
Write-Host "installed: $($installed.Version) ($($installed.Status))"

# --- 8. 恢复数据 + 校验 ----------------------------------------------------------
Write-Step '8/9 Restore LocalState and verify'
if ($hasData) {
    Copy-Tree $snapshot $userData
    $after = Get-Hashes $userData
    $before = Get-Hashes $snapshot
    $diff = Compare-Object $before $after -Property Path, Sha256
    if ($diff) {
        $diff | Format-Table -AutoSize | Out-String | Write-Host
        throw "LocalState hash mismatch after restore ($($diff.Count) differences)"
    }
    Write-Host "LocalState restored: $($after.Count) file(s), SHA256 all match"
}

$pkgCount = @(Get-AppxPackage $meta.Name).Count
$cacheFile = Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\commandProviderCache.json'
$cacheCount = if (Test-Path $cacheFile) {
    (Select-String -Path $cacheFile -Pattern $meta.Name -AllMatches | Measure-Object).Count
} else { 'n/a' }
Write-Host "verify: packages=$pkgCount providerCacheRefs=$cacheCount status=$($installed.Status)"

# --- 9. 重开面板 -----------------------------------------------------------------
Write-Step '9/9 Start Command Palette'
if (-not $NoRestart) {
    & explorer.exe 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
}

Write-Host "`nDONE. $($meta.Name) $version" -ForegroundColor Green
