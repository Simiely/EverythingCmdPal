---
id: docs.release
title: 发版规则
summary: tag/资产命名、按插件独立递增版本、发版步骤与发布前检查表。
type: how-to
kind: standard
status: active
owner: "@simiely"
tier: critical
topics: [release]
defines: release-guide
source_type: code
source: shared/build/package.ps1
last_reviewed: 2026-11-11
related: ["docs.install:depends-on"]
---

# 发版规则

> 已有历史：tag `v1.3.4` + Release `EverythingCmdPal-1.3.4.zip`（单插件时代的命名，**保留不动**）。

## 1. 命名

| 项 | 规则 | 示例 |
| --- | --- | --- |
| Tag | `<plugin 小写>-v<Identity Version>` | `cmdpallinks-v0.1.3.0`、`everythingcmdpal-v1.3.5` |
| Release 标题 | `<Plugin> <Version>` | `CmdPalLinks 0.1.3.0` |
| 资产 | `<Plugin>-<Version>.msix` + `<Plugin>-<Version>.msix.sha256` | `CmdPalLinks-0.1.3.0.msix` |
| 源码包（可选） | GitHub 自动生成 | 不用手工上传 zip |

- Tag 前缀与插件目录名一一对应（小写），由 `plugin.psd1` 的 `TagPrefix` 给出。
- **不要**再用无前缀的 `v1.3.4` 形式：两个插件会撞号，`git log` 也无法从 tag 判断归属。
- `v1.3.4` 是历史遗留，保留即可；新的 EverythingCmdPal 版本用 `everythingcmdpal-v*`。

## 2. 版本号

- `<Identity Version>` **按插件独立递增**，与仓库无关；两个插件可以同为 `0.1.3.0`，互不影响
  （MSIX 版本是包级属性，`Identity Name` 不同即隔离）。
- 同一插件每次构建必须递增（否则 `0x80073CFB`），见 [install.md](install.md) §3。
- Release 的版本必须与 `Package.appxmanifest` 的 `Identity Version` 完全一致；
  发版前用 `shared/build/package.ps1` 打包，产物名自动带版本。

## 3. 发版步骤

```powershell
# 1) 递增版本并打包签名（产物：plugins\<Plugin>\package\<Plugin>-<版本>.msix）
powershell -NoProfile -ExecutionPolicy Bypass -File shared\build\package.ps1 -Plugin CmdPalLinks -Bump

# 2) 实机安装验证（复用同一流程；不装的话至少要确认包能 Add-AppxPackage）
powershell -NoProfile -ExecutionPolicy Bypass -File plugins\CmdPalLinks\tools\install.ps1 -NoBump

# 3) 生成校验文件
Get-FileHash plugins\CmdPalLinks\package\CmdPalLinks-<版本>.msix -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  CmdPalLinks-<版本>.msix" } |
    Set-Content plugins\CmdPalLinks\package\CmdPalLinks-<版本>.msix.sha256
```

4. 提交 `package.appxmanifest` 的版本变更；
5. `git tag <plugin>-v<版本>` → `git push origin <plugin>-v<版本>`；
6. 在 GitHub 创建 Release，上传 `.msix` 与 `.sha256`，正文引用该插件 `CHANGELOG.md` 的对应段落。

## 4. 禁止

- 不 force push、不改写已推送历史；tag 一旦发布不再移动。
- 不把 `.pfx/.cer/.pvk`、`.testdata/`、`package/` 提交进仓库。
- 不在没有实机安装验证的情况下宣告可用（至少验证：包 `Status: Ok` + 首页 1 条）。
- 不改仓库名来「整理」：会影响 remote URL 与所有既有 Release 链接，需单独决策。

## 5. 发布前检查表

- [ ] `<Identity Version>` 已递增且与 Release 版本一致
- [ ] `dotnet build` 0 警告 0 错误
- [ ] `makeappx` / `signtool` 成功，产物名带版本号
- [ ] 实机安装后 `Get-AppxPackage <Name>` 1 条、`Status: Ok`
- [ ] `LocalState` 哈希与安装前一致（有用户数据时）
- [ ] 首页同名条目 1 条（UIA）
- [ ] `CHANGELOG.md` 已补对应版本段落
