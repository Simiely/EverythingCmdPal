# CmdPalLinks

PowerToys Command Palette 的「链接库」扩展：把常用的网址、文件、文件夹收进一个列表，在面板里即时检索并打开。

> 插件目录：`plugins/CmdPalLinks/`。仓库总览见根 [README](../../README.md)。

## 是什么

- 顶层只有 1 条命令「链接库」，副标题显示条目数；「添加条目」是页内入口（空态主按钮 + 每行 `MoreCommands` 首项）。
- 支持 URL / 文件 / 文件夹三类条目；标题、别名、标签、域名、文件名、完整目标都参与模糊检索；空查询按最近使用排序。
- 条目动作：打开所在文件夹、复制目标、编辑、删除；文件/文件夹失效时标 `⚠ 路径不存在` 与「缺失」标签，不自动删除。
- 设置项：布局（列表 / 中网格）、显示完整路径、显示副标题、显示标签，改动即时生效并落盘。
- 数据：包内 `LocalState/links.json` 单文件 JSON（原子写 + `.bak` 回退），索引在内存构建。
- 批量编辑：列表末项「批量编辑」启动扩展进程内的回环 HTTP 服务并打开浏览器编辑器，加载即显示现有条目，
  保存直接写回 `links.json`（随机端口 + 一次性 token，只绑 `127.0.0.1`，端口与 token 不落盘、不写日志；空闲 20 分钟自动关闭）。
- 导入/导出：CSV（`type,target,title,alias,tags,emoji,color,icon`，旧版 7 列仍可导入）/ JSON，两段式预览后确认导入，可撤销上次导入。
- 图标：自定义 emoji / 色块 / 透明底由 GDI 自绘为单色 PNG（彩色 emoji 字形为已知限制）；无自定义图标时用站点 favicon / 类型字形 / 首字色块。

## 安装

```powershell
# 构建 → 打包签名 → 快照 LocalState → 停 CmdPal 与扩展 → 卸载 → 安装 → 恢复 → 校验 → 重开
powershell -NoProfile -ExecutionPolicy Bypass -File plugins\CmdPalLinks\tools\install.ps1

# 只打包不安装
powershell -NoProfile -ExecutionPolicy Bypass -File shared\build\package.ps1 -Plugin CmdPalLinks
```

通用机制与禁止项：[docs/install.md](../../docs/install.md)；本插件标识、PFN、LocalState 期望内容：[docs/install-notes.md](docs/install-notes.md)。

## 使用

1. 打开 Command Palette（`Win+Alt+Space`）；
2. 输入「链接库」进入列表，或直接输入标题/别名/标签检索；
3. 在列表里按 `添加条目` 新建；对已有条目用右键菜单（`MoreCommands`）编辑/复制/打开所在文件夹/删除；
4. 批量维护用列表末项「批量编辑」：浏览器打开编辑器，改完点「保存到插件」即写回；CSV 导入/导出仍可在编辑器内或行内进行。

## 排障

- 先看 [docs/troubleshooting.md](../../docs/troubleshooting.md) 的三层取证（包 / 持久缓存 / UIA 渲染）。
- 数据坏了：`links.json` 读取失败会自动回退 `links.json.bak`；两者都坏则空库且不覆盖坏文件。
- 常见安装错误码（`0x80073CFB` / `0x80073CFA` / `0x80073D02`）见同名文档 §4。

## 开发

```powershell
# 构建（0 警告 0 错误为基线）
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build plugins\CmdPalLinks\src\CmdPalLinks\CmdPalLinks.csproj -c Debug

# 图标资源（单源生成 Assets 下的 PNG）
python plugins\CmdPalLinks\tools\gen-icons.py

# 存储层 / 编辑器接口自测（临时目录，不碰用户数据）
dotnet build plugins\CmdPalLinks\tests\StoreHarness\StoreHarness.csproj
# <dir> editor <seed links.json>：真实 HTTP 接口读 7 条 + 加/删 + 改图标写回
# <dir> csv：8 列（含 icon）往返 + 旧版 7 列兼容
```

文档：[design.md](docs/design.md)（调研与方案）、[spec.md](docs/spec.md)（实施规格）、[tasks.md](docs/tasks.md)（任务清单）、[self-test.md](docs/self-test.md)（自测步骤）、[ux-icon-sort-plan.md](docs/ux-icon-sort-plan.md)（M3 交互方案）。

## 发版

按 [docs/release.md](../../docs/release.md)：tag `cmdpallinks-v<版本>`，资产 `CmdPalLinks-<版本>.msix` + `.sha256`，
`Identity Version` 每次递增（当前 `0.1.4.0`）。变更记录见 [CHANGELOG.md](CHANGELOG.md)。
