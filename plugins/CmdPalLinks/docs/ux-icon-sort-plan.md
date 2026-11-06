# CmdPalLinks 下一阶段方案：添加体验 / 图标系统 / 排序 / 批量导入导出

> 状态：**方案文档，未改代码、未实机验证**（用户正在使用电脑，本轮禁止 UIA/键鼠注入/前台截图/启动 CmdPal）。
> 基线：CmdPal 0.100.2 / CmdPal UI 0.11.11762.0 / SDK `Microsoft.CommandPalette.Extensions 0.11.260520004`。
> 依据来源：本仓库源码、`ref/PowerToys/src/modules/cmdpal/extensionsdk` 的 IDL 与 Toolkit 源码、官方示例扩展（`ext/SamplePagesExtension`）、官方技能文档（`ExtensionTemplate/.github/skills/*`）。
> 凡标注「待验证」的判断，都是静态分析得出、尚未实机跑过的结论；文末 §6 给出统一的「假设 + 依据 + 验证方法」表。

---

## 0. 一句话结论

| 需求 | 结论 |
|---|---|
| 一、添加条目体验 | **能明显变好**：可从 CmdPal 搜索直达（3–4 步，原来 9–13 步），剪贴板可自动预填、文件/文件夹可用**系统原生选择器**（宿主有这条 API）。但「分段控件」和「字段下方逐字即时校验」受宿主 Adaptive Card 限制**做不到**，只能做等价替代。 |
| 二、图标系统 | **圆角色块 / 透明底可行**（自绘 PNG 交给 `IconInfo.FromStream`）；**彩色 emoji 可行但成本中高**（必须 DirectWrite+Direct2D，GDI+/WPF 只能单色），且 Win11 的 COLRv1 彩色支持需实机验证；`IconInfo` 不传尺寸，靠我们输出 64×64 PNG 让宿主缩放。 |
| 三、排到第二位 | **部分可控**：扩展自己列表页内的顺序、分组（`IListItem.Section`）、Dock 常驻可控；**全局搜索/首页里「排第二」不可控**——IDL 没有任何权重/pin/importance 字段，排序由宿主决定，不能承诺。 |
| 四、批量导入 | **CSV 能做到「解析→预览→确认→原子写→可撤销」的完整闭环**，严格对齐 `type,target,title,alias,tags,emoji,color`；**拖拽导入做不到**（IDL 无 drop 能力），改为原生文件选择器 / 粘贴内容两条路子；5000 行量级可做（需新增批量写盘，不能沿用逐条 `Add`）。 |

---

## 1. 需求一：添加条目体验重做

### 1.1 现状复现（按真实操作步骤编号）

入口结构与代码事实：Provider 只暴露 **1 条**顶层命令（[CmdPalLinksCommandsProvider.cs](../src/CmdPalLinks/CmdPalLinksCommandsProvider.cs#L31-L37)），「添加」不是顶层命令。

| # | 步骤 | 代码位置 | 摩擦点 |
|---|---|---|---|
| 1 | `Win+Space` 打开 CmdPal | 宿主 | 不可避免 |
| 2 | 输入「链接库」并回车 | Provider L31-37 | 只有 1 条命令，搜「添加」什么也搜不到 |
| 3 | 进入列表页 | Provider L37 / [ItemsPage.cs](../src/CmdPalLinks/Pages/ItemsPage.cs#L30-L55) | 多一次页面跳转 |
| 4 | 列表**非空**时空态入口消失 | ItemsPage L39-44（`EmptyContent` 仅在零条目时渲染） | 有数据后没有页面级「添加」按钮 |
| 5 | 必须选中任意一行，打开 `MoreCommands` | ItemsPage L208-217 | 添加藏在第 1 项，要先有焦点行 |
| 6 | 点「添加条目」 | ItemsPage L212（`new AddItemPage(_store)`） | 每次新建页面实例，无状态复用 |
| 7 | 面对 **5 个字段同时出现** | [ItemFormTemplate.cs](../src/CmdPalLinks/Pages/ItemFormTemplate.cs#L17-L33) | 无渐进披露，选填项和必填项同等重量 |
| 8 | 展开「类型」下拉再选 | ItemFormTemplate L18-28（`Input.ChoiceSet` 默认 compact = 下拉） | 多一次点击，且下拉遮挡 |
| 9 | 手工粘贴 target | [AddItemForm.cs](../src/CmdPalLinks/Pages/AddItemForm.cs#L17)（初始值传空串） | 明明剪贴板里就有，还要再粘一次 |
| 10 | 提交后才校验 | ItemFormTemplate L66-71（`isRequired`+`errorMessage`）；[LibraryStore.cs](../src/CmdPalLinks/Services/LibraryStore.cs#L322-L376) | URL 合法性/路径存在/重复全部在提交时才知道 |
| 11 | 点「保存」 | ItemFormTemplate L34-49 | — |
| 12 | 成功即 `Dismiss`，面板关闭 | AddItemForm L41-47 | 上下文丢失，继续加要重走 1–6 |
| 13 | 重复只给 Toast | AddItemForm L49-54 | 不定位到已有条目，也不能「跳到已有」 |
| 14 | 写盘失败也刷新列表 | AddItemForm L56-57（`Added?.Invoke()` 在 `SaveFailed` 分支） | 内存已加、磁盘失败，视觉上像成功了 |

补充事实：

- 表单注册 `Added` 只为刷新（[AddItemPage.cs](../src/CmdPalLinks/Pages/AddItemPage.cs#L18)），没有「继续添加 / 撤销」入口。
- 宿主表单**没有逐字段校验 API**：IDL `IStringParameterRun` 只有 `Text { get; set; }`，并明确写着 `// TODO! do we need a way to validate string inputs?`（[IDL L437-443](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl#L437-L443)）。
- 表单只能整体提交：`IFormContent.SubmitForm(inputs, data)` / `IFormContent2.SubmitAction(...)`（IDL L343-348、L494-496），没有「字段事件」。

### 1.2 步骤数对照

| 场景 | 旧流程 | 新流程 | 说明 |
|---|---|---|---|
| 加一条网址（剪贴板已有 URL） | 9 步（1 打开→2 搜→3 进页→4 选行→5 菜单→6 添加→7 选类型→8 粘 URL→9 保存，且保存后面板关闭） | **3 步**（1 打开→2 搜「添加链接」/直接命中→3 回车，URL 已预填→4 保存）→ 实际按键 3–4 次 | 类型自动判定，无需选 |
| 加一个文件（从零键入路径） | 11 步（含选类型 + 手打路径 + 标题） | **2–3 步**（「添加文件…」命令 → 系统文件对话框选 → 保存） | 走 `ParametersPage` + 原生 `FileOpenPicker` |
| 加一个文件夹 | 11 步 | **3 步**（选文件夹对话框 → 保存） | 需自建目录选择参数（见 §1.4） |
| 连加 3 条 | 27–39 步 | **5–6 步**（成功不关页 + 清空 target 继续） | 复用页面 |

### 1.3 重设计方案

#### A. 入口：新增 2 条顶层命令（一步到位）

```
顶层命令（TopLevelCommands 返回多条，IDL L398）
├─ 链接库            （保持不变，进列表/搜索）
├─ 添加链接/文件      （新增：直接打开 AddItemPage，剪贴板自动预填）
└─ 批量导入          （新增：见 §4）
```

- 依据：`ICommandProvider.TopLevelCommands()` 返回 `ICommandItem[]`，可多条（IDL L389-404）。当前只返回 1 条（Provider L37）。
- 效果：全局搜索里输入「添加」「add」「添加链接」都能直接命中，回车即进表单，省掉「进列表 → 选行 → 开菜单」这 3 步。
- 风险：顶层命令变多会稀释首页列表（排序不可控，见 §3）。建议提供设置项「显示快捷添加入口」（默认开），可关闭回到单命令模式。

#### B. 剪贴板预填（不再让用户粘第二次）

- 读取方式：Win32 P/Invoke `OpenClipboard` / `GetClipboardData(CF_UNICODETEXT)` / `IsClipboardFormatAvailable(CF_HDROP)`。**不用** WinRT `Windows.ApplicationModel.DataTransfer.Clipboard`（桌面进程 STA/前台限制风险）。
- 判定链（顺序执行，命中即止）：
  1. `CF_HDROP`（资源管理器里复制了文件/文件夹）→ 取第一条，`File.Exists`→file / `Directory.Exists`→folder；多条时提示「检测到 N 个文件，转批量导入」。
  2. 文本 trim → 去成对引号（「复制为路径」带 `"..."`）→ 展开 `%USERPROFILE%` → `file:///` 前缀做 URI 解码。
  3. `http/https` 绝对 URL → `kind=url`。
  4. `File.Exists` → `kind=file`；`Directory.Exists` → `kind=folder`。
  5. 都不命中 → 留空，不报错。
- 时机：页面构造 / `GetContent()` 时同步读一次（失败即跳过，非致命）。
- 多条文本（含换行）→ 不逐条加，显示一行提示并给出「去批量导入」入口（见 §4）。
- 隐私/副作用：只读、不写剪贴板；日志只记类型不记内容。

#### C. 类型切换：分段控件做不到，改为「自动判定 + 可选单选」

事实依据：

- `IFormContent` 只交 `TemplateJson` 给宿主（IDL L343-348），宿主用 Adaptive Card 1.5 渲染；IDL 里没有任何「表单控件类型」定义。
- Adaptive Card 1.5 的 `Input.ChoiceSet` 只有 `style: compact`（下拉）和 `style: expanded`（纵向单选列表），**没有 segmented control**。
- 因此「网址/文件/文件夹 三格分段控件」在宿主表单里**不可实现**。

替代方案（推荐）：

1. 默认**不显示类型选择**：按 §1.3B 自动判定；在表单顶部用一段 `TextBlock` 显示 `将保存为：网址` / `文件` / `文件夹`（预先填值时给出，静态快照）。
2. 仅当判定失败或用户不认同时，才显示 `Input.ChoiceSet style=expanded`（3 个单选）。这让类型选择从「必经步骤」降为「可选的纠正步骤」。
3. 视觉最接近分段的做法只能自绘（把 3 个类型做成 ListPage 的 3 行），代价是每次切类型多一次页面跳转 —— **步数反而变多，不推荐**。

#### D. 文件/文件夹选择：宿主**有**原生选择器，但不在表单里

**结论：能用系统原生选择器，证据充分。**

- IDL 有专门的参数页协议：`IParametersPage` 暴露 `IParameterRun[] Parameters` + `IListItem Command`（IDL L454-459）；`ICommandParameterRun.GetSelectValueCommand(UInt64 hostHwnd)` 会把**宿主窗口句柄**交给扩展（IDL L445-452）。
- Toolkit 已封装 `FilePickerParameterRun`：内部 `new FileOpenPicker()`，并用 `WinRT.Interop.InitializeWithWindow.Initialize(picker, hostHwnd)` 绑定宿主窗口（[FilePickerParameterRun.cs L70-82](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/Parameters/FilePickerParameterRun.cs#L70-L82)）。
- 官方示例扩展**实际使用**它：[ParameterSamples.cs L53-98](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/ext/SamplePagesExtension/Pages/ParameterSamples.cs#L53-L98)（`ButtonParameterTest` 选文件并读取 `StorageFile.Path`），L100-176 还有「文件 + 文本」组合页。
- Toolkit 参数页基类：[ParametersPage.cs L7-12](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/Parameters/ParametersPage.cs#L7-L12)。

落地方式：

- 新增顶层命令「添加文件…」→ `ParametersPage`：
  - `LabelRun("选择文件")` + `FilePickerParameterRun`（`SetupFilePicker` 里把 `FileTypeFilter` 设为 `*`）+ `StringParameterRun("标题（选填）")` + `StringParameterRun("标签（选填，用 | 分隔）")`；
  - `Command` = `ListItem(保存命令)`，`Invoke()` 里用 `StorageFile.OpenReadAsync`/直接 `File.Path` 落库，`ClearValue()` 清空，`KeepOpen`。
- 「添加文件夹…」：Toolkit **没有**现成的目录选择参数。两条可行路径：
  1. `Windows.Storage.Pickers.FolderPicker` + 同样的 `InitializeWithWindow`（与 FileOpenPicker 同源 API，**待验证**在扩展进程可用）；
  2. 仿官方示例 `SelectFolderPage`（[ParameterSamples.cs L276-322](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/ext/SamplePagesExtension/Pages/ParameterSamples.cs#L276-L322)）：自建一个 `ListPage` 列常用目录，选中后回填 `CommandParameterRun`。这条路径 100% 有官方代码可参照，但需要自己列目录。
- **表单里放「浏览…」按钮不行**：Adaptive Card 的 `Action.Submit` 只能整卡提交，提交后再弹对话框会把「校验/字段状态」和「导航」混在一起（`SubmitForm` 返回值只有 `ICommandResult`，没有「回填字段」的机制）。所以文件选择必须走独立的参数页命令，而不是表单按钮。

#### E. 校验：即时逐字段做不到，做「提交前预判 + 提交时具体错误」

| 能级 | 能否 | 做法 |
|---|---|---|
| 字段下方逐字校验 | **不能** | 宿主表单无字段事件/校验 API（IDL L437-443 明确 TODO）；`isRequired/errorMessage` 仅在提交时触发（ItemFormTemplate L66-71） |
| 表单顶部「预检结论」 | 能（静态） | 打开页面时把剪贴板预填值跑一遍 `LibraryStore.TryNormalize`（L322-376）+ 重复检查（L439-452），渲染成 `TextBlock`：`✓ 网址有效` / `⚠ 文件不存在` / `⚠ 已存在：<title>`。用户改字后这段不会更新（快照） |
| 提交后具体错误 + 留在页面 | 能 | `SubmitForm` 拿 `AddStatus`：`InvalidTarget`→显示 `error` 原文（L160-208 已返回「文件不存在」「网址必须…」）；`Duplicate`→「已存在：标题」+ 提供「打开已有条目」命令 |
| 参数页实时状态 | 能（最接近字段下方） | `ParametersPage` 的 `Command` 是 `ListItem`，可动态改 `Subtitle`（官方示例 L155-161 就是这么做的），显示 `✓ 路径存在` / `✗ 不存在`；但它是整行副标题，不是字段正下方 |

结论：**「错误就在字段下方、随打字更新」做不到**；能做到「进页即预判 + 提交时把具体原因留在页内」。这条必须如实告知用户，不能承诺成「边输入边校验」。

#### F. 提交后的反馈：留在原页 + 撤销 + 继续添加

- 成功不再 `Dismiss`（现状 AddItemForm L46），改为 `KeepOpen`：
  - 页面顶部显示 `✓ 已添加：<title>`（重建 `TemplateJson`，target 清空，类型/tags 保留）；
  - `Command` 返回 `ShowToast` 时给一条「撤销」入口。
- 撤销：`LibraryStore.Remove(id)` 已存在（L~240）；记录 `lastAddedId`，撤销命令删掉它并提示「已撤销」。更省事的做法是用 `IToastArgs2.Command`（IDL L103-107）把「撤销」按钮挂在 Toast 上 —— **待验证**宿主 0.11 是否渲染该按钮；不渲染就退回「页面底部的撤销命令项」。
- 「继续添加」：就是 `KeepOpen` + 清空 target，同一页可连加。
- 「已存在」顺带「打开已有条目」命令（`OpenItemCommand` 已有），闭环。
- 修正现状 14（写盘失败也 `Added?.Invoke()`）：只有当 `Status == Added` 才刷新列表；`SaveFailed` 显示「写入磁盘失败，未生效」并保留页面。

### 1.4 可行边界汇总（需求一）

| 子需求 | 判定 | 依据 |
|---|---|---|
| 从命令一步进入 | ✅ 可做 | 多顶层命令（IDL L398） |
| 剪贴板自动预填 | ✅ 可做 | Win32 剪贴板 API；现有 `ClipboardHelper` 已有 P/Invoke 样例 |
| 类型分段控件 | ❌ 不可 | Adaptive Card 1.5 无 segmented；宿主只渲染 Template |
| 文件/文件夹原生选择器 | ✅ 可做（文件已证实；文件夹需验证/自建列表） | `IParametersPage`+`FilePickerParameterRun`；官方示例实跑 |
| 字段下方即时校验 | ❌ 不可 | IDL L442 TODO；表单仅整体提交 |
| 提交后停留/撤销/继续 | ✅ 可做（Toast 撤销按钮待验证） | `CommandResult.KeepOpen`、`Remove`、`IToastArgs2.Command` |

### 1.5 工作量（需求一）

| 项 | 估时 |
|---|---|
| 顶层命令 + 剪贴板预填 + 自动判定类型 | 0.5–1 天 |
| 表单重构（3 个选填字段折叠、预检 TextBlock、失败留页） | 1 天 |
| 「添加文件…」参数页（原生 picker） | 0.5 天 |
| 「添加文件夹…」参数页（自建目录列表或 FolderPicker） | 0.5–1 天 |
| 撤销/继续添加 + 列表联动修正 | 0.5 天 |
| **合计** | **3–4 天** |

---

## 2. 需求二：图标系统（色块 / 透明底 / emoji）

### 2.1 `IconInfo` 到底接受什么

| 能力 | 事实 | 依据 |
|---|---|---|
| 字符串 | 被宿主当作**绝对文件路径**或**字体字形**（如 `"\uE774"`） | `IconInfo(string?)` 把同一个 `IconData` 赋给 Light/Dark（[IconInfo.cs L19-22](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/IconInfo.cs#L19-L22)）；官方 dock 技能用 `new IconInfo("\uE774")` 作字形 |
| 流 | `IRandomAccessStreamReference`（`IconData.Data`） | `IconInfo.FromStream(stream)`（IconInfo.cs L41-45）；IDL `IIconData`：`String Icon` + `Data`（L22-25） |
| 相对路径 | 需自己拼 `AppDomain.CurrentDomain.BaseDirectory` | `IconHelpers.FromRelativePath`（[IconHelpers.cs L8-12](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/IconHelpers.cs#L8-L12)） |
| 明暗双份 | `IconInfo.Light` / `IconInfo.Dark` | IDL L28-31；`IconInfo(IconData light, IconData dark)` L24-28 |
| 尺寸/格式约束 | **IDL 未定义**任何尺寸；宿主自行缩放 | IDL L21-31 无尺寸字段；本项目实测 256×256 PNG（jumbo 图标）在列表/网格渲染正常（V1 记录：notepad 256×256、67 608 B） |
| 来源 | 只支持位图解码（PNG 等）与字体字形，**不支持 SVG/CSS/HTML** | `IconInfo` 无 stream 以外的内容类型入口 |

设计含义：我们要出图，就走「内存 PNG → `FromStream`」这条（`IconCache.ToIconInfoAsync` 已在用，写完后 `stream.Seek(0)`），并**明暗各出一份**。

### 2.2 彩色 emoji 可行性（关键判断）

| 方案 | 彩色？ | 结论 | 依据/成本 |
|---|---|---|---|
| GDI+ / `System.Drawing`（`Graphics.DrawString`、`TextRenderer`） | ❌ | **不可行** | GDI/GDI+ 只取字形轮廓，不解析 COLR/CPAL 彩色表；Segoe UI Emoji 会被画成单色字形。SDK 包本身也不携带 `System.Drawing.Common`（本项目 M2 已因此弃用 `ThumbnailHelper`） |
| WPF `RenderTargetBitmap` | ❌ | **不可行** | WPF 文本栈不支持彩色字体（已知限制），且需要额外引入 PresentationCore |
| Win2D (`CanvasTextLayout`) | ✅ 理论可行 | **不采用** | 需要 WinAppSDK/WinUI 依赖，与本进程外 WinRT 扩展（`net10.0-windows10.0.26100.0`）不匹配，包体与启动成本都过高 |
| XAML `RenderTargetBitmap`（WinUI island） | ✅ 理论可行 | **不采用** | 需在扩展进程里起 WinUI 树/XAML island，成本与风险最高 |
| **DirectWrite + Direct2D（WIC 位图渲染目标）** | ✅ | **可行（推荐）** | D2D 的 `DrawTextLayout`/`DrawGlyphRun` 配 `D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT` 会走彩色字形路径；可渲到 `IWICBitmap`（纯软件，不需要 GPU/交换链） |

**明确判断：彩色 emoji 可行，但必须 DirectWrite+Direct2D；GDI+ 路线只能单色。**

落地依赖的两条路：

1. `Vortice.Windows`（NuGet，D2D/DWrite/WIC 包装）——接入约 1–2 天，发布时自包含 DLL 进 MSIX（现有打包方式支持）。
2. 手写 COM interop（`ID2D1Factory`、`IDWriteFactory`、`IDWriteTextLayout`、`IWICBitmap`）——3–5 天，风险高。

**置信度分级（必须如实说）：**

- COLR v0 彩色字形（Win10 经典扁平 emoji）：**高置信可行**。
- Win11 的 COLR v1（渐变/3D Fluent emoji）：**中高置信**，取决于 D2D 版本是否实现 v1 paint 解析。**待验证**：写一个离线小工具把 `😀🚀❤️👨‍👩‍👧‍👦` 渲成 PNG，检查像素是否非灰阶。
- 若 v1 渲染失败，仍能拿到 v0 扁平彩色或单色回退，不会「完全不能用」。

### 2.3 若彩色不可行的替代方案与视觉损失

| 替代 | 做法 | 视觉损失 |
|---|---|---|
| 单色字形掩膜 + 着色 | GDI+ 渲黑字形 → 用 alpha 掩膜按 `iconColor` 着色 | emoji 失去原色/渐变/肤色，变成单色图标；用户认不出「🍎 vs 🍅」的差别会变大 |
| 内置矢量符号集 | 用 Segoe Fluent Icons 字形（本身就是单色，`new IconInfo("\uE774")` 同机制） | 没有 emoji 表情语义，只能选几十个通用符号 |
| 单色 emoji + 色块底 | 色块提供颜色，emoji 提供形状 | 比纯单色好一些，但仍不是彩色 emoji |

### 2.4 设计

#### 数据模型（`schemaVersion` 1 → 2）

`Models/LinkItem.cs` 追加三个字段（camelCase 落盘）：

```json
{
  "iconKind": "auto",          // auto | block | transparent | emoji
  "iconEmoji": "🚀",           // 单个 emoji（允许 ZWJ/肤色修饰序列），可空
  "iconColor": "#2563EB"       // #RRGGBB；空 = 透明底
}
```

- `auto`：沿用现有行为（URL favicon / file/folder Shell 图标 / 通用字形）。
- `block`：圆角色块底 + 前景（emoji 或首字母/类型字形）。
- `transparent`：无底色，仅前景。
- `emoji`：以 emoji 为主体（可叠加 `iconColor` 作为底色；不填色则透明）。
- 读取旧文件：缺字段补默认 `auto`；写回时升级 `schemaVersion=2`，旧版读新文件会忽略未知字段（向前兼容）。
- 与 CSV 的映射（见 §4.10）：`emoji` 非空 → `iconKind=emoji`；仅 `color` → `block`；两者都空 → `auto`。

#### 渲染规则

| 参数 | 规则 |
|---|---|
| 输出尺寸 | 64×64 PNG（覆盖 16/20/24/32 DIP 的 1×–2× 缩放）；宿主自行缩到实际尺寸 |
| 圆角半径 | `radius = size * 0.22`（64px → 14px），接近 Fluent 的「圆角方块」观感 |
| 内边距 | 色块内 8%（64px → 5px）；emoji 字号 `size * 0.72`（64px → 46px） |
| 透明底 | 只画前景，保留 alpha；宿主深色底上需保证对比 |
| 深浅对比 | Light/Dark 各出一份：深色主题 → 前景提亮（白/`#F8FAFC`）、色块降饱和；浅色主题 → 前景压暗、色块用 `iconColor` 原值。用 `IconInfo(light, dark)` 双份传入 |
| 无 emoji 时的前景 | 首字母（title 首个非空白字符）或类型字形（URL `\uE71B` / file `\uE8A5` / folder `\uE8B7`） |
| 已选 emoji 但渲染失败 | 回退「单色字形 + `iconColor` 着色」，并记日志 `emoji render fallback` |

#### 缓存

- 键：`SHA1(iconKind|iconEmoji|iconColor|theme|size)`；文件名 `icon-{hash}.png`，落在 `LocalState/icons/`（与现有 favicon 缓存同目录）。
- 失效：任一字段变化 → 新键；旧文件按现有 90 天清理。
- 复用 `IconCache` 已有结构（内存 `ConcurrentDictionary` + 30s 校验节流 + 5min 失败重试），只新增「虚构图标」分支，不做文件系统探测。
- emoji 键要按**规范化后的码点序列**（避免同一 emoji 的不同变体选择符生成两份）。

#### 表单里怎么选

宿主表单能力有限，拆成两部分：

1. **emoji 输入**：`Input.Text`（用户粘贴/输入 emoji；UTF-8 原样，不做 `\uXXXX` 转义），文案提示「粘贴一个 emoji，可留空」。
2. **颜色选择**：`Input.Text` 输入 `#RRGGBB`，卡片 `TextBlock` 里列出 10 个预设色值（`#2563EB #0EA5E9 #10B981 #F59E0B #EF4444 #8B5CF6 #EC4899 #64748B #111827 #FFFFFF`）。**真正的色板 chips 做不了**（Adaptive Card 无此控件），除非用 `Input.ChoiceSet expanded` 把 10 个颜色写成 10 个单选项（可用但很长）。
3. **预览**：表单内嵌图片预览**不可靠**（Adaptive Card `Image` 需要可访问的 URL，本地 `file://` 是否被宿主加载**待验证**）。现实做法：
   - 保存后在列表里立刻看到最终效果（0 额外成本）；
   - 或新增「图标预览」页：3 行 `ListItem` 分别代表 `block` / `transparent` / `emoji`，每行 `Icon` 就是实时生成的 PNG，用户在选之前先看三种效果。这是**最可靠**的预览方式。

### 2.5 工作量（需求二）

| 项 | 估时 |
|---|---|
| 数据模型 + schema 迁移 + 设置/表单字段 | 0.5 天 |
| 色块/透明底渲染器（GDI+ 或自绘，含明暗双份） | 1 天 |
| 彩色 emoji（引入 Vortice + D2D/DWrite 渲染 + 回退） | 2–3 天 |
| 缓存键/落盘/清理 | 0.5 天 |
| 表单/预览页 | 0.5–1 天 |
| **合计** | **4.5–6 天**（若只做单色 emoji，约 2–2.5 天） |

---

## 3. 需求三：能不能排到第二位

### 3.1 IDL / 文档依据（逐条）

| 接口 | 字段 | 结论 |
|---|---|---|
| `ICommandItem`（IDL L268-275） | `Command` / `MoreCommands` / `Icon` / `Title` / `Subtitle` | **无**权重、评分、排序、固定、重要度字段 |
| `IListItem`（IDL L287-293） | `Tags` / `Details` / `Section` / `TextToSuggest` | 唯一可控的是 `Section`（分组标题） |
| `IFallbackCommandItem`（L378-382）/ `IFallbackCommandItem2`（L384-387） | `FallbackHandler` / `DisplayTitle` / `Id` | 无 rank/score；handler 只能 `UpdateQuery(query)`，不能改变宿主排名 |
| `ICommandProvider`（L389-404） | `TopLevelCommands()` / `FallbackCommands()` / `GetCommand(id)` | 只有「提供哪些」，没有「排第几」 |
| `ICommandProvider3`（L461-465） | `GetDockBands()` | Dock 常驻是唯一的「固定」形态 |
| `IListPage`（L317-330） | `HasMoreItems` / `GetItems()` / `LoadMore()` 等 | 页内顺序 = 我们返回的数组顺序 |
| 设置页 | `ICommandSettings`（L369-371） | 只能控制本扩展自己的设置页 |

补充事实：本仓库 `ref/PowerToys/src/modules/cmdpal` 下只有 `ext/`、`extensionsdk/`、`ExtensionTemplate/`，**没有 CmdPal UI 宿主源码**，所以「宿主用什么算法排序」不能从源码证实；只能从 IDL「没有提供控制点」+ 我们已有的运行观察推断。这是本结论的依据边界，必须说明。

### 3.2 结论：**部分可控**

**能控的：**

1. **自己列表页内的顺序**：`ItemsPage.GetItems()` 返回的数组顺序就是显示顺序（IDL L317-330）。可在 `SearchEngine` 排序前加一层「置顶」权重（新增 `pinned` 字段 → 分数置顶）。
2. **分组**：`ListItem.Section` 可把结果分成「置顶 / 最近 / 其他」（IDL L291）。组内顺序仍由数组顺序决定；**组间顺序由宿主决定（待验证是否按首次出现顺序）**。
3. **Dock 常驻**：`GetDockBands()` 可让扩展出现在屏幕边缘的常驻工具栏（IDL L461-465；官方技能文档 `add-dock-band` 明确「多按钮带 + 需要 Command 的 Id 非空」）。这是唯一稳定的「固定入口」。
4. **减少顶层命令数量**：顶层命令越少，本扩展在首页列表里越不分散（这不是排序控制，只是减少竞争项）。

**不能控的：**

1. CmdPal 首页/全局搜索中本扩展与其它扩展的相对位置。
2. 搜索结果的评分算法（frecency、最近使用、匹配度由宿主决定）。
3. 扩展列表在「扩展管理」里的顺序（按注册/安装顺序或名称，宿主内部逻辑）。
4. `FallbackCommands` 的插入位置。

**因此：不能承诺「排第二」。** 想达到「打开面板第一眼就看到链接库」，可靠手段只有：
- 把「链接库」pin 到 Dock（用户手动或在 Dock 里固定）；
- 用全局唯一关键词（如输入 `lk` 直接命中）；
- 减少顶层命令数量（只保留 1 条）。

### 3.3 需要拍板

- 是否接受「首页不保证第二，但 Dock 常驻 + 关键词直达可以做到」？
- 若坚持「第二」，需要在用户离开电脑时实机验证多顶层命令的实际排列（当前无法验证）。

### 3.4 工作量

- 页内置顶/分组（`pinned` 字段 + Section 分组）：0.5 天。
- Dock band 接入（`GetDockBands()` + 命令 Id + 图标）：0.5 天（**待验证**宿主是否显示我们的 band）。
- 全局排序：**0 天（做不到）**。

---

## 4. 需求四：批量导入 / 导出（严格对齐 CSV 表结构）

### 4.1 表结构（唯一交换格式）

```csv
type,target,title,alias,tags,emoji,color
url,https://github.com, GitHub ,gh|git,dev|code,🐙,#2563EB
file,C:\Users\me\Documents\报价单 2026.xlsx,,,,
folder,D:\项目\客户资料,,客户|crm,,#10B981
```

- 表头**必须完全一致、顺序一致**：`type,target,title,alias,tags,emoji,color`。
- `type`：`url` | `file` | `folder`（小写）。
- `target`：完整网址或绝对路径。
- `title`：空则按现有规则推断（`DefaultTitle`：URL 用 host+path，文件用文件名，文件夹用目录名，[LibraryStore.cs L454+](../src/CmdPalLinks/Services/LibraryStore.cs#L454-L460)）。
- `alias` / `tags`：多个用 `|` 分隔。
- `emoji`：单个 emoji，可空。
- `color`：`#RRGGBB` 或空（空 = 透明底）。

### 4.2 支持格式与优先级

| 扩展名 | 用途 | 解析规则 |
|---|---|---|
| `.csv` | **主交换格式**（与 HTML 编辑页互通） | 见 §4.3 |
| `.json` | 本插件自己的导出格式（完整备份/还原） | 现有 `LibraryData`（`schemaVersion`/`items`），直接 `JsonSerializer` 复用 `LibraryStore.ReadFile` 逻辑；校验 `items` 存在 |
| `.txt` | 一行一条 URL/路径，`#` 开头为注释，空行跳过 | 每行 trim；`#` 整行注释；逐行 `TryNormalize` 自动判定类型；无法判定 → 非法行 |
| `.md` | 尽力而为（见 §4.12） | 抽取行内链接/裸 URL；不做结构语义 |

按扩展名路由；未知扩展名 → 报「不支持的格式（支持 .csv/.json/.txt/.md）」。

### 4.3 CSV 解析规则（容错逐条）

| 情况 | 处理 |
|---|---|
| UTF-8 BOM | 读取后剥离首个 `\uFEFF`；**导出时写入 BOM**（保证 Excel 中文不乱码） |
| 非 UTF-8（GBK 等） | 先按 UTF-8 解码；出现替换字符 `\uFFFD` → 该文件判为「编码不是 UTF-8」，提示另存为 UTF-8（不做静默 GBK 猜测，避免乱码入库） |
| CRLF / CR / LF | 统一按 `\r\n|\r|\n` 切行；末行无换行也接受 |
| 引号字段 | RFC 4180：`"..."` 包裹；内部 `""` = 一个 `"`；引号内允许逗号与换行 |
| 字段内逗号 | 必须被引号包裹；未包裹的多余逗号 → 字段数 ≠ 7 → 非法行（行号 + 原因） |
| 中文路径 | 不做任何转义；允许空格；`target` 只 trim 首尾，不折叠内部空格 |
| 首尾空格 | 所有字段 trim；`title` 前后空格去掉（示例里 `" GitHub "` → `GitHub`） |
| emoji 编码 | **UTF-8 原样存储**（`😀` 就是 UTF-8 字节，不写 `\uXXXX`、不写 HTML 实体）；允许 ZWJ 组合（`👨‍👩‍👧‍👦`）与肤色修饰（`👍🏽`） |
| `alias`/`tags` 分隔 | 按 `|` 切；去空、去重（保序）；也兼容中文逗号（`，`）与英文逗号作为次级分隔（容错，但以 `|` 为准） |
| `emoji` 多字符 | 只接受「一个 emoji 序列」（1–2 个码点含变体选择符，或 ZWJ 链）；超过则截取第一个并给警告 |
| `color` | 正则 `^#[0-9A-Fa-f]{6}$`；非法 → **警告并置空**（不判整行非法） |
| 空行 | 跳过，不计入非法 |
| 表头缺失/顺序不符 | **整份拒绝**，报「期望表头 … 实际表头 …」 |

### 4.4 用户怎么触发导入

**能用原生文件选择器（证据同 §1.3D）：**

- `IParametersPage` + `FilePickerParameterRun`：官方示例 `ParameterSamples.cs L53-98` 就是这么选文件的；`SetupFilePicker` 里设置 `FileTypeFilter.Add(".csv")`/`.json`/`.txt`。
- 流程：顶层「批量导入」→ 参数页 → 点「选择文件」弹系统对话框 → 选中 → 点「解析预览」。

**拖放：做不到。**

- IDL 没有任何 drop target / drag 事件接口；`ICommandItem` 只有 `Command/MoreCommands/Icon/Title/Subtitle`（L268-275）。
- Toolkit 的 `CommandItem.DataPackage`（[CommandItem.cs L105-127](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/CommandItem.cs#L105-L127)）是**把内容放进剪贴板/拖出**用的输出通道，不是接收拖入。官方技能目录（5 个）里也没有 drag-drop。
- 结论：**不承诺拖拽**。替代：`FileOpenPicker` 选文件，或把文件内容粘进多行文本框。

**兜底入口：**

- 「批量导入（粘贴内容）」：`ContentPage` + `FormContent`，一个 `Input.Text`（`isMultiline: true`）粘贴 CSV 文本 → 走同一解析器。

### 4.5 导入流程（解析 → 预览 → 确认 → 写入）

```
[选择文件] → 读取（≤5MB，UTF-8 校验）
   → 后台解析（单遍，无 IO）
   → 结果分组：可导入 N / 重复 D / 非法 E（含行号+原因）
   → 预览页（顶部「确认导入」行 + 统计行 + 前 20 条 + 问题列表）
   → 用户点「确认导入」→ 单次原子写 → Toast「导入 N（跳过 D，非法 E）· 总数 A→B」
   → 可撤销（恢复快照）
```

预览页建议做成 `ListPage`：

- 第 1 行：`✔ 确认导入 117 条`（`ListItem`，`Icon=Icons.Add`），点它才写盘；
- 第 2 行：统计 `可导入 117 · 重复 8 · 非法 3`；
- 第 3 行起按 `Section` 分组：`新增`（前 20 条预览）/ `重复（将跳过）` / `非法（行 12：type 必须是 url/file/folder）`。

### 4.6 重复判定与策略

- 判重键 = 现有 `SameKey`：`(kind, 规范化 target)`；URL 规范化去端口/fragment，路径 `GetFullPath` + `TrimEnd`（[LibraryStore.cs L322-376](../src/CmdPalLinks/Services/LibraryStore.cs#L322-L376)、L439-452）。
- 默认策略：**跳过**（不覆盖），并在预览里显示「已存在 → 将跳过」。
- 可选策略：**覆盖**（用户在预览页切换，或设置项 `importDuplicatePolicy = skip|overwrite`）：
  - 更新 `title/target/alias/tags/iconKind/iconEmoji/iconColor/updatedAt`；
  - `alias` 与 `tags`：**合并（并集，保序去重）**，不整段替换（避免覆盖用户手写的别名/标签）；
  - `openCount` / `lastOpenedAt` / `createdAt`：**保留旧值**（不重置使用历史）；
  - `id`：保留旧 `Id`（引用不失效）。
- 同一文件内的重复行：按「先出现为准」，后面的标为「文件内重复」并跳过（给出两行行号）。

### 4.7 非法行

- 每行独立判定，**不整份拒绝**；报告 `行号 + 原因`（示例：`第 3 行：type 值 'link' 非法（应为 url/file/folder）`）。
- 行号 = 文件真实行号（表头为第 1 行）；引号内含换行时按起始行号计，并注明「跨行字段」。
- 全部非法或 0 条可导入 → **不写盘**，预览页显示「没有可导入的条目」+ 问题清单。
- 表头不符 → 整份拒绝（这是唯一整份拒绝的情况）。
- 决策点：`file/folder` 的 target 在本机不存在 → 默认**仍可导入**(标「缺失」，复用现有 `IsMissing` 机制) 而不是判非法；`url` 非 http/https → 判非法。**此点需用户拍板**。

### 4.8 导出

- 顶层命令「导出全部（CSV）」：
  - 路径：`LocalState/exports/links-YYYYMMDD-HHmm.csv`（目录自动创建）；
  - 编码：**UTF-8 with BOM**，行尾 CRLF，表头严格 `type,target,title,alias,tags,emoji,color`；
  - 字段转义：含 `,` `"` `\r` `\n` 时用 `"` 包裹、内部 `"` 写成 `""`；`alias/tags` 用 `|` 连接；`color` 空则留空；`emoji` 空则留空；
  - `iconKind` 的映射：`auto` → emoji/color 都空；`block` → color 非空、emoji 空；`emoji` → emoji 非空（color 可空）；`transparent` → 两者都空（**损失**：`transparent` 与 `auto` 在 CSV 里无法区分，导出时若原值是 `transparent` 则额外写设置提示或接受降级为 auto —— 需拍板）。
  - 完成后 Toast 路径 + 「打开所在文件夹」命令（`RevealInFolderCommand` 已有）。
- 顶层命令「导出备份（JSON）」：完整存储格式（含 `id/openCount/lastOpenedAt/schemaVersion`），用于回导/迁移。
- **自洽性保证**：导出 CSV → 再导入 → 除运行态字段（`openCount/lastOpenedAt`）与 `transparent` 外必须一致；写一条自动化回归用例（离线，无需 CmdPal）。

### 4.9 规模、超时与性能

- 目标：**5000 行解析 < 300 ms**（单遍、无正则回溯、无逐行 IO；CSV 手写状态机）。
- 上限：**10 000 行或 5 MB**（先到者为准）；超出提示「请拆分为多个文件」。
- 超时保护：解析在后台线程，`CancellationToken` 15 s；超时 → 中止并显示「文件过大/解析超时，未写入任何条目」。
- UI：预览只渲染前 200 条（其余只计数量），避免宿主一次渲染上万行。
- **必须新增批量写**：现有 `Add()` 每条都 `SaveLocked()`（[LibraryStore.cs L196](../src/CmdPalLinks/Services/LibraryStore.cs#L160-L208)），5000 条会写盘 5000 次（不可接受）。新增 `ImportBatch(items)`：单锁、单次 `SaveLocked`。

### 4.10 写入安全与撤销

- **原子写**：复用 `SaveLocked`（`.tmp` → `File.Replace(..., .bak)`，[L411-437](../src/CmdPalLinks/Services/LibraryStore.cs#L411-L437)）。
- **快照**：导入前把当前 `links.json` 复制为 `links.json.import-YYYYMMDD-HHmmss.bak`（保留最近 3 份）。
- **失败回滚**：写盘异常 → 内存回滚到导入前状态（与现有 `Add` 的「内存不回滚」不同，导入要回滚，因为批量）。
- **条数对比**：Toast/预览 `共 5 条 → 导入 117 条（跳过 8，非法 3）→ 现 122 条`。
- **撤销整批**：命令「撤销上次导入」→ 恢复快照（需确认 `IConfirmationArgs`，IDL L109-114），并记录 `importId`。

### 4.11 与前三项共用同一数据模型

| CSV 列 | 落到 | 说明 |
|---|---|---|
| `type` | `LinkItem.Kind` | 已是 `url/file/folder` |
| `target` | `LinkItem.Target` | 规范化后再存（URL 去 fragment/端口；路径 `GetFullPath`） |
| `title` | `LinkItem.Title` | 空则 `DefaultTitle` |
| `alias` | `LinkItem.Aliases[]` | `|` 分隔 |
| `tags` | `LinkItem.Tags[]` | `|` 分隔 |
| `emoji` | `LinkItem.IconEmoji` + `IconKind="emoji"` | 非空即 emoji 图标 |
| `color` | `LinkItem.IconColor` + `IconKind="block"`（emoji 为空时） | 空 = 透明底 |

- `schemaVersion`：**1 → 2**（新增 `iconKind/iconEmoji/iconColor`；旧文件读入补默认值，写回时升级）。
- 添加表单（需求一）与导入共用同一个 `IconSpec` 值对象，避免两套规则。
- 图标系统（需求二）的缓存键直接复用 CSV 的 `emoji+color`，导入后无需额外处理。

### 4.12 非结构化「清单」能解析到什么程度

若用户维护的是一份 Markdown 清单：

| 形态 | 能解析 | 不能 |
|---|---|---|
| 行内链接 `[文字](https://…)` | ✅ 取 URL + 用「文字」作 title | — |
| 裸 URL / `<https://…>` | ✅ | — |
| Markdown 表格第一列是 URL | ✅ 尽力（按 `|` 切第一/第二列） | 多列表格的语义 |
| 纯文本绝对路径整行（`C:\…` 或 `\\server\…`） | ✅ 仅当该行**只有一个**路径 | 行内夹带说明文字 |
| 相对路径 / 图片链接 / 锚点 | ❌ | 不会做基路径解析 |
| 按标题/章节归类成 tags | ❌ | 不解析 Markdown 结构，不推断层级 |
| front-matter / YAML | ❌ | 不解析 |

结论：**Markdown 只能「抽 URL」，不能当结构化清单**。要可靠批量导入，请用 CSV。

### 4.13 工作量（需求四）

| 项 | 估时 |
|---|---|
| CSV 解析器（RFC4180 + BOM + 行号/错误模型） | 0.5–1 天 |
| 导入参数页（原生 picker）+ 粘贴入口 | 0.5 天 |
| 预览页（统计/分组/前 20 条/问题清单） | 1 天 |
| `ImportBatch` 单次写盘 + 快照/回滚/撤销 | 0.5–1 天 |
| 导出 CSV / JSON | 0.5–1 天 |
| 回归用例（导出→导入一致） | 0.5 天 |
| **合计** | **3.5–5 天** |

---

## 5. 需要拍板的点（按优先级）

1. **接受「没有分段控件、没有字段下方逐字校验、不能拖拽、不能保证排第二」这四条硬限制吗？**（如果必须满足原文，需求一/三/四要降级或改交互）
2. **彩色 emoji 的预算**：接受引入 `Vortice.Windows`（+1 依赖）和 2–3 天成本，还是先做单色 emoji + 色块？
3. **导入时 file/folder 指向不存在的路径**：允许导入并标「缺失」，还是判非法？
4. **重复策略默认值**：跳过（安全）还是覆盖（方便）？覆盖时 alias/tags 合并规则是否照 §4.6？
5. **CSV 里 `transparent` 无法表达**（与 `auto` 同形）：接受导出降级为 `auto`，还是增加第 8 列（违背「表头严格一致」）？
6. **是否保留 `.txt` / `.md` 入口**（会增加维护面）。
7. **`schemaVersion` 升到 2**（旧版扩展读新文件会忽略图标字段），确认可以接受。
8. **顶层命令是否允许从 1 条增加到 3 条**（会影响首页列表观感，且排序不可控）。

---

## 6. 假设与待验证清单（本轮未实机）

| # | 假设 | 依据 | 验证方法（用户不在电脑时） |
|---|---|---|---|
| A1 | 宿主会为 `IParametersPage` 提供有效 HWND，原生对话框能弹出 | IDL L445-452；`CommandParameterRun.GetSelectValueCommand` 设置 HWND；官方示例在用 | 装包 → 打开「添加文件…」→ 点「选择文件」→ 看是否弹系统对话框 |
| A2 | 宿主 0.11 渲染 `IToastArgs2.Command`（Toast 上的撤销按钮） | IDL L103-107 定义了该字段（较新 contract） | 保存一条 → 看 Toast 是否带按钮；无则用页面内撤销命令 |
| A3 | 修改 `FormContent.TemplateJson` 会触发宿主重绘 | 模板驱动，但无文档保证 | 保存后清空 target 并换模板 → 看输入框是否刷新 |
| A4 | Direct2D 能渲染 Segoe UI Emoji 的 COLRv1（Win11 3D emoji） | D2D `ENABLE_COLOR_FONT`；COLRv1 支持取决于版本 | 离线小工具渲染 `😀🚀👨‍👩‍👧‍👦👍🏽` → 检查 PNG 像素是否带彩色 |
| A5 | 多顶层命令时宿主如何排序/呈现 | IDL 无排序字段；宿主源码不在本仓库 | 加 3 条命令 → 记录首页顺序与搜索命中位置 |
| A6 | `StorageFile` 可直接读取（无需额外文件能力） | 官方示例 `ParameterSamples` 读取 `file.Path`；picker 自带授权 | 导入页选一个 CSV → 看能否读出内容 |
| A7 | `FolderPicker` 在扩展进程内可用 | 与 `FileOpenPicker` 同族，但未在示例中出现 | 加临时命令 → 点选文件夹 → 看是否弹出 |
| A8 | `IListItem.Section` 的组间顺序按首次出现 | IDL 只有 `Section` 字符串 | 造 3 组数据 → 观察组顺序 |
| A9 | 宿主接受 64×64 PNG 并缩放清晰（非模糊） | 现有 256×256 实测可渲染 | 输出 64×64 后目视列表/网格清晰度 |
| A10 | 表单 `Input.Text` 能原样保留 emoji（不做代理对截断） | Adaptive Card 文本输入；未见限制 | 输入 `👨‍👩‍👧‍👦` → 保存后看 `links.json` 是否为完整序列 |

---

## 7. 总工作量汇总

| 需求 | 估时 | 可交付程度 |
|---|---|---|
| 一、添加体验 | 3–4 天 | 步数 9–13 → 3–4；剪贴板预填 + 原生文件选择器；分段/逐字校验不可 |
| 二、图标系统 | 4.5–6 天（单色版 2–2.5 天） | 色块/透明底 100%；彩色 emoji 依赖 D2D，需验证 |
| 三、排序 | 1 天 | 页内顺序/分组/Dock 可做；全局「第二」不可 |
| 四、CSV 导入导出 | 3.5–5 天 | 完整闭环；拖拽不可 |
| **合计** | **12–16 天** | — |

> 本文件不含任何实测数据；所有「可行/不可行」均基于 IDL 与官方源码的静态证据，运行时行为一律列入 §6 待验证。

---

## 8. 落地实现状态（2026-11-05，仅代码 + 构建，未实机）

用户反馈「找不到导入入口」，确认按 §5 的推荐默认值落地：接受分段/逐字校验降级、重复默认跳过 + 覆盖可选、`schemaVersion` 2、`color` 空=自动/`none`=透明/十六进制=指定色、彩色 emoji 暂缓、排序不做承诺。

### 8.1 入口位置（对「导入应放设置里」的回应）
- 设置页 `链接库设置` 的**页面命令区**挂 4 条：`导入 CSV`、`导出全部为 JSON`、`导出全部为 CSV`、`撤销上次导入`。
- 为什么不能「插进表单最后一页底部」：`Settings.ToContent()` 只返回表单 `IContent`，Toolkit 没有把命令注入表单的 API；命令只能挂 `ContentPage.Commands`，由宿主渲染为页面级命令，**位置/呈现由宿主决定**。
- 顶层保留 `导入` 别名：搜索可直接命中；与设置页入口共用同一个 `ImportPage` 实例。

### 8.2 与方案 §4 的差异（已按可实现的降级落地）
| 方案 §4 描述 | 落地实现 |
|---|---|
| 独立预览页 | 参数页两段式：第一次点「解析预览」，第二次点「确认导入」；预览信息用 `LabelRun` + Toast，避免依赖未验证的页面导航 |
| 快照保留 3 份（`import-<ts>.bak`） | 单份 `links.json.import-bak`（覆盖上一次）；快照写入“导入前内存状态”，首次导入也可撤销 |
| 后台解析 + 15s 超时 | 同步解析（上限 10000 行 / 5MB，解析为内存操作，不做超时线程） |
| 导出降级 transparent 为 auto | CSV 用 `none` 精确表达 `transparent`；JSON 完整保留 `iconKind` |
| `.md` 仅抽 URL | 未实现（只支持 `.csv/.json/.txt`），减少维护面 |

### 8.3 视觉限制（彩色 emoji 暂缓）
- 本轮不引入 `Vortice.Windows`，emoji 用 GDI 取 `Segoe UI Emoji` 字形轮廓做**单色掩膜**，再按色块/明暗主题着色。
- 视觉上限：**没有原色、渐变和 COLR/CPAL 彩色层**；ZWJ 组合序列（如家庭 emoji）在 GDI 下可能没有单个字形，会退回首字（`Segoe UI` 渲染）。
- 彩色方案仍为 DirectWrite + Direct2D（`ENABLE_COLOR_FONT`）+ WIC，COLRv1 支持待验证（A4），后续再评估依赖成本。

### 8.4 本轮未做的验证（不实机、不动用户输入）
- T40/A1–A10 全部仍未执行；新增 T47（设置页可见性、原生对话框、预览渲染、列表刷新、撤销、CSV 回导、JSON 往返、图标观感）。
- 所有结论以「构建 0 警告 0 错误 + 静态代码路径」为证据，不代表运行时表现。
