# DEVELOPMENT.md · 开发说明

## 项目概览

本仓库收纳自研的 PowerToys Command Palette 扩展，当前两个插件：

| 插件 | 一句话 | 当前版本 | 文档 |
|---|---|---|---|
| EverythingCmdPal | 用 Everything 秒搜全盘文件 | `1.3.4` | [README](plugins/EverythingCmdPal/README.md) · [开发说明](plugins/EverythingCmdPal/docs/development.md) |
| CmdPalLinks | 本地链接 / 文件 / 文件夹收藏与检索 | `0.1.17.0` | [README](plugins/CmdPalLinks/README.md) · [design](plugins/CmdPalLinks/docs/design.md) |

宿主基线：PowerToys `0.100.2` · CmdPal UI `0.11.11762.0` · SDK `Microsoft.CommandPalette.Extensions 0.11.260520004` · `net10.0-windows10.0.26100.0`（细节见 [docs/host-notes.md](docs/host-notes.md)）。

安装与更新见 [docs/install.md](docs/install.md)，排障见 [docs/troubleshooting.md](docs/troubleshooting.md)，发版见 [docs/release.md](docs/release.md)，文档治理见 [docs/index.md](docs/index.md)。

## 架构说明

```
EverythingCmdPal/
├─ plugins/<Name>/          # 自包含单元：源码、MSIX 清单、构建脚本、文档、资产
│  ├─ src/<Name>/           # C# 源码 + Package.appxmanifest（版本真源）
│  ├─ tools/                # install.ps1 / update.cmd 等
│  ├─ docs/                 # 插件内文档（暂未纳入统一扫描）
│  ├─ README.md
│  └─ CHANGELOG.md          # 插件版本变更（按版本分节）
├─ shared/build/*.ps1       # 跨插件参数化构建脚本（真源）
├─ docs/                    # 仅跨插件机制：install / troubleshooting / host-notes / release
│  ├─ decisions/            # ADR
│  └─ _generated/           # 生成索引与反向链接，勿手改
├─ tools/repo-kit/          # 文档校验、生成、版本锁
└─ schema/                  # taxonomy / frontmatter / sources / kit.lock
```

约束：插件之间不互相引用，只共享 `shared/build/` 与根 `docs/` 的机制说明；插件特有数值留在插件自己的 `docs/install-notes.md`。文档契约与扫描范围见 [docs/concepts/plugin-model.md](docs/concepts/plugin-model.md)。

## 关键问题与方案

- **文档治理引入**：[ADR-0001](docs/decisions/ADR-0001-adopt-repo-kit.md) —— 为什么要机器层校验、人层四件套怎么分工
- **跨插件治理 v2.2**：[ADR-0002](docs/decisions/ADR-0002-v2-2-upgrade.md) —— 根 `docs/` 与 `plugins/**` 的扫描边界、`exempt_basenames`、manifest 的 `co_change` 映射
- **版本漂移**：README 的"当前版本"以 `Package.appxmanifest` 的 `Identity Version` 为准；改清单必须在同一 PR 更新根 README、插件 README 与插件 CHANGELOG（`schema/sources.json` 已配置 `co_change`，CI 在 PR 上校验）
- **插件内历史问题**：EverythingCmdPal 见 [plugins/EverythingCmdPal/docs/development.md](plugins/EverythingCmdPal/docs/development.md)；CmdPalLinks 见 [design.md](plugins/CmdPalLinks/docs/design.md) / [spec.md](plugins/CmdPalLinks/docs/spec.md)
- **Phase 2（未做）**：插件内 19 份 Markdown 尚未纳入 frontmatter 契约，迁移时按插件逐个纳入
