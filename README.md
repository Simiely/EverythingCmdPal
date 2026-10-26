# EverythingCmdPal

PowerToys Command Palette 的 Everything 搜索扩展（基于官方 SDK3 / Everything 1.5）。

## 功能

- **秒搜全盘**：Everything SDK3 官方 IPC，首屏 50 条、无限滚动加载，防抖去重
- **预览**：图片大图（跟随详情栏宽度）、文本片段、其他格式 64px 缩略图；完整预览走 PowerToys Peek
- **拖拽**：结果可直接拖到资源管理器 / 邮件 / 聊天窗口（StorageItems DataPackage）
- **动作**：打开、打开所在文件夹、复制完整路径、用 Peek 预览、在 Everything 中打开当前搜索
- **排序过滤条**：名称 / 路径 / 大小 / 修改时间 × 升/降序，8 种组合，切换即用服务端排序重搜，并记住选择
- **详情栏开关**：`Ctrl+K` 右键「隐藏详细信息」进入纯列表（文件名占满整行）；纯列表页右键「显示详细信息」返回
- **设置**：详情栏宽度、预览图清晰度、每页条数、Everything 实例名、默认排序、输入即搜（fallback）

## 安装 / 更新

**新电脑（推荐）**：`git clone` 或 Download ZIP 解压 → 双击 `tools\update.cmd`

脚本会自动完成：安装 .NET SDK 10（仅用户目录）→ 生成并信任开发证书（一次 UAC）→
构建 → 打包签名 → 安装 → 启动。打包工具（makeappx/signtool）来自 NuGet 的
`Microsoft.Windows.SDK.BuildTools` 包，无需手动下载。

**已有机器**：改完代码后同样双击 `tools\update.cmd` 即可（安全流程：关 CmdPal →
卸载旧包 → 安装 → 重启，不会出现重复条目）。

**前提**：Windows 10/11 x64；本机需安装并运行 Everything 1.5+（扩展通过 IPC 连接）。

## 开发

```
tools\build.ps1            # 编译 + 铺包
tools\update.cmd           # 一键安全更新（推荐）
tools\SelfTest             # SearchSession 分页/去重自测（dotnet run）
tools\QueryTest            # SDK3 连通性独立验证
```

- 无 Visual Studio 依赖：.NET SDK 10 + NuGet（CsWinRT 的 winmd 指向 NuGet 包，见 csproj）
- 无 Windows SDK 依赖（同上）

## 已知限制

- 详情栏宽度 = `Details.Size` 三档 + 窗口比例，宿主无拖拽分隔条
- 公开 SDK（0.11/0.12）无 `Details.Content`，面板内不支持图片查看器（等 SDK 更新）
- 切换页面后搜索框文字会被宿主清空：搜索框文本来自宿主侧页面状态，扩展只能在导航前给目标页设 `SearchText`；
  且 `SearchText` 赋值会触发一次重搜、光标位置无法控制（详见 DEVELOPMENT.md 问题记录）
- 一切需要 `Everything 1.5+` 正在运行（默认实例回退 `1.5a`）

## 结构

```
src/EverythingCmdPal/
  Actions/      动作命令（打开/定位/复制/Peek/在 Everything 中打开/定位器）
  Interop/      Everything3.dll P/Invoke（签名对齐官方 Everything3.h）
  Items/        列表条目与条目上下文（ResultContext）
  Pages/        结果页（调度 + UI 适配，分页在 SearchSession）
  Services/     SearchSession / EverythingClient / ResultMetadata / PreviewService / SettingsManager / HostLog
  Interop 之外均不直接触碰 P/Invoke
```
