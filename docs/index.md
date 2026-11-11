---
id: docs.index
title: 跨插件文档索引
summary: install / troubleshooting / host-notes / release 的导航枢纽。
type: reference
kind: index
status: active
owner: "@simiely"
topics: [governance]
source_type: doc
source: README.md
last_reviewed: 2026-11-11
related: ["docs.install:see-also", "docs.troubleshooting:see-also", "docs.host-notes:see-also", "docs.release:see-also", "docs.concepts.plugin-model:see-also", "docs.decisions.0001:see-also"]
---
# 跨插件文档索引

跨插件的通用机制文档在这里。插件自己的文档仍在 `plugins/<Name>/docs/`，不复制到本目录。

## 机制

- [安装与更新](install.md) — 零残留流程、禁止项、三层核对
- [排障](troubleshooting.md) — 症状→先查什么、错误码对照
- [宿主笔记](host-notes.md) — 版本基线、0.11/0.13 差异、API 边界
- [发版规则](release.md) — tag/资产命名、独立版本、发布前检查表
- [插件自包含单元模型](concepts/plugin-model.md) — 目录、隔离、共享与禁止项

## 导航与决策

- [生成索引](_generated/index.md) — 从 frontmatter 自动生成
- [反向链接](_generated/backlinks.md) — 从 `related` 派生
- [决策索引](decisions/index.md) — ADR
- [仓库总览](../README.md) — 插件索引与按任务找入口
- [EverythingCmdPal](../plugins/EverythingCmdPal/README.md)
- [CmdPalLinks](../plugins/CmdPalLinks/README.md)

## 范围

Phase 1 只治理根 `docs/` 与跨插件机制文档。插件目录 `plugins/**` 暂不进入扫描范围；
迁移时按插件逐个纳入，避免一次改动 87KB 的 design.md。
