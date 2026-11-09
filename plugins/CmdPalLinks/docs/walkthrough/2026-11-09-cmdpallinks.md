# CmdPalLinks 场景走查报告（2026-11-09）

> 方法：workbuddy-skills / scenario-walkthrough v3（能力清单 → 素材剧本 + 覆盖矩阵 → 分级走查 → 报告 → 修复闭环 → 迭代停止）。
> 范围：当前 0.1.12.0 的 CmdPal 页面链路 + `links-editor.html` 编辑器的读、写、文件夹关系、批量选择与 CSV。
> 触发：用户反馈「网页编辑时多选框用不了」。

## 总体结论

第 1 轮 12 条场景链全部走通；按要求再走查 2 轮（第 2/3 轮）共追加 5 条场景，用户回归又发现 1 条作用域问题。
累计发现 **18 个问题**（2 个真缺陷、13 个 UX/一致性/可操作/边界问题、3 个极低风险），均已修复并回归。
0.1.12.0 之前的数据不变（558 条、15 个文件夹）；最终修复版本 **0.1.16.0**。

## 一、能力清单（C-ID）

| C-ID | 能力 | 真实入口 / 证据 |
|---|---|---|
| C-01 | 链接库首页：一级文件夹、未分类条目、尾部「添加条目 / 新建文件夹 / 批量编辑」 | `ItemsPage.BuildRows` |
| C-02 | 文件夹子页：直接子文件夹 + 本层条目 + 尾部「添加到此文件夹」 | `FolderItemsPage.GetItems` |
| C-03 | 添加条目：URL / 文件 / 文件夹，folder 可空（未分类）或指定任意层级 | `AddItemPage` / `AddItemForm` |
| C-04 | 编辑条目：类型、目标、标题、别名、标签、folder（清空即未分类） | `EditItemForm` / `LibraryStore.Update` |
| C-05 | 删除条目 | `DeleteItemCommand` |
| C-06 | 移动到文件夹：未分类、已有文件夹、现场新建 | `MoveToFolderPage` |
| C-07 | 文件夹新建 / 改名 / 删除：位置可选网页链接内或顶层；改名保留父路径；删除条目回落常用或未分类 | `FolderCreateForm` / `FolderRenameForm` / `FolderDeleteForm` / `LibraryStore` |
| C-08 | 编辑器加载 / 保存：GET/POST `/api/links`，完整 folders，空 folder 保留 | `EditorServer` + `LibraryStore.ReplaceAll` |
| C-09 | 编辑器文件夹树：未分类、全部顶层文件夹、任意深度子文件夹、常用置顶 | `renderTree` |
| C-10 | 编辑器条目编辑 / 新建 / 删除 | `renderList` / `renderInspector` / `btn-apply` |
| C-11 | 编辑器多选：勾选、批量移动、设为常用、取消选择 | `renderList` checkbox / `renderBulk` |
| C-12 | CSV 导入 / 导出：10 列、旧 8/7 列、空 folder、顶层 folder | `importCsvFile` / `exportCsv` |
| C-13 | JSON / TXT / CSV 导入、JSON/CSV 导出、撤销上次导入 | `ImportPage` / `LibraryStore` |

## 二、素材剧本与覆盖矩阵

| S-ID | 等级 | 操作链 | 预期结果 | 覆盖 | 走查 |
|---|---|---|---|---|---|
| S-01 | L1 | 首页 → 添加条目 → 填 URL → 保存 | 条目出现在首页（folder 为空） | C-01,C-03 | 深度 |
| S-02 | L1 | 进入任意文件夹 → 添加到此文件夹 → 保存 | 条目落在当前文件夹 | C-02,C-03 | 深度 |
| S-03 | L2 | 新建顶层文件夹 → 首页可见 → 改名 → 删除 | 改名后条目跟随；删除后条目回未分类 | C-01,C-07,C-09 | 深度 |
| S-04 | L4 | 编辑器勾选 2 条 → 批量移动 → 保存 | 勾选可见、批量目标正确、保存后 folder 更新 | C-10,C-11,C-08 | 深度 |
| S-05 | L3 | 删除文件夹 → 取消；含子文件夹 → 删除 | 取消不生效；含子级时拒绝并提示 | C-07 | 深度 |
| S-06 | L2 | 编辑器顶层 / 嵌套文件夹 → 改名 | 任意深度都可见、改名保留父路径 | C-09,C-07 | 深度 |
| S-07 | L8 | 库里只有文件夹、0 条目 → 首页 | 仍有「添加条目 / 新建文件夹 / 批量编辑」 | C-01 | 快速 |
| S-08 | L9 | 保存请求失败 / 校验失败 | Toast 报错，原数据不被写坏 | C-08 | 深度 |
| S-09 | L2 | 导出 CSV（含空 folder / 顶层）→ 再导入 | folder 原样往返 | C-12 | 快速 |
| S-10 | L2 | 编辑条目 → 文件夹清空 → 应用 → 保存 | 条目回到未分类 | C-04,C-08 | 深度 |
| S-11 | L4 | 文件夹重命名 → 删除 | 网页链接内的条目进常用，顶层条目进未分类 | C-07 | 深度 |
| S-12 | L1 | 首页 / 文件夹页输入关键词 | 跨全部条目检索，不显示文件夹行 | C-01,C-02 | 快速 |

覆盖矩阵：

```
              | 正常 | 空态 | 边界 | 状态推进 | 误操作 | 故障注入
首页/文件夹页 | S-01 | S-07 | S-07 | S-03     | S-05   | S-08
文件夹关系    | S-03 | S-07 | S-07 | S-11     | S-05   | S-08
编辑器条目    | S-10 | S-07 | S-07 | S-04     | S-05   | S-08
编辑器批量    | S-04 | S-07 | S-07 | S-04     | S-05   | S-08
CSV/导入导出  | S-09 | S-07 | S-09 | —        | S-05   | S-08
数据保存      | S-01 | S-07 | S-09 | S-11     | S-05   | S-08
```

无权限差异 / 并发 / 离线列：单用户本地扩展，无角色、无多端并发、无离线队列，属于能力不存在而非覆盖缺失。

## 三、分级走查证据（关键链）

1. **S-01 首页添加未分类**：`ItemsPage.BuildRows` 尾部 `AddItemPage(_store)` → `AddItemForm.SubmitForm` → `LibraryStore.Add(folder: "")` → `SaveLocked` → 首页 `_results` 过滤 `folder` 为空 → 行可见。通。
2. **S-02 文件夹页添加**：`FolderItemsPage` 尾部 `AddItemPage(_store, _folder)` → 表单 folder 预填当前层 → `Add` → `ItemsInFolder` 精确匹配。通。
3. **S-03/S-11 顶层文件夹生命周期**：`FolderCreateForm` 读 `location`；`top` 不补 `网页链接/` → `CreateFolder`；改名保留父路径；`DeleteFolder` 对顶层目的地为空 → 条目回未分类。通。
4. **S-04 编辑器批量**：复选框 `onchange` 写 `state.selectedIds` → `renderBulk` 更新计数/目标 → `bulk-move` 改 folder → `save` POST `{items,folders}` → `EditorServer` DTO → `ReplaceAll` 校验并原子写。通（修复 P-01 后）。
5. **S-05 删除纪律**：CmdPal `DeleteFolder` 先拒绝含子文件夹；编辑器 `openDeleteFolder` 同样先拒绝；确认弹窗写明条目回落目标。通。
6. **S-06 嵌套树**：`renderTree` 递归 `childFolders(parent)`，按深度缩进；任一层文件夹都可选中、改名、删除。通。
7. **S-07 空库边界**：`ItemsPage` 尾部条件从「Items.Count > 0」改为「Items.Count > 0 || Folders.Count > 0」；真正空库仍由 EmptyContent 兜底。通。
8. **S-08 保存失败**：`EditorServer` 对 JSON、items 数量、`ReplaceAll` 行号错误返回 400；`save()` catch 后 toast，未改磁盘。通。
9. **S-09 CSV**：空 folder 不再强制转「常用」，非空按完整路径；顶层文件夹可往返。通。
10. **S-10 清空 folder**：`btn-apply` 不再把空 folder 写回 `COMMON`；`ReplaceAll` 允许 `folder=""`。通。

## 四、问题清单

| 编号 | 级别 | 问题 | 位置 | 受影响链路 | 风险分 | 处理 |
|---|---|---|---|---|---|---|
| P-01 | 🔴 真缺陷 | 条目复选框点击冒泡到整行 `onclick` → `selectItem` → `renderAll` 重建复选框，`change` 丢失；表现为勾不上，且勾选会误触发详情选中 | `links-editor.html` renderList 复选框 | S-04 | 5×4=20 | 已修：`cb.onclick` stopPropagation；回归勾选/取消/行选中 |
| P-02 | 🟡 UX | 首页有文件夹但 0 条目时，尾部「添加条目 / 新建文件夹 / 批量编辑」整组不渲染，形成无法新增的死角 | `ItemsPage.BuildRows` 条件 | S-07 | 3×4=12 | 已修：条件纳入 `Folders.Count > 0` |
| P-03 | 🟡 一致性 | CmdPal 允许改名/删除「网页链接」，编辑器却标明不可删除；删除会把整库散成未分类 | `ItemsPage.BuildFolderCommands` | S-03,S-11 | 3×4=12 | 已修：与「常用」一起保护 |
| P-04 | 🟡 UX | 删除文件夹确认页只数直接条目，有子文件夹时仍显示「为空 / N 条」，实际提交才被 Store 拒绝 | `FolderDeleteForm` / `BuildDelete` | S-05 | 2×4=8 | 已修：`CountInTree` + 子文件夹提示 |
| P-05 | 🟡 一致性 | 编辑器重命名冲突检查大小写敏感，Store 用 `OrdinalIgnoreCase`，可能产生仅大小写不同的重名并在保存时被去重 | `openRenameFolder` | S-06 | 2×3=6 | 已修：改 `toLowerCase()` 比较 |
| P-06 | 🟡 一致性 | 编辑器删除父文件夹会连同子文件夹一起删，CmdPal 则拒绝；提示未说明子级 | `openDeleteFolder` | S-05,S-06 | 2×4=8 | 已修：含子文件夹时拒绝删除 |
| P-07 | 🟡 可操作点 | 编辑器树只渲染两级；名称含 `/` 或 CmdPal 建的三级文件夹在编辑器中不可见、不可操作 | `renderTree` | S-06 | 3×3=9 | 已修：递归渲染任意深度 |
| P-08 | ⚪ 极低 | 每次 `renderBulk` 重建目标下拉，用户已选目标被重置为「未分类」 | `renderBulk` | S-04 | 2×1=2 | 已修：`state.bulkTarget` 保存并在重建时恢复 |
| P-09 | ⚪ 极低 | 搜索过滤时已勾选但被隐藏的条目仍参与批量操作 | 搜索 `input` | S-04,S-12 | 2×2=4 | 已修：搜索词变化时清空选择 |
| P-10 | ⚪ 极低 | 新条目临时 id 为 `new-<timestamp>`，同一毫秒连续新建理论上可能撞 id | `newItem` | S-04,S-10 | 1×2=2 | 已修：追加 `newIdSeq` 序号 |

残余风险：本轮问题清零；剩余验证缺口见第五节。

## 五、修复与回归

修复文件：
- `src/CmdPalLinks/Assets/links-editor.html`：复选框事件隔离、递归文件夹树、新建/改名/删除纪律。
- `src/CmdPalLinks/Pages/ItemsPage.cs`：尾部入口条件、保护「网页链接」。
- `src/CmdPalLinks/Pages/FolderPages.cs`：删除确认数量/子文件夹提示。
- `src/CmdPalLinks/Package.appxmanifest`：0.1.12.0 → 0.1.16.0。
- `src/CmdPalLinks/Services/LibraryStore.cs`：Add / Update / ImportBatch 的 folder 校验。

回归：
- `node --check` 提取后的编辑器脚本：通过。
- headless Edge 编辑器用例（本地 mock API，5 条 / 5 文件夹含嵌套）：通过。覆盖勾选 1→2→3、取消、点行后勾选保持、
  批量目标在再次勾选后保持、搜索词变化清空选择、搜索无结果空态、超长 folder 前端拦截、当前文件夹内新建子文件夹、
  含子文件夹拒绝删除、顶层改名/删除、未分类新建、保存 payload `folders` 含顶层与嵌套、新条目 `folder=""`。
- `dotnet build`：0 警告 0 错误。
- 安装 0.1.16.0：标准 `install.ps1 -NoBump -Configuration Release`，LocalState 快照 SHA256 全一致、`Status: Ok`。
- 数据：558 条、15 个文件夹；当前有 2 条 `folder=""` 的未分类条目（`D:\Tool` 与「软件工具」），位于「网页链接」外。
  这是新能力的合法状态，不是坏数据；旧校验脚本仍把未分类记为 `errors`，按新语义看总数 558、重复 0、空标题 0 均成立。

验证缺口：CmdPal 宿主的行数 UIA 本次未取到——窗口在当前会话被系统 cloaked（DWM `cloaked=1`），
但扩展日志已确认 `Started extension CmdPalLinks_0.1.16.0` 与 `Loaded 1 command(s)`；改用编辑器实测 + 扩展日志 + 数据校验作为本轮证据。

## 六、覆盖矩阵结论

- 无整行空白；无整列空白（权限 / 并发 / 离线列按能力不存在填写理由）。
- L4 状态链路：S-04 批量勾选 + 保存、S-11 改名/删除回落，已覆盖。
- L8 边界：S-07 空库/零条目。
- L9 故障注入：S-08 保存失败/校验失败。
- 盲区：CmdPal 宿主的真实 UIA 行数未在本轮采到，列入残余验证风险；编辑器侧已用 headless 浏览器补足。

## 七、走查验证的良好点

- 文件夹删除始终保留条目：网页链接内 → 常用，顶层 → 未分类；CmdPal 与编辑器使用同一语义。
- 保存路径统一走 `ReplaceAll`：先全量校验（类型、判重、folder、iconSource、颜色），再单锁原子写；失败回滚内存。
- 编辑器不直接写文件：回环 HTTP + 一次性 token + Host 校验 + 请求体上限 + 行号错误。
- 文件夹重命名保留父路径，禁止移到自己的子层级；「常用」不可改名/删除。
- 搜索与文件夹浏览分离：有检索词时跨全部条目，不显示文件夹行。

## 八、结论三问

1. **链路逻辑通顺吗？** 通。首页/文件夹/编辑器三个入口的 folder 语义一致，空 folder = 未分类。
2. **是补丁叠补丁吗？** 否。folder 的归属、删除回落、保存校验都收在 `LibraryStore`；编辑器只维护 state，不另造规则。
3. **会无法操作/出 bug 吗？** 首轮修复了多选框真缺陷和 3 个可操作点；第 2/3 轮又抓到 folder 校验真缺陷
   与 6 个 UX/一致性/边界问题；用户回归再抓到 1 条「网页链接」作用域问题。全部修复后真实后端回归通过，
   剩余风险仅为宿主 UIA 验证缺口。

## 九、建议处理（按风险分）

- 已修：P-01（20）、P-02/P-03/P-16（12）、P-07/P-15/P-18（9）、P-04/P-06（8）、P-05/P-12/P-14/P-17（6）、
  P-09（4）、P-08/P-10/P-11/P-13（2）。
- 本轮问题清零，无需继续修复；只保留验证补强：找一次宿主窗口正常显示的会话补跑 UIA 首页/二级/文件夹计数。

## 十、第二轮 / 第三轮走查（0.1.15.0）

### 10.1 新增素材

| S-ID | 等级 | 操作链 | 预期结果 | 覆盖 |
|---|---|---|---|---|
| S-13 | L8 | 进入空文件夹（无条目、无子文件夹） | 仍显示「此文件夹为空」空态，而不是只剩一行添加入口 | C-02 |
| S-14 | L8 | 新建/改名文件夹输入 70 字符、含控制字符 | 前端直接拦截，不生成非法 folder | C-07,C-09 |
| S-15 | L3 | 对含子文件夹的父文件夹点删除 | 菜单提示「需先删除子文件夹」，不误导条目去向 | C-07 |
| S-16 | L3 | 搜索无结果 | 空态写「没有匹配的条目」，不是「文件夹为空」 | C-09,C-10 |
| S-17 | L4 | 选中嵌套文件夹 → 新建文件夹 | 默认建在「当前文件夹内」，树递归可见 | C-07,C-09 |

### 10.2 第二轮（编辑器空态与文件夹纪律）发现

| 编号 | 级别 | 问题 | 位置 | 风险分 | 修复 |
|---|---|---|---|---|---|
| P-11 | 🟡 UX | `FolderItemsPage` 无条件追加「添加到此文件夹」，空文件夹的 EmptyContent 永远不显示 | `FolderItemsPage.GetItems` | 2×2=4 | 有行才追加 |
| P-12 | 🟡 边界 | 编辑器新建/改名/CSV 导入不校验 folder ≤64/≤256/控制字符，保存时才被 Store 拒绝 | `openNewFolder` / `openRenameFolder` / `importCsvFile` | 2×3=6 | 新增 `folderValidationError` 前端拦截 |
| P-13 | 🟡 一致性 | CmdPal 对含子文件夹的父文件夹，删除菜单仍写「条目会移入常用」，实际会被拒绝 | `ItemsPage.BuildFolderCommands` | 2×2=4 | 菜单改为「需先删除子文件夹」 |

### 10.3 第三轮（探索会话：搜索/新建位置/Store 校验）发现

| 编号 | 级别 | 问题 | 位置 | 风险分 | 修复 |
|---|---|---|---|---|---|
| P-14 | 🟡 UX | 搜索无结果时空态仍写「这个文件夹还没有条目」 | `renderList` empty | 3×2=6 | 按 query 切换「没有匹配的条目」 |
| P-15 | 🟡 可操作点 | 新建文件夹只有「网页链接内 / 顶层」，选中嵌套文件夹后无法直接建子文件夹 | `openNewFolder` | 3×3=9 | 新增「当前文件夹内」并作默认 |
| P-16 | 🔴 真缺陷 | `LibraryStore.Add` / `Update` 只规范化 folder，不校验；CmdPal 表单可把非法 folder 写入 links.json | `LibraryStore.Add` / `Update` | 3×4=12 | 补 `IsValidFolderName`，非法返回 `InvalidTarget` |
| P-17 | 🟡 数据边界 | `ImportBatch` 遇到非法 folder 不跳过，可能污染导入结果 | `LibraryStore.ImportBatch` | 2×3=6 | 计入 skipped，不写非法 folder |

### 10.4 回归

- Playwright 用例扩展并通过：空搜索文案、70 字符 folder 拦截、选中文件夹后默认当前层新建子文件夹、
  保存 payload 包含 `嵌套顶层/子2`。
- `dotnet build` 0 警告 0 错误；安装 0.1.15.0，LocalState 657 文件 SHA256 全一致、`Status: Ok`；
  扩展日志 `Started extension CmdPalLinks_0.1.15.0`、`store loaded: 558 items, 15 folders`。
- 第 2/3 轮新增问题全部修复，未再引入新的 🔴；剩余仅为宿主 UIA 验证缺口。

## 十一、用户回归发现（0.1.16.0）

| S-ID | 等级 | 操作链 | 预期结果 | 覆盖 |
|---|---|---|---|---|
| S-18 | L2 | 选中「网页链接」→ 查看列表与计数 | 只包含「网页链接」自身子树，不含未分类 / 顶层文件夹条目 | C-09,C-10 |

| 编号 | 级别 | 问题 | 位置 | 风险分 | 修复 |
|---|---|---|---|---|---|
| P-18 | 🟡 作用域 | 编辑器选中「网页链接」时 `currentItems` 用 `state.items` 全库，提取到未分类或顶层文件夹的条目仍显示在网页链接下；列表计数也用全库 | `currentItems` / `renderList` 的 ROOT 分支 | 3×3=9 | ROOT 改为 `isUnder(folder, 网页链接)` 只取子树；计数用 `countInTree(ROOT)`；子文件夹 meta 改本层直接条数 |

验证：
- headless mock：网页链接 3 条 / 未分类 1 条，列表与计数一致。
- 真实 C# 后端（当前用户库副本 558 条、2 条未分类）：网页链接只显示 **556** 条、未分类只显示 **2** 条；
  新建顶层文件夹 + 未分类根条目后保存，GET 为 559 条 / 16 文件夹 / 未分类 3 条，`PASS real-editor`。
- 安装 0.1.16.0，扩展日志 `Started extension CmdPalLinks_0.1.16.0`、`store loaded: 558 items, 15 folders`。
