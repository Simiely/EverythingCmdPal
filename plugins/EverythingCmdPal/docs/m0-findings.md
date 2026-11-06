# M0 实测记录（2026-10-26）

## 环境

| 项 | 值 |
|---|---|
| Windows | 10.0.26100 |
| PowerToys | 0.100.2（CmdPal 0.11.11762） |
| Everything | 1.5.0.1408a，实例 `1.5a`，`allow_ipc_pipe=1` |
| Peek | 已安装：`%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.Peek.UI.exe` |
| .NET | 运行时 6/8/9/10；SDK 10.0.100 已装到 `%LOCALAPPDATA%\Microsoft\dotnet` |
| Visual Studio | 未安装（用 `dotnet build` + 手工 MSIX 打包绕过） |

## 构建结论

1. **没有 Windows SDK 也能编译**：CsWinRT 默认去注册表找 SDK，会报
   `Could not find the Windows SDK in the registry`。
   解法：把 NuGet 里的 winmd 目录喂给它。
   ```xml
   <CsWinRTWindowsMetadata>$(NuGetPackageRoot)microsoft.windows.sdk.net.ref\$(WindowsSdkPackageVersion)\winmd</CsWinRTWindowsMetadata>
   ```
2. **SDK 0.11 与 0.12 都没有 `Details.Content`**（`IDetails2` 只存在于 PowerToys 仓库源码）。
   实测：换 0.12.260812002 后 `details.Content = ...` 仍然编译失败。
3. 因此**面板内的图片/文本预览当前无法通过公开 SDK 实现**。
   可用能力：`Details.HeroImage`（缩略图）、`Details.Body`（Markdown）、`Details.Metadata`；
   完整预览走 **Peek**（`PowerToys.Peek.UI.exe "<path>"`）。
4. 0.100.2 的宿主 `DetailsViewModel` 也没有 `IDetails2` 分支；0.101 同样没有，只有 main（0.102 一线）才有。
   即便升级 PowerToys，SDK 不暴露接口也拿不到。

## 安装结论

- 松散注册（`Add-AppxPackage -Register`）→ 0x80073CFF：需要开发者模式或旁加载策略。
- 自签名 MSIX → 0x800B0109：根证书必须在**本机**受信任根，`CurrentUser\Root` 不够。
- 0x80073CF6 + 0x80070057：manifest 里 `<Resource Language="x-generate" />` 是 VS 构建占位符，
  手工打包必须写具体语言（已改为 `zh-CN`）。
- 最终：证书进 `LocalMachine\Root` + `TrustedPeople` 后，
  `Add-AppxPackage -Path EverythingCmdPal.msix` 安装成功（1.0.0.0，Status Ok）。

## 运行验证

- CmdPal 日志（`%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\0.100.2.0\Log_2026-10-25.log`）：
  `Started extension EverythingCmdPal_1.0.0.0_neutral__byt16r9dq2e2y in 2089 ms`
  `Loaded 1 command(s) and 0 band(s) from EverythingCmdPal_... in 60 ms`
- 独立查询测试（`tools/QueryTest`，同一套 P/Invoke）：
  `connected: 1.5a`，`*.sln` → `total: 58 viewport: 10`，路径正确。
- 环境中还装有 `VictorLin.EverythingCP_0.9.26030.0`（lin-ycv 的 EverythingCommandPalette），
  两个 Everything 扩展会同时出现，建议在 CmdPal 设置里禁用其一。


## 后续可选

- 若要在面板内做“大图预览”，可加一个 `ContentPage` + `MarkdownContent` 的预览命令（0.11 可用），
  但图片仍需 Markdown 渲染支持，风险未知；更稳的是 Peek。
- 真要做内嵌查看器，等 SDK 正式发布 `IDetails2`，或直接用 0.102 源码自编译宿主（不现实）。
