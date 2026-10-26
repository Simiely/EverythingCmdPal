# AGENTS.md · 项目规则

> 📌 **文档基线**：2026-10-26（commit `103f71b`）排序过滤条 + 详情栏换页开关（1.3.3.2）
> **更新文档/代码后，请更新此行**（日期 + 新 commit hash），并在 CHANGELOG 追加版本

## 技术栈

- .NET SDK **10.0.100**（装在 `%LOCALAPPDATA%\Microsoft\dotnet`；无 Visual Studio、无 Windows SDK）
- `net10.0-windows10.0.26100.0` + `WindowsSdkPackageVersion 10.0.26100.68-preview`
- `Microsoft.CommandPalette.Extensions` **0.11.260520004**（Toolkit 同包）、`Shmuelie.WinRTServer` 2.1.1、`Microsoft.Windows.CsWinRT` 2.2.0、`Microsoft.Windows.SDK.BuildTools` 10.0.26100.4188（NuGet 自动，提供 makeappx/signtool）
- 宿主：PowerToys **0.100.2**（CmdPal 0.11.11762）
- Everything **1.5.0.1408a**，实例名 `1.5a`（`allow_ipc_pipe=1`），SDK 为 `src/EverythingCmdPal/native/Everything3.dll`

## 关键坑（详见 DEVELOPMENT.md 问题记录）

- SDK3：一旦调用 `AddSearchPropertyRequest`，`GetResultFullPathNameW` 对所有条目返回 0 → 不请求属性，大小/时间用 `FileInfo`
- 手工打包：manifest 的 `Language="x-generate"` 必须改成具体语言，否则 `0x80073CF6`
- 安装：签名证书必须进 **本机** `LocalMachine\Root`（CurrentUser 不够）；**先卸载旧包再安装**，否则 CmdPal 出现重复条目
- 预览：宿主先全尺寸解码再布局 → 大图先降采样成临时文件再引用；URL 加 `--x-cmdpal-fit=fit` 才跟随栏宽
- `ChoiceSetSetting` 默认值 = **第一个选项**，默认项必须排在第一位
- `DynamicListPage.SearchText` 的 setter 会**同步回调 `UpdateSearchText`**（`DynamicListPage.cs` L9-18）→ 在后台回调里写它会自激成环（界面持续闪）；搜索框文本归宿主管，GoBack 返回时无法保留
- `ShowDetails` 只在页面构造时被宿主读取，运行期改值 + `OnPropertyChanged` + `RaiseItemsChanged` 均不重排 → 详情栏开关只能换页
- 一对页面来回切换时只允许一个方向 push，回程用 `CommandResult.GoBack()`，否则返回栈越切越深

## 约定

- 面向用户的文本用中文；注释用中文
- 单项目不拆多工程、不引 DI 容器（对齐官方 Indexer 扩展粒度）
- 更新必须走 `tools\update.cmd`（关 CmdPal → 卸载 → 安装 → 重启）；**不要**用 `-ForceUpdateFromAnyVersion` 覆盖安装
- 日志走 `HostLog`（宿主日志），禁止逐项写临时文件

## 常用命令

- 一键安装/更新（新机器也能跑：自动装 SDK、证书走一次 UAC）：双击 `tools\update.cmd`
- 分页自测：`dotnet run --project tools\SelfTest`
- SDK 连通性：`dotnet run --project tools\QueryTest -- "*.sln"`

## 详细规则（按需 @引用）

- @DEVELOPMENT.md（架构说明 + 问题记录）
