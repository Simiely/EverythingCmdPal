# CmdPalLinks 安装/更新说明（插件特有）

> 通用机制、禁止项与经验总结见仓库根 [docs/install.md](../../../docs/install.md)。
> 本文只记录本插件的具体数值与命令。

## 标识

| 项 | 值 |
| --- | --- |
| MSIX Identity Name | `CmdPalLinks` |
| PackageFamilyName | `CmdPalLinks_byt16r9dq2e2y` |
| Publisher | `CN=EverythingCmdPal Dev`（与 EverythingCmdPal 共用开发证书） |
| 当前版本 | `0.1.3.0` |
| 进程 | `CmdPalLinks.exe` |
| 用户数据 | `%LOCALAPPDATA%\Packages\CmdPalLinks_byt16r9dq2e2y\LocalState` |
| 快照目录 | `%TEMP%\cmdpal-CmdPalLinks-userdata-snapshot` |

> PFN 由 `Identity Name` + `Publisher` 派生。**改动 Publisher 会换 PFN，等于换数据目录** —— 会表现为「用户数据丢失」。

## 命令

```powershell
# 安装/更新（自动递增版本 → 打包签名 → 快照 → 停 → 卸 → 装 → 恢复 → 校验 → 重开）
powershell -NoProfile -ExecutionPolicy Bypass -File plugins\CmdPalLinks\tools\install.ps1

# 只构建打包，不安装（产物在 plugins\CmdPalLinks\package\CmdPalLinks-<版本>.msix）
powershell -NoProfile -ExecutionPolicy Bypass -File shared\build\package.ps1 -Plugin CmdPalLinks
```

`install.ps1` 支持 `-NoBump` / `-NoRestart` / `-SkipBuild`，以及 `-Configuration Release`。

## LocalState 期望内容

安装 0.1.3.0 后实测（恢复后 SHA256 应与安装前一致；`links-editor.html` 自 0.1.3.0 起不再解出到 LocalState）：

| 文件 | 体积 | 说明 |
| --- | --- | --- |
| `links.json` | 3013 B | 条目数据，实测 `items=7` |
| `links.json.bak` | 3014 B | 原子写回退副本 |
| `settings.json` | 108 B | `showFullPath` / `layout` / `showSubtitle` / `showTags` |
| `icons\*.png` | 图标缓存 | 自绘图标按 `r2\|kind\|color\|emoji\|title` 哈希命名 |

校验脚本（只读）：

```powershell
$ls = "$env:LOCALAPPDATA\Packages\CmdPalLinks_byt16r9dq2e2y\LocalState"
Get-ChildItem $ls -Recurse -File | ForEach-Object {
    "{0}`t{1}`t{2}" -f $_.Name, $_.Length, (Get-FileHash $_.FullName -Algorithm SHA256).Hash
}
```

## 安装后自检

```powershell
$p = Get-AppxPackage CmdPalLinks
"packages={0} version={1} status={2}" -f @($p).Count, $p.Version, $p.Status

$cache = "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\commandProviderCache.json"
(Select-String -Path $cache -Pattern 'CmdPalLinks' -AllMatches).Matches.Count   # 期望 1

# 首页条数（UIA）：名称=「链接库」的 ListItem 只应 1 项
```

## 当前状态（2026-11-06）

- 已装包 `CmdPalLinks 0.1.2.0`，`Status: Ok`，用户数据零丢失（哈希全一致）。
- 首页「链接库」1 条，副标题「共 7 条」。
- PowerShell 中 `@()` 包裹 `Get-AppxPackage` 可避免单对象没有 `Count` 的问题。
