# DEVELOPMENT.md

## 项目概览

PowerToys Command Palette 的 Everything 搜索扩展：全盘秒搜、无限滚动、拖拽外发、
图片大图预览、文本片段预览、Peek 深链、设置页、fallback 输入即搜。

- 入口链：`Program（COM 服务器）→ EverythingExtension → EverythingCommandsProvider → ResultsPage`
- 功能面：搜索 / 分页 / 预览 / 拖拽 / 动作 / 设置 / 日志，全部走官方 SDK 0.11 公开接口

## 架构说明

```
src/EverythingCmdPal/
  Actions/      InvokableCommand 动作（打开/定位/复制/Peek/在 Everything 中打开/定位器）
  Interop/      Everything3.dll P/Invoke（签名对齐官方 Everything3.h，含实例名连接）
  Items/        ResultItem（行 UI）+ ResultContext（页面注入的上下文聚合）
  Pages/        ResultsPage（列表 UI 适配 + 查询调度；分页状态在 SearchSession）
  Services/     SearchSession / IEverythingSearch+EverythingClient / ResultMetadata
                PreviewService / SettingsManager(JsonSettingsManager) / HostLog
```

要点：

- **分页**：`SearchSession` 持有 query/offset/total/去重集合；页面只调 `Start()/LoadNext()`
- **线程**：查询走 `Channel<string>(容量1, DropOldest)` 单消费者；列表读写有锁；
  宿主会把 observable 属性变更封送到 UI 线程（`ExtensionObjectViewModel.ApplyPendingUpdates`）
- **预览**：`PreviewService` 是唯一写 `Details.HeroImage/Body` 的地方；图片先降采样成
  `%TEMP%\EverythingCmdPal\preview` 里的 PNG，再以 `file://...?--x-cmdpal-fit=fit` 引用
- **元数据**：只在 `ResultMetadata.Build()` 组装（路径/大小/修改时间），条目与预览共用
- **切换详情模式**：页面注入现成的 `CommandContextItem`（指向反向模式的新页面，携带当前查询），
  条目不知道页面存在；`ShowDetails=false` 时宿主自动补 "Show details" 动作
- **无构建依赖**：无 VS / 无 Windows SDK；CsWinRT 的 winmd 指向 NuGet 包（见 csproj `CsWinRTWindowsMetadata`）

## 关键问题与方案

### 问题：SDK3 请求属性后，路径全部为空（列表看似无结果）

**TL;DR**：调过 `AddSearchPropertyRequest` 后 `GetResultFullPathNameW` 全部返回 0。

- 问题：viewport 有 50 条，路径全空 → 界面判定“没有更多结果”
- 根因：请求属性后结果列表进入属性模式，路径读取依赖 `PATH_AND_NAME` 属性
- 解决：不请求任何属性；大小/修改时间用 `FileInfo` 在预览阶段取
- 预防：凡是用 SDK3 新增取值，先写独立 harness（`tools/QueryTest`）验证

### 问题：手工打包报 0x80073CF6（资源语言 x-generate 无效）

**TL;DR**：`x-generate` 是 VS 构建占位符，makeappx 不认识。

- 问题：打包/安装失败，报“指定的资源语言 x-generate 不是有效的语言”
- 根因：模板 manifest 面向 VS 构建；手工打包没有替换步骤
- 解决：改成 `<Resource Language="zh-CN" />`
- 预防：脱离 IDE 打包时，通读 manifest 里所有构建期占位符

### 问题：签名包安装报 0x800B0109（根证书不受信任）

**TL;DR**：AppX 部署服务只认**本机**受信任根，`CurrentUser\Root` 不够。

- 问题：自签名 MSIX 安装失败；`Import-Certificate` 到 CurrentUser\Root 无效
- 根因：部署服务以系统身份校验链
- 解决：管理员运行 `certutil -addstore -f Root dev.cer`（+ TrustedPeople）
- 预防：先装证书再装包；避免用 `-ForceUpdateFromAnyVersion` 覆盖装（会残留 staged 旧版本，CmdPal 出现重复条目）

### 问题：CmdPal 列表出现已卸载扩展的残留条目

**TL;DR**：CmdPal 不清理设置里的扩展键，卸载包不等于列表干净。

- 问题：设置页出现已卸载的 Goucey/VictorLin Everything 扩展
- 根因：`settings.json`（`Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState`）残留
- 解决：关闭 CmdPal → 手动删除对应 JSON 键 → 重启
- 预防：更新一律“卸载旧包→安装→重启”

### 问题：详情栏预览图只有 64px 且大图显示不出来

**TL;DR**：HeroImage 固定 64px；大图要用 Body 的 Markdown 本地图，且必须自己降采样。

- 问题：`IDetails2/Details.Content` 未随公开 SDK 发布（0.11/0.12 都没有）；宿主先全尺寸解码，
  大图直接失败返回 null；`fit=fit` 缺失时图片钉死在自然像素宽
- 根因：宿主 `RtbInlineImageFactory` 把宽度 clamp 到列宽；`ImageSourceFactory` 先解码后缩放
- 解决：`StorageFile.GetThumbnailAsync` 降采样成临时 PNG（按 路径+mtime+宽度 缓存）→
  `![preview](<file:///...?--x-cmdpal-fit=fit>)`；HeroImage 与 Body 互斥（两插槽都设会出两张图）
- 预防：宿主行为以源码为准（DetailsSizeToGridLengthConverter / RtbInlineImageFactory）

### 问题：ChoiceSetSetting 的默认值不生效

**TL;DR**：默认值 = **第一个选项**，与代码里的 fallback 无关。

- 问题：想让默认 640/大，实际是 240/小
- 根因：`ChoiceSetSetting` 构造取 `choices[0].Value`
- 解决：默认项排第一；已保存的旧值需迁移（删 LocalState/settings.json 或改键值）
- 预防：新增下拉设置时先排默认项

### 问题：Ctrl+K 打开的不是顶层命令的菜单

**TL;DR**：`Ctrl+K` 打开的是**当前选中项/当前页**的右键菜单。

- 问题：在结果页按 Ctrl+K 找不到顶层命令的上下文动作
- 根因：`ShellPage_OnKeyDown` → `OpenContextMenuMessage` 作用于当前焦点项
- 解决：需要全局可用的动作就做成独立顶层命令，或挂在每个结果项的 MoreCommands 上
- 预防：设计交互时区分“页级”与“项级”动作面

### 问题：在 Everything 中打开必须带实例名

**TL;DR**：1.5a 是独立实例，命令行要 `-instance "1.5a" -search "词"`。

- 问题：不带 instance 时打开的是默认实例（1.4）窗口，两边数据不一致
- 根因：1.5a 与 1.4 并行设计
- 解决：`EverythingLocator` 定位 exe（Program Files/运行中进程），拼接 `-instance` + `-search`
- 预防：与 Everything 相关的外部调用统一走 locator + 实例名

### 问题：来回切换「隐藏详细信息 / 显示详细信息」后要按多次返回

**TL;DR**：双向都 `push` 新页面会让返回栈越切越深；回程必须用 `CommandResult.GoBack()`。

- 问题：切 N 次后按一次返回退不出去
- 根因：`CommandContextItem(page)` 每次都是 push；两个 ResultsPage 互相 push 成链
- 解决：详情页 →「隐藏详细信息」仍 push；纯列表页 →「显示详细信息」返回 `CommandResult.GoBack()`（宿主 `Frame.GoBack()`，栈深恒定）
- 预防：一对页面来回切换时，只允许一个方向 push，另一方向必须 pop

### 问题：切换页面后搜索框文字消失（且无法用 SearchText 回填）

**TL;DR**：宿主搜索框文本取自 backstack 里 VM 的 `SearchTextBox`，只在 VM 初始化时读一次；运行期写 `SearchText` 只对当前页生效。

- 现象 1：从纯列表页 GoBack 回详情页，搜索框变空
- 现象 2：在扩展里给非当前页写 `SearchText`，界面持续闪烁（像一直在搜）
- 根因 1：`ShellPage` 切页时 `FilterBox.Text = page.SearchTextBox`（`SearchBar.xaml.cs` L104-115），该字段来自 `ListViewModel` 初始化（L1196-1203），不重读插件模型
- 根因 2：Toolkit 的 `DynamicListPage.SearchText` setter 是 `SetSearchNoUpdate` **加同步调用 `UpdateSearchText`**（`DynamicListPage.cs` L9-18，且无同值判断）→ 我们的 `UpdateSearchText` 再排一次搜索 → `ConsumeAsync` 又 `SetPendingQuery` → 设 `SearchText` → 自激成环
- 解决：两个方向都不回填（当前行为）；若必须回填，只能加防重入标志，且无法解决返回时为空
- 预防：不要在 `SetPendingQuery`／后台回调里写 `SearchText`；官方 Calc 扩展用 `old == new` 短路防同类循环

### 问题：运行时修改 ShowDetails 宿主不重新布局

**TL;DR**：`ShowDetails` 只在页面构造时被宿主读取，运行期改值 + `OnPropertyChanged` + `RaiseItemsChanged` 都无效。

- 问题：想在同一个页面上实时开关详情栏
- 根因：宿主按页面实例构建时读取该属性，没有对应的变更事件
- 解决：只能换页（`ShowDetails=false` 的新页面），代价是导航栈与搜索框文本
- 预防：详情栏开关一律按「换页」设计，不要再尝试运行时切换

### 问题：程序化设置 SearchText 必然全选且无法控制光标

**TL;DR**：扩展 SDK 没有 caret/selection/focus API，全选发生在宿主 `SearchBar`。

- 根因：`SearchBar.xaml.cs` 在 `InitialSearchText` 变化时 `SelectSearch()` → `SelectAll()`；GoBack 后同样 `FocusActiveControl()` + 全选
- 解决：无（SDK 只暴露 `SearchText` 的 get/set）；导航 push 路径是 `Select(Text.Length, 0)`，光标在末尾
- 预防：需要「文字保留」时，只在导航**前**给目标页设 `SearchText`（此时目标页不在前台）
