---
id: docs.decisions.0002
title: ADR-0002 升级到 repo-kit v2.2
summary: 启用 context/health 豁免与同 PR co-change；Phase 2 baseline 从 22 降到 16/15。
type: explanation
kind: decision
status: active
owner: "@simiely"
topics: [governance]
source_type: doc
source: scripts/check_repo.py
last_reviewed: 2026-11-11
related: []
---
# ADR-0002 升级到 repo-kit v2.2

- Status: Accepted
- Date: 2026-11-11
- Owners: @simiely

## Context

v2.1 的 Phase 1 已经让跨插件文档达到 `ERROR=0`，但真实仓库暴露了三个缺口：

- `plugins/EverythingCmdPal/AGENTS.md` 是 monorepo 的局部上下文文件，却被当内容页；
  插件 README/CHANGELOG 也被要求 frontmatter。
- `Package.appxmanifest` 的版本变了，README/CHANGELOG 没有机器检查。
- 插件目录下有 14 份一次性报告/计划，全部逼成 evergreen 是反效果。

v2.2 把这些能力做成 opt-in，默认不改变 v2.1 行为，因此可以在本仓库启用而不重写文档。

## Decision

1. 配置 `context_files: ["AGENTS.md"]`：任意层级的 AGENTS.md 是上下文文件，不再是内容页。
2. 配置 `exempt_basenames: ["README.md","CHANGELOG.md","CONTRIBUTING.md","SECURITY.md"]`：
   健康/产物文件不强制 frontmatter，但仍检查链接。
3. `schema/sources.json` 增加 manifest → README/CHANGELOG 的 `co_change` 映射：
   版本变更必须同 PR 更新这两份文档。
4. `summary_policy: "warn"`：standard/index/decision 缺 summary 时给 WARN，不阻塞。
5. `slugger: "github"`：中文标题按 GitHub 风格生成锚点；站点 build 仍是最终裁判。
6. Phase 2 的 14 份插件文档继续用 baseline 冻结；先选一份 `m0-findings.md` 标
   `kind: report` + `status: archived` 作为样板，不强制全量迁移。

## Consequences

- Phase 2 baseline：22 → 16（14 个 `FM_MISSING` + 2 个目录链接）。
- 再把 `m0-findings.md` 标为 `report/archived` 后：15（13 个 `FM_MISSING` + 2 个目录链接），
  且 2020 年的 `last_reviewed` 不触发 `STALE`。
- manifest 单独变更会触发 `TRUTH_DOCS_NOT_UPDATED`；带上 README 或 CHANGELOG 后通过。
- 插件源码未改；插件文档没有强制迁移。

## Verification

```bash
python scripts/check_repo.py --root .
python scripts/generate.py --root . --check
python scripts/check_repo.py --root . --changed-files changed.txt
```

- Phase 1：`ERROR=0, WARN=2, pages=9`。
- Phase 2：baseline 16/15，带 baseline `ERROR=0`。
- co-change：manifest 单独变更 → `TRUTH_DOCS_NOT_UPDATED`；manifest + README → PASS。
- SARIF：2 个 warning 结果，包含 `ruleId`、`location`、`partialFingerprints`。

## NOT RUN

- 未迁移其余 13 份插件文档；仍在 baseline。
- 未接 GitHub code scanning、zhlint、MkDocs/Docusaurus build。
- 未验证中文/CJK 锚点在 GitHub/Docusaurus/MkDocs 的差异。
- README 插件表的版本列仍靠人工；表格生成/比对是 Phase 3。

## Supersession

None。要改本决策，写新 ADR 并标 `Supersedes: ADR-0002`，本文只翻 status，正文不动。
