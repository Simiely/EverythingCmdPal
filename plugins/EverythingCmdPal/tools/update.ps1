# One-shot build+install for EverythingCmdPal.
# 实际流程在 shared/build/install.ps1（参数化：构建 → 打包签名 → 快照 LocalState →
# 停 CmdPal 与扩展 → 卸载 → 安装 → 恢复 → 校验 → 重开）。这里只固定插件名。
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$NoBump,
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '..\..\..\shared\build\install.ps1') `
    -Plugin EverythingCmdPal -Configuration $Configuration `
    -NoBump:$NoBump -NoRestart:$NoRestart
