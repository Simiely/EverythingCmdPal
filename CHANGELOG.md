# CHANGELOG · 仓库

插件版本各自记录在 `plugins/<Name>/CHANGELOG.md`（按插件的 MSIX `Identity Version` 分节）；本文件只记仓库级变更：文档治理、构建脚本、共享机制。

| 插件 | 当前版本 | 变更记录 |
|---|---|---|
| EverythingCmdPal | `1.3.4` | [CHANGELOG](plugins/EverythingCmdPal/CHANGELOG.md) |
| CmdPalLinks | `0.1.17.0` | [CHANGELOG](plugins/CmdPalLinks/CHANGELOG.md) |

## 2026-11-16 · 升级 repo-kit 2.5.3 / 规范 2.1.2

- 重装 kit：`check_repo.py` / `generate.py` 换代，`schema/kit.lock.json` 写入 `v2.5.3 / spec 2.1.2`
- `schema/taxonomy.json` 首次把 `DEVELOPMENT.md` 与 `CHANGELOG.md` 纳入扫描
  （此前四件套里这两份不在 `include`，等于从未被检查；插件目录仍按 Phase 1 惯例不扫）
- `check_repo.py` 新增 `--prune-baseline`，本仓库无 baseline，暂不使用
- CI 无需改动：本仓库此前已手工补过 `fetch-depth: 0` 与 PR 的 `--changed-since`，
  `requirements.txt` 本身就是文档依赖
- 验收：`REPO CHECK: PASS`（ERROR=0、WARN 2 → 0、pages=9）、`GENERATE CHECK: PASS`、
  `KIT LOCK: PASS (v2.5.3, spec=2.1.2)`

## 2026-11-13 · 文档治理收尾

- 根目录补齐四件套：新增本文件与 [DEVELOPMENT.md](DEVELOPMENT.md)（概览 / 架构 / 关键问题门面）
- `AGENTS.md` 增加文档基线（日期 + commit hash），符合单项目规范第十节
- `schema/sources.json` 补 CmdPalLinks 的 manifest 真源映射；manifest 的 `co_change` 文档集合加入插件 README
- CI 增加 PR 增量 co-change 校验；lychee 内部链接检查覆盖 `DEVELOPMENT.md` / `CHANGELOG.md`
- 修正版本漂移：CmdPalLinks 在根 README 的 `0.1.2.0`、插件 README 的 `0.1.16.0` 统一为 `0.1.17.0`

## 2026-11-11 · 跨插件文档治理 v2.2

- 引入 repo-kit v2.2：`schema/` 契约、`tools/repo-kit/` 校验与生成、`repo-check` CI、`kit.lock` 版本锁
- 根 `docs/` 纳入扫描，插件内文档延后（见 [ADR-0002](docs/decisions/ADR-0002-v2-2-upgrade.md)）

## 2026-10 · 多插件重组

- 单插件仓库重组为 `plugins/<Name>/` 自包含单元 + `shared/build/`
- 原根目录四件套下沉：`README`/`CHANGELOG` 进插件目录，`DEVELOPMENT` 改名为 `plugins/EverythingCmdPal/docs/development.md`
