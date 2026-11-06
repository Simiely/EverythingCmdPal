# Changelog — CmdPalLinks

格式：`<版本> · <日期>` + 变更点。版本与 `Package.appxmanifest` 的 `Identity Version` 一致。

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
