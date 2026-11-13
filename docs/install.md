---
id: docs.install
title: 安装与更新（零残留流程，通用）
summary: 9 步安装流程、禁止项，以及包/持久缓存/UIA 渲染三层残留核对。
type: how-to
kind: standard
status: active
owner: "@simiely"
tier: critical
topics: [install]
defines: install-guide
source_type: code
source: shared/build/install.ps1
last_reviewed: 2026-11-11
related: ["docs.troubleshooting:see-also"]
---

# 安装与更新（零残留流程，通用）

> 基线：2026-11-06 · 宿主 `PowerToys 0.100.2` / `CmdPal UI 0.11.11762.0` / `SparseApp 0.100.2.0`
> 脚本：`shared/build/install.ps1`（参数化，按 `plugins/<Plugin>/plugin.psd1` 取插件常量）
> 本文的机制结论来自 CmdPalLinks `0.1.2.0` 的实测；插件特有数值见各插件 `docs/install-notes.md`。

## 0. 结论

流程是「关 → 卸 → 装 → 启」，但卸载前必须把扩展的 `LocalState` 快照到临时目录、装完原样恢复：
`Remove-AppxPackage` 会删除包内 `LocalState`（实测），而 `-PreserveApplicationData` 对普通签名包无效
（实测 `0x80073CFA`）。

```
递增版本 → 构建 → 打包签名 → 快照 LocalState → 停 CmdPal UI + 扩展宿主 → 等 2 秒并断言
        → 卸载旧包 → Add-AppxPackage → 恢复 LocalState → 校验（哈希/Provider 条数/包注册数）→ 重开 CmdPal
```

## 1. 正确顺序（`shared/build/install.ps1` 的 9 步）

| 步骤 | 动作 | 关键点 |
| --- | --- | --- |
| 1 | 版本必须高于已安装版本 | 改 `<Identity Version>`；脚本自动递增 revision |
| 2 | 构建 | `dotnet build <插件工程> -c Debug` |
| 3 | 布局 + 打包 + 签名 | `makeappx pack` → `signtool sign /fd SHA256 /sha1 <thumb>`（工具来自 NuGet BuildTools） |
| 4 | 用户数据快照 | `%LOCALAPPDATA%\Packages\<PFN>\LocalState` → `%TEMP%\cmdpal-<Plugin>-userdata-snapshot` |
| 5 | 停进程并等待 | 停 `Microsoft.CmdPal.UI` + 插件进程 → `Start-Sleep 2` → 断言进程已退出 |
| 6 | 卸载旧包 | `Remove-AppxPackage -Package <PackageFullName>`（**不加** `-PreserveApplicationData`） |
| 7 | 安装新包 | `Add-AppxPackage -Path <插件>-<版本>.msix`（**不加** `-ForceUpdateFromAnyVersion`） |
| 8 | 恢复数据 + 校验 | 快照复制回 `LocalState`；逐文件 SHA256 比对；打印包/缓存条数 |
| 9 | 重开面板 | `explorer.exe shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App` |

一键执行（任选其一）：

```powershell
# 插件薄封装（推荐）
powershell -NoProfile -ExecutionPolicy Bypass -File plugins\CmdPalLinks\tools\install.ps1

# 通用脚本
powershell -NoProfile -ExecutionPolicy Bypass -File shared\build\install.ps1 -Plugin CmdPalLinks
```

可选开关：`-NoBump`（版本已手工改好）、`-NoRestart`（装完不重开面板，自动化测试用）、
`-SkipBuild`（直接安装已有产物）。

## 2. 必须停的进程

| 进程 | 为什么必须停 |
| --- | --- |
| `Microsoft.CmdPal.UI.exe` | 命令注册与命令列表缓存在宿主进程内；运行中装包会让新旧注册同时留在内存里，首页出现两条同名条目 |
| `<插件进程>`（如 `CmdPalLinks.exe` / `EverythingCmdPal.exe`） | COM 扩展宿主占用包内 DLL，不停会报 `0x80073D02`（文件被占用） |

`PowerToys.exe` / `PowerToys.Awake` / `FancyZones` / `QuickAccess` 不需要停。
停完必须**等 2 秒**再卸载，并断言进程真的退出。

## 3. 版本递增规则

- 每次构建都让 `<Identity Version="...">` 变大，粒度是第三段 +1：`0.1.1.0 → 0.1.2.0`。
  与 EverythingCmdPal 的 `1.3.3 → 1.3.3.1 → … → 1.3.4` 做法一致。
- 同版本号 + 不同内容装不上：`0x80073CFB`（`提供的程序包具有与已安装的程序包相同的标识，但内容不相同`）。
- 改版本时**只能改 `<Identity>` 内的 `Version`**（见 §9.7）。
- 每个插件的版本号相互独立，不要求全局唯一；同名同编号才会冲突。

## 4. 禁止项

| 禁止 | 证据 / 后果 |
| --- | --- |
| `Add-AppxPackage -ForceUpdateFromAnyVersion` | 会残留 staged 旧版本，CmdPal 出现重复条目 |
| 面板运行中安装 | 宿主把新旧命令注册都留在会话内 → 首页重复条目 |
| 同版本覆盖 | `0x80073CFB`，只能升版本或卸载 |
| 卸载后不备份 `LocalState` | 包内数据随包删除（实测：卸载 PowerToys 后宿主 `LocalState` 整目录消失） |
| 卸载时加 `-PreserveApplicationData` | 普通签名包报 `0x80073CFA` |
| 手工在宿主 `settings.json` 给 SparseApp 写 `IsEnabled=false` | 实测无效：SparseApp 照旧启动、照旧抛「You forgot to start the extension」 |
| 只重启扩展宿主、不重启面板 | 命令注册不重建，旧注册继续显示 |

## 5. 残留的判定与清理

判定顺序（全部只读，先取证再动手）：

1. **包层**：`Get-AppxPackage <Name> | Select Name,Version,PackageFullName,Status` —— 只应 1 条。
2. **宿主缓存层**：`…\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\commandProviderCache.json`
   与 `settings.json` 的 `ProviderSettings` / `CommandHotkeys` —— 每个 Provider 只应 1 条。
3. **渲染层**：UIA 枚举首页 `ControlType.ListItem`，同一插件名只应 1 项。

清理命令（只在对应层确认重复时执行）：

```powershell
# ① 旧包/多版本：按第 1 节流程重装（脚本自带快照与恢复）
powershell -NoProfile -ExecutionPolicy Bypass -File plugins\CmdPalLinks\tools\install.ps1

# ② 缓存层重复：关面板 → 删缓存（不删 settings.json）→ 重开
Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\commandProviderCache.json" -Force
Start-Process 'explorer.exe' 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
```

若 `settings.json` 里出现**已卸载扩展**的键，照 `plugins/EverythingCmdPal/tools/clean_cmdpal_settings.py`
的做法：关面板 → 只删该扩展前缀的键 → 重启；不要整文件删除。

## 6. 两个插件的脚本差异

| 动作 | EverythingCmdPal | CmdPalLinks |
| --- | --- | --- |
| 一键更新 | `plugins/EverythingCmdPal/tools/update.cmd` | `plugins/CmdPalLinks/tools/install.ps1` |
| 独立安装脚本 | `tools/install.ps1 -Signed`（`-Signed` 走通用流程；缺省为松散注册） | 无（直接走通用流程） |
| 数据保护 | 无（设置丢失不显眼） | 快照 + 装后恢复 + SHA256 校验 |
| 残留自检 | 无 | 打印包/缓存/设置条数 |

两个插件的构建/打包/安装逻辑都已收敛到 `shared/build/`，插件目录只留薄封装。

## 7. 实测记录（2026-11-06，CmdPalLinks 0.1.2.0）

1. **回退 PowerToys**：`winget uninstall --id Microsoft.PowerToys --version 0.102.3001.0` 日志末尾为 `已成功卸载`；
   此步连带删除 `Microsoft.CommandPalette` 与 `Microsoft.PowerToys.SparseApp` 包及宿主 `LocalState`（`Test-Path` = False）。
   直接 `winget install --version 0.100.2` 被陈旧 ARP 记录拦住，日志为「找到已安装的现有包…没有可用的较新的包版本」；
   改用 `--force` 后重新下载官方 `PowerToysUserSetup-0.100.2-x64.exe`，日志末尾 `已成功安装`。
   版本核对：`PowerToys 0.100.2`、`Microsoft.CommandPalette 0.11.11762.0 Ok`、
   `SparseApp 0.100.2.0 Ok`、`CmdPalLinks 0.1.1.0 Ok`。
2. **恢复宿主配置**：`settings.json` 用升级前备份（9975 B，与 0.100.2 匹配；11552 B 那份含 0.13 的 Dock 键，未使用），
   `state.json` / `apps.settings.json` 用备份原文件；`commandProviderCache.json` 不恢复，删掉让宿主重建。
   恢复后逐行 `Compare-Object` 差异 0；`ProviderSettings` 20 条、`CommandHotkeys` 1 条。
3. **按新流程装 0.1.2.0**：卸载 `0.1.1.0` → 装 `0.1.2.0`（`Status: Ok`）→ 快照恢复；
   `LocalState` 8 个文件 SHA256 与安装前**全部一致**（`links.json` 3013 B / `18A06DC7…`、`links.json.bak` 3014 B、
   `settings.json` 108 B / `A370B098…`、`links-editor.html` 58001 B、`icons` ×4），`items=7`。
4. **残留核对**：`Get-AppxPackage CmdPalLinks` 1 条（`0.1.2.0`）；`commandProviderCache.json` 中
   `CmdPalLinks` 1 条；宿主 `ProviderSettings` 中 `CmdPalLinks` 1 条；`CommandHotkeys` 1 条。
5. **首页条数（UIA）**：`ControlType.ListItem` 共 5 项 —— 占位项、`搜索应用`、`计算器`、`Everything 搜索`、`链接库`；
   **名称=「链接库」的只有 1 项**，副标题 `共 7 条`。
6. **日志（0.100.2）**：每次宿主启动恰好 `SparseApp` 异常 1 次、`Started extension CmdPalLinks` 1 次、
   `Loaded 1 command(s) … from CmdPalLinks` 1 次；本次观察窗口内没有重载循环。

## 8. PowerToys 0.13 与 0.11 的可观测差异

| 项 | 0.11.11762（当前保留） | 0.13.13001（曾升级，已回退） |
| --- | --- | --- |
| 日志汇总行 | `Loaded 3 command(s) and 0 band(s) from 2 extension(s)` | 改为逐 Provider 输出 `Loaded N command(s) … from <ext>` |
| 宿主设置 | 9975 B | 11552 B（新增 Dock 相关键） |
| SparseApp 异常 | 启动时 1 次 | 启动时 1 次（数量相同） |
| 重载循环 | 本次干净安装后未复现（历史旧环境曾 29 秒 7 次） | 静止/交互 60 秒均为 1 次 |
| 检索结果条数 / 排序 / 召回 | — | **需用户主观确认**（两版都未改我们的搜索实现） |

> 用户结论：0.13 检索体验变差，**不再升级 PowerToys**。本文件不提供升级建议。

## 9. 经验总结（2026-11-06 本轮实测）

### 9.1 卸载会连带删数据，所以流程必须「先快照再卸载」

- `winget uninstall Microsoft.PowerToys 0.102.3001.0` 不只会删 PowerToys：`Microsoft.CommandPalette` 与
  `Microsoft.PowerToys.SparseApp` 两个包一起消失，宿主 `%LOCALAPPDATA%\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe`
  整个目录被删除（`Test-Path` = False）。
- `Remove-AppxPackage -PreserveApplicationData` 对普通签名 MSIX **报 `0x80073CFA`**（该参数只对部署模式的包生效）。
- 因此固定为：**快照 `LocalState` → 卸载 → 安装 → 装后原样恢复 → SHA256 逐文件校验**。
  正常路径下哈希必须全一致，否则脚本直接抛错。

### 9.2 同版本覆盖必然失败，版本号要每次递增

- 同标识、同版本、内容不同 → `0x80073CFB`：`提供的程序包具有与已安装的程序包相同的标识，但内容不相同…请提高版本号`。
- 只能「卸载重装」或「升版本」；而卸载会删数据（见 9.1），所以**每次构建都递增 `<Identity Version>`**。

### 9.3 陈旧 ARP 登记会让 winget 误判「已安装」

- 现象：`winget install --id Microsoft.PowerToys --exact --version 0.100.2` 被拒，输出
  「找到已安装的现有包，正在尝试升级…没有可用的较新的包版本」，退出码 `-1978335189`；而包实际已被卸载，
  只是 HKCU 里还留着旧版 `{5FC150C0-…}` 记录（`InstallLocation` 指向已不存在的文件）。
- 处理：核对 **HKLM / HKLM\WOW6432Node / HKCU** 三处 ARP；确认没有可用卸载程序时用 `--force` 重装，
  或直接运行官方安装包。
- 装完出现两条同版本登记（用户安装记录 + 机器 MSI）属正常组合，不是残留；`winget list` 只显示一行。

### 9.4 首页重复条目 ≠ 装了两个包

- `Get-AppxPackage` 只有 1 条、持久存储里也只有 1 条 Provider 记录；两条 UI 条目共用同一个
  `CommandId` 与同一个热键 —— **宿主会话内重复注册/重复渲染**，不是包并存。
- 放大因素：`SparseApp` 启动抛 `System.ArgumentException: You forgot to start the extension…` 时，历史环境
  会把全部扩展整轮重载（29 秒内 7 次），重复条目随之出现。
- 另一类与时序有关、可被扩展侧一次性修掉的成因见 §9.10（0.1.5.0 已修复）；两类在 UIA 上都表现为
  「同一名称出现两次」，所以取证顺序固定为：**包层 → 缓存层 → UIA 条数 → 日志时序**（§9.6）。
- 干净安装 + **完整重启 CmdPal**（不是只重启扩展宿主）能清掉已发生的重复；若日志里仍是
  `loaded=False` → `loaded=True` 两次 `TopLevelCommands`，说明扩展还在启动期抢跑，属代码问题，重启治不了。

### 9.5 进程、等待与禁止项

- 必须停：`Microsoft.CmdPal.UI.exe`（命令注册缓存在进程内）与插件进程（占用包内文件，否则 `0x80073D02`）。
- 停完 `Start-Sleep 2` 并**断言进程已退出**，再卸载/安装。
- 禁止：面板运行中安装、同版本覆盖、`-ForceUpdateFromAnyVersion`、卸载前不备份数据、只重启扩展宿主。

### 9.6 残留判定（三层，先只读取证）

1. 包层：`Get-AppxPackage <Name>` 只应 1 条；`winget list --id Microsoft.PowerToys --exact` 只应 1 行目标版本。
2. 缓存层：`commandProviderCache.json` 与宿主 `settings.json` 的 `ProviderSettings`/`CommandHotkeys`，
   每个 Provider 只应 1 条。
3. 渲染层：UIA 枚举首页 `ControlType.ListItem`，同一插件名只应 1 项。
清理命令见 §5（缓存重复只删缓存文件，不删 `settings.json`；旧包按 §1 重装）。

### 9.7 `makeappx` 被版本号正则与编码弄坏的坑

- 用全局 `Version="[^"]+"` 改版本，会把 XML 声明的 `version="1.0"` 和 `TargetDeviceFamily` 的
  `MinVersion="10.0.19041.0"` 一起替换成新版本号，`makeappx` 报 `0xC00CEE40`（XML 非法）。
- 现在只匹配 `<Identity … Version="…">`（`shared/build/package.ps1` 的 `Set-ManifestVersion`）。
- **编码坑**：PowerShell 5.1 按 ANSI(GBK) 读无 BOM 的 UTF-8 文件，清单里的中文 `DisplayName` 会把紧随的
  `<` 吞成 `?`，`[xml]` 报 `tag mismatch`（`makeappx` 根本还没跑到）。读写清单必须显式
  `[IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)`；本仓库新建的 `.ps1` / `.psd1` 一律保存为
  **UTF-8 带 BOM**。

### 9.8 0.13 与 0.11 的已知差异

- 0.13 日志改为逐 Provider 输出；0.11 是汇总行 `Loaded 3 command(s) … from 2 extension(s)`。
- 0.13 宿主设置新增 Dock 键（9975 → 11552 B）。
- **「检索体验变差」无法用日志/计数取证，需用户主观确认**——两版都没有改我们的搜索实现，本轮只能记录用户结论，不做技术归因。

### 9.9 接手检查清单

1. 装前：版本已递增、`plugin.psd1` 的 PFN 与实际一致、快照目录可写。
2. 装后：`Status: Ok`、`LocalState` 哈希一致、缓存/设置各 1 条、首页 1 条。
3. 出问题先看日志 `…\PowerToys\CmdPal\Logs\<版本>\Log_*.log`，再按 §9.6 三层取证；不要先卸载。

### 9.10 启动期 `RaiseItemsChanged()` 抢跑登记 → 同一 provider 两行（0.1.5.0）

**宿主时序**（0.11.11762，`TopLevelCommandManager.cs`）：

1. `TryLoadCommandsAsync` → `LoadTopLevelCommandsFromProvider` → 扩展 `TopLevelCommands()`，**先把数组取走**；
2. 等所有 provider 加载任务完成后，`RegisterAndLoadCommandsAsync` 才逐个 `TopLevelCommands.Add()`。

`CommandsChanged`（扩展 `RaiseItemsChanged()`）→ `UpdateCommandsForProvider`：按 `CommandProviderId`
`RemoveAll` + `InsertRange`，**本身不会重复**。但如果扩展在启动加载完成时就 raise，此刻第 2 步还没跑、
顶层列表是空的 → 宿主直接插入一份**新建**的 `TopLevelViewModel`；随后第 2 步又 append 掉登记时那份，
同一 provider 就有了两行。

- **可复现条件**：扩展首次 `TopLevelCommands()` 返回后、宿主 append 之前触发 provider 级
  `RaiseItemsChanged()`（启动加载 30ms 量级完成时即触发）。
- **日志特征**：同一会话两次 `provider: TopLevelCommands called, loaded=False` → `loaded=True`，
  而 `Loaded 1 command(s) and 0 band(s) from CmdPalLinks …` 只有一次；
  包层/缓存层各只有 1 条（区别于真多包）。
- **排查顺序**：`Get-AppxPackage` 条数 → `commandProviderCache.json` / `ProviderSettings` 条数 →
  UIA 首页 `ListItem` 计数 → 日志时序。前三层都是 1，只有 UI 是 2，再对上日志两次 `TopLevelCommands`，
  即可判定为时序型重复。
- **清理命令**：已发生的重复只能靠完整重启面板清掉（`Stop-Process Microsoft.CmdPal.UI`，再重开）；
  要根治必须改扩展（0.1.5.0：首次 `TopLevelCommands()` 内同步 `WaitForLoad()` 并写定副标题，
  启动与 `_store.Changed` 都不再 `RaiseItemsChanged`）。
- **Id 连带约束**：`TopLevelViewModel.Id = WyHash64(ProviderId + DisplayTitle + Title + Subtitle)`，
  只在 `InitializeProperties` / `Command` / `IsInitialized` 变化时重算。所以副标题要在首次返回前写定；
  否则重建顶层项会让 Id 随 `共 N 条` 变化，已绑定的 `CommandHotkeys` 失效。
- **本轮实测**：0.1.4.0 首页 ListItem 6 项 = 1 分组头 + 5 命令（2 条「链接库」）；
  0.1.5.0 5 项 = 1 分组头 + 4 命令（1 条「链接库」，`共 7 条` + `Win + Ctrl + Z` 仍在）。

### 9.11 0.1.5.0 安装记录（本轮补记）

- 版本递增：`0.1.4.0 → 0.1.5.0`（`Package.appxmanifest` 的 `<Identity Version>`，构建前手工改好，装时用 `-NoBump`）。
- 卸载会删 `LocalState` 的规则不变（见 §9.1）：脚本快照 **17 个文件** → 卸载 `0.1.4.0` →
  安装 `0.1.5.0`（`Status: Ok`）→ 逐文件恢复 → **SHA256 all match** → `packages=1 providerCacheRefs=1`。
- 装后只读核对：`links.json` 仍 7 条 / `schema=2`，两条 emoji（🧩、🖥️）与其余 `auto` 条目字段未变。
