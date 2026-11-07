# 链接核验轮数研究与 79 条「打不开但原因不明」复核报告（0.1.11.0）

> 日期：2026-11-07 · 插件版本 `0.1.11.0` · 方法：headless Microsoft Edge（Playwright 临时 profile、串行、不弹窗、走系统代理）
> 同一批 URL 串行探测 3 轮，单条超时 30 秒，轮间间隔 90 秒。

## 1. 结论

| 失败类型 | 例子 | 建议轮数 | 实测翻转率 |
|---|---|---|---|
| 确定性失败 | DNS 不存在、410、域名停放/过期、明确 404 错误页 | 1 轮即可判定，2 轮留档确认 | 0 / 4 = **0%** |
| 抖动型失败 | 超时、连接被重置、TLS、429、5xx、资源拦截 | **3 轮**，轮间 ≥60–90s，超时 ≥30s | 4 / 67 = **5.97%** |
| 首轮成功（不代表稳定） | 2xx 正文、403 反爬页 | 至少 3 轮才能说「稳定可达」 | 5 / 8 = **62.5%** 后续又失败 |

整体：79 条 × 3 轮 = **237 次**真实导航，总耗时约 40.6 分钟；同一 URL 跨轮「失败↔成功」翻转 **9/79 = 11.39%**。
结论：**确定性失败 1–2 轮、抖动型 3 轮、间隔 60–90 秒、单条 30 秒**；只用 1 轮会把 5.97% 的抖动误判成死链，只用状态码会把反爬页误判成死链。

## 2. 轮数依据（公开做法）

- [lychee](https://raw.githubusercontent.com/lycheeverse/lychee/lychee-v0.24.2/lychee.example.toml) 默认 `max_retries = 2`、`retry_wait_time = 2`；早期版本默认 3 次重试、1 秒等待。
- [markdown-link-check](https://www.npmjs.com/package/markdown-link-check) 默认 `retryOn429: true`、`retryCount: 2–5`、`fallbackRetryDelay: 30s`，超时 10–20s。
- [Screaming Frog](https://www.screamingfrog.co.uk/http-status-codes-when-crawling/) 建议对 404 用浏览器复验，以排除 UA/反爬造成的假 404。
- 通用重试语义（[Shifter](https://shifter.io/es/blog/logica-reintentos-proxies-residenciales)）：终端错误（404/400/401）不重试；挑战页 403/验证码换会话后重试；瞬态错误（超时/5xx/429）退避重试。
- 指数退避 + 抖动是通行做法（首轮 1s、次轮 2s、再 4s）；本机实测采用更保守的 90 秒轮间间隔，避免同一出口 IP 连打。

## 3. 实测设置

| 项 | 值 |
|---|---|
| 浏览器 | Microsoft Edge（Playwright，`headless=True`，临时 profile） |
| 网络 | 系统代理 `127.0.0.1:7890`（Edge 默认读取系统代理） |
| 超时 | 单条 30 秒 |
| 轮间间隔 | 90 秒 |
| 样本 | 75 条上一轮「保留·需人工」+ 4 条已删对照 = 79 |
| 轮次时长 | 第 1 轮 854s（11:33:48–11:48:02），第 2 轮 836s（11:49:32–12:03:28），第 3 轮 748s（12:04:58–12:17:26） |
| 资源 | 本轮不拦截图片/字体/媒体，避免代理下出现假失败 |

## 4. 翻转率实测

| 分组（按第 1 轮结果） | 样本 | 跨轮翻转 | 翻转率 |
|---|---|---|---|
| 确定性失败 | 4 | 0 | 0.00% |
| 抖动型失败 | 67 | 4 | 5.97% |
| 第 1 轮成功 | 8 | 5 | 62.50% |

翻转明细（同一 URL 的 3 轮结果）：

| 标题 | URL | 3 轮结果 |
|---|---|---|
| 精靈學院 | https://portal.riolu-node.link/dashboard | JIT_5XX → ALIVE → JIT_5XX |
| 精靈學院 | https://master.riolu.sbs/dashboard | JIT_5XX → ALIVE → JIT_5XX |
| AI研究所 - 国内外AI工具资讯首发站 | http://www.aiyjs.com/ | ALIVE → JIT_TIMEOUT → ALIVE |
| 登录 - 精靈學院 | https://user.riolu-node.link/login | JIT_5XX → ALIVE → JIT_5XX |
| Steam | https://store.steampowered.com/ | ALIVE → JIT_OTHER → ALIVE |
| 在 Steam 上激活产品 | https://store.steampowered.com/account/registerkey | ALIVE → JIT_OTHER → ALIVE |
| 联机游戏-SGAMES | https://www.s-sgames.com/multiple/ | ALIVE → JIT_EMPTY → ALIVE |
| 橱窗 | https://www.behance.net/gallery/26931445/Cavendish-Beyond-W… | ALIVE_ANTIBOT → JIT_BLOCKED → JIT_BLOCKED |
| CGer.com\|艺术\|设计\|技术\|建筑\|软件行业网站搜集。资源\|… | https://www.cger.com/ | JIT_4XX → ALIVE → JIT_4XX |

确定性对照（4 条上一轮已删条目）3 轮结果完全一致，证明「DNS/410/停放/明确 404」不需要多轮也能判死：

| 标题 | URL | 3 轮 | 结论 |
|---|---|---|---|
| 正版中国 \| 正版软件限时免费 | https://getitfree.cn/ | DET_404_CONTENT → DET_404_CONTENT → DET_404_CONTENT | 确认删除（未恢复） |
| 红色gif | http://www.min-liu.com/bloody-dairy-a-daily-animation-… | DET_404_CONTENT → DET_404_CONTENT → DET_404_CONTENT | 确认删除（未恢复） |
| R站\|学习使我快乐！ - 专注C4D三维\|建模\|渲染\|后期\|动效\|绑定\|产… | https://www.btbat.com/ | DET_NOTFOUND → DET_NOTFOUND → DET_NOTFOUND | 确认删除（未恢复） |
| 苹果软件园_苹果游戏_Mac游戏_苹果软件_Mac软件 | http://www.macapp.cc/ | DET_PARKED → DET_PARKED → DET_PARKED | 确认删除（未恢复） |

## 5. 79 条最终结局

| 结局 | 条数 | 说明 |
|---|---|---|
| 跳过探测、直接保留 | 4 | 本机/局域网服务（localhost、192.168.*），未运行不等于失效 |
| 保留（至少 1 轮成功） | 12 | 12 条，含 3 条稳定可达与 9 条抖动可达 |
| 保留·需人工 | 63 | 63 条，3 轮均未成功，但无确定性失败证据 |
| 删除 | 0 | 0 条；无任何条目 3 轮全部确定性失败 |
| 已删对照再确认 | 4 | 3 轮全部确定性失败，维持删除，不恢复 |

跳过探测的 4 条：

- Sunshine — `https://localhost:47990/`
- 小米路由器yacd — `http://192.168.31.1:9999/ui/`
- 仪表盘 - Cpolar — `http://localhost:9200/#/dashboard`
- yacd — `http://192.168.2.1:9999/ui/#/`

至少 1 轮成功的 12 条：

| 标题 | URL | 3 轮结果 |
|---|---|---|
| 精靈學院 | https://portal.riolu-node.link/dashboard | JIT_5XX → ALIVE → JIT_5XX |
| 精靈學院 | https://master.riolu.sbs/dashboard | JIT_5XX → ALIVE → JIT_5XX |
| Sketchfab | https://sketchfab.com/ | ALIVE → ALIVE → ALIVE |
| 電視電影 | https://kimivod.com/ | ALIVE_ANTIBOT → ALIVE_ANTIBOT → ALIVE_ANTIBOT |
| AI研究所 - 国内外AI工具资讯首发站 | http://www.aiyjs.com/ | ALIVE → JIT_TIMEOUT → ALIVE |
| 登录 - 精靈學院 | https://user.riolu-node.link/login | JIT_5XX → ALIVE → JIT_5XX |
| Steam | https://store.steampowered.com/ | ALIVE → JIT_OTHER → ALIVE |
| 在 Steam 上激活产品 | https://store.steampowered.com/account/registerkey | ALIVE → JIT_OTHER → ALIVE |
| 联机游戏-SGAMES | https://www.s-sgames.com/multiple/ | ALIVE → JIT_EMPTY → ALIVE |
| 橱窗 | https://www.behance.net/gallery/26931445/Cavendish-Beyond-W… | ALIVE_ANTIBOT → JIT_BLOCKED → JIT_BLOCKED |
| CGer.com\|艺术\|设计\|技术\|建筑\|软件行业网站搜集。资源\|… | https://www.cger.com/ | JIT_4XX → ALIVE → JIT_4XX |
| 微软远程桌面优化-CSDN博客 | https://blog.csdn.net/weixin_45642871/article/details/13220… | ALIVE_ANTIBOT → ALIVE_ANTIBOT → ALIVE_ANTIBOT |

63 条「保留·需人工」按主因分布：

| 主因 | 条数 |
|---|---|
| 资源/下载被拦截 | 24 |
| 30s 超时 | 18 |
| 连接被重置 | 11 |
| 5xx 服务端错误 | 3 |
| 其他 4xx | 2 |
| 正文为空 | 2 |
| 其他网络错误 | 1 |
| TLS 错误 | 1 |
| 重定向异常 | 1 |

## 6. 处理、校验与安装

| 项 | 值 |
|---|---|
| 删除 | 0 条（无条目满足「3 轮全部确定性失败」） |
| 恢复 | 0 条（4 条对照 3 轮均为确定性失败，维持删除） |
| 备份 | `links-before-multiround-20261107-122214.json` 与 `LocalState/links.json.multiround-bak` |
| 总条目 | **558**（与上一轮一致） |
| 校验 | `total = sum(categories) = 558`；一级 1、二级 14、未分类 0、空标题 0、重复 URL 0，`errors 0` |
| 安装 | `0.1.11.0`（Release，标准流程；LocalState 657 文件快照恢复 SHA256 全一致，Status Ok） |
| links.json SHA256 | `FA7A97E10F99A7F41A6D9070C849D416FFA704098B15E017C0E27936F31D449E` |
| UIA | 首页 3 项；二级 14 项「常用」第一；常用 65、本地工具 22、小工具 55、搜索 21、软件 75、信息 42、参考 28、素材 23、ai视频 6、教程 112、MCP 5、三维素材 40、视频&音乐 38、Ai 26，BAD=0 |

## 7. 证据文件

- 第 1/2/3 轮逐条记录：`cmdpaplinks-data-backup/roundprobe-20261107-113348-r1.jsonl` / `-r2.jsonl` / `-r3.jsonl`
- 汇总（含翻转、判定）：`roundprobe-20261107-113348-summary.json`
- 清理记录：`multiround-cleanup-20261107-122214.json`（0 删除、0 恢复、备份路径）

