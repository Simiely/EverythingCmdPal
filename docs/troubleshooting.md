# 排障（Command Palette 扩展通用）

> 安装流程见 [install.md](install.md)；宿主 API 与版本差异见 [host-notes.md](host-notes.md)。

## 1. 症状 → 先查什么

| 症状 | 先确认 | 常见原因 |
| --- | --- | --- |
| 首页看不到插件 | `Get-AppxPackage <Name>` 是否有 1 条且 `Status: Ok` | 没装成功、包被卸载、宿主没重启 |
| 首页出现两条同名条目 | 包数 / 持久缓存条数 / UIA 条数 / 日志时序（见 §2） | 面板运行中安装、`-ForceUpdateFromAnyVersion` 残留、会话内重复注册、扩展启动期 `RaiseItemsChanged()` 抢跑登记 |
| 条目在但点了没反应 | 扩展进程是否在跑；`Log_*.log` 有无异常 | 扩展进程启动失败、数据文件损坏 |
| 列表空白 / 一直加载 | `GetItems()` 是否抛异常；数据文件是否可解析 | `GetItems()` 里做了重活、JSON 损坏 |
| 安装报错 | 错误码（见 §4） | 版本没递增、文件被占用、证书不受信任 |
| 检索体验变化 | 无法用日志取证 | 宿主版本变化（见 [host-notes.md](host-notes.md)），需主观确认 |

日志位置：

```
%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\<宿主版本>\Log_YYYY-MM-DD.log
```

## 2. 「首页重复条目」取证（三层，只读）

### 层 1：包

```powershell
$p = @(Get-AppxPackage CmdPalLinks)
"count={0}" -f $p.Count
$p | Select-Object Name, Version, PackageFullName, Status
```

`count > 1` 才是真的多包；否则继续往下。

### 层 2：持久缓存

```powershell
$ls = "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState"
(Select-String -Path "$ls\commandProviderCache.json" -Pattern 'CmdPalLinks' -AllMatches).Matches.Count
(Get-Content "$ls\settings.json" -Raw | ConvertFrom-Json).ProviderSettings.Count
```

持久层只有 1 条，说明重复发生在宿主会话内。

### 层 3：渲染（UIA）

面板窗口在后台时 DWM 会 cloaked，`AutomationElement.FromHandle` 取不到子项。取证要点：

1. 先激活一个**普通权限**窗口（前台是提权窗口时，合成的 `Win+Alt+Space` 会被系统忽略）。
2. 发送热键打开面板，等 `cloaked = 0`。
3. 枚举 `ControlType.ListItem`，统计名称等于插件显示名的项数。

```powershell
Add-Type -AssemblyName UIAutomationClient
# $hwnd 为 Command Palette 窗口句柄；cloak 检查见 docs/install.md §9.4
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ListItem)
$root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond) |
    ForEach-Object { $_.Current.Name }
```

注意：分组头也是一个 `ListItem`（高度 28，名称「命令」），统计插件条数时按名称过滤，别把总数当命令数。

### 层 4：日志时序（区分「宿主重载」与「扩展抢跑」）

```powershell
$log = "$env:LOCALAPPDATA\Microsoft\PowerToys\CmdPal\Logs\0.100.2.0\Log_$(Get-Date -f yyyy-MM-dd).log"
Select-String -Path $log -Pattern 'TopLevelCommands called|Loaded \d+ command\(s\) and 0 band\(s\) from .*CmdPalLinks|Started extension CmdPalLinks' |
  Select-Object -Last 30 | ForEach-Object { $_.Line }
```

| 日志形态 | 判定 |
| --- | --- |
| 同一会话 `loaded=False` → `loaded=True`，`Loaded 1 command(s) from CmdPalLinks` 只有一次 | 扩展启动期 `RaiseItemsChanged()` 抢在宿主 append 之前插入新实例（0.1.5.0 已修） |
| 短时间多次 `Started extension …` + 多次 `Loaded …` | 宿主整轮重载（SparseApp 异常放大），见 §5 |
| 每会话只 1 次 `Started` / 1 次 `Loaded 1 command(s)`，但仍显示 2 条 | 已发生的历史重复，完整重启面板即可；若重启后重现，回到层 1 |

时序型重复的机制、可复现条件与 Id 连带约束见 [install.md](install.md) §9.10。

## 3. 重复条目的处理

1. **不要只重启扩展宿主**。停 `Microsoft.CmdPal.UI` 与插件进程，重新打开面板。
2. 仍重复 → 按 [install.md](install.md) §1 走一遍完整安装（不要 `-ForceUpdateFromAnyVersion`）。
3. 缓存层有已卸载扩展的残留键（`settings.json` 的 `ProviderSettings`）→ 关面板，**只删该扩展前缀的键**，
   不要整文件删除；参考 `plugins/EverythingCmdPal/tools/clean_cmdpal_settings.py`。
4. 上面都干净、日志却仍是 `loaded=False` → `loaded=True` 两次调用 → 属扩展代码问题（启动期 `RaiseItemsChanged()`
   抢在宿主 append 之前，见 [install.md](install.md) §9.10），重启治不了；升级到 CmdPalLinks 0.1.5.0 及以后。

## 4. 错误码对照

| 错误码 | 含义 | 处理 |
| --- | --- | --- |
| `0x80073CFB` | 同标识同版本、内容不同 | 递增 `Identity Version` 或卸载重装 |
| `0x80073CFA` | `-PreserveApplicationData` 对普通签名包不可用 | 改「快照 + 装后恢复」 |
| `0x80073D02` | 文件被占用 | 停 `Microsoft.CmdPal.UI` 与插件进程 |
| `0x80073CF6` / `0xC00CEE40` | 清单非法（语言占位符 / 版本正则改坏 XML） | 手写具体语言；只改 `<Identity>` 的 `Version` |
| `0x800B0109` | 证书不受信任 | 证书进 `LocalMachine\Root` + `TrustedPeople` |
| `0x80073CFF` | 松散注册但未开开发者模式 | 开开发者模式或改用签名 MSIX |

## 5. SparseApp 异常与重载循环

日志里反复出现：

```
You forgot to start the extension with "-RegisterProcessAsComServer"... (System.ArgumentException)
```

- 这是宿主发现扩展进程的方式，**每次启动出现 1 次属正常**，不是插件 bug。
- 若出现整轮扩展重载（短时间多次 `Loaded …`），会放大「重复条目」；先按 §3 完整重启，再观察是否复现。
- 在宿主 `settings.json` 里给 SparseApp 写 `IsEnabled=false` 实测**无效**，不要把它当修复手段。

## 6. 数据文件坏了

- CmdPalLinks：`links.json` 读失败会回退 `links.json.bak`；两者都坏则空库但**不覆盖坏文件**。
- 修复：先把坏文件改名留证，再启动一次让扩展重建空库；或从导入功能恢复。
