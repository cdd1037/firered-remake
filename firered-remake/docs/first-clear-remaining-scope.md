# 首次主线后：当前剩余范围

更新：2026-10-14，稳定节点已包含Fly、日托／培育、碎岩及原57机器的获取与携带闭环。本页只维护当前状态和未完项；历史逐批数字、夹具、失败探针和精确执行身份保留在各模块报告及Git记录，不把旧段落的“下一步”当成今天的待办。

## 当前工作与统计边界

Fly已完成原16号道路Cut／Lea-Jed双打／HM02赠送／教学／取消／飞玉虹实际链和独立Continue；短headless View另过4号道路中心自动起降与原声事件，原生屏幕和物理音频仍未验。见[field Fly当前边界](field-fly-integration.md)。5号道路单槽日托已通过真实寄存／16有效步／100元取回、5档独立Continue和共享队伍短View；四岛两槽培育已实际往返、寄存／行走／取回并独立Continue；蛋领取／孵化／命名／后备战斗和幼体进化另有明示受控公共链，短headless View通过。原完整孵化背景和专用孵化音效仍缺。见[单槽育屋](route5-daycare-integration.md)、[四岛培育](four-island-daycare-integration.md)。

原代币盒／余额／换币柜台与三位普通赠币NPC已完成实际应用路线、独立Continue和短headless View；奖品三柜台／15项已接入，受控边界与实际三类兑换、独立Continue及短headless View均通过。原12处隐藏币已通过实际120→380续走与独立Continue；老虎机规则及生产入口已接入，原默认实走一回合/退出与短视图通过，精确原生视听仍未完成。通信进化替代的具体接入仍待讨论。

当前已加载组合为343地图、186可持有物种、209普通敌方定义、354招式、147普通物品、57机器和18导师定义。新增五TM实际取得与真道具携带往返已通过，TM10另有明示受控Pickup→公共TAKE验证，见[本批边界](original-machine-integration.md)。塔专用敌方另列；这些是数据与准入集合，不是地图交互、全部物种或招式表现的完工率。普通单打和原预设双打共享规则阶段已达到既定出口，76个有效特性身份及有边界的整回合／应用回归已建立；所有交互、获得途径与特殊模式仍分别验收。[战斗出口](battle-system-completion-matrix.md#2026-10-11-ordinary-core-closure-and-bounded-refactor)

项目级深入重构R1–R4的有界结果已经交付：普通单／双打共用唯一frame与执行生命周期，旧执行器及桥接实体退休，Content只保留当前入口，普通训练家、场外遭遇和药品／进化石使用共享绑定。原始资源证明、特殊实验室／教学战和只读表现投影是实际保留边界。[重构范围](project-refactor-plan.md#2026-10-13-final-bounded-refactor-result)

## 已完成的完整 Drive 备份节点（2026-10-14）

2026-10-14 16:02 UTC 用户将下一次完整包提前至“补齐剩余 TM/HM 的获取和携带流程”。
本次接入原50TM＋7HM目录，补TM10的Pickup后取回、TM18／48原道具球、TM27／29原NPC和TM42原纪念柱赠送，并使用共享TM携带／取回事务。定义、受控边界、实际来源和视图验收分别记录；五处新地图来源已真实续走，真TM18携带往返通过；TM10为受控原Pickup结果接公共TAKE，不冒称自然随机取得。

该范围已完成并以02c32b8固定为stage13完整包，包含当前源码、运行资源、必要原输入及少量关键存档／验收摘要。以下第2项后继工作分别继续，不把历史备份节点误当当前未完成要求：
- 玉虹游戏厅原老虎机：规则、App 会话、原资源和显示/音频接线已接入；用户批准 None/0 初值与 21 格负索引归一，原默认入口实际单回合与退出、短 headless View 通过，精确调色动画/原生视听仍待验。见[当前边界](slot-machine-integration.md)。
- 对战搜寻器原规则、351队伍、共享单／双打入口和场外表现已接入，原领取／100步充电／6号路真实再战与独立Continue、短headless View通过；全部重战地点和原生视听仍分别验收。见[当前边界](vs-seeker-integration.md)。
- 人物收集器列表／事实／肖像界面已接通；96条资料中仍有7条区域取得入口未挂载，精确原生表现另列。
- Teachy TV已接原目录／菜单／临时演示所有者，状态与相性课原行动表耗尽的安全补一回合方案待用户批准；当前显式边界可B返回。完整公开流程／原生视听仍按[教学接线说明](teachy-tv-integration.md)区分。
- 邮件九词编写／确认取消和12种原信纸已接通，真实既有Retro Mail写信／读信／送PC、独立Continue和短View通过，见[邮件当前边界](mail-composer-integration.md)。

育屋／培育与碎岩已完成的有界结果仍按上文记录。GitHub继续小节点同步。
完整包不含离线SDK或录制转音频；恢复所需SS Anne合成cue目录、已回收作者helper必须纳入输入闭包。
备份只做结构／哈希与上传核对，不为备份重复历史构建或路线。

## 存档谱系与验收来源

- 历史自然队伍停在generation41923，**没有自然首通**。明确准备的成型队／补给分支实际战胜后续联盟成员和原冠军，执行奖励，写出42922名人堂并在42943完成Credits→Reset；不是注入胜利，也不是自然新游戏首通。[联盟报告](pokemon-league-campaign.md)、[受控链](../tests/Game.PokemonLeagueControlledRoute.Tests/README.md)
- 后续区域沿实际合法检查点推进，但有明示的源码站位、受控个体／资源或独立分支。各模块文档分别说明，不把不同分支合成一条自然旅行。
- 2026-10-14磁盘恢复保留stage12真实52746档，并显式离线迁内容版本。旧58063／58103只剩历史报告，不能声称原始文件已经恢复。当前谱系为PP52786→5F维生素52894→4F三石52996→屋顶53266→代币53539→奖品53821→隐藏币53999→Fly54201→Route5日托54401→四岛双亲取回54616。前几段的站位条件见各自报告；**屋顶、代币、奖品和隐藏币路线全程公共门／楼梯，没有新定位或物品注入**。[恢复后PP](pp-boost-field-integration.md#recovered-current-product-acceptance-2026-10-14)、[屋顶终档](department-rooftop-services.md#actual-route-and-representative-view-closure)
- 最近的headless Draw、源素材和Dummy合成检查不等于原生窗口像素／实际听感。历史原生结果只适用于其固定源和运行批次，不自动覆盖后继代码。

## 已接通的主要单机模块

下表说明业务已经接通，具体未验分支仍以链接报告为准；不能据此推定整个地区全交互。

| 模块 | 当前已接通范围与重要边界 | 记录 |
| --- | --- | --- |
| 八徽章、冠军之路、首次联盟 | 主线应用路线、奖励与严格存读；联盟自然／受控来源见上文。PC名人堂回看仍缺。 | [联盟](pokemon-league-campaign.md)、[冠军之路](victory-road-campaign.md) |
| 首次一至三岛 | 原救人、陨石交付、往返与部分可再生隐藏物；侧路仍有缺口。 | [首次七岛](first-sevii-campaign.md) |
| 全国图鉴、红蓝宝石与网络机 | 图鉴条件、红宝石／彩虹通行证、四岛塌冰与攀瀑、六岛谜题、五岛仓库、Celio交付及游走个体初始化／存读。 | [图鉴](national-dex-campaign.md)、[红宝石](sevii-ruby-campaign.md)、[四岛](four-island-campaign.md)、[六岛](six-island-campaign.md)、[五岛](five-island-campaign.md)、[Celio](celio-campaign.md) |
| 华蓝洞与强化联盟 | 三层洞窟、超梦事件／有界捕获及败退；强化四天王与冠军用明示成型队实际结算，多次Hall生命周期。超梦KO应用分支仍未在该批实走。 | [华蓝洞](cerulean-cave-implementation.md)、[强化联盟](league-rematch-campaign.md) |
| 七岛遗迹与训练家之塔 | 原推石解谜、七间未知图腾石室、按PID区分28形态；塔四种原版本地课程均完成八层与一次奖励，外部e-Reader不纳入单机。 | [七岛](seven-island-campaign.md)、[塔](trainer-tower-campaign.md) |
| 活动双岛 | 精确pret的本次新授冠军ribbon且无Aurora条件赠两票；航线、Ho-Oh/Lugia事件、Birth谜题及Deoxys有界捕获／存读。无独立读档补发系统。 | [双岛](event-islands-campaign.md) |
| 发电厂、双子岛 | 闪电鸟、两只伪装顽皮雷弹及拾取；双子岛推石止流／未止流支路／返程。双子岛默认55989仍未捕获急冻鸟，捕获回调另用明确受控实例验证，不能混作原个体自然捕获。 | [发电厂](power-plant-campaign.md)、[双子岛](seafoam-campaign.md) |
| 格斗道场、化石与原NPC交换 | 道场五位训练家和二选一赠物；三化石复活及相应进化；九项原NPC交换均接通，Dontae供体由真实钓获／升级获得。通信进化替代另议。 | [道场](fighting-dojo-campaign.md)、[化石](fossil-lab-campaign.md)、[琥珀](old-amber-campaign.md)、[交换](npc-trades-campaign.md)、[邮件](npc-trade-mail.md) |
| 教学与钓鱼 | 原15项一次导师、蘑菇回忆、浅红遗忘、岬边终极招式；三钓竿、已加载地区原钓表、鲤鱼王大小纪录。Softboiled成功为独立受控检查；Safari高级钓鱼与未加载区域仍缺。 | [导师](original-move-tutors.md)、[回忆](move-reminder-flow.md)、[遗忘](move-deleter-flow.md)、[岬边](cape-brink-campaign.md)、[好竿](good-rod-campaign.md)、[高级竿](super-rod-campaign.md) |
| 玉虹基本设施 | 普通2F商店、六行TM柜台、3F教学、4F六行货品、5F两柜台、原楼梯／电梯、茶／伊布、姓名判定师。部分物品／服务表现仍独立列边界。 | [室内](regional-interaction-coverage.md#accepted-bounded-regional-interiors-2026-10-10)、[2F机器](department-machine-shop.md)、[4F](department-fourth-floor-shops.md)、[5F](department-fifth-floor-shops.md) |
| PP、维生素与六种进化石 | 共享PP增益／六维生素使用、七个新增原石头目标及兼容闭包；实购火／水／叶石用于既有Growlithe／Eevee／Gloom，三档独立Continue。其他合法分支有各自受控规则证据。 | [PP](pp-boost-field-integration.md)、[维生素](vitamin-field-integration.md)、[石头闭包](stone-evolution-owned-catalog.md)、[实际三族](department-fourth-floor-shops.md#actual-purchases-and-three-family-evolution) |
| 屋顶饮料 | 原三款售货机、三份一次TM、动态饮料菜单、重复拒绝。实际270接受命令＋9拒绝重试，独立Continue29检查；headless267检查／8Draw／70输入、恰一次SE_VEND。满TM时先扣饮料的原顺序保留。 | [屋顶](department-rooftop-services.md#actual-route-and-representative-view-closure) |

## 关都和首次七岛仍缺的内容

| 范围 | 未完成或未完整验收 | 记录 |
| --- | --- | --- |
| 各城镇和道路 | 普通／条件对白、隐藏物、侧室与剩余训练家仍须逐区补齐；早期常青学校／民居、深灰博物馆购票与展览没有完整交付证明。主线通过不等于全交互。 | [交互覆盖](regional-interaction-coverage.md)、[早期遗留](first-badge-followups.md) |
| 玉虹服务 | 代币盒／余额／两档换币与原三NPC赠币已接通，实际coins600／money5798并独立存读；奖品三柜台／15项已接入，实际售物换币→三类兑换／PC命名与独立Continue通过；12处隐藏币实际260币／一次性flag／Itemfinder排除与独立Continue通过；老虎机原默认入口实际一回合/退币退出已通过，精确原生视听另列。Retro Mail销售已完成；写信和空白预览、真实送PC／独立Continue与短View已通过，原生表现仍另列。 | [原代币服务](game-corner-coins.md#actual-city-route-and-short-view-closure)、[奖品接线](game-corner-redemption.md)、[4F准确边界](department-fourth-floor-shops.md)、[完整地区分母](full-scope-audit.md#4-关都全部地区与主线) |
| 南路与浅红周边 | 自行车道、另一条南行路线、未覆盖浅红设施／拾取、尚未开放钓鱼区域及Safari高级钓鱼。Haden18号门楼可达不等于整条道路开放。 | [Haden](npc-trade-haden.md)、[浅红](fuchsia-arrival.md)、[高级竿](super-rod-campaign.md) |
| 海路与传说分支 | 另一条海路与尚未列入验收的静态遇敌分支；双子岛21队未逐队实战，急冻鸟自然原个体捕获未完成。保留上述发电厂／双子岛已通业务，不重新列为从零待做。 | [双子岛](seafoam-campaign.md)、[红莲](cinnabar-campaign.md) |
| 首次一至三岛支路 | 宝物海滩、灯火山顶与火焰鸟；三岛港口城镇地图定位点和未覆盖支线。岬边／回忆、灯火山道／温泉／红宝石洞窟与救人往返已经完成。 | [首次七岛余项](first-sevii-campaign.md#remaining-regional-boundaries) |
| 通关后区域 | 四岛尚缺服务、五六七岛未覆盖侧路／支线和部分世界变化；未知图腾原目标编译器随机顺序及原生表现仍需独立核对。 | [全单机七岛](full-scope-audit.md#5-七岛不是一个可删除的后日谈)、[七岛现状](seven-island-campaign.md) |

## 养成、场外能力及特殊服务

- 原单只寄放、四岛双亲培育、蛋和孵化已接通，真实服务路线与受控蛋链分别记录；自然Wynaut的原悠闲薰香取得仍缺；Rhydon、Slowbro、Crobat、Blissey 四项普通进化闭包已组合接入，取得与表现验收边界另列。原50TM／7HM至少一条当前取得入口及携带规则已接通；未覆盖物种的教学兼容与特殊表现仍按物种范围继续补齐。碎岩已在原温泉领取并通过实际破石路径。Fly、攀瀑、Surf、Strength、Cut已在各自场景接通，不能用这些能力替代其他服务。[养成分母](full-scope-audit.md#6-宝可梦数据与养成的完整口径)
- 对战搜寻器已通过有界实际重战／独立Continue与短View，Teachy TV目录／菜单／六段临时演示已接线；状态／相性课的原行动表耗尽仍显式停止，单次追加FIGHT方案待确认，不能称教学全完成。人物收集器已接通原16人／96条数据、列表／事实／肖像模式、背包与注册入口及存读；原96条事实中另有7条的地图收集入口未挂载，不能视为全收集完成。文本逐像素滚动、控制码精确帧耗与原生视听仍待验。[人物收集器](fame-checker-integration.md)。可再生隐藏物的原步数／入口机制已有实现；专属探宝器箭头／星光原生表现仍缺。
- 原NPC交换专用表现仍未完整还原。12种原信纸与九词编写已接线，真实公共路线／独立Continue／短UI通过，精确动画和原生视听仍待验；不会给原火红没有本地来源的信纸擅增购买入口。[邮件当前边界](mail-composer-integration.md)
- 狩猎特殊捕获／步数循环、四种塔课程已经实现；动态谜之果载荷、外部e-Reader和真实通信另有输入依赖，不计普通主线能力。[狩猎](safari-campaign.md)

## 菜单、专属表现和声音

- 实用界面：PC Move Items、放生及HM保护、箱子命名／壁纸／标记；图鉴栖息地／搜索排序／分布／大小／叫声页面；训练家卡背面和持久游玩时长。普通PC存取／14×30容量、数值图鉴和卡片正面已有功能。单只Move Pokémon已接入真实持有游标／交换／跨箱和邮件所有权，隔离公共PC命令与Continue、主组合构建通过，短View尚待当前批验收；多只矩形选择仍未实现，见[Move接线](storage-move-application.md)。名人堂只读回看已通过真实PC进入／历史队伍浏览／退出、独立Continue及短headless View；原过渡与精确原生视听仍另列，见[Hall PC](hall-pc-contract.md)。普通持物与50TM的给予／取回／交换及PC普通物品GIVE已接通；7HM按原重要物品规则禁止携带。场外背包Items／Balls／Berry Pouch的TOSS已接通，取消、结果确认才扣量、最后栈压缩与真实存读／短View通过；战内Berry Pouch丢弃仍未接通。[PC](pokemon-storage-contract.md)、[携带物](held-item-transfers.md)、[菜单](basic-menu-application.md)
- 选项：文字速度、SHIFT／SET、边框可编辑；Battle Scene、Sound、Button Mode的玩家入口仍提示不可用。测试Scene Off不等于完整设置功能。菜单鼠标／键盘已有实现，地图鼠标移动／寻路后置。[选项代码](../src/Game.Godot/CampaignOptionsView.cs)、[鼠标边界](menu-pointer-controls.md)
- 专属动画：当前矩阵20／354，余334未纳入专属还原。通用效果不计专属；已有20也有各自源／主机／原生证据边界。[矩阵](battle-system-completion-matrix.md)、[trace批次](source-trace-choreography.md)
- 场景与物种：原片头／大木介绍、原命名键盘、部分进化／捕获／狩猎演出和完整菜单动效仍缺；晃晃斑PID斑点未实现。Hall背景混色／纸屑／排布／过渡未全还原；片尾66条源脚本与顺序保留，地图镜头／放大构图等仍简化，时长显示--:--。[联盟表现差异](pokemon-league-presentation.md#verification-and-explicit-boundaries)、[物种边界](full-scope-audit.md#61-三个不同集合)
- 声音：默认原序列／采样C#合成器已运行；完整招式声音时序和全部场景仍未验收，硬件声道／起音／尾音等近似见文档。Dummy有限非零PCM、事件和结束检查不证明实际听感／音质／同步；P0实际音频输出门禁仍未关闭。原生检查只保留其固定批次范围。[合成器](synth-audio-integration.md#pcm-buffering-and-sound-boundaries)、[联盟音频](pokemon-league-presentation.md)

## 中文化

中文仍是最终目标；文字依据已选Xzonn/PretFRLG `a04f63173ca5fcf9c2db18ee223cf057c7f232c0`。中文菜单、对白／词汇导入、字体／分页和名字输入尚未完成。只取审阅后的文字，不能带入汉化分支经验等玩法修改。此前保留英文标题和五张界面PNG的选择不自动撤销。[中文来源](adr/0003-chinese-text-baseline.md)、[字体](source-font.md)

## 已批准修正与仅供以后评估的候选

**已有实现，不再列为待开发**：公共副作用概率的标称边界修正、金珠桥金珠一次性奖励、连续 Protect 第五次以后 12.5% 安全夹限、纹理２合法属性候选选择、双打 KO 回合末补位、王者之证读取实际攻击者参数、AI 无效历史招式安全读取，以及梦幻／代欧奇希斯采用普通原训练家／徽章服从规则。它们是明确记录的项目规则差异，不冒称原版逐项相同。[修正记录](research/prioritized-improvements.md)、[金珠桥实装](bill-campaign.md)、[双打实装](preset-doubles-route8.md)

**已批准方向、具体接入仍待讨论**：单机通信进化替代仍未实现。用户最近明确要求后续再讨论具体服务方式，因此不预先固定NPC、地点、费用或入口；原持物／消耗、图鉴条件和个体身份应在具体方案确定时核对。旧研究文档属于候选设计，不覆盖此决定。[设计研究](research/solo-trade-evolution.md)、[批准清单](research/prioritized-improvements.md)

**另外已完成**：活动双岛采用精确pret Ribbon/Aurora授票条件及原航线；游走个体完整IV保存／恢复与吼叫退场后仍可再遇在Celio节点实施。两者不再列为未接入。邮件拍落／回收导致状态脱钩的窄防护见[原邮件范围](npc-trade-mail.md)。

**AI 两项已批准修正已经集成**：`4394bc769116` 将硬撑评分的状态对象改为实际使用者，并交换原程序中沙暴／冰雹两个错误的条件操作数；原认证 AI 包保持原字节。公开希巴选择入口的七个状态／36 项检查验证了硬撑结果、合法选择和随机消耗。天气分支保留独立源向量证据；正常入口先被另一项原版“目标是否有招式效果”的错误判断挡住，因此不宣称自然路线触发过天气错分支。本次没有扩展修改该目标效果判断。[已批准修正的实现与范围](../tests/Game.ApprovedTrainerAi.Tests/README.md)

**仅记录候选，未批准实施**：取消自爆／大爆炸的目标防御减半、计时球极长战斗回绕修正、跟随精灵、地图明雷，以及原 AI 替补评分的 stale-current-move 改动。它们不属于本轮精简，更不应与已批准修正混在一起。[候选状态](research/prioritized-improvements.md)、[仍保留的替补评分](source-four-trainer-switch.md)

真实玩家交换／对战、无线大厅和多人小游戏、原 ROM 存档互通及云存档仍是另外登记的延后扩展；不能用“完整单机”暗示这些已经交付。[范围边界](scope.md#3-延后与排除边界)

### AI 修正的追溯边界

原包字节与缺陷证据仍保留，运行时只在认证的三个操作数字节处采用批准修正。原 `89171036` 下 PC6845 的状态对象、PC6691／6697 的天气比较都是修正前证据，不再表示当前未实现。详见 [实现报告](../tests/Game.ApprovedTrainerAi.Tests/README.md) 和 [原作修正记录](research/prioritized-improvements.md)。
