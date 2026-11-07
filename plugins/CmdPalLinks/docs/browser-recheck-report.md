# 132 条「待确认」真实浏览器复核报告（0.1.10.0）

> 日期：2026-11-07 · 插件版本 `0.1.10.0` · 方法：headless Microsoft Edge（Playwright，临时 profile，串行，单条 ≤15s，图片/字体/媒体资源不加载）
> 上一轮 132 条 401/403/429/超时/TLS/5xx 待确认项全部重新用浏览器真实渲染，按页面内容而非 HTTP 状态码判定。

## 1. 汇总

| 判定 | 条数 | 含义 |
|---|---|---|
| 删除 | 4 | 明确错误页 / 停放过期页 |
| 保留 | 45 | 正常渲染，或 401/403/429/Cloudflare/登录墙等反爬但站点有效 |
| 保留·需人工 | 79 | 超时、连接被重置、TLS、本机服务未启动、渲染失败等，不据此判死 |
| 疑似失效 | 4 | 跳首页/搜索页/无此页面提示，先不删，待你确认 |
| **合计** | **132** | 与上一轮待确认清单一一对应 |

## 2. 已删除条目（4 条，可直接对照是否误杀）

备份：`cmdpaplinks-data-backup/links-before-browser-cleanup-20261107-102305.json`（删除前 562 条）

| # | 标题 | 原分类 | URL | 状态 | 判定依据 |
|---|---|---|---|---|---|
| | 正版中国 \| 正版软件限时免费 | 软件 | https://getitfree.cn/ | 404 | HTTP 404 明确错误页 |
| | 红色gif | 参考 | http://www.min-liu.com/bloody-dairy-a-daily-animation-… | 404 | HTTP 404 明确错误页 |
| | R站\|学习使我快乐！ - 专注C4D三维\|建模\|渲染\|后期\|动效\|绑定\|产品\|包装\|动画\|自由艺术… | 三维素材 | https://www.btbat.com/ | 200 | 错误页特征：notfound:404（正文 19 字） |
| | 苹果软件园_苹果游戏_Mac游戏_苹果软件_Mac软件 | 软件 | http://www.macapp.cc/ | 200 | 停放/过期页特征：parked:域名过期/待售 |

逐条证据：

- **正版中国 | 正版软件限时免费**：最终 https://getitfree.cn/；页面标题「404 Not Found」；可见正文 257 字。
- **红色gif**：最终 https://www.min-liu.com/bloody-dairy-a-daily-animation-project；页面标题「Min Liu」；可见正文 456 字。
- **R站|学习使我快乐！ - 专注C4D三维|建模|渲染|后期|动效|绑定|产品|包装|动画|自由艺术|设计学习交流**：最终 https://www.btbat.com/；页面标题「404 Not Found」；可见正文 19 字。
- **苹果软件园_苹果游戏_Mac游戏_苹果软件_Mac软件**：最终 https://www.macapp.cc/；页面标题「macapp.cc」；可见正文 134 字。

## 3. 疑似失效（4 条，未删除，等你确认）

| # | 标题 | URL | 最终 URL | 现象 |
|---|---|---|---|---|
| 37 | 微软绘图 | https://www.bing.com/create | https://cn.bing.com/images/create/ai-image-generator | Bing 图片创建器 404 页面，属常用服务，先列待确认 |
| 58 | Search Results \| 3D Models | https://rendercrate.com/3D/search/#!/new | https://rendercrate.com/3D/search#!/new | 目标搜索页 404 且正文为空，疑似失效，先列待确认 |
| 80 | 邮箱Free Email Addresses: Web based and secure Emai… | https://navigator-lxa.mail.com/home?sid=959359fcf95f9b4207b… | https://www.mail.com/ | 跳转到其他地址：https://www.mail.com/ |
| 104 | 未越狱福利，破解软件ipa合集分享，更新最新不闪退迅雷5.21版（不是老版本... - iPhon… | https://bbs.feng.com/forum.php?mod=viewthread&tid=11250834 | https://www.feng.com/NoDetail?status=1&text=%E6%97%A0%E6%AD… | 跳转到其他地址：https://www.feng.com/NoDetail?status=1&text=%E6%97%A0%E6%AD%A… |

## 4. 保留·需人工（79 条，按原因分组）

**HTTP 认证未通过（1）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 1 | 外网图库 | https://w2e0b1d6av.gd.ddnsto.com:443 | net::ERR_INVALID_AUTH_CREDENTIALS |

**HTTP 503 服务端/其他错误（5）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 2 | Dpanl外网 | https://sidmiepaanily.gd.ddnsto.com:443 | 503 |
| 3 | win7外网 | https://simiwimiln7y.gd.ddnsto.com:443 | 503 |
| 5 | 精靈學院 | https://portal.riolu-node.link/dashboard | 503 |
| 15 | 精靈學院 | https://master.riolu.sbs/dashboard | 503 |
| 42 | 登录 - 精靈學院 | https://user.riolu-node.link/login | 503 |

**HTTP 405 服务端/其他错误（2）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 6 | 花瓣 | http://huaban.com | 405 |
| 88 | CGer.com\|艺术\|设计\|技术\|建筑\|软件行业网站搜集。资源\|模型\|教… | https://www.cger.com/ | 405 |

**浏览器 15s 超时（26）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 7 | 网盘搜索 | https://ysxjjkl.souyisou.top/ | timeout(15000ms) |
| 33 | 電視電影 | https://kimivod.com/ | timeout(15000ms) |
| 43 | Steam | https://store.steampowered.com/ | timeout(15000ms) |
| 54 | POCO摄影图片社区 - 领先的时尚摄影平台，摄影视频，摄影爱好者分享作品和技巧首选，个… | https://photo.poco.cn/ | timeout(15000ms) |
| 56 | 免费资源网 | https://www.mfzyw1.com/ | timeout(15000ms) |
| 59 | Veer免费图库 - 可商用正版图片素材库 | https://free.veer.com/ | timeout(15000ms) |
| 63 | 谷歌商城应用 | https://apkdl.in/ | timeout(15000ms) |
| 65 | V2Ray / SSR 传输协议哪个好? (各种协议对比) - 无主界 | https://www.idleleo.com/05/2071.html | timeout(15000ms) |
| 66 | APP搜索 - Sunny在线工具 | https://tools.lancely.tech/apple/app-search | timeout(15000ms) |
| 67 | 3元海淘8元奈飞 | https://joyo96.cc/user | timeout(15000ms) |
| 68 | 最好用的国内免费接码平台大全 \| 免费临时手机号 \| 免费手机号 - 一灯不是和尚 | https://iyideng.fun/welfare/best-domestic-platform-for-rece… | timeout(15000ms) |
| 69 | 小米路由器yacd | http://192.168.31.1:9999/ui/ | timeout(15000ms) |
| 70 | 九九美剧 - 天天美剧在线观看_美剧迷真正的美剧天堂 | https://www.99meiju.vip/ | timeout(15000ms) |
| 71 | 全网影片搜索 - MyFreeMP4 | https://lab.liumingye.cn/ | timeout(15000ms) |
| 73 | Mp4电影_2022最新高清MP4电影资源下载 | https://www.dbmp4.com/ | timeout(15000ms) |
| 77 | 在 Steam 上激活产品 | https://store.steampowered.com/account/registerkey | timeout(15000ms) |
| 93 | 面包读书会 | https://kindlebookfor.me/ | timeout(15000ms) |
| 109 | 爱绿软-收集分享各类有趣好玩的绿色软件 | https://ilvruan.com/ | timeout(15000ms) |
| 111 | Windows 下 OpenCore 安装 macOS BigSur 教程 (完整版) … | https://macx.top/22374.html | timeout(15000ms) |
| 112 | 黑苹果EFI - 免费分享黑苹果EFI信息 | https://macefi.com/ | timeout(15000ms) |
| 116 | 寻它网址 \| 寻它网址导航！ | https://kingsanne.com/ | timeout(15000ms) |
| 120 | 网易云到Youtube Music, Spotify, Apple Music | https://yyrcd.com/2018/11/14/n2s-zh/ | timeout(15000ms) |
| 121 | win10系统右下角网络图标异常的解决方法 - win10之家 | https://www.2016win10.com/w10/5245.html | timeout(15000ms) |
| 126 | 旁门左道小一白 | https://hyczby.com/ | timeout(15000ms) |
| 129 | 小米路由器SSH密码计算器 | https://mi.tellme.top/ | timeout(15000ms) |
| 130 | 微软远程桌面优化-CSDN博客 | https://blog.csdn.net/weixin_45642871/article/details/13220… | timeout(15000ms) |

**连接被对端关闭（网络层）（10）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 9 | switch游戏 | https://sway.cloud.microsoft/1zyVegf0n3cHdR8o | net::ERR_CONNECTION_CLOSED |
| 26 | C4DSKY\|书生 | https://c4dsky.com/ | net::ERR_CONNECTION_CLOSED |
| 30 | Sketchfab | https://sketchfab.com/ | net::ERR_CONNECTION_CLOSED |
| 38 | Ub文字转语音 | https://app.uberduck.ai/text-to-voice | net::ERR_CONNECTION_CLOSED |
| 39 | Comfyui工作流 | https://comfyworkflows.com/ | net::ERR_CONNECTION_CLOSED |
| 45 | 半次元 | https://bcy.net | net::ERR_CONNECTION_CLOSED |
| 48 | 动漫之家 | https://manhua.dmzj.com/ | net::ERR_CONNECTION_CLOSED |
| 72 | 在线漫画,手机漫画,动漫之家漫画网手机版 | https://m.dmzj.com/# | net::ERR_CONNECTION_CLOSED |
| 97 | Z-Library – 世界上最大的电子图书馆。自由访问知识和文化。 | https://zh.singlelogin.re/ | net::ERR_CONNECTION_CLOSED |
| 98 | Receive SMS Online \| Temporary SMS and Disp… | https://smsreceivefree.com/ | net::ERR_CONNECTION_CLOSED |

**本机/局域网服务未运行（3）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 10 | Sunshine | https://localhost:47990/ | net::ERR_CONNECTION_REFUSED |
| 81 | 仪表盘 - Cpolar | http://localhost:9200/#/dashboard | net::ERR_CONNECTION_REFUSED |
| 128 | yacd | http://192.168.2.1:9999/ui/#/ | net::ERR_CONNECTION_REFUSED |

**HTTP 504 服务端/其他错误（1）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 11 | 软件 - IPA商店 | https://ipa.store/app | 504 |

**HTTP 412 服务端/其他错误（1）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 16 | 中国卫健委 | http://www.nhc.gov.cn/wjw/fyjk/wsbz.shtml | 412 |

**资源/下载被浏览器拦截，未渲染（22）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 20 | 全景网 | http://www.quanjing.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 31 | 云模型 | http://www.yunmoxing.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 36 | AI研究所 - 国内外AI工具资讯首发站 | http://www.aiyjs.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 44 | 每日一文 | http://meiriyiwen.com | net::ERR_BLOCKED_BY_CLIENT |
| 47 | 抽屉新热榜 | http://dig.chouti.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 52 | 免费下载PNG | http://www.996pic.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 53 | WE LOVE AD \| 全球广告数据库与创意人社群 | http://www.welovead.com/cn/ | net::ERR_BLOCKED_BY_CLIENT |
| 74 | 作品集 - D5渲染器 | http://cn.d5render.com/gallery | net::ERR_BLOCKED_BY_CLIENT |
| 84 | 菜鸟C4D交流论坛-与你一起从零开始! | http://www.c4dcn.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 89 | 动效中期表 motiontable | http://foxcodex.html.xdomain.jp/index.html | net::ERR_BLOCKED_BY_CLIENT |
| 92 | 虚幻时代  素材网站 | http://xuhuantime.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 94 | PDF电子书-书享家 | http://shuxiangjia.cn/?cate=3 | net::ERR_BLOCKED_BY_CLIENT |
| 95 | [搬书匠] - 电子书(EBook) | http://www.banshujiang.cn/ | net::ERR_BLOCKED_BY_CLIENT |
| 101 | Mountain Lion GM(12A269)纯净懒人版支持硬盘助手 [BT种子 离线… | http://www.kenapple.com/forum.php?mod=viewthread&tid=197626… | net::ERR_BLOCKED_BY_CLIENT |
| 102 | 双系统WIN7+狮子详细安装+驱动过程【附全部工具及大量截图】 - Mac OS X 1… | http://www.kenapple.com/thread-117247-1-1.html | net::ERR_BLOCKED_BY_CLIENT |
| 106 | MXC | http://www.itmxc.com/forum.php | net::ERR_BLOCKED_BY_CLIENT |
| 107 | 工具 - 设优SHEUI | http://www.sheui.com/Tools/ | net::ERR_BLOCKED_BY_CLIENT |
| 108 | 新新漫画 - 在线漫画 - 耽美人生 - 最全热血耽美BL漫画 | http://www.77mh.com/ | net::ERR_BLOCKED_BY_CLIENT |
| 110 | 啃苹果论坛_(Apple)苹果电脑,iPhone手机,Mac,祯元翰通,苹果技术及应用交… | http://www.kenapple.com/forum.php | net::ERR_BLOCKED_BY_CLIENT |
| 114 | kf.sf-express.com/css/myquery/appleTrack.act… | http://kf.sf-express.com/css/myquery/appleTrack.action?resp… | net::ERR_BLOCKED_BY_CLIENT |
| 119 | 卤蛋君分享 - Good Luck To You! | http://abbw.ml/ | net::ERR_BLOCKED_BY_CLIENT |
| 123 | 植物僵尸相关下载 | http://pvz4.lonelystar.org/download.htm#top | net::ERR_BLOCKED_BY_CLIENT |

**HTTP 200 服务端/其他错误（3）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 49 | 晴天吉他谱_周杰伦晴天弹唱谱-吉他堂 | https://www.jitatang.com/qingtian.html | 200 |
| 79 | 联机游戏-SGAMES | https://www.s-sgames.com/multiple/ | 200 |
| 96 | 书单网 | https://www.shudan.vip/ | 200 |

**TLS 协议错误（1）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 57 | Terraform4D 地形插件 | https://www.terraform4d.com/terraform4d/building-terrains/o… | net::ERR_SSL_PROTOCOL_ERROR |

**空响应/下载（3）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 82 | 橱窗 | https://www.behance.net/gallery/26931445/Cavendish-Beyond-W… | net::ERR_HTTP_RESPONSE_CODE_FAILURE |
| 85 | C4DNB-荔枝资源站-温馨精干的小站 | http://c4dnb.com/ | net::ERR_HTTP_RESPONSE_CODE_FAILURE |
| 132 | simiely chen on Behance | https://www.behance.net/billpotterfb2b?tracking_source=sear… | net::ERR_HTTP_RESPONSE_CODE_FAILURE |

**重定向环（1）**

| # | 标题 | URL | 状态/错误 |
|---|---|---|---|
| 115 | 电子书文件 - OneDrive | https://a1od-my.sharepoint.com/personal/a319_my365_tech/_la… | net::ERR_TOO_MANY_REDIRECTS |

## 5. 逐条结果（132 条）

| # | 标题 | URL | 最终 URL | 状态 | 判定 | 依据 |
|---|---|---|---|---|---|---|
| 1 | 外网图库 | https://w2e0b1d6av.gd.ddnsto.com:443 | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_INVALID_AUTH_CREDENTIALS |
| 2 | Dpanl外网 | https://sidmiepaanily.gd.ddnsto.com:443 | https://sidmiepaanily.gd.ddnsto.com/ | 503 | KEEP_MANUAL | HTTP 503 服务端错误，不自动删 |
| 3 | win7外网 | https://simiwimiln7y.gd.ddnsto.com:443 | https://simiwimiln7y.gd.ddnsto.com/ | 503 | KEEP_MANUAL | HTTP 503 服务端错误，不自动删 |
| 4 | The Movie D | https://www.themoviedb.org/movie?language=zh-CN | https://www.themoviedb.org/movie?language=zh-CN | 200 | KEEP | HTTP 200 正常渲染（正文 1256 字） |
| 5 | 精靈學院 | https://portal.riolu-node.link/dashboard | https://portal.riolu-node.link/dashboard | 503 | KEEP_MANUAL | HTTP 503 服务端错误，不自动删 |
| 6 | 花瓣 | http://huaban.com | https://huaban.com/ | 405 | KEEP_MANUAL | HTTP 405 其他 4xx，不自动删 |
| 7 | 网盘搜索 | https://ysxjjkl.souyisou.top/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 8 | 一元机场 | https://xn--4gq62f52gdss.com/#/subscribe | https://xn--4gq62f52gdss.com/#/subscribe | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证 |
| 9 | switch游戏 | https://sway.cloud.microsoft/1zyVegf0n3cHdR8o | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 10 | Sunshine | https://localhost:47990/ | about:blank | — | KEEP_MANUAL | 本机地址，Sunshine 服务未运行不等于失效 |
| 11 | 软件 - IPA商店 | https://ipa.store/app | https://ipa.store/app | 504 | KEEP_MANUAL | HTTP 504 服务端错误，不自动删 |
| 12 | 解密IPA商店 | https://decrypt.day/ | https://decrypt.day/ | 451 | KEEP | HTTP 451 反爬/鉴权，不自动删；Cloudflare验证 |
| 13 | 懒得勤快 | https://masuit.com/ | https://masuit.net/ | 200 | KEEP | 已迁移到 masuit.net，落点为浏览器环境验证页（反爬） |
| 14 | 软件收集 | https://linux.do/t/topic/49853 | https://linux.do/t/topic/49853 | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 15 | 精靈學院 | https://master.riolu.sbs/dashboard | https://master.riolu.sbs/dashboard | 503 | KEEP_MANUAL | HTTP 503 服务端错误，不自动删 |
| 16 | 中国卫健委 | http://www.nhc.gov.cn/wjw/fyjk/wsbz.shtml | https://www.nhc.gov.cn/wjw/fyjk/wsbz.shtml | 412 | KEEP_MANUAL | HTTP 412 其他 4xx，不自动删 |
| 17 | ArtStation | https://www.artstation.com/ | https://www.artstation.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 18 | 视觉中国 - 首页 | https://www.vcg.com/ | https://www.vcg.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；验证码/人机验证 |
| 19 | 美工云 | http://www.meigongyun.com/ | https://meigongyun.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 20 | 全景网 | http://www.quanjing.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 21 | 安鱼设计资源 | http://www.anyusj.com/ | https://www.anyusj.com/ | 200 | KEEP | HTTP 200 正常渲染（正文 1785 字） |
| 22 | Behance | https://www.behance.net/ | chrome-error://chromewebdata/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 23 | Twinmotion | https://www.twinmotion.com/zh-CN | https://www.twinmotion.com/zh-CN | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 24 | Epic UE5 | https://www.unrealengine.com/zh-CN/ | https://www.unrealengine.com/zh-CN/?__cf_chl_rt_tk=9JG84U… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 25 | 爱学习的老土狗 | https://www.cgltgcg.com/ | https://www.cgltgcg.com/ | 200 | KEEP | 登录墙/验证码/人机验证，保留标注：验证码/人机验证 |
| 26 | C4DSKY\|书生 | https://c4dsky.com/ | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 27 | 爱给网 | http://www.aigei.com/ | https://www.aigei.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 28 | Free3D | https://free3d.com/ | https://free3d.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 29 | 3D Models | https://www.turbosquid.com/ | https://www.turbosquid.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 30 | Sketchfab | https://sketchfab.com/ | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 31 | 云模型 | http://www.yunmoxing.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 32 | TMDB | https://www.themoviedb.org/?language=zh-CN | https://www.themoviedb.org/?language=zh-CN | 200 | KEEP | HTTP 200 正常渲染（正文 781 字） |
| 33 | 電視電影 | https://kimivod.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 34 | 字幕库 | https://srtku.com/ | https://srtku.com/ | 404 | KEEP | 返回 404 但实为站点防火墙验证码页，非死链 |
| 35 | Civitai | https://civitai.com/ | https://civitai.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证/验证码/人机验证 |
| 36 | AI研究所 - 国内外AI工具资讯首发站 | http://www.aiyjs.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 37 | 微软绘图 | https://www.bing.com/create | https://cn.bing.com/images/create/ai-image-generator | 404 | SUSPECT | Bing 图片创建器 404 页面，属常用服务，先列待确认 |
| 38 | Ub文字转语音 | https://app.uberduck.ai/text-to-voice | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 39 | Comfyui工作流 | https://comfyworkflows.com/ | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 40 | Le Chat | https://chat.mistral.ai/chat | https://chat.mistral.ai/chat?__cf_chl_rt_tk=rcdgivg4ej2Mr… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证 |
| 41 | ArtStation | https://www.artstation.com/?sort_by=community&dimension=a… | https://www.artstation.com/?sort_by=community&dimension=a… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 42 | 登录 - 精靈學院 | https://user.riolu-node.link/login | https://user.riolu-node.link/login | 503 | KEEP_MANUAL | HTTP 503 服务端错误，不自动删 |
| 43 | Steam | https://store.steampowered.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 44 | 每日一文 | http://meiriyiwen.com | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 45 | 半次元 | https://bcy.net | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 46 | 小众技术资源库 | https://www.xiaozhongjishu.com/?from=itab | https://www.xiaozhongjishu.com/?from=itab | 200 | KEEP | HTTP 200 正常渲染（正文 6833 字） |
| 47 | 抽屉新热榜 | http://dig.chouti.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 48 | 动漫之家 | https://manhua.dmzj.com/ | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 49 | 晴天吉他谱_周杰伦晴天弹唱谱-吉他堂 | https://www.jitatang.com/qingtian.html | https://www.jitatang.com/qingtian.html | 200 | KEEP_MANUAL | HTTP 200 正文近乎为空（10 字），需人工 |
| 50 | 学习如何使用强大的实时3D创作品台——虚幻引擎 - Unreal Engine | https://www.unrealengine.com/zh-CN/learn?lang=zh-CN&state… | https://www.unrealengine.com/zh-CN/learn?lang=zh-CN&state… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 51 | 虚幻引擎4文档 \| 虚幻引擎文档 | https://docs.unrealengine.com/4.27/zh-CN/ | https://docs.unrealengine.com/4.27/zh-CN/?__cf_chl_rt_tk=… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 52 | 免费下载PNG | http://www.996pic.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 53 | WE LOVE AD \| 全球广告数据库与创意人社群 | http://www.welovead.com/cn/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 54 | POCO摄影图片社区 - 领先的时尚摄影平台，摄影视频，摄影爱好者分享作品和技巧首… | https://photo.poco.cn/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 55 | 素材庫相片，免版稅影像、圖像、向量圖和影片 \| Adobe Stock | https://stock.adobe.com/hk/ | https://stock.adobe.com/hk/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 56 | 免费资源网 | https://www.mfzyw1.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 57 | Terraform4D 地形插件 | https://www.terraform4d.com/terraform4d/building-terrains… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_SSL_PROTOCOL_ERROR |
| 58 | Search Results \| 3D Models | https://rendercrate.com/3D/search/#!/new | https://rendercrate.com/3D/search#!/new | 404 | SUSPECT | 目标搜索页 404 且正文为空，疑似失效，先列待确认 |
| 59 | Veer免费图库 - 可商用正版图片素材库 | https://free.veer.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 60 | TinEye Reverse Image Search | https://www.tineye.com/ | https://www.tineye.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证 |
| 61 | 英文字体 \| 设计达人 | http://www.shejidaren.com/tag/%e8%8b%b1%e6%96%87%e5%ad%97… | https://www.shejidaren.com/tag/%e8%8b%b1%e6%96%87%e5%ad%9… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 62 | 有哪些堪称「神器」，却鲜为人知的软件/网站/互联网服务？ - 知乎 | https://www.zhihu.com/question/36546814/answer/80647839?u… | https://www.zhihu.com/question/36546814/answer/80647839?u… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 63 | 谷歌商城应用 | https://apkdl.in/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 64 | 正版中国 \| 正版软件限时免费 | https://getitfree.cn/ | https://getitfree.cn/ | 404 | DELETE | HTTP 404 明确错误页 |
| 65 | V2Ray / SSR 传输协议哪个好? (各种协议对比) - 无主界 | https://www.idleleo.com/05/2071.html | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 66 | APP搜索 - Sunny在线工具 | https://tools.lancely.tech/apple/app-search | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 67 | 3元海淘8元奈飞 | https://joyo96.cc/user | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 68 | 最好用的国内免费接码平台大全 \| 免费临时手机号 \| 免费手机号 - 一灯不是… | https://iyideng.fun/welfare/best-domestic-platform-for-re… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 69 | 小米路由器yacd | http://192.168.31.1:9999/ui/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 70 | 九九美剧 - 天天美剧在线观看_美剧迷真正的美剧天堂 | https://www.99meiju.vip/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 71 | 全网影片搜索 - MyFreeMP4 | https://lab.liumingye.cn/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 72 | 在线漫画,手机漫画,动漫之家漫画网手机版 | https://m.dmzj.com/# | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 73 | Mp4电影_2022最新高清MP4电影资源下载 | https://www.dbmp4.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 74 | 作品集 - D5渲染器 | http://cn.d5render.com/gallery | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 75 | MetaHuman Creator | https://metahuman.unrealengine.com/ | https://www.epicgames.com/id/authorize?response_type=code… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 76 | CentiLeo Renderer | https://centileo.com/ | https://centileo.com/?__cf_chl_rt_tk=70XDAaQVgk4pXW_oeZ4q… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证 |
| 77 | 在 Steam 上激活产品 | https://store.steampowered.com/account/registerkey | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 78 | Xbox-Now - Xbox数字游戏世界范围比价 | https://www.xbox-now.com/zh/game-comparison | https://www.xbox-now.com/zh/game-comparison | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证/验证码/人机验证 |
| 79 | 联机游戏-SGAMES | https://www.s-sgames.com/multiple/ | chrome-error://chromewebdata/ | 200 | KEEP_MANUAL | 浏览器导航落到 chrome-error 页，渲染失败需人工 |
| 80 | 邮箱Free Email Addresses: Web based and sec… | https://navigator-lxa.mail.com/home?sid=959359fcf95f9b420… | https://www.mail.com/ | 200 | SUSPECT | 跳转到其他地址：https://www.mail.com/ |
| 81 | 仪表盘 - Cpolar | http://localhost:9200/#/dashboard | about:blank | — | KEEP_MANUAL | 本机地址，Cpolar 服务未运行不等于失效 |
| 82 | 橱窗 | https://www.behance.net/gallery/26931445/Cavendish-Beyond… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_HTTP_RESPONSE_CODE_FAILURE |
| 83 | 红色gif | http://www.min-liu.com/bloody-dairy-a-daily-animation-pro… | https://www.min-liu.com/bloody-dairy-a-daily-animation-pr… | 404 | DELETE | HTTP 404 明确错误页 |
| 84 | 菜鸟C4D交流论坛-与你一起从零开始! | http://www.c4dcn.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 85 | C4DNB-荔枝资源站-温馨精干的小站 | http://c4dnb.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_HTTP_RESPONSE_CODE_FAILURE |
| 86 | R站\|学习使我快乐！ - 专注C4D三维\|建模\|渲染\|后期\|动效\|绑定… | https://www.btbat.com/ | https://www.btbat.com/ | 200 | DELETE | 错误页特征：notfound:404（正文 19 字） |
| 87 | 免费正版高清图片素材库 超过2.5百万张优质图片和视频素材可供免费使用和下载 | https://pixabay.com/zh/ | https://pixabay.com/zh/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证/验证码/人机验证 |
| 88 | CGer.com\|艺术\|设计\|技术\|建筑\|软件行业网站搜集。资源\|模型… | https://www.cger.com/ | https://www.cger.com/ | 405 | KEEP_MANUAL | HTTP 405 其他 4xx，不自动删 |
| 89 | 动效中期表 motiontable | http://foxcodex.html.xdomain.jp/index.html | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 90 | 虚幻引擎中的基于物理的材 \| 虚幻引擎文档 | https://docs.unrealengine.com/5.0/zh-CN/physically-based-… | https://docs.unrealengine.com/5.0/zh-CN/physically-based-… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 91 | 虚幻商城 - 虚幻引擎商城 | https://www.unrealengine.com/marketplace/zh-CN/store?lang… | https://www.unrealengine.com/marketplace/zh-CN/store?lang… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 92 | 虚幻时代  素材网站 | http://xuhuantime.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 93 | 面包读书会 | https://kindlebookfor.me/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 94 | PDF电子书-书享家 | http://shuxiangjia.cn/?cate=3 | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 95 | [搬书匠] - 电子书(EBook) | http://www.banshujiang.cn/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 96 | 书单网 | https://www.shudan.vip/ | https://www.shudan.vip/ | 200 | KEEP_MANUAL | HTTP 200 正文近乎为空（10 字），需人工 |
| 97 | Z-Library – 世界上最大的电子图书馆。自由访问知识和文化。 | https://zh.singlelogin.re/ | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 98 | Receive SMS Online \| Temporary SMS and D… | https://smsreceivefree.com/ | about:blank | — | KEEP_MANUAL | 连接被对端关闭（网络层，非站点失效） |
| 99 | Bandwagon Host - Client Area | https://bwh8.net/clientarea.php?action=products | https://bwh8.net/clientarea.php?action=products | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证/验证码/人机验证 |
| 100 | 我在等风也等你 \| 感谢你能来，也不遗憾你离开 | https://www.binye.xyz/ | https://www.binye.xyz/ | 200 | KEEP | HTTP 200 正常渲染（正文 4420 字） |
| 101 | Mountain Lion GM(12A269)纯净懒人版支持硬盘助手 [BT种子… | http://www.kenapple.com/forum.php?mod=viewthread&tid=1976… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 102 | 双系统WIN7+狮子详细安装+驱动过程【附全部工具及大量截图】 - Mac OS … | http://www.kenapple.com/thread-117247-1-1.html | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 103 | Home \| tonymacx86.com | https://www.tonymacx86.com/ | https://www.tonymacx86.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证/验证码/人机验证 |
| 104 | 未越狱福利，破解软件ipa合集分享，更新最新不闪退迅雷5.21版（不是老版本...… | https://bbs.feng.com/forum.php?mod=viewthread&tid=11250834 | https://www.feng.com/NoDetail?status=1&text=%E6%97%A0%E6%… | 200 | SUSPECT | 跳转到其他地址：https://www.feng.com/NoDetail?status=1&text=%E6%9… |
| 105 | 苹果软件园_苹果游戏_Mac游戏_苹果软件_Mac软件 | http://www.macapp.cc/ | https://www.macapp.cc/ | 200 | DELETE | 停放/过期页特征：parked:域名过期/待售 |
| 106 | MXC | http://www.itmxc.com/forum.php | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 107 | 工具 - 设优SHEUI | http://www.sheui.com/Tools/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 108 | 新新漫画 - 在线漫画 - 耽美人生 - 最全热血耽美BL漫画 | http://www.77mh.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 109 | 爱绿软-收集分享各类有趣好玩的绿色软件 | https://ilvruan.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 110 | 啃苹果论坛_(Apple)苹果电脑,iPhone手机,Mac,祯元翰通,苹果技术及… | http://www.kenapple.com/forum.php | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 111 | Windows 下 OpenCore 安装 macOS BigSur 教程 (完整… | https://macx.top/22374.html | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 112 | 黑苹果EFI - 免费分享黑苹果EFI信息 | https://macefi.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 113 | 【技术贴】美s版a1453（无锁）破解电信4g教程_iphone5s吧_百度贴吧 | http://tieba.baidu.com/p/3770855131 | https://tieba.baidu.com/p/3770855131 | 200 | KEEP | HTTP 200 正常渲染（正文 47 字） |
| 114 | kf.sf-express.com/css/myquery/appleTrack.… | http://kf.sf-express.com/css/myquery/appleTrack.action?re… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 115 | 电子书文件 - OneDrive | https://a1od-my.sharepoint.com/personal/a319_my365_tech/_… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_TOO_MANY_REDIRECTS |
| 116 | 寻它网址 \| 寻它网址导航！ | https://kingsanne.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 117 | NO视频 – 不负追剧好时光 (￣▽￣)" | https://www.novipnoad.com/ | https://www.novipnoad.com/?__cf_chl_rt_tk=W2ZilVH_MM.UvvV… | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删；Cloudflare验证/验证码/人机验证 |
| 118 | 【图片】给大家分享几个可用的代理IP和端口【iobit吧】_百度贴吧 | http://tieba.baidu.com/p/6257751691 | https://tieba.baidu.com/p/6257751691 | 200 | KEEP | HTTP 200 正常渲染（正文 47 字） |
| 119 | 卤蛋君分享 - Good Luck To You! | http://abbw.ml/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 120 | 网易云到Youtube Music, Spotify, Apple Music | https://yyrcd.com/2018/11/14/n2s-zh/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 121 | win10系统右下角网络图标异常的解决方法 - win10之家 | https://www.2016win10.com/w10/5245.html | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 122 | C4D控制失灵，其实是和QQ冲突，QQ防盗钩子所致。【c4d吧】_百度贴吧 | http://tieba.baidu.com/p/5992982445 | https://tieba.baidu.com/p/5992982445 | 200 | KEEP | HTTP 200 正常渲染（正文 47 字） |
| 123 | 植物僵尸相关下载 | http://pvz4.lonelystar.org/download.htm#top | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_BLOCKED_BY_CLIENT |
| 124 | 网盘搜索-小白盘 | https://www.xiaobaipan.com/ | https://www.xiaobaipan.com/ | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 125 | 【视频】火影忍者：鸣人恶作剧，假装自己的头被吃掉，把大和整个人都吓白了！_火影忍者… | https://tieba.baidu.com/p/5955948661?red_tag=1881262804&t… | https://tieba.baidu.com/p/5955948661?red_tag=1881262804&t… | 200 | KEEP | HTTP 200 正常渲染（正文 47 字） |
| 126 | 旁门左道小一白 | https://hyczby.com/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 127 | 免 SHSH 从 iOS 9.0 ~ 9.3.5 降级到 iOS 8.4.1 如果… | http://tieba.baidu.com/p/5406211052 | https://tieba.baidu.com/p/5406211052 | 200 | KEEP | HTTP 200 正常渲染（正文 47 字） |
| 128 | yacd | http://192.168.2.1:9999/ui/#/ | about:blank | — | KEEP_MANUAL | 局域网路由器地址，未连接当前网络不等于失效 |
| 129 | 小米路由器SSH密码计算器 | https://mi.tellme.top/ | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 130 | 微软远程桌面优化-CSDN博客 | https://blog.csdn.net/weixin_45642871/article/details/132… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：timeout(15000ms) |
| 131 | 下载文件失败提示病毒扫描失败怎么解决_scanwithantivirus-CSDN… | https://blog.csdn.net/yuemouren/article/details/131147851 | https://blog.csdn.net/yuemouren/article/details/131147851 | 403 | KEEP | HTTP 403 反爬/鉴权，不自动删 |
| 132 | simiely chen on Behance | https://www.behance.net/billpotterfb2b?tracking_source=se… | about:blank | — | KEEP_MANUAL | 无头浏览器未能完成加载：net::ERR_HTTP_RESPONSE_CODE_FAILURE |

## 6. 清理后数据与安装校验

| 项 | 值 |
|---|---|
| 总条目 | **558**（562 − 4） |
| 常用 | 65 |
| 本地工具 | 22 |
| 小工具 | 55 |
| 搜索 | 21 |
| 软件 | 75 |
| 信息 | 42 |
| 参考 | 28 |
| 素材 | 23 |
| ai视频 | 6 |
| 教程 | 112 |
| MCP | 5 |
| 三维素材 | 40 |
| 视频&音乐 | 38 |
| Ai | 26 |
| 一级 / 二级文件夹 | 1 / 14 |
| 未分类 / 空标题 / 重复 URL | 0 / 0 / 0 |
| 校验脚本 | `total = sum(categories) = 558`，`errors 0` |
| 安装版本 | `0.1.10.0`（Release，标准 install.ps1 -NoBump；LocalState 656 文件快照恢复，SHA256 全一致，Status Ok） |
| links.json SHA256 | `9A793E6B086D4DD4552F37D18FD286623BFE1A3E73BAF5D45A699BC47D820BD5` |
| UIA 首页 | 顶层 5 项 → 链接库首页 3 项（网页链接 / 新建文件夹 / 批量编辑） |
| UIA 二级 | 14 项，「常用」第一 |
| UIA 各分类 | 常用 65、本地工具 22、小工具 55、搜索 21、软件 75、信息 42、参考 28、素材 23、ai视频 6、教程 112、MCP 5、三维素材 40、视频&音乐 38、Ai 26，全部与数据一致（BAD=0） |

## 7. 证据文件

- 逐条原始记录：`cmdpaplinks-data-backup/browser-probe-20261107-100315.jsonl`（132 行，含 URL/最终 URL/状态/标题/正文长度/证据片段）
- 汇总 JSON：`browser-probe-20261107-100315-summary.json`
- 删除前备份：`links-before-browser-cleanup-20261107-102305.json`
- 删除明细：`browser-cleanup-20261107-102305.json`

## 8. 判定口径

- **删**：HTTP 404/410、页面标题或正文明确「404 / Not Found / 页面不存在」、nginx 等默认错误页、域名停放/过期/待售页。
- **留**：401/403/429/451、Cloudflare 挑战、验证码、登录墙，但站点或页面本身有效；正常 200 渲染。
- **疑似**：跳转到首页/搜索页、目标页被替换成「无此页面」提示，先不删。
- **需人工**：15s 超时、连接被对端关闭、TLS、下载/拦截、本机或局域网服务未启动；网络层问题不等于站点失效。
- 反例修正：`srtku.com` 返回 404 但页面是「网站防火墙/验证码」，按保留处理；`bing.com/create` 的 404 页面列入疑似而非删除。
