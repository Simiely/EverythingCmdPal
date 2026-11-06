# Command Palette 宿主笔记（版本与 API 约束）

> 记录实测到的宿主行为差异与扩展 API 边界。改动宿主版本前先读本文。

## 1. 本机版本基线

| 组件 | 保留版本（当前） | 曾升级（已回退） |
| --- | --- | --- |
| PowerToys | `0.100.2` | `0.102.3001.0` |
| `Microsoft.CommandPalette` | `0.11.11762.0` | `0.13.13001.0` |
| `Microsoft.PowerToys.SparseApp` | `0.100.2.0` | `0.102.3001.0` |
| SDK 包 | `Microsoft.CommandPalette.Extensions 0.11.260520004` | `0.12.260812002`（可用） |
| TFM | `net10.0-windows10.0.26100.0`，`MinVersion 10.0.19041.0` | 同 |

> 用户结论：0.13 检索体验变差，**不升级**。本轮不提供升级建议。

## 2. 0.11 与 0.13 的可观测差异

| 项 | 0.11.11762 | 0.13.13001 |
| --- | --- | --- |
| 日志汇总 | `Loaded 3 command(s) and 0 band(s) from 2 extension(s)` | 逐 Provider 输出 `Loaded N command(s) … from <ext>` |
| 宿主 `settings.json` | 9975 B | 11552 B（新增 Dock 键） |
| SparseApp 异常 | 启动 1 次 | 启动 1 次（数量相同） |
| 重载循环 | 干净安装后未复现 | 静止/交互 60 秒均 1 次 |
| 检索体验 | 用户认可的基线 | 用户反馈变差，**无法用日志取证**（未改搜索实现，只能主观确认） |

升级前的设置备份与降级后版本可能不兼容：`11552 B` 那份含 0.13 的 Dock 键，
回退到 0.100.2 时应使用与 0.100.2 匹配的 `9975 B` 版本。

## 3. 扩展 API 边界（0.11 实测）

| 能力 | 结论 | 依据 |
| --- | --- | --- |
| `Details.Content`（详情栏渲染图片） | 公开 SDK 0.11/0.12 都没有 | IDL 无该成员 |
| 运行期修改 `ShowDetails` | 不可，构造时读取 | 实测 |
| `SearchText` setter | 会触发一次重搜；在回调里写会自激循环 | 实测 |
| 页面切换后搜索框文字 | 宿主清空，扩展只能给目标页预置 | 实测 |
| 页面返回 | 一个方向 push，回程用 `CommandResult.GoBack()`，否则返回栈变深 | 实测 |
| `ChoiceSetSetting` 默认值 | 3 参构造取**第一个选项** | 实测 |
| 列表排序权重 / pin / importance | IDL 无；只有 `IListItem.Section` 与 `GetDockBands()` | IDL |
| 网格每行列数 / 图标尺寸 | `IGridProperties` 无属性；Medium 仅 `ShowTitle` | IDL |
| `GetItems()` 调用频率 | 高频，禁止 IO/日志；内存索引 + 变更时重建 | 实测 |
| 扩展进程首次启动 | ~2 秒（.NET + WinRT 初始化） | 实测 |
| 扩展 Logger | `ExtensionHost.LogMessage`；本仓库插件用 `HostLog` 统一格式 | Both |

### 3.1 顶层命令登记时序与 Id 派生（0.11.11762 源码 + 实测）

宿主 `TopLevelCommandManager` 的登记不是原子的，顺序固定为：

1. `TryLoadCommandsAsync` → `LoadTopLevelCommandsFromProvider` → `CommandProviderWrapper.LoadTopLevelCommands`
   → 扩展 `TopLevelCommands()`：**此时只把数组取走**；
2. 所有 provider 的加载任务 `Task.WhenAll` 完成后，`RegisterAndLoadCommandsAsync` 才逐个
   `TopLevelCommands.Add(c)`。

补充事实：

- `CommandsChanged`（扩展 `RaiseItemsChanged()`）→ `UpdateCommandsForProvider` 按 `CommandProviderId`
  `RemoveAll` + `InsertRange`，本身幂等；但它在第 2 步之前完成时，列表为空 → 直接插入新实例，
  随后第 2 步再 append 登记实例，同一 provider 两行。**启动期不要 raise**。
- `TopLevelViewModel.Id` = `WyHash64(CommandProviderId + DisplayTitle + Title + Subtitle)`（`GenerateId()` 在
  `InitializeProperties` 与 `Command`/`IsInitialized` 变化时算；后续副标题变化不重算）。
  `CommandHotkeys` 按这个 Id 绑定，所以副标题必须在首次 `TopLevelCommands()` 返回前写定，
  否则登记后改副标题 / 重建顶层项都会换 Id、快捷键失效。
- 副标题不需要 `ItemsChanged`：`ICommandItem` 的 `SetProperty` 会经 `PropChanged` → `CommandItemViewModel`
  → `TopLevelViewModel.PropChanged` 传到 UI。
- 分组头在 UIA 里也是 `ListItem`（高 28、名称「命令」），统计首页命令数时按名称过滤。

## 4. 不受支持或不可依赖的做法

- **无包身份的 unpackaged 运行**：扩展发现依赖包身份，官方模板的 `(Unpackaged)` profile 不会部署包。
- **`Add-AppxPackage -Register` 松散注册**：需要开发者模式，否则 `0x80073CFF`。
- **手改宿主 `settings.json` 禁用 SparseApp**：实测无效。
- **同版本覆盖安装**：`0x80073CFB`，见 [install.md](install.md)。
- **启动期用 `RaiseItemsChanged()` 刷新顶层副标题**：会被宿主当成「命令列表变了」，在登记完成前重建顶层项，
  造成重复条目与 Id/快捷键漂移，见 §3.1。

## 5. 调试与日志

- 日志：`%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\<宿主版本>\Log_YYYY-MM-DD.log`
- 官方模板：`ref/PowerToys/src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension/`（本地参考，未入库）
- 扩展侧日志用 `HostLog`，格式固定为 `模块: 事件 key=value`，便于 grep。
