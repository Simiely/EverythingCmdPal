---
id: docs.generated.index
title: 生成索引
summary: 从 frontmatter 自动生成的导航；勿手改。
type: reference
kind: index
status: active
owner: "@platform"
source_type: doc
source: schema/taxonomy.json
last_reviewed: 2026-11-11
related: []
---

# 生成索引

> 生成文件，勿手改。运行 `python tools/repo-kit/generate.py --root . --write` 重建。

## 按 type

### how-to

| 页面 | kind | status | owner | tier | source |
|---|---|---|---|---|---|
| [安装与更新（零残留流程，通用）](../install.md) | standard | active | @simiely | critical | `shared/build/install.ps1` |
| [发版规则](../release.md) | standard | active | @simiely | critical | `shared/build/package.ps1` |
| [排障（Command Palette 扩展通用）](../troubleshooting.md) | standard | active | @simiely | critical | `docs/install.md` |

### reference

| 页面 | kind | status | owner | tier | source |
|---|---|---|---|---|---|
| [决策索引](../decisions/index.md) | index | active | @simiely | normal | `docs/decisions/ADR-0001-adopt-repo-kit.md` |
| [跨插件文档索引](../index.md) | index | active | @simiely | normal | `README.md` |

### explanation

| 页面 | kind | status | owner | tier | source |
|---|---|---|---|---|---|
| [插件自包含单元模型](../concepts/plugin-model.md) | standard | active | @simiely | critical | `README.md` |
| [ADR-0001 用 repo-kit v2 治理跨插件文档](../decisions/ADR-0001-adopt-repo-kit.md) | decision | active | @simiely | normal | `tools/repo-kit/check_repo.py` |
| [ADR-0002 升级到 repo-kit v2.2](../decisions/ADR-0002-v2-2-upgrade.md) | decision | active | @simiely | normal | `tools/repo-kit/check_repo.py` |
| [Command Palette 宿主笔记（版本与 API 约束）](../host-notes.md) | standard | active | @simiely | critical | `plugins/EverythingCmdPal/docs/development.md` |

## 全部页面

| id | 标题 | type | kind | status |
|---|---|---|---|---|
| `docs.concepts.plugin-model` | [插件自包含单元模型](../concepts/plugin-model.md) | explanation | standard | active |
| `docs.decisions.0001` | [ADR-0001 用 repo-kit v2 治理跨插件文档](../decisions/ADR-0001-adopt-repo-kit.md) | explanation | decision | active |
| `docs.decisions.0002` | [ADR-0002 升级到 repo-kit v2.2](../decisions/ADR-0002-v2-2-upgrade.md) | explanation | decision | active |
| `docs.decisions.index` | [决策索引](../decisions/index.md) | reference | index | active |
| `docs.host-notes` | [Command Palette 宿主笔记（版本与 API 约束）](../host-notes.md) | explanation | standard | active |
| `docs.index` | [跨插件文档索引](../index.md) | reference | index | active |
| `docs.install` | [安装与更新（零残留流程，通用）](../install.md) | how-to | standard | active |
| `docs.release` | [发版规则](../release.md) | how-to | standard | active |
| `docs.troubleshooting` | [排障（Command Palette 扩展通用）](../troubleshooting.md) | how-to | standard | active |
