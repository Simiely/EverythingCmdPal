# 安装/更新 CmdPalLinks：薄封装，流程在 shared/build/install.ps1（docs/install.md）。
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$NoBump,     # 版本已手工改好时用
    [switch]$NoRestart   # 装完不重开 CmdPal（自动化测试用）
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '..\..\..\shared\build\install.ps1') `
    -Plugin CmdPalLinks -Configuration $Configuration `
    -NoBump:$NoBump -NoRestart:$NoRestart
