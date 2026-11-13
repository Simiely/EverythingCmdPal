# CmdPalLinks

PowerToys Command Palette 的「链接库」扩展：把常用的网址、文件、文件夹收进一个列表，在面板里即时检索并打开。

> 插件目录：`plugins/CmdPalLinks/`。仓库总览见根 [README](../../README.md)。

## 是什么

- 顶层只有 1 条命令「链接库」，副标题显示条目数；「添加条目」是页内入口（空态主按钮 + 每行 `MoreCommands` 首项）。
- 两层文件夹：首页默认是一级文件夹「网页链接」，进入后第一位固定为「常用」
  （原首页散落的 68 条，合并与清理后保留 65 条），其后是 13 个分类文件夹（原顺序 / 名称不变），可继续下钻到第三层，返回由宿主页面栈负责。
  条目 `folder` 存实际完整路径（如 `网页链接/常用`），不用虚拟聚合，因此不会重复显示或重复计数；
  「常用」名称与第一位置固定，不可改名 / 删除 / 后移。子列表与主列表共用「列表 / 网格」布局设置；
  分类文件夹可重命名、可删除（先显示影响条数，删除只把条目移入「常用」）；条目更多菜单可「移动到文件夹」。
  首页（「网页链接」外）也支持散项与额外顶层文件夹：列表尾部可直接「添加条目」（folder 留空即未分类），
  「新建文件夹」可选「网页链接」内或顶层（网页链接 外）；顶层文件夹可改名、删除，删除后条目回落到未分类。
- 支持 URL / 文件 / 文件夹三类条目；标题、别名、标签、域名、文件名、完整目标都参与模糊检索；空查询按最近使用排序。
- Edge 收藏夹已只读并入（418 条），与原有 261 条一起做 URL 归一化去重、逐条可达性探测和重新分类：
  100 条重复未导入、15 条真 404 删除；132 条 401/403/429/超时/TLS 等原先只按状态码判定的项，
  0.1.10.0 又用 headless Edge 逐条真实渲染复核——删除 4 条明确错误页 / 停放页，45 条保留、79 条保留·需人工、4 条疑似未删；
  0.1.11.0 先实测「核几轮才靠谱」（确定性失败翻转率 0%、抖动型 5.97%），再按「确定性 1–2 轮 / 抖动 3 轮、30s 超时、90s 间隔」
  重核那 79 条：0 删除、12 条证实抖动可达、63 条仍需人工，4 条已删对照维持删除；
  最终 **558** 条，13 个分类名称与顺序不变，「常用」仍在第一位且保持 65 条。
  明细见 [edge-merge-report.md](docs/edge-merge-report.md)、[browser-recheck-report.md](docs/browser-recheck-report.md)
  与 [link-multiround-report.md](docs/link-multiround-report.md)。
- 条目动作：打开所在文件夹、复制目标、编辑、删除；文件/文件夹失效时标 `⚠ 路径不存在` 与「缺失」标签，不自动删除。
- 设置项：布局（列表 / 中网格）、显示完整路径、显示副标题、显示标签，改动即时生效并落盘。
- 数据：包内 `LocalState/links.json` 单文件 JSON（schema 3，原子写 + `.bak` 回退）；条目带独立 `folder` 字段，
  顶层 `folders` 保存文件夹顺序与空文件夹，索引在内存构建。
- 批量编辑：列表末项「批量编辑」启动扩展进程内的回环 HTTP 服务并打开浏览器编辑器，加载即显示现有条目与文件夹结构，
  保存直接写回 `links.json`（随机端口 + 一次性 token，只绑 `127.0.0.1`，端口与 token 不落盘、不写日志；空闲 20 分钟自动关闭）。
  编辑器按 macOS 风格分为「文件夹侧栏 / 条目主区 / 编辑详情」三区，支持新建 / 重命名 / 删除文件夹、调整文件夹顺序
  （「常用」固定在第一位）、条目批量移动与「设为常用」，覆盖 hover / 选中 / 焦点 / 禁用状态，空态与长标题都有兜底显示。
  侧栏顶部「未分类（网页链接 外）」可查看 / 新增 / 编辑不归属任何文件夹的条目；顶层文件夹与「网页链接」同级显示，
  可改名、删除；选中「网页链接」时只显示它自己的子树，不会把未分类或顶层文件夹的条目混进来；
  新建文件夹可选顶层或「网页链接」内；保存会提交完整文件夹顺序，不再把空 folder 强塞进「常用」。
- 导入/导出：CSV（`type,target,title,alias,tags,emoji,color,icon,folder,iconSrc`，旧版 8 / 7 列仍可导入）/ JSON，两段式预览后确认导入，可撤销上次导入。
- 图标：自定义 emoji / 色块 / 透明底由 GDI 自绘为单色 PNG（彩色 emoji 字形为已知限制）；
  条目可带 `iconSrc` 远程图片（如 iTab 导入的 `src`），无可用图片时回落站点 favicon / 类型字形 / 首字色块。

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
3. 在列表里按 `添加条目` 新建；首页的入口默认放「网页链接」外（未分类），文件夹页的入口默认放当前文件夹；
   对已有条目用右键菜单（`MoreCommands`）编辑 / 复制 / 打开所在文件夹 / 删除 / 移动到文件夹；
4. 打开链接库先看到一级文件夹「网页链接」，进入后第一项是「常用」，其后为 13 个分类；
   首页也可新建顶层文件夹或直接放未分类条目；`Esc` / 返回键回上一层，搜索框输入任意关键词会跨所有分组检索；
5. 批量维护用列表末项「批量编辑」：浏览器打开编辑器，左侧改文件夹关系、中间选条目、右侧改详情，改完点「保存到插件」即写回；
   JSON 导入/导出在编辑器工具栏（导入会替换当前列表，点「保存到插件」后写回）；设置页仍保留 CSV / JSON / TXT 导入与 JSON / CSV 导出。

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
# <dir> csv：10 列（含 folder / iconSrc）往返 + 旧版 8 / 7 列兼容
```

文档：[design.md](docs/design.md)（调研与方案）、[spec.md](docs/spec.md)（实施规格）、[tasks.md](docs/tasks.md)（任务清单）、[self-test.md](docs/self-test.md)（自测步骤）、[ux-icon-sort-plan.md](docs/ux-icon-sort-plan.md)（M3 交互方案）。

## 发版

按 [docs/release.md](../../docs/release.md)：tag `cmdpallinks-v<版本>`，资产 `CmdPalLinks-<版本>.msix` + `.sha256`，
`Identity Version` 每次递增（当前 `0.1.17.0`）。变更记录见 [CHANGELOG.md](CHANGELOG.md)；
场景走查报告见 [docs/walkthrough/2026-11-09-cmdpallinks.md](docs/walkthrough/2026-11-09-cmdpallinks.md)。
