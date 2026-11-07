# Edge 收藏夹合并、去重、失效清理与重分类报告

> 执行日期：2026-11-07 · 插件版本 0.1.9.0 · 数据文件 schema 3
>
> 后续：0.1.10.0 已把本报告中的 132 条「待确认」用 headless Edge 逐条真实渲染复核，删除 4 条，库降至 **558** 条，
> 见 [browser-recheck-report.md](browser-recheck-report.md)。本页 562 为 0.1.9.0 阶段的历史数字。

## 一、结论概览

| 项目 | 数值 |
|---|---|
| 原有链接库条目 | 261（260 URL + 1 本地文件夹 `D:\Tool`） |
| Edge 收藏条目 | 418（书签栏 416 / 其他书签 0 / 移动收藏夹 2） |
| 合并后（去重） | 578 |
| 失效清理后 | 563 |
| 补漏重复清理后（最终） | **562** |
| 一级文件夹 | 1（`网页链接`） |
| 二级文件夹 | 14（`常用` + 13 个分类） |
| 未分类 / 空标题 / 重复 URL | 0 / 0 / 0 |

原有 261 条中：**清理 6 条**（均为 404）、保留在「常用」65 条、其余 190 条参与重分类（其中 17 条换了文件夹、173 条维持原分类）。

## 二、阶段 1：只读读入 Edge 收藏夹

- 源文件：`%LOCALAPPDATA%\Microsoft\Edge\User Data\Profile 2\Bookmarks`
- 方式：先复制到 `C:\Temp\edge-bookmarks-copy.json`，只解析副本；原文件未改动。
- 结果：418 条收藏、33 个文件夹节点、最大层级 4、空标题 0。
- 分布：书签栏 416 条、其他书签 0 条、移动收藏夹 2 条。
- 保留字段：层级路径、标题、URL、添加时间（Chromium 时间戳换算后写入条目的 `createdAt`）。
- 1 条 `javascript:` 伪书签不进入链接库（不是可打开的网址）。

## 三、阶段 2：合并与去重

URL 归一化规则：scheme/host 小写、去默认端口、去 fragment、去 `utm_*`/`gclid`/`fbclid` 等跟踪参数、解 URL 编码、去尾斜杠。

| 指标 | 数值 |
|---|---|
| 归一化重复组 | 97 |
| 完全重复（同 URL 同标题） | 23 组 |
| 同 URL 不同标题 | 74 组 |
| 两库交叉重复组 | 97 组（旧库 97 条 / Edge 100 条） |
| Edge 内部重复 | 3 组 |
| 旧库内部重复 | 0 组 |
| Edge 重复条目未导入 | 100 条 |
| 同一站点不同页面（只报告） | 52 个站点 / 181 个 URL |

合并策略：保留原有条目（有图标、有分类、有使用记录），Edge 的标题与旧标题不同时追加为别名；共补充别名 75 个。Edge 独有的 317 条先写入 `网页链接/待分类`，参与后续统一重分类。

## 四、阶段 3：失效清理

探测范围：**577 个 URL**（含原有 261 条中的全部 URL，不是只查 Edge）；`D:\Tool` 为本地文件夹，单独检查存在性（存在）。

| 结果 | 条数 |
|---|---|
| 可达 | 430 |
| 待确认（403/401/429/超时/TLS/5xx 等） | 128 |
| 明确失效（404/410/DNS 等） | 19 |
| 网络级失败占比 | 8.8%（阈值 30%，未触发中止） |

19 条 404 逐条复核响应体后，**15 条确认为真 404 并删除**，4 条为软 404（站点本身可访问，仅深链失效）转入待确认清单。

### 已删除的明确失效条目（15 条）

| 来源 | 标题 | URL |
|---|---|---|
| Edge 新增 | GitHub - s337443501/NeteaseMusicCloudMatch | https://github.com/s337443501/NeteaseMusicCloudMatch |
| Edge 新增 | GitHub - xxxily/v2ray.fun: 全新的v2ray.fun脚本， 支持多用户多端口管理 | https://github.com/xxxily/v2ray.fun |
| Edge 新增 | WebHD.top | https://webhd.top/ |
| Edge 新增 | ondrive host | https://github.com/Marukon/Microsoft-Hosts |
| Edge 新增 | vip解析-全网在线视频解析、音乐解析、短视频解析_聚神铺 | http://www.jspooo.com/col.jsp?id=154 |
| Edge 新增 | writingo.net/document | https://www.writingo.net/document |
| Edge 新增 | 红米 AX6S 性能远超想象，解锁 SSH 安装 ShellClash｜刷 openwrt 教程 | 酱紫表 | https://qust.me/post/ax6s/ |
| Edge 新增 | 网站导航 | http://max77.cn/maxa.html |
| Edge 新增 | 配置IPv6隧道，IP被墙的另一种解决方案 - 明镜录 | https://www.mingjinglu.com/coding/504.html |
| 原有 261 | C4D模型 | https://www.cgufo.com/cgziyuan/c4d/c4dmodel |
| 原有 261 | Simiely/hub-world | https://github.com/Simiely/hub-world |
| 原有 261 | iPA应用商店_iOS免签安装, | https://png.0210.net/index.php |
| 原有 261 | 世界的风吹向你 / 探索 | https://simiely.github.io/hub-world/ |
| 原有 261 | 什么值得读 | https://shenmezhidedu.com/?from=itab |
| 原有 261 | 零点 FRP | https://www.frp0.top/client/dashboard |

### 待确认清单（132 条，未删除）

软 404 的 4 条：字幕库 `srtku.com`（防火墙）、微软绘图 `bing.com/create`（跳转到图像创建）、`rendercrate.com`（应用壳）、`min-liu.com`（站点活着但深链 404）。其余为 401/403/429/超时/TLS/5xx。

按原因统计：http-403 41、tls-error 37、http-502 19、timeout 10、http-503 5、http-404 4、connection-error 3、http-401 2、http-405 2、http-444 2、http-521 2、http-504 1、http-451 1、http-412 1、proxy-error 1、other:TooManyRedirects 1。

| 来源 | 状态/原因 | 标题 | URL |
|---|---|---|---|
| Edge 新增 | None / connection-error | 仪表盘 - Cpolar | http://localhost:9200/#/dashboard |
| Edge 新增 | None / connection-error | yacd | http://192.168.2.1:9999/ui/#/ |
| Edge 新增 | 401 / http-401 | 联机游戏-SGAMES | https://www.s-sgames.com/multiple/ |
| Edge 新增 | 403 / http-403 | 学习如何使用强大的实时3D创作品台——虚幻引擎 - Unreal Engine | https://www.unrealengine.com/zh-CN/learn?lang=zh-CN&state=%2F5.0%2Fzh-CN%2FWelcome%2F |
| Edge 新增 | 403 / http-403 | 虚幻引擎4文档 | 虚幻引擎文档 | https://docs.unrealengine.com/4.27/zh-CN/ |
| Edge 新增 | 403 / http-403 | 素材庫相片，免版稅影像、圖像、向量圖和影片 | Adobe Stock | https://stock.adobe.com/hk/ |
| Edge 新增 | 403 / http-403 | TinEye Reverse Image Search | https://www.tineye.com/ |
| Edge 新增 | 403 / http-403 | 英文字体 | 设计达人 | http://www.shejidaren.com/tag/%e8%8b%b1%e6%96%87%e5%ad%97%e4%bd%93 |
| Edge 新增 | 403 / http-403 | 有哪些堪称「神器」，却鲜为人知的软件/网站/互联网服务？ - 知乎 | https://www.zhihu.com/question/36546814/answer/80647839?utm_source=wechat_session&utm_medium=social& |
| Edge 新增 | 403 / http-403 | MetaHuman Creator | https://metahuman.unrealengine.com/ |
| Edge 新增 | 403 / http-403 | CentiLeo Renderer | https://centileo.com/ |
| Edge 新增 | 403 / http-403 | Xbox-Now - Xbox数字游戏世界范围比价 | https://www.xbox-now.com/zh/game-comparison |
| Edge 新增 | 403 / http-403 | 橱窗 | https://www.behance.net/gallery/26931445/Cavendish-Beyond-Words |
| Edge 新增 | 403 / http-403 | 免费正版高清图片素材库 超过2.5百万张优质图片和视频素材可供免费使用和下载 | https://pixabay.com/zh/ |
| Edge 新增 | 403 / http-403 | 虚幻引擎中的基于物理的材 | 虚幻引擎文档 | https://docs.unrealengine.com/5.0/zh-CN/physically-based-materials-in-unreal-engine/ |
| Edge 新增 | 403 / http-403 | 虚幻商城 - 虚幻引擎商城 | https://www.unrealengine.com/marketplace/zh-CN/store?lang=zh-CN |
| Edge 新增 | 403 / http-403 | Bandwagon Host - Client Area | https://bwh8.net/clientarea.php?action=products |
| Edge 新增 | 403 / http-403 | Home | tonymacx86.com | https://www.tonymacx86.com/ |
| Edge 新增 | 403 / http-403 | 【技术贴】美s版a1453（无锁）破解电信4g教程_iphone5s吧_百度贴吧 | http://tieba.baidu.com/p/3770855131 |
| Edge 新增 | 403 / http-403 | NO视频 – 不负追剧好时光 (￣▽￣)" | https://www.novipnoad.com/ |
| Edge 新增 | 403 / http-403 | 【图片】给大家分享几个可用的代理IP和端口【iobit吧】_百度贴吧 | http://tieba.baidu.com/p/6257751691 |
| Edge 新增 | 403 / http-403 | C4D控制失灵，其实是和QQ冲突，QQ防盗钩子所致。【c4d吧】_百度贴吧 | http://tieba.baidu.com/p/5992982445 |
| Edge 新增 | 403 / http-403 | 网盘搜索-小白盘 | https://www.xiaobaipan.com/ |
| Edge 新增 | 403 / http-403 | 【视频】火影忍者：鸣人恶作剧，假装自己的头被吃掉，把大和整个人都吓白了！_火影忍 | https://tieba.baidu.com/p/5955948661?red_tag=1881262804&traceid= |
| Edge 新增 | 403 / http-403 | 免 SHSH 从 iOS 9.0 ~ 9.3.5 降级到 iOS 8.4.1 如 | http://tieba.baidu.com/p/5406211052 |
| Edge 新增 | 403 / http-403 | simiely chen on Behance | https://www.behance.net/billpotterfb2b?tracking_source=search_projects%7Coctane+lake |
| Edge 新增 | 404 / http-404 | Search Results | 3D Models | https://rendercrate.com/3D/search/#!/new |
| Edge 新增 | 404 / http-404 | 红色gif | http://www.min-liu.com/bloody-dairy-a-daily-animation-project |
| Edge 新增 | 405 / http-405 | CGer.com|艺术|设计|技术|建筑|软件行业网站搜集。资源|模型|教程|软 | https://www.cger.com/ |
| Edge 新增 | 444 / http-444 | C4DNB-荔枝资源站-温馨精干的小站 | http://c4dnb.com/ |
| Edge 新增 | 444 / http-444 | 工具 - 设优SHEUI | http://www.sheui.com/Tools/ |
| Edge 新增 | 502 / http-502 | 免费下载PNG | http://www.996pic.com/ |
| Edge 新增 | 502 / http-502 | WE LOVE AD | 全球广告数据库与创意人社群 | http://www.welovead.com/cn/ |
| Edge 新增 | 502 / http-502 | 作品集 - D5渲染器 | http://cn.d5render.com/gallery |
| Edge 新增 | 502 / http-502 | 菜鸟C4D交流论坛-与你一起从零开始! | http://www.c4dcn.com/ |
| Edge 新增 | 502 / http-502 | 虚幻时代  素材网站 | http://xuhuantime.com/ |
| Edge 新增 | 502 / http-502 | PDF电子书-书享家 | http://shuxiangjia.cn/?cate=3 |
| Edge 新增 | 502 / http-502 | [搬书匠] - 电子书(EBook) | http://www.banshujiang.cn/ |
| Edge 新增 | 502 / http-502 | Mountain Lion GM(12A269)纯净懒人版支持硬盘助手 [BT种 | http://www.kenapple.com/forum.php?mod=viewthread&tid=197626&extra=&page=1 |
| Edge 新增 | 502 / http-502 | 双系统WIN7+狮子详细安装+驱动过程【附全部工具及大量截图】 - Mac OS | http://www.kenapple.com/thread-117247-1-1.html |
| Edge 新增 | 502 / http-502 | MXC | http://www.itmxc.com/forum.php |
| Edge 新增 | 502 / http-502 | 新新漫画 - 在线漫画 - 耽美人生 - 最全热血耽美BL漫画 | http://www.77mh.com/ |
| Edge 新增 | 502 / http-502 | 啃苹果论坛_(Apple)苹果电脑,iPhone手机,Mac,祯元翰通,苹果技术 | http://www.kenapple.com/forum.php |
| Edge 新增 | 502 / http-502 | kf.sf-express.com/css/myquery/appleTrack | http://kf.sf-express.com/css/myquery/appleTrack.action?respLang=ZH&respCountry=CN&cons=960080119832# |
| Edge 新增 | 502 / http-502 | 卤蛋君分享 - Good Luck To You! | http://abbw.ml/ |
| Edge 新增 | 502 / http-502 | 植物僵尸相关下载 | http://pvz4.lonelystar.org/download.htm#top |
| Edge 新增 | 521 / http-521 | 微软远程桌面优化-CSDN博客 | https://blog.csdn.net/weixin_45642871/article/details/132208652 |
| Edge 新增 | 521 / http-521 | 下载文件失败提示病毒扫描失败怎么解决_scanwithantivirus-CSD | https://blog.csdn.net/yuemouren/article/details/131147851 |
| Edge 新增 | None / other:TooManyRedirects | 电子书文件 - OneDrive | https://a1od-my.sharepoint.com/personal/a319_my365_tech/_layouts/15/onedrive.aspx |
| Edge 新增 | None / proxy-error | V2Ray / SSR 传输协议哪个好? (各种协议对比) - 无主界 | https://www.idleleo.com/05/2071.html |
| Edge 新增 | None / timeout | 小米路由器yacd | http://192.168.31.1:9999/ui/ |
| Edge 新增 | None / timeout | 全网影片搜索 - MyFreeMP4 | https://lab.liumingye.cn/ |
| Edge 新增 | None / timeout | Mp4电影_2022最新高清MP4电影资源下载 | https://www.dbmp4.com/ |
| Edge 新增 | None / timeout | 动效中期表 motiontable | http://foxcodex.html.xdomain.jp/index.html |
| Edge 新增 | None / timeout | 未越狱福利，破解软件ipa合集分享，更新最新不闪退迅雷5.21版（不是老版本.. | https://bbs.feng.com/forum.php?mod=viewthread&tid=11250834 |
| Edge 新增 | None / timeout | Windows 下 OpenCore 安装 macOS BigSur 教程 (完 | https://macx.top/22374.html |
| Edge 新增 | None / tls-error | 晴天吉他谱_周杰伦晴天弹唱谱-吉他堂 | https://www.jitatang.com/qingtian.html |
| Edge 新增 | None / tls-error | POCO摄影图片社区 - 领先的时尚摄影平台，摄影视频，摄影爱好者分享作品和技巧 | https://photo.poco.cn/ |
| Edge 新增 | None / tls-error | 免费资源网 | https://www.mfzyw1.com/ |
| Edge 新增 | None / tls-error | Terraform4D 地形插件 | https://www.terraform4d.com/terraform4d/building-terrains/operator-objects/ |
| Edge 新增 | None / tls-error | Veer免费图库 - 可商用正版图片素材库 | https://free.veer.com/ |
| Edge 新增 | None / tls-error | 谷歌商城应用 | https://apkdl.in/ |
| Edge 新增 | None / tls-error | 正版中国 | 正版软件限时免费 | https://getitfree.cn/ |
| Edge 新增 | None / tls-error | APP搜索 - Sunny在线工具 | https://tools.lancely.tech/apple/app-search |
| Edge 新增 | None / tls-error | 3元海淘8元奈飞 | https://joyo96.cc/user |
| Edge 新增 | None / tls-error | 最好用的国内免费接码平台大全 | 免费临时手机号 | 免费手机号 - 一灯不是和 | https://iyideng.fun/welfare/best-domestic-platform-for-receiving-captcha-free-of-charge.html |
| Edge 新增 | None / tls-error | 九九美剧 - 天天美剧在线观看_美剧迷真正的美剧天堂 | https://www.99meiju.vip/ |
| Edge 新增 | None / tls-error | 在线漫画,手机漫画,动漫之家漫画网手机版 | https://m.dmzj.com/# |
| Edge 新增 | None / tls-error | 在 Steam 上激活产品 | https://store.steampowered.com/account/registerkey |
| Edge 新增 | None / tls-error | 邮箱Free Email Addresses: Web based and se | https://navigator-lxa.mail.com/home?sid=959359fcf95f9b4207b0be2cb86a8e2a9e06763d0440e1b7557bc745dc13 |
| Edge 新增 | None / tls-error | R站|学习使我快乐！ - 专注C4D三维|建模|渲染|后期|动效|绑定|产品|包 | https://www.btbat.com/ |
| Edge 新增 | None / tls-error | 面包读书会 | https://kindlebookfor.me/ |
| Edge 新增 | None / tls-error | 书单网 | https://www.shudan.vip/ |
| Edge 新增 | None / tls-error | Z-Library – 世界上最大的电子图书馆。自由访问知识和文化。 | https://zh.singlelogin.re/ |
| Edge 新增 | None / tls-error | Receive SMS Online | Temporary SMS and D | https://smsreceivefree.com/ |
| Edge 新增 | None / tls-error | 我在等风也等你 | 感谢你能来，也不遗憾你离开 | https://www.binye.xyz/ |
| Edge 新增 | None / tls-error | 苹果软件园_苹果游戏_Mac游戏_苹果软件_Mac软件 | http://www.macapp.cc/ |
| Edge 新增 | None / tls-error | 爱绿软-收集分享各类有趣好玩的绿色软件 | https://ilvruan.com/ |
| Edge 新增 | None / tls-error | 黑苹果EFI - 免费分享黑苹果EFI信息 | https://macefi.com/ |
| Edge 新增 | None / tls-error | 寻它网址 | 寻它网址导航！ | https://kingsanne.com/ |
| Edge 新增 | None / tls-error | 网易云到Youtube Music, Spotify, Apple Music | https://yyrcd.com/2018/11/14/n2s-zh/ |
| Edge 新增 | None / tls-error | win10系统右下角网络图标异常的解决方法 - win10之家 | https://www.2016win10.com/w10/5245.html |
| Edge 新增 | None / tls-error | 旁门左道小一白 | https://hyczby.com/ |
| Edge 新增 | None / tls-error | 小米路由器SSH密码计算器 | https://mi.tellme.top/ |
| 原有 261 | None / connection-error | Sunshine | https://localhost:47990/ |
| 原有 261 | 401 / http-401 | 外网图库 | https://w2e0b1d6av.gd.ddnsto.com:443 |
| 原有 261 | 403 / http-403 | The Movie D | https://www.themoviedb.org/movie?language=zh-CN |
| 原有 261 | 403 / http-403 | 一元机场 | https://xn--4gq62f52gdss.com/#/subscribe |
| 原有 261 | 403 / http-403 | 懒得勤快 | https://masuit.com/ |
| 原有 261 | 403 / http-403 | 软件收集 | https://linux.do/t/topic/49853 |
| 原有 261 | 403 / http-403 | ArtStation | https://www.artstation.com/ |
| 原有 261 | 403 / http-403 | 视觉中国 - 首页 | https://www.vcg.com/ |
| 原有 261 | 403 / http-403 | 美工云 | http://www.meigongyun.com/ |
| 原有 261 | 403 / http-403 | 安鱼设计资源 | http://www.anyusj.com/ |
| 原有 261 | 403 / http-403 | Behance | https://www.behance.net/ |
| 原有 261 | 403 / http-403 | Twinmotion | https://www.twinmotion.com/zh-CN |
| 原有 261 | 403 / http-403 | Epic UE5 | https://www.unrealengine.com/zh-CN/ |
| 原有 261 | 403 / http-403 | 爱给网 | http://www.aigei.com/ |
| 原有 261 | 403 / http-403 | Free3D | https://free3d.com/ |
| 原有 261 | 403 / http-403 | 3D Models | https://www.turbosquid.com/ |
| 原有 261 | 403 / http-403 | TMDB | https://www.themoviedb.org/?language=zh-CN |
| 原有 261 | 403 / http-403 | Civitai | https://civitai.com/ |
| 原有 261 | 403 / http-403 | Le Chat | https://chat.mistral.ai/chat |
| 原有 261 | 403 / http-403 | ArtStation | https://www.artstation.com/?sort_by=community&dimension=all |
| 原有 261 | 404 / http-404 | 字幕库 | https://srtku.com/ |
| 原有 261 | 404 / http-404 | 微软绘图 | https://www.bing.com/create |
| 原有 261 | 405 / http-405 | 花瓣 | http://huaban.com |
| 原有 261 | 412 / http-412 | 中国卫健委 | http://www.nhc.gov.cn/wjw/fyjk/wsbz.shtml |
| 原有 261 | 451 / http-451 | 解密IPA商店 | https://decrypt.day/ |
| 原有 261 | 502 / http-502 | 全景网 | http://www.quanjing.com/ |
| 原有 261 | 502 / http-502 | 云模型 | http://www.yunmoxing.com/ |
| 原有 261 | 502 / http-502 | 每日一文 | http://meiriyiwen.com |
| 原有 261 | 502 / http-502 | 抽屉新热榜 | http://dig.chouti.com/ |
| 原有 261 | 503 / http-503 | Dpanl外网 | https://sidmiepaanily.gd.ddnsto.com:443 |
| 原有 261 | 503 / http-503 | win7外网 | https://simiwimiln7y.gd.ddnsto.com:443 |
| 原有 261 | 503 / http-503 | 精靈學院 | https://portal.riolu-node.link/dashboard |
| 原有 261 | 503 / http-503 | 精靈學院 | https://master.riolu.sbs/dashboard |
| 原有 261 | 503 / http-503 | 登录 - 精靈學院 | https://user.riolu-node.link/login |
| 原有 261 | 504 / http-504 | 软件 - IPA商店 | https://ipa.store/app |
| 原有 261 | None / timeout | 网盘搜索 | https://ysxjjkl.souyisou.top/ |
| 原有 261 | None / timeout | 電視電影 | https://kimivod.com/ |
| 原有 261 | None / timeout | AI研究所 - 国内外AI工具资讯首发站 | http://www.aiyjs.com/ |
| 原有 261 | None / timeout | Steam | https://store.steampowered.com/ |
| 原有 261 | None / tls-error | switch游戏 | https://sway.cloud.microsoft/1zyVegf0n3cHdR8o |
| 原有 261 | None / tls-error | 爱学习的老土狗 | https://www.cgltgcg.com/ |
| 原有 261 | None / tls-error | C4DSKY|书生 | https://c4dsky.com/ |
| 原有 261 | None / tls-error | Sketchfab | https://sketchfab.com/ |
| 原有 261 | None / tls-error | Ub文字转语音 | https://app.uberduck.ai/text-to-voice |
| 原有 261 | None / tls-error | Comfyui工作流 | https://comfyworkflows.com/ |
| 原有 261 | None / tls-error | 半次元 | https://bcy.net |
| 原有 261 | None / tls-error | 小众技术资源库 | https://www.xiaozhongjishu.com/?from=itab |
| 原有 261 | None / tls-error | 动漫之家 | https://manhua.dmzj.com/ |

## 五、阶段 4：重新分类

分类体系保持原有 13 个分类的名称与顺序（不合并、不新增），`常用` 固定第一位。作为精选收藏保留原首页 68 条中清理后剩下的 65 条；其余 498 条（旧库 190 + Edge 308）按域名、标题关键词与分类语义重新识别，一条只属于一个文件夹。

### 最终分类清单

| 顺序 | 文件夹 | 条数 |
|---|---|---|
| 1 | 常用 | 65 |
| 2 | 本地工具 | 22 |
| 3 | 小工具 | 55 |
| 4 | 搜索 | 21 |
| 5 | 软件 | 77 |
| 6 | 信息 | 42 |
| 7 | 参考 | 29 |
| 8 | 素材 | 23 |
| 9 | ai视频 | 6 |
| 10 | 教程 | 112 |
| 11 | MCP | 5 |
| 12 | 三维素材 | 41 |
| 13 | 视频&音乐 | 38 |
| 14 | Ai | 26 |
| | **合计** | **562** |

### 原有 261 条的迁移情况

| 去向 | 条数 |
|---|---|
| 清理（404） | 6 |
| 保留「常用」 | 65 |
| 重分类后换文件夹 | 17 |
| 重分类后维持原分类 | 173 |

旧条目换文件夹的 17 条：

| 原标题 | 原分类 | 新分类 |
|---|---|---|
| AI工具集 | 搜索 | Ai |
| Artlist 资源 | 参考 | 视频&音乐 |
| GitHub | 搜索 | 软件 |
| Kling AI | Ai | ai视频 |
| Movie 电影配色 | 三维素材 | 参考 |
| Octane blender下载页 | 三维素材 | 软件 |
| SaveFrom | 视频&音乐 | 小工具 |
| Sunshine | 软件 | 本地工具 |
| ZeroTier Central | 软件 | 本地工具 |
| ZeroTier – Network | 软件 | 本地工具 |
| pinshape | 参考 | 三维素材 |
| youtube 解析 | 视频&音乐 | 小工具 |
| 包小盒 样机 | 三维素材 | 素材 |
| 字体传奇网 | 参考 | 素材 |
| 街並みのカテゴリに関連する写真素材198 | 三维素材 | 素材 |
| 视觉中国 - 首页 | 参考 | 素材 |
| 音乐播放器 | 视频&音乐 | 软件 |

### Edge 新增条目的归类分布

| 新分类 | Edge 条数 |
|---|---|
| 教程 | 94 |
| 小工具 | 52 |
| 软件 | 40 |
| 信息 | 31 |
| 视频&音乐 | 28 |
| 三维素材 | 17 |
| 参考 | 16 |
| 搜索 | 11 |
| 素材 | 10 |
| Ai | 5 |
| 本地工具 | 3 |
| **合计** | **307** |

## 六、阶段 5：校验与安装

数据校验：总数 562 = 各文件夹之和；一级 1 个、二级 14 个、三级 0 个；未分类 0；空标题 0；归一化重复 URL 0（补删 1 条 you.com 的 Cloudflare 挑战参数重复）；schema 3 合法；`iconKind/iconColor/iconEmoji/iconSource` 字段保留。

安装：Release 构建，版本 `0.1.8.0 → 0.1.9.0`，按 `tools/install.ps1 -NoBump` 流程（快照 LocalState → 卸载 → 安装 → 恢复 → SHA256 校验）。

| 项 | 值 |
|---|---|
| 已安装版本 | 0.1.9.0 |
| links.json 条目 | 562 |
| links.json SHA256 | `6BCC98E92A0263E12B129ECD887B6A3CB371EF18F2D8D7849C0E4D1DA08B18D3` |

### UIA 实测

| 页面 | ListItem 数 |
|---|---|
| 首页（链接库内） | 3（网页链接 / 新建文件夹 / 批量编辑） |
| `网页链接` 二级 | 14（常用 第一，其后 13 个分类） |
| 常用 | 65 |
| 本地工具 | 22 |
| 小工具 | 55 |
| 搜索 | 21 |
| 软件 | 77 |
| 信息 | 42 |
| 参考 | 29 |
| 素材 | 23 |
| ai视频 | 6 |
| 教程 | 112 |
| MCP | 5 |
| 三维素材 | 41 |
| 视频&音乐 | 38 |
| Ai | 26 |

## 七、备份与证据文件

均在 `C:\Users\2504\Documents\Trae-CN\temp_20261105_03\cmdpaplinks-data-backup\`：

- `links-before-edge-merge-20261107-091047.json`：合并前 261 条
- `links-before-stage3-delete-*.json`：删除失效条目前
- `links-before-stage4-reclassify-*.json`：重分类前
- `links-before-stage5-dedupe-*.json`：补删重复前
- `probe-20261107-091139.jsonl` / `-summary.json`：577 条探测原始记录
- `stage3-deletions-*.json` / `stage3-pending-*.json`：删除明细与待确认清单
- `stage4-migration-*.json`：逐条新旧的分类对照
- `stage5-extra-dedupe-*.json`：补删明细

原始 iTab 备份与 Edge 源文件均未删除；插件 `LocalState` 内另有 `.itab-bak` / `.merge-bak` / `.twolevel-bak` 历史快照。
