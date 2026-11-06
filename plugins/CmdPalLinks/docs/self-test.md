# 自测指南（CmdPalLinks M2.3）

## 1. 构建
```
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build src\CmdPalLinks\CmdPalLinks.csproj -c Debug
```
预期：`0 个警告 / 0 个错误`。
（Release 版仅用于查看产物：`dotnet build -c Release`，输出在
`src\CmdPalLinks\bin\Release\net10.0-windows10.0.26100.0\win-x64\`）

## 2. 装包 / 升级
前提（本机此前已装过同证书签名的包，通常已满足）：开发者模式已开、签名证书在「受信任人/受信任根」。
```
powershell -NoProfile -ExecutionPolicy Bypass -File plugins/CmdPalLinks/tools/install.ps1
```
流程：**递增包版本号 → 构建 → 打包签名 → 停面板与扩展宿主并等 2 秒 → 卸载旧包（卸载前快照 `LocalState`）→ `Add-AppxPackage` → 恢复并校验数据 → 重开面板**。
顺序照抄 `../../../plugins/EverythingCmdPal/tools/update.ps1` L75-L106；**不用 `-ForceUpdateFromAnyVersion`、不在面板运行中安装**
（详见 `../../../docs/install.md`）。`LocalState` 在卸载后原样恢复，8 个文件 SHA256 必须一致。
装完自动重开 CmdPal（不想重开加 `-NoRestart`）；**必须让 CmdPal 进程重启**，否则旧注册不会清掉，首页可能同时出现新旧两条「链接库」。

### 2.1 几条实测事实（2026-11-06）
- 同版本号 + 不同内容无法安装：`0x80073CFB` ——「提供的程序包具有与已安装的程序包相同的标识，但内容不相同…请提高版本号」。
- 版本递增（`0.1.1.0 → 0.1.2.0`）后重装成功（`Status: Ok`）。
- `Remove-AppxPackage` 会删除包内 `LocalState`；`-PreserveApplicationData` 对普通签名包无效（`0x80073CFA`）。因此流程里用「快照 + 装后恢复」。
- 卸载/装回后 `LocalState` 8 个文件（links.json / links.json.bak / settings.json / links-editor.html / icons×4）SHA256 全部一致，`items=7`、`icons=4`。
- 宿主 `ProviderSettings` 与 `commandProviderCache` 中 `CmdPalLinks…` 始终各 1 条，无重复注册。
- 部署报 `0x80073D02`（文件占用）时，先关闭扩展宿主 `CmdPalLinks.exe` 再装即可。
- 首页出现两条相同「链接库」：不是装了两个包（`Get-AppxPackage` 只有 1 条），是宿主在同一会话里重复注册/重复渲染；**完整重启 CmdPal 即恢复**。所以装完要重开面板，而不是只重启扩展。

### 2.2 宿主重复条目：与安装方式无关（2026-11-06 实测，已回退升级）
- 旧环境 `PowerToys 0.100.2` + `CmdPal UI 0.11.11762.0` + `SparseApp 0.100.2.0`：宿主每次启动 SparseApp 都抛
  `System.ArgumentException: You forgot to start the extension…`，历史环境里交互期间会把全部扩展整轮重载（29 秒内 7 次），首页出现 2 条「链接库」。
- `PowerToys 0.102.3001.0`（CmdPal UI `0.13.13001.0`）实测确实抑制了重载循环（静止 60 秒 + 6 轮交互，异常/加载各 1 次），
  但**用户否决该升级**（检索体验变差），已回退到 `0.100.2`。
- 回退后按 §2 新流程装 `0.1.2.0`：宿主每次启动仍是 SparseApp 异常 1 次、`CmdPalLinks` 启动/加载各 1 次，
  本次观察窗口内**没有重载循环**；UIA 首页 `ListItem` 共 5 项，名称=「链接库」的只有 1 项。
- 无效做法（已实测，勿再尝试）：在宿主 `settings.json` 的 `ProviderSettings` 里给 SparseApp 加 `IsEnabled=false` —— 键写入后 SparseApp 照旧启动、照旧抛同一异常。
- 副作用记录：回退时 `winget uninstall Microsoft.PowerToys 0.102` 会连带删除 `Microsoft.CommandPalette` 包及其 `LocalState`
  （已用 `.testdata\rollback-20261106-1055` 备份恢复）；`winget list` 在回退期间曾同时列出 `0.102.3001.0` 与陈旧的 `0.100.2` 记录。


## 3. 打开面板
`Win+Alt+Space`（前台若是全屏/被判定全屏的应用，热键会被忽略；先切到普通窗口）。
导航：搜索「链接库」→ 回车进入。

## 4. 测试点（详细预期见 `design.md` §12 的 V1–V17）
1. **图标**：文件夹、`.txt`、`.html`、`.png`、`.lnk`、不存在路径、不可达 UNC。看是否有真实图标、是否卡顿。
2. **缓存/失效**：同一路径二次打开不应再提取；改动该文件后应重新提取。
3. **网格**：设置 → 扩展 → 已安装 → 链接库 → 「列表布局」切「网格（中）」→ Save；列表应立即变卡片。
4. **滚动**：网格下滚轮、PageDown、方向键；看最后一行是否可见、面板是否被关闭。
5. **条数**：用 30/100/500 条数据（`.testdata\gridset\links-30/100/500.json`，复制为 `LocalState\links.json` 后重启 CmdPal）。预期首次 100 条，滚到底自动追加 100。
6. **设置**：4 项逐个切换 → 立即生效；重启 CmdPal 后仍生效（`settings.json` 保留）。
7. **批量编辑（0.1.3.0）**：列表末项只有「批量编辑」。点击后浏览器应打开 `http://127.0.0.1:<随机端口>/?token=...`，
   页面统计条显示「已连接」与现有条数（当前 7），每行左侧有图标预览（emoji / 色块首字 / favicon）。
   改内容后点「保存到插件」→ 提示成功并刷新 id；`LocalState\links.json` 立即更新且生成 `.bak`。失败会弹窗显示服务端原因。
8. **emoji 图标**：给任一条设置 emoji（如 `📁`）→ 保存 → 列表/搜索/详情应显示清晰单色轮廓（不是汉字乱码、不是方框）。
   中文标题的首字色块也应显示正确汉字。彩色 emoji 目前为单色轮廓（GDI 限制，见 `design.md` §10.6）。
9. **CSV 往返图标**：导出 CSV（8 列，末列 `icon`）→ 删除/清空后回导 → 各条 `iconKind/iconColor/iconEmoji` 不变；
   旧版 7 列 CSV 仍可导入（按 emoji + color 推断形态）。

## 5. 日志
```
%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\0.100.2.0\Log_YYYY-MM-DD.log
```
关键字：`shell icon loaded/unavailable/invalidated`、`search q=`,
`load more:`、`layout applied:`、`settings loaded:`、`store loaded:`。

## 6. 坏了怎么判断
- 图标全是通用字形 → 看有无 `shell icon skipped reason=missing`（路径真不存在）或 `unavailable`（提取失败）。
- 网格不出现 → 看 `layout applied:` 是否 `grid=True`；没有则设置未保存（看 `settings.json`）。
- 滚动没反应 → 先点一下列表项再滚（焦点问题）；仍无效记录日志里的 `VerticalScrollPercent`（宿主行为，插件无法控制）。
- 条数只显示 30 → 旧构建的截断；当前应显示 100（或全部，小于 100 时）。
