# Changelog — CmdPalLinks

格式：`<版本> · <日期>` + 变更点。版本与 `Package.appxmanifest` 的 `Identity Version` 一致。

## 0.1.16.0 · 2026-11-09

- 修复编辑器选中「网页链接」时仍把外部条目算进来：`currentItems` 对 ROOT 从「全库」改为 `isUnder(folder, 网页链接)`
  只取自身子树；列表计数改为 `countInTree(网页链接)`。未分类与顶层文件夹的条目不再出现在「网页链接」列表里。
- 子文件夹列表的计数改为本层直接条目数，与列表实际显示的行数一致（树节点仍显示整棵子树计数）。
- 验证：headless mock 用例（网页链接 3 条 / 未分类 1 条）与真实 C# 后端用例（网页链接 556 条 / 未分类 2 条，
  当前用户库实际数量）均通过；真实后端保存后 559 条 / 16 文件夹，未分类 3 条。
- 安装实测：`0.1.16.0`（Release，标准流程；LocalState 657 文件快照 SHA256 全一致，`Status: Ok`）。

## 0.1.15.0 · 2026-11-09

- 按要求再走查 2 轮（第 2/3 轮），新增 7 个问题，全部修复；累计 17 个问题（2 个真缺陷 / 其余 UX、一致性、可操作点）。
  报告已补第二轮/第三轮：见 [docs/walkthrough/2026-11-09-cmdpallinks.md](docs/walkthrough/2026-11-09-cmdpallinks.md)。
- **P-16（真缺陷）**：`LibraryStore.Add` / `Update` 只规范化 folder，不校验 64 字符/控制字符；CmdPal 表单可把非法 folder 写进条目。
  补 `IsValidFolderName` 校验，非法时返回 `InvalidTarget`，不落盘。
- P-17：`ImportBatch` 遇到非法 folder 不跳过；改为计入 skipped，不污染数据。
- P-11：`FolderItemsPage` 无条件追加「添加到此文件夹」，空文件夹不再显示 EmptyContent 的空态提示；改为有行才追加。
- P-12：编辑器新建/改名/CSV 导入未校验 folder 长度；新增 `folderValidationError`（每段 ≤64、总长 ≤256、禁控制字符），
  创建/改名/导入都在前端拦下，不再等保存失败。
- P-13：CmdPal 含子文件夹的删除菜单仍写「条目会移入常用」；改为「含子文件夹，需先删除子文件夹」。
- P-14：编辑器搜索无结果时空态仍写「这个文件夹还没有条目」；改为「没有匹配的条目 / 换个关键词」。
- P-15：新建文件夹只有「网页链接内 / 顶层」；新增「当前文件夹内」并作为选中文件夹时的默认位置。
- 回归：`node --check` 通过；headless Edge 用例新增覆盖超长 folder 拦截、搜索无结果空态、当前文件夹内新建子文件夹；
  原有勾选、批量目标保持、搜索清空选择、嵌套树、含子级拒绝删除、顶层改名/删除、未分类新建、保存 payload 全部通过。
- 安装实测：`0.1.15.0`（Release，标准流程；LocalState 657 文件快照 SHA256 全一致，`Status: Ok`）；
  扩展日志 `Started extension CmdPalLinks_0.1.15.0` / `store loaded: 558 items, 15 folders`。

## 0.1.14.0 · 2026-11-09

- 收尾场景走查的 3 个极低风险项：
  - P-08：批量目标下拉每次 `renderBulk` 被重置为「未分类」；把目标存进 `state.bulkTarget`，重建后恢复。
  - P-09：搜索过滤时已勾选但被隐藏的条目仍参与批量；搜索词变化时清空 `state.selectedIds`。
  - P-10：新条目临时 id 为 `new-<timestamp>`，同一毫秒连续新建可能撞 id；追加递增序号 `newIdSeq`。
- 回归：`node --check` 通过；headless Edge 用例新增覆盖「批量目标保持」「搜索清空选择」，
  原有勾选、取消、行点击保持勾选、嵌套树、含子级拒绝删除、顶层改名/删除、未分类新建、保存 payload 全部通过。
- 走查报告已更新为 10 个问题全部修复，见
  [docs/walkthrough/2026-11-09-cmdpallinks.md](docs/walkthrough/2026-11-09-cmdpallinks.md)。

## 0.1.13.0 · 2026-11-09

- 按 workbuddy-skills `scenario-walkthrough` v3 对当前插件走查：12 条场景链（首页/文件夹页/编辑器/CSV/保存失败），
  发现 7 个问题（1 个真缺陷 + 6 个 UX/一致性/可操作点），全部修复。报告见
  [docs/walkthrough/2026-11-09-cmdpallinks.md](docs/walkthrough/2026-11-09-cmdpallinks.md)。
- **P-01（真缺陷）网页编辑器复选框用不了**：点击冒泡到整行 `onclick` → `selectItem` → `renderAll` 重建复选框，
  `change` 丢失，表现为勾不上并误触发详情选中。改为复选框 `click` stopPropagation，勾选只更新批量栏，不再重绘列表。
- P-02：首页有文件夹但 0 条目时，尾部「添加条目/新建文件夹/批量编辑」不渲染；条件改为 `Items > 0 || Folders > 0`。
- P-03：CmdPal 允许改名/删除「网页链接」，与编辑器「不可删除」不一致；`BuildFolderCommands` 与「常用」一起保护。
- P-04：删除文件夹确认页只数直接条目；改为 `CountInTree`，含子文件夹时明确提示先删子文件夹。
- P-05：编辑器重命名冲突检查大小写敏感；改为忽略大小写，与 Store 一致。
- P-06：编辑器删除父文件夹会连带子文件夹；改为含子级时拒绝，与 CmdPal 一致。
- P-07：编辑器文件夹树只渲染两级；改为递归渲染任意深度，名称含 `/` 创建的层级可见可操作。
- 回归：编辑器脚本 `node --check` 通过；headless Edge 用例通过（勾选 1→2、取消、点行保持勾选、含子文件夹拒绝删除、
  顶层改名/删除、未分类新建、保存 payload 含顶层与嵌套文件夹、空 folder 保留）；`dotnet build` 0 警告 0 错误。
- 安装实测：`0.1.13.0`（Release，标准流程；LocalState 657 文件快照 SHA256 全一致，`Status: Ok`）；
  扩展日志 `Started extension CmdPalLinks_0.1.13.0` / `Loaded 1 command(s)`，数据仍 558 条 / 15 个文件夹
  （其中 1 条 `D:\Tool` 位于网页链接外，`folder=""`，属新能力的合法未分类状态）。

## 0.1.12.0 · 2026-11-09

- 修复「网页链接」外面不能新增 / 修改的问题。此前首页尾部只有「新建文件夹」和「批量编辑」，没有「添加条目」；
  新建文件夹又对不含 `/` 的名称强制补 `网页链接/`；编辑器加载时把空 `folder` 强转成 `网页链接/常用`，保存只提交
  「网页链接 / 常用 / 二级文件夹」，导致网页链接外的散项和顶层文件夹无法新增，已有内容也会被移走或丢掉顺序。
- CmdPal：首页尾部新增「添加条目」（`folder` 留空 = 未分类）；每个文件夹页尾部新增「添加到此文件夹」（默认当前层）；
  「新建文件夹」表单增加「位置」，可选「网页链接」内（默认）或顶层（网页链接 外）；顶层文件夹删除后条目回落到未分类。
- 编辑器：侧栏顶部新增「未分类（网页链接 外）」；按 `folders` 顺序显示所有顶层文件夹及直接子级；新建文件夹可选顶层 / 网页链接 内；
  顶层文件夹可改名、删除（条目回落到未分类）；主区标题栏固定「＋ 新建条目」；检查器文件夹下拉与批量移动目标都含「未分类」；
  保存提交完整 `folders`（含顶层与空文件夹），空 `folder` 原样保留；CSV 空 folder = 未分类、非空按完整路径。
- 验证：`dotnet build` 0 警告 0 错误；headless Edge 编辑器用例通过（顶层文件夹新建 / 改名 / 删除、未分类新建条目、
  保存 payload 含顶层 folders 且新条目 `folder=""`）。
- 安装实测：`0.1.12.0`（Release，标准 `install.ps1 -NoBump`；LocalState 657 文件快照恢复 SHA256 全一致，`Status: Ok`）；
  扩展日志 `store loaded: 558 items, 15 folders`、`Loaded 1 command(s)`，数据未变。

## 0.1.11.0 · 2026-11-07

- 先做「一条链接核几遍才靠谱」实测：79 条样本 × 3 轮串行真实渲染（headless Edge、系统代理、单条 30s、轮间 90s，共 237 次导航、约 40.6 分钟），
  并按失败类型统计翻转率：确定性失败 **0/4 = 0%**、抖动型失败 **4/67 = 5.97%**、首轮成功 **5/8 = 62.5%** 后续又失败；
  同一 URL 跨轮翻转 **9/79 = 11.39%**。
- 结论：确定性失败（DNS / 410 / 停放页 / 明确 404）1–2 轮即可判定；抖动型（超时 / 重置 / TLS / 429 / 5xx / 拦截）需 3 轮、间隔 60–90s、超时 ≥30s。
  公开做法参照 lychee（默认 2–3 次重试、1–2s 等待）、markdown-link-check（retryOn429、retryCount 2–5、30s 退避）与 Screaming Frog 的「404 用浏览器复验」。
- 按该口径重核上一轮 79 条：4 条本机/局域网地址（localhost、192.168.*）跳过探测直接保留；75 条各 3 轮，
  **删除 0 条**（无任何条目 3 轮全部确定性失败），**12 条**至少 1 轮成功（含精靈學院 503→200→503、Steam 200→超时→200 等抖动可达），
  **63 条**保留·需人工（超时 21、连接被重置 10、资源拦截 23、5xx 3、4xx 2、TLS/空正文/重定向各 1）。
- 已删 4 条对照：3 轮全部确定性失败，维持删除、不恢复（`getitfree.cn` 404、`min-liu.com` 404、`btbat.com` nginx 404、`macapp.cc` 停放页）。
- 数据未变，仍 **558** 条；删除前仍备份 `links-before-multiround-20261107-122214.json` 与 `LocalState/links.json.multiround-bak`。
  校验：`total = sum = 558`、一级 1 / 二级 14、未分类 0、空标题 0、重复 URL 0，`errors 0`。
- 安装实测：`0.1.11.0`（Release，标准 `install.ps1 -NoBump`；LocalState 657 文件快照恢复 SHA256 全一致，`Status: Ok`）；
  `links.json` SHA256 `FA7A97E10F99A7F41A6D9070C849D416FFA704098B15E017C0E27936F31D449E`；
  UIA 首页 3 项、二级 14 项（常用第一）、14 个文件夹计数全部一致（BAD=0）。
- 方法、翻转率、79 条结局与证据见 [docs/link-multiround-report.md](docs/link-multiround-report.md)。

## 0.1.10.0 · 2026-11-07

- 上一轮 132 条「待确认」不再只看 HTTP 状态码，改用 **headless Microsoft Edge**（Playwright，临时 profile、不弹窗、不抢焦点、
  串行、单条 ≤15s、不加载图片/字体/媒体）逐条真实渲染，记录最终 URL、状态、页面标题、可见正文长度与错误页特征。
- 复核结果：**删除 4 / 保留 45 / 保留·需人工 79 / 疑似失效 4**。
  - 删除 4：`getitfree.cn`（LiteSpeed 404 首页）、`min-liu.com` 文章页（明确 404）、`btbat.com`（nginx 404 首页）、
    `macapp.cc`（域名停放/待售）。
  - 疑似 4（未删，待确认）：`bing.com/create`（跳转后 404，但属常用服务）、`rendercrate.com/3D/search`（404 空正文）、
    旧 mail.com 会话 URL 跳首页、威锋帖子跳「无此页面」。
  - 保留·需人工 79：15s 超时、连接被对端关闭、TLS 错误、资源/下载拦截、本机与局域网服务未启动；网络层失败不据此判死。
  - 反例修正：`srtku.com` 返回 404 但页面为「网站防火墙 + 验证码」→ 保留；`masuit.com` 迁移到 `masuit.net` 且为浏览器验证页 → 保留；
    `localhost` / `127.0.0.1` / `192.168.*` 的本机与局域网条目全部保留。
- 数据：562 → **558**（删除前备份 `links-before-browser-cleanup-20261107-102305.json`，另留 `LocalState/links.json.browser-bak`）。
  校验：总数 558 = 各文件夹之和；一级 1、二级 14（常用 65 + 13 分类）、未分类 0、空标题 0、重复 URL 0，`errors 0`。
  各文件夹：常用 65、本地工具 22、小工具 55、搜索 21、软件 75、信息 42、参考 28、素材 23、ai视频 6、教程 112、
  MCP 5、三维素材 40、视频&音乐 38、Ai 26。
- 安装实测：`0.1.10.0`（Release，标准 `install.ps1 -NoBump`；LocalState 656 文件快照恢复 SHA256 全一致，`Status: Ok`）；
  `links.json` SHA256 `9A793E6B086D4DD4552F37D18FD286623BFE1A3E73BAF5D45A699BC47D820BD5`。
  UIA：顶层 5 项 → 链接库首页 3 项；二级 14 项且「常用」第一；14 个文件夹计数全部与数据一致（BAD=0）。
- 逐条证据与可核对表见 [docs/browser-recheck-report.md](docs/browser-recheck-report.md)（132 行：URL / 最终 URL / 状态 / 标题 / 判定 / 依据）。

## 0.1.9.0 · 2026-11-07

- 只读并入 Edge 收藏夹：复制 `Profile 2\Bookmarks` 后解析（原文件不动），418 条（书签栏 416 / 其他书签 0 /
  移动收藏夹 2）、33 个文件夹节点、最大层级 4；标题、URL、添加时间保留，1 条 `javascript:` 伪书签跳过。
- 合并去重（覆盖全部原有 261 条）：URL 归一化后 97 个重复组（23 组完全相同、74 组同 URL 不同标题），
  Edge 100 条重复未导入，补别名 75 个；Edge 内部重复 3 组、旧库内部重复 0；52 个站点 / 181 个 URL 属同站不同页，只报告。
  合并后 578 条（原有 261 + Edge 独有 317，后者暂入 `网页链接/待分类`）。
- 失效清理：逐条探测 577 个 URL（覆盖全部旧条目），430 可达 / 128 待确认 / 19 明确失效；网络级失败 8.8%（未触发 30% 中止）。
  19 条 404 复核后删除 15 条真 404（原有 261 条中 6 条），4 条软 404 转入待确认；132 条 401/403/429/超时/TLS/5xx 不自动删。
  清理后 563 条；再补删 1 条 you.com Cloudflare 参数重复，最终 **562** 条。
- 重新分类：13 个分类名称与顺序不变，「常用」固定第一并保留 65 条精选；其余条目按域名 / 标题关键词 / 分类语义一条一文件夹。
  原有 261 条：清理 6、常用 65、重分类 190（17 条换分类、173 条维持原分类）。
- 校验：总数 562 = 各文件夹之和；一级 1、二级 14（常用 + 13）、未分类 0、空标题 0、归一化重复 URL 0；schema 3 与图标字段完整。
  完整明细与可复现证据见 [docs/edge-merge-report.md](docs/edge-merge-report.md)。
- 安装实测：`0.1.9.0`（Release 构建，标准 `install.ps1 -NoBump` 流程）；`links.json` SHA256
  `6BCC98E92A0263E12B129ECD887B6A3CB371EF18F2D8D7849C0E4D1DA08B18D3`。
  UIA：首页 3 项（网页链接 / 新建文件夹 / 批量编辑）；二级 14 项且「常用」第一；
  各二级条目实测：常用 65、本地工具 22、小工具 55、搜索 21、软件 77、信息 42、参考 29、素材 23、
  ai视频 6、教程 112、MCP 5、三维素材 41、视频&音乐 38、Ai 26，与数据一致。

## 0.1.8.0 · 2026-11-07

- 两层文件夹结构：首页只剩一个一级文件夹「网页链接」，不再直接出现 13 个分类或散项；进入后第一位固定为「常用」
  （原首页散落的 68 条），其后为 13 个分类文件夹（原顺序 / 名称 / 归属不变）。尾部「新建文件夹 / 批量编辑」保留。
- 文件夹改为**路径层级**：`folder` 存实际路径（`网页链接/常用`、`网页链接/本地工具`），顶层 `folders` 保存完整路径；
  `ChildFolderCounts` / `CountInTree` 按前缀取直接子级与整棵子树计数，「常用」是实文件夹、不做虚拟聚合，
  因此条目不会重复显示或重复计入 261。`FolderItemsPage` 支持递归下钻（文件夹里再放文件夹），返回由宿主页面栈负责。
- 文件夹操作语义：分类文件夹删除只解散分组、条目移入「常用」（不再产生未分类散项）；「常用」固定第一位，
  禁止重命名 / 删除，且不提供排序入口；重命名只改叶子名并保留父路径；新建文件夹默认落在「网页链接」之下。
- 数据迁移（迁移前备份 `LocalState/links.json.twolevel-bak` 与 `C:\Temp\cmdpaplinks-backup-20261107\links-before-twolevel.json`）：
  总条目 **261**、一级文件夹 **1**、二级文件夹 **14**（常用 + 13）、未分类 **0**；
  常用 68、本地工具 17、小工具 1、搜索 12、软件 38、信息 11、参考 16、素材 9、ai视频 5、教程 18、MCP 5、
  三维素材 28、视频&音乐 12、Ai 21。
- 编辑器（`Assets/links-editor.html`）整体重做为 macOS 风格三分区：左侧文件夹树 / 中间条目主区 / 右侧编辑详情，
  顶部工具栏（搜索、导入 CSV、导出 CSV、保存到插件），底部状态栏。文件夹树加载即显示一级「网页链接」、
  二级「常用」+ 13 分类与条数；支持新建、重命名（叶子名）、删除（提示条目移入「常用」）、↑↓ 调整顺序，
  「常用」常驻第一位且无排序 / 改名 / 删除入口；条目支持批量勾选移动、单条「设为常用」、右侧详情编辑。
  hover / 选中 / 焦点 / 禁用状态齐备，空态、超长标题、图标缺失均有兜底；单文件离线、无外部 CDN、系统字体栈。
- CSV 往返：导出 261 条（表头 `type,target,title,alias,tags,emoji,color,icon,folder,iconSrc`，BOM + CRLF），
  重新导入后仍为 **261 条 / 14 个二级文件夹**（常用 68），保存写回后 `links.json` 条目与各文件夹条数一致；
  旧 8 / 7 列仍可导入。
- 安装实测：`0.1.8.0`（`Status: Ok`）；`LocalState` 259 个文件快照恢复后 SHA256 全一致；
  首页 UIA `ListItem` **3** 项（网页链接 / 新建文件夹 / 批量编辑）；进入「网页链接」后 **14** 项，
  顺序为「常用」+ 本地工具 / 小工具 / 搜索 / 软件 / 信息 / 参考 / 素材 / ai视频 / 教程 / MCP / 三维素材 / 视频&音乐 / Ai，
  「常用」在第一位；「常用」内部 68 条。

## 0.1.7.0 · 2026-11-06

- 文件夹子页（`FolderItemsPage`）跟随设置里的「列表 / 网格」布局：`ApplyLayout()` 与主列表同一份逻辑
  （`grid-medium` → `MediumGridLayout { ShowTitle = true }`，否则 `null`），并监听设置变化即时重排。
  之前主列表是网格、子列表仍是普通行，属遗漏。
- 网格中每格使用既有图标规则（emoji / 文字图标 / favicon / 自绘），标题由宿主渲染在图标下方；
  条目的 `MoreCommands`（移动到文件夹 / 编辑 / 删除等）随 `ListItem` 保留，网格下仍可长按/右键触发；
  宿主负责滚动与列数（中号网格本次窗口宽度下实测每行 8 个，磁贴 100×100、图标约 48×44、标题在图标下方）。
- 「新建文件夹」「批量编辑」等尾部入口仍追加在链接库首页结果之后，网格下作为最后的磁贴保留，不会被挤掉。
- 安装实测：`0.1.7.0`（`Status: Ok`）；文件夹子页「本地工具」17 条在 `grid-medium` 下渲染为 8 列 × 3 行（8 + 8 + 1），
  UIA 可数到 17 个 `ListItem`，日志 `folder layout applied: grid-medium ... grid=True`。

## 0.1.6.0 · 2026-11-06

- 文件夹成为独立字段：条目新增 `folder`（空 = 未分类），`LibraryData` 升到 `schema 3` 并新增顶层 `folders`
  （保存顺序与空文件夹）；迁移旧文件时从条目前缀反推，不丢数据。与 `kind=folder`（本地目录）互不影响。
- 链接库页面：无检索词时文件夹以「文件夹名 + 条数」成行，点进去是子列表；子列表内可继续搜索，返回由宿主页面栈负责。
  文件夹行级 `MoreCommands`：重命名、删除（删除页先显示影响条数，确认后仅把条目移回未分类，条目不删）；
  条目更多菜单新增「移动到文件夹」（可选择未分类、已有文件夹或现场新建）。
- 新建文件夹入口放在链接库列表尾部（不在首页新增顶层命令）；受限说明：`IListPage` 没有页面级 Commands，
  页面级新增只能靠列表行承载。
- 编辑器：新增「文件夹」列（`datalist` 提示 + 逐行编辑可批量归入/改名），GET 返回 `folders`，保存时映射回 `folder`；
  CSV 表头升为 `type,target,title,alias,tags,emoji,color,icon,folder,iconSrc`（兼容旧 8 / 7 列），导入导出都保留文件夹。
- 新增 `iconSrc`：条目可记录远程图标地址（iTab `src`），`IconCache` 优先按 URL 缓存下载（30 天 TTL），失败/不支持时回落
  favicon → 自绘 → 首字。已知限制：SVG 无法用字节嗅探识别扩展名，这类 `src` 会回落 favicon。
- 按 iTab 原有分类导入（只读快照，未改 Edge/iTab 数据）：iTab `navConfig` 272 个叶子链接 `url` → 跳过 1 条非 http(s)
  （`itab://store`）→ 去重 12 条（1 条与现有库相同、11 条快照内重复，均跳过；现有条目无文件夹可补，合并 0 条）
  → **新增 259 条**，导入后 **261 条 / 13 个文件夹**、唯一网址 260。
  导入前备份：`LocalState/links.json.itab-bak` 与 `C:\Temp\itab-readonly-20261106\links-before-itab-import.json`。
  各文件夹条数：本地工具 17、小工具 1、搜索 12、软件 38、信息 11、参考 16、素材 9、ai视频 5、教程 18、
  MCP 5、三维素材 28、视频&音乐 12、Ai 21。
- 安装实测：`0.1.6.0`（`Status: Ok`，包 1 条）；`LocalState` 21 个文件快照恢复后 SHA256 全一致；
  首页 UIA `ListItem` 5 项（1 分组头 + 4 命令，名称「链接库」仅 1 项）；`links.json` 261 条、`schema=3`、13 文件夹。

## 0.1.5.0 · 2026-11-06

- 修复首页出现两条「链接库」：宿主的登记顺序是「先调 `TopLevelCommands()`、后把返回项 append 到顶层列表」，
  而扩展在启动加载完成时调用 `RaiseItemsChanged()`，宿主随即走 `UpdateCommandsForProvider` 抢在 append 之前
  插入一份新的 `TopLevelViewModel`，随后 append 又追加上登记时的那一份，最终同一 provider 出现两行。
  现在 `TopLevelCommands()` 首调用内同步等待存储加载并写好副标题（`共 N 条`），全程不再触发 provider 级
  `RaiseItemsChanged`（副标题靠 `INotifyPropChanged` 刷新）。顺带避免每次编辑条目都重建顶层项、
  `TopLevelViewModel.Id`（由标题+副标题派生）变化导致已绑定快捷键失效。
- 实测：`0.1.4.0` 首页 5 条命令（含 2 条「链接库」）→ `0.1.5.0` 4 条（「链接库」1 条）。
- 文档：时序根因、日志特征与 Id 连带约束写入 `docs/install.md` §9.10 / §9.11、`docs/troubleshooting.md` §2 层 4、
  `docs/host-notes.md` §3.1 与 §4、本插件 `docs/design.md` §10.9。

## 0.1.4.0 · 2026-11-06

- 修复「切回默认图标不生效」：`LibraryStore.ReplaceAll` 与 CSV `icon` 列在显式 `iconKind=auto/transparent` 时
  会强制清空残留的 `iconEmoji` / `iconColor`（此前非空 emoji 会把显式 `auto` 改回 `emoji`），
  编辑器「应用默认图标」同时清空行内 emoji。测试：`StoreHarness editor` / `csv` 覆盖「kind=auto + 残留 emoji/底色」。
- 编辑器图标改成两卡并列：`默认图标`（favicon / 色块首字 / 透明底）与 `自定义 emoji`，各自带「应用」按钮；
  生效项显示「使用中」并高亮，保存把生效项持久化为 `iconKind`，重开编辑器保持选中态。
- emoji 改为彩色：`Vortice.Direct2D1 3.8.3`（成熟 COM 封装）+ DirectWrite `EnableColorFont` + WIC 位图，
  在独立子进程 `CmdPalLinks.exe --render-emoji` 内渲染成 PNG（`SetErrorMode` 抑制错误框），宿主只读 PNG；
  无彩色字形/子进程失败/超时一律降级 GDI 单色轮廓。缓存 key 前缀改为 `r3|`，彩色产物落 `icons/color-*.png`。
- 移除此前手写 D2D/DWrite 的裸 P/Invoke 探针（曾在用户机器上两次 `0xc0000005`），不再保留任何会弹系统错误框的可执行文件。

## 0.1.3.0 · 2026-11-06

- 编辑器闭环：`批量编辑` 启动扩展进程内的回环 HTTP 服务（只绑 `127.0.0.1`、随机端口、一次性 token，不落盘/不写日志），
  页面与 API 同源；加载即显示现有条目，保存直接写回 `LocalState/links.json`（复用存储层原子写 + `.bak`），不再导出/导入。
- 列表末项改为「批量编辑」；删除「批量添加」入口及只为它服务的 `showBulkAdd` 设置项。
- 编辑器每行显示图标预览：emoji / 色块首字 / `auto` 站点 favicon（失败自动退回首字），保存成功/失败均有明确反馈。
- 修复 emoji 与中文首字被渲染成乱码：GDI `CreateFontW` / `DrawTextW` 的 P/Invoke 缺少 `CharSet.Unicode`，
  字符串先按 ANSI 转换再被 `DrawTextW` 按 UTF-16 解码（本机 ACP=936，emoji 变 `??` → 被渲染成 `U+3F3F` 等汉字）。
  同时给自绘图标缓存加渲染版本前缀，旧错误 PNG 不再命中。
- CSV 新增第 8 列 `icon`（`auto` / `block` / `transparent` / `emoji`），旧版 7 列文件仍可导入；
  修复导出文件的 UTF-8 BOM 导致字符串回导时表头校验失败；`block` 无底色保存后不再被降级成 `auto`。
- `tools/install.ps1` 修复快照恢复时把目录整体嵌套进 `LocalState` 的问题（改为按相对路径逐文件复制）。
- 已知限制：GDI 只能取 emoji 的单色轮廓（无法渲染 COLR 彩色字形），emoji 图标为单色；字形无墨迹时退回首字。

## 0.1.2.0 · 2026-11-06

- 安装/更新流程整改：改为「关 → 卸 → 装 → 启」，卸载前快照 `LocalState`、装后恢复并做 SHA256 校验，
  解决卸载删数据与 `0x80073CFA`（`-PreserveApplicationData` 对普通签名包不可用）。
- 每次构建递增 `<Identity Version>`，规避同版本覆盖的 `0x80073CFB`。
- 清理首页重复条目：完整停止 `Microsoft.CmdPal.UI` 与扩展进程、等 2 秒断言，再安装并完整重启面板。
- 新增 `docs/install.md`（通用机制与实测经验）与 `docs/install-notes.md`（本插件数值）。
- 打包脚本修正版本号正则：只改 `<Identity>` 内的 `Version`，避免误伤 XML 声明与 `MinVersion` 导致 `makeappx` 失败。

## 0.1.1.0 · 2026-11-05

- M2：条目动作（打开所在文件夹、复制目标、编辑、删除）、缺失路径标记、设置页。
- 列表增量加载（首屏 100 条 + `LoadMore`）。
- 图标：URL favicon 本地缓存、文件/文件夹 Shell 图标、单色 emoji / 色块自绘。
- M3：CSV / JSON 导入导出（严格表头、RFC 4180、预览确认、撤销上次导入）。
- 设置项：`layout` / `showFullPath` / `showSubtitle` / `showTags`。

## 0.1.0.0 · 2026-11-05

- 首个可用版本：单文件 JSON 存储（原子写 + `.bak` 回退）、添加入口、内存检索、空态与最近使用排序。
