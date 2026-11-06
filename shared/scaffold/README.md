# 新插件脚手架

复制本目录到 `plugins/<Name>/`，按下面的替换表改三处标识，然后用 `shared/build/install.ps1` 首次安装。

## 步骤

1. `plugins/<Name>/` 下建 `src/<Name>/`、`tools/`、`docs/`（可参考 `plugins/CmdPalLinks/` 的完整形态）。
2. 复制 `Package.appxmanifest.template` → `src/<Name>/Package.appxmanifest`，替换 `__NAME__` / `__DISPLAY__` / `__CLSID__` / `__PUBLISHER__`。
3. 复制 `Extension.csproj.template` → `src/<Name>/<Name>.csproj`，替换 `__NAME__`。
4. 复制 `plugin.psd1.template` → `plugins/<Name>/plugin.psd1`，填 `PackageFamilyName`（装一次后用 `Get-AppxPackage <Name>` 查）。
5. 代码结构照 `plugins/CmdPalLinks/src/CmdPalLinks/`：`Program.cs`（`-RegisterProcessAsComServer` + `ComServer`）、
   `*Extension.cs`（实现 `IExtension`）、`*CommandsProvider.cs`（实现 `ICommandProvider`）。
6. 写 `README.md`（6 节）与 `CHANGELOG.md`；在根 `README.md` 的插件索引加一行。
7. 首次安装：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File shared\build\install.ps1 -Plugin <Name>
```

## CLSID 必须三处一致

| 位置 | 内容 |
| --- | --- |
| C# 类 | `[Guid("__CLSID__")]` |
| 清单 `com:Class` | `<com:Class Id="__CLSID__" />` |
| 清单 `CreateInstance` | `<CreateInstance ClassId="__CLSID__" />` |

不一致时宿主发现不了扩展，或 COM 激活失败。

## 清单硬性要求

- `uap3:AppExtension` 的 `Name` 必须严格是 `com.microsoft.commandpalette`。
- `SupportedInterfaces` 目前只支持 `Commands`。
- `Resource Language` 不能写 `x-generate`（手工 `makeappx` 会报 `0x80073CF6`）。
- 需要 `rescap:runFullTrust`；要联网才加 `internetClient`。
- `PublicFolder="Public"` 对应的目录必须存在（放 `.gitkeep`）。
- `AppListEntry="none"`，否则开始菜单会出现一个无用的应用项。
