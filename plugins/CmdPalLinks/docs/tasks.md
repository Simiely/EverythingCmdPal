# CmdPalLinks 任务清单（M1 / M1.1 / M2）

按顺序执行；每项完成后立即实机/构建验证，失败则记录并停下说明。M2 本轮按用户要求**只做代码与构建验证**，实机项统一列在 T20。

- [x] **T1 模型**：新增 `Models/LinkItem.cs`（`LinkKind` 常量、`LinkItem`、`LibraryData`），字段与 spec §2 完全一致。
- [x] **T2 存储**：新增 `Services/LibraryStore.cs`（Load/Add/RecordOpen/Changed；原子写；`.bak` 回退；去重；日志 `store loaded`）。
- [x] **T3 检索**：新增 `Services/SearchEngine.cs`（索引、打分、排序、空查询最近、`results/took/top` 日志）。
- [x] **T4 列表页**：新增 `Pages/ItemsPage.cs`（`DynamicListPage`，刷新订阅，空态指向 AddItemPage），删除 `Pages/LinksPage.cs`。
- [x] **T5 表单**：新增 `Pages/AddItemPage.cs` + `Pages/AddItemForm.cs`（3 类型、5 字段、校验、去重 Toast、`Added` 事件）。
- [x] **T6 命令**：新增 `Commands/OpenItemCommand.cs`（剪贴板命令已于 2026-11-05 按用户要求移除，见 T13）。
- [x] **T7 装配**：改 `CmdPalLinksCommandsProvider.cs` 为 2 条顶层命令（剪贴板命令已移除）+ 计数副标题 + 变更通知。
- [x] **T8 构建+安装**：`dotnet build` 0/0 → 装包（2026-11-06 起改为「递增版本 → 停宿主与扩展 → 卸载（先快照 LocalState）→ `Add-AppxPackage` → 恢复快照」，照抄 `../../../plugins/EverythingCmdPal/tools/update.ps1`）→ 启动 CmdPal → 日志确认扩展与存储加载。
- [x] **T9 功能实机**：F1-F6 全部执行，逐条记录日志、`links.json` 片段、打开结果。
- [x] **T10 破坏性实机**：D1/D2（坏/截断 JSON）、D3（并发 harness）、D4（1000/5000 条计时）。
- [x] **T11 文档**：把 F1-F6/D1-D4 证据与计时表写入 `design.md` §8；如实标注无改善项。
- [x] **T12 回报**：中文简洁 Markdown，先结论后证据；列仍需用户手动的事项（若有）。
- [x] **T13 移除剪贴板**：删除 `SaveClipboardCommand`/`Icons.Clipboard`/`ClipboardHelper` 引用；顶层降为「链接库」「添加条目」两条；同步 spec/设计文档。
- [x] **T14 启动优化**：索引延迟构建 + 存储后台加载；同口径冷启动各 3 次记录；如实报告是否变快。

## M2（代码完成，实机待验）

- [x] **T15 存储扩展**：`LibraryStore.Update/Remove`（`UpdateStatus/UpdateResult`、`TryNormalize` 改类型校验、`SameKey` 排除自身去重、写盘失败仅记日志）。
- [x] **T16 条目动作**：`Commands/RevealInFolderCommand.cs`、`Commands/CopyTargetCommand.cs`、`Commands/DeleteItemCommand.cs`；`Pages/EditItemPage.cs` + `Pages/EditItemForm.cs`；`Pages/ItemFormTemplate.cs`（添加/编辑共用模板，`Input.value` 预填）；`Icons` 增加 Copy/Edit/Delete/FolderOpen；`ItemsPage.BuildCommands` 装配 `MoreCommands`（URL 3 项、file/folder 4 项，删除 `IsCritical`）。
- [x] **T17 缺失标记**：新增 `Services/ItemHealth.cs`；`ItemsPage.GetItems` 逐条判定，副标题 `⚠ 路径不存在 · <target>` + `缺失` 标签；日志加 `missing=N`；不自动删除。
- [x] **T18 设置页**：新增 `Services/SettingsManager.cs`（`showFullPath` 开关，`settings.json`）；Provider 接 `Settings` 与 `SettingsChanged`；`LoadInBackground` 先加载设置且失败不阻断存储。
- [x] **T19 构建验证**：`dotnet build` → 0 警告 / 0 错误（已通过）。
- [ ] **T20 实机验证（未执行）**：spec §6.6 的 F7-F16、D5-D7 全部待补，含装包/启动/UIA 操作；本轮禁止执行。
- [x] **T21 文档同步**：spec.md（§1/§5.1/§5.8-5.11/§6.6/§8/§9）、tasks.md、`design.md` §9 已更新。
- [x] **T22 列表图标（M2.1）**：新增 `Services/IconCache.cs`；URL favicon 本地缓存（30 天 TTL / 90 天清理 / 离线回落）、file/folder 用 `ThumbnailHelper.GetThumbnail` 后台取 Shell 图标；`Get` 同步无网络；`ItemsPage` 接管 `Icon` 与 `Changed`；manifest 加 `internetClient`。
- [x] **T23 构建验证（M2.1）**：`dotnet build` → 0 警告 / 0 错误。
- [x] **T24 图标实机验证（历史）**：F17/F18 在旧构建上通过；F19 在旧构建上失败（`ThumbnailHelper.GetThumbnail` 返回 null）；F20 帧时间未测出。
- [x] **T25 首页简化**：顶层降为 1 条「链接库」，页内新增「添加条目」（空态主按钮 + MoreCommands 首项）；构建 0/0；实机 4 次启动均 `Loaded 1 command(s)`，搜索结果仅 1 个插件条目。
- [x] **T26 网格测试（历史，旧构建）**：Small(44×44 仅图标) / Medium(100×100 有标题) / Gallery(尺寸不一、图标裁切、副标题截断)；30 条时 Medium 第 4 行被视口裁切、滚轮与 PageDown 未见位移；结论：默认列表，网格仅保留 Medium 作为可选设置。
- [x] **T27 Shell 图标（代码完成）**：新增 `Services/ShellIconExtractor.cs`（SHGetFileInfo + SHGetImageList/JUMBO + HICON→PNG，手写 PNG 编码，不依赖 `System.Drawing`）；`IconCache` 改为内存缓存 + 30s 节流校验 + 5min 失败重试 + 路径失效；缺失/UNC 不阻塞 UI。构建 0/0，**待用户自测**。
- [x] **T28 网格可选设置（代码完成）**：`GridProperties` 由设置项 `layout`（`list` / `grid-medium`）驱动，默认 `list`；切换后立即 `RaiseItemsChanged()`；宿主 `IGridProperties` 无属性、Medium 仅有 `ShowTitle`，故**不提供每行列数/图标尺寸设置**。构建 0/0，**待用户自测**。
- [x] **T29 设置项扩展（代码完成）**：`layout`（ChoiceSet，默认列表）、`showFullPath`、`showSubtitle`、`showTags`（默认均 `true`）；`SettingsChanged` 落盘 + 即时刷新；`Load()` 期间抑制回写，未知值回落。
- [x] **T30 应用图标（代码完成）**：`tools/gen-icons.py` 单源生成 StoreLogo/Square44/Square150/Square71/Square89/Square310/Wide310x150/Splash/LockScreen 及 scale/targetsize 变体；构建 0/0；静态校验通过。
- [x] **T31 构建验证（收尾）**：Debug/Release 均 0 警告 / 0 错误；`bin\...\Assets` 60 个 PNG 齐全。
- [x] **T33 列表增量加载（代码完成）**：`SearchEngine.Search` 支持外部上限（不再固定 30/50 截断）；`ItemsPage` 首屏 100 条 + `HasMoreItems` + `LoadMore()` 追加；搜索词变化重置分页。依据 IDL L325/L329。构建 0/0，**待用户自测**。
- [ ] **T32 用户自测**：按 `self-test.md` 执行；重点 T27 各类型图标、网格滚动/翻页、设置切换与重启持久化、100/500 条性能。

## 验证命令速查
```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build src\CmdPalLinks\CmdPalLinks.csproj -c Debug
# 安装流水线：递增 manifest 版本 → 布局 → makeappx → signtool → 快照 LocalState → 停宿主与扩展 → 卸载 → Add-AppxPackage → 恢复快照（不用 ForceUpdateFromAnyVersion）
# 日志：%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\0.100.2.0\Log_YYYY-MM-DD.log
```

## 完成定义
- M1/M1.1：2 个顶层命令可用；添加/去重/持久化/检索/空态全部通过；坏文件与并发有证据；计时表落盘；无用户系统设置改动。
- M2（本轮）：代码与文档完成 + `dotnet build` 0 警告/0 错误；F7-F16、D5-D7 未实机执行，登记在 spec §6.6 与 T20，后续一次性补验；不编造实测数据。

## M3（方案阶段：只写文档，不改代码、不实机）

依据：`ux-icon-sort-plan.md`；决策级摘要见 `spec.md` §10。

- [x] **T34 现状复现**：按「入口→字段→校验→落盘→反馈」列出添加流程 14 个摩擦点，逐条给文件行号（ItemsPage L39-44/L208-217、ItemFormTemplate L17-33/L66-71、AddItemForm L41-47、LibraryStore L160-208/L322-376/L411-437）。
- [x] **T35 添加体验方案**：3 条顶层命令 + 剪贴板预填 + 自动类型判定 + `IParametersPage`/`FilePickerParameterRun` 原生选择器 + 预检/提交错误 + KeepOpen 撤销继续；步数 9–13 → 3–4；明确「分段控件/字段级即时校验不可」。
- [x] **T36 图标方案**：`IconInfo` 能力边界（路径/字形/FromStream/明暗双份/无尺寸）；彩色 emoji 判断（GDI+/WPF 不可、DWrite+D2D 可行、Win2D/XAML 不采用）；`iconKind/iconEmoji/iconColor` 模型 + 渲染/缓存/表单；`schemaVersion` 1→2。
- [x] **T37 排序结论**：IDL 无权重/pin/importance（ICommandItem L268-275、IFallbackCommandItem L378-387）；仅 `IListItem.Section` 与 `GetDockBands()` 可控；结论「部分可控」，全局第二不可承诺。
- [x] **T38 CSV 导入导出方案**：表头 `type,target,title,alias,tags,emoji,color` 严格对齐；容错（BOM/CRLF/RFC4180/中文/emoji UTF-8）；解析→预览→确认→单次原子写；重复/非法/上限/超时/快照撤销/导出 BOM；拖拽不可。
- [x] **T39 用户拍板（按推荐默认执行）**：用户授权按方案推荐值落地——接受分段/逐字校验降级；重复默认跳过 + 导入页可切覆盖；`schemaVersion` 1→2；`color` 空=自动 / `none`=透明 / 十六进制=指定色；彩色 emoji 暂缓；排序不做承诺。
- [ ] **T40 实机验证（未执行）**：方案 §6 的 A1–A10（参数页 HWND、Toast 按钮、模板重绘、D2D COLRv1、多命令排序、StorageFile、FolderPicker、Section 组序、64px 图标清晰度、emoji 代理对）。

### M3.4 导入/导出落地（2026-11-05，仅代码 + 构建，未实机）

- [x] **T41 数据模型与存储**：`LinkItem` 增 `IconKind`（`auto|block|transparent|emoji`）/`IconColor`/`IconEmoji`；`schemaVersion=2` 且 v1 加载补齐字段不丢数据；`DedupeKey`（URL 去末尾 `/`、小写 host/去默认端口；路径绝对化+去尾分隔符+小写、不要求存在）；`ImportBatch` 单锁单写 + 写前快照 + 失败回滚；`UndoLastImport`；`ExportJson`/`ExportCsv` 走 `.tmp` + `File.Replace`/`File.Move`。
- [x] **T42 解析器**：严格表头 `type,target,title,alias,tags,emoji,color`；UTF-8 严格解码（BOM 剥离，非 UTF-8 拒绝且不按 GBK 猜测）；RFC 4180 状态机（引号/`""`/字段内逗号换行/CRLF/CR/LF）；`|` 分隔 alias/tags；行号级非法原因；上限 10000 行 / 5MB；JSON/TXT 兜底。
- [x] **T43 导入页**：`ParametersPage` + `FilePickerParameterRun`（`ConfigureFilePicker` 限定 `.csv/.json/.txt`，宿主注入 HWND）；两段式「解析预览 → 确认导入」；预览计数（可导入/重复/非法/警告）+ 前 20 条非法行号；切换「跳过/覆盖」实时更新计数；确认后单次写盘、清空文件选择。
- [x] **T44 导出与撤销命令**：`导出全部为 JSON`（含 icon 字段/schemaVersion 2/updatedAt，可回导）、`导出全部为 CSV`（UTF-8 BOM + CRLF，`transparent`→`none`）、`撤销上次导入`；导出到 `LocalState/exports/links-{时间戳}.{ext}`，Toast 回显路径。
- [x] **T45 设置页入口 + 顶层别名**：`LibrarySettingsPage`（`ContentPage` 包住 `Settings.ToContent()`）页面命令区挂 4 条命令；`CmdPalLinksCommandsProvider` 保留顶层「导入」别名并注明两种入口的理由（命令位置由宿主决定，不能插入表单内部）。
- [x] **T46 单色 emoji / 色块图标**：`CustomIconRenderer` 用 GDI 字形掩膜 + 圆角色块输出 64×64 PNG，`IconCache` 缓存 Light/Dark；彩色 emoji（DWrite+D2D）暂缓，视觉上限已写入 spec §10.6。
- [ ] **T47 实机验证（未执行）**：设置页 4 条命令可见；原生文件对话框；预览文本渲染；导入后列表刷新与 Toast；撤销（含首次导入）；编辑页 CSV 回导；JSON 往返；自绘图标观感。
