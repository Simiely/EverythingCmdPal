# Everything for Command Palette — 详细开发计划

> 目标：给 PowerToys 命令面板（CmdPal）做一个 Everything 搜索扩展，结果可以**在面板内预览**，并且能**直接拖到其他软件**。
>
> 本计划的所有关键结论都基于一手来源（微软 Learn、PowerToys 仓库源码、voidtools SDK 仓库），来源见 [§11](#11-依据与参考)。

---

## 1. 结论先行

| 需求 | 可行性 | 实现方式 | 依据 |
|---|---|---|---|
| 调用 Everything 检索 | ✅ | 复用 EverythingToolbar 的搜索客户端（MIT），1.5 走 SDK3 命名管道，1.4 回落 WM_COPYDATA | `EverythingToolbar.Core/Search`、`Platform/Search`、voidtools `everything_sdk3` |
| 面板内预览 | ✅ | `ListItem.Details` + `ListPage.ShowDetails = true`，`Details.Content` 放 `ImageContent` / `PlainTextContent` / `MarkdownContent` | `Details.cs`、`DetailsViewModel.cs`、`DetailsPane.xaml.cs` |
| 预览大图 / PDF / Office | ✅（外部窗口） | 调 PowerToys **Peek**：`PowerToys.Peek.UI.exe "<path>"` | `PeekFileCommand.cs` |
| 拖到其他软件 | ✅ | 结果项设置 `DataPackage`（StorageItems，异步 deferral） | `CommandItem.cs`、`IndexerListItem.cs`、`DataPackageHelper.cs`、PowerToys 0.97 release notes |
| 完全嵌入 EverythingToolbar 的 UI | ❌ | CmdPal 扩展是进程外 COM，只返回命令对象，不承载任意窗口 | `extensibility-overview` |

**一句话方案：不碰 EverythingToolbar 的 UI，只搬它的搜索内核；预览用「详情栏 + Peek」组合；拖拽用 `DataPackage`。**

---

## 2. 范围

### P0（必须）

- 顶层命令「Everything 搜索」，输入即搜（`DynamicListPage.UpdateSearchText`）
- 结果列表分页（首屏 50 条，`LoadMore` 继续取）
- 每个结果项：真实文件图标、拖拽（文件）、详情栏预览、右键动作
- 动作：打开、打开所在文件夹、复制路径、复制文件、属性、Peek 预览（`Ctrl+Space`）
- 空态/错误态：Everything 未运行、未索引、版本不支持、查询语法错误

### P1（应该）

- fallback：任意输入时出现一行「在 Everything 中搜索「xxx」」，点开进结果页
- 过滤器（全部 / 文件 / 文件夹 + 自定义 Everything 语法前缀）
- 排序（名称 / 路径 / 大小 / 修改时间）
- 设置页（实例名、每页条数、预览开关与大小、fallback 开关、拖拽附带文本）

### P2（可选）

- 1.4 IPC 兼容通道（跨机器/旧版本）
- 缩略图磁盘缓存
- 结果高亮（SDK3 提供 highlighted name/path）
- WinGet 上架 + Gallery 提交

### 明确不做

- 不内置 Everything 引擎（必须已安装并运行，Lite 版不支持）
- 不重写 EverythingToolbar 的 Deskband / WinUI 窗口
- M0 未验证前，不承诺「独立可缩放的预览窗口」

---

## 3. 架构

```
CmdPal (Microsoft.CmdPal.UI)
   │  WinRT / out-of-process COM（appxmanifest 声明）
   ▼
EverythingCmdPal.Ext（MSIX 包，net10.0-windows）
   ├── CommandsProvider      → 顶层命令 / fallback
   ├── ResultsPage           → DynamicListPage（防抖、分页、取消）
   ├── EverythingResultItem  → ListItem（Icon / DataPackage / Details）
   ├── PreviewService        → 图片 / 文本 / Markdown / 缩略图 + LRU 缓存
   └── Actions               → 打开 / 定位 / 复制 / 属性 / Peek
   │  项目引用
   ▼
EverythingCmdPal.Search（net8.0-windows，无 UI 依赖）
   ├── IEverythingClient / SearchQuery / SearchResult
   ├── Everything3Client     → Everything3.dll（1.5，命名管道，viewport 分页）
   ├── EverythingIpcClient   → WM_COPYDATA（1.4，P2）
   └── EverythingClientRouter→ 按版本选通道
   │  IPC
   ▼
Everything 1.5a / 1.4（必须运行）
```

设计原则：

1. **搜索层与 CmdPal SDK 完全隔离**：`EverythingCmdPal.Search` 不引用 `Microsoft.CommandPalette.Extensions`，方便单测和替换。
2. **UI 层只做适配**：把 `SearchResult` 映射成 `ListItem`，不写业务查询逻辑。
3. **预览全部异步 + 可取消**：选中项快速滚动时丢弃过期预览。

---

## 4. 项目结构

```
EverythingCmdPal/
├─ EverythingCmdPal.sln
├─ Directory.Build.props            # TFM / LangVersion / Nullable
├─ Directory.Packages.props         # 集中版本，锁 SDK 版本
├─ nuget.config
├─ PLAN.md                          # 本文件
├─ docs/
│  ├─ m0-findings.md                # M0 实测结论（拖拽目标、详情栏宽度）
│  └─ test-matrix.md                # 测试清单
├─ src/
│  ├─ EverythingCmdPal.Ext/
│  │  ├─ EverythingCmdPal.Ext.csproj
│  │  ├─ Package.appxmanifest       # com.microsoft.commandpalette 声明
│  │  ├─ app.manifest
│  │  ├─ Program.cs                 # 模板生成，勿改
│  │  ├─ EverythingExtension.cs     # IExtension
│  │  ├─ EverythingCommandsProvider.cs
│  │  ├─ Pages/
│  │  │  ├─ ResultsPage.cs
│  │  │  └─ SettingsPage.cs
│  │  ├─ Items/
│  │  │  └─ EverythingResultItem.cs
│  │  ├─ Actions/
│  │  │  ├─ OpenFileCommand.cs
│  │  │  ├─ RevealInFolderCommand.cs
│  │  │  ├─ CopyPathCommand.cs
│  │  │  ├─ CopyFileCommand.cs
│  │  │  ├─ ShowPropertiesCommand.cs
│  │  │  └─ PeekCommand.cs
│  │  ├─ Services/
│  │  │  ├─ PreviewService.cs
│  │  │  ├─ ThumbnailService.cs
│  │  │  └─ DataPackageFactory.cs
│  │  ├─ Assets/
│  │  └─ Public/
│  └─ EverythingCmdPal.Search/
│     ├─ EverythingCmdPal.Search.csproj
│     ├─ Abstractions/{IEverythingClient,SearchQuery,SearchResult}.cs
│     ├─ Everything3Client.cs
│     ├─ EverythingIpcClient.cs     # P2
│     ├─ EverythingClientRouter.cs
│     └─ NativeMethods.txt          # CsWin32
└─ native/
   └─ Everything3/{x64,arm64}/Everything3.dll
```

---

## 5. 版本与选型

| 项 | 选择 | 理由 |
|---|---|---|
| 扩展 SDK | `Microsoft.CommandPalette.Extensions` **0.11.260520004** | 与本机 CmdPal 0.11.11762（PowerToys 0.100.2）匹配；NuGet 已有 0.12，留给升级阶段 |
| TFM | `net10.0-windows10.0.26100.0` | 官方模板值；最低 19041 |
| 打包 | MSIX，`runFullTrust`，`windows.appExtension Name="com.microsoft.commandpalette"` | 扩展发现机制要求 |
| 发布 | Release 自包含（`SelfContained=true`） | 目标机器不一定装 .NET 10 |
| Everything 通道 | **SDK3 优先**（用户是 1.5.0.1408a），IPC 作为 P2 回落 | SDK3 有 viewport 分页和高亮，正好对上 `LoadMore` |
| 搜索代码 | vendor EverythingToolbar 的 `Core/Search` + `Platform/Search`（MIT） | 已有双通道、锁、过期查询处理；避免重写 |
| 预览 | 详情栏为主，Peek 补复杂格式 | 详情栏常驻、零操作；Peek 覆盖 PDF/Office/视频 |
| 拖拽 | `DataPackage` + `StorageItems`（异步 deferral） | 第一方 Indexer 已验证；扩展作者可用的官方能力 |

> ⚠️ 版本锁定：`Microsoft.CommandPalette.Extensions` 与 CmdPal 宿主版本强相关。0.100.2 用 0.11.x；升级 PowerToys 后再评估 0.12。

---

## 6. 关键实现

### 6.1 结果项（拖拽 + 预览的落点）

```csharp
internal sealed class EverythingResultItem : ListItem
{
    public EverythingResultItem(SearchResult r, PreviewService preview)
        : base(new OpenFileCommand(r.FullPath))
    {
        Title = r.Name;
        Subtitle = r.Directory;

        // 真实 shell 图标（含图片缩略图）
        Icon = new IconInfo(ShellItemIconProtocol.Create(r.FullPath));

        // ★ 拖拽：文件
        DataPackage = DataPackageFactory.ForPath(r.FullPath, includeText: Settings.DragIncludeText);

        // ★ 预览：详情栏（异步填充，先给缩略图占位）
        Details = preview.CreatePlaceholder(r);
        _ = preview.FillAsync(r, details => Details = details);

        MoreCommands =
        [
            new CommandContextItem(new PeekCommand(r.FullPath)) { RequestedShortcut = KeyChords.Peek },
            new CommandContextItem(new RevealInFolderCommand(r.FullPath)),
            new CommandContextItem(new CopyPathCommand(r.FullPath)),
            new CommandContextItem(new CopyFileCommand(r.FullPath)),
            new CommandContextItem(new ShowPropertiesCommand(r.FullPath)),
        ];
    }
}
```

### 6.2 拖拽数据包

```csharp
internal static class DataPackageFactory
{
    public static DataPackage ForPath(string path, bool includeText)
    {
        var package = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy,
            Properties = { Title = Path.GetFileName(path), Description = Path.GetDirectoryName(path) ?? "" },
        };

        // 昂贵操作：只有目标应用真的要文件时才解析
        package.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            var deferral = request.GetDeferral();
            try
            {
                if (File.Exists(path))
                    request.SetData(new[] { await StorageFile.GetFileFromPathAsync(path) });
                else if (Directory.Exists(path))
                    request.SetData(new[] { await StorageFolder.GetFolderFromPathAsync(path) });
            }
            catch { /* 拿不到就只给文本 */ }
            finally { deferral.Complete(); }
        });

        if (includeText)
            package.SetText(path);

        return package;
    }
}
```

注意：

- `RequestedOperation = Copy`，不要 Move。
- 目标应用是管理员权限时会被 UIPI 拦，这是系统限制，UI 里只提示不修。
- 默认**不**附带路径文本（避免拖进编辑器时变成粘贴路径而不是附件），作为设置项。

### 6.3 结果页（防抖 + 分页）

```csharp
internal sealed partial class ResultsPage : DynamicListPage, IDisposable
{
    private readonly Channel<string> _queries = Channel.CreateBounded<string>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

    public ResultsPage()
    {
        Id = "com.everything.cmdpal.results";
        Title = "Everything";
        Name = "搜索";
        PlaceholderText = "文件名、通配符或 Everything 语法（如 ext:psd dm:thisweek）";
        ShowDetails = true;          // ★ 打开右侧详情栏
        Icon = Icons.App;
        _ = ConsumeAsync();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
        => _queries.Writer.TryWrite(newSearch);   // 只保留最后一次

    public override IListItem[] GetItems() => [.. _items];

    public override void LoadMore()
    {
        // 取下一页（默认 50），追加；HasMoreItems = 还有更多；RaiseItemsChanged()
    }
}
```

消费循环串行访问搜索客户端（Everything SDK 全局状态不可并发），取到结果后检查是否有更新查询，有则丢弃。

### 6.4 预览策略

| 类型 | 详情栏 | 备注 |
|---|---|---|
| 图片（png/jpg/gif/webp/bmp/svg/avif/heic） | `ImageContent` | 720px 缩略图；0.99 起是可缩放查看器 |
| 纯文本/代码/日志/json/xml/csv | `PlainTextContent { WrapWords = true }` | 只读前 64KB；二进制嗅探后跳过 |
| Markdown | `MarkdownContent` | 渲染标题/列表/链接 |
| PDF / Office / 视频 / 压缩包 | `HeroImage`（shell 缩略图）+ 元数据 | 要看内容按 `Ctrl+Space` 走 Peek |
| 文件夹 | `HeroImage`（文件夹图标）+ 子项数量 | 可以后续加「浏览」页 |

```csharp
internal sealed partial class PreviewService
{
    public async Task<Details> BuildAsync(string path, CancellationToken ct)
    {
        var d = new Details { Size = ContentSize.Large, Title = Path.GetFileName(path) };
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ImageExtensions.Contains(ext))
        {
            var s = await _thumbs.GetAsync(path, 720, ct);      // 带 LRU 缓存
            d.Content = [new ImageContent(IconInfo.FromStream(s)) { MaxWidth = 720 }];
        }
        else if (ext == ".md")
        {
            d.Content = [new MarkdownContent(await ReadHeadAsync(path, 64 * 1024, ct))];
        }
        else if (TextExtensions.Contains(ext) && !await LooksBinaryAsync(path, ct))
        {
            d.Content = [new PlainTextContent(await ReadHeadAsync(path, 64 * 1024, ct)) { WrapWords = true }];
        }
        else
        {
            d.HeroImage = IconInfo.FromStream(await _thumbs.GetAsync(path, 512, ct));
            d.Metadata = BuildMetadata(path);
        }

        return d;
    }
}
```

要点：

- 缓存键：`(path, lastWriteTime, size)`，避免文件改动后显示旧图。
- 缓存上限：64 张 / 32MB，LRU。
- 预览流大小要克制（详情栏要跨进程传输）。
- 所有 IO 都要 catch `FileNotFoundException` / `UnauthorizedAccessException`。

### 6.5 Peek 命令

```csharp
public override CommandResult Invoke()
{
    var exe = PeekLocator.Find();   // %LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.Peek.UI.exe
                                    // 或 %ProgramFiles%\PowerToys\WinUI3Apps\...
    if (exe is null) return CommandResult.ShowToast("未找到 Peek，请启用 PowerToys 的 Peek 模块");
    Process.Start(new ProcessStartInfo(exe, $"\"{_path}\"") { UseShellExecute = false });
    return CommandResult.Dismiss();
}
```

### 6.6 搜索客户端（SDK3）

从 EverythingToolbar 搬运，保留 MIT 头，做三处改造：

1. `NLog` → `Microsoft.Extensions.Logging.Abstractions` 或空实现。
2. 去掉 `EverythingToolbar.snk` 强名称与 `net8.0-windows10.0.17763` 约束，改为本仓库 TFM。
3. `DllImport("Everything3.dll")` 保持不变；构建时把 `native/Everything3/{arch}/Everything3.dll` 复制到输出目录（官方 SDK 压缩包内是按架构命名的 DLL，需要重命名为 `Everything3.dll`，或在 csproj 里用 `Link` 改名）。

连接顺序（照抄官方与 EverythingToolbar 的约定）：

```csharp
client = Everything3_ConnectW(null);        // 默认实例
if (client == IntPtr.Zero)
    client = Everything3_ConnectW("1.5a");  // 1.5 alpha 实例
```

---

## 7. 里程碑

| 阶段 | 目标 | 产出 | 验收标准 | 预计 |
|---|---|---|---|---|
| **M0 证伪** | 证明「详情栏预览 + 拖拽」在你的机器上真的可用 | 模板工程 + 3 条硬编码文件项 | ① 拖到资源管理器/Outlook/VS Code/微信至少 3 个成功 ② 详情栏能显示图片且宽度可接受 ③ 拖拽过程中面板不消失 | 0.5–1 天 |
| **M1 搜索打通** | Everything 查询 + 分页 | `EverythingCmdPal.Search` + `ResultsPage` | 输入 3 字符出首屏 50 条；滚到底加载下一页；连打 10 字符不排队 | 2 天 |
| **M2 预览** | 图片/文本/Markdown/缩略图 + 缓存 | `PreviewService`、`ThumbnailService` | 选中图片 <150ms 出图；1MB 文本不卡；滚动 100 条无内存暴涨 | 2–3 天 |
| **M3 交互** | 拖拽 + 右键动作 + 空态 | `DataPackageFactory`、6 个 Action | 拖拽 4 类目标全部通过；Everything 关闭时有明确提示 | 1–2 天 |
| **M4 fallback 与设置** | 输入即搜 + 设置页 | `FallbackItem`、`SettingsPage` | fallback 可开关；设置改完即时生效 | 2 天 |
| **M5 打包** | MSIX + 安装/卸载 | 签名包、README | 干净机器安装可用；升级不丢设置 | 1 天 |

**总工期：约 9–12 个工作日**（不含等待上架审核）。

M0 是硬门槛：如果详情栏宽度不足以看预览，方案改为「详情栏只放缩略图+元数据，主预览走 Peek」；如果拖拽在 WinUI 弹窗里根本不工作，退路是保留 EverythingToolbar 作为拖拽源，CmdPal 只做检索+预览。

---

## 8. 测试矩阵

### 文件类型

图片：jpg / png / gif / webp / svg / heic
文本：txt / md / json / cs / log / csv / xml（含 UTF-8 无 BOM、GBK、二进制伪装）
文档：pdf / docx / xlsx / pptx
媒体：mp4 / mp3
其他：zip / exe / 文件夹

### 路径

中文路径、超长路径（>260）、UNC（`\\server\share`）、OneDrive 占位文件、已删除文件、无权限目录

### 拖拽目标

资源管理器、桌面、Outlook、微信/QQ、VS Code、浏览器上传框、管理员权限窗口（预期失败）

### 状态

Everything 未运行 / 索引未完成 / 实例名不对 / 1.4 版本 / 管理员运行 Everything / CmdPal 未启用

---

## 9. 风险登记

| 风险 | 影响 | 概率 | 对策 |
|---|---|---|---|
| 详情栏宽度/渲染不如预期 | 预览体验打折 | 中 | M0 实测；退化为「缩略图+元数据」，主预览走 Peek |
| 拖拽在部分目标失效 | 核心需求受损 | 中 | M0 覆盖 4 类目标；附带文本格式；文档标注 UIPI 限制 |
| SDK3 DLL 加载失败（位数/路径） | 搜不出结果 | 中 | 按架构分发；`NativeLibrary.Load` 全路径；加载失败回落 IPC |
| Everything 1.5 仍是 beta | API 变动 | 中 | 锁定 `Everything3.dll` 3.0.1.x；Router 双通道 |
| CmdPal SDK preview 变动 | 升级即坏 | 中 | 锁 0.11.260520004；SDK 只在 Ext 层引用 |
| 查询阻塞导致打字卡 | 体验差 | 高 | 单消费者 + Channel(DropOldest) + 取消 + 分页 |
| 缩略图开销大 | 滚动卡顿/内存涨 | 中 | LRU + 限并发（4）+ 720px 上限 |
| 许可污染 | 法律风险 | 低 | 只用 MIT 来源（EverythingToolbar / PowerToys / voidtools），不用 EPL 的 EverythingCommandPalette 代码 |
| 目标机器无 .NET 10 | 装不上 | 中 | Release 自包含发布 |

---

## 10. 环境准备（本机现状）

已具备：

- PowerToys **0.100.2**（CmdPal 0.11.11762）
- Peek：`%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.Peek.UI.exe` ✅
- Everything **1.5.0.1408a**（`C:\Program Files\Everything 1.5a`）运行中 ✅
- .NET 运行时 6/8/9/10 ✅

缺：

- .NET SDK（`dotnet --list-sdks` 为空）
- Visual Studio（部署 MSIX 开发包最省事）
- Windows 开发者模式（部署松散包需要）

安装（任选其一）：

```powershell
winget install Microsoft.DotNet.SDK.10
winget install Microsoft.VisualStudio.2022.Community
# VS 安装时勾选：.NET 桌面开发 + Windows 应用 SDK / WinUI
```

开发者模式：设置 → 系统 → 开发者选项 → 开发人员模式。

生成模板工程：打开 CmdPal → 输入 `Create a new extension` → 填名字/显示名/路径（比手抄模板可靠）。

调试循环：

1. VS 里 **Deploy**（不是 Build）
2. CmdPal 里执行 `Reload`（选 "Reload Command Palette extensions"）
3. 改代码 → 再 Deploy → 再 Reload

---

## 11. 依据与参考

- 扩展模型（进程外 COM / appxmanifest）：https://learn.microsoft.com/windows/powertoys/command-palette/extensibility-overview
- 创建扩展与项目结构：https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension
- 发布渠道（WinGet tag / Store / 自托管）：https://learn.microsoft.com/windows/powertoys/command-palette/publish-extension
- 模板工程（TFM、包版本、manifest）：`microsoft/PowerToys/src/modules/cmdpal/ExtensionTemplate`
- 动态列表与搜索写法：`.../ext/Microsoft.CmdPal.Ext.Indexer/Pages/IndexerPage.cs`
- 拖拽实现：`.../Helpers/DataPackageHelper.cs`、`.../Data/IndexerListItem.cs`（`DataPackage` 属性）
- 详情栏预览：`.../extensionsdk/.../Details.cs`、`DetailsPane.xaml.cs`、`DetailsViewModel.cs`
- 图片/文本内容类型：`ImageContent.cs`、`PlainTextContent.cs`、`MarkdownContent.cs`
- Peek 调用：`.../Commands/PeekFileCommand.cs`
- EverythingToolbar（MIT）：`srwi/EverythingToolbar` 的 `Core/Search`、`Platform/Search`、`LICENSE`
- Everything SDK3（MIT）：https://github.com/voidtools/everything_sdk3

---

## 12. 待确认 → 已定（2026-10-26）

四条都有官方一手依据，不再悬置；代码改动落在 1.3.3.3。

| # | 问题 | 结论 | 依据 |
|---|---|---|---|
| 1 | 预览主形态 | **详情栏优先**（`Details.HeroImage` + `Body` markdown），Peek 深链做补充 | 公开 [`IDetails`](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/microsoft-commandpalette-extensions/idetails) 至今只有 Body / HeroImage / Metadata / Title，**没有 `Content`**；`IDetails2` 未随 0.11 / 0.12 发布。这不是取舍，是唯一可行解 |
| 2 | fallback 默认开关 | **默认开**，代码不动 | 宿主 `MainListRanker`：fallback 参与正常排名，无匹配时 floor 到 `RankTier.FallbackFloor`，**永不被丢弃**，不会挤掉真实结果；宿主设置另有逐扩展 fallback 开关（`FallbackSettingsViewModel.IsEnabled` / `IncludeInGlobalResults`）。本扩展已有 `>` `$` `=` 前缀抑制 |
| 3 | 拖拽附带路径文本 | **不带**（维持现状），不加开关 | `DataPackage` 同时放 StorageItems + Text 会让聊天 / 编辑器类目标改走文本分支，文件从「附件」变成「一串路径」；「复制完整路径」动作已覆盖该需求 |
| 4 | 1.4 兼容通道 | **不做** | [官方 1.5 SDK 说明](https://voidtools.com/forum/viewtopic.php?t=15853)：v3 只连 1.5 命名管道，v2 才是 1.4。支持 1.4 = 第二套 P/Invoke + WM_COPYDATA 回复窗口 + 消息泵 + 双路测试；而 1.5 beta 已公开发布 |

### P2 四项处置

| 项 | 处置 |
|---|---|
| 缩略图磁盘缓存 | 已有（`PreviewService` 按 path+mtime+width 落 PNG）；1.3.3.3 补条数 / 体积上限与淘汰 |
| 结果高亮 | 暂缓。高亮只能经 `AddSearchPropertyRequestHighlighted` 走属性模式，而属性模式会让 `GetResultFullPathNameW` 全返回 0（SDK 3.0.0.9 实测）；且条目的 Title / Subtitle 是纯字符串，列表行不支持 markdown |
| 1.4 IPC 通道 | 不做（见上表第 4 条）；改为连不上时给明确提示（1.3.3.3） |
| WinGet 上架 + Gallery | 缓做。官方现行 WinGet 指南要求 **unpackaged EXE + Inno Setup**（`WindowsPackageType=None` + HKCU CLSID 注册），不是 MSIX；真要发布再切，顺带消掉自签名证书那一类坑 |
