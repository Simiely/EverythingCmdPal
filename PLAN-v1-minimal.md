# Everything for Command Palette — 极简版（v1）

> 回答一个问题：**不搬 EverythingToolbar 的代码，能不能做？**
> 能，而且更简单。下面的每个结论都有官方来源，末尾附证据。

---

## 1. 为什么可以不搬

EverythingToolbar 的价值是「双通道 + 一堆边界处理」。你的场景不需要：

| 它做的事 | 我们是否需要 | 替代 |
|---|---|---|
| 封装 1.4 的 WM_COPYDATA | ❌ | 你用的是 **1.5.0.1408a**，直接走 SDK3 |
| 封装 1.5 的命名管道 | ❌ | voidtools 官方 `Everything3.dll` 已经封好了 |
| 版本路由 / 实例名 | 部分 | 一行 `ConnectW(NULL)` → `ConnectW("1.5a")` |
| 查询锁、过期查询丢弃 | ✅ 需要 | 20 行 Channel(容量 1) 就够 |
| UI / 过滤器 / 缩略图 | ❌ | CmdPal 自己渲染，缩略图用 Toolkit |

官方 SDK3 是 **MIT**、单 DLL、C 接口，头文件里我们只需要 **13 个函数**。用它反而比搬第三方客户端更可靠——签名直接来自官方 `Everything3.h`。

---

## 2. 极简架构（一个项目）

```
CmdPal
  │ WinRT / 进程外 COM
  ▼
EverythingCmdPal（单个 csproj，约 550 行）
  ├─ EverythingCommandsProvider   顶层命令：Everything 搜索
  ├─ ResultsPage : DynamicListPage 防抖 + 分页
  ├─ ResultItem : ListItem         Icon / DataPackage / Details
  ├─ PreviewService                缩略图 + 图片/文本内容
  ├─ Actions                       打开 / 定位 / 复制路径 / Peek
  └─ Everything3.cs                P/Invoke（13 个函数）
  │
  ▼ Everything3_x64.dll（官方 SDK，随包分发）
Everything 1.5a（必须运行）
```

没有第二个项目、没有 NLog、没有 CsWin32、没有路由器、没有 C++。

---

## 3. 砍掉什么，代价是什么

| 砍掉 | 代价 | 什么时候补 |
|---|---|---|
| 1.4 兼容 | 只能用于 Everything 1.5+ | 需要时加 60 行 SDK2（`Everything64.dll`） |
| ARM64 | 只发 x64 | 加一个 DLL + 两个条件 |
| fallback（输入即搜） | 必须先选「Everything 搜索」再打字 | v2，约 40 行 |
| 设置页 | 页大小/预览上限写死常量 | v2 |
| 过滤器、排序 UI | 用 Everything 语法代替（`ext:psd dm:today`） | 一般不需要 |
| Markdown / 高亮 / 磁盘缓存 | 预览只有图片和纯文本 | v2 按需 |
| EverythingToolbar 的提权/UIPI 处理 | Everything 以管理员运行时可能连不上 | 提示用户以普通权限运行 |

**能力不打折的部分**：检索、分页、预览、拖拽、Peek、右键动作——这些是 CmdPal 侧的能力，跟用不用 toolbar 无关。

---

## 4. 依赖与版本（已核实）

| 项 | 值 | 来源 |
|---|---|---|
| SDK 包 | `Microsoft.CommandPalette.Extensions` **0.11.260520004** | PowerToys 模板；匹配本机 CmdPal 0.11.11762 |
| TFM | `net10.0-windows10.0.26100.0` | 官方模板 csproj |
| 原生库 | `Everything3_x64.dll`（503KB 压缩包内 `dll/` 目录） | 官方 SDK 3.0.0.9 压缩包实测 |
| 连接 | `Everything3_ConnectW(NULL)` → 失败再 `ConnectW("1.5a")` | 官方 README 示例 |
| 实例名 | 你的安装是 `Everything-1.5a.ini` → 实例 **1.5a** | 本机文件 + 官方 README |
| 许可证 | MIT（可随包分发） | voidtools/everything_sdk3 LICENSE |

> NuGet 上的 `Voidtools.Everything.Net`、`WpfAdmin.Everything`、`EverythingNet` 都封装的是 **SDK2（Everything64.dll）**，连默认实例，接不上 1.5a。所以不引现成包，自己写 13 个 P/Invoke。

---

## 5. 文件清单

```
EverythingCmdPal/
├─ EverythingCmdPal.csproj          # 模板生成后改 TFM + 拷 DLL
├─ Package.appxmanifest             # com.microsoft.commandpalette
├─ Program.cs                       # 模板生成，别动
├─ EverythingExtension.cs           # ~20 行，返回 Provider
├─ EverythingCommandsProvider.cs    # ~35 行，顶层命令
├─ Pages/ResultsPage.cs             # ~110 行，防抖 + 分页
├─ Items/ResultItem.cs              # ~60 行，拖拽 + 预览
├─ Services/PreviewService.cs       # ~80 行，缩略图 / 图片 / 文本
├─ Actions/FileActions.cs           # ~90 行，打开/定位/复制/Peek
├─ Interop/Everything3.cs           # ~150 行，P/Invoke + Query
└─ native/Everything3_x64.dll
```

---

## 6. 关键代码骨架

### 6.1 P/Invoke（签名取自官方头文件）

```csharp
internal static class Everything3
{
    private const string Dll = "Everything3.dll";

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    internal static extern IntPtr Everything3_ConnectW(string? instanceName);

    [DllImport(Dll)] internal static extern bool Everything3_DestroyClient(IntPtr client);
    [DllImport(Dll)] internal static extern IntPtr Everything3_CreateSearchState();
    [DllImport(Dll)] internal static extern bool Everything3_DestroySearchState(IntPtr state);

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    internal static extern bool Everything3_SetSearchTextW(IntPtr state, string text);

    [DllImport(Dll)] internal static extern bool Everything3_SetSearchViewportOffset(IntPtr state, nuint offset);
    [DllImport(Dll)] internal static extern bool Everything3_SetSearchViewportCount(IntPtr state, nuint count);
    [DllImport(Dll)] internal static extern bool Everything3_AddSearchPropertyRequest(IntPtr state, uint propertyId);

    [DllImport(Dll)] internal static extern IntPtr Everything3_Search(IntPtr client, IntPtr state);
    [DllImport(Dll)] internal static extern IntPtr Everything3_GetResults(IntPtr client, IntPtr state);
    [DllImport(Dll)] internal static extern bool Everything3_DestroyResultList(IntPtr list);

    [DllImport(Dll)] internal static extern nuint Everything3_GetResultListCount(IntPtr list);
    [DllImport(Dll)] internal static extern nuint Everything3_GetResultListViewportCount(IntPtr list);

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    internal static extern unsafe nuint Everything3_GetResultFullPathNameW(
        IntPtr list, nuint index, char* buffer, nuint bufferChars);

    [DllImport(Dll)] internal static extern ulong Everything3_GetResultSize(IntPtr list, nuint index);
    [DllImport(Dll)] internal static extern ulong Everything3_GetResultDateModified(IntPtr list, nuint index);
    [DllImport(Dll)] internal static extern bool Everything3_IsFolderResult(IntPtr list, nuint index);
}
```

部署时统一改名：构建后把 `Everything3_x64.dll` 复制成输出目录的 `Everything3.dll`（`DllImport` 名称固定）。

```xml
<Target Name="CopyEverythingSdk" AfterTargets="Build">
  <Copy SourceFiles="$(MSBuildProjectDirectory)\native\Everything3_x64.dll"
        DestinationFiles="$(OutDir)Everything3.dll" />
</Target>
```

### 6.2 查询与分页

```csharp
// 新查询：Search 一次；翻页：改 viewport 后 GetResults 复用结果集
internal sealed class EverythingClient : IDisposable
{
    private IntPtr _client;

    public bool Connect()
    {
        _client = Everything3.Everything3_ConnectW(null);
        if (_client == IntPtr.Zero) _client = Everything3.Everything3_ConnectW("1.5a");
        return _client != IntPtr.Zero;
    }

    public List<Result> Query(string text, int offset, int count, out int total)
    {
        var state = Everything3.Everything3_CreateSearchState();
        try
        {
            Everything3.Everything3_SetSearchTextW(state, text);
            Everything3.Everything3_SetSearchViewportOffset(state, (nuint)offset);
            Everything3.Everything3_SetSearchViewportCount(state, (nuint)count);
            Everything3.Everything3_AddSearchPropertyRequest(state, 2); // size
            Everything3.Everything3_AddSearchPropertyRequest(state, 5); // date modified

            var list = offset == 0
                ? Everything3.Everything3_Search(_client, state)
                : Everything3.Everything3_GetResults(_client, state);

            // 读 viewport 内条目 → List<Result>；total = GetResultListCount
            // 读完立刻 DestroyResultList
        }
        finally { Everything3.Everything3_DestroySearchState(state); }
    }
}
```

> 注意：SDK 内部是全局状态，**必须单线程串行调用**。用 `Channel<string>(容量 1, DropOldest)` 起一个消费者协程，天然实现防抖 + 丢弃过期查询。

### 6.3 结果项（拖拽 + 预览，各一行）

```csharp
var item = new ListItem(new OpenFileCommand(r.FullPath))
{
    Title = Path.GetFileName(r.FullPath),
    Subtitle = Path.GetDirectoryName(r.FullPath),
    Icon = new IconInfo(ShellItemIconProtocol.Create(r.FullPath)),
    DataPackage = DataPackageFactory.ForPath(r.FullPath),   // 拖拽
    Details = await PreviewService.BuildAsync(r.FullPath),  // 预览
};
```

`ShowDetails = true` 在 `ResultsPage` 构造函数里设一次。

### 6.4 拖拽（20 行）

```csharp
package.SetDataProvider(StandardDataFormats.StorageItems, async request =>
{
    var deferral = request.GetDeferral();
    try
    {
        if (File.Exists(path)) request.SetData(new[] { await StorageFile.GetFileFromPathAsync(path) });
        else request.SetData(new[] { await StorageFolder.GetFolderFromPathAsync(path) });
    }
    catch { }
    finally { deferral.Complete(); }
});
```

### 6.5 预览（一种类型 3 行）

```csharp
Details d = new() { Size = ContentSize.Large, Title = name };
if (isImage) d.Content = [new ImageContent(IconInfo.FromStream(await ThumbAsync(path, 720)))];
else if (isText) d.Content = [new PlainTextContent(await ReadHeadAsync(path, 64 * 1024)) { WrapWords = true }];
else d.HeroImage = IconInfo.FromStream(await ThumbAsync(path, 512));
```

### 6.6 Peek

```csharp
var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                       @"PowerToys\WinUI3Apps\PowerToys.Peek.UI.exe");
if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe, $"\"{path}\"") { UseShellExecute = false });
```

---

## 7. 工期

| 阶段 | 内容 | 验收 | 时间 |
|---|---|---|---|
| **M0** | 模板 + 3 条硬编码结果 | 拖拽到 Explorer/Outlook/VS Code 至少 3 个成功；详情栏能看图；拖拽时面板不消失 | 半天 |
| **M1** | P/Invoke + 查询 + 分页 | 输入 3 字符出 50 条；滚到底加载下一页 | 1 天 |
| **M2** | 拖拽 + 预览 + 动作 + Peek | 图片/文本预览正常；4 个动作可用 | 1 天 |
| **M3** | 打包 MSIX | 干净机器能装 | 半天 |

**合计约 3 天**（不含安装 .NET SDK / VS 的时间）。

---

## 8. 风险

| 风险 | 概率 | 处理 |
|---|---|---|
| Everything 未运行 / 版本 <1.5 | 中 | 空态提示「需要 Everything 1.5+ 并正在运行」 |
| Everything 以管理员运行 | 中 | 命名管道受 ACL 限制，提示以普通权限运行 |
| 详情栏宽度不够看图 | 中 | M0 实测；退化为缩略图 + Peek 看大图 |
| 拖到管理员窗口失败 | 高 | Windows UIPI 限制，文档说明 |
| SDK3 是 1.5 beta 的接口 | 低 | 锁定 `Everything3_x64.dll` 3.0.0.9 |
| CmdPal SDK preview 变动 | 低 | 锁 0.11 版本 |

---

## 9. 依据

- SDK3 只支持 1.5、1.5a 实例名、连接示例：`voidtools/everything_sdk3` README
- 函数签名 / 属性 ID：官方 `Everything3.h`（SDK 3.0.0.9，`dll/Everything3_x64.dll` 实测存在）
- 扩展模型 / 模板 / TFM：`microsoft/PowerToys` 的 `ExtensionTemplate`
- 动态列表写法：`Microsoft.CmdPal.Ext.Indexer/Pages/IndexerPage.cs`
- 拖拽：`CommandItem.DataPackage`、`DataPackageHelper.cs`、0.97 release notes
- 预览：`Details.cs`、`DetailsViewModel.cs`、`ImageContent.cs`、`PlainTextContent.cs`
- Peek：`PeekFileCommand.cs`
- 本机事实：PowerToys 0.100.2、Everything 1.5.0.1408a（`C:\Program Files\Everything 1.5a`、`Everything-1.5a.ini`）、Peek 已安装

---

## 10. 和完整版的关系

`PLAN.md` 是「能上架、兼容 1.4、有设置与 fallback」的完整版（9–12 天）。
本文件是 v1，先跑通核心体验（3 天）。两者共用同一套 CmdPal 侧设计（拖拽 / 预览 / 动作），差别只在搜索层的厚度。建议先做 v1，跑一周再决定要不要补 1.4 和设置页。
