# Changelog — CmdPalLinks

格式：`<版本> · <日期>` + 变更点。版本与 `Package.appxmanifest` 的 `Identity Version` 一致。

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
