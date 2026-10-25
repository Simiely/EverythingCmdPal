# CHANGELOG

所有版本的变更记录。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [1.2.2] - 2026-10-26

- 新增：「在 Everything 中打开」当前搜索（`-instance` + `-search`，exe 自动定位）
- 重构：新增 `ResultContext`，`ResultItem` 构造参数 6 → 2
- 文档：四件套（README/AGENTS/DEVELOPMENT/CHANGELOG）按知识库单项目规范建立

## [1.2.1] - 2026-10-26

- 新增：`tools/update.cmd` 一键安全更新（关 CmdPal → 卸载旧包 → 打包签名 → 安装 → 重启）

## [1.2.0] - 2026-10-26

- 重构（行为不变）：`SearchSession`（分页/去重/总数）、`ResultMetadata`（元数据唯一来源）、
  `HostLog`（走宿主日志，删除逐项临时文件日志）、`Items→Pages` 依赖切断
- 测试：`tools/SelfTest` 13 项分页/去重断言全过

## [1.1.x] - 2026-10-26

- 1.1.0：设置页（详情栏宽度 / 预览清晰度 / 每页条数 / 实例名 / 输入即搜）
- 1.1.1–1.1.5：预览迭代——Markdown 大图（file:// + `--x-cmdpal-fit=fit`）、
  降采样临时文件、HeroImage 与 Body 互斥、兜底提示
- 1.1.6：修复缺 `fit=fit` 导致图片不跟随窗口；修复 ChoiceSet 默认值取第一个选项
- 1.1.7–1.1.9：「纯列表」模式（先顶层命令、后改为每项 Ctrl+K 切换）；修复
  LoadMore 偏移错位、列表读写锁

## [1.0.x] - 2026-10-26

- 1.0.0：首个可用版本——SDK3 P/Invoke、Channel 防抖、分页、拖拽外发、
  详情栏（缩略图/元数据）、Peek 深链、自签名 MSIX 安装链路打通
- 1.0.1：fallback（输入即搜）
- 1.0.2：修复请求属性导致路径全空（核心 bug）
- 1.0.3：真实 shell 图标、分页偏移修复（初版）、文件夹直开
