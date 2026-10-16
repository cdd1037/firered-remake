# 完整单机范围审计

本页是产品分母与固定来源索引，不是当前实现报告。原始静态审计：2026-10-08；维护整理：2026-10-15。当前已接通／未接通／未完整验证的区别见 [当前状态](first-clear-remaining-scope.md)，战斗见 [当前矩阵](battle-system-completion-matrix.md)。本次整理未运行参考游戏或重放工程路线。

## 1 最终目标

完成关都八徽章与首次联盟、七岛两段剧情、宝石任务后的强化联盟和华蓝洞、单机养成／收集、全部正常单机支线及重复玩法。第一徽章和首次名人堂都是中间节点；完整单机不等于一个无交换存档能收齐386种。中文为最终目标，具体文字来源和保留英文标题的决定见当前状态页。

## 2 基线与验收口径

规则基线为英文原始GBA FireRed、pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`。不混入LeafGreen、后世代或再发行便利机制。已批准的项目差异以当前状态页为准；本页原作要求不撤销那些决定。

定义存在、玩家可达、业务实现、规则测试、实际路线、原生画面与实际音频是不同结论。原始地图目录包括通信、活动、未使用地图，不能直接作为普通单机分母；代码行、测试行和地图数量均不是整体完成率。当前运行只使用生成资源，固定来源用于离线研究／导入，不要求运行时重验源文件身份。

活动双岛在当前工程已按既定授票条件接通；以下C类仍描述其原作外部来源属性，不表示当前活动岛业务缺失。玩家通信和外部输入协议继续单独延后。

## 3 范围分类

|分类|含义|建议处理|
|---|---|---|
|A 单机核心与内容|普通原始火红存档不连接另一台设备或外部配信即可游玩的全部内容，含非主线、一次性、可重复和分支内容|完整单机交付的硬门禁|
|B 单机辅助表现|帮助、回顾、收集展示等对完整体验有价值但可后置打磨的功能|可后置实现；在清单里保留，不以永久占位冒充完成|
|C 外部依赖扩展|玩家间交换/对战、无线小游戏、Mystery Gift、活动票、外部版本输入等|较低性价比，可延后；数据与状态边界尽早设计|
|D 不属于本基线|原型/未使用地图、其他版本独有游玩系统、后世代规则、原创全图鉴获取路径|不加入原版兼容范围；需要时作为独立改版规则|

A/B/C 是依赖及排序分类，不是功能质量等级。NPC 交换是 A；修复七岛联网机器的单机剧情也是 A。存在通信代码的共同类型，不等于使用它的所有单机内容都能后置。

## 4 关都全部地区与主线

下列每个地区都包括其正常可达室内、中心/商店/民居、出入口、标牌、隐藏物、训练家、NPC 条件对话和奖励；不能只接一条通关走廊。地图权威目录与逐图脚本均在固定 data/maps 快照。[F03]

|交付段|必需内容|关键验收点|
|---|---|---|
|开场至深灰|主角/劲敌命名、真新镇、初始伙伴三分支、研究所劲敌战、1/2/22 号道路可达部分、常青市、包裹与图鉴、老人教程、常青森林、深灰市与小刚|三起始路线、可选 22 号路劲敌战、剧情败北与普通败北区分、徽章和 TM 奖励补领|
|月见山至华蓝|3/4 号道路、月见山各层与化石二选一、鲤鱼王购买、华蓝市、水系道馆、24/25 号道路、金珠桥、正辉与船票、偷窃事件及出口|不可逆跳台、化石选择保存、捕获/赠送满队处理、华蓝及金珠桥劲敌战|
|枯叶与东部关都|5/6 号道路、南北地下通道、枯叶市、圣特安努号全部舱室/甲板、船长与居合斩、马志士、电气机关、11 号道路、地鼠洞、2 号路回访、9/10 号道路、岩山隧道|船票、船离港的一次性事件、HM 与徽章分别判定、闪光与黑暗地图、已捕获数奖励|
|紫苑与玉虹|紫苑镇、宝可梦塔及幽灵/嘎啦嘎啦特殊战斗、7/8 号道路与东西地下通道、玉虹市、百货公司、游戏城/奖品兑换、火箭队基地与电梯钥匙、西尔佛检视镜、莉佳、茶与金黄入口、宝可梦之笛|幽灵识别和不可捕捉例外、旋转地砖、隐藏开关、币/金钱分离、卡比兽事件与通路|
|金黄与浅红|金黄市、西尔佛公司全楼层/传送/卡片钥匙/拉普拉斯/大师球、格斗道场二选一、娜姿；12–15 号道路与16–18 号道路两条南行路线、自行车道、浅红市、阿桔、狩猎地带、金牙与怪力、冲浪、遗忘招式|不能强制单一挑战顺序；两条可选路线、公司与道馆独立状态、狩猎特别规则、招式遗忘与 HM 安全|
|海上与红莲|19–21 号道路、双子岛全层/水流/推石、急冻鸟；无人发电厂与闪电鸟/伪装道具球；红莲岛、研究所 NPC 交换和化石复活、宝可梦屋开关/钥匙、夏伯问答与道馆|静态传奇捕获/战败/逃跑后状态、化石互斥与重复交互、海上/陆上移动切换|
|常青与联盟|常青道馆坂木、八徽章检查、22 号路终盘劲敌、23 号道路、冠军之路、石英高原、四天王与冠军三起始分支、名人堂、结尾、通关存档与回家|五连战、失败重来、结尾中断恢复、通关后世界仍可活动；不能把片尾设为游戏终止|

八道馆以小刚、霞、马志士、莉佳、阿桔、娜姿、夏伯、坂木为覆盖集合；开发顺序可分批，但实际可进入顺序必须按原作 gate 实现，不能为了方便写成八个线性关卡。

与上述各段并行纳入的关都 A 类内容：
- 博物馆与秘密琥珀、三种化石复活链、伊布、拉普拉斯、格斗道场选择、鲤鱼王购买和全部游戏城宝可梦奖励。
- 三种钓竿、自行车券/自行车、跑鞋、地图、道具探测器、对战搜寻器、TM 盒/树果袋、关键道具注册和正常用途。
- 大木助手的捕获数条件奖励。已核查 2 号路 HM05、11 号路道具探测器、15 号路学习装置的阈值分别为 10/30/50 种；读取 caught 计数，不能被脚本中“seen”的局部命名误导。[F05]
- NPC 交换、重新命名、遗忘招式、教学招式、寄放、亲密度相关 NPC、钓到的宝可梦体型记录、一次性和可再生隐藏物。
- 所有野生、训练家、剧情/教程、幽灵、狩猎和传奇战斗类型的正常结果分支。可选内容的胜负、放弃和错过不应伪造成主线失败。[F26]

## 5 七岛不是一个可删除的后日谈

### 5.1 首次一至三岛

夏伯战后由正辉邀请进入。用户可以先拒绝、稍后再去，系统应保留这些合法顺序。一至三岛不是只能在首次联盟之后进入。[F06]

|地区|A 类内容|
|---|---|
|一岛|镇区、宝可梦中心中正辉/锦辉剧情、三岛通行证、宝物海滩、灯火山道、温泉、灯火山外部/登顶及火焰鸟；后期红宝石通道另外受条件控制|
|二岛|失踪女孩事件的起点与送回、陨石交付、商店进展、岬边及原作终极招式教学、蘑菇换回忆招式服务|
|三岛|港口/城镇暴走族事件、索桥、树果森林找回 Lostelle 与引梦貘人战斗、隐藏树果；全国图鉴后变化的土龙弟弟洞窟也需回访实现|

欢乐游戏城的建筑、人物和救人剧情属于 A；里面要求伙伴连接的跳绳与嘟嘟利采树果玩法属于 C，二者不能一起删掉。[F07]

### 5.2 全国图鉴与宝石任务的真实链条

已直接核查：
1. 首次名人堂后，大木检查至少 60 种已捕获宝可梦，且玩家到过一岛；不足时可继续捕获、之后回研究所触发。不能只检查 seen=60，也不能一通关就无条件开放。[F08]
2. 全国图鉴使锦辉提出红宝石任务。灯火山红宝石与火箭队相关事件完成后，交付红宝石，把三岛通行证换成彩虹通行证，开放四至七岛。[F09]
3. 四岛冻瀑洞窟中的科拿与火箭队事件、六岛点之穴的盲文/落洞谜题与蓝宝石被夺、五岛火箭队仓库的双口令/旋转地砖/干部及研究员，构成取回蓝宝石链。具体前置按脚本，不以一条粗略布尔值代替。[F10]
4. 交给锦辉蓝宝石的同一脚本调用 SetPostgameFlags，设置 FLAG_SYS_CAN_LINK_WITH_RS，移走华蓝洞入口守卫，更新科拿位置并 InitRoamer。联盟 NPC 也使用这一 flag 切换强化队伍。[F09][F11]

因此，即使玩家间联网延后，宝石任务、联网机器的完成演出、华蓝洞、游走神兽与强化联盟仍必须交付。可用更有意义的领域名表示这条剧情进度，但应保留源 flag 映射；不要删掉名字含 Link 的状态。

### 5.3 四至七岛全部单机内容

|地区|A 类内容与机制|
|---|---|
|四岛|镇区及住家、科拿住家与剧情变化、冻瀑洞窟全层、薄冰/落穴/瀑布与 HM07、火箭队事件、双亲培育屋/蛋/孵化、记录贴纸等正常 NPC 分支|
|五岛|镇区、五岛空地、火箭队仓库；水之迷宫的波克比蛋赠送及满队补领；豪华度假地、迷失洞窟循环房间/寻人、Selphy 要求展示宝可梦与时限/奖励；回忆之塔的纪念事件和 TM42|
|六岛|水之步道、绿之步道、记号树林、外缘岛、遗迹山谷、点之穴；赫拉克罗斯体型记录与奖励；变化洞窟按普通单机的默认遭遇开放|
|七岛|镇区、训练家之塔完整本地四模式；溪谷入口、七宝溪谷及民居、阿斯卡纳钥匙推石谜题、阿斯卡纳遗迹七个石室与未知图腾形态分布|

对应来源包括地图总目录、冻瀑洞窟/仓库/遗迹脚本、波克比蛋赠送、Selphy 展示任务、纪念塔奖励和未知图腾解锁代码。[F03][F10][F12][F13]

### 5.4 后期仍需关都回访

- 华蓝洞三层与超梦，入口须服从宝石任务完成状态。
- 一只游走传说宝可梦：妙蛙种子开局为炎帝，小火龙开局为水君，杰尼龟开局为雷公。它不是三只同时出现，状态应跨地图和存档保持。[F14]
- 四天王与冠军强化阵容、反复名人堂记录、科拿相关世界变化。
- 对战搜寻器可重复训练家与进展相关队伍、练级经济、持续捕获、孵蛋和养成。可再生隐藏物有自己的步数机制，不能改成每日刷新的 RTC 任务。[F25]
- 首次通关后解锁的 NPC 对话、人物收集信息、地区布局/遭遇变化和奖励，不能只替换联盟队伍。

## 6 宝可梦数据与养成的完整口径

### 6.1 三个不同集合

必须分别记录：
- 全国数据目录：全国编号 1–386 的数据身份，另有蛋、形态和内部占位 ID；内部 species 编号不能直接当全国编号。
- 单机可接触规则闭包：所有正常可达玩家宝可梦、敌方宝可梦、招式随机调用、教学、道具和剧情产生的行为。
- 每个存档可取得集合：在该版本、开局选择、化石选择、当前进度及无外部输入条件下可捕获、领取、购买、交换或繁殖得到的物种和形态。

“不在单机捕获集合”不表示可以缺少对战支持。NPC 阵容、训练家之塔会使用玩家单机未必能得到的物种；Metronome 等招式可扩大招式效果闭包。训练家之塔的原版集合含 Espeon/Umbreon 等，不能只按玩家野生表生成战斗能力。

已知物种表现未覆盖（2026-10-10）：Spinda 的正面四处斑点须由其 32 位 PID 的四组 XY 各 4 位生成，各轴偏移为 −8 至 7；不是 IV 随机值，同一 PID 必须保持外形。当前未实现，后续纳入对应物种表现验收，不能以统一静态正面图替代。固定 pret `src/pokemon.c` 的 `DrawSpindaSpots` 及 `src/decompress.c` 为实现依据。

全国图鉴目录完整不应通过让所有物种出现在草丛实现。完整单机验收也不要求每条互斥路线在同一个存档同时成立。

### 6.2 A 类捕获与获得方式

- 草丛/洞穴、冲浪、三种钓竿、碎岩的遭遇率与等级；驱虫喷雾、相关道具/能力、地域与剧情 gate。
- 普通球种、HP/状态/球种修正、逃跑、捕获摇动与结算、昵称、满队转 PC、全部盒子满时的拒绝和提示。
- 狩猎地带须独立处理球/投饵/扔石/逃跑及敌方逃跑，原始入口收费 500，初始 30 球、600 步；球耗尽、步数耗尽、提前退出是不同分支。[F15]
- 三鸟、超梦、两只卡比兽、引梦貘人事件、伪装成道具的宝可梦与其他固定获得途径；区分可捕捉野生事件和不可捕捉的幽灵/教程/敌方。
- 化石选择与复活、道场赠送选择、初始伙伴、伊布、拉普拉斯、鲤鱼王、波克比蛋、游戏城奖品。
- 九种 NPC 交换记录，保留火红实际要求物种、对方 OT、固定个体/性格相关值、昵称、持有物和一次性标志。它们是普通单机内容。[F16]

### 6.3 A 类成长与训练

- 等级至 100、所有原版成长曲线、经验分配、参与/学习装置/外来宝可梦等修正；连续升级、进化前后学招、四招替换与取消。
- 个体值、努力值、性格、性别、特性、异色、亲密度、PP/PP 提升、持有物、病毒等状态的正确存储与合法来源；显示语言不改变 personality、原始训练家身份或数值。
- 全部可达升级、石头、亲密度及特殊条件进化。通信进化记录和触发方式保留为 C，不擅自改成“升到某级”。
- 50 个 TM 与火红的 7 个 HM 的原版兼容性/消耗/遗忘限制/场外效果；招式教学的一次性与重复限制、二岛蘑菇回忆、终极招式条件；按实际来源核对，不照搬绿宝石导师表。[F17]
- 5 号路单只寄放与四岛双亲培育屋分别处理；寄放成长、取回费用、蛋群/性别/百变怪、遗传招式/个体、产蛋与孵化步数、亲密度和领取满队失败。[F18]
- 队伍六只、PC 多盒、移动/整理/放生/持有物/昵称与必要限制；背包和 PC 物品容量、道具买卖、钱币上限、关键物品安全、治疗/全灭恢复。

### 6.4 不能误抄的原版差异

- 全国图鉴前，进化到非关都物种会被阻止，例如大嘴蝠不能提前进化叉字蝠。跨世代通信进化也有图鉴限制。[F19]
- 火红没有 RTC，昼夜亲密度进化分支被禁用，不能在火红本机自然把伊布进化为太阳伊布/月亮伊布。[F19]
- 不把绿宝石的培育改动默认为火红行为。例如火红 daycare.c 中电气球遗传伏特攻击的调用被注释；存在帮助函数不等于功能启用。应逐项核对不变之石遗传、孵蛋加速等，不能按“第三世代通用”处理。[F18]
- 共用道具常量仍包含 HM08 潜水，并把 HM 总表定义为 8；这不表示火红正常地图有第八个秘传获得流程或水下区域。单机可达清单按火红脚本接入七种场外 HM，不按共用常量计数凭空增加玩法。[F03][F27]
- 不能加入第四世代物理/特殊按招式划分、妖精属性、后代进化、招式/特性数值改动、无限次 TM、全队自动经验等。
- 原版可观察缺陷按兼容规则登记。游走宝可梦及吼叫、个体生成等要有针对性证据；若修复，应单独 rulesVersion，不能静默把原版行为改掉。

### 6.5 全量战斗完成标准

不能只用“基础伤害、换人、捕获已经可玩”代表规则完成。A 类规则覆盖还包括：
- 第三世代属性相性与按属性区分的物理/特殊、原版威力和命中、STAB、暴击、整数取整顺序、能力等级、天气和徽章加成。
- 优先度、速度与同速、行动前失败、锁招/蓄力/多次攻击/替身/反伤/吸收/反击/复制/随机调用、强制换人、连续回合与回合末顺序；PP 耗尽的挣扎及所有可达失败分支。
- 异常状态与临时状态、特性/持有物的触发时点、免疫、离场重置、场外中毒和全队倒下，避免只实现招式文本与动画。
- 双打的己方/敌方/单体/多目标与有效目标规则、同时倒下、补位、群体效果；领域中不能写死两个 battler。
- 训练家 AI 的招式选择、换人及允许使用的道具，剧情对手与普通训练家使用同一规则引擎；原作阵容、等级、招式、持有物、AI 标志分别导入。
- 野生、训练家、教程、幽灵、狩猎、游走与训练家之塔的允许命令、胜负/逃跑/捕获结果和奖励策略；表现层不得自行推导奖励。

招式数据表含 354 个实际招式 ID（另有 MOVE_NONE），但仍需按可达效果闭包说明当前验证覆盖。即使分期导入，未实现效果必须显式阻止该阶段发布，不能回落为普通伤害或无声成功。[F27] 原 plan 第 7 页的分层证据要求应应用于这个全游戏集合。

## 7 版本专属与外部来源

### 7.1 火红与叶绿的物种来源差异

下表列出版本来源家族，含后续进化/繁殖关系，不声称每个最终形态都能在无交换单存档取得。Scizor、Slowking 等仍需通信进化。以野生表、获得脚本和进化表联合判断。[F04][F19]

|火红来源家族|叶绿对应来源家族|
|---|---|
|Ekans / Arbok|Sandshrew / Sandslash|
|Oddish / Gloom / Vileplume / Bellossom|Bellsprout / Weepinbell / Victreebel|
|Psyduck / Golduck|Slowpoke / Slowbro / Slowking|
|Growlithe / Arcanine|Vulpix / Ninetales|
|Shellder / Cloyster|Staryu / Starmie|
|Scyther / Scizor|Pinsir|
|Elekid / Electabuzz|Magby / Magmar|
|Wooper / Quagsire|Azurill / Marill / Azumarill|
|Murkrow|Misdreavus|
|Qwilfish|Remoraid / Octillery|
|Delibird|Sneasel|
|Skarmory|Mantine|

野生分布独占不等于整个物种独占：例如 Weezing/Muk、Seadra/Kingler 的直接野生遭遇有版本差别，但其基础物种与进化会改变取得结论。不能把这类野生差异直接放入“必须交换”清单。

初始伙伴、月见山两种化石、游走神兽等是同版本存档选择依赖，不能混写成叶绿专属。格斗道场初次赠送二选一还要考虑后期繁殖/进化可否补齐；不能永远按第一次赠送直接判为互斥物种。

### 7.2 C 类通信与其他游戏输入

- 玩家间交换，以及因交换才能得到的最终形态、另两个初始伙伴家族、另一种月见山化石家族、另外两只游走神兽、叶绿来源家族。
- 由其他第三世代作品输入的未在火红正常来源闭包内的宝可梦、招式、道具、蛋、特殊身份数据。不能为了实现“386”添加丰缘地图或在七岛塞入这些遭遇。
- 玩家间单打、双打/多人对战、无线大厅、通信房间、记录交换以及连接中断/重试/同步等；与单机双打不是同一交付物。
- 连接条件也要版本化：全国图鉴、蛋和非关都宝可梦限制，及宝石任务开放跨区域连接的进度标志。[F20]
- 原版存档导入导出、GBA 线缆/无线协议模拟、跨其他游戏/设备兼容属于独立兼容工程，不是用一个“交换”按钮即可宣称完成。

### 7.3 C 类活动岛与隐藏分发

本节说明原作外部输入边界；当前工程已接入的Ribbon／Aurora授票与活动岛业务按 [当前决定](first-clear-remaining-scope.md#中文与规则决定)保留，不由下面的原作来源分类撤销。

- Navel Rock 的洛奇亚与凤王依赖神秘船票及活动开启标志。
- Birth Island 的代欧奇希斯依赖极光船票及活动开启标志，并有三角谜题、遭遇及失败/离场状态；采用火红的形态规则。
- 固定 VermilionCity 脚本分别同时检查船票物品与 FLAG_ENABLE_SHIP_NAVEL_ROCK / FLAG_ENABLE_SHIP_BIRTH_ISLAND；只给票可能仍不能启航。[F21]
- Mystery Gift 的卡片/新闻、配信合法状态等可以后置；若将票改为普通 NPC 赠送，须命名为可选便利/活动模拟模式，不能声称是未改原版自然进程。
- 变化洞窟默认只有超音蝠。另八套表对应咩利羊、榛果球、戴鲁比、熊宝宝、长尾怪手、壶壶、惊角鹿、图图犬；普通基线不主动改动 VAR_ALTERING_CAVE_WILD_SET。无需为了文件里的备用表，宣称这些都在单机可捕获范围。[F04]
- 梦幻、时拉比、基拉祈等不能被虚构成火红本体地图遭遇。Faraway Island、Southern Island、Battle Frontier、秘密基地、华丽大赛、宝可方块及树果种植等不是本基线要移植的火红游玩内容。源库有共用/遗留结构也不改变范围。

### 7.4 图鉴完成与训练家卡不能用错门槛

固定 src/pokedex.c 的 HasAllKantoMons 排除梦幻，要求另外 150 种；HasAllMons 排除梦幻、洛奇亚、凤王、时拉比、基拉祈、代欧奇希斯共六种，检查 380 种。386 是数据目录数量，不是该函数的完成门槛。[F22]

训练家卡满星还涉及外部依赖：除了名人堂和图鉴相关星，跳绳连续次数与嘟嘟利采树果数量都至少为 200 才满足小游戏星条件。无通信单机无法自然达成所有这些目标，不能用满星作为 A 类结束条件。[F23]

正确验收是“判断与展示按原版实现，普通单机可实现的进度可完成”，而不是偷偷降低门槛或向存档塞入缺失物种。开发可用明确测试夹具验证 150/380/386 边界，但夹具不冒充正常游玩存档。

## 完整交付仍需区分的边界

- 三种开局、化石／道场等互斥选择、可选劲敌战和合法非线性顺序；受控成型队通关不冒充自然新游戏通关，也不据此判定剧情未实现。
- 全国图鉴59／60 caught、只有seen达标、未访问一岛、拒绝邀请后补做；宝石／口令／船票的前后状态和重复交互。
- 满队／满盒／满包、赠送／兑换／交换／化石失败、蛋延后领取、费用扣除、取消和异常中断后恢复。
- 双打同时KO、狩猎末球／末步、幽灵禁捕、游走HP／状态／退场、联盟失败重试、塔独立结算。
- 培育与孵化、等级上限、教学／进化条件、所有交通和机关改变后回访、长期重复游玩的保存与容量边界。
- 已批准后置的专属动画和全面视听验收保持单列；不能把规则通过转写为原版像素或真实声音通过。

实际维护入口见 [测试说明](../tests/README.md)；逐项结果以当前可用测试及明确范围为准，不要求恢复已删除的历史证据流水账。

## 固定来源索引
### F01 原始版与修订标识

- [README.md](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/README.md)

### F02 官方产品与再发行边界

- [Pokémon 官方原始产品介绍](https://www.pokemon.com/us/pokemon-video-games/pokemon-firered-version-and-pokemon-leafgreen-version)
- [Nintendo 再发行 FAQ](https://en-americas-support.nintendo.com/app/answers/detail/a_id/71365/p/989)

### F03 地图清单与分组

- [data/maps/map_groups.json](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/map_groups.json)

### F04 野生表与条件选择

- [src/data/wild_encounters.json](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/data/wild_encounters.json)
- [src/wild_encounter.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/wild_encounter.c)

### F05 捕获数奖励阈值

- [data/maps/Route2_EastBuilding/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/Route2_EastBuilding/scripts.inc)
- [data/maps/Route11_EastEntrance_2F/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/Route11_EastEntrance_2F/scripts.inc)
- [data/maps/Route15_WestEntrance_2F/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/Route15_WestEntrance_2F/scripts.inc)

### F06 红莲邀请与一至三岛交通

- [data/maps/CinnabarIsland/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/CinnabarIsland/scripts.inc)
- [data/scripts/seagallop.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/scripts/seagallop.inc)

### F07 欢乐游戏城剧情与通信玩法

- [data/maps/TwoIsland_JoyfulGameCorner/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/TwoIsland_JoyfulGameCorner/scripts.inc)
- [data/scripts/cable_club.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/scripts/cable_club.inc)

### F08 全国图鉴触发

- [data/maps/PalletTown/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/PalletTown/scripts.inc)
- [data/maps/PalletTown_ProfessorOaksLab/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/PalletTown_ProfessorOaksLab/scripts.inc)

### F09 红蓝宝石与全后期解锁

- [data/maps/OneIsland_PokemonCenter_1F/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/OneIsland_PokemonCenter_1F/scripts.inc)

### F10 四至六岛宝石任务

- [data/maps/FourIsland_IcefallCave_Back/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/FourIsland_IcefallCave_Back/scripts.inc)
- [data/maps/SixIsland_RuinValley/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/SixIsland_RuinValley/scripts.inc)
- [data/maps/SixIsland_DottedHole_1F/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/SixIsland_DottedHole_1F/scripts.inc)
- [data/maps/FiveIsland_RocketWarehouse/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/FiveIsland_RocketWarehouse/scripts.inc)

### F11 强化联盟

- [data/maps/PokemonLeague_LoreleisRoom/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/PokemonLeague_LoreleisRoom/scripts.inc)

### F12 五六岛支线与赠蛋

- [data/maps/FiveIsland_WaterLabyrinth/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/FiveIsland_WaterLabyrinth/scripts.inc)
- [data/maps/FiveIsland_ResortGorgeous_House/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/FiveIsland_ResortGorgeous_House/scripts.inc)
- [data/maps/FiveIsland_MemorialPillar/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/FiveIsland_MemorialPillar/scripts.inc)
- [data/maps/SixIsland_WaterPath_House1/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/SixIsland_WaterPath_House1/scripts.inc)

### F13 遗迹谜题与土龙弟弟洞窟

- [data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc)
- [data/maps/ThreeIsland_DunsparceTunnel/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/ThreeIsland_DunsparceTunnel/scripts.inc)

### F14 游走物种与状态

- [src/roamer.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/roamer.c)

### F15 狩猎地带

- [src/safari_zone.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/safari_zone.c)
- [data/maps/FuchsiaCity_SafariZone_Entrance/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/FuchsiaCity_SafariZone_Entrance/scripts.inc)

### F16 九项 NPC 交换

- [src/data/ingame_trades.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/data/ingame_trades.h)

### F17 教学与回忆

- [data/scripts/move_tutors.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/scripts/move_tutors.inc)
- [data/maps/TwoIsland_House/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/TwoIsland_House/scripts.inc)
- [src/data/pokemon/tutor_learnsets.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/data/pokemon/tutor_learnsets.h)

### F18 寄放与培育

- [src/daycare.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/daycare.c)
- [data/maps/FourIsland_PokemonDayCare/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/FourIsland_PokemonDayCare/scripts.inc)
- [src/data/pokemon/egg_moves.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/data/pokemon/egg_moves.h)

### F19 进化与无 RTC 差异

- [src/pokemon.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokemon.c)
- [src/evolution_scene.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/evolution_scene.c)
- [src/data/pokemon/evolution.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/data/pokemon/evolution.h)

### F20 通信限制

- [src/trade.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/trade.c)

### F21 活动票双条件与活动岛

- [data/maps/VermilionCity/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/VermilionCity/scripts.inc)
- [data/maps/BirthIsland_Exterior/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/BirthIsland_Exterior/scripts.inc)
- [data/maps/NavelRock_Summit/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/NavelRock_Summit/scripts.inc)
- [data/maps/NavelRock_Base/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/NavelRock_Base/scripts.inc)

### F22 图鉴完成判定

- [src/pokedex.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokedex.c)
- [include/constants/pokedex.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/constants/pokedex.h)
- [data/maps/CeladonCity_Condominiums_3F/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/CeladonCity_Condominiums_3F/scripts.inc)

### F23 训练家卡星条件

- [src/trainer_card.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/trainer_card.c)

### F24 训练家之塔

- [include/constants/trainer_tower.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/constants/trainer_tower.h)
- [src/trainer_tower.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/trainer_tower.c)
- [src/trainer_tower_sets.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/trainer_tower_sets.c)
- [data/maps/TrainerTower_Lobby/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/TrainerTower_Lobby/scripts.inc)
- [data/maps/TrainerTower_Lobby/text.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/TrainerTower_Lobby/text.inc)

### F25 长期可重复内容

- [src/vs_seeker.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/vs_seeker.c)
- [src/renewable_hidden_items.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/renewable_hidden_items.c)

### F26 特殊战斗与地区事件

- [data/maps/ThreeIsland_BerryForest/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/ThreeIsland_BerryForest/scripts.inc)
- [data/maps/PokemonTower_6F/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/PokemonTower_6F/scripts.inc)
- [data/maps/CeladonCity_GameCorner/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/CeladonCity_GameCorner/scripts.inc)
- [data/maps/CeladonCity_GameCorner_PrizeRoom/scripts.inc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/CeladonCity_GameCorner_PrizeRoom/scripts.inc)

### F27 招式和共用道具表及战斗规则

- [include/constants/moves.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/constants/moves.h)
- [include/constants/items.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/constants/items.h)
- [src/battle_script_commands.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_script_commands.c)
- [src/field_poison.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/field_poison.c)

