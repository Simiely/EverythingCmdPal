# AGENTS.md — EverythingCmdPal 仓库入口

> 生成文件，勿手改。改规则请改 `schema/agents.template.md` 或 `tools/repo-kit/generate.py`，再运行 `python tools/repo-kit/generate.py --root . --write`。

这是本仓库给 AI 与人的**唯一入口**。先读 [README](README.md) 的插件索引；跨插件机制见
[docs/index.md](docs/index.md)；安装见 [docs/install.md](docs/install.md)；排障见
[docs/troubleshooting.md](docs/troubleshooting.md)；宿主约束见 [docs/host-notes.md](docs/host-notes.md)；
发版见 [docs/release.md](docs/release.md)。

## 硬约束
1. 改真源（`shared/build/*.ps1`、插件源码、MSIX 清单）时，**同一个 PR** 带上所有引用它的文档。
2. 跨插件机制只写在根 `docs/`；插件特有数值留在插件自己的 `docs/install-notes.md`。
3. 同一命令、报错表、清单只允许一个权威位置；别处链接，不复制。
4. 禁止写回 backlinks；反向链接由 `tools/repo-kit/generate.py` 生成。
5. Phase 1 只扫描 `README.md` + `docs/**`；新增跨插件文档必须带 frontmatter。插件内文档暂不强制。
6. 未经用户确认不装软件、不付费、不对外发送、不改生产库。

## 常用命令
```bash
python tools/repo-kit/check_repo.py --root .
python tools/repo-kit/generate.py --root . --check
```

## 当前规模
- 页面：9（critical=5）
- 按 type：explanation=4, how-to=3, reference=2
- 词表版本：2.3.2

## 验收
`REPO CHECK: PASS`（ERROR=0）。检查器验证不了「这句话是不是真的」；改安装脚本 / 宿主版本 /
发版流程时，需要人把文档与实际脚本、实机日志并排读一遍。

## 边界
机器只保结构、引用、新鲜度与真源路径；语义正确性靠生成 + 人审。不要把本文件写成第二本规范。
