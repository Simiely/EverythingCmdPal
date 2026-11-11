---
id: docs.concepts.plugin-model
title: 插件自包含单元模型
summary: 一个插件一个目录；Identity/CLSID/LocalState/进程隔离；跨插件只共享机制与脚本。
type: explanation
kind: standard
status: active
owner: "@simiely"
tier: critical
topics: [architecture]
defines: plugin-model
source_type: doc
source: README.md
last_reviewed: 2026-11-11
related: []
---
# 插件自包含单元模型

本仓库收纳自研的 PowerToys Command Palette 扩展。每个插件是一个**自包含单元**：源码、MSIX 清单、
构建脚本、文档、资产都在自己的目录里。

## 目录与命名

- 目录名 = MSIX `Identity Name` = csproj 名。
- 固定文件名：`README.md`、`CHANGELOG.md`、`plugin.psd1`、`Package.appxmanifest`。
- 固定文档名：`design.md`、`spec.md`、`tasks.md`、`self-test.md`、`install-notes.md`。
- 固定脚本名：`tools/install.ps1`、`tools/gen-icons.py`。

## 隔离与共享

| 维度 | 规则 |
|---|---|
| 隔离 | `Identity Name`、CLSID、`LocalState`、进程名各自独立 |
| 共享 | 证书与 Publisher 可共享；机制文档进根 `docs/`，参数化脚本进 `shared/build/` |
| 禁止 | 插件直接引用另一个插件的源码或 `LocalState` |

## 新增插件的顺序

1. 复制 `shared/scaffold/` 到 `plugins/<Name>/`，改 `plugin.psd1` 与 `Package.appxmanifest` 的 `Identity`。
2. CLSID 三处必须一致：C# `[Guid]`、`com:Class Id`、`CreateInstance ClassId`。
3. 写 `README.md`、`CHANGELOG.md`。
4. 在根 [README](../../README.md) 的插件索引加一行。
5. 用 `shared/build/install.ps1 -Plugin <Name>` 首次安装。

## 为什么这对文档治理重要

- 插件文档是「本地真源」，跨插件文档是「共享机制」。两者混在一起会复制命令与清单。
- 根 `docs/` 只放跨插件机制；插件特有数值留在插件自己的 `docs/install-notes.md`。
- 新增插件时，只需要给跨插件文档补 frontmatter；插件内文档按插件逐步迁移。

## Related Items

- **see-also**: [安装与更新](../install.md) — 自包含单元如何落地安装。
- **see-also**: [发版规则](../release.md) — 每个插件独立递增版本。
