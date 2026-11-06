# Command Palette「链接/文件收藏」扩展 — 调研与方案

> 调研日期：2026-11-05
> 本机基线：Windows 11 26100 · PowerToys **0.100.2.0** · Command Palette **0.11.11762.0**
> 参考仓库：`ref/EverythingCmdPal`（git clone，HEAD 见下）· `ref/PowerToys`（microsoft/PowerToys main，commit `0b1dd814`，稀疏检出）
> 当前阶段只做调研与设计，不写实现代码、不推送。

---

## 0. 一句话结论

可行，而且比 EverythingCmdPal 简单：**不需要原生 SDK、不需要全盘索引**。推荐用官方模板骨架（MSIX + 进程外 COM/WinRT），复用 EverythingCmdPal 的「无 VS 打包脚本 + 自签证书 + 递增版本 + 关→卸→装→启」流水线，存储用包内 `LocalState` 的单个 JSON，检索在内存里做，打开统一走 ShellExecute。

官方已经有一个同类内置扩展 **Bookmark**（`Microsoft.CmdPal.Ext.Bookmark`），它的表单、持久化、启动分类逻辑可以直接作为行为参照（它绑定在 PowerToys 仓库内构建，不能直接拷工程）。

---

## 1. 机制确认（以官方文档 / 官方模板为准）

### 1.1 先区分：旧 PowerToys Run 插件 ≠ 新 Command Palette 扩展

| 维度 | PowerToys Run 插件（旧） | Command Palette 扩展（新） |
|---|---|---|
| 运行模型 | 加载进 PowerToys Run 进程内 | **独立进程**，通过 WinRT API 与宿主通信 |
| 注册方式 | 插件目录 + Run 自己的插件机制 | `.appxmanifest` 声明 `windows.appExtension`（`com.microsoft.commandpalette`）+ 进程外 COM server |
| 分发 | 放插件目录 | MSIX（Store / WinGet / 自签旁加载） |
| 现状 | 官方称 Run 正在升级为 v2，指向 Command Palette | 官方主推 |

出处：
- 官方文档明确写扩展是「standalone .NET applications ... communicate with Command Palette through a WinRT API ... runs in its own process and registers ... through its app manifest」：https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- PowerToys Run 文档页提示「PowerToys Run is getting an upgrade to v2! Check out the Command Palette, an evolution of PowerToys Run.」：https://learn.microsoft.com/en-us/windows/powertoys/run

**结论：不要按 PowerToys Run 的插件目录机制做，按 CmdPal 的 MSIX + COM/WinRT 扩展做。**

### 1.2 项目模板

- 最快路径：打开 Command Palette → 运行 `Create a new extension` 命令 → 填 ExtensionName / Display Name / Output Path。官方文档：https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- 模板源码（官方仓库）：`ref/PowerToys/src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension/`
- 官方模板生成的结构（官方文档同款）：`Directory.Build.props`、`Directory.Packages.props`、`nuget.config`、`*.sln`、`app.manifest`、`Package.appxmanifest`、`Program.cs`、`<Name>.cs`、`<Name>CommandsProvider.cs`、`Assets/`、`Pages/`、`Properties/launchSettings.json`、`Properties/PublishProfiles/`。

模板自带 AI 说明（很实用）：`ref/PowerToys/src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension/.github/instructions/cmdpal-extension.instructions.md`。

### 1.3 清单字段与注册（三处 CLSID 必须一致）

`Package.appxmanifest` 必需两块（`ref/.../TemplateCmdPalExtension/Package.appxmanifest` L45-72）：

```xml
<com:Extension Category="windows.comServer">
  <com:ComServer>
    <com:ExeServer Executable="你的.exe" Arguments="-RegisterProcessAsComServer" DisplayName="...">
      <com:Class Id="<CLSID>" DisplayName="..." />
    </com:ExeServer>
  </com:ComServer>
</com:Extension>

<uap3:Extension Category="windows.appExtension">
  <uap3:AppExtension Name="com.microsoft.commandpalette"
                     Id="<你的唯一 Id>"
                     PublicFolder="Public"
                     DisplayName="..." Description="...">
    <uap3:Properties>
      <CmdPalProvider>
        <Activation><CreateInstance ClassId="<CLSID>" /></Activation>
        <SupportedInterfaces><Commands /></SupportedInterfaces>
      </CmdPalProvider>
    </uap3:Properties>
  </uap3:AppExtension>
</uap3:Extension>
```

- `Name` 必须严格是 `com.microsoft.commandpalette`（否则宿主发现不了）。
- CLSID 要同时出现在：C# 类 `[Guid]`、`com:Class Id`、`CreateInstance ClassId`。
- `SupportedInterfaces` 目前只支持 `Commands`。
- 还需要 `<rescap:Capability Name="runFullTrust" />`；模板还带 `internetClient`（我们不请求网络可去掉，待实现时确认是否影响宿主校验）。

出处：
- 官方：https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview（"Make sure this CLSID is unique and matches across all three locations in the manifest."）
- 模板说明 `cmdpal-extension.instructions.md` L85-93：CLSID 三处一致、两块注册缺一不可
- 实证：`ref/EverythingCmdPal/src/EverythingCmdPal/Package.appxmanifest` L45-71 与 `EverythingExtension.cs` L8 的 GUID `8F3C2B1A-7D4E-4A9B-9C6D-2E1F0A5B7C31` 一致

### 1.4 COM / 进程外扩展的启动流程

`Program.cs`（官方模板 `ref/.../TemplateCmdPalExtension/Program.cs` L14-42）：

1. 进程由 COM 激活，命令行带 `-RegisterProcessAsComServer`；
2. `Shmuelie.WinRTServer.ComServer` 起 WinRT server；
3. 注册 `IExtension` 实例（单例或按需新建，模板注释建议单例）；
4. 扩展实现 `IExtension.GetProvider(ProviderType.Commands)` 返回 `CommandProvider`；
5. 扩展 `Dispose()` 时 set 事件，主线程退出，server stop。

EverythingCmdPal 完全同构：`Program.cs` L14-27 → `EverythingExtension.cs` L19-28 → `EverythingCommandsProvider.cs` L37-40。

### 1.5 调试方式

官方文档/repo 实操路径：
- Visual Studio：`Build → Deploy <ExtensionName>`（**Deploy**，不是 Build）→ F5；然后在 CmdPal 里运行 `Reload`（副标题 `Reload Command Palette Extension`）强制重建扩展实例。https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- 官方模板 `launchSettings.json` 有 `(Package)` 和 `(Unpackaged)` 两个 profile；**必须用 `(Package)`**，`(Unpackaged)` 不会部署包。`cmdpal-extension.instructions.md` L334-353 同样强调。
- 无 VS 路径（EverythingCmdPal 已验证）：`dotnet build` → 拷 layout → `makeappx pack` → `signtool sign` → `Add-AppxPackage` → 重启/重载 CmdPal。见 `ref/EverythingCmdPal/tools/update.ps1` L29-107。
- 日志：CmdPal `%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\<版本>\Log_*.log`（`ref/EverythingCmdPal/docs/m0-findings.md` L41-43）；扩展内用 `ExtensionHost.LogMessage`（官方 Bookmark 的 `FileBookmarkDataSource.cs` L31）或 `HostLog`（EverythingCmdPal `Services/HostLog.cs`）。

### 1.6 安装 / 分发：MSIX 是唯一正式路径，没有真正的「免打包」

| 路径 | 可行性 | 关键条件 | 出处 |
|---|---|---|---|
| VS Deploy（MSIX） | 官方推荐开发路径 | VS + Windows SDK / MSIX 工具 | 官方 creating-an-extension |
| 自签 MSIX | 可行（EverythingCmdPal 已跑通） | 证书进 **LocalMachine\Root + TrustedPeople**；Publisher 与证书 Subject 一致 | `plugins/EverythingCmdPal/docs/development.md` L58-66；`tools/update.ps1` L48-71 |
| 松散注册 `Add-AppxPackage -Register` | 可行但需 **开发者模式** | 否则 `0x80073CFF` | `docs/m0-findings.md` L31-32；官方前置条件也要求 Developer mode |
| 真正 unpackaged（无包身份） | **不作为可用路径** | 清单发现机制依赖包身份；模板的 Unpackaged profile 明确不会部署 | 官方 creating-an-extension 的 Warning |
| Store / WinGet | 面向分发的正式渠道 | Store：Partner Center + MSIX；WinGet：manifest 带 `windows-commandpalette-extension` tag | https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension |

### 1.7 版本与 SDK（已核实）

| 项 | 值 | 依据 |
|---|---|---|
| 本机宿主 | PowerToys 0.100.2.0 / CmdPal 0.11.11762.0 | 本机 `Get-AppxPackage` 实测 |
| SDK 包 | `Microsoft.CommandPalette.Extensions` **0.11.260520004**（Toolkit 同包） | 官方模板 `Directory.Packages.props` L6；EverythingCmdPal `AGENTS.md` L10 |
| NuGet 实际可用版本 | 0.9.260303001 / **0.11.260520004** / 0.12.260812002 | NuGet flat container：`https://api.nuget.org/v3-flatcontainer/microsoft.commandpalette.extensions/index.json` |
| TFM | `net10.0-windows10.0.26100.0`，MinVersion `10.0.19041.0` | 官方模板 csproj L7-L10 |
| RID | `win-x64`（模板同时给 arm64） | 模板 csproj L11；EverythingCmdPal csproj L13 |
| 依赖 | CsWinRT 2.2.0、Shmuelie.WinRTServer 2.1.1、SDK.BuildTools（MSIX 工具） | 模板 `Directory.Packages.props` L6-L14 |
| 打包工具 | `Microsoft.Windows.SDK.BuildTools` 的 `makeappx.exe` / `signtool.exe`，无需装 Windows SDK | `tools/update.ps1` L34-46；`docs/m0-findings.md` L14-L21 |

> 待验证：0.12 SDK 需要哪个 CmdPal 宿主版本。EverythingCmdPal 实测 0.12 可在 0.100.2 上编译，但 `Details.Content` 仍不可用；我们锁定 0.11 以匹配已安装宿主。

---

## 2. EverythingCmdPal 实证：这类扩展的坑

| # | 坑 | 现象 / 根因 | 对策 | 出处 |
|---|---|---|---|---|
| 1 | 清单 `Language="x-generate"` | 手工 `makeappx` 报 `0x80073CF6`，提示语言无效；`x-generate` 是 VS 构建占位符 | 手写 manifest 改成具体语言（如 `zh-CN`） | `plugins/EverythingCmdPal/docs/development.md` L49-56；`Package.appxmanifest` L28 |
| 2 | 自签证书不受信 | 安装报 `0x800B0109`；导入 CurrentUser\Root 无效 | 管理员 `certutil -addstore -f Root` + `TrustedPeople` | `plugins/EverythingCmdPal/docs/development.md` L58-66；`m0-findings.md` L33-37 |
| 3 | 松散注册失败 | `Add-AppxPackage -Register` → `0x80073CFF` | 开开发者模式，或改走签名 MSIX | `m0-findings.md` L31-32 |
| 4 | 覆盖安装残留 | 同标识**同版本**但内容不同 → `0x80073CFB`，装不上；若用 `-ForceUpdateFromAnyVersion` 强推，会留下 staged 旧版本，CmdPal 里出现重复扩展条目 | **每次构建递增包版本号（`0.1.1.0 → 0.1.2.0`）→ 停宿主与扩展 → 卸载旧包（先快照 `LocalState`）→ `Add-AppxPackage` → 恢复快照**；**不要** `-ForceUpdateFromAnyVersion`、不在面板运行中安装 | 本仓库 `../../../docs/install.md` §4（实测）；`../../../plugins/EverythingCmdPal/tools/update.ps1` L75-L106；`plugins/EverythingCmdPal/docs/development.md` L65；`AGENTS.md` L18、L29 |
| 5 | 卸载后设置键残留 | 卸载包不等于清掉 CmdPal `settings.json` 里的扩展键 | 手动删键或按上条流程 | `plugins/EverythingCmdPal/docs/development.md` L67-75 |
| 6 | 缺 Windows SDK 编译失败 | CsWinRT 去注册表找 SDK 报错 | 把 NuGet 的 winmd 目录喂给 CsWinRT | `m0-findings.md` L14-L21；`csproj` L25-L26 |
| 7 | 打包后缺 DLL / 缺运行时 | 目标机无 .NET 运行时，或原生 DLL 未随包 | `SelfContained=true`；原生 DLL 作为 `Content` 拷进 layout；`PublishTrimmed=false` | `csproj` L16-L19、L38-L40；`docs/m0-findings.md` L14 |
| 8 | 裁剪/AOT 破坏 WinRT 反射 | Release 默认 `PublishTrimmed=true` / AOT | 先沿用 EverythingCmdPal 的保守配置；要开裁剪必须逐项验证 | 模板 csproj L61-L93 vs EverythingCmdPal csproj L17 |
| 9 | 图标/包资源缺失 | 包校验失败或列表图标空白 | Assets 必须全部生成并被 `<Content>` 打包；manifest 引用的 logo 必须存在 | `tools/gen_assets.py` L37-L45；`csproj` L38；`Package.appxmanifest` L39-L42 |
| 10 | `Public` 文件夹缺失 | `PublicFolder="Public"` 指向不存在的目录 | 仓库里放 `Public/.gitkeep` | `src/EverythingCmdPal/Public/.gitkeep` + manifest L57 |
| 11 | 设置页默认值不生效 | `ChoiceSetSetting` 3 参构造取 **第一个选项** 作为默认 | 默认项排第一，并处理旧值迁移 | `plugins/EverythingCmdPal/docs/development.md` L87-94；`SettingsManager.cs` L59-L64 |
| 12 | `DynamicListPage.SearchText` 自激循环 | setter 同步回调 `UpdateSearchText`；在回调里写它会无限重搜/闪屏 | 只读 `newSearch`，绝不在回调里写 `SearchText` | `DynamicListPage.cs` L9-L18；`plugins/EverythingCmdPal/docs/development.md` L123-132 |
| 13 | 切换页面搜索框清空 | 搜索框文本来自宿主 VM，扩展回填不可控 | 不为「保留输入」做复杂设计；用页面携带初始 query | `plugins/EverythingCmdPal/docs/development.md` L123-132、L143-150 |
| 14 | `ShowDetails` 运行期改不了 | 只在页面构造时读取 | 详情栏开关只能换页 | `plugins/EverythingCmdPal/docs/development.md` L134-141 |
| 15 | 页面来回切换返回栈变深 | 双向都 push | 一个方向 push，回程 `CommandResult.GoBack()` | `plugins/EverythingCmdPal/docs/development.md` L114-121 |
| 16 | `GetItems()` 里做重活 | 该方法被高频调用，IO/日志会卡列表 | 内存索引 + 只在数据变更时重建 | `cmdpal-extension.instructions.md` L352-353 |
| 17 | 启动慢 | 扩展进程首次启动 ~2s（.NET + WinRT 初始化） | 数据懒加载、文件小、启动不拉网络图标 | `m0-findings.md` L42-L43；Bookmark 懒加载 `BookmarksCommandProvider.cs` L88-L114 |
| 18 | 内置 Bookmark 的数据位置 | 它用宿主包身份的 `LocalState` | 自建扩展要用**自己包**的 LocalState | `BookmarksCommandProvider.cs` L37-42；`Utilities.cs` L15-L58 |

> 说明：第 1/9/10 条属于「手工打包 vs VS 打包」的差异，走官方 VS Deploy 时部分不会遇到；但只要采用 EverythingCmdPal 的无 VS 流水线就会遇到。

---

## 3. 方案

### 3.1 功能边界与 MVP

**条目类型（MVP）**
- URL（http/https）
- 文件
- 文件夹（与文件共用「路径」模型，成本极低）

**添加方式**
| 方式 | MVP | 说明 |
|---|---|---|
| 扩展内「添加条目」表单 | ✅ | `ContentPage` + `FormContent`（Adaptive Card），字段：目标、标题、别名、标签；自动识别 URL / 已存在路径 |
| 从剪贴板保存 | ❌ 已移除 | 用户明确不需要剪贴板功能；顶层命令曾实现后于 2026-11-05 删除 |
| 批量导入 | ⏳ P1 | 表单粘贴多行 `标题<TAB>目标<TAB>标签`，或 JSON；文件选择器为 P1 |
| 拖拽文件进面板 | ❓ 待验证 | 未在官方 SDK/文档找到「接收拖放」能力；不作为 MVP 承诺 |
| 全局热键直接弹「添加」 | ❓ 待验证 | 扩展侧未见注册全局热键 API；可先用 CmdPal 全局热键 + Dock 固定「添加条目」 |

**检索**
- 匹配字段：标题、别名、标签、域名/主机、完整路径、文件名
- 分词：空格 = AND；支持 `tag:foo`
- 排序：精确命中 > 前缀 > 词首 > 子串 > 子序列；同分按「使用次数 + 最近打开」frecency
- 空查询：显示最近使用 / 全部（按 frecency），避免空白页
- 搜索在内存完成，无 IO、无网络

**打开行为**
- URL → 默认浏览器；文件/文件夹 → 默认程序 / 资源管理器
- 统一 `ShellHelpers.OpenInShell(target)`（Toolkit 内部就是 ShellExecute；`OpenUrlCommand.cs` L20-24 同款）
- 失败 → Toast 并保持面板；文件不存在时标记「缺失」而不是删除
- 条目动作：打开、复制链接/路径、打开所在文件夹、编辑、删除

**设置（MVP 最小集）**
- 详情栏开关（构造时生效）
- 列表最多显示条数
- 默认排序（最近 / 最常用 / 名称）
- 数据文件路径只读展示 + 「打开数据目录」（便于备份）

### 3.2 存储设计

**位置**
- 主文件：`Utilities.BaseSettingsPath("CmdPalLinks")` 下的 `links.json`
  - 打包运行时 = `%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\links.json`
  - 未打包运行时 = `%LOCALAPPDATA%\CmdPalLinks\links.json`
  - 依据：`Utilities.cs` L15-58 的注释与实现
- 备份：同目录 `links.json.bak`（每次成功写入前轮换）+ 可选 `backups\links-YYYYMMDD.json`（保留最近 5 份）
- 导出：用户显式「导出 JSON」到用户选择的位置（P1）

**格式（版本化）**

```json
{
  "schemaVersion": 1,
  "items": [
    {
      "id": "guid",
      "kind": "url | file | folder",
      "title": "GitHub",
      "target": "https://github.com",
      "aliases": ["gh"],
      "tags": ["dev"],
      "createdAt": "2026-11-05T10:00:00Z",
      "updatedAt": "2026-11-05T10:00:00Z",
      "openCount": 0,
      "lastOpenedAt": null
    }
  ]
}
```

**写入与并发**
- 单进程单写者；所有变更先在内存改，再经「合并写」队列落盘（参照 Bookmark 的 `SupersedingAsyncGate`：`BookmarksManager.cs` L141-L166）
- 原子写：写 `links.json.tmp` → `File.Replace(tmp, links.json, links.json.bak)`；失败则保留旧文件并写宿主日志
- 读取：启动时一次读入；解析失败 → 尝试 `.bak` → 仍失败则空库 + Toast/日志（不覆盖坏文件）
- 去重键：`kind + normalizedTarget`
  - URL：scheme/host 小写、去默认端口、去 fragment、根路径统一、可选去常见跟踪参数（utm_* 等，P1）
  - 文件/文件夹：`Path.GetFullPath` + 统一分隔符 + Windows 不区分大小写比较
- 唯一 ID：GUID；别名允许重复但精确命中时加权
- 卸载风险：**已实测确认**——`Remove-AppxPackage` 会删除包内 `LocalState`（链接库数据全丢，需从备份回写）；安装流程因此改为「卸载前快照 `LocalState` → 安装 → 装后原样恢复」，并用 SHA256 校验；P1 仍保留导出，卸载/重置属于不可逆操作（见开放问题 1）

### 3.3 技术选型与项目结构

- 单项目、单 csproj；.NET 10；`net10.0-windows10.0.26100.0`；`win-x64`（后续再加 arm64）；`SelfContained=true`；`PublishTrimmed=false`；`PublishSingleFile=false`（先稳）
- 不引入 DI、不拆多工程（对齐 EverythingCmdPal 约定 `AGENTS.md` L31-32）
- UI 全部用 SDK 内置页型：`DynamicListPage`（列表/搜索）、`ContentPage` + `FormContent`（添加/编辑）、`ListItem/Details`（详情栏）
- 复用 EverythingCmdPal 的：`Program.cs` / `Extension` / `Provider` 骨架、`Icons.cs`、`HostLog.cs`、`SettingsManager` 写法、`tools/build.ps1` + `tools/update.ps1`（改成新包名/证书 Subject/CLSID）
- 不复用：`Interop/Everything3`、`SearchSession`、`PreviewService`、`EverythingClient`（与本任务无关）
- 参考官方 Bookmark 的行为（不拷工程）：表单 `AddBookmarkForm.cs` L23-124、启动失败 Toast `LaunchBookmarkCommand.cs` L64-80、懒加载 `BookmarksCommandProvider.cs` L88-114

建议结构：

```
src/CmdPalLinks/
  Program.cs
  CmdPalLinksExtension.cs          # [Guid] 与清单一致
  CmdPalLinksCommandsProvider.cs   # TopLevelCommands + Settings
  Pages/ItemsPage.cs               # DynamicListPage：搜索/排序/空态 + MoreCommands + 缺失标记
  Pages/AddItemPage.cs             # ContentPage + FormContent
  Pages/EditItemPage.cs            # ContentPage 包装编辑表单
  Pages/ItemFormTemplate.cs        # 添加/编辑共用的 Adaptive Card 模板
  Pages/AddItemForm.cs
  Pages/EditItemForm.cs
  Commands/OpenItemCommand.cs
  Commands/RevealInFolderCommand.cs
  Commands/CopyTargetCommand.cs
  Commands/DeleteItemCommand.cs
  Services/LibraryStore.cs         # 加载/原子保存/备份/去重/Update/Remove
  Services/SearchEngine.cs         # 内存匹配与打分（索引懒构建）
  Services/ItemHealth.cs           # 缺失文件判定
  Services/SettingsManager.cs      # JsonSettingsManager + showFullPath
  Services/IconCache.cs            # favicon 本地缓存 + Shell 图标（后台提取）
  Models/LinkItem.cs
  Icons.cs
  HostLog.cs
  Assets/ ...
  Public/.gitkeep
  Package.appxmanifest
```

### 3.4 分步实施（每步都有可验证产物）

| 阶段 | 内容 | 可验证产物 |
|---|---|---|
| **M0** 骨架 | 官方模板 → 改包名/CLSID/证书 Subject；3 条硬编码条目；打开 URL/文件 | CmdPal 列表出现扩展，3 条能打开；`Reload` 后仍正常 |
| **M1** 存储 + 添加 | `links.json` 加载/保存、表单添加、去重 | 重启 CmdPal 后条目还在；重复添加被拒；坏 JSON 不丢库 |
| **M2** 检索 + 管理 | 内存搜索/排序（M1 已落地）、编辑/删除/复制、缺失文件标记、设置页 | **代码完成，2026-11-05 仅 `dotnet build` 0/0，未实机验证（见 §9）** |
| **M3** 打包安装 | 自签 MSIX + `plugins/CmdPalLinks/tools/install.ps1`（递增版本→构建→打包签名→快照 `LocalState`→停宿主与扩展→卸载→安装→恢复快照→重开面板） | 干净机器/新用户可安装；无重复条目；升级后 `LocalState` 数据完整 |
| **M4** 增强（按需） | 批量导入/导出、Dock 固定、fallback、图标缓存、可选热键 | 每项独立验收；热键/拖拽若宿主不支持则明确不做 |

---

## 4. 风险

| 风险 | 影响 | 处理 |
|---|---|---|
| SDK 0.11 是 preview 语义、版本绑定宿主 | 换 PowerToys 版本可能不兼容 | 锁 0.11.260520004；升级前先在测试机跑 `Reload` 冒烟 |
| 全局热键 / 拖拽入面板能力不确定 | 影响「快速添加」体验 | M0 先验证；不支持则用 Dock 固定「添加条目」 |
| 卸载丢数据 | 用户收藏丢失（**已实测**：`Remove-AppxPackage` 删除包内 `LocalState`） | 安装脚本卸载前快照、装后恢复并校验 SHA256；`.bak` + 导出作为兜底；必要时改存储位置（开放问题 1） |
| 文件条目失效 | 打开失败 | 打开时检测 + 列表中标记「缺失」，不自动删 |
| 大规模条目（>1 万）搜索卡顿 | 输入延迟 | 内存索引 + 预计算小写字段；必要时增量索引；避免每项 IO |
| AOT/裁剪 | WinRT 类型丢失、启动崩溃 | 沿用 `PublishTrimmed=false`；若要开裁剪单独验证 |
| 重复安装残留 | 设置页出现多个扩展 | 固定用 `plugins/CmdPalLinks/tools/install.ps1`（关宿主→卸载→安装→重启）；不 `-ForceUpdateFromAnyVersion`、不在面板运行中安装 |

---

## 5. 开放问题（需要拍板，勿默认假设）

1. **数据放哪、卸载是否必须保留？**
   - A. 包内 `LocalState`（最省事，随卸载删除，靠导出）
   - B. 用户可见目录（如 `%USERPROFILE%\Documents\CmdPalLinks\links.json`），需先验证 packaged 进程写外部路径的行为与权限
   - C. LocalState + 自动镜像到用户目录
2. **「快速添加」的最低要求？** 只接受「打开 CmdPal → 添加命令」，还是必须有系统级全局热键（若必须，可能要额外常驻进程，超出普通扩展范围）？
3. **条目范围**：是否支持非 http(s) 协议（`mailto:`、`vscode://`、`steam://`）？是否支持文件夹？自定义协议会带来安全边界问题。
4. **是否需要多机同步 / 团队共享？** 若要 OneDrive/共享盘，需要处理多写者、冲突与文件锁，格式也要加 `updatedAt` 冲突策略。
5. **发布渠道与架构**：仅自用自签旁加载，还是准备 Store/WinGet？只发 x64 还是 x64+arm64？这会影响包身份、Publisher、签名与裁剪配置。

---

## 6. 来源清单

**官方文档**
- 扩展工作原理：https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview（更新 2026-04-17）
- 创建扩展：https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension（更新 2026-09-12）
- 发布/分发：https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension（更新 2026-06-05）
- 示例索引：https://learn.microsoft.com/en-us/windows/powertoys/command-palette/samples
- PowerToys Run（v2 说明）：https://learn.microsoft.com/en-us/windows/powertoys/run
- NuGet 版本列表：https://api.nuget.org/v3-flatcontainer/microsoft.commandpalette.extensions/index.json

**官方仓库（本地稀疏检出 `ref/PowerToys`）**
- 模板清单：`src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension/TemplateCmdPalExtension/Package.appxmanifest` L45-72
- 模板 csproj：`.../TemplateCmdPalExtension.csproj` L7-16、L61-93
- 模板依赖：`.../Directory.Packages.props` L6-16
- 模板 COM 入口：`.../Program.cs` L14-42
- 模板调试说明：`.../.github/instructions/cmdpal-extension.instructions.md` L334-353
- 设置技能：`.../.github/skills/add-extension-settings/SKILL.md`
- 官方 Bookmark 扩展：`src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Bookmark/`
  - 数据路径 `BookmarksCommandProvider.cs` L37-47
  - 原子/合并保存 `BookmarksManager.cs` L141-166
  - 表单 `Pages/AddBookmarkForm.cs` L23-124
  - 打开与失败 Toast `Commands/LaunchBookmarkCommand.cs` L64-80
  - 懒加载 `BookmarksCommandProvider.cs` L88-114
- Toolkit `Utilities.BaseSettingsPath`：`src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/Utilities.cs` L15-58
- `DynamicListPage.SearchText`：`.../DynamicListPage.cs` L9-18
- `OpenUrlCommand`：`.../Commands/OpenUrlCommand.cs` L20-24

**EverythingCmdPal（本地 clone `ref/EverythingCmdPal`）**
- `src/EverythingCmdPal/Package.appxmanifest` L45-71（COM + appExtension）
- `src/EverythingCmdPal/EverythingCmdPal.csproj` L9-19、L24-35、L38-40
- `src/EverythingCmdPal/Program.cs` L14-27
- `src/EverythingCmdPal/EverythingCommandsProvider.cs` L37-40
- `src/EverythingCmdPal/Services/SettingsManager.cs` L59-64、L89-113
- `tools/update.ps1` L29-107（构建、证书、卸载、打包、安装、启动）
- `tools/gen_assets.py` L37-45（包图标清单）
- `plugins/EverythingCmdPal/docs/development.md` L49-75、L87-94、L114-150
- `docs/m0-findings.md` L14-47
- `AGENTS.md` L8-23

---

## 7. M0 实机验证记录（2026-11-05）

### 7.1 产物与位置

- 项目：`src\CmdPalLinks\`
- 包：`...\package\CmdPalLinks.msix`（`Identity Name=CmdPalLinks`，`Version=0.1.0.0`）
- 布局：`...\package\layout\`
- 清单：`src\CmdPalLinks\Package.appxmanifest`（`Name="com.microsoft.commandpalette"`，CLSID `79B2B289-B5A9-4E88-AB21-B2509F22762B` 三处一致）
- 测试文件：`...\test-open-me.html`、`...\test-open-me.txt`

### 7.2 构建

```
dotnet build src\CmdPalLinks\CmdPalLinks.csproj -c Debug
    0 个警告
    0 个错误
```

SDK：`%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`（.NET 10，复用 EverythingCmdPal 已装好的本地 SDK）。

### 7.3 打包与安装

- `makeappx pack /d package\layout /p package\CmdPalLinks.msix /o`（BuildTools 10.0.26100.4188）
- `signtool sign /fd SHA256 /sha1 <thumbprint>`（复用本机已存在的自签证书 `CN=EverythingCmdPal Dev`）
- `Add-AppxPackage -Path package\CmdPalLinks.msix`
- 结果：`Get-AppxPackage CmdPalLinks` → `0.1.0.0`，`Status = Ok`

### 7.4 宿主发现与加载（日志原文）

```
Loaded 1 command(s) and 0 band(s) from CmdPalLinks_0.1.0.0_neutral__byt16r9dq2e2y in 28 ms
Loaded 3 command(s) and 0 band(s) from 2 extension(s) in 322 ms
```

日志路径：`%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\0.100.2.0\Log_2026-11-05.log`

### 7.5 打开行为

| 目标 | 扩展日志 | OS 侧证据 |
|---|---|---|
| `https://example.com` | `Open requested: https://example.com` / `Open result: True`（11:55:55） | 同一秒新建 msedge 进程（PID 34856） |
| `...\test-open-me.html` | `Open requested: ...\test-open-me.html` / `Open result: True`（11:55:28） | 同一秒新建 6 个 msedge 进程（PID 6832/7580/23404/23720/23740/25132） |

UI 驱动方式：`Win+Alt+Space` 唤出面板 → 聚焦窗口 → 剪贴板粘贴「链接库」→ Enter 进入扩展页 → 粘贴目标 → Enter。首次自动化时键盘事件写法错误（`Kbd::` 前漏 `[ ]`），导致 URL 条目未被点到；修正后重测通过，已如实记录。

### 7.6 系统设置

- **未要求用户做任何系统设置**：没有开启开发者模式，也没有新装证书。
- 原因：本机已存在受信任的自签证书 `CN=EverythingCmdPal Dev`（LocalMachine\Root + LocalMachine\TrustedPeople，且当前用户 `Cert:\CurrentUser\My` 有私钥），本次直接复用它签名。
- 风险提示：该证书的有效期与私钥归属属于既有环境；若要长期自用，建议后续生成独立的 `CN=CmdPalLinks Dev` 证书并手动导入一次 Root/TrustedPeople（届时需要用户操作）。

### 7.7 已知偏差 / 待办

- `.txt` 默认处理程序是 Windows Notepad AppX，但本机即使 `Start-Process test-open-me.txt` 也不出窗口；判定为环境问题，因此改用 `.html` 做可见验证（代码路径相同）。
- 条目仍是硬编码的两个测试项，无存储、无检索；属于 M1 范围。
- 安装流程（2026-11-06 已整改）：递增包版本 → 构建打包签名 → 停 CmdPal UI 与扩展宿主并等 2 秒 → 卸载旧包（先快照 `LocalState`）→ `Add-AppxPackage` → 恢复数据并校验 → 重开面板；不用 `-ForceUpdateFromAnyVersion`（详见 §7.8、`../../../docs/install.md`）。

### 7.8 安装方式整改：复刻 EverythingCmdPal 的「关→卸→装→启」（2026-11-06）

**结论**：`../../../plugins/EverythingCmdPal/tools/update.ps1` L75-L106 的顺序就是可用做法，我们照抄；唯一必要差异是卸载前快照 `LocalState`、装完原样恢复（`-PreserveApplicationData` 实测报 `0x80073CFA`，只对部署模式的包有效）。

**旧流程（本仓库）** `plugins/CmdPalLinks/tools/install.ps1`（重写前）：停面板与扩展 → 卸载旧包 → pack/sign → `Add-AppxPackage` → 启动；数据全靠不卸载来保住。一旦版本没递增（`0x80073CFB`）就必须卸载，数据随之丢失。

**EverythingCmdPal 的做法（仓库内证据）**：
- `tools\update.ps1` L75 停 `Microsoft.CmdPal.UI` 与扩展进程 → L76 等 2 秒 → L77-L81 `Remove-AppxPackage` → L100 `Add-AppxPackage -Path`（无 ForceUpdate）→ L106 重开面板。
- `tools\update.cmd` L4 一键入口；`README.md` L24-L25 明确「关 CmdPal → 卸载旧包 → 安装 → 重启，不会出现重复条目」。
- `AGENTS.md` L18「先卸载旧包再安装，否则 CmdPal 出现重复条目」；L29「更新必须走 update.cmd；不要用 `-ForceUpdateFromAnyVersion`」。
- `plugins/EverythingCmdPal/docs/development.md`「签名包安装报 0x800B0109」：`-ForceUpdateFromAnyVersion` 会残留 staged 旧版本 → 重复条目；「列表出现已卸载扩展的残留条目」：`settings.json` 不清理旧扩展键，清理脚本见 `plugins\EverythingCmdPal\tools\clean_cmdpal_settings.py` L13-L39。
- `tools\install.ps1` L11/L30 仍带 `-ForceUpdateFromAnyVersion`，与 `AGENTS.md` L29 相冲，属于旧独立安装路径，不照抄。
- 版本递增：`CHANGELOG.md` 逐版递增（L5 `1.3.4`、L15 `1.3.3.3`…），每次构建都换号，便于核对。

**残留的真实来源（三条，分开说）**：
1. **同版本、内容不同 → 装不上（`0x80073CFB`）**：旧流程被迫「卸载→重装」，这是数据丢失风险的起点。
2. **卸载删除包内 `LocalState`**：本次回退 PowerToys 时实测整个 `Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState` 消失；我们自己的包卸载同理（新脚本用快照恢复）。
3. **首页重复条目**：不是新旧包并存（`Get-AppxPackage` 只有 1 条），而是宿主会话内重复注册/渲染；历史环境由 SparseApp 启动异常触发的整轮重载放大（29 秒 7 次）。干净安装 + 完整重启后不再复现。

**新流程（已实测）**：
1. `src\CmdPalLinks\Package.appxmanifest` 的 `<Identity Version>` 递增（`0.1.1.0 → 0.1.2.0`）。只改 Identity 内属性；全局 `Version="…"` 正则会破坏 XML 声明与 `MinVersion`（`makeappx` 报 `0xC00CEE40`）。
2. `dotnet build` → 布局 → `makeappx pack` → `signtool sign`。
3. 快照 `LocalState` → 停 `Microsoft.CmdPal.UI`、`CmdPalLinks` → 等 2 秒并断言退出 → `Remove-AppxPackage` → `Add-AppxPackage` → 恢复快照。
4. 校验：8 个文件 SHA256、`items=7`、`ProviderSettings`/`commandProviderCache` 各 1 条 `CmdPalLinks`；随后重开 CmdPal。
5. **禁止**：面板运行中安装、`-ForceUpdateFromAnyVersion`、同版本覆盖、卸载后不备份数据、只重启扩展宿主不重启面板。

**实测结果（2026-11-06）**：`0.1.2.0` 安装 `Status: Ok`；`LocalState` 8 文件哈希全一致（`links.json` 3013 B / `18A06DC7…`、`links.json.bak` 3014 B、`settings.json` 108 B / `A370B098…`、`links-editor.html` 58001 B、`icons`×4）；宿主缓存与 `ProviderSettings` 各 1 条；UIA 首页 5 项中「链接库」1 项；日志每启动 SparseApp 异常 1 次、`CmdPalLinks` 启动/加载各 1 次，无重载循环。完整对照见 `../../../docs/install.md`。

**宿主重复条目的处置（2026-11-06）**：
- 曾用 `winget upgrade` 升到 `0.102.3001.0`（CmdPal `0.13.13001.0`）验证有效（静止 60 秒 + 6 轮交互，异常/加载各 1 次）。
- **用户否决该升级**（0.13 检索体验变差），已回退到 `0.100.2`；回退后用新流程重装扩展，首页仍只有 1 条「链接库」。
- 无效手段：给 SparseApp 写 `ProviderSettings.IsEnabled=false` 后仍启动、仍抛同一异常。
- 热键注记：合成 `Win+Alt+Space` 在前台为提权窗口时会被系统忽略；取证前先激活一个普通窗口。

---

## 8. M1 实机验证记录（2026-11-05）

### 8.1 产物与构建

- 新增：`src\CmdPalLinks\Models\LinkItem.cs`、`Services\LibraryStore.cs`、`Services\SearchEngine.cs`、`Pages\ItemsPage.cs`、`Pages\AddItemPage.cs`、`Pages\AddItemForm.cs`、`Commands\OpenItemCommand.cs`、`Icons.cs`
- 删除：`Pages\LinksPage.cs`、`Commands\OpenTargetCommand.cs`；`Commands\SaveClipboardCommand.cs`（M1.1，见 8.5）
- 修改：`CmdPalLinksCommandsProvider.cs`（最终 2 条顶层命令：链接库 / 添加条目；M1.1 起改为后台加载，见 8.6）
- `dotnet build`：0 警告 / 0 错误；重打包 → 签名 → `Add-AppxPackage` → 启动 CmdPal 全部成功。（注：当时的记录用的是「卸载后重装」旧流程，现已改为 §7.8 的「快照 → 关 → 卸 → 装 → 恢复」流程。）

### 8.2 功能验证（F1–F6）

| 编号 | 操作 | 证据（扩展日志 / 数据） | 结论 |
|---|---|---|---|
| F1 | ~~剪贴板保存~~ | **不再适用**：命令已于 M1.1 删除 | — |
| F2 | ~~重复剪贴板保存去重~~ | **不再适用**：去重逻辑保留在 `LibraryStore.Add`，由 D3 harness 覆盖 | — |
| F3 | 表单添加：URL `https://github.com`，标题 `GitHub`，别名 `gh`，标签 `dev`（UIA 直接 `ValuePattern.SetValue` 填表） | 保存成功；重启扩展进程后 `store loaded: 4 items ... (source=main)`，字段完整落盘 | PASS |
| F4 | ~~剪贴板添加文件/文件夹~~ | **不再适用**；M1.1 用表单补验 URL 条目（`Python Docs`） | — |
| F5 | 搜索 `gh` / `open-me` / `github.com` / `tag:dev` 后双击打开 | 日志 top 分别为 `GitHub` / `test-open-me` / `GitHub` / `GitHub`；`Open result: True`，同一秒新建 msedge 进程（34→37、39→40） | PASS |
| F6 | 清空查询 | `test-open-me \| GitHub \| test-folder \| example.com`（按 `lastOpenedAt` 倒序），`search q='' items=4 results=4` | PASS |

数据文件示例（`%LOCALAPPDATA%\Packages\CmdPalLinks_byt16r9dq2e2y\LocalState\links.json`，1791 字节）：

```json
{ "schemaVersion": 1, "updatedAt": "2026-11-05T04:33:44Z",
  "items": [ { "kind": "url", "title": "GitHub", "target": "https://github.com",
               "aliases": ["gh"], "tags": ["dev"], "openCount": 1,
               "lastOpenedAt": "2026-11-05T04:33:25Z" }, ... ] }
```

### 8.3 破坏性验证（D1–D4）

| 编号 | 场景 | 证据 | 结论 |
|---|---|---|---|
| D1 | 主文件截断到 63 字节，`.bak` 完好 | `store load main failed: JsonException ...`；`store loaded: 4 items ... (source=bak)` | PASS（回退 .bak） |
| D2 | 主文件与 `.bak` 同时损坏 | 两处 `JsonException`；`store loaded: 0 items ... (source=empty)`；损坏文件保留（13/14 字节）；随后新增条目走首次写入路径生成 468 字节合法文件 | PASS（不清空、不覆盖） |
| D3 | Harness 并发 / 损坏 / 去重 | `concurrency: added=400 duplicate=0 failed=0 reloaded=400 source=main elapsed=8.53ms`；`truncated main + valid bak: source=bak count=3`；`both corrupt: source=empty count=0`；`url: Added/Duplicate/Added`、`file: Added/Duplicate count=3` | PASS |
| D4 | 超长列表检索 | 见 8.4 计时表 | PASS |

D3 并发口径：20 个线程 × 20 次 `Add`（共 400 条不同 URL），无丢失、无残留 `.tmp`、重载 `source=main` 且 400 条完整，说明 `File.Replace` + 单写锁的原子切换有效。

### 8.4 计时（同一口径）

口径：扩展内 `Stopwatch`，日志行 `search q='...' items=N results=K took=X.XX ms` / `store loaded: N items in X ms (source=...)`；宿主日志 `Loaded N command(s) ... in X ms`。同机、同包、同一天，均为进程冷启动后首次操作（下表为 M1 优化前的数据）。

| 场景 | 条目数 | `store loaded` | `Loaded N command(s)` | 空查询 | `item` | `site42` | `tag:tag5` | 无命中 `zzzz` |
|---|---|---|---|---|---|---|---|---|
| M1 验收数据 | 4 | 30.31 ms | 86 ms | 3.31 ms（4 条全量） | — | — | — | 0.03 ms |
| D4 | 1000 | 34.77 ms | 90 ms | 3.70 ms | 5.25 ms | 1.01 ms | 0.19 ms | 0.56 ms |
| D4 | 5000 | 65.97 ms | 123 ms | 5.21 ms | 8.31 ms | 4.42 ms | 0.77 ms | 2.99 ms |

Harness 纯反序列化：1000 条 `first=14.41 ms / second=7.03 ms`；5000 条 `first=40.23 ms / second=33.84 ms`。

**与 M0 对比（如实写）**：
- M0（硬编码 1 条命令）`Loaded 1 command(s) ... in 28 ms`；M1 为 `Loaded 3 command(s) ... in 74–133 ms`（含 JSON 解析 + 索引构建）。**启动更慢约 46–105 ms，没有改善**——M0 本就没有检索，这一项不存在"改善"，只存在新增成本。
- 检索侧唯一可比的是"条目数增长"：4 → 5000 条时，查询耗时 0.03–3.31 ms → 0.77–8.31 ms，仍低于 10 ms 的目标线；`store loaded` 30.31 → 65.97 ms，主要来自 JSON 反序列化，未做增量/分片。
- 结论：M1 在 5000 条内可用；若未来上万条，需把解析移出 UI 线程或改增量存储——M1.1 已把解析移出宿主加载路径（见 8.6）。

### 8.5 M1.1：剪贴板移除与回归（2026-11-05）

- 删除 `Commands\SaveClipboardCommand.cs`、`Icons.Clipboard`、`ClipboardHelper` 引用与相关文档描述；`dotnet build` 0 警告 / 0 错误，重装成功。
- 顶层命令实测：`链接库` ✅、`添加条目` ✅、`保存剪贴板` ❌（UIA 精确查找不存在）。
- 回归：表单新增 `Python Docs`（`https://docs.python.org/3/`，别名 `py, docs`，标签 `dev`）→ `links.json` items 4→5，字段完整；重开面板查 `py` 命中 `Python Docs`；`gh` 命中 `GitHub`，双击 `Open result: True`（msedge 35→38）。
- 注：卸载重装会清空包内 `LocalState`，验证前从 `.testdata` 备份恢复；这是 MSIX 的预期行为，也正是后来安装脚本加入「卸载前快照、装后恢复并校验 SHA256」的原因（见 §7.8）。

### 8.6 M1.1：启动优化（如实对比）

改动：索引延迟构建（首次 `GetItems` 才 `BuildIndex`，新增 `index built: N items in X ms` 日志）+ 存储后台加载（`StartLoading()` → `Task.Run(Load)`，`Add/RecordOpen/Items` 前 `WaitForLoad()`）。

口径：同机同日、4 条数据（除 5000 条行）、每轮强杀 `CmdPalLinks` + `Microsoft.CmdPal.UI` 后冷启动，读宿主日志。

| 版本 | 顶层命令 | 加载方式 | `Loaded N command(s)`（3 次） | `store loaded` |
|---|---|---|---|---|
| M0 基线 | 1 | 无存储 | 28 ms | — |
| M1 | 3 | 同步 | 87 / 88 / 90 ms | 30.53 / 31.87 / 32.32 ms |
| M1.1 | 2 | 后台 | **37 / 38 / 41 / 45 ms** | 29–34 ms（异步，不再计入命令加载） |

- 结论：命令加载耗时从 87–90 ms 降到 37–45 ms（约 **-50 ms**），已接近 M0 的 28 ms；但**数据可用时间并未整体减少**——`store loaded` 只是从"加载命令时阻塞"挪到后台，4 条时约 30 ms、5000 条时 71.60 ms。
- 5000 条：`Loaded 2 command(s) in 40 ms`；`index built: 5000 items in 12.40 ms`；查询 `''` 6.35 / `item` 7.66 / `site42` 3.58 / `tag:tag5` 0.58 / `zzzz` 3.15 ms（与 M1 eager 索引的 5.21/8.31/4.42/0.77/2.99 同量级，懒索引未损害检索）。
- 首次检索会多付一次 `index built`（4 条时 3.77–5.03 ms，含 JIT；5000 条 12.40 ms）。若未来要求"命令加载"进一步贴近 28 ms，只能减少 WinRT 命令对象或彻底异步建页（未做）。

### 8.7 系统设置与人工步骤

- 全程未改动任何系统安全设置，未要求开启开发者模式，未新装证书（复用既有 `CN=EverythingCmdPal Dev`）。
- 自动化期间多次强杀 `CmdPalLinks` + `Microsoft.CmdPal.UI` 以模拟冷启动，均由脚本完成，无需用户手动重启宿主。

---

## 9. M2 代码实现（2026-11-05，未实机验证）

> 本轮用户约束：只写代码与文档，验证仅限 `dotnet build`（要求 0 警告 / 0 错误）+ 代码层静态自查；**不卸载/安装 MSIX、不启动 CmdPal、不跑 UIA、不做任何实机操作**。因此本节**不含任何实测数据**。

### 9.1 已实现

| 能力 | 实现位置 | 说明 |
|---|---|---|
| 打开所在文件夹 | `Commands/RevealInFolderCommand.cs` | 仅 file/folder；file → `explorer.exe /select,"<path>"`；folder → 打开目录本身；路径不存在 Toast + KeepOpen |
| 复制目标 | `Commands/CopyTargetCommand.cs` | `ClipboardHelper.SetText`，Toast「已复制」后 Dismiss |
| 编辑 | `Pages/EditItemPage.cs` + `EditItemForm.cs` + `ItemFormTemplate.cs` | 与添加共用同一 Adaptive Card 模板，编辑时 `Input.value` 预填；`LibraryStore.Update` 走同一套 `TryNormalize` 校验与去重 |
| 删除 | `Commands/DeleteItemCommand.cs` | 直接删除，`CommandContextItem.IsCritical=true` 仅样式；无二次确认 |
| 缺失标记 | `Services/ItemHealth.cs` + `ItemsPage.GetItems` | 每次刷新对结果集（≤50 条）判定 `File.Exists`/`Directory.Exists`；副标题 `⚠ 路径不存在 · <target>` + `缺失` 标签；不落盘、不自动删 |
| 设置页 | `Services/SettingsManager.cs` | `showFullPath` 单一开关，写入 `LocalState/settings.json`，变更即时刷新列表 |

构建结果（本轮唯一验证项）：`dotnet build -c Debug` → **0 个警告，0 个错误**（2026-11-05，耗时 0.92s）。

### 9.2 代码层静态自查（非实测）

- 编辑时把 `kind` 从 url 改为 file/folder：`Update` 复用 `TryNormalize`，`File.Exists`/`Directory.Exists` 不通过即返回 `InvalidTarget`，原条目字段不被写入（校验在赋值前）。
- 重名去重：`SameKey(e.Id != id ...)` 排除自身，改标题/别名不会误判为重复；同类型同 target 会被拒绝并返回已有条目。
- 空值：`title` 空 → `DefaultTitle`；`aliases/tags` 空 → `Clean()` 返回空数组；`payload` 解析失败 → Toast + `KeepOpen`。
- 删除最后一条：`Remove` 后 `Changed` 触发，`GetItems` 返回空数组并走 `EmptyContent`（`链接库为空`）。
- 路径不存在：`OpenItemCommand`/`RevealInFolderCommand` 各自有失败 Toast；`ItemHealth` 仅对 file/folder 判定，URL 恒 `false`。
- 删除写盘失败：`Remove` 捕获异常只记日志，内存已删、磁盘未删 → 重启复活（已知行为，见 9.4）。

### 9.3 待实机验证清单

与 `spec.md` §6.6 同一份编号：

| # | 项目 |
|---|---|
| F7/F8 | file / folder 的「打开所在文件夹」 |
| F9 | 「复制目标」剪贴板内容一致 |
| F10 | 编辑标题/别名/标签后列表与 `links.json` 一致 |
| F11 | 编辑时改类型 + 非法路径 → 提示且不修改 |
| F12 | 编辑指向已有 target → 「已存在」 |
| F13 | 删除最后一条 → 空态 |
| F14 | 源文件被删/改名 → `⚠ 路径不存在` + `缺失` 标签，条目保留 |
| F15 | 设置「显示完整路径」开关生效并持久化 |
| F16 | MoreCommands 数量与顺序（URL 3 / file·folder 4） |
| D5 | 检索进行中删除不崩溃 |
| D6 | 删除写盘失败 → 重启复活（记录并确认可接受性） |
| D7 | 断开的 UNC 路径下 `GetItems` 是否阻塞 |

### 9.4 待用户确认的设计点

1. 删除是否需要二次确认（当前直接删除，`IsCritical` 仅样式）。
2. 编辑是否继续复用同一表单模板（当前依赖 `Input.value` 预填，宿主是否渲染待实机确认）。
3. 缺失判定时机（当前每次刷新判定可见行；是否改启动时全量扫描 / 提供「清理缺失」）。
4. folder 的「打开所在文件夹」语义（当前打开目录本身，非父目录 + 选中）。
5. 设置页范围（当前仅 `showFullPath`）。
6. 删除写盘失败是否回滚内存并提示失败（当前不回滚）。

### 9.5 风险 / 不确定点

- `File.Exists` 对断开的 UNC 可能长时间阻塞；每页最多 50 次，未做超时或后台化。
- `Input.value` 预填、`ChoiceSet` 预选的宿主行为未验证（M1 表单曾依赖人工点击）。
- 缺失标签与用户自定义 tag 同名时会重复展示。
- 这些均**未**通过实机确认，不做结论性描述。

---

## 10. 列表呈现能力与图标（2026-11-05，仅代码 + 构建）

### 10.1 API 事实（均来自本地 `ref/PowerToys` 源码与已安装 NuGet 0.11.260520004）

| 结论 | 依据 |
|---|---|
| 列表行是「图标 + 标题 + 副标题」结构，另可带标签与更多命令 | `ICommandItem`：`Command / MoreCommands / Icon / Title / Subtitle`，`Microsoft.CommandPalette.Extensions.idl` L268-275；`IListItem` 追加 `Tags / Details / Section / TextToSuggest`，同文件 L287-293 |
| 行高、字号、图标尺寸不可由扩展控制；扩展只提供数据 | 同上接口无样式/尺寸成员；`ListItem` 仅属性（`Toolkit/ListItem.cs` L7-27） |
| 支持**两行式**（标题 + 副标题），且标题/副标题/图标同时存在 | `CommandItem.Title/Subtitle/Icon`（`Toolkit/CommandItem.cs` L28-44）；官方示例 `cmdpal-extension.instructions.md` L241-264 |
| 支持**详情栏**（列表右侧/下方详情页） | `IListPage.ShowDetails`（IDL L322）+ `ListItem.Details`（`Toolkit/ListItem.cs` L11）+ `Toolkit/Details.cs`、`DetailsElement.cs`；示例 L246-256 |
| 支持**网格/卡片式**（3 种宿主模板） | `IListPage.GridProperties`（IDL L324）；`ISmallGridLayout`、`IMediumGridLayout.ShowTitle`、`IGalleryGridLayout.ShowTitle/ShowSubtitle`（IDL L295-314）；实现 `Toolkit/SmallGridLayout.cs`、`MediumGridLayout.cs`、`GalleryGridLayout.cs`；示例 `SamplePagesExtension/Pages/SampleGridsListPage.cs` L14-44、`SampleGalleryListPage.cs` L12-58；`DynamicListPage` 也可用（`Pages/SampleIncrementalLoadingPage.cs` L23-29） |
| 卡片内容仍是「图标（图片）+ 标题 + 副标题」，**无法自定义卡片模板/样式** | 示例 `SampleGalleryListPage` 仅设置 `Title/Subtitle/Icon`（图片路径）；SDK 无自定义模板/XAML 接口 |
| 支持 Dock（任务栏/面板固定项），布局由宿主决定 | `Toolkit/Dock/WrappedDockItem.cs`、`WrappedDockList.cs`、`DockLabelWidth.cs`；扩展只能提供图标与标签，不能自定义卡片 |
| 图标来源：字体图标、Emoji、包内图片、**远程 URL/SVG**、`exe/dll,index`、流 | 官方说明 `cmdpal-extension.instructions.md` L290-308；`IconInfo`（L19-45）、`IconInfo.FromStream`（L41-45）、`IconHelpers.FromRelativePath`（L9-14）、`ThumbnailHelper.GetThumbnail`（L65-93） |
| **重要版本差异**：`ShellItemIconProtocol` 只存在于 ref 源码，**不在已安装 NuGet 0.11.260520004 的 DLL 中**（二进制扫描确认） | `extensionsdk/.../ShellItemIconProtocol.cs`（源码有）；包内 `Microsoft.CommandPalette.Extensions.Toolkit.dll` 无该类型——故本插件改用 `ThumbnailHelper` |

### 10.2 方案与成本排序

1. **图标 + 名称 + 副标题（已实现，成本最低）**：`ListItem.Icon` + `Title` + `Subtitle`；URL 直接给出 `Subtitle=target`，file/folder 也可显示路径；标签沿用 `Tags`。
2. **两行 + 标签（已具备，无需改动）**：`Tags` 已渲染；当前只在缺失时追加 `缺失`，可按需把自定义标签也展示。
3. **详情栏（低成本，未做）**：`ShowDetails = true` + 每行 `Details`（标题、Markdown 正文、元数据链接），可放完整 URL/路径与备注；不影响列表密度。
4. **卡片/网格（中等成本，未做）**：给 `ItemsPage` 设 `GridProperties = new MediumGridLayout { ShowTitle = true }` 或 `GalleryGridLayout { ShowTitle=true, ShowSubtitle=true }`，其余不变；**视觉效果由宿主决定**，扩展无法做自定义卡片，也无法保证 favicon/Shell 图标在网格中按期望尺寸渲染（待实机）。
5. **自定义视觉（做不到）**：SDK 不提供行模板、自定义 XAML 或任意卡片渲染；不能承诺“像商店卡片那样”。

### 10.3 本次实现（M2.1）

- `Services/IconCache.cs`：`Get()` 同步、无网络、无 Shell 调用 → 命中缓存直接返回，未命中返回通用图标并后台补齐，补齐后 500ms 合并 `Changed` → 列表刷新。
  - URL：`LocalState/icons/<sha256(host)[:32]>.{ico,png,jpg,gif,webp}`；`https://host/favicon.ico` → 失败再试 `http`；6s 超时、3 并发、≤512KB、魔数校验；30 天过期后台刷新、90 天清理；离线无缓存回落通用图标。
  - file/folder：`ThumbnailHelper.GetThumbnail(path)` 后台提取（Shell 图标/图片缩略图），`IconInfo.FromStream` 结果存内存；缺失/失败回落通用图标。
- `ItemsPage` 接入 `IconCache`；`Package.appxmanifest` 增加 `internetClient`。
- 构建：`dotnet build -c Debug` → **0 警告 / 0 错误**。

### 10.4 待实机验证（编号与 spec §6.6 一致）

F17 favicon 首次下载并缓存、F18 离线/404 回落、F19 Shell 图标、F20 图标补齐刷新无抖动且不影响启动耗时、D8 带 `internetClient` 后装包正常。

### 10.5 不确定点

- 宿主是否渲染 favicon 的 `.ico` 缓存文件（内容嗅探 vs 扩展名），未验证。
- 宿主是否接受远程 URL 图标（文档有写，但本插件选择本地缓存路线）。
- `ThumbnailHelper` 在扩展进程内的线程模型（Shell COM/MTA）是否稳定，未验证。
- 网格模式下图标尺寸与裁剪由宿主决定，未验证。

### 10.6 自绘图标与 emoji 修复（2026-11-06）

- **为什么不用宿主字形**：内置图标（`Icons.*`）用 `new IconInfo("\uE71B")` 字形，工作正常；但自定义图标需要色块底 + 透明底 +
  单色 emoji 组合，宿主字形无法表达底色，因此仍走 `CustomIconRenderer` 生成 light/dark PNG，再由
  `new IconInfo(new IconData(lightPath), new IconData(darkPath))` 交给宿主。
- **emoji 乱码根因**：`CustomIconRenderer` 的 `CreateFontW` / `DrawTextW` P/Invoke 未声明 `CharSet`（默认 Ansi），
  C# `string` 先被转换成 ANSI（本机 ACP=936）再传给 `...W` 函数按 UTF-16 读取。emoji（代理对）与中文都被转成 `?`
  （例如 `📁` → `0x003F 0x003F`），`DrawTextW` 再把它们当成 `U+3F3F` 等汉字绘制，于是图标显示为乱码字形。
  这也是中文标题首字色块显示异常的原因。修复：两个 P/Invoke 都加 `CharSet = CharSet.Unicode`；
  自绘缓存 key 增加 `r2|` 前缀，避免复用历史错误 PNG。
- **彩色 emoji 限制与降级（0.1.3.0）**：0.1.3.0 的自绘走 GDI/GDI+，只能得到 emoji 的单色字形轮廓
  （COLR 彩色字形不参与，实测 `📁` / `🛠️` / `🚀` 均为单色可辨轮廓）。降级：字形无墨迹（不支持的组合）→ 首字；
  `block` / 有底色 → 底色块 + 白字。彩色字形方案见 §10.8。
- **CSV / 编辑器字段**：`links.json` 的 `iconKind` / `iconColor` / `iconEmoji` ↔ CSV 第 8 列 `icon` ↔ 编辑器行模型。
  覆盖规则：**显式 `iconKind` 优先**；写入 `auto` / `transparent` 时必须同时清空 `iconEmoji` 与 `iconColor`，
  CSV 同一列同理；`block` + 空底色不再降级成 `auto`。
  反例（0.1.4.0 修过的坑）：旧逻辑在 `iconKind=auto` 但 `iconEmoji` 非空时把 kind 反写成 `emoji`，
  用户点「切回默认图标 → 应用」后 emoji 还在，表现为"设置不生效"。

### 10.7 编辑器闭环（2026-11-06）

- `EditorServer`：`TcpListener(IPAddress.Loopback, 0)` 随机端口，32 字节随机 token（Base64Url），
  请求校验 `Host` 头 + `X-CmdPalLinks-Token`（定长比较）；`GET /` 返回同源编辑器 HTML，`GET/POST /api/links` 读写；
  5MB 请求体上限、15s 请求超时、20 分钟空闲超时，扩展 `Dispose` 时关闭；异常只记 method/path/status，不记端口与 token。
- 保存复用 `LibraryStore.ReplaceAll`（`lock (_gate)` + `SaveLocked` 原子写 + `.bak`），成功触发 `Changed` 让列表失效重读。
- **切回默认图标**：显式 `iconKind`（`auto` / `block` / `transparent` / `emoji`）是权威值，
  `ReplaceAll` 与 CSV 的 `icon` 列在 `auto` / `transparent` 时必须清空残留 `iconEmoji` / `iconColor`；
  编辑器两卡各带「应用」按钮，点「默认图标 → 应用」会同时清空行内 emoji。

### 10.8 彩色 emoji（0.1.4.0）

背景：0.1.4.0 尝试过手写 D2D/DWrite 的裸 P/Invoke 探针，两次在用户机器上以 `0xc0000005` 崩溃并弹出系统错误框
（第一次 `CreateBitmap` 用错 vtable 槽位把宽度当指针解引用；第二次修正 `+3` 后 `CreateSolidColorBrush` / `DrawText`
槽位仍对不上）。裸 COM 的访问冲突无法被 `try/catch` 捕获，会直接带走扩展进程，因此改为下面的路线。

- **渲染栈**：`Vortice.Direct2D1 3.8.3`（SharpGen 生成的成熟 COM 封装，不自己数 vtable，也没有手写结构体布局），
  用 `DWriteCreateFactory` + `CreateTextFormat("Segoe UI Emoji")` + `CreateWicBitmapRenderTarget`，
  `DrawTextLayout(..., DrawTextOptions.EnableColorFont | NoSnap)` 取 COLR/CPAL 彩色字形，`IWICBitmap.CopyPixels` 读回像素。
- **进程隔离**：真正渲染发生在 `CmdPalLinks.exe --render-emoji <base64emoji> <color|-> <0|1> <out.png>` 子进程里，
  宿主只通过 `EmojiRenderClient` 拉起它并读取 64px PNG（`IconInfo(IconData(path), IconData(path))`）。
  子进程首行 `SetErrorMode(FAILCRITICALERRORS|NOGPFAULTERRORBOX|NOOPENFILEERRORBOX)`，不弹任何窗口/错误框；
  所有异常只写 `<out>.err` 并用退出码表达。最坏情况是子进程退出，CmdPal 扩展进程不受影响。
- **宿主不阻塞**：emoji 条目先同步走 GDI 单色轮廓（列表立刻有图标），彩色 PNG 由后台子进程补齐，
  成功写 `icons/color-<stem>.png` 并 `NotifyChanged` 让列表刷新；缓存 key 前缀 `r3|`，旧 `r2|` 产物不再命中。
- **降级判定（实际表现）**：
  1. 子进程渲染完成后统计不透明像素中 RGB 通道最大差 ≥ 24 的数量，少于 8 个即判定「没有彩色字形」
     （字体缺 COLR/CPAL 或回退到非彩色字体时整条 emoji 只有一种颜色，spread=0）→ 返回 `null`；
  2. 子进程超时（6s）、非 0 退出、PNG 无效或文件缺失 → 返回 `null`；
  3. 上述任一情况都保持 GDI 单色轮廓（`CustomIconRenderer`），`block`/有底色时仍是底色块 + 白色单色字形，
     不支持的组合退回首字；同一 key 失败后不再反复拉起子进程。
- **实测**：`🚀` 透明底输出 64×64、620 种颜色、RGB 最大差 215；`📁` + `#2563EB` 底色块角内像素
  `(37,99,235)`、中心 `(255,216,87)`，证明底色与彩色字形合成正确。宿主/环境无彩色字形时只能单色，判定条件同上。
- **探针污染的教训**：`emoji-color-probe.exe` / `d2d-slot-probe.exe` 曾在用户机器上弹
  `Exception Processing Message 0xc0000005 - Unexpected parameters`。此后规则写死：本机原生渲染探针只允许
  **手动触发**、结果**写文件**（`.png` / `.err` / 退出码），进程先 `SetErrorMode`，绝不弹窗；
  任何可能弹系统错误框的 exe 都不留在仓库与用户机器上。生产路径同理：渲染放子进程，宿主只读 PNG。

### 10.9 顶层命令登记时序与首页重复条目（0.1.5.0）

宿主 0.11.11762 的登记顺序（`TopLevelCommandManager.cs` / `CommandProviderWrapper.cs`）：

1. `TryLoadCommandsAsync` → `LoadTopLevelCommandsFromProvider` → 扩展 `TopLevelCommands()`（**先取数组**）；
2. 所有 provider 的加载任务完成后，`RegisterAndLoadCommandsAsync` 才把数组里的项 `TopLevelCommands.Add()`（**后 Add**）。

`CommandsChanged`（扩展 `RaiseItemsChanged()`）走 `UpdateCommandsForProvider`：按 `CommandProviderId`
`RemoveAll` + `InsertRange`，本身不会重复。但 0.1.4.0 在启动加载完成时就 `RaiseItemsChanged()`，
此时第 2 步还没执行、顶层列表是空的 → `UpdateCommandsForProvider` 直接插入一份**新建**的 `TopLevelViewModel`；
随后第 2 步又 append 掉登记时那份 → 同一 provider 两行「链接库」。日志特征就是同一会话出现两次
`provider: TopLevelCommands called, loaded=False` → `loaded=True`，而 `Loaded 1 command(s) from CmdPalLinks` 只有一次。

连带约束：`TopLevelViewModel.Id` 是 `CommandProviderId + DisplayTitle + Title + Subtitle` 的 WyHash64
（`GenerateId()` 只在 `InitializeProperties` 与 `Command`/`IsInitialized` 变化时重算，之后改副标题不重算）。
因此：①副标题必须在首次 `TopLevelCommands()` 返回前写定（否则登记后写副标题会换 Id）；
②编辑条目触发的 provider 级 `RaiseItemsChanged` 会重建顶层项，`Id` 随 `共 N 条` 变化，
已绑定的 `CommandHotkeys`（`…!CmdPalLinks<hash>`）随之失效。

0.1.5.0 的做法：首次 `TopLevelCommands()` 内同步 `_store.WaitForLoad()` 并写定副标题；
启动路径与 `_store.Changed` 都不再 `RaiseItemsChanged`（副标题靠 `INotifyPropChanged` 刷新）。
实测：首页 ListItem 6 项（分组头 1 + 命令 5，含 2 条「链接库」）→ 5 项（分组头 1 + 命令 4，1 条），
`Win + Ctrl + Z` 仍在。

---

## 11. M2.2：首页简化与网格实机测试（2026-11-05 已执行）

### 11.1 首页简化

- 顶层命令由 2 条降为 **1 条**「链接库」；「添加条目」移到页内：
  - 空态主按钮：`添加第一个条目 / URL / 文件 / 文件夹`
  - 每行 `MoreCommands` 首项「添加条目」
- 实机日志 4 次启动：`Loaded 1 command(s) and 0 band(s) from CmdPalLinks ... in 30/33/37/40 ms`。
- 搜索结果截图 `.testdata/search2.png`：插件仅「链接库 共 5 条」一项。

### 11.2 图标实机

| 项 | 结果 | 证据 |
|---|---|---|
| favicon 下载/缓存 | 通过 | `github.com .ico 6518 bytes 741 ms`、`docs.python.org .ico 15086 bytes 771 ms`；`LocalState/icons/` 落盘 2 文件；截图可见两个站点图标 |
| 无 favicon 回落 | 通过 | `example.com ... fallback=generic`，显示通用链接图标 |
| Shell 图标 | **失败** | `shell icon unavailable: test-open-me.html / test-folder`，`ThumbnailHelper.GetThumbnail` 返回 null，实际为通用字形 |

### 11.3 网格布局实测

临时开关：`LocalState\grid-mode.txt`（内容 `small|medium|gallery`，构造函数读取，缺省即列表）。

| 布局 | UIA 尺寸 | 截图 | 结论 |
|---|---|---|---|
| Small | 44×44，间距 56 | `.testdata/grid-small.png` | 只有图标、无标题 → 不可用 |
| Medium | 100×100，间距 112，7 列 | `.testdata/grid-medium.png` | 图标+标题正常可读 |
| Gallery | 168×214 ~ 213×230 | `.testdata/grid-gallery.png` | 图标放大且裁切、尺寸不一致、副标题截断 → 不可用 |

30 条（Medium）：
- `index built: 30 items in 3.10 ms`；首屏 `search q='' items=30 results=30 missing=0 took=65.08 ms`
- 5 行（Y=534/646/758/870/∞），第 4 行被底部命令栏裁切
- 滚轮 6 格、PageDown 无位移；方向键 10 次导致面板关闭 → **滚动未测出**
- 帧时间：**未测出**（未插桩）

### 11.4 结论与保留

- 默认保持**列表布局**；网格不暴露为用户设置。
- `grid-mode.txt` 开关保留（仅测试用，不写文档给最终用户）。
- 已知缺陷：file/folder Shell 图标不可用（T27）。

## 12. 待用户自测（最终构建未复测）

> 下列均未在 2026-11-05 收尾构建上执行，仅给出预期与判定方式；操作步骤见 `self-test.md`。

| 编号 | 验证点 | 预期 | 判定方式 |
|---|---|---|---|
| V1 | file/folder Shell 图标 | 真实文件/文件夹图标（非通用字形） | 列表截图 + 日志 `shell icon loaded source=jumbo/large/small` |
| V2 | 绝对路径存在 | 显示对应图标 | 同上 |
| V3 | 绝对路径不存在 | 通用字形 + `⚠ 路径不存在` 副标题 + 「缺失」标签；UI 不卡 | 日志 `shell icon skipped reason=missing` |
| V4 | UNC 路径不可达 | 先通用字形、不阻塞；后台约 2s 失败 | 日志 `shell icon skipped reason=unc offline`/`unavailable` |
| V5 | 快捷方式 `.lnk` | 显示目标类图标（Shell 决定） | 截图 |
| V6 | `.html/.txt/.png` | 各自扩展名关联图标 | 截图 |
| V7 | 图标缓存与失效 | 同一路径第二次打开不再提取；改文件时间后重取 | 日志第二次无 `shell icon loaded`；改动后出现 `shell icon invalidated` + 重新 loaded |
| V8 | 网格切换即时生效 | 设置切「网格（中）」保存后列表立即变卡片 | 截图对比 |
| V9 | 网格第 4 行裁切 | 滚到底后可见最后一行（属视口裁剪，非布局错） | 滚到底截图 |
| V10 | 滚轮 / PageDown | 焦点在列表上时滚动百分比变化 | UIA `ScrollPattern.VerticalScrollPercent` + 截图 |
| V11 | 方向键 | 面板不被关闭、选中项移动 | 连续按 ↓ 后窗口仍在前台 |
| V12 | 30/100/500 条滚动 | 不崩、`LoadMore` 追加（首次 100 条，之后每次 +100） | 日志 `load more: visible=… total=…` |
| V13 | 帧时间 | 宿主不暴露帧计时；可看 `build=… ms` 与 UIA 滚动延迟 | 日志 `search … build=… ms` |
| V14 | 设置落盘 | 改任一设置 → `settings.json` 更新；重启扩展后 `settings loaded:` 值一致 | 文件内容 + 日志 |
| V15 | 关闭「显示标签/副标题/完整路径」 | 对应元素立即消失；缺失警告仍显示 | 截图 |
| V16 | 未知 layout 值 | 回落列表（`IgnoreUnknownValue`） | 手改 `settings.json` 为 `grid-small` 后重启 → 列表 |
| V17 | 新图标资产 | 安装后设置页「扩展 → 链接库」与列表项图标为新链环图 | 截图（不声称已验证外观） |

剩余未覆盖的历史项：F7–F16、D5–D7（见 spec §6.6），F20 帧时间。

---

## 13. M2.3 收尾实现（2026-11-05，仅代码 + 构建）

### 13.1 能力边界证据（为何设置页没有「每行列数/图标尺寸」）

- `ref/PowerToys/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl`
  - **L297**：`interface IGridProperties requires INotifyPropChanged { }` → 空接口，**没有任何属性**。
  - **L304-307**：`IMediumGridLayout` 只有 `Boolean ShowTitle { get; }`。
  - **L310-313**：`IGalleryGridLayout` 只有 `ShowTitle` / `ShowSubtitle`。
  - **L317-330**：`IListPage` 提供 `GridProperties`（L324）、`HasMoreItems`（L325）、`GetItems()`（L328）、`LoadMore()`（L329）。
- Toolkit（`Microsoft.CommandPalette.Extensions.Toolkit/ListPage.cs`）：`HasMoreItems` 与 `LoadMore()` 都是 `virtual`，可覆写。
- 结论：卡片尺寸、列数、图标尺寸**由宿主决定**；插件只能选布局种类并控制标题/副标题显示。因此设置页只提供 `list` / `grid-medium`。

### 13.2 T27：Shell 图标（代码）

- 新文件 `Services/ShellIconExtractor.cs`：`SHGetFileInfo` + `SHGetImageList(SHIL_JUMBO)` → `HICON` → `GetDIBits` → 手写 PNG（无 `System.Drawing` 依赖）。
- 回退链：jumbo(256) → large(32) → small(16) → `Icons.ForKind`。
- 资源释放：`GetIconInfo` 的两张位图 `DeleteObject`，`HICON` `DestroyIcon`（`finally`）。
- `IconCache`：内存 `ConcurrentDictionary` + `_inflight` 去重 + 失败 5 分钟重试 + 命中期 30s 节流校验 `mtime^length`；目录 mtime 变化触发失效。
- 阻塞控制：本地缺失路径同步判定（结果缓存 30s）；UNC `\\` 不同步探测；提取在后台线程（3 并发）。

### 13.3 网格与滚动

- `ItemsPage.ApplyLayout()`：`layout=grid-medium` → `MediumGridLayout { ShowTitle = true }`，否则 `GridProperties = null`。
- **增量加载**：`SearchEngine.Search` 不再固定截断；`ItemsPage` 每页 100 条，`HasMoreItems` + `LoadMore()` 追加（依据 §13.1 IDL）。
- 关于「第 4 行被命令栏裁切」：判断为**视口高度不足导致的常规裁剪**（列表自身可滚动，宿主命令栏固定），非页面高度写死；规避=默认列表 + 可滚动。
- 关于「滚轮/PageDown 无位移」：**最可能原因**是滚动事件按焦点路由，焦点不在列表容器；**未证实**。已做的规避：默认列表、首项可聚焦、增量加载；未改宿主行为。

### 13.4 设置项

| key | 默认 | 取值 | 落盘 |
|---|---|---|---|
| `layout` | `list` | `list` / `grid-medium` | 立即 |
| `showFullPath` | `true` | bool | 立即 |
| `showSubtitle` | `true` | bool | 立即 |
| `showTags` | `true` | bool | 立即 |

- `Load()` 用 `_loading` 抑制 `SettingsChanged` 回写，避免半加载状态覆盖文件；`IgnoreUnknownValue` 处理未知枚举。

### 13.5 应用图标

- 生成脚本 `tools/gen-icons.py`（唯一源，Pillow）；输出 `src/CmdPalLinks/Assets/` 共 60 个 PNG。
- 设计：深靛蓝渐变圆角方块 + 白色双链环（45°）；≤32px 用透明底 + 亮蓝单/双环。
- 宿主读取位置：扩展条目图标来自代码 `Icons.App = IconHelpers.FromRelativePath("Assets\\StoreLogo.png")`；包/系统展示来自 `Package.appxmanifest` 的 `Logo` / `Square150x150Logo` / `Square44x44Logo` / `Wide310x150Logo` / `SplashScreen`（全部已生成）。
- 静态自检：`py -3 tools/gen-icons.py --check` → 全部存在、有 alpha、非空白。

### 13.6 构建与产物

- `dotnet build -c Debug` / `-c Release`：**0 警告 / 0 错误**（`--no-restore` 复核输出 `0 个警告 / 0 个错误`）。
- Release 输出绝对路径：`src\CmdPalLinks\bin\Release\net10.0-windows10.0.26100.0\win-x64\`
- 资产目录（同路径下 `Assets\`）含 60 个 PNG。

