# CmdPalLinks 实施规格（Spec）：M1 + M2

> M1：存储/添加/检索（已实机验收）。
> M2：条目动作 / 缺失标记 / 设置页（2026-11-05 仅完成代码与构建验证，实机验证待补，见 §6.6）。

> 目标：在 M0 已验证的 MSIX/COM 骨架上，完成「本地 JSON 存储 + 添加入口 + 内存检索」。
> 本规格为决策级：文件、类型、函数、JSON 结构、算法、验证步骤均已固定，可直接执行。
> 基线：PowerToys 0.100.2.0 / CmdPal 0.11.11762.0 / SDK 0.11.260520004 / net10.0-windows10.0.26100.0。

## 1. 范围

### 做
1. `LocalState` 单文件 JSON 持久化：原子写、`.bak` 回退、URL/路径去重、`schemaVersion`/`updatedAt`。
2. 顶层命令：仅 `链接库`（列表+搜索）；「添加条目」作为页内入口（空态主按钮 + MoreCommands）。
3. 内存检索：标题/别名/标签/域名/文件名/完整目标模糊匹配、排序；空查询显示最近使用。
4. 实机验证 + 破坏性场景验证 + 检索/启动耗时记录。

### M2 追加（2026-11-05，本轮只写代码，未实机验证）
5. 条目动作：打开所在文件夹、复制目标、编辑、删除。
6. 缺失文件标记（仅 `file` / `folder`）。
7. 设置页：`CmdPalLinksCommandsProvider.Settings`，当前仅「显示完整路径」开关。

### 不做（留给 M3+）
批量导入/导出、详情栏、Dock、全局热键、图标抓取与图标缓存（M2 明确后置）、frecency 衰减、删除二次确认对话框（待用户确认，见 §9）。

## 2. 数据模型与 JSON

文件：`Utilities.BaseSettingsPath("CmdPalLinks") + "links.json"`
- 打包运行：`%LOCALAPPDATA%\Packages\CmdPalLinks_<pfn>\LocalState\links.json`
- 未打包运行：`%LOCALAPPDATA%\CmdPalLinks\links.json`

```json
{
  "schemaVersion": 1,
  "updatedAt": "2026-11-05T12:00:00.0000000+00:00",
  "items": [
    {
      "id": "0f3d...guid",
      "kind": "url",
      "title": "GitHub",
      "target": "https://github.com",
      "aliases": ["gh"],
      "tags": ["dev"],
      "createdAt": "2026-11-05T12:00:00.0000000+00:00",
      "updatedAt": "2026-11-05T12:00:00.0000000+00:00",
      "openCount": 0,
      "lastOpenedAt": null
    }
  ]
}
```

- `kind`: `"url" | "file" | "folder"`（字符串，便于向前兼容）。
- 读取时缺失字段给默认值：`aliases/tags=[]`、`openCount=0`、`id=Guid.NewGuid()`（内存补，不立即回写）。
- 未知 `kind` 的条目保留但不参与打开/检索（记录日志）。

## 3. 存储实现（`Services/LibraryStore.cs`）

公开成员：
- `string FilePath`
- `IReadOnlyList<LinkItem> Items`（返回快照）
- `event Action? Changed`
- `LoadResult Load()`：返回 `{ Source: main|bak|empty, ItemCount, ElapsedMs }`
- `AddResult Add(string kind, string title, string target, string[] aliases, string[] tags)`
- `void RecordOpen(Guid id)`

### 加载
1. `Stopwatch` 开始 → 读 `links.json`（不存在则 `empty`）。
2. `JsonSerializer.Deserialize<LibraryData>`；任何异常/`items` 为 null → 尝试 `links.json.bak`。
3. `.bak` 也失败 → 空库，**不覆盖坏文件**，日志 `load failed: ...`。
4. 日志固定格式：`store loaded: N items in X ms (source=main|bak|empty)`。

### 原子写（在 `_gate` 锁内串行，单写者）
1. `Directory.CreateDirectory(dir)`。
2. 序列化到 `links.json.tmp`（UTF-8，缩进）。
3. 目标存在：`File.Replace(tmp, main, bak, ignoreMetadataErrors: true)`；
   目标不存在：`File.Move(tmp, main)`。
4. 成功后才更新内存 `UpdatedAt`，并触发 `Changed`。
5. 失败：删除 tmp、保留旧主文件、`HostLog.Error`，内存不回滚（下次再写）。
6. `File.Replace` 需要同卷：tmp/bak 均与主文件同目录，满足。

### 去重
`Key(kind, target)`：
- `url`：`new Uri(target)`；scheme/host 小写；去默认端口；去 fragment；`AbsolutePath == "/"` 归空；query 原样保留。非法 URL → `InvalidTarget`。
- `file` / `folder`：`Path.GetFullPath(target).TrimEnd('\\','/')`，比较时 `OrdinalIgnoreCase`。
- 键含 `kind`，避免同路径 file/folder 冲突。
- 已存在则 `Add` 返回 `{ Status = Duplicate, Existing }`，不修改原条目。

### 校验
- `url`：必须 `http/https`，`Uri.TryCreate` 绝对 URI。
- `file`：`File.Exists`。
- `folder`：`Directory.Exists`。
- `title` 为空时自动生成：URL 用 `Host`（根路径时）否则 `Host + AbsolutePath`；文件用 `Path.GetFileNameWithoutExtension`；文件夹用目录名。

### 打开计数
`RecordOpen`：`OpenCount++`、`LastOpenedAt = now`、`UpdatedAt = now`，异步/同步落盘一次（与 Add 同锁）。

## 4. 检索（`Services/SearchEngine.cs`）

- 输入：`IReadOnlyList<LinkItem>`、`string query`。
- 预处理索引 `IndexedItem`：小写 `Title/Aliases/Tags/Domain/FileName/Target`、`Words`（按非字母数字切分）。
- 查询：按空白切词；支持前缀 `tag:xxx`（仅匹配 tags）。
- 单词匹配分（取字段内最高）：
  - 完全相等 1000
  - 前缀 700
  - 词首（单词边界）550
  - 子串 400
  - 子序列 200
  - 否则 0
- 字段权重：title 1.00、alias 0.90、filename 0.85、domain 0.80、tags 0.70、target 0.60。
- AND 语义：每个词必须 `> 0`；总分求和。
- 排序：总分降序 → frecency 降序 → `UpdatedAt` 降序 → 标题升序。
- frecency 简化：`LastOpenedAt` 有值按 `0.5 * OpenCount + recencyBonus`；无值 `0.1 * OpenCount`。
- 空查询：按 `LastOpenedAt` 降序（null 最后）→ `OpenCount` 降序 → `UpdatedAt` 降序；不再固定截断。
- 有查询时按分数排序，不再固定 50 条截断。列表按 **100 条/页** 增量呈现：`HasMoreItems` + `LoadMore()`（IDL L325/L329）。
- 日志固定格式：`search q='<query>' items=N results=K took=X.XX ms top='<title>'`。

## 5. UI / 命令

### 5.1 `Pages/ItemsPage.cs`（`DynamicListPage`）
- `Title="链接库"`、`Name="打开"`、`PlaceholderText="搜索标题、别名、域名、文件名…"`。
- 索引懒构建（首次 `GetItems` 才建，见 §5.6）；订阅 `store.Changed` → 置空索引 + `RaiseItemsChanged()`。
- `UpdateSearchText(old,new)`：仅保存 `_query` 并 `RaiseItemsChanged()`；**不写回 `SearchText`**（避免自激）。
- `GetItems()`：`Stopwatch` → `SearchEngine.Search` → 逐条计算缺失 → 记录日志 → 构造 `ListItem(new OpenItemCommand(store, item))`。
  - `Title=item.Title`；`Subtitle`: 默认显示 target（设置「显示完整路径」关闭时只显示类型名）；缺失时改为 `⚠ 路径不存在 · <target>`；tags 转 `Tag`（缺失时追加 `缺失` 标签）。
  - `Icon`: url 用 `\uE71B`，file 用 `\uE8A5`，folder 用 `\uE8B7`。
  - `MoreCommands`：见 §5.8；缺失判定见 §5.9。
  - 订阅 `settings.Settings.SettingsChanged` → `RaiseItemsChanged()`（开关即时生效）。
- `EmptyContent`：`CommandItem(new AddItemPage(store)) { Title="添加第一个条目", Subtitle="URL / 文件 / 文件夹", Icon=Icons.Add }`。
- 页内添加能力：`EmptyContent` 主按钮 + 每行 `MoreCommands` 首项「添加条目」，两者都打开 `AddItemPage`；`store.Changed` 负责添加后的列表与顶层计数刷新。

### 5.2 `Pages/AddItemPage.cs` + `Pages/AddItemForm.cs`
- `ContentPage`，`Title="添加条目"`，`Icon=\uE710`。
- `FormContent.TemplateJson`（Adaptive Card 1.5）字段：
  1. `kind`：`Input.ChoiceSet`（`url`/`file`/`folder`），默认 `url`，label「类型」。
  2. `target`：`Input.Text`，必填，label「网址或路径」。
  3. `title`：`Input.Text`，选填，label「标题」。
  4. `aliases`：`Input.Text`，选填，逗号分隔，label「别名」。
  5. `tags`：`Input.Text`，选填，逗号分隔，label「标签」。
  6. `Action.Submit` title「保存」。
- `SubmitForm(payload)`：解析 JSON；调 `store.Add(...)`；
  - `Added` → `CommandResult.ShowToast("已添加：<title>")`（Result=`Dismiss`）。
  - `Duplicate` → Toast「已存在：<title>」，`KeepOpen`。
  - `Invalid` → Toast 具体原因，`KeepOpen`。
- 公开事件 `event Action? Added`，Provider 订阅后刷新列表与顶层副标题。

### 5.3 已移除：`Commands/SaveClipboardCommand.cs`（2026-11-05）
- 用户决定扩展不提供剪贴板功能，该命令已删除；`Icons.Clipboard` 与 `ClipboardHelper` 依赖一并清除。
- 顶层命令由 3 条降为 2 条；原 F1/F2/F4 中依赖剪贴板保存的步骤不再适用。

### 5.4 `Commands/OpenItemCommand.cs`
- `InvokableCommand`，`Name="打开"`。
- `store.RecordOpen(item.Id)` → `ShellHelpers.OpenInShell(item.Target)`。
- 成功 `Dismiss`；失败 Toast「无法打开…」`KeepOpen`。
- 日志保留 M0 口径 `Open requested/Open result`。

### 5.5 `CmdPalLinksCommandsProvider`
- 构造：仅创建对象，不做磁盘 IO。
- 顶层 **1** 项：`CommandItem(_itemsPage) { Title="链接库", Subtitle=$"共 N 条" }`（「添加条目」不再作为顶层命令，改由 §5.1 页内入口提供）。
- `TopLevelCommands()` 首次调用时 `_ = LoadInBackground()`：`await _store.StartLoading()` 完成后刷新副标题与列表，避免 JSON 解析阻塞宿主加载命令。
- `Changed` → 更新 Subtitle、`_itemsPage.Refresh()`、`RaiseItemsChanged()`。

### 5.6 启动优化（M1.1）
- 索引延迟构建：`ItemsPage` 不再在构造函数里 `BuildIndex`；首次 `GetItems()` 时构建并记录 `index built: N items in X ms`，数据变更只置空缓存。
- 加载后台化：`LibraryStore.StartLoading()` 用 `Task.Run(Load)`，`Items`/`Add`/`RecordOpen` 访问前 `WaitForLoad()` 保证数据就绪。

### 5.7 删除
- 删除 `Pages/LinksPage.cs`（硬编码测试页，已不再引用）。
- 删除 `Commands/SaveClipboardCommand.cs`（剪贴板功能移除）。

### 5.8 条目动作（M2，2026-11-05 仅代码）
`ItemsPage.BuildCommands(item)` 为每条 `ListItem` 装配 `MoreCommands`，顺序固定：

| 顺序 | 命令类 | 显示名 | 适用 | 行为 |
|---|---|---|---|---|
| 0 | `CommandContextItem(new AddItemPage(store))` | 添加条目 | 全部 | 打开添加表单（顶层已无「添加条目」命令） |
| 1 | `RevealInFolderCommand` | 打开所在文件夹 | 仅 `file` / `folder` | folder → `OpenInShell(target)` 打开目录本身；file → `OpenInShell("explorer.exe", /select,"<target>")`；路径不存在 → Toast「路径不存在」`KeepOpen` |
| 2 | `CopyTargetCommand` | 复制目标 | 全部 | `ClipboardHelper.SetText(item.Target)` → Toast「已复制：<title>」`Dismiss` |
| 3 | （`EditItemPage` 包装） | 编辑 | 全部 | `new CommandContextItem(new EditItemPage(store, item))`，进入表单页 |
| 4 | `DeleteItemCommand` | 删除 | 全部 | `store.Remove(id)` → Toast「已删除」`Dismiss`；失败 Toast「条目已不存在」`KeepOpen`；`CommandContextItem.IsCritical = true`（仅样式） |

- 日志：`Reveal requested/result`、`Copy target: <target>`、`Delete requested: id=… result=true|false`。
- 图标：`Icons.FolderOpen(\uE838)`、`Icons.Copy(\uE8C8)`、`Icons.Edit(\uE70F)`、`Icons.Delete(\uE74D)`。

### 5.9 缺失文件标记（M2.3）
- 判定：`Services/IconCache.IsMissing(item)`：`file` → `!File.Exists(Target)`；`folder` → `!Directory.Exists(Target)`；`url` 恒 `false`。本地路径结果缓存，30s 节流复查；UNC `\\` 不同步判定（后台核实）。
- 时机：每次 `GetItems()` 对**当前已呈现的结果**（首屏 100 条）逐条判定，不做后台轮询；结果不落盘。
- 呈现：副标题 `⚠ 路径不存在 · <target>` + 追加 `缺失` 标签；条目照常可打开/编辑/删除，**不自动删除**。
- 已知风险：断开的 UNC 路径不做同步探测；盘符形式的离线网络盘（如 `Z:\`）仍可能短暂阻塞（`File.Exists` 不可取消），待实机验证（§6.6 F12）。

### 5.10 设置页（M2.3 收尾，2026-11-05 仅代码）
- `Services/SettingsManager.cs : JsonSettingsManager`，`FilePath = Utilities.BaseSettingsPath("CmdPalLinks")/settings.json`。
- 设置项（全部新增项都落盘、切换后立即刷新列表）：

| key | 类型 | 默认值 | 取值 | 说明 |
|---|---|---|---|---|
| `layout` | `ChoiceSetSetting` | `list` | `list` / `grid-medium` | 列表 / 网格（中号卡片）；未知值回落 `list`（`IgnoreUnknownValue`） |
| `showFullPath` | `ToggleSetting` | `true` | bool | 关：副标题只显示类型名 |
| `showSubtitle` | `ToggleSetting` | `true` | bool | 关：列表不显示副标题（缺失警告仍显示） |
| `showTags` | `ToggleSetting` | `true` | bool | 关：不显示条目自带标签（缺失标签仍显示） |

- **不提供的设置项**：每行列数、卡片/图标尺寸。依据：`IGridProperties` 在 IDL 中无任何属性；`IMediumGridLayout` 只有 `ShowTitle`（见 `design.md` §10.1/§13.1）。
- 变更即 `SaveSettings()`；`Load()` 期间用 `_loading` 抑制回写，避免加载过程中的 `SettingsChanged` 覆盖文件。
- `Provider.Settings = _settings.Settings`；`LoadInBackground` 先 `_settings.Load()`（失败只记 `settings load failed`，不阻断存储加载）。

### 5.11 存储扩展（M2）
- `LibraryStore.Update(id, kind, title, target, aliases, tags)`：走 `TryNormalize` 校验（**改类型同样校验**）→ `SameKey` 去重（排除自身）→ 更新 `UpdatedAt` → `SaveLocked()`；返回 `Updated / Duplicate / InvalidTarget / SaveFailed / NotFound`。
- `LibraryStore.Remove(id)`：存在则从内存移除并保存；返回 `bool`（不存在返回 `false`）；写盘失败只记日志，内存不回滚（重启可能复活，§6.6 D6）。
- `Pages/ItemFormTemplate.Build(kind, target, title, aliases, tags)`：添加/编辑共用同一 Adaptive Card 模板，编辑时用 `Input.value` 预填；`EditItemForm.SubmitForm` 调 `Update`。

### 5.12 列表图标（M2.3：T27 修复，2026-11-05 仅代码）
- 文件：`Services/IconCache.cs`（内存缓存 + favicon 磁盘缓存）+ `Services/ShellIconExtractor.cs`（Shell 图标提取）。
- URL：`Get()` 只查本地文件（无网络）；未命中返回通用图标并后台 `GET https://<host>/favicon.ico`（失败再试 `http`），≤512KB、魔数校验后落盘 `LocalState/icons/`；30 天过期后台刷新、90 天清理。
- file/folder：**不再使用 `ThumbnailHelper`**（旧构建实测返回 null；SDK 包未携带其依赖 `System.Drawing.Common`）。改走 `ShellIconExtractor`：
  1. `SHGetFileInfo(SHGFI_SYSICONINDEX)` + `SHGetImageList(SHIL_JUMBO)` + `ImageList_GetIcon`（优先 256px）；
  2. 失败 → `SHGetFileInfo(SHGFI_ICON | SHGFI_LARGEICON)`；
  3. 失败 → `SHGetFileInfo(SHGFI_ICON | SHGFI_SMALLICON)`；
  4. 全部失败 → `Icons.ForKind` 通用字形。
- `HICON → PNG`：`GetIconInfo` → `GetDIBits`（32bpp 顶向下）→ alpha 全 0 时用 AND 掩码合成 → 手写 PNG 编码（zlib/deflate）；结束后 `DeleteObject` 位图 + `DestroyIcon`（无句柄泄漏）。
- 缓存与失效：`ConcurrentDictionary` 内存缓存；命中时按 30s 节流后台比较 `mtime ^ length`（目录取 mtime），变化则移除并触发 `Changed`；缺失路径移除缓存并记 `shell icon invalidated`。
- 不阻塞 UI：`Get()` 同步路径对本地路径做 `File.Exists/Directory.Exists`（结果缓存，30s 复查），UNC `\\` 路径**不**同步探测，直接返回通用图标并在后台核实；提取在后台线程、3 并发、失败 5 分钟后才重试。
- 日志：`shell icon loaded/unavailable/failed/invalidated`、`favicon fetched/unavailable/rejected/failed`、`icon cache pruned`。
- manifest 保留 `internetClient`（普通能力）。

### 5.13 布局（M2.3，2026-11-05 仅代码）
- `ItemsPage.ApplyLayout()`：`layout == grid-medium` → `GridProperties = new MediumGridLayout { ShowTitle = true }`；否则 `null`（列表）。
- 生效时机：构造、`Refresh()`、`Settings.SettingsChanged`（同时 `RaiseItemsChanged()`）。
- 网格只提供 Medium 一种：Small 无标题（无法辨认）、Gallery 图标被裁切且尺寸不一（旧构建实测），宿主也不允许自定义卡片。
- 网格滚动/行高：**由宿主实现**，扩展无法控制行高或滚动策略；每页条数通过 `HasMoreItems` + `LoadMore()`（每页 100）控制。若网格滚轮无效，最可能原因是焦点未落在列表容器（宿主按焦点路由滚轮/键盘），代码侧无法规避；保守默认仍是列表。


## 6. 验证（每块完成后实机执行）

### 6.1 构建/安装（复刻 EverythingCmdPal 的「关→卸→装→启」，2026-11-06 起）
1. 递增 `Package.appxmanifest` 的 `<Identity Version>`（同版本不同内容装不上：`0x80073CFB`）；`dotnet build src\CmdPalLinks\CmdPalLinks.csproj -c Debug` → 0 warning / 0 error。
2. 拷贝 `bin\Debug\net10.0-windows10.0.26100.0\win-x64\*` 到 `package\layout`；manifest 拷为 `AppxManifest.xml`。
3. `makeappx pack` → `signtool sign`。
4. 快照 `LocalState` → 停 `Microsoft.CmdPal.UI` 与 `CmdPalLinks.exe` → 等 2 秒 → `Remove-AppxPackage` → `Add-AppxPackage` → 恢复快照；**不用 `-ForceUpdateFromAnyVersion`、不在面板运行中安装**；装完重开 CmdPal（依据 `../../../plugins/EverythingCmdPal/tools/update.ps1` L75-L106，详见 `../../../docs/install.md`）。
5. 校验：数据 8 文件 SHA256 一致、`items=7`、宿主 `ProviderSettings`/`commandProviderCache` 各 1 条；UIA 首页「链接库」1 条。
6. 日志确认：`Loaded N command(s) ... from CmdPalLinks ... in X ms` 与 `store loaded ...`。

### 6.2 功能（CmdPal 内真实操作，逐条记录日志/文件证据）
| # | 操作 | 通过标准 |
|---|---|---|
| F1 | （已移除）剪贴板保存 | 不再适用，功能随剪贴板命令一并删除 |
| F2 | （已移除）重复保存去重 | 改由 F3 表单重复提交覆盖（去重逻辑保留在 `LibraryStore.Add`） |
| F3 | 进入「链接库」→ MoreCommands/空态「添加条目」填 url + 标题 + 别名 → 保存 | 列表出现；重启 CmdPal 后仍在 |
| F4 | 添加一个存在的文件（`.html`）与一个文件夹 | 通过表单选择类型添加；两类条目可被检索并打开 |
| F5 | 搜索 `gh`/别名/域名/文件名片段 | 日志 `results>0` 且 top 命中预期；实测打开成功 |
| F6 | 空查询进入列表 | 显示最近使用（按 `lastOpenedAt`），非空白页 |

### 6.3 破坏性
| # | 操作 | 通过标准 |
|---|---|---|
| D1 | 主文件写半截 JSON，`.bak` 为合法数据，重启 CmdPal | 日志 `source=bak`；列表恢复；不覆盖坏文件直到下次保存 |
| D2 | 主/备都损坏 | 空库可用、可新增；日志 `source=empty`；旧坏文件保留 |
| D3 | 并发写（测试 harness 20 线程 × 20 次 Add） | 最终 JSON 可反序列化、条目数 = 预期、无重复 |
| D4 | 1000 / 5000 条数据下检索 | 日志记录 `items=N results=K took=X ms`；如实报告，不做美化 |

### 6.4 计时口径
- 启动：宿主日志 `Loaded 2 command(s) ... in X ms`（M0 基线 `Loaded 1 command ... 28 ms`）+ `store loaded: N items in X ms`；同一数据、同一机器，至少 3 次冷启动取范围。
- 检索：`GetItems` 内 `Stopwatch` 包裹 `SearchEngine.Search`；索引构建单独记录 `index built`。
- 改前/改后：M1（3 命令 + 同步加载 + 构造时建索引）vs M1.1（2 命令 + 后台加载 + 懒索引）。如实报告提升与剩余差距。

### 6.5 测试 harness（仅本地，不进包）
- `tests/StoreHarness/StoreHarness.csproj`：`Compile Include` 复用 `Models/LinkItem.cs`、`Services/LibraryStore.cs`（不复制代码）。
- 用法：`StoreHarness <dir> concurrency` / `corrupt` / `bench <N>`。
- 产物不入 `package\layout`，不影响安装包。

### 6.6 M2 待实机验证清单（2026-11-05 未执行）
> 本轮按用户要求只做代码与 `dotnet build`，下列项目**全部未实机验证**，后续一次性补验。

| # | 操作 | 通过标准 |
|---|---|---|
| F7 | file 条目「打开所在文件夹」 | Explorer 打开并选中该文件；日志 `Reveal requested ... result=True` |
| F8 | folder 条目「打开所在文件夹」 | 打开该目录本身（非父目录）；打开失败有 Toast |
| F9 | 「复制目标」后粘贴 | 剪贴板内容与 `target` 完全一致；Toast「已复制」 |
| F10 | 「编辑」改标题/别名/标签 | 列表即时刷新；`links.json` 字段更新且 `updatedAt` 变化 |
| F11 | 编辑时把 `kind` 改成 file/folder 并填非法或不存在的路径 | Toast 提示 `InvalidTarget` 原文，`KeepOpen`，原条目未被修改 |
| F12 | 把已存在条目指向另一条目的 target（同类型） | Toast「已存在：<title>」，原条目未改 |
| F13 | 删除最后一个条目 | Toast「已删除」，列表进入空态（`链接库为空`），`links.json` items 为空数组 |
| F14 | 缺失文件标记：把已添加文件的源文件改名/删除后重开面板 | 该行副标题 `⚠ 路径不存在 · …` + `缺失` 标签；条目仍在；`打开` 失败有 Toast |
| F15 | 设置页「显示完整路径」关→开 | 关后副标题只显示类型名；开关状态写入 `settings.json` 并在重启后保留 |
| F16 | 条目 `MoreCommands` 数量与顺序 | URL 条目 3 项（复制/编辑/删除）；file/folder 4 项，且第 1 项为「打开所在文件夹」 |
| D5 | 删除时分页/搜索进行中（边检索边删） | 无崩溃；刷新后结果一致 |
| D6 | 模拟删除写盘失败（只读/锁文件） | Toast 仍报已删除但重启后条目复活——**当前已知行为**，需记录并确认是否可接受 |
| D7 | 缺失判定对断开的 UNC 路径 `\\server\share\...` | 观察 `GetItems` 是否阻塞（记录实际耗时）；不崩溃 |
| F17 | URL 条目图标：首次打开面板 → 后台下载 → 二次打开命中缓存 | 日志先 `favicon fetched` 后 `favicon unavailable` 不再出现；`LocalState/icons/` 出现对应文件；列表该行显示站点图标 |
| F18 | 离线/拦截场景（断网或该站 404） | 行图标回落通用图标，不阻塞、不崩溃；日志 `favicon unavailable ... fallback=generic` |
| F19 | file/folder 条目图标 | 显示真实 Shell 图标（非通用文档图标）；日志 `shell icon loaded: ...`；缺失路径走通用图标 |
| F20 | 图标补齐导致列表刷新 | 不出现明显闪烁/抖动；`Loaded N command(s)` 与 `store loaded` 不因图标变慢（同口径记录） |
| D8 | 安装包含 `internetClient` 后重新装包 | 安装/启动正常（能力声明合法）；若安装失败需回退该能力 |

### 6.7 M2.2 实机结果（2026-11-05，**旧构建历史记录**）
> 以下数据来自停令前的旧构建；其后代码有改动（SHGetFileInfo 图标、设置项、图标资产），**最终构建未复测**，仅作参考。

**首页简化（已验证）**
- 日志 4 次启动均为 `Loaded 1 command(s) and 0 band(s) from CmdPalLinks ... in 30/33/37/40 ms`。
- 搜索结果只剩「链接库 共 5 条」一个插件条目（截图 `search2.png`），确认顶层已降为 1 条。
- 页内「添加条目」出现在每行命令栏（网格截图中可见），空态主按钮存在。

**图标（F17/F18 通过，F19 失败）**
| 项 | 结果 | 证据 |
|---|---|---|
| F17 favicon 缓存 | 通过 | `favicon fetched: host=github.com ext=.ico bytes=6518 took=741 ms`；`docs.python.org ... 15086 bytes 771 ms`；`LocalState/icons/` 落盘 2 个 `.ico`；截图可见 GitHub/Python 图标 |
| F18 无 favicon 回落 | 通过 | `favicon unavailable: host=example.com fallback=generic`，列表显示通用链接图标 |
| F19 Shell 图标 | **旧构建失败** | `shell icon unavailable: ...test-open-me.html fallback=generic`（`ThumbnailHelper.GetThumbnail` 返回 null）；现已改为 `ShellIconExtractor`，待自测 |

**网格（临时开关 `LocalState\grid-mode.txt`，已删除）**
| 布局 | UIA 实测尺寸 | 结论 |
|---|---|---|
| SmallGridLayout | 44×44，行内间距 56 | 宿主渲染成功；**只有图标，无标题/副标题**，无法辨认名称 → 不适合本插件 |
| MediumGridLayout(ShowTitle) | 100×100，间距 112，每行 7 个 | 图标与标题渲染正常、可读；5 条时单行居中 |
| GalleryGridLayout(ShowTitle+Subtitle) | 168×214 ~ 213×230 不等 | **不可用**：图标被大幅放大并裁切、卡片尺寸不一致、副标题被截断（`C:\Users\2504…`） |

**30 条数据下的网格（MediumGridLayout）**
- `index built: 30 items in 3.10 ms`；首屏 `search q='' items=30 results=30 missing=0 took=65.08 ms`（冷启动首次调用）。
- 5 行排布（Y=534/646/758/870/∞），**第 4 行明显被视口裁切**（网格视口 `VerticalViewSize≈63.87%`）。
- 旧构建：滚轮 6 格、`PageDown` 未见位移；10 次方向键后面板被关闭。**未取得结论**。
- 帧时间：**未测出**（宿主未暴露帧计时，未插桩）。

**结论**：网格可用但存在裁切/滚动疑问；Small 无标题、Gallery 视觉破损。代码见 §5.13：默认列表，Medium 作为**用户可选设置**（`layout=grid-medium`）。

### 6.8 M2.3 收尾状态（2026-11-05，仅代码 + 构建）
- 代码：T27 已改用 `ShellIconExtractor`；设置项 4 个；网格改由设置驱动；应用图标资产由 `tools/gen-icons.py` 生成。
- 构建：Debug / Release 均 **0 警告 / 0 错误**；MSIX 布局内的 `Assets` 已静态确认 60 个 PNG。
- **未做实机验收**：以下全部交由用户自测（见 `self-test.md`）：图标各类型、网格滚动/翻页、100/500 条性能、设置切换与持久化、装包与外观。


## 7. 风险与假设
1. Adaptive Card 的 `ChoiceSet` 与键盘自动填充可能受宿主焦点影响；若无法稳定自动化，记录为“表单需人工点击验证”，不伪造结果。
2. `File.Replace` 在极少数文件锁场景会失败；失败时保留旧文件并记日志，不丢库。
3. 打包进程写 `LocalState` 需 `runFullTrust`（M0 已具备）。
4. `.bak` 只在主文件被 `File.Replace` 时生成；首次写入无 `.bak` 属预期。
5. 5000 条以内按内存线性扫描；超过后（P1）再考虑倒排索引。

## 8. 验收定义
- M1：构建 0/0；安装成功；F1-F6 全部有日志或文件证据；D1-D4 有可复现输出；耗时表写入 `design.md` 第 8 节。
- M2（本轮）：仅要求 `dotnet build` 0 警告 / 0 错误 + 代码层静态自查；F7-F16、D5-D7 全部列入 §6.6 待实机验证清单，未取得任何实测数据（不伪造）。
- 任何步骤若必须用户改系统设置（开发者模式/证书/重启宿主）→ 停止并说明，不擅自执行。

## 9. 待用户确认的设计点（M2）
1. **删除是否需要二次确认**：当前直接删除，`IsCritical=true` 只是视觉强调；若要确认框，需新增确认页或宿主确认 API（未验证可用性）。
2. **编辑是否复用同一表单页**：当前添加/编辑共用 `ItemFormTemplate`，靠 `Input.value` 预填；若宿主不渲染预填值，则改为「只编辑非路径字段」或独立模板。
3. **缺失判定时机与呈现**：当前每次列表刷新对可见行做 `File.Exists`/`Directory.Exists`，不落盘、不自动删；是否需要「一键清理缺失」或启动时统一扫描待定。
4. **folder 的「打开所在文件夹」语义**：当前打开目录本身；若期望打开父目录并选中该目录，需改命令实现。
5. **设置页范围（已定）**：`layout` + `showFullPath` + `showSubtitle` + `showTags`；宿主不支持每行列数/卡片尺寸，故不再提供（依据见 §5.10）。
6. **删除写盘失败的处理**：当前内存已删、磁盘未删（重启复活）；是否改为失败即回滚并提示「删除失败」。
7. **过期缓存的降级策略**：需求写的是「离线回退通用图标」，当前实现为「有旧缓存就继续用旧图标（过期仅触发后台刷新），完全无缓存才用通用图标」；若必须严格按需求，需要改成过期即弃用。

## 10. M3 方案（2026-11-05，仅方案，未改代码/未实机）

> 完整方案（摩擦点清单、IDL 逐条依据、CSV 解析规则、假设与验证方法、工作量）见 `ux-icon-sort-plan.md`。本节只固定决策级结论，避免两份文档漂移。

### 10.1 添加体验（M3.1）
- 顶层命令 1 → 3：`链接库` / `添加链接或文件` / `批量导入`（依据 IDL L398 `TopLevelCommands()` 返回数组）；可用设置项关闭快捷入口。
- `AddItemPage`：剪贴板（Win32 `CF_UNICODETEXT` / `CF_HDROP`）预填 + 自动判定 kind；表单 5 字段收敛为「target 必填 + 3 个选填折叠」。
- 类型选择：**不做分段控件**（Adaptive Card 1.5 无该控件），默认隐藏，判定失败才显示 `ChoiceSet style=expanded`。
- 文件/文件夹：新增参数页命令，走 `IParametersPage` + `FilePickerParameterRun`（IDL L445-459；`FilePickerParameterRun` L70-82；官方示例 `ParameterSamples.cs` L53-98）。文件夹：`FolderPicker` 或自建 `SelectFolderPage`（待验证）。
- 校验：**无字段级即时校验**（IDL L437-443 TODO）；只做进页预检（TextBlock 快照）+ 提交时具体错误（`AddStatus` 原文）+ KeepOpen。
- 反馈：成功 `KeepOpen`（清空 target、保留类型/tags），不再 `Dismiss`；`lastAddedId` 支持撤销；`SaveFailed` 不触发列表刷新（修正 AddItemForm L56-57）。
- 目标步数：9–13 → 3–4。

### 10.2 图标系统（M3.2）
- 模型：`LinkItem` 增 `IconKind`（`auto` / `block` / `transparent` / `emoji`）、`IconEmoji`、`IconColor`（`#RRGGBB`）；`schemaVersion` 1 → 2。
- 渲染：64×64 PNG，圆角 `0.22*size`，内边距 8%，emoji 字号 72%；Light/Dark 双份（IDL L28-31）。
- 彩色 emoji：**GDI+/WPF 不可行；DWrite+D2D（`D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT` → WIC 位图）可行**，建议引入 `Vortice.Windows`；COLRv1 支持待验证（A4）；失败回退单色掩膜着色。
- 缓存：`SHA1(iconKind|emoji|color|theme|size)` → `LocalState/icons/icon-{hash}.png`，复用 `IconCache` 的 30s 校验/90 天清理。
- 选择：emoji 用 `Input.Text`；颜色用 `Input.Text` + 预设色值文本（色板 chips 不可用）；预览用独立「图标预览」页 3 行 ListItem（表单内嵌图片不可靠）。

### 10.3 排序（M3.3）
- **部分可控**：`ICommandItem`（L268-275）无权重/pin/importance；`IFallbackCommandItem`（L378-387）无 rank；`IListItem.Section`（L291）只能分组；`GetDockBands()`（IDL L461-465）提供 Dock 常驻。
- 可做：页内数组顺序（`SearchEngine` 加分/置顶）、Section 分组、Dock band、减少顶层命令。
- **不可做**：CmdPal 首页/全局搜索固定第二。结论写入方案 §3.2。

### 10.4 批量导入/导出（M3.4）
- CSV 表头严格：`type,target,title,alias,tags,emoji,color`；`alias/tags` 用 `|`；`color` 空=自动、`none`=透明、`#RRGGBB`=指定色（定稿见 §10.6）；`emoji` UTF-8 原样。
- 解析容错：BOM 剥离、CRLF/CR/LF、RFC4180 引号/`""`、字段内逗号需引号、中文路径不转义、全字段 trim；非 UTF-8 直接拒绝（不静默 GBK）。
- 触发：`IParametersPage` + `FilePickerParameterRun`（原生对话框）；**拖拽不支持**（IDL 无 drop API；`CommandItem.DataPackage` 是输出）；兜底为多行文本粘贴。
- 流程：解析（后台，5000 行 <300ms，上限 10000 行/5MB，15s 超时）→ 预览（可导入/重复/非法 + 行号原因）→ 确认 → **单次** `SaveLocked`（新增 `ImportBatch`，禁止逐条 `Add`）。
- 重复：`SameKey(kind, normalized target)`；默认跳过，可选覆盖（title/alias/tags/图标更新，alias/tags 合并，openCount/lastOpenedAt/createdAt/id 保留）。
- 非法行：逐行跳过，不整份拒绝；表头不符才整份拒绝；0 条可导入不写盘；file/folder 不存在也允许导入（不额外标记缺失，与 `DedupeKey` 一致）。
- 安全：导入前快照 `links.json.import-bak`（单份，覆盖上一次）、失败回滚内存并保留快照、Toast 条数对比、「撤销上次导入」。
- 导出：CSV（UTF-8 BOM、CRLF、同表头）+ JSON 备份；`transparent` 在 CSV 用 `none` 表达（已定稿，见 §10.6）。
- 其他格式：`.json` 本插件格式、`.txt` 一行一条（`#` 注释）、`.md` 仅抽 URL（明确不解析结构）。

### 10.5 M3 待拍板
见方案文档 §5（8 项），其中影响架构的四项：分段控件/字段校验的降级接受度、彩色 emoji 依赖与预算、覆盖策略默认值、`schemaVersion=2` 迁移。

### 10.6 M3.4 落地实现（2026-11-05，仅代码 + 构建，未实机）

用户反馈「找不到导入入口，导入应放在设置里」，本轮按推荐默认值落地（用户已授权不再逐条等待拍板）：

| 决策点 | 落地值 |
|---|---|
| 分段控件 / 字段级即时校验 | 接受降级，不做 |
| 重复默认策略 | **跳过**；导入页提供「重复处理」切换为**覆盖**（title 替换、alias/tags 合并、保留 Id/OpenCount/时间） |
| `schemaVersion` | 1 → 2；加载 v1 时补齐 `IconKind=auto`、`IconColor`/`IconEmoji` 空串，不丢字段 |
| `color` 语义 | 空 = 自动；`none` = 透明；`#RRGGBB` = 指定色块 |
| 彩色 emoji | **暂缓**（不引入 Vortice/D2D）；先按单色字形掩膜渲进色块 |
| 排序 | 不做承诺；仅用 Section / 页内顺序优化观感 |

**入口位置（回答「设置页里能否看到」）**
- 设置页 `链接库设置`（`Pages/LibrarySettingsPage.cs`，自实现 `ICommandSettings` → `ContentPage` 包住原 `Settings.ToContent()`）的**页面命令区**新增 4 条：`导入 CSV`、`导出全部为 JSON`、`导出全部为 CSV`、`撤销上次导入`。
- 位置说明：Toolkit 的 `Settings.ToContent()` 只产出表单内容，无法把命令插进表单内部；命令挂在 `ContentPage.Commands`，由宿主渲染**页面级命令**，因此「在现有设置项之后」只能保证顺序与同页可见，**具体排布由宿主决定**（不保证出现在表单最后一行的正下方）。
- 顶层别名：`CmdPalLinksCommandsProvider` 保留 `导入` 命令（Subtitle 提示「也可在设置页导入」），理由是设置页常驻、搜索可直接命中；两条入口指向同一个 `ImportPage` 实例，状态一致。

**实现映射（文件 → 作用）**
- `Services/LibraryStore.cs`：`DedupeKey`（URL 小写 scheme/host、去默认端口、去末尾 `/`；路径绝对化 + 去尾分隔符 + 小写，不要求存在）、`ImportBatch`（单锁 + 单次原子写、写前快照、失败 `Load()` 回滚内存并保留快照）、`UndoLastImport`、`ExportJson`/`ExportCsv`、`WriteAtomic`（`.tmp` → `File.Replace`/`File.Move`）。
- `Services/CsvCodec.cs`：严格表头 `type,target,title,alias,tags,emoji,color`；UTF-8 严格解码（含 BOM 剥离，非法字节直接报「不会按 GBK 猜测」）；RFC 4180 手写状态机（引号、`""`、字段内逗号/换行、CRLF/CR/LF）；`alias/tags` 以 `|` 分隔（无 `|` 时兼容中文逗号）；emoji 只取首个文本元素；行号级非法原因。
- `Pages/ImportPage.cs`：`ParametersPage` + `FilePickerParameterRun`（宿主注入 HWND 后 `InitializeWithWindow`）；两段式状态机「解析预览 → 确认导入」；预览显示 可导入/重复/非法/警告 计数与前 20 条非法行号；确认后单次写盘；成功清空文件选择。
- `Services/CustomIconRenderer.cs` + `Services/IconCache.cs`：64×64 自绘 PNG（圆角色块/透明底 + 单色字形），Light/Dark 两份，按 `kind|color|emoji|title` 哈希缓存。
- `Commands/ExportCommands.cs`：JSON / CSV 导出与「撤销上次导入」，导出目录 `LocalState/exports/links-{yyyyMMdd-HHmmss}.{json|csv}`，结果用 Toast 回显完整路径。

**已实现的格式与往返**
- JSON 导出含完整条目（含 icon 字段、`schemaVersion=2`、`updatedAt`），可被同一 `ParseFile` 原样回导（回导按 target 归一化判重，不复制 Id/时间）。
- CSV 导出：UTF-8 BOM + CRLF；`transparent` 写 `none`，自定义图标写 `#RRGGBB`，其余留空——与 `color` 语义一一对应，解决了方案 §5 的「transparent 无法表达」。
- TXT 兜底：一行一条、`#` 注释、按 URL/已存在文件/已存在文件夹推断 kind。

**视觉限制（如实记录）**
- emoji 用 GDI 取 Segoe UI Emoji 字形轮廓作**单色掩膜**，再按色块/深色主题着色；**没有原色、渐变与 COLR/CPAL 彩色层**，复杂 ZWJ 序列在 GDI 下可能无单字形，会退回首字（用 Segoe UI）。
- 彩色 emoji 需要 DirectWrite + Direct2D（`D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT`）+ WIC，本轮明确**不做**，后续再评估依赖成本（Vortice.Windows）与 COLRv1 支持。
- 自绘图标为 64×64；宿主缩放后的清晰度待实机验证（原 A9）。

**待实机验证**（沿用 §6 清单，新增/收敛为本轮相关项）
1. 设置页能看到 4 条命令（其中「导入 CSV」「导出全部为 JSON」为验收项）。
2. 点「导入 CSV」→ 选择文件是否弹出系统对话框（HWND 是否被宿主注入）。
3. 解析预览计数与非法行号是否正确显示在参数行（宿主是否折行/截断长文本）。
4. 确认导入后 `links.json` 是否变化、列表是否刷新、Toast 是否回显条数。
5. 「撤销上次导入」是否恢复到导入前（首次导入也应可撤销，快照按内存状态生成）。
6. 用编辑页导出的 CSV（UTF-8 BOM、CRLF）回导是否 0 非法、重复计数符合预期。
7. JSON 导出 → 清空/新库回导是否原样恢复（含 icon 字段）。
8. 自绘图标在列表/网格中的实际观感（单色 emoji 的视觉上限）。
