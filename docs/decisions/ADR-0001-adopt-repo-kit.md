---
id: docs.decisions.0001
title: ADR-0001 用 repo-kit v2 治理跨插件文档
summary: 先治理根 docs/ 与跨插件机制文档，插件内文档用 baseline 渐进纳入。
type: explanation
kind: decision
status: active
owner: "@simiely"
topics: [governance]
source_type: doc
source: tools/repo-kit/check_repo.py
last_reviewed: 2026-11-11
related: []
---
# ADR-0001 用 repo-kit v2 治理跨插件文档

- Status: Accepted
- Date: 2026-11-11
- Owners: @simiely

## Context

仓库已有 24 个 Markdown 文件、两个插件、四个跨插件机制文档（install / troubleshooting /
host-notes / release），但没有任何机器可读的 frontmatter。文档靠人工维护，存在三类风险：

- 命令、报错表、清单在多个文件里复制，改一处漏一处；
- 文档链接和锚点没有检查，改标题后会软断链；
- 没有 owner / 复核窗口，过期结论无法被发现。

直接用全量规范会第一天全红：24 个文件都缺 frontmatter，插件内还有 87KB 的 design.md 和若干
一次性报告。全量迁移的成本大于收益。

## Decision

采用 repo-kit v2 的「四平面 × 三表面」，但**分阶段**：

1. **Phase 1 范围**：根 `README.md` + `docs/**`，共 4 个机制文档 + 新增索引/概念/ADR。
   `plugins/**` 与 `shared/**` 暂不进入扫描范围。
2. **内容契约**：给 4 个机制文档补 frontmatter；`type` 用 Diátaxis，`kind` 用 standard/decision/index。
3. **生成优先**：`AGENTS.md`、`llms.txt`、`docs/_generated/index.md`、`docs/_generated/backlinks.md`
   全部由 `tools/repo-kit/generate.py` 生成。
4. **渐进执行**：用 `--write-baseline` 冻结 Phase 1 之外的遗留问题；CI 只对新增/修改文件报 ERROR。
5. **目录链接**：根 README 里指向 `plugins/.../src` 的链接是合法导航，降为 WARN，不阻塞。

## Consequences

正面：

- 4 个机制文档有了 owner、复核窗口、真源和类型；新增页面必须合规。
- 生成索引/反链/AGENTS/llms.txt，减少手写导航与重复清单。
- 插件内文档不被强行改造；按插件逐步纳入。

代价：

- Phase 1 只覆盖跨插件文档，插件内文档仍是「未治理区」，需要后续 ADR 决定何时纳入。
- `source` 是页面级路径存在性校验，不能证明「这句话是真的」。
- 根 README 的目录链接会产生 WARN；需要人工确认不是误链。

## Verification

```bash
python tools/repo-kit/check_repo.py --root .
python tools/repo-kit/generate.py --root . --check
# drift_probe 是 kit 自测，不随本仓库分发；在 kit 源仓库运行
```

初始审计：24 个 `FM_MISSING`、2 个 `LINK_DIR`、1 个 `TRUTH_MISSING`、4 个 `GENERATED_MISSING`。
Phase 1 迁移后，跨插件文档应达到 `ERROR=0`；插件内文档的遗留问题用 baseline 冻结。

## NOT RUN

- 未迁移 `plugins/**` 下的 19 个 Markdown 文件（含 87KB design.md 与一次性报告）。
- 未接真实 git 历史做 `source_commit` 校验。
- 未验证中文/CJK 锚点在 GitHub/Docusaurus/MkDocs 的差异。
- 未接 CI；workflow 文件已随包提供，需在仓库启用后再观察一轮。

## Supersession

None。要改本决策，写新 ADR 并标 `Supersedes: ADR-0001`，本文只翻 status，正文不动。
