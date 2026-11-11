# Command Palette 插件集合

本仓库收纳自研的 PowerToys Command Palette 扩展。每个插件是一个**自包含单元**：源码、MSIX 清单、构建脚本、文档、资产都在自己的目录里；跨插件通用的机制放在根 `docs/`，参数化脚本放在 `shared/build/`。

基线：PowerToys `0.100.2` · CmdPal UI `0.11.11762.0` · SDK `Microsoft.CommandPalette.Extensions 0.11.260520004` · `net10.0-windows10.0.26100.0`。

## 插件索引

| 插件 | 一句话 | 当前版本 | 宿主最低版本 | 安装 | 排障 | 源码 | CHANGELOG |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [EverythingCmdPal](plugins/EverythingCmdPal/README.md) | 用 Everything 秒搜全盘文件 | `1.3.4` | `0.100.2` | [install.ps1](plugins/EverythingCmdPal/tools/install.ps1) / [update.cmd](plugins/EverythingCmdPal/tools/update.cmd) | [troubleshooting](docs/troubleshooting.md) | [src](plugins/EverythingCmdPal/src) | [CHANGELOG](plugins/EverythingCmdPal/CHANGELOG.md) |
| [CmdPalLinks](plugins/CmdPalLinks/README.md) | 本地链接 / 文件 / 文件夹收藏与检索 | `0.1.2.0` | `0.100.2` | [install.ps1](plugins/CmdPalLinks/tools/install.ps1) | [install-notes](plugins/CmdPalLinks/docs/install-notes.md) | [src](plugins/CmdPalLinks/src) | [CHANGELOG](plugins/CmdPalLinks/CHANGELOG.md) |

## 按任务找入口

**我要改文档 / 加新文档**
→ 跨插件机制文档见 [docs/index.md](docs/index.md)；新页模板见 [docs/_template.md](docs/_template.md)；
AI/人的仓库入口见 [AGENTS.md](AGENTS.md)。

**我要装一个插件**
1. 读 [docs/install.md](docs/install.md) 的 9 步流程与禁止项；
2. 跑插件自己的薄封装：`plugins/<Plugin>/tools/install.ps1`（EverythingCmdPal 也可用 `tools\update.cmd`）；
3. 装完按 [docs/install.md](docs/install.md) §5 做三层残留核对。

**装了不显示 / 重复 / 打不开**
→ [docs/troubleshooting.md](docs/troubleshooting.md)：三层取证（包 / 持久缓存 / UIA 渲染）、错误码对照、SparseApp 异常说明。插件特有数值在各插件 `docs/install-notes.md`。

**我要发版**
→ [docs/release.md](docs/release.md)：tag `<plugin>-v<version>`、资产带版本号、每插件 `Identity Version` 独立递增。历史 tag `v1.3.4` 保留不动。

**我要加新插件**
1. 复制 `shared/scaffold/` 到 `plugins/<Name>/`，改 `plugin.psd1`、`Package.appxmanifest` 的 `Identity`；
2. CLSID 三处必须一致（C# `[Guid]`、`com:Class Id`、`CreateInstance ClassId`）；
3. 写 `README.md`、`CHANGELOG.md`；
4. 在本文件「插件索引」加一行；
5. 用 `shared/build/install.ps1 -Plugin <Name>` 首次安装（版本从 `0.1.0.0` 起）。

## 目录约定

```
.
├─ README.md              # 本文件：插件索引 + 按任务找入口
├─ docs/                  # 跨插件通用：install / troubleshooting / host-notes / release
├─ shared/
│  ├─ build/              # 参数化：package.ps1（构建+打包+签名）、install.ps1（零残留安装）
│  └─ scaffold/           # 新插件脚手架（psd1 / manifest / csproj 模板）
└─ plugins/<Name>/        # 一个插件一个目录，目录名 = MSIX Identity Name
   ├─ README.md           # 固定 6 节：是什么 / 安装 / 使用 / 排障 / 开发 / 发版
   ├─ CHANGELOG.md
   ├─ plugin.psd1         # 插件常量：Manifest / Project / PFN / 证书 / 进程名 / TagPrefix
   ├─ src/<Name>/         # csproj + Package.appxmanifest + 代码 + Assets
   ├─ tests/              # 可选
   ├─ tools/              # 插件专属脚本（瘦封装，逻辑在 shared/build/）
   └─ docs/               # 插件文档：design.md / spec.md / tasks.md / self-test.md / install-notes.md
```

命名约定：目录名 = MSIX `Identity Name` = csproj 名；文档固定名 `design.md`、`spec.md`、`tasks.md`、`self-test.md`、`install-notes.md`；脚本固定名 `tools/install.ps1`、`tools/gen-icons.py`。

## 多插件共存规则

- **隔离**：`Identity Name`、CLSID、`LocalState`、进程名各自独立；证书与 Publisher 可共享。
- **版本**：每个插件独立递增，互不干扰；tag 一律带插件前缀。
- **共享**：只有机制文档与构建脚本进 `shared/` 与根 `docs/`，插件代码不跨目录引用。
- **禁止**：插件直接引用另一个插件的源码或 `LocalState`。

## 不纳入版本库

`.testdata/`（备份、日志、截图、历史安装包）、`package/`（layout 与 msix 产物）、`ref/`（上游参考 clone，含嵌套 `.git`）、`.screenshots/`、证书文件。详见 `.gitignore`。
