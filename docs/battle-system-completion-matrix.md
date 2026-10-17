# 当前战斗系统矩阵

更新：2026-10-17。本文与 [机器可读清单](battle-system-completion-matrix.json)维护当前实现边界，不累计历史批次或要求恢复已退休测试。普通单／双打规则阶段已达到既定出口，主线及后期业务已经继续推进；不再沿用“第五徽章阶段仍在开发”的历史状态。

<a id="shared-utilityweather-and-ability-commands-2026-10-11"></a>

## 口径

- 354个招式身份已有规则准入；[AllMoveRecipeTests](../tests/Game.Battle.Tests/Turns/AllMoveRecipeTests.cs)为每招的四个真实出战位置生成受控公共回合用例，共1,416行。它验证实际执行而非仅注册，但不能证明全部组合、随机分支、获得途径或特殊模式。
- 76个有效特性身份已准入，不含NONE和未使用的CACOPHONY；身份准入不是所有入场、复制、天气、免疫和残留交互穷尽验证。
- 专属招式脚本166／354，其余188尚未接专属还原。这是实现范围，不是总体完成率，也不是“全部动画写好只差验收”。
- 定义存在、规则执行、应用入口、专属动画、原生像素一致和真实声音输出分别成立；不能相互替代。当前166快照完整Presentation793／793通过，包含全部生成根逐帧合成；本次有界Application238／238通过。最新完整Application仍为投影基础1562／1562，完整Battle仍为131快照1709／1709。这些受控检查不等于原生逐帧或实际扬声器验收。

2026-10-16原生有界抽查：使用已验证场地存档，从Route 1普通遇敌进入战斗；敌方Tackle后，我方Thief实际切换Dark背景，脚本结束后草地恢复且战斗菜单可继续，无错误日志。这只验证该调用路径，不代表全部背景逐帧一致。

同日Double Slap受控场地原生样本记录两次真实命中回执各自开始／结束，均未额外ACK，随后显示两次命中及敌方HP归零。但第二击期间一次1180×812截图全黑（含HUD与外边框），相邻截图正常且日志无异常；尚未确定是渲染还是捕获瞬态，不能据此接受该路径的视觉一致性。需保留引擎视口与桌面截图的配对证据复查。 证据：`/workspace/shared/firered-multihit-visual/native-double-slap-2002.log`，MVID `e7ca54ed-3ee5-4d8a-962c-9dea810eb49f`；Sequence8在126311→126874ms播放40帧，Sequence9在127317→127903ms播放34帧，两次结束均为`acknowledged=false`。

## 当前所有者与维护检查

| 范围 | 已有实现／检查 | 仍须限定的结论 |
| --- | --- | --- |
| 回合与招式 | [共享Kernel](../src/Game.Domain/Battle/SourceBattleKernel.cs)、[Frame](../src/Game.Domain/Battle/SourceBattleFrame.cs)，当前单／双打共用生命周期；[招式回合测试](../tests/Game.Battle.Tests/Turns/AllMoveRecipeTests.cs) | 每招受控回合不等于所有招式、特性、物品组合 |
| 换人、场地与残留 | [换人测试](../tests/Game.Battle.Tests/Switching/SwitchingTests.cs)、[残留测试](../tests/Game.Battle.Tests/Residuals/EndTurnResidualTests.cs)、[生命周期测试](../tests/Game.Battle.Tests/Lifecycle/BattleLifecycleTests.cs) | 保留事件顺序、真实队伍身份、暂停／恢复及已批准KO补位规则；不推定所有长期分支已验 |
| AI与训练家 | [策略测试](../tests/Game.Battle.Tests/TrainerPolicy/TrainerPolicyTests.cs)、[应用训练家测试](../tests/Game.Application.Tests/TrainerBattles/TrainerBattleTests.cs) | 当前源AI与行动组合已有实现；不将单个训练家或有限状态检查扩张为所有队伍验收 |
| 捕获与物品 | [捕获应用测试](../tests/Game.Application.Tests/WildBattles/WildCaptureTests.cs)、[道具测试](../tests/Game.Battle.Tests/Items/BerryItemTests.cs) | 普通捕获／持物／药品边界不自动覆盖特殊遭遇或动态谜之果载荷 |
| 特殊模式 | [Safari](../src/Game.Application/CampaignSafari.cs)、[训练家塔](../src/Game.Application/CampaignTrainerTower.cs)等独立应用所有者 | 核心业务已接；全部特殊结果、区域和原生表现仍单列，外部通信／e-Reader另属延后范围 |
| 当前组合与保存 | [组合入口](../src/Game.Content/PretCurrentGame.cs)、[组合测试](../tests/Game.Application.Tests/Campaign/CampaignCompositionTests.cs)、[generated-only测试](../tests/Game.Application.Tests/Campaign/GeneratedOnlyCampaignTests.cs) | 严格存读与短移动不证明自然完整主线；受控战斗序列化不等于玩家可战中保存 |
| 专属表现 | [分派白名单](../src/Game.Application/CampaignMoveAnimationDispatch.cs)、[Godot播放入口](../src/Game.Godot/CampaignSourceMoveAnimationView.cs) | 规则全部准入不表示每招有专属动画；已有脚本也有情境和验证边界 |

持物触发另属通用演出，不计入354招：`General_HeldItemEffect`已接入普通单／双打的真实持物结果，播放原版三次摆动、星光、扩环及完整声音命令，替换首声占位。与能力变化结果共用播放／取消生命周期；在首个效果回执之前播放，使用最终消耗回执去重，结束不提交ACK、RNG或游戏规则命令。按Battle Scene、替身和事件时刻的半无敌状态跳过，跨命令只保留有界的前态快照。23个分派用例通过，包括真实四位置Lum治疗及Dig／Leftovers两回合；六个单／双打位置的脚本／材质检查通过。原生持物调用路径尚未抽查。

能力变化通用波纹与专属招式分开排程：直接Bulk Up、Tickle、Swagger已按原脚本把双能力合为一次波纹，保留封顶／特性阻挡的部分成功顺序及后续White Herb、Lum等独立持物效果；33个分组分派用例通过。能力封顶文字仅按这三条源路径静默，阻挡／无变化不生成波纹。原作能力波纹在Battle Scene关闭时仍播放，持物演出仍受该选项控制，5个独立策略用例通过。单／双打共用Own Tempo、Clear Body、White Smoke及Hyper Cutter／Keen Eye的正确能力／具体能力值文字映射，7个表现用例通过。

这不覆盖其他多能力招式、入场／道具／次要效果的全部波纹时序。Calm Mind、Dragon Dance、Memento、Superpower、Overheat、全能力次要提升、Curse、Belly Drum、Flatter等仍需各自真实结果脚本闭包；调用／复制／Snatch／反射结果也须满足明确调用上下文，不能由最终Frame推断。通用渲染器支持组合参数不等于这些调用路径已完成。

## 专属动画范围

当前分派支持：Mega Punch、Mega Kick、Blaze Kick、Pound、Karate Chop、Double Slap、Comet Punch、Fire Punch、Ice Punch、Thunder Punch、Scratch、Vice Grip、Cut、Gust、Wing Attack、Bind、Slam、Vine Whip、Stomp、Jump Kick、Rolling Kick、Headbutt、Horn Attack、Fury Attack、Horn Drill、Tackle、Body Slam、Wrap、Take Down、Double Edge、Tail Whip、Poison Sting、Twineedle、Pin Missile、Bite、Supersonic、Acid、Ember、Flamethrower、Mist、Water Gun、Hydro Pump、Ice Beam、Psybeam、Bubble Beam、Peck、Submission、Low Kick、Counter、Strength、Absorb、Mega Drain、Growth、Razor Leaf、Poison Powder、Stun Spore、Sleep Powder、Dragon Rage、Fire Spin、Thunder Shock、Thunderbolt、Thunder Wave、Thunder、Rock Throw、Toxic、Confusion、Psychic、Hypnosis、Agility、Quick Attack、Screech、Recover、Harden、Minimize、Withdraw、Defense Curl、Barrier、Light Screen、Reflect、Focus Energy、Self Destruct、Egg Bomb、Smog、Bone Club、Fire Blast、Clamp、Swift、Spike Cannon、Hi Jump Kick、Dream Eater、Leech Life、Bubble、Spore、Flash、Psywave、Splash、Crabhammer、Explosion、Rock Slide、Slash、Triple Kick、Thief、Flame Wheel、Aeroblast、Cotton Spore、Powder Snow、Octazooka、Zap Cannon、Bone Rush、Outrage、Giga Drain、Endure、Charm、False Swipe、Swagger、Steel Wing、Dynamic Punch、Dragon Breath、Megahorn、Iron Tail、Metal Claw、Synthesis、Vital Throw、Crunch、Mirror Coat、Whirlpool、Magic Coat、Snatch、Mud Sport、Slack Off、Crush Claw、Metal Sound、Tickle、Cosmic Power、Signal Beam、Sand Tomb、Icicle Spear、Aerial Ace、Dragon Claw、Iron Defense、Bulk Up、Mud Shot、Poison Tail、Covet。

Double Slap／Comet Punch按每个真实HP或替身命中回执播放，使用明确的零起始命中序号选择源分支；结束保留同一回执，不再提交ACK。18个应用用例含原版随机向量、提前KO、Rough Skin接触截断、替身破裂后的本体命中及零伤害命中；8个表现用例含全部256字节分支输入和缺失上下文拒绝。Triple Kick另以固定三次计划命中契约接入相同逐回执生命周期，13个应用用例验证逐击命中检查、提前截断及10／20／30威力；4个表现用例含1,024个原C条件分支向量。Beat Up等不同调用上下文未据此准入。

Rock Throw／Rock Slide共用原版落石与地形摇动回调；BG3纵向寄存器按16位值变化并恢复各控制器捕获的原值，重叠控制器及非零恢复向量已验证。画面从保留的原版512×256索引地图采样，拒绝用裁平的可见纹理假装滚动；健康栏与文字仍在240×112场地图层之后固定绘制。

Metal Claw／Crush Claw／Dragon Claw复用共享爪击帧播放器及原版地形／OBJ坐标偏移控制器；全局偏移仅投影到启用的OBJ，不移动monbg副本或HUD。528组原C向量覆盖爪击与四种寄存器摇动模式，显式SE1包装器保留即时播放、参数前缀写入及零视觉任务计数。共享素材读取器支持原版128像素宽图块存储，取消与自然结束分别验证；禁用的金属背景寄存器快照不被误当活动图层。

Thunder／Cosmic Power复用保留索引地图的滑动背景任务；摇动读取实时共享参数，非攻击者调色覆盖四个OBJ调色板组并保留原参数移位。SE1／SE2包装器使用源歌曲播放器身份校验；测试覆盖即刻执行、无视觉任务计数、TRUE／FALSE及非法标志。该批通过完整Battle 1,709／1,709及应用组合494／494；最后一次完整Application 1,202／1,202仍属于此前129招快照，未扩大为本批的全套应用证据。

Poison Sting／Twineedle／Spike Cannon共用整数BIOS角度、既有线性平移和命中图元；Twineedle按固定两次计划、Spike Cannon按2–5次计划逐真实回执播放。角度检查器可复现786,440次对照，仅保留60个紧凑边界向量；104组提取原C回调覆盖位置、友方目标、生命周期及255／256／257位移的16位分子截断。现有图块复用，无新增像素素材。

Horn Attack／Fury Attack／Horn Drill共用原版7位定点Horn回调，保留signed16累加溢出、持续帧夹限和倒数第二帧定位。Fury按真实2–5击回执继续；OHKO检查涵盖命中阈值、Sturdy、等级、Protect及Ghost阻断。导入器核对FireRed的`jumpifcontest`函数仅前进5字节后，折叠零时间死分支并重定位可执行标签；不导入比赛背景、不保留死分支运行时元数据。Horn批应用组合542／542通过；完整Application与Battle证据仍按各自快照限定。

其他招式返回 `script-outside-slice`，不启动该专属脚本；普通文字、HP及结果表现仍独立工作。不能因此说招式规则不可用，也不能把通用反馈当原版专属动画。此名单由当前单／双打分派代码核对，JSON逐招标记相同范围。

用户于2026-10-16要求完成除汉化外的单机内容，专属动画与剩余原生表现重新进入当前工作范围。采用共享实现与代表性检查，保持实现范围和已验证范围分开。[Options玩家入口](../src/Game.Godot/CampaignOptionsView.cs)已开放Battle Scene和Sound编辑并接入保存与运行时应用，见[设置说明](battle-scene-sound-options.md)。

Cosmic Power的规则修正限定在现有`EFFECT_COSMIC_POWER`分支：保留双项封顶的提前失败，按原作执行Defense再SpDef两条命令。单项封顶现在产生真实最大值回执，表现层跳过该文字；这也经现有历史归约使`LastLandedMove`无效，并保留既有`LastHitByType`，不是仅表现改动。HP、PP、最终能力阶级及RNG保持既定结果。依据固定源`battle_scripts_1.s:2679–2702`与`battle_script_commands.c:6809`，没有新增招式专用历史例外。公共回合检查含两种部分封顶顺序、四个出战位置及非空既有类型历史。

调用者复核：BasicStats还接入Swords Dance、Sand Attack、Tail Whip、Leer、Growl、Growth、String Shot、Agility、Screech、Double Team、Harden、Smokescreen、Withdraw、Focus Energy、Kinesis、Flash、Sweet Scent；这17个其他分支及共享`BasicStatChange`均未修改，未将Cosmic的两命令顺序推广到它们。

Megahorn复用同一线性移动、帧动画与仿射播放器，按目标侧改写共享参数，保留8.8定点舍入和完成后的回调交接帧。四个实际规则入口用例覆盖所有位置及队友目标、未命中、Protect、Wonder Guard、替身、击倒及Rough Skin；当前138招快照通过Presentation 584／584、应用组合546／546、14个动画生产器一致性检查及布局28／28。首次构建的验证器确定赋值错误已以显式受验证配方查找修正，随后Godot／Presentation构建零警告、零错误；完整Application及Battle范围仍保持上述历史快照。

Pin Missile／Icicle Spear复用同一BIOS整数角度、帧播放器和线性移动器；第二次弧线预取恢复原版八个数据字所对应的可变状态，但保留预取后的坐标及矩阵。128组原C向量覆盖持续帧1／2／255、16位边界和结束路径；每次实际命中独立播放脚本中的三颗投射物。140招快照完整构建零警告、零错误，Presentation 598／598、应用组合558／558及全部14个导入器一致性检查通过。

Aeroblast复用共享线性移动器，按原版SeekSpriteAnim的直接FRAME路径设置初始帧与延时，保留循环计数；导入器验证源指令索引对应四个FRAME，控制指令不能被当作帧索引。原C提取的13组／117帧向量及结束、循环、暂停边界通过；天空辅助例程的常量条件仍仅在导入时折叠。141招快照通过Presentation 615／615、应用组合563／563、14个生产器一致性检查，完整构建零警告、零错误。

共享控制流支持按当前程序作用域解析标签、signed16参数相等分支、即刻写入返回参数的源任务和无淡化的changebg。导入宏归一为jumpargeq；辅助例程可向前面的return跳转，但构造期拒绝循环、跨作用域标签和递归。原版restorebg与取消清理分别验证；这项基础设施未增加专属招式数，完整Presentation 629／629通过。

Mega Punch／Mega Kick／Blaze Kick共用原版旋转拳脚回调与既有帧／仿射播放器，256组提取原C向量验证缩放、暂停后重置、等待和销毁时序。Impact辅助例程使用同一作用域标签与即时背景替换，按目标侧选择原版索引背景；导入时仅折叠经源函数证明恒假的比赛分支。144招快照完整构建零警告、零错误，Presentation 644／644、应用组合583／583（专属分派357、持物23、分组39、策略5、Quest159）、14个动画生产器一致性检查和布局28／28通过。全部生成招式共用一个完整材质构造器检查每帧；两项重复的Physical／Elemental全目录循环已移除，未删减独立源向量或调用边界检查。次要能力变化的通用波纹与专属脚本计数分开，当前有界结果入口见下。

2026-10-17共享次要能力波纹已接入八类源效果：目标Attack／Defense／Speed／SpAtk／SpDef／Accuracy下降一级，以及攻击者Attack／Defense上升一级。由既有招式元数据选择18个单目标定义，不使用逐招白名单；严格核对直接调用、真实PP、一次HP／替身结果及critical／survival／effectiveness顺序，在成功能力回执文字前播放。目标下降要求存活本体，攻击者上升可发生于目标倒下或替身命中之后；Endure零损失需真实伤害调整和存活回执。后续Rage、接触及持物回执不能借用该命中重复播放，封顶／未触发／阻挡不合成波纹；Battle Scene关闭及受影响者替身沿用共享策略。52个新增应用用例通过，含八类自然招式公共Kernel入口、四位置／友方目标、单打导出、无RNG／状态改写及畸形回执拒绝；完整Presentation 644／644、应用组合635／635通过，完整构建零警告／零错误。首次组合测试的多PP夹具辅助方法已按拥有该能力回执的PP修正，未改变运行时。该144快照尚未包含四个双目标定义的分目标PP语法；后续闭合见下。全能力提升组、Superpower／Overheat及道具自身StatUp仍未纳入此入口。

## 验证与规则决定

当前执行入口是 [八个维护MSTest项目](../tests/README.md)，不是已删除的历史Console套件。维护用例含受控队伍、HP、位置与随机状态，不能把总通过行数解释为自然玩家路线数量。源向量、逻辑测试、headless Draw、原生窗口及真实扬声器是不同证据；Dummy音频不能关闭实际输出门禁。

已批准的概率／Protect／Conversion 2／双打KO／王者之证／AI与服从规则差异保持不变，详见 [当前规则决定](first-clear-remaining-scope.md#中文与规则决定)。未批准候选不得由整理顺带实现。全游戏功能缺口、中文、通信进化服务待讨论及当前存档谱系边界也统一见该页。

Gust／Wing Attack／Steel Wing共用索引GUST材质、椭圆／目标平移回调及原版调色板轮换任务；帧／仿射结束交接、共享参数改写、六个出战位置与Steel Wing金属材质依赖均通过受控表现检查。

Bone Club／Bone Rush共用原版骨棒线性／仿射回调，Bone Rush复用2～5次真实命中回执；32组／352行原C向量与20个应用用例通过。普通专属入口同时统一收紧直接调用的选中招式、PP所有者／槽位／正成本与严格事件顺序，保留Pressure及耗尽时的扣减截断；范围仍不包含调用／复制等不同上下文。

Mist／Smog共用索引雾片与原版旋转平移回调，保留初始化即步进、隐藏搭档BASE坐标平均及OAM优先级3→0回绕；`splitbgprio_all`无条件写入BG1／BG2优先级，清除图层不擅自恢复寄存器。Mist按成功的五回合场地状态回执播放，已有状态／错误来源回执不播放。

已验证滑动背景基础层（不增加招式计数）：有符号8.8累加、原版256／512地图模式、独立优先级5无计数任务与共享参数停止／复位。六个新增背景的393,216像素独立核对按地图实际调色板银行读取，bank0保留原文字调色板、bank2使用招式调色板；既有背景／地形数据块逐字节保持不变。该基础层仍不等于其依赖招式已接入。

2026-10-17 Ice／Future Sight批次新增Blizzard、Icy Wind和Future Sight成功设置脚本，专属范围147／354。Snow与既有粒子共用源4-bit线性移动器，提取C回调2352行及基础算术3606行可重现；BG_ICE原生65536像素及旧材质不变检查通过。共享控制流验证负责全部路径终止，允许末尾goto回接较早的end，仍拒绝跌出程序和循环；panse使用生成的真实声音依赖。双目标分派接受源第二次PP访问保留正CommandCost但不再扣PP，按真实阻挡／无效回执选择第一个可播放目标，不用HP损失判断；覆盖首个Wonder Guard、两者阻挡、替身破坏、KO及1HP Endure零损失。Future Sight只由准确的scheduled位置计时0→3回执触发，Protect／替身／命中率不误拦截设置，延迟伤害不会重播设置；B_ANIM_FUTURE_SIGHT_HIT仍未实现。该147快照的Icy Wind分目标次要Speed波纹当时尚未接入，后续闭合见下。

147完整构建零警告／零错误（70.47秒）；修正后的Presentation 658／658、应用组合649／649（专属371、持物23、分组39、策略5、Quest159、次要能力52）通过。首次失败暴露并修复了旧末指令位置限制和首目标阻挡选择；测试另修正了Wonder Guard零损失回执、非法四位置导出及Blizzard背景侧命名的错误前提。最终Presentation复测1分37秒，应用33秒；14个生产器一致性、布局28／28、elemental22／22、background scripts10／10、background material8／8通过。证据：/tmp/animation147-build.log、/tmp/animation147-refresh.log、/tmp/animation147-presentation-final-refresh.log、/tmp/animation147-presentation-final.log、/tmp/animation147-application-corrected.log、/tmp/animation147-import-check.log。这些是受控投影／合成证据；最新完整Application仍为129快照1202／1202，完整Battle仍为131快照1709／1709，未新增原生逐帧或真实扬声器验收。

2026-10-17 Sand Attack／Mud-Slap共享AnimDirtScatter闭合，专属范围149／354。每个源脚本创建30粒子，在各自初始化时按X后Y恰好消耗两次Random，共60次；运动、快照、合成和销毁不取随机数。208组提取原C用例执行12120行，压缩保留2304行，验证掩码、符号、16-bit算术、生命周期及第31粒子的连续随机位置；复用既有mud-sand材质和线性／帧播放器。27个公共Kernel应用用例验证Sand Attack主效果和Mud-Slap次要Accuracy波纹、选中友方、阻挡／封顶／替身／KO及回执归属。

视觉随机是明确的引擎边界：保留源Random的uint32溢出LCG（1103515245×state+24691，再取高16位）和回调取数顺序，但不复现原GBA共享玩法／VBlank随机流的具体取值。种子仅存在于瞬态CommittedMoveAnimation，使用固定FNV-1a32编码既有BattleId、回执Sequence、招式／参与者、可空命中序号和通用标签／参数；字符串为UTF-8加小端长度，整数固定小端，可空值有存在标记，版本域为firered-source-animation-v1。不读取时间／进程哈希／玩法RNG，也不新增存档、事件、资源凭据或持久种子。相同调用重放一致，不同回执可产生不同粒子；取消丢弃本地流；实际调用RNG回调且缺种子时明确拒绝，非随机脚本无需种子。当前无Random2消费者，未将其错误合并进此流。

149完整构建零警告／零错误（69.28秒）；最终Presentation 679／679（新增16个Dirt向量／生命周期用例及5个共享RNG用例）、应用组合676／676（专属398、持物23、分组39、策略5、Quest159、次要能力52）通过。验证含身份派生种子、重复快照／合成无副作用、取消后独立重放、交错玩法取数及玩法状态／trace不变。首次Presentation失败揭示共享目录未调用的Dirt辅助例程被错误要求种子；修正为复用资源解析器的同一递归依赖闭包，仅实际调用依赖需要瞬态上下文，全部辅助例程的结构与配方仍严格检查；嵌套调用仍按既有VM规则拒绝。修正后Godot／Presentation构建通过，完整Presentation 1分46秒、应用31秒。47个physical导入检查、14个生产器一致性和布局28／28通过。日志：/tmp/animation149-build.log、/tmp/animation149-refresh.log、/tmp/animation149-presentation-corrected.log、/tmp/animation149-application.log、/tmp/animation149-import-check.log、/tmp/animation149-layout.log、/tmp/dirt-product-checks.log。未扩展原生逐帧／扬声器验收；需要同帧原始子精灵创建调度的Meteor Mash等仍未闭合，该149初始快照的BOTH-target次要能力波纹仍待单独跟进。


2026-10-17双目标次要能力波纹已通过同一元数据入口闭合Acid、Bubble、Icy Wind、Muddy Water：按当前能力回执之前最后一条真实PP访问确定目标，核对首轮扣减和第二轮NO_ATTACKSTRING保留正成本但不扣PP；不从当前场面推测Pressure。单目标成本1～2、双目标1～3和PP字节范围严格校验。每个目标各自使用既有伤害／存活／效果顺序解析，首目标未命中、Protect、免疫、Clear Body、封顶、替身或KO不阻止后续合法目标；Rage／White Herb与畸形后缀不能借用先前命中重复播放。普通专属双目标入口同步采用源成本／字节边界，无玩法生产器变化。

此批完整构建零警告／零错误（70.37秒），完整Presentation 679／679（1分42秒），应用组合706／706（32秒；专属398、持物23、分组39、策略5、Quest159、次要能力82）。保留50个原次要能力用例，新增12个回执边界及20个公共Kernel用例；仅移除两条已经过时的双目标排除向量。首次四个单打夹具在入口因缺少Trainer队伍被拒绝，改用既有合法Wild单打设置后，Application.Tests刷新4.82秒并全组合通过；匹配器无需修正。日志：/tmp/animation149-spread-build.log、/tmp/animation149-spread-presentation.log、/tmp/animation149-spread-refresh.log、/tmp/animation149-spread-application-corrected.log。专属计数仍为149／354；全能力组、Superpower／Overheat、调用／复制上下文和道具自身StatUp仍是独立未闭合范围。最新完整Application仍为129快照1202／1202，完整Battle仍为131快照1709／1709。


2026-10-17 Bullet Seed331接入同一2～5次真实命中入口，专属范围150／354。每次已提交命中运行原脚本的10颗种子，不能把视觉粒子数当作玩法命中数。共享线性／帧／仿射播放器保留20次位移、独立回调交接、撞击声音先于两次本地Random取数、16次反弹及销毁时序；取消释放视觉随机流。回调声音由固定源函数提取为生成依赖，缺少实际依赖时拒绝，未增加声音或随机解释器。304组提取C用例执行11856行，其中272组处于已准入坐标范围；更宽数值探针仅作为源算术证据，不扩大运行时输入范围。

有界验证：Godot、Presentation.Tests、Application.Tests构建均零警告／零错误；Presentation100／100（含9个新增用例、共享RNG／线性／仿射回归以及全部生成招式每帧依赖／合成门），应用320／320（含6个新增公共Kernel用例与既有多段命中入口回归），physical导入50／50、生产器一致性及布局28／28通过。首次取消夹具错误要求随机流保留，改为核对既定释放契约、无新增声音及独立重放后通过，无运行时修补。日志：/tmp/animation150-build.log、/tmp/animation150-refresh.log、/tmp/animation150-presentation-corrected.log、/tmp/animation150-application.log、/tmp/bullet-seed-stage-checks-refreshed.log、/tmp/bullet-seed-product-import.log、/tmp/animation150-layout.log。未重跑未改动的完整套件；完整Presentation679、组合Application706仍属于前一149快照，完整Application1202和Battle1709仍分别属于129／131快照。未新增原生逐帧或扬声器验收。


2026-10-17 Rock Smash249／Rock Blast350复用既有rocks素材，专属范围152／354。两条源脚本由既有elemental依赖发现直接解析，physical150目录完全不变，无重复根或逐招导入排除。碎片使用源有符号16-bit分子／累加器和算术右移，复用既有同类步进；Rock Blast共享目标平移器按参数高／低字节分别选择攻击者与目标的BASE／PIC坐标，保留对手侧共享参数变号与仿射程序。168组原C用例2880行可重现，旧0／1坐标模式24组迁移向量同时核对位置与共享参数，新增256／257模式不替代旧语义。

Godot、Presentation.Tests、Application.Tests有界构建均零警告／零错误；Presentation95／95（含7个新增源向量／完整脚本用例和全部生成招式逐帧合成），应用423／423（含21个新增HM06兼容／自然招式公共Kernel用例及既有多段／次要能力回归）一次通过。覆盖Rock Smash独立Defense波纹、Rock Blast真实2～5次命中、四位置／选中友方、未命中／Protect／免疫、替身／KO／Endure零损失和无玩法RNG改写。elemental导入24／24、14个生产器一致性、布局28／28及原C夹具重现通过；无新增像素文件。证据：/tmp/animation152-build.log、/tmp/animation152-presentation.log、/tmp/animation152-application.log、/tmp/animation152-elemental-import.log、/tmp/animation152-producers.log。完整套件和原生／音频边界沿用上段明确的快照，不能将这组有界通过写成新完整Application或真实扬声器验收。


2026-10-17 Fang三招Hyper Fang158、Super Fang162、Poison Fang305接入，专属范围155／354。两种薄回调复用既有帧／仿射播放器，保留32／9个可见更新及不同END销毁交接；共享Fang图块不另设解释器。Hyper Fang由背景依赖发现独立拥有，另外两招归physical；固定FireRed的contest判断由唯一导入函数验证后折叠，保留arg7写入、目标侧Impact分支及重定位标签，拒绝绕过判断任务的标签入口，不载入死分支素材。

普通专属入口现在可由实际招式元数据区分EFFECT_POISON_FANG成功命中之后的毒属性次要NoEffect。它复用次要能力入口的同一伤害前缀解析，要求HP／critical／存活／属性结果顺序；Endure一HP零损失必须有真实调整和成功存活回执，状态命中路径的零损失critical也必须有对应标记。Steel／Ghost主效果免疫仍拒绝，未增加玩法事件、规则或RNG写入。

有界构建Godot／Presentation.Tests／Application.Tests零警告／零错误；最终Presentation103／103（新增10例、共享指令／背景回归与全部生成招式逐帧合成）、应用594／594（新增26个自然招式公共Kernel用例及既有专属／分组／次要能力回归）通过。初次失败仅为源scale误当矩阵系数、以及故意伪造slot3的双打负例被错误导出为单打；按源倒数转换和明确适用的适配器修正测试后通过，原C向量及运行时未因这两项失败修改。physical导入56／56、background12／12、14个生产器一致性及布局28／28通过。日志：/tmp/animation155-build.log、/tmp/animation155-refresh.log、/tmp/animation155-presentation-corrected.log、/tmp/animation155-application-corrected.log、/tmp/fang-stage-checks.log、/tmp/animation155-background-import.log、/tmp/animation155-producers.log。完整套件、原生画面及实际声音边界仍沿用此前明确快照。


2026-10-17 Soft-Boiled135／Milk Drink208由既有elemental依赖发现闭合，专属范围157／354。两种确定性回调复用帧／仿射播放器，增加共享源仿射LOOP的计数、回跳、同次更新继续和重新开始语义；保留蛋壳上下半部、共享arg7终止及全局alpha清理顺序。没有导入时展开或第二套仿射解释器。现有elemental脚本统一带源数值spriteTags，资源解析与旧目录读取使用同一元数据，未增加两个新tag的运行时白名单。旧56根命令／子例程／配方／像素不变，新增两个源图块，ThinRing继续引用既有共享素材。

恢复入口要求源动作已规范化为自身目标，并核对唯一恢复回执的拥有者与HP算术；满HP失败不播放，选中敌方／友方的自然输入仍由公共Kernel规范化为自身。Godot／Presentation.Tests／Application.Tests有界构建零警告／零错误；Presentation179／179（8个新增用例、48组原C轨迹8176行、既有仿射用户回归及全部生成招式逐帧合成）、应用183／183（10个新公共Kernel用例、既有恢复／普通入口和选项回归）首次通过。elemental导入27／27、14个生产器一致性及布局28／28通过，4096个新源像素和旧素材保持检查成立。日志：/tmp/animation157-build.log、/tmp/animation157-presentation.log、/tmp/animation157-application.log、/tmp/animation157-elemental-import.log、/tmp/animation157-producers.log。仍不扩大此前完整Application／Battle或原生画面／真实声音验收范围。


2026-10-17 Fury Swipes154／Arm Thrust292接入既有逐次命中回执路线，专属范围159／354。两种源回调复用帧播放器：Fury Swipes在源帧END后直接销毁，每次已提交命中保留脚本作者安排的两道抓痕；Arm Thrust依据已提交命中序号与目标侧进行u8奇偶调整，按独立持续计数销毁，不以帧END代替。未调用的共享Arm辅助程序不要求命中上下文，但全部辅助程序仍接受结构／参数校验；实际调用必须具备上下文。新增一张注册SWIPE源图块，旧50个physical根及子例程保持不变。

Godot／Presentation.Tests／Application.Tests串行构建零警告／零错误；Presentation62／62（9个新用例、80组原C紧凑轨迹1012行、全部生成招式逐帧合成及既有逐次命中／Fang／physical回归），Application336／336（16个新公共Kernel用例及既有逐次命中入口）通过。公共回合覆盖2至5次独立计数、替身破裂、提前KO、Endure及接触截断；呈现不改变战斗状态或RNG。6个有界导入检查、physical生产器一致性及布局28／28通过。隔离原语测试在编译前清除不再适用的原根标签；源数据和运行时未因此修改。日志：/tmp/animation159-build.log、/tmp/animation159-presentation.log、/tmp/animation159-application.log、/tmp/animation159-physical-producer.log、/tmp/animation159-layout.log。最新完整Presentation仍为149快照679／679，完整Application／Battle与原生／真实音频验收边界不变。


2026-10-17 Sing47／Grass Whistle320／Belly Drum187由既有elemental依赖发现闭合，专属范围162／354。三种确定性回调复用帧、仿射、线性和正弦机制；音乐彩色音符使用一套共享OBJ调色板槽位所有权：保留战斗槽0至3，动画分配槽4至15，重复tag分配、首次匹配释放、释放后颜色保留、每个精灵固定槽选择均按源行为。tag视图由唯一槽数组派生；帧快照复制颜色，合成不改变状态；没有新增素材查找回退或第二调色板解释器。新音符和手部使用源索引图块，旧58根／73配方及既有像素保持不变。源仿射字面量0xFFF4按signed16解析为−12。

睡眠入口复用已提交主要状态回执；公共Kernel拒绝自身选择，避免源零速度音符的不终止情境。Belly Drum核对相邻PP、Attack升至12、自身HP费用回执及所有权／顺序／算术，动画归属于stat回执；源脚本没有General_StatsChange，因此不添加通用升能力波。成功与失败均不依赖最终Frame推断。公共回合证明HP费用max(1,maxHP/2)、低HP／能力上限失败及自身目标规范化。

首次串行Godot／Presentation.Tests／Application.Tests构建零警告／零错误，完整Presentation730／730（2分20秒）与应用243／243（含10个新公共Kernel用例）通过。源复核随后修正已准入cycle32767的s16混色计数回绕；原脚本使用0／12，修正不改变其结果。Godot／Presentation有界刷新后68／68通过，包含新增原C32767→−32768边界、全部生成根逐帧合成和既有调色板使用者。最终Music9个Presentation用例、257组原C轨迹12316行、20个分配／释放快照；31个导入检查、14个生产器和布局28／28通过。日志：/tmp/animation162-build.log、/tmp/animation162-presentation.log、/tmp/animation162-application.log、/tmp/animation162-refresh.log、/tmp/animation162-presentation-corrected.log、/tmp/animation162-elemental-import.log、/tmp/animation162-producers.log。未把此前完整Application／Battle证据扩展为本快照完整运行，也未增加原生画面／真实音频验收声明。

已核实的共享显示边界：Castform源形态使用独立前方尺寸／Y／悬浮表，背面尺寸固定32×40但Y也不同；当前按species／face查找的通用几何甚至与Normal源表不同。双打保留事件时CalledOverlay但视图未消费形态，单打仍需事件前缀投影审计。此项的事件时点投影及静态形态素材／几何已由后述两道门槛修复；一般形态变化过渡仍开放，Double Kick仍需其独立回调／回执门槛。

2026-10-17 Water Pulse352接入，专属范围163／354。共享精灵调度以单一64槽注册表取代效果列表，保留既有出战精灵占位；按源顺序实时遍历，回调后重新读取当前槽，因此高槽新生子精灵同帧执行、低槽等待下次遍历、当前槽销毁再分配只推进替代对象的帧／仿射阶段。原有回调保持已验证的组合更新，首个原始子精灵使用独立回调／播放器阶段；残留推进复用同一注册表。16个注册表用例包含10组原C遍历轨迹74事件，覆盖复用、保留槽、满池哨兵及立即创建。

Water Pulse三个源回调复用既有帧／仿射／正弦和动画局部随机流，三环共创建12个不计入visual wait的子精灵，按源次序消耗12次随机数。37组原C配置验证较低槽新生精灵首次显示tile8／单位矩阵／零偏移、随后20次回调生命期；完整根的最终调色板渐变排空子精灵，独立短脚本另验残留推进与取消。保守源占位上界为60／64精灵、19／32矩阵，并以固定保留槽下的自由槽顺序对应证明相对分配语义，不声称本地槽号等于GBA数值身份。仅增加蓝环调色材质，elemental新增352，旧61根及既有素材保持不变。

普通成功伤害回执驱动专属根；Water Absorb的主要阻止拒绝，Own Tempo／Shield Dust的无声次要阻止不撤销已经发生的命中。14个真实Kernel用例覆盖四位置／同伴、替身、命中失败、零损失Endure及不可变投影。完整Presentation、应用与导入结果见本批日志。Castform形态中心／显示几何仍沿用已记录的未闭合边界；Double Kick的尺寸依赖仍未准入，未增加原生视觉／真实音频验收声明。

本批构建Godot／Presentation／Application零警告零错误（46.65／4.64／7.35秒）；应用197／197通过。完整Presentation749／754的五个失败均为新夹具在返回脚本后释放共享配方字典，修正仅复制解析器返回的配方字典；测试刷新后29／29通过（41秒），包含全部37组回调配置、16个注册表用例及全部生成根逐帧合成。旧31个elemental导入检查、新3个依赖检查、生产器一致性及布局28／28通过。日志：/tmp/animation163-build.log、/tmp/animation163-presentation.log、/tmp/animation163-application.log、/tmp/animation163-refresh.log、/tmp/animation163-presentation-corrected.log、/tmp/water-pulse-elemental-check.log、/tmp/water-pulse-product-checks.log、/tmp/animation163-layout.log。完整Application仍为129快照1202／1202，完整Battle仍为131快照1709／1709。

2026-10-17 Meteor Mash309接入，专属范围164／354。两种源回调复用163的实时精灵注册表及既有帧播放器；三个父星产生48个原始子星，子星只在首次回调按源次序消耗两次动画局部随机数，共96次。新gold-stars源图块为16×24／0xC0字节，父星与子星共享；既有Cosmic背景、旋转拳、命中图块、shake及panse不另设机制，旧52个physical根／子程序保持不变。

20组原C配置覆盖低槽延迟初始化的tile0→4／5、首次回调随机顺序、闪烁、有符号字宽、分配失败不消耗随机数及清理；后续严格UBSan复核发现Meteor和Water Pulse外部保留槽的虚拟播放器未初始化，故撤回此前整套O0／O2+UBSan一致的结论。两个生成器仅将这些人工惰性占位的帧／仿射播放器暂停；修正后严格UBSan均exit0，完整O0／O2轨迹一致，两份已提交紧凑fixture逐字节不变，未改运行时或托管预期。复核日志：/tmp/paused-reservation-oracle-verification.log。原始子星峰值36，父星加子星微步峰值37；保守加入拳和命中图块及普通双打18个固定占位，仍低于64槽，矩阵上界6／32。首星之前至少36帧渐变排空当前已准入的有限残留，完整根最早156帧结束，子星最迟146帧消失。证明限定当前普通单／双打入口和已准入残留集合，未来新增残留消费者需重审；没有把本地槽号等同于GBA数值身份。

普通命中和既有metadata驱动的自身Attack次要提升分别拥有各自视觉回执。12个真实Kernel用例覆盖四位置／同伴、能力／命中／Protect阻止、目标替身／KO后自身提升、能力接触KO、封顶及畸形PP；没有新增规则或随机数消费。7个Presentation用例覆盖源向量、48次出生／96次抽取、完整逐帧合成／重放、残留／取消、满池与依赖拒绝。首次两个完整根测试因本地音频夹具未实现已有SetPan失败，补齐记录并断言源pan活动后重跑。Castform几何和未来Hail／Double Kick范围仍未计入；无新的原生画面或真实音频验收。

164串行构建Godot／Presentation／Application零警告零错误（46.38／4.86／7.80秒）；修正后Presentation53／53（1分3秒，含全部生成根逐帧合成）、应用235／235（21秒）、布局28／28通过。physical完整65个检查首次63通过／2个旧清单断言失败，修正仅增加既有Cosmic依赖期望并移除309旧未支持断言，两项定向重跑2／2通过；生产器一致性通过。日志：/tmp/animation164-build.log、/tmp/animation164-presentation.log、/tmp/animation164-application.log、/tmp/animation164-refresh.log、/tmp/animation164-presentation-corrected.log、/tmp/animation164-layout.log、/tmp/meteor-physical-check.log、/tmp/meteor-import-refresh.log、/tmp/meteor-product-producer.log。完整套件与原生验收边界保持前述记录，不把本有界回归写成新的完整Application／Battle运行。

2026-10-17 Castform共用事件时点投影基础完成首道门槛，专属根数量仍164。13处真实InitializeEntry调用在能力处理前捕获不可变EntrySlots，经既有三种入口结果传至应用；字段仅为JsonIgnore瞬态数据，不改变事件／规则／RNG或玩家存档。普通双打原有身体更新逻辑抽为唯一共享reducer并保留ACK相位；单打以严格canonical事件游标在既有曝光相位更新身体，跨过未曝光回执、相同sequence的训练家镜像和尾部事件时只应用一次。新批次必须先排空旧游标，再从真正的前结果Frame开始，禁止由最终天气／最终Frame反推。野生、roamer、普通训练家、区域双打、VS Seeker及Tower入口均转发真实种子。

新10个受控用例验证slot身份／昏厥身体、稀疏畸形种子拒绝、Transform复制当时供体形态、替换先于入场天气、HP不可变、真实Castform／Kyogre入口及RNG一致性、公开入场ACK／后续行动、严格活动战斗save_event_active拒绝与场地Continue不变。共同Controlled测试夹具原先直接构造战斗而缺少瞬态种子，首次完整Application的37个失败均源于此；补入其真实入口种子和原有显式敌方覆盖，不向运行时添加最终状态回退。形态素材／普通摆放／动画几何属于下一独立门槛；B_ANIM_CASTFORM_CHANGE一般过渡动画仍未闭合。

投影基础完整solution构建零警告零错误（74.34秒）；测试夹具刷新后完整Application1562／1562通过（2分49秒），成为当前最新完整应用证据；源入口／生命周期／换人与准入Battle有界65／65通过，Presentation资源／全部生成根逐帧合成6／6通过（38秒）。新10个投影／公开生命周期用例均包含在完整应用门槛内。日志：/tmp/castform-projection-build.log、/tmp/castform-projection-application.log、/tmp/castform-projection-refresh.log、/tmp/castform-projection-application-corrected.log、/tmp/castform-projection-battle.log、/tmp/castform-projection-presentation.log。完整Battle仍为131快照1709／1709，未将本65行有界检查等同于新的完整战斗运行；当前源向量生成器的保留槽修正见前述独立证据。


2026-10-17 Castform形态素材／几何第二道门槛通过，专属根仍164。既有hit-art生产器从固定原始四形态前后图／normal与shiny调色板生成16份64×64素材和8行源几何，既有素材／配方／通用几何保持不变。SourceBattleFormCatalog复用既有认证像素加载器；普通单／双打、捕获中心和动画场景共用事件时点形态选择。前方Normal／Sunny／Rainy／Snowy使用精确尺寸、Y及悬浮量；背面均32×40、Y0。Transform复制供体形态，却按原使用者身份选择shiny，再执行源white系数6；退出中的旧身体保持原显示时序。没有新增存档字段或最终Frame反推。

独立源检查覆盖65,536像素、8行几何、抽取缓存相等及畸形调色板次序／尺寸拒绝；维护hit/explosion导入16检查、布局28／28、统一14生产器检查通过。完整solution构建零警告零错误67.41秒；Presentation形态5行加全部生成根资源／逐帧合成6行共11／11通过（35秒），Application形态／投影／入口／流程22／22通过（9秒）。日志：/tmp/castform-form-build.log、/tmp/castform-form-presentation.log、/tmp/castform-form-application.log、/tmp/castform-hit-import-results/result.json、/tmp/castform-layout.log、/tmp/castform-unified-import-check.log。最新完整Application仍为上一投影快照1562／1562；本次没有重跑完整应用或原生视觉／实际声卡验收。B_ANIM_CASTFORM_CHANGE的一般过渡动画与其他种类的既有Transform配色边界仍独立开放；此静态形态修正不计入新增专属根。


2026-10-17 Wish273成功预约专属根完成，当前165／354。沿用单一sprite注册表实现CreateSpriteAndAnimate：新mini-star立即初始化／推进共用帧播放器，较高slot仍在当次扫描再次回调，较低slot等待下一帧。Wish复用Meteor的mini-star／金星素材，只参数化源subpriority；21个子sprite、42次动画本地随机抽取，不改玩法RNG。保守22个动画cell加18个普通双打常驻cell小于64，即使保留两组party summary仍不超过54；源parent65次回调后配合delay60确保raw子sprite先排空。37个sprite源配置以及22个pan源配置可复现，完整O0与严格O2+UBSan输出一致。

panse_adjustall复用已有PanSweep但保留原signed8增量、actor-side调整和signed16累计；Wish玩家63到−64、对方−63到+64均43次更新，无额外63夹限。源宽度、0增量、延迟、取消、1,024个调整结果和18个可推进任务轨迹纳入门槛。普通panse仍通过现有所有根合成与共享命令回归。Wish预约在匹配AttackString／MoveUsed／PP后紧邻自身scheduled(0→2、合法party index)回执时只播放一次；倒计时／恢复不会重播该根。源预约脚本没有“made a wish”文字，单／双打共用延迟回执静默谓词移除额外预约页，保留Future Sight／Doom Desire预约文字及既有resolved归属。

边界：B_ANIM_WISH_HEAL及零HP到期时既有resolved文字问题仍开放。普通完整回合Protect清理后Wish已验证；低层ResolveAction可在人为连续重组、仍Protected的自身状态上产生scheduled，而当前事件没有导出原生动画禁止旗标，这个低层重组范围没有宣称忠实准入。没有为此改变规则、增加存档／事件字段或伪造成功回执。

本批串行Godot／Presentation.Tests／Application.Tests构建均零警告错误（49.89／5.48／6.70秒）；Presentation96／96通过（56秒），包含新15行Wish／pan／静默文本、Meteor、raw注册表、本地随机流、共享命令及全部生成根逐帧合成；Application204／204通过（18秒），包含新14行真实Wish kernel、Future Sight／Meteor、形态投影及Quest回归。维护physical69／69暂存检查及产品producer --check通过，布局28／28通过。日志：/tmp/animation165-build.log、/tmp/animation165-presentation.log、/tmp/animation165-application.log、/tmp/wish-physical-check.log、/tmp/animation165-physical-producer.log、/tmp/animation165-layout.log。最新完整Application仍为投影基础1562／1562，完整Battle仍为131快照1709／1709；未增加原生画面／实际音频验收。按用户“先把手头工作做完。然后停一下”，完成本批本地提交后暂停；Hail仅保存外部未编译草稿和源审计，Double Kick没有实现或准入。


2026-10-17 Hail258与Wish延迟恢复表现完成，当前专属范围166／354。Hail沿用共享天气设置回执，只有成功设置五回合hail时在原MoveUsed页播放一次；已存在天气、调用招式及残留伤害页不会借用该根。30个raw冰雹粒子由计数任务拥有，最多8个命中粒子把计数转交给先分配的raw冲击；初始分配失败不增计数，冲击分配失败保留源未释放计数行为，不发明补救。24组抽取原C轨迹覆盖可见性、四种Castform尺寸和两种分配失败，实际瞬时峰值9个动画owner，保守14加4个战斗矩阵仍低于32。source sprite ID与本地ID采用严格递增空闲槽映射验证，不宣称数值相同；原始向量未改写。

共享渲染快照现在锁存BuildOamBuffer阶段：sprite回调中生成的raw子对象可在同帧显示，RunTasks后分配的对象到下一sprite pass才进入OAM。唯一sprite注册表不变；重复快照、取消及历史帧不受后续分配影响。Hail的两种raw模板通过源提取的spriteDependencies进入现有资源解析器，未添加按任务名分派素材的第二路径。旧physical原语夹具统一改用同一个解析器辅助方法，避免pack级未使用子例程缺配方；缺资源／畸形配方的负例仍保留。

Wish的resolved(1→0)与同回合、紧邻正数Recovered配对时，在原resolved文本前播放General_WishHeal，目标为当前受益位置；已有结果门控用recovery回执作重播身份，完成后不额外ACK。满HP仅显示原施放者愿望实现及HP已满，零HP实际到期静默；单独resolved形状不被当作零HP独立证明，也不播放动画。单／双打文本与动画共用同一个纯分类器，保留换人前原施放者及源Foe／Wild前缀，HP按新受益者上限恢复。源Battle Scene、替身及事件时点半无敌规则复用既有通用结果可见性机制。没有规则、RNG、事件或存档字段变更。

本次只增加Hail专属根；Wish延迟恢复是命名通用动画，不重复计数。General_Hail残留天气、Future Sight延迟命中及Castform形态改变的一般过渡仍开放；Wish低层仍Protected状态直接重组ResolveAction的预约边界保持。最新完整Application仍为投影基础1562／1562，完整Battle仍为131快照1709／1709；没有新增原生画面或真实声卡验收。用户本次明确要求补入冰雹与延迟回血后同步远端，因此本地验证提交后交给独立同步所有者并暂停，不继续其他家族。

验证：最终完整Presentation793／793（3分5秒），Claw源状态／OAM阶段14／14，有界Application238／238（18秒，含Hail8、Wish延迟恢复14、结果策略、持物、投影与Quest）通过。最终solution刷新零警告／零错误9.23秒；最后仅测试夹具的Presentation刷新4.70秒。首次构建的唯一错误是Hail负例误用不存在的MoveUsed.ChosenMove，改为真实AttackString／PP字段。首次完整呈现650／793，139处旧资源夹具、4处OAM／Hail夹具问题；统一解析器及源阶段修正后789／793，最后4处Claw初始化inUse与未构建OAM的混淆修正后全绿。未改变原C向量来迎合结果。维护elemental33、general7、统一14生产器、布局28全部通过，Hail24组原C输出逐字节复现。日志：/tmp/animation166-refresh-build.log、/tmp/animation166-final-presentation-build.log、/tmp/animation166-claw-phase.log、/tmp/animation166-presentation-final.log、/tmp/animation166-application.log、/tmp/hail-product-import-tests.log、/tmp/wish-heal-import-check-resume.log、/tmp/hail-unified-check.log、/tmp/animation166-layout.log、/tmp/hail-oracle-reproducibility.log。失败日志保留在/tmp/animation166-build.log、/tmp/animation166-presentation.log、/tmp/animation166-presentation-corrected.log。

### Castform命名通用形态过渡（2026-10-17，非新增招式）

General_CastformChange复用现有四形态素材、唯一事件时点身体投影和结果动画门。Forecast能力回执与紧邻同方的形态overlay回执必须一致；不可变前后身体来自当前事件页，不从最终天气或最终Frame反推。单打既有曝光相位、双打既有ACK相位不变；完成后的最终合成帧保留至该消息ACK，避免双打短暂回到旧形态。Scene Off仍执行该控制器通用动画；替身直接保留既有形态更新，不启动过渡；已知隐藏的活体执行立即换图。所有位置的隐藏证据复用既有蓄力回执追踪，未知则拒绝，不新增存档字段。

共享渲染增加BG1／BG2独立mosaic开关、水平／垂直4位大小寄存器及不可变身体素材选择。马赛克按屏幕原点对齐；源回调以每3次更新递增至15，在回调序号46（从0起算）替换frame／palette／默认Y，再递减并在序号92清理。普通OBJ在monbg期间隐藏；源priority2换图后的priority10背景跟随效果由同帧选定材质合成。Transform继续使用行动者shiny身份和white6基色；只复用既有16份素材，不新增像素。取消恢复本动画的初始材料和寄存器。

四个回调／命名根用例覆盖原C八组轨迹、隐藏／可见完整生命周期及缺少替代材料／非法模式拒绝；五个独立合成用例覆盖两BG等级、0／1／7／15大小、负坐标／边缘、透明采样、同伴不变和前后帧不可变。28个Application用例覆盖真实入口／天气／能力压制／切换、Transform身份、四位置、隐藏同伴、替身、重复游标及畸形回执。两条额外结果策略行验证Scene Off政策。专属范围仍166／354；General_Hail残留和Future Sight延迟命中未因此完成。

验证：完整solution构建零警告／零错误72.82秒；有界刷新30.28秒。Application262／262通过（20秒），含上述28行和身体投影、持物／Wish可见性、Quest与box回归。首次六个新夹具失败来自重复reducer生成的ImmutableArray引用比较，改为完整字段结构比较，未改生产语义。首次完整Presentation785／802，17个旧重放序列化失败均为新增材料快照暴露AnimationSlot.Partner递归；将该派生导航标记JsonIgnore，保留全部真实slot／材料数据及独立断言。最终完整Presentation802／802通过（3.1863分钟，零警告／零错误）。维护general10、统一14生产器、布局28通过；八组原C任务轨迹与隐藏／立即探针逐字节复现。日志：/tmp/castform-transition-build.log、/tmp/castform-transition-refresh.log、/tmp/castform-transition-application-corrected.log、/tmp/castform-transition-presentation-corrected.log、/tmp/castform-transition-unified-check.log、/tmp/castform-transition-layout.log。无新Castform原生或真实音频验收；最新完整Application仍1562／1562，完整Battle仍1709／1709。
