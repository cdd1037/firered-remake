# 当前战斗系统矩阵

更新：2026-10-18。本文与 [机器可读清单](battle-system-completion-matrix.json)维护当前实现边界，不累计历史批次或要求恢复已退休测试。普通单／双打规则阶段已达到既定出口，主线及后期业务已经继续推进；不再沿用“第五徽章阶段仍在开发”的历史状态。

<a id="shared-utilityweather-and-ability-commands-2026-10-11"></a>

## 口径

- 354个招式身份已有规则准入；[AllMoveRecipeTests](../tests/Game.Battle.Tests/Turns/AllMoveRecipeTests.cs)为每招的四个真实出战位置生成受控公共回合用例，共1,416行。它验证实际执行而非仅注册，但不能证明全部组合、随机分支、获得途径或特殊模式。
- 76个有效特性身份已准入，不含NONE和未使用的CACOPHONY；身份准入不是所有入场、复制、天气、免疫和残留交互穷尽验证。
- 专属招式脚本352个，另有Nature Power／Mirror Move两个身份按原作转入经认证的实际调用招式，共覆盖354／354身份。转发身份不虚增独立脚本数量，实际叶招式仍须已有脚本及真实成功结果。这是实现范围，不是总体完成率，也不是“全部动画写好只差验收”。
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
| 专属表现 | [共享元数据分派](../src/Game.Application/CampaignMoveAnimationDispatch.cs)、[Godot播放入口](../src/Game.Godot/CampaignSourceMoveAnimationView.cs) | 规则全部准入不表示每招有专属动画；已有脚本也有情境和验证边界 |

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


## Double Kick and shared random fist/foot callback — 2026-10-17

Double Kick24 adds one source root, bringing the generated unique-root inventory to167/354. AnimFistOrFootRandomPos uses the existing form-aware battler dimensions and animation-local RNG: four draws in X/Y/sign/sign order, with the source optional fifth frame-selection draw only for negative frame arguments. Its counted parent owns a raw dummy impact child in the single SpriteScene pool; child frame/affine Begin, slot ordering, sentinel allocation failure, duration-zero destruction and cancellation retain source semantics. Existing hands/feet and impact pixels are reused. Thrash shares the callback but is not admitted by this slice.

The canonical catalog/resolver supplies script identity; existing EFFECT_DOUBLE_HIT metadata supplies committed per-hit dispatch. No move ID/support table or gameplay producer changed. Source52-case traces cover callback/player/OAM phases, and the maintained physical importer72/72 preserves all previous scripts/materials. Strict original-C UBSan reproduction is byte-identical. Managed gates passed Presentation94/94 (all167 roots resolved/composed, new10 cases and affected shared scene/RNG/registry/form coverage), Application488/488 (new12 actual-kernel cases and existing dispatch regressions), and layout28/28. Initial full solution compilation found only five MSTest analyzer naming warnings treated as errors; renaming misleading expected-value locals/parameters left assertions unchanged. Final Presentation/Application project refreshes passed with zero warnings/errors; all product projects compiled in the initial build. Evidence: `/tmp/animation167-{build,refresh,presentation,application,layout}.log`, `/tmp/double-kick-{import-check,source-vector-check,product-producer}.log`. No new native frame/audio acceptance is claimed.


## Conversion pair and shared indexed OBJ fades — 2026-10-17

Conversion160 and Conversion2 176 add two complete source roots, bringing the generated unique catalog to169/354. Both reuse one indexed conversion sheet, the existing frame/affine/linear players and global palette-fade scheduler. Source callback vectors preserve shared arg7 termination, delayed frame unpause, 30-update translation and the extra destroy-callback handoff. Alpha tasks and FlashAnimTagWithColor retain the source global-fade refusal/wait semantics. Indexed OBJ fades now write the existing faded slot banks from retained unfaded source banks; direct Music writes remain faded-only, free retains bytes, and scene-owned retired materials cannot be recolored by a later invocation. No second compositor tint or palette authority was introduced.

One shared effect-based type-change receipt matcher normalizes singles/doubles and authenticates adjacent self-owned PP→overlay mutation with unchanged unrelated overlay fields. The canonical script catalog remains admission authority; no numeric support list was added. Presentation consumes the committed type and does not rerun Conversion selection or change the approved Conversion2 candidate policy. Legacy singles overlays have no Source field; canonical foreign-Source rejection is tested on doubles rather than inferred from the legacy schema.

Full solution build passed with zero warnings/errors (75.17s). Presentation78/78 passed (48.64s), including all169-root composition, nine new callback/palette/scene cases and Music/indexed-palette regressions. Application523/523 passed (25.03s), including seven natural-kernel/receipt cases and existing move/Castform dispatch coverage. Maintained elemental importer36/36, all14 producers and layout28/28 passed. The original-C oracle reproduced1860 sprite rows,130 alpha rows and19 flash-request rows byte-for-byte. Logs: `/tmp/animation169-{build,presentation,application}.log`, `/tmp/conversion-product-{tests,unified,layout,check}.log`. No new native or real-speaker acceptance is claimed; raw/generated Pokémon materials remain in their existing private local resource paths.

## Swords Dance / Sharpen shared self-stat slice (171 roots, 2026-10-17)

Adds only Swords Dance14 and Sharpen159 through the existing elemental catalog. Both use new source-indexed materials and shared frame/affine/linear players; Swords Dance reuses the existing elliptical battler task and Conversion's normal palette flash. Original `battle_anim_effects_2.c1461–1475` preserves affine-END, callback assignment, six linear updates (the fixed-point endpoint is −31), and deferred destruction. `battle_anim_effects_1.c4843–4878` preserves Sharpen's signed counters, increasing blink interval, frame-END condition and callback-only `SE_M_SWAGGER2` sound, extracted by the maintained importer. No new interpreter, raw-child scheduler, support list or gameplay rule was added.

A shared primary self-stat effect mapping now serves both dedicated-root selection and the general stat wave, replacing the eight numeric self-stat IDs. Attack+1/+2 use committed results and retain the requested source wave argument under clamping. Actual public-kernel tests cover all actor positions, natural learners, full/clamped/capped outcomes, last PP, malformed metadata/receipts and the Meditate boundary: its generic stat wave is supported while its absent dedicated script remains unavailable.

Full solution build passed with zero warnings/errors (74.86s). Application709/709 passed (28.97s), including20 new cases. Presentation's99-case affected cohort passed98 initially, including all171-root composition and source vectors; one new palette-alias fixture incorrectly expected a sprite during `loadspritegfx`'s source wait. After correcting only that timing assertion, all9 new Presentation cases passed (4.58s); the test-project refresh had zero warnings/errors (5.70s). Original failed log is retained. Maintained elemental importer41/41, producer parity and layout28/28 passed. Extracted original-C oracle66 full-life configurations plus14 signed/wrap probes reproduced byte-for-byte under strict UBSan. Logs: `/tmp/animation171-{build,application,presentation,refresh,presentation-refresh,layout}.log`, `/tmp/self-attack-{elemental-checks,product-producer,source-reproduction}.log`. No new native or real-speaker acceptance is claimed; the private raw/generated asset policy is unchanged.

## String Shot / Spider Web shared web slice (173 roots, 2026-10-17)

Adds only String Shot81 and Spider Web169 through the elemental catalog, with three source-indexed sheets. The three `battle_anim_bug.c250–342` callbacks reuse shared frame/affine, speed-derived linear translation, sine and blend-register presentation. Source behavior includes target/partner averaging, signed duration conversion, 51-callback wrap blinking, Spider Web's20-callback wait and alternating alpha decrements, followed by separate blend cleanup. No new scheduler, random stream, terrain input or logical-visibility subsystem is required.

Both primary-stat dispatchers now use effect/target metadata instead of a Tail Whip39 exception. The ordinary damage and primary-reduction spread paths share PP ownership/cost/clamp validation. Each successful reduction keeps its own stat wave; the dedicated root selects the first eligible pass after cap, Protect, miss, substitute or ability prevention. White Herb restoration is excluded only through the existing authenticated consumed-item group authority. The first gate caught an overly broad foreign-actor exclusion in the new root selector; it was corrected to reject forged stats while retaining real item suffixes. Spider Web uses the normalized immediate self-consistent PP→mean-look receipt, without reading final volatile state. No Domain producer or save schema changed.

Full solution build passed with zero warnings/errors (74.30s); the bounded correction refresh also passed (47.04s). Corrected Application767/767 passed (29.85s), including26 new kernel/receipt cases plus existing move/stat/spread regressions. Initial Presentation91/93 passed all173-root composition and original-C trajectories; the two failing negative fixtures confused resolver dependencies with VM commands and used a nonzero speed-derived duration. After correcting only those fixtures, all5 new Presentation cases passed (8.53s). Source runtime/vector expectations were unchanged. Maintained importer44/44, all14 producers and layout28/28 passed;216 original-C configurations/9768 rows reproduced byte-for-byte. Logs retain initial and corrected outcomes: `/tmp/animation173-{build,refresh,application,application-refresh,presentation,presentation-refresh}.log`, `/tmp/web-product-{tests,unified,layout,check}.log`. No additional native or actual-speaker acceptance is claimed.

## Mean Look / Block trapping slice (175 roots, 2026-10-17)

Adds Mean Look212 and Block335 through the existing elemental catalog and authenticated mean-look receipt path. Two source callbacks reuse frame/affine players, alpha, sine and owned audio. The eye preserves affine restart while paused, first-frame absolute scale, unpause, jitter and deferred destruction; Block preserves byte-width target-side subpriority, two bounce sounds and final blink lifetime. Callback-only sound dependencies and the XSign/HollowOrb palette mapping are extracted and checked by the maintained importer. No new receipt gate, support table, scheduler or gameplay behavior was added.

Full solution build passed with zero warnings/errors (74.37s). Presentation67/67 passed (53.02s), including all175-root composition, four new cases and affected web/scene/command checks. Application36/36 passed (5.55s), including ten new trapping cases and existing web receipts. Maintained importer47/47, unified14 producers and layout28/28 passed. Original-C40 configurations/5970 rows reproduced byte-for-byte. Logs: `/tmp/animation175-{build,presentation,application}.log`, `/tmp/trapping-product-import-tests.log`, `/tmp/trapping-unified-check.log`. These are managed/source checks; no new native or real-speaker acceptance is claimed.

## Shared self-stat, projectile and cry batch (184 roots, 2026-10-17)

Adds nine dedicated roots: Growl45, Sonic Boom49, Hyper Beam63, Meditate96, Waterfall127, Amnesia133, Bonemerang155, Hyper Voice304 and Howl336. The generated catalog remains the only move-script authority:184 unique roots, no duplicate owners. Self-stat and spread reductions use source effect metadata; projectile damage, fixed-two hits and recharge reuse existing committed receipt families. No new gameplay RNG or numeric move-support table was introduced.

Shared work includes one extracted task-affine importer, one horizontal-arc motion primitive with caller-owned lifecycle, uniform bounded visual choices, and source cry presets3/4/6/9/10 over the existing decoded sample cache. Cry tasks use actual global cry liveness and token-owned cancellation; Howl deliberately does not wait for its cry after visual completion. The view snapshots original party species under Transform and reuses the existing pre-command charge receipt replay for every battler's invocation-time visibility. Missing cry capability/identity or unknown visibility fails closed. Source-indexed Orbs retains its prior RGB fallback; new source art supplies AirWave, Amnesia and NoiseLine/HitDuplicate.

Hyper Beam's seven-valid-frame selection and reverse-cry terminal interpolation clamp are the explicit user-approved differences recorded in [original behavior improvements](original-behavior-improvements.md). Original callback vectors retain the original random operands; approved-policy tests separately cover uniform accepted counts, both rejected16-bit values, repeated rejection and stream consumption. Reverse tests separately check every valid source sample and the approved boundary rule.

Final full solution build passed with zero warnings/errors (28.03s); Audio.Tests separately built cleanly. Application210/210 passed (7.31s). Audio536/536 passed (22.05s), covering the new modes and existing normal/doubles/encounter cries, playback and pan regressions. Full Presentation initially passed883/887 (3.92min); all four failures were the same missing shared RGB_YELLOW source constant. After adding that constant and two source-color equivalence rows, the affected Presentation/shared-command/all184-root composition refresh passed65/65 (43.96s). This is a full initial run plus targeted corrected refresh, not a claimed second full run. Earlier compile/fixture corrections retained assertions and used the actual special-defense delta, public ordered-action normalization and representable singles envelopes.

Maintained Python gates: elemental52/52 (112.55s), background14/14 (41.27s), projectile8/8 (3.45s), layout28/28; all14 resource producers passed --check. Portable original-C evidence includes576 projectile configurations/13864 rows,520 Hyper Voice layouts/23920 rows,2080 DeepInhale rows,270 noise rows,28 terrain rows,144 cry-task rows and the Meditate/Amnesia lifecycle/width probes. Existing arc/elemental users passed the full Presentation run. Logs are `/tmp/animation184-{final-build,application-refresh,audio,presentation,presentation-refresh,elemental-tests,background-tests,projectile-tests,producers,layout}.log`.

Batch execution used three independent source families, one cumulative catalog generation and shared managed gates. Source staging took about23minutes; integration/generation about3minutes; build, diagnosis and corrected gates about9minutes. No per-family upload or full producer cycle was performed. No new native-window or real-speaker acceptance is claimed; Roar, delayed/residual general effects and the remaining170 dedicated roots retain their explicit boundaries.

## Primary-stat and impact callback batch (192 roots, 2026-10-17)

Adds eight dedicated roots: Leer 43, Constrict 132, Dizzy Punch 146, Knock Off 282, Tail Glow 294, Needle Arm 302, Fake Tears 313 and Frenzy Plant 338. The canonical resolver now exposes 192 unique positive move IDs, with no duplicate owners. All previous 78 elemental roots, 96 recipes, 47 subroutines and existing material bytes are preserved. The new callbacks reuse frame/affine/arc/sine, palette-bank and scene ownership authorities. BlendParticle resolves the loaded indexed palette tag to its real OBJ bank and reuses the existing palette task; no second palette model was added.

Primary stat admission uses existing normalized single/double receipts and source effect metadata, including Special Attack +2. Leer shares the existing BOTH-target primary-reduction path; Fake Tears uses its committed two-stage Special Defense reduction. Knock Off exposed a genuine shared item-secondary boundary: Sticky Hold can prevent the item effect after a successful hit. A shared prefix matcher now authenticates that late marker for both Knock Off and Thief, requiring actual HP damage/result-message ordering rather than substitute or primary-prevention receipts. Their differing source target-alive checks remain intact. Public kernel tests cover critical/noncritical zero-loss Endure, Thief target KO, White Herb, substitute, selected allies, recharge, natural move acquisition and malformed receipts.

Portable extracted-C fixtures cover 940 primary-stat sprite configurations/33720 rows, six palette configurations/349 rows and 1000 impact configurations/33760 rows. They retain source affine pause/restart and callback handoffs, signed arithmetic, blink/end timing and all four positions. The first integrated gate found a missing Duck sheet: a known graphics tag did not imply a material existed. The maintained importer now owns the pinned 16×48/0x0180 sheet and verifies its exact pixels and registration. Other first-pass failures were test assumptions: raw damage can exceed remaining HP at KO, Tail Glow's source accuracy is 100, and stepping a task directly bypasses scheduler removal. Corrections preserve the source vectors and existing assertions of state, colors and lifetime.

Final affected gates passed: Application 279/279 (7 seconds) and Presentation 140/140 (1 minute 36 seconds), including every generated root's dependency resolution and frame composition, shared task/scene/palette regressions and all new callback vectors. Full solution build passed with zero warnings/errors (76.20 seconds), then the corrected incremental solution build passed with zero warnings/errors (11.68 seconds). Maintained Python checks passed: five primary-stat checks, six impact checks (rerun after Duck material correction), all 14 producer parity checks, and layout 28/28. Final elemental parity and prior-byte preservation were independently rechecked. Logs: `/tmp/animation192-{build,refresh-build,application-final,presentation-final,primary-import,duck-check,producers,layout,duck-producer}.log`.

The batch recovered external stages after service interruptions, completed parallel staging and integrated once. From the 19:41 recovery, staging/integration took about nine minutes and builds, diagnosis and corrected gates about seven minutes. Earlier service downtime is excluded from that active interval. Audio was unchanged and its suite was not repeated; no new native-window or real-speaker acceptance is claimed. Dedicated root coverage is now 192/354, with 162 remaining; primitive reuse and general/residual effects are not counted as additional roots.

## Ground shake, fire and seed batch (199 roots, 2026-10-17)

Adds seven dedicated roots: Leech Seed 73, Earthquake 89, Rage 99, Sacred Fire 221, Magnitude 222, Pursuit 228 and Blast Burn 307. The canonical resolver exposes 199 unique positive move IDs, with no duplicate owners. Physical and elemental producers preserve previous roots, recipes and pixel bytes; only the existing source FIRE sheet is added as a material. New callbacks reuse the shared task, scene, arc, frame, affine, sound and composition authorities.

The normalized invocation cue carries committed move power and adjusted damage separately from animation turn. Earthquake/Magnitude authenticate one PP command for all-other-slot traversal, including partner Pressure, then select the first eligible typed hit. Rage reads committed Damage before HP clamping; Endure/Focus Band adjustments remain authoritative. Pursuit authenticates the adjacent interception receipt and retains catalog power40 even when interception doubles damage. Its animation turn no longer implies per-hit acknowledgement ownership. Leech Seed authenticates the adjacent self-owned setup result; later drain/removal pages do not replay the setup animation. Existing selected-hit handling covers Sacred Fire and Blast Burn without new secondary-effect exceptions.

Magnitude's source declares eight arguments but writes and branches on index15. The approved bounded-register interpretation and original-source limits are recorded in [original behavior improvements](original-behavior-improvements.md#magnitude-动画寄存器边界的安全解释2026-10-17). Legal native callback vectors are separate from safe-register policy tests; no native out-of-bounds write is executed. The remake keeps eight callback operands and one bounded16-slot script register file, with no damage or battle-RNG change.

Independent source oracles reproduce 680 fire/seed configurations and32,680 rows, plus113 horizontal-shake and864 target-shake configurations and20,361 retained rows. Five scalar predicate cases are separate from native storage. Import checks include six fire/seed cases,74 physical cases (two obsolete inventory expectations corrected and rerun), both affected producer parity checks and layout28. The JSON inventory also reconciles eight previously completed roots omitted from its192 checkpoint update; this is documentation reconciliation, not eight newly implemented roots.

Managed gate: the full Presentation run passed941/942; its sole failure was the aggregate resource fixture omitting newly required invocation power/damage inputs. The fixture now supplies explicit controlled values, while family/kernel tests retain exact committed-value and threshold assertions. The final solution build passed with zero warnings/errors in27.67seconds. The affected Application gate passed747/747 in25seconds, including37 new seven-root cases and previous dispatch, spread, multi-hit and visibility coverage. A lethal Rage fixture was corrected to execute through the existing doubles faint owner; its exported singles adapter parity remains checked, and this is not claimed as natural singles lifecycle acceptance.

Final corrected Presentation refresh passed46/46 in1minute36seconds, including all199 generated roots' dependency/frame composition, both new callback families, source vectors and register-policy tests. Combined with the unchanged941 passing rows of the full run, this closes the one fixture-input failure without rerunning unrelated Presentation cases. Logs: `/tmp/animation199-{final-build,application-final,presentation,presentation-final,fire-import,shake-import,elemental-parity,layout}.log`. No audio implementation changed, so the separate Audio suite was not repeated; no new native-window or real-speaker acceptance is claimed. Active recovery/staging/integration/gates took about21minutes from20:13 to20:34, excluding the earlier service-limit interruption; full Presentation accounted for5minutes9seconds, and the corrected focused composition pass1minute36seconds. Dedicated coverage is199/354;155 roots remain, with general and residual effects counted separately.

## BG1 overlays and impact emission batch (207 roots, 2026-10-17)

Adds eight dedicated roots: Pay Day 6, Drill Peck 65, Haze 114, Glare 137, Barrage 140, Scary Face 184, Cross Chop 238 and Mist Ball 296. The generated catalog contains 207 unique positive move IDs with no duplicate owners. All previous elemental/physical script rows, recipes and pixel bytes are preserved. Generic primary-status admission now uses source effect metadata instead of nine numeric move IDs. Haze authenticates its source-ordered stat resets and unique reset marker, including the valid neutral-field marker-only case; it does not synthesize stat-change waves.

A shared BG1 indexed overlay and hardware blend-target model preserves immediate underlying-layer selection, BG priorities, palette-bank ownership, transparent BG3 versus backdrop provenance, independent coefficient clamps and source Mist Ball brightening. Existing common OBJ alpha composition retains a tested equivalent fast path. Explicit resource dependencies own load-only graphics. The bounded overlay closure rejects simultaneous monbg ownership; scanline/window effects are not implied. Glare and the impact emitter family use the existing scene-owned slot registry and source callback/frame/OAM phases, including immediate children, lower-slot reuse, cancellation and allocation failure. Pattern shake now accepts the source omitted fourth argument, retaining its previous register, and selects the second pattern for any nonzero value.

Independent BG oracles include 99 task configurations, 180 Mist Ball configurations, 37 Glare scenarios, source material samples, 532 blend results and 15 viewport hashes. Relocated regeneration reproduces all four fixtures; only temporary compiler path provenance needed normalization, with all numerical rows unchanged. Physical callbacks reproduce 280 configurations and 11,836 rows. The affected importer checks pass seven new overlay, eight existing background and six impact-emission cases; canonical producer parity passes.

Final solution build passed with zero warnings/errors (25 seconds). New actual-kernel Application coverage passed 70/70 (5 seconds), alongside the previously green affected 250/250 (22 seconds). First affected Presentation run passed 152/156 (53 seconds); four failures exposed the shared omitted-pattern-argument restriction. After its source-backed correction, the final Overlay/Sliding/resource refresh passed 36/36 (1 minute 8 seconds), including every one of the 207 roots' dependency resolution and frame composition. Existing unchanged passing cases were retained rather than rerunning the full Presentation suite. The added pattern oracle covers eight configurations/160 callbacks and sixteen explicit/omitted managed comparisons. Initial Application failures were fixture setup errors (already-capped Scary Face target, KO without the public turn owner, and an unconstructible invalid slot); corrected tests retain actual turn ownership and the original outcome assertions.

Logs: `/tmp/animation207-{core-build,layer-tests,existing-application,build,presentation-build,presentation,app-refresh-build,application-final,final-build,presentation-final}.log`. This batch adds no audio implementation, so Audio tests were not repeated; no new native-window or real-speaker acceptance is claimed. Dedicated coverage is 207/354, with 147 remaining; general/residual effects and primitive reuse are counted separately.

## Weather and orbital-player batch (215/354)

Eight complete dedicated roots are added together: Sandstorm 201, Hidden Power 237, Rain Dance 240, Sunny Day 241, Heat Wave 257, Astonish 310, Sky Uppercut 327 and Magical Leaf 345. Canonical discovery adds exactly these eight and retains all 207 prior roots. Field-setting admission is selected from move-effect metadata and an exact adjacent, actor-owned PP→weather marker; suppression/Forecast and later weather receipts do not become replacement animation evidence.

The shared non-affine subsprite table preserves native top-left offsets, flip reflection, OAM piece order, per-piece priority, coordinate wrapping and retained palettes without creating logical sprites. New source pixel hashes independently check the composed 240×112 field. The sand closure validates source 64-entry capacity conservatively. Weather callbacks reuse existing BG1 overlays, BG3, palette banks, frame/affine and raw-scene scheduling. Rain raw children retain their own invocation materials after visual completion. The raw signed-pan command reuses the existing sweep without source-side adjustment. Magical Leaf keeps the original shared indexed leaf assets rather than duplicating physical-pack art.

Physical source vectors cover 1,005 configurations / 27,569 rows. Weather portable C vectors cover task/register/sprite/affine/palette/pan and native OAM phases, with independent material hashes. Callback vectors, script scheduler timing and pixel sampling remain separately asserted.

Validation uses affected suites rather than a full Presentation rerun: the cumulative solution build has zero warnings/errors; all 83 new Application cases pass. The initial affected Presentation run passed 120 of 122 cases, including all 215 generated roots composing every frame. The two failed expectations assumed ten-frame crescent spacing. Extracted original Cmd_delay/WaitAnimFrameCount scheduling proves twelve-frame spacing, exact births and a peak of four logical crescents/eight OAM pieces. Only those fixture expectations, their source schedule evidence and explanatory comments changed; the conservative production capacity guard and callback vectors did not change. The final eight-case weather refresh checks every authored birth and OAM count across both sides. Early unchanged field-setting regression also passed Hail 8 and existing physical-dispatch 172 cases.

Source evidence includes physical 1,005 configurations / 27,569 rows, weather 44,073 independent structural assertions and six reproducible fixtures, and 36 native subsprite field hashes against the compositor. Narrow importer checks passed 6 physical plus 15 weather/overlay; physical/elemental/background producers match regenerated resources. The new checks are registered in the maintained ResourceImport runner. No audio backend/PCM or new native-window claim is made for this batch. Coverage is 215/354, with 139 remaining.

Logs: `/tmp/animation215-{application-build,build,application,presentation,final-build,weather-refresh}.log`; early shared-command/subsprite and field-admission checks are `/tmp/animation215-{pan,subsprite,app-core,field-core}-*.log`. Original source observations remain separate from the fixture-only schedule correction.

Reproduction commands (after `source scripts/env.sh` and the maintained resource rebuild):

```sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~WeatherSetupAnimationKernelTests|FullyQualifiedName~SelectedWeatherHitAnimationKernelTests|FullyQualifiedName~HeatWaveAnimationKernelTests'
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~OrbitalParticleAnimationTests|FullyQualifiedName~OrbitalSourceVectorTests|FullyQualifiedName~WeatherAnimationTests|FullyQualifiedName~WeatherSourceOracleTests|FullyQualifiedName~SubspriteCompositionTests|FullyQualifiedName~AdjustedPanCommandTests|FullyQualifiedName~TaskAffineAnimationTests|FullyQualifiedName~SlidingBackgroundTests|FullyQualifiedName~SlidingEffectTests|FullyQualifiedName~IndexedSpritePaletteTests|FullyQualifiedName~SharedSpriteSceneTests|FullyQualifiedName~SpriteOamPhaseTests|FullyQualifiedName~OverlayCompositionTests|FullyQualifiedName~SourceSoundPlayerPolicyTests|FullyQualifiedName~SourceUnpannedSoundTests|FullyQualifiedName~SourceAnimationResourceTests'
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~WeatherAnimationTests'
```

## Reaction and recovery batch (223/354)

Eight complete roots added: Aurora Beam 62, Rest 156, Tri Attack 161, Morning Sun 234, Moonlight 236, Facade 263, SmellingSalt 265 and Leaf Blade 348. Canonical catalog coverage is 223/354, with 131 remaining.

Rest uses the shared result-boundary dispatcher: its authenticated PP → rest status (source counter 3) → positive recovery prefix launches before the HP result, after the sleep message. Later Lum/Chesto restoration cannot erase that earlier cue. Ordinary recovery is selected by effect metadata and consumes committed HP amounts without recalculating weather. Indexed background writes share sparse faded-palette entries, with normal fades, inversion, resource replacement and cancellation preserving ownership. Moonlight does not admit unreviewed monbg combinations.

Physical callbacks retain source affine handoffs, raw child ownership and local visual RNG. The source harness covers 620 scenarios / 33,940 rows, with 138 maintained configurations / 8,226 rows. Recovery evidence includes 48 motion configurations / 1,950 rows and 28 task/palette configurations / 2,632 rows, schedules and independent pixel samples. Extracted fixtures reproduce at O0/O2. Existing material records remain unchanged. Endeavor remains outside this slice because its shake task reads retained move power not yet authenticated by the invocation context.

Validation: cumulative solution build zero warnings/errors; affected Presentation 57/57 plus recovery source/lifecycle 10/10; canonical resource tests 6/6 including composition of every frame of all 223 roots; new kernel cases 81/81; existing recovery/dispatch regression 200/200. Narrow importer checks passed 6 physical and 7 recovery, with changed producer parity. One immunity fixture needed a target Speed-stage correction so its claimed first actor really moved first; production behavior and source vectors were unchanged. No full Presentation, new native-window or audio parity claim. Logs: `/tmp/animation223-{build,final-build,presentation,resources,recovery-final,application-final,recovery-regression}.log`.

## Friendship, contact and water/root batch (231/354)

Eight complete dedicated roots added: Lick 122, Poison Gas 139, Struggle 165, Return 216, Frustration 218, Safeguard 219, Ingrain 275 and Hydro Cannon 308. Canonical catalog union is 231/354, with 123 remaining; every prior root is retained.

Return/Frustration consume the detached event-time actor friendship byte, including the original actor under Transform, and preserve all source branch thresholds. Forced Struggle authenticates the synthetic -1 slot and zero PP while retaining the nominal Pressure command cost. Safeguard reuses field-setting ownership; Ingrain consumes its adjacent rooted receipt without replaying later residual recovery. Trace sprites are uncounted children owned by their task, share real scene capacity and are admitted only in reviewed visible/non-affine callback closures. No new numeric support table or gameplay RNG path was added.

Physical evidence covers 480 extracted-C configurations / 17,420 rows plus all 256 friendship inputs for both predicates. Water/root evidence covers 113 configurations / 8,813 rows, with 110 geometrically reachable configurations / 8,360 rows consumed by managed tests. The source harness now records raw OAM matrix bits as well as its unchanged base flip flags: the native BeginAnim writes effective non-affine flips into OAM bits 3/4. Removing that added observation reconstructs the prior fixture exactly; this correction does not change runtime behavior or original callback rows. Both source families reproduce independently; the water/root generator verifies O0/O2 parity.

New actual-kernel tests pass 40/40 and affected preexisting dispatch/recovery tests pass 253/253. Narrow import checks pass six physical plus six water/root, with six previous physical importer cases and both changed producer parity checks. The first affected Presentation run passed 72/74, including every frame of all 231 roots; the remaining failures were a cancellation fixture missing its battler material and the source observer's base-versus-OAM flip distinction. Earlier fixture corrections supplied the audio pan recorder and compared the eight native argument words separately from the unchanged extended register bank. No native window, full Presentation suite or real-speaker parity is claimed.

The initial Godot compiler exited 137 twice while the shared machine had low available memory. Old inactive evidence directories were preserved on disk with their original paths linked, releasing approximately 1.15 GiB of tmpfs memory. Godot then built with zero warnings/errors. This is an execution-resource correction, with no disabled analyzers or weakened validation. Logs: `/tmp/animation231-{core-regression,kernel,presentation,godot-memory-refresh,final-build}.log`.

Final refresh: the full solution builds with zero warnings/errors (11.78 seconds); all 33 new Presentation rows pass (9 seconds), including the corrected source OAM observation and every-tick cancellation. The unchanged 41 affected/resource cases passed in the prior 74-case run, including all-root composition. Final log: `/tmp/animation231-presentation-final.log`. This cumulative evidence does not relabel the earlier failed run as green.

## Selected status and rock batch (239/354)

Eight complete roots added: Disable 50, Confuse Ray 109, Lovely Kiss 142, Sweet Kiss 186, Foresight 193, Detect 197, Ancient Power 246 and Rock Tomb 317. The canonical catalog adds exactly these eight and preserves every previous root; 115 dedicated roots remain. The old 75 physical and 105 elemental script rows and recipes retain their values.

The shared invocation matcher consumes typed Disable, confusion and identified receipts through the existing single/double adapter. Protect/Endure require both the successful attempt and its adjacent owned state marker. Ancient Power's five ordered stat outcomes share one secondary wave, including mixed capped/raised groups; capped source messages remain visible. No battle RNG, damage or status duration is recalculated by presentation.

Renderer additions reuse frame/affine/linear/sine players, scene-owned sprites, existing grayscale/alpha controls and the normal palette-fade owner. Confuse Ray adds the registered Ghost background and custom pan variable; its later plain PlaySE preserves the song's panning instead of applying a new pan command. Lovely Kiss consumes four animation-local RNG draws in source sprite-slot order. A generic pointer-table parser correction retains a valid final entry without a trailing comma. Detect/Disable reuse the existing wall sparkle material; an initial duplicate indexed representation was removed after the strict resolver detected its mismatch with the byte-identical RGB owner. Resolver equality was not relaxed.

Validation: final cumulative solution build zero warnings/errors (30 seconds); affected Presentation 93/93 (28 seconds), including all 21 new family cases, source vectors, full-root direction/ally composition, wall reuse, palette and sound-policy regressions; new actual-kernel cases 56/56 (5 seconds); existing affected dispatch/stat cases 293/293; opt-in audio panning 18/18. Portable native fixtures cover 580 physical configurations / 24,880 rows and 212 elemental configurations / 16,673 rows; independent reproduction and elemental O0/O2 parity pass. Narrow import checks pass 5 physical, 13 elemental and 8 background, plus changed-producer parity. The cancellation fixture now asserts the existing ended-controller rejection, and asset-set fixtures include the compositor's shared impact dependency. No full Presentation, all-move visual campaign, new native-window or real-speaker claim is added. Logs: `/tmp/animation239-{final-build,presentation-final,kernel,core-regression,audio-panning,physical-material-tests,background-import-tests}.log`; elemental import log: `/tmp/confusion-kiss-animation-stage/scripts/tests/maintainable-check.log`.

## Flail and selected control scripts — 244 roots

Adds Flail175, Attract213, Torment259, Follow Me266 and Taunt269. Catalog discovery adds exactly these five IDs and removes none. Flail consumes the dynamic-power command's already prepared base-power receipt; HP/substitute export preserves it without extra events or RNG draws. Attract retains successful setup through a later Mental Herb clear, and Follow Me retains the real singles no-redirection result. Torment uses the existing scene's raw allocation/ownership, actual successful-child counter and bounded global template scan. Attract reuses indexed BG1 overlay rendering with the source clear/reset phase separation.

Solution build passed with zero warnings/errors. Affected Presentation passed96/96 (53 seconds), including16 new cases; Application passed211/211 (20 seconds), including30 new root cases and4 producer regressions. The producer regressions also exercise Return/Frustration/Low Kick/Eruption, exact RNG order, both Eruption target passes and HP/substitute receipt export without claiming new animation roots. Narrow import checks passed5 physical and10 elemental/background, plus all three changed producer parity checks. The two initial test failures were fixture premises: integrated hearts must use source actor-relative subpriority, and Follow Me is forbidden to Metronome. Both were corrected without changing runtime or original-C observations.

Portable source fixtures cover1,040 power/guard configurations (42,180 rows) and174 control/heart configurations (20,158 rows); reproduction and elemental O0/O2/sanitizer checks pass. The numeric power/guard fixture includes three callback prototypes whose missing-art roots remain unadmitted. Reversal179, Protect182 and Shadow Ball247 remain external pending permission to supplement four fixed-source material files; they are not included in244. No new native-window, real-speaker, full Presentation or all-root frame-composition claim is made. Logs: `/tmp/animation244-{build,presentation-final,application-final,flail-import,social-import-tests,elemental-parity,background-parity}.log`; prepared-power producer cases: `/tmp/animation247-prepared-power-tests.log`.


## Cut, wall and support-status scripts — 250 roots

Adds Fury Cutter210, Twister239, Helping Hand270, Brick Break280, Imprison286 and Teeter Dance298. The three maintained producers discover exactly these six roots using existing local materials. Fury Cutter carries the damage command’s already prepared counter through HP/substitute receipts, including the capped fifth hit and later contact-faint reset. Brick Break authenticates ordered opposing-side screen removal before the hit, preserving the source animation against Ghost immunity when screens were removed. Helping Hand uses the real partner receipt; Imprison preserves its extra Pressure PP ownership; Teeter Dance starts once at the first successful ordered confusion result, without letting failures on other targets veto that invocation.

The existing frame/affine/linear/sine and scene-owned raw sprite authorities are reused. In-place shake now applies source signed16 doubling and restores the corresponding truncated half; actor/target selectors retain present hidden battlers while partner selectors require visibility. Original-C fixtures cover1,580 cut/wall configurations /66,092 rows plus256 Fury predicate inputs, and228 support configurations /16,604 rows. Fixtures reproduce independently; support O0/O2 and sanitizer checks agree. No new downloads, battle RNG draws or gameplay arithmetic are introduced.

Final solution build passed with zero warnings/errors (37.62 seconds). Affected Presentation passed156/156 (25.23 seconds), including28 new family cases; Application passed283/283 (23.29 seconds), including72 new kernel cases. Focused importer checks passed6 cut/wall and10 support cases, plus changed-producer parity. Three stale literal inventory assertions were refreshed to current canonical discovery without weakening assertions; only their targeted checks were rerun. Initial failures were resolved by preserving natural Brick Break alpha12/8 at end (Cancel still clears it), validating the third Teeter note’s exact41-frame source command, and routing the Rough Skin KO fixture through the public ordered-turn owner. Original source vectors were unchanged.

Reversal179, Protect182 and Shadow Ball247 remain outside the admitted count pending material-download permission. No full Presentation, all-root frame-composition, native-window or audio-output claim is added. Logs: `/tmp/animation250-{final-build,presentation-final,application-final,physical-import,physical-maintained,support-import,support-refresh,support-preservation}.log`.

## Retained attacks, caller preludes and visibility roots — 258 roots

Adds Thrash37, Bide117, Metronome118, Faint Attack185, Sleep Talk214, Camouflage293, Odor Sleuth316 and Calm Mind347. Existing source art is reused; no download or new pixel payload is required. Caller roots cover their own authored preludes, not arbitrary called-leaf playback. Thrash authenticates no-PP retained hits; Bide separately authenticates setup and release, including friendly stored-damage sources.

Shared presentation now separates raw x/y from x2/y2 and battler presence from logical/sprite/OAM invisibility. Source background copies remain independent of hidden OBJ state. Camouflage consumes the committed native terrain enum; grouped stat result selection uses current effect metadata, including Calm Mind's ordered SpAtk/SpDef outcomes and capped-message silence. No combat RNG, damage, target selection or save schema is changed.

Validation: cumulative solution build passed with zero warnings/errors (73.53 seconds), followed by a zero-warning refresh (30.69 seconds). Affected Presentation first passed201/203 in53.97 seconds; old Teeter fixtures were corrected to assert independent raw/x2 contributions and supply the synthetic monbg clear command. The21-case refresh passed20, then the final11-case SupportStatus run passed11/11 (4.02 seconds), leaving every selected case green. Application first passed548/556 in11.88 seconds, then the112-case affected refresh passed112/112 in5.44 seconds. Fixes preserved malformed-envelope checks at their representable adapter boundary, asserted actual Protect failure receipts and valid ordered targets, and strengthened no-PP cue-sequence coherence. No source vectors were changed to accommodate these fixtures.

Portable original-C coverage totals464 configurations/18,550 retained rows across caller, retained and visibility families. The visibility oracle also matched O0/O2 and ASan/UBSan with leak detection disabled for the container's ptrace restriction. Focused import gates passed7 caller,5 retained and10 visibility checks, plus2 updated legacy inventories; changed physical producer parity passed. All115 old elemental roots,18 old background roots,83 old physical roots and prior material bytes are preserved. General source visibility, OAM, monbg, shared-scene, affine, form-change, voice and Hail regressions were included. No full Presentation, all-root frame-composition, native-window or audio-output acceptance is claimed.

Reversal179, Protect182 and Shadow Ball247 remain outside the count pending material-download permission. Logs: `/tmp/animation258-{build,refresh-build,presentation,application,presentation-refresh,application-refresh,final-support,caller-import,visibility-import,visibility-inventory,physical-parity,preservation}.log`.

## Charge phases and battler-copy scripts — 264 roots

Adds Razor Wind13, Skull Bash130, Night Shade101, Mimic102, Double Team104 and Psycho Boost354. All six use existing local source materials; no download or new pixel payload was needed. The generated catalog and maintained inventory agree on all264 identities, leaving90 without dedicated scripts.

Charge setup owns the PP→charging-result boundary without an attack string; release authenticates its actual released marker and zero-consumption PP, including final-PP and spread-target cases. A following actor's headerless setup no longer leaks into the earlier invocation. Mimic consumes its committed temporary-copy receipt; the renderer does not select or copy gameplay moves. Psycho Boost's committed self Special Attack reduction reuses the shared stat-wave authority.

The shared renderer now supports static monbg copies without hiding OBJ, whole battler-background-plane enable, and retained affine state on program changes. Double Team's bounded two-copy closure uses scene-owned raw slots and detached uniform palette blending. Source UBFIX zero-axis affine behavior is explicitly recorded in original-behavior-improvements.md; nonzero arithmetic and gameplay RNG are unchanged.

Solution build and refresh passed with zero warnings/errors (82.5s and55.5s). Affected Application passed500/500 (22s), including93 new charge/copy kernel cases and the shared secondary-stat additions. Initial affected Presentation passed158/161 (62s). After correcting two source-vector fixture premises and routing battler-copy material through the existing battler composer, the affected26/26 refresh passed (18s); all161 distinct selected cases are green. The native fixtures retain6,670 original-C rows across the three family suites, with optimized/unoptimized reproduction and sanitizer checks as documented by their source audit files. Changed importer checks passed5charge +5copy +4PsychoBoost; three affected legacy inventory/tag checks and producer parity passed. This is not a full Presentation, per-move native-window, or audio-output claim.

Reversal179, Protect182 and Shadow Ball247 remain outside the count pending material-download permission. Logs: `/tmp/animation264-{build,refresh-build,presentation,presentation-refresh,application,resolver,background-inventory}.log`.

## Expulsion and contributor animations — 268 roots

Adds Whirlwind18, Roar46, Teleport100 and Beat Up251. All four use existing local source materials; the generated catalog and inventory agree on268 dedicated identities, leaving86. No pending download was attempted.

The expulsion roots reuse the shared battler geometry/affine and source cry authorities. Exact source callback vectors cover slide direction/participant visibility, Teleport's u8 affine duration and terminal position, Whirlwind frame seeking, and the two-phase Roar cry. Beat Up uses committed contributor/hit pairs and their executed ordinal, including gaps, genuine owned eggs, a single-member wild opponent, Substitute, KO and Endure. Opposing multi-member action-only fixtures are recipe evidence, not fabricated trainer acquisition provenance.

A shared completed-pose handoff prevents departing battlers reappearing on silent PP/force-out pages. It retains only clean, detached battler geometry/visibility/affine while live scene residuals and OAM ordering continue. Same-member Natural Cure bookkeeping preserves the pose; actual replacement entry, a new animation, cancellation or battle exit releases it. Singles Substitute material comes from the shared event-time display-body projection. Teleport's existing skipped terminal-result page still omits the source fled text; this remains a presentation gap, separate from its dedicated script coverage.

Validation: default build and final refresh passed with zero warnings/errors; affected Application74/74, Presentation104/104 (including generated-only resolver and scene residual regressions), explicit Audio51/51. Focused import checks: expulsion7/7, Beat Up3/3 and physical regressions3/3; both changed producers pass parity. Both original-C fixtures reproduce byte-for-byte from published generators. Initial failures were invalid test eggs/actor targeting, cry scheduler timing and nullable test code; corrected fixtures retain the original behavior assertions. No full Presentation sweep, new native UI run or real-speaker parity is claimed. Logs: `/tmp/animation268-{build,final-build,application-final,presentation-final,audio-final,import,oracles}.log`.

Reversal179, Protect182 and Shadow Ball247 remain outside the count pending the previously requested fixed-input permission. Further missing inputs are consolidated for a separate decision; no broader download authority is inferred.

## Shared palette snapshots and rolling/feather callbacks — 272 roots

Adds Nightmare171, Rollout205, Feather Dance297 and Overheat315 from local pinned inputs. The catalog contains exactly272 dedicated identities, leaving82. No pending download was attempted. Mind Reader/Lock-On/Ice Ball and the scanline families remain outside this batch; their missing inputs are not counted as completion.

Overheat preserves separate normal/faded palette buffers using immutable same-bank transforms, including sequential RGB555 rounding, initialized backup ownership, restore timing, reload and cancellation. Its Small Ember material now has one canonical indexed owner; the physical pack references that owner instead of repeating an RGB-only declaration. Every old RGB pixel is retained, with regressions for Blaze Kick299 and Dragon Claw337. Source `setalpha 12,18` retains the five-bit register value; shared hardware composition saturates coefficients at16. Nightmare uses the existing target material and shared raw-sprite registry. Feather Dance preserves the source base-coordinate selector, native-width falling accumulator and manually written affine matrix.

Rollout authenticates the owned postdecrement counter, including its final1→0 turn and retained attack-string/no-PP invocation. Its existing calculated power is passed through the existing damage receipt field; gameplay calculation and RNG are unchanged. Genuine Endure/Focus Band adjustments, Substitute, failure/reset and all participant layouts have bounded kernel coverage.

Validation: solution build85.40s and bounded refreshes18.38s/31.87s passed with zero warnings/errors. Application initial170/171 plus corrected Rollout6/6 establishes171 distinct affected passing cases. Presentation initial179/189, focused refresh11/16 and final31/31 establish200 distinct affected passing cases with no unresolved failures, including every generated root's composition and generated-only resolution. Failures exposed a real duplicate material declaration and the shared alpha validation limit; fixture corrections retained assertions while fixing the queue owner, mandatory impact dependency, indexed tag binding and source ally panning. No full Presentation sweep, new audio-engine run or native UI claim.

Four source-C fixtures reproduce byte-for-byte: Nightmare36cases/2,052rows; Rollout100cases/7,200task frames plus10,080particle/2,120sound rows and65,536counter combinations; Feather Dance960cases/120,480rows; Overheat11,232flame rows,216palette rows and32,768exhaustive colors. New import checks17/17, existing elemental/physical regeneration checks1/1 each and both changed producer parity checks pass. Stale exact inventory expectations were reconciled for previously completed expulsion18/46/100 and Razor Wind13/Skull Bash130/Beat Up251; no prior root was newly counted. Logs: `/tmp/animation272-{build,refresh-build,final-build,application,application-refresh,presentation,presentation-refresh,presentation-final,physical-check,elemental-check}.log`. No physical/elemental full-suite claim is implied by focused checks.

## Additional monster silhouette — 273 roots

Role Play272 is the only newly discovered root; the generated catalog now contains273 identities, leaving81. It uses local species art, including source Deoxys frame0 converted through the existing hit-art producer. Its task preserves source alpha cadence, nine affine updates, OAM-before-task destruction and palette ownership. Source party-side identity selection and Unown bitmap/base-coordinate separation are explicit ephemeral inputs. Same-ability copies, Substitute and protection behavior follow committed source receipts; failures/called leaves do not invent playback.

The extracted native oracle covers84 identity/face/Transform configurations and3,444 rows with O0/O2 equality and byte-identical product-path regeneration. Focused Presentation26/26 and Application24/24 passed; the latter includes genuine public Transform→same-side Role Play receipts. The initial Application run was23/24 because a negative fixture expected a canonical Source field to survive the older singles envelope, which does not contain it. That assertion now remains on canonical doubles; runtime validation was not loosened. Source-form pixels65,536/eight geometry rows, import7/7, maintained hit/explosion16/16 and both changed producer parity checks passed. Solution build84s and Application refresh8.39s had zero warnings/errors. Evidence: `/tmp/animation273-{build,refresh-build,application,application-refresh,presentation}.log`. The all-root fixture was adapted for the new explicit material input but no full all-root or full Presentation rerun is claimed. No native-window/audio-output claim or download was added.

## Local-only window and prepared hit — 278 roots

Adds Fake Out252 and Focus Punch264 using only already-local pinned inputs and existing art. The canonical resolver contains278 unique dedicated identities, leaving76. Focus Punch covers its successful hit, not the separate pre-turn general setup. Its affine-END/shake lifecycle reuses the shared sprite players; public ordered turns prove both physical/special interruption and genuine Substitute, critical, miss, protection and immunity outcomes.

Fake Out adds a bounded shared WIN0/color-effect state. Its original task and VBlank vectors prove the closing rectangle, palette flash and cleanup. Brightness operates on actual frontmost layer provenance, preserving equal-colored backdrop and occluding objects/battler backgrounds. Existing alpha uses the same window CLR permission. Inverted/wrapping bounds, layer suppression and unreviewed concurrent register owners fail closed; this does not claim all window hardware effects. Source callback vectors contain30 task and28 latched frame rows; independent raster tests cover clipping, empty rectangles, five-bit coefficient saturation, rounding and layer ownership. Focus Punch contributes51 native callback/affine rows; the extracted shared Fang harness still reproduces its existing fixture unchanged.

Elemental metadata discovery is now reusable without generating pixels. The background producer defers to that discovered ownership, fixing a regeneration overlap introduced when Teleport's callback became available; no copied move-ID exclusion table or manual generated-row filtering was added. Existing generated rows/materials are unchanged except the two new roots and one existing-art fist recipe.

Validation: solution build85.33s, bounded test-project refreshes8.82s/9.89s/8.35s, all zero warnings/errors. Final Presentation66/66 (17.78s) and Application20/20 (4.67s) passed. Initial failures were test fixtures: opponent literal63 panning negates to−63; Fake Out requires an ordered flinch queue; the immune queued Gastly needed a legal self-target move (natural Destiny Bond) rather than Lick. Runtime/golden assertions were not weakened. Focus import6/6, window import3/3, background suite15/15 and physical regeneration/inventory1/1 passed; the initial mistyped unittest method lookup is retained as a failed command, not counted. Logs: `/tmp/animation278-{build,application-verified,presentation-final,focus-import,fakeout-import,background-import,physical-inventory-verified}.log`. No full Presentation, full application, native-window, audio-output or download claim is added.

## Round-shadow charge, orb-defense and restored local inputs — 286 roots

Adds Fly19, Reversal179, Protect182, Shadow Ball247, Stockpile254, Swallow256, Dive291 and Bounce340. Existing PowerGuard callbacks close three roots using restored pinned local inputs; OrbDefense and RoundShadowCharge reuse the canonical resolver, shared sprite/affine players and scene-owned lifetimes. No duplicate support-ID table or gameplay RNG is introduced.

Stockpile authenticates the owned increment; Swallow authenticates the exact consumed stockpile count and positive recovery, preserving the committed count for animation context. Empty/full-HP failures do not borrow a successful root. Charge setup and release have separate PP/marker owners. Successful release callbacks restore their sprite; failed/status-cancelled release retains only its owned visibility through the actual clear batch's ACK. Sleep clears at public end-turn preparation rather than the earlier blocked-action page. The protocol has no dedicated native MOVEEND visibility receipt, so this conservative batch boundary remains an explicit presentation timing limit.

Dive kernel fixtures use a source-HM08-compatible controlled Wailmer entrant. The campaign intentionally does not admit HM08 and has no natural level-up/egg Dive learner; these tests establish committed rule/animation behavior, not a new acquisition route. No machine/item admission changed.

Original-C oracles retain 440 OrbDefense configurations / 18,210 rows and 210 RoundShadowCharge configurations / 15,050 rows with O0/O2 equality. Generated evidence is local and ignored under the shared governance; generators and explicit comparison tests are tracked. Source END parks its command index before the marker while the managed player parks on END; tests compare the explicit encoding without changing source vectors. Focused import checks OrbDefense6, Charge6 and physical inventory1 pass, and changed producer parity passes.

Validation: refreshed solution build passed with zero warnings/errors (85.16 seconds); affected Application182/182 (7.5481 seconds), Presentation116/116 (35.7075 seconds). Initial failures exposed fixture HM availability, actual late sleep-clear ownership, resolver shared-impact dependency and equivalent affine-END register encoding; all corrected assertions retain genuine source values. No new native UI or real-speaker claim. Logs: `/tmp/animation286-{refresh-build,application-final,presentation-final}.log`.

## Charged roots, party cures and psychic utilities — 294 roots

Adds Sky Attack143, Spit Up255, Heal Bell215, Refresh287, Aromatherapy312, Kinesis134, Psych Up244 and Luster Purge295. Admission continues to use canonical catalog roots and committed effect receipts. Sky Attack reuses charge ownership; Spit Up authenticates the consumed 1–3 stockpile count without multiplying the source move-power register. Party cures retain Soundproof/healthy-party source behavior, while Refresh requires its actual cure; common immunity, held-item and active-party synchronization suffixes are authenticated. Active toxic counters are compared through the existing persistent-status projection. Psych Up accepts the ordered committed stage copy, including an unchanged-stage copy, without rereading a final frame.

Shared presentation additions are a source OBJ-window union mask for BG1, native frame-loop extraction, selected three-bank sprite-palette groups, source unload ownership, and bounded seven-bit simple palette selection. Window sprites retain their captured battler geometry/material and never paint normal OBJ color. The canonical Impact material now preserves native 4bpp indices and its palette; its existing RGB555 bytes and other hit/art assets are unchanged. Basic hits and Luster Purge resolve the same material. The new excluding-battler palette-cycle task respects the native 6-bit delay counter and 4-bit step limits.

Maintained original-C generators exercise complete representative callback traces and boundaries; generated expected data remains local and ignored. A psychic palette-harness macro scope error discovered during integration was corrected in the extractor and independently regenerated before managed comparison; the superseded oracle is not parity evidence. No new native UI, natural acquisition or real-speaker claim is made.

The affected sweep also caught an obsolete round-shadow family sound guard intercepting the older Explosion script. Its duplicate load/sound cases were removed so the existing shared metadata validator and Explosion command authority handle their respective commands; no additional sound-ID exception was introduced.

Validation: solution refresh passed with zero warnings/errors (31.74 seconds, following the 53.20-second integrated build). Affected Application128/128 passed in6.6699 seconds. The107-row Presentation run passed106 and exposed the obsolete sound guard; the final affected25/25 refresh, including every generated root's every-frame compositor, passed in1.6093 minutes. Together these cover the107 affected rows after the correction. Maintained importer entrypoints4/4 passed in42.1023 seconds (charged7, cure8, psychic4 source checks, plus28 hit/explosion checks); changed producers passed exact regeneration. Generated catalog has294 unique positive roots, no duplicates, and Git contains no generated vector outputs. Logs: `/tmp/animation294-{final-refresh-build,application-final,presentation-final,presentation-refresh,import-gate}.log`.

## 2026-10-18：302根有界批次

新增Solar Beam76、Guillotine12、Petal Dance80、Ice Ball301、Yawn281、Skill Swap285、Grudge288、Spikes191。共享原始帧／仿射／线性玩家与场景对象所有权；新增三选背景操作按目标方选择前两项，第三项为未使用的Contest资源。Solar Beam分别证明普通蓄力／释放、晴天提前PP与继承蓄力回执；不从最终天气猜测调用。Utility结果验证使用原有逐事件回执，Spikes允许经验证的双打Pressure耗PP，Skill Swap保留双方覆盖字段。

同一串行编译后Application160／160通过（8.36秒），受影响Presentation39／39通过（27.92秒），包含新根完整生命周期、原始C逐帧比较、共享场景／OAM及规范资源解析。Solar代表25场景与按需全量225场景使用同一比较器，全量1／1通过（4.66秒）。ResourceImport5／5通过（54.84秒），另有背景导入8／8。初次Presentation六处失败均为Utility测试的初始地形调色板、空背景ID及隐藏槽分配前提；修正夹具后全部原语义断言保留。生成期望仅保存在忽略目录，普通测试不运行C，缺失数据明确失败并给生成命令。未运行本批原生UI或真实声音验收。

## 2026-10-18：309根共享批次

新增Mind Reader170、Lock-On199、Snore173、Uproar253、Will-O-Wisp261、Charge268、Shock Wave351。共享sure-hit命令所有权、已有普通伤害／状态分派与保留攻击协议；Uproar首次被调用的leaf与后续真实保留攻击分开。源setcharge无条件成功，既有typed命令回执现在保留2→2成功；不再由仅PP或最终状态猜测。原有计时、伤害、战斗RNG不变。

渲染复用共享任务仿射、帧解释器、raw sprite场景、未淡化／淡化palette bank。LoadPalette范围写入与BlendPalette范围操作共用小型helper；ThinRing增加真实indexed数据，原RGB保持逐字不变。monbg／clearmonbg按源byte语义接受数值，0／2选actor、其他选target；普通位置helper补齐源signed16回绕。容量检查限制本组已审阅闭合，覆盖重复task／orb、过量对象、其他所有者占槽及原生分配失败，不引入替代清理。

最终编译0警告／错误；Application157／157（7.74秒）、受影响Presentation89／89（36.61秒）、ResourceImport2／2（2.24秒，含11个Python检查）通过，三个修改producer的--check通过。原C代表证据FocusSound194场景／10,430帧，ElectricWisp78场景／6,412帧，逐帧几何、矩阵、palette、音效及清理继续由同一比较器验证。Electric原生harness补上原CreateSpriteAt的首tile初始化；Focus原生harness补真实idle battler模式／slot初始化，保留旧文件作本地对照；没有据实现改写源期望。首次失败中的8源参数对16内部寄存器、normal对double affine前提已在夹具中纠正；十六进制信号解析与Snore未使用额外参数准入由真实脚本触发修正。所有生成期望仍仅在忽略目录；没有本批原生UI或真实扬声器验收。

同一FocusSound比较器另对按需全量1,092场景／54,148帧／89,480 sprite行通过（1／1，3.00秒）。常规运行仍使用本地预生成代表数据，不暗中编译C。

## 2026-10-18：321根扫描线共享批次

315的Destiny Bond／Perish Song／Curse／Trick／Recycle／Silver Wind验证见[分派架构](animation-dispatch-architecture.md)。本批新增Surf57、Muddy Water330、Dig91、Acid Armor151、Sketch166、Dragon Dance349；规范目录共321根，余33根。Sketch复用效果驱动的复制回执验证，保留永久复制的base PP与PP Ups；Dragon Dance复用成对自提升结果，Acid Armor的White Herb后缀由既有完整物品回执认证，不混作第二次招式结果。

共享场景保留两个960字扫描线buffer及翻转索引，支持BG1／BG2水平或成对偏移、逐行alpha和显式512×256地图。原图块先采样再裁剪；首行dirty GPU写入、行1–159、终止传输、停止恢复和跨调用取消均有独立测试。帧阶段模型及160／161硬件周期不确定边界见[背景说明](source-animation-backgrounds.md)，不宣称周期级原版一致。Surf终止后的局部调色板值保留在旧快照，新调用独立加载；Dig玩家地面线在视口下缘，像素中性由几何推导，对手侧另有逐像素遮罩验证。

完整解决方案构建0警告／错误（72.61秒）；Application240／240（7.60秒），首轮受影响Presentation151／152含全部321根每帧组合通过，修正唯一Dig裁剪前提后定向53／53（22.33秒）；额外测试加入两个对手遮罩用例。ResourceImport2／2（0.72秒，8个Python检查），两个修改producer精确再生通过。独立源证据：scanline状态58场景／444操作，Surf24场景／3272帧，四消费者83场景／6993帧；原C O0／O2一致，生成期望仍仅本地忽略。旧315根、335素材及49背景条目／基础palette保持一致。未开展本批原生UI或真实声音验收。


## 327 根：背景运动、仿射复制与调用前奏（2026-10-18）

新增地裂、地球上投、绝对零度、协助、甜甜香气和神通力。地球上投使用真实命中命令保留的伤害输入；协助认证实际选择池、调用目标和前奏，后续叶招式独立准入；甜甜香气复用双方目标的能力下降结果。神通力保留原作缩小目标伤害规则。

共享层增加按控制流最大路径计算的有界容量、完整仿射战斗者复制、源有符号声像指令，以及统一周期正弦安全采样。合法表索引0..319保持原值；越界显示索引按256周期归一，详见行为差异文档，不声称原版可见错误或周期级硬件一致。

最终构建零警告／错误；受影响Application242／242（8.61秒），最终Presentation50／50（31.30秒），前置共享回归107／107。同一源比较器的完整绝对零度60场景及神通力68场景另行通过3／3。ResourceImport4／4对应18项Python检查，四个受影响生成器一致性通过。既有321根行和341份材质字节保持一致。没有新增原生窗口或实际声音输出验收；生成对照数据继续留在本地忽略目录。

## 333 根：高速接触、气刃与聚光灯（2026-10-18）

新增报复、神速、高速旋转、空气利刃、再来一次和吹捧。伤害组复用提交命中、双目标及条件威力归属，高速旋转的固定源威力仅由已认证命中输入提供。再来一次认证目标锁招结果；吹捧与虚张声势共用元数据选择的能力提升／混乱分组，保留部分结果和失败分支。

共享显示层增加 WIN0／WIN1／OBJ-window 优先级与颜色效果遮罩、源硬件淡入淡出的六位计数和 VBlank 结束阶段。完整有界脚本路径一次执行，吹捧的 19 次 helper 调用在同帧产生 38 片纸屑。沿用场景级原始子对象、局部随机数、扫描线、仿射与材质所有权；RGB 素材无需为无调色操作的回调重复生成索引版本。

最终解决方案构建零警告／错误（36.42 秒）；Application 278／278（7.90 秒），受影响 Presentation 67／67（35.68 秒），前置共享／背景回归 31／31 和 26／26。ResourceImport 2／2（64.33 秒）覆盖 13 项 Python 检查。原 C 对照含伤害组 268 场景／8559 帧、聚光灯 46 场景／3444 帧及硬件淡入淡出 7 场景／350 阶段行；同一伤害比较器另跑完整 516 场景／15275 帧通过 1／1（9.71 秒）。现有 327 根脚本行与 350 份材质字节保持一致。生成对照数据仅保留在本地忽略目录。本批没有新增原生窗口或真实音频验收。

## HP结果与任务派生粒子（2026-10-18）

新增Pain Split220、Eruption284和Water Spout323，当前336个专属根。Pain Split使用一次有序双方HP结果；后两者使用真实damage producer输出的动画威力，并独立冻结Water Spout调用时的原队伍HP四分位。公开回合产生的Water Absorb首目标恢复、Sport所有者首目标KO及Transform原最大HP均有边界检查。starter特性取得由真实双打Skill Swap／换人验证；单打复用该已产生body作为受控状态，不宣称自然单打取得路径。最终active-sync在第二次命中之后，不能影响较早调用的HP快照。

本次恢复后验证：受影响Application296／296；Presentation受影响103行全部通过（首次102行通过、1行因本地源fixture缺失失败，明确生成后相关27行刷新全通过）；Squash完整349场景使用同一逐帧比较器2／2通过；Battle伤害／规则回归1425／1425、Domain伤害8／8；两项导入包装2／2及对应Python11项通过。重建的基础Content12／12与world／field／audio-cue／HELP等资源一致性检查通过。完整solution编译和Godot托管headless探针已验证官方4.7.2及.NET10；这不是本次全Presentation、桌面原生逐帧或扬声器验收。源C oracle、完整帧数据和恢复证据均保留在忽略的本地／外部位置。


## 340根：烟雾、礼物、蛮力与玩水（2026-10-18）

新增Smokescreen108、Present217、Superpower276、Water Sport346。全部仍经唯一资源目录与元数据结果分派；泥/水运动共用相邻状态提交和已验证move-end尾部。礼物伤害沿用普通命中入口，只携带实际伤害寄存器的正负谓词；目标恢复分支认证四分之一最大HP及封顶结果。Endure/Focus Band留下零伤害时，源脚本选择恢复外观的行为被显式保留，战斗结果不变。

烟雾的动态图像/调色板由场景内原始父对象持有，四象限退出后释放；脚本结束、残留交接、重叠拒绝及取消清理均有测试。共享OAM顺序改用源priority与byte subpriority，而非假定所有特效priority2。正常脚本同步创建的烟雾象限在该帧sprite/OAM阶段出现；后置task阶段仍沿用既有帧锁存。蛮力复用已有线性/仿射播放器及真实自身能力下降结果；玩水复用原始子对象与两段抛物线，随机数只来自动画局部流。

累计solution构建零警告/错误（70.93秒）。受影响Presentation首轮84/87通过，三项新测试前提修正后相关25/25刷新通过（12秒，含额外克隆/优先级合成）；原C逐帧比较器均通过。Application扩展回归153/154通过后，替身归零记录规范化为null的测试修正完成，新四根35/35最终通过（4秒），先前其余回归不变。资源导入包装3/3（63秒，对应15项Python检查），玩水2项及元素包重建1项独立通过。最终Godot刷新零警告/错误。现有134个physical和146个elemental脚本行及既有素材保留。

源C证据包括玩水18场景3774帧（完整30场景6282帧另行生成核验）、烟雾25原语场景1064行及六个完整根738帧、蛮力86场景7188行、礼物168场景3798行；O0/O2结果一致。Git保留生成器、明确用例与比较器，生成轨迹/素材仍在本地忽略目录。未新增桌面逐帧或真实音频验收。Nature Power与Mirror Move的源意图是转发实际叶子；当前调用来源认证尚未补齐，仍不计已完成，不创建虚假独立脚本。

## 当前调用转发与 Volt Tackle 边界

Nature Power 的九种地形叶招式与 Mirror Move 的实际历史选择共用调用链描述器，保留原始选择／转移／PP／结果回执。Mirror Move 的 Counter／Mirror Coat 重选伤害来源或 Follow Me 目标通过真实生产者新增的有类型目标回执认证；准备型招式使用实际无攻击文字的 PP／charge 边界。伪造、重复、错目标及无效 PP 仍拒绝。Metronome／Sleep Talk／Assist 的原有前奏和真实叶招式分别保持各自时序。缺失叶脚本、Focus Punch 准备、延迟攻击与其他未实现通用演出不能由转发身份数掩盖。

Volt Tackle 344 复用既有电光素材和场景精灵所有权，保持五段移动及最多17个同时电光。原作当前学习数据无自然直接学习入口；测试明确区分受控直接执行与真实 Metronome 调用入口。源对照102个案例／3,172行由本地生成器提供，未加入版本控制；没有追加原生画面或扬声器一致性结论。

## 当前安全表现组

Sludge124／Sludge Bomb188／Spark209／Mach Punch183／Shadow Punch325 已使用共享素材、精灵场景、帧／仿射和副本合成通道接入。三项用户批准的源边界解释见[明确差异](original-behavior-improvements.md)。独立源轨迹与批准策略分开对照：Sludge／Spark894案例11,122行，拳击残影10案例173行，均为本地显式生成。已有素材字节不变。

剩余六个身份为 Spite180、Baton Pass226、Memento262、Endeavor283、Secret Power290、Weather Ball311。通用演出与特定上下文的既有边界仍独立保留，身份数不等于全部演出组合或原生像素／实际声音验收。


## 2026-10-18：Baton Pass／Spite／Endeavor／Secret Power

接力棒226在真实成功换人选择之前播放，携带并认证原有 suspended invocation，保留调用者PP及离场同步。怨恨180从目标招式PP减少收据准入，通用优先级0波纹任务与已有场景扫描线存储、延迟停止和残影材料共用所有权。蛮干283按用户批准使用本次已提交、HP截断前伤害驱动现有震动公式；不读取残留威力或修改战斗结果。秘密之力290保留11条源选择指令和10个目录标签引用，同一调用／随机流／帧中转入已有脚本，无新身份或展平大程序。

当前350个实际脚本加两个经认证转发身份，共352／354。剩余身份为Memento262与Weather Ball311；后续通用演出和真实音频边界仍独立，不随身份数关闭。

验证：完整solution刷新0警告／0错误；首次受影响Presentation98／99（3分11秒），唯一失败是旧全根资源夹具遗漏Water Spout已要求的HP分段输入，补齐后资源6／6（1分54秒）全部通过，含350根逐帧合成。Application首次88／89，唯一失败为刻意默认ImmutableArray在进入matcher前被JSON工具拒绝；改为直接检验全部页面的拒绝及原值保留后89／89（6秒）。新源轨迹Baton12场景297行、Spite10场景955行显式复现；导入检查Baton5、Spite4、Secret4、Reaction／Endeavor6通过。未新增原生桌面或实际扬声器验收；所有生成轨迹与游戏素材继续不入Git。


## 354身份：临别礼物与气象球（2026-10-18）

Memento262认证自损、目标双能力结果及濒死／队伍同步尾部；保护分支不播放，替身失败仍拥有原作动画。共享自损尾部也供既有GhostSong使用。静态BG、纵向16位扫描线、WIN0／WIN1的BG1／BG2选择和可见精灵优先级恢复均属于共享表现层。原作不检查目标半无敌状态，因此真实Fly／Bounce／Dig／Dive目标仍播放；隐藏目标不复制贴图、不被显示，任务读取保留BG偏移并保留其隐藏优先级。

Weather Ball311复用已有帧、仿射、直线、岩石和冰晶玩家，仅新增一份索引球素材。真实伤害producer在扣除body／substitute HP之前冻结控制器天气位，包括已濒死Cloud Nine／Air Lock所有者；动画按源Sun→Rain→Sand→Hail优先级选择五个分支，不从最终场地或招式类型推断。普通／永久天气、四位置、替身、调用叶及缺失／畸形收据均有检查。

最终solution0警告／0错误（46.97秒）。初轮受影响Presentation64／66通过（4分2秒），两失败均为恢复后缺本地源对照文件；显式补齐固定输入并生成后，含隐藏目标与共享扫描线／窗口的刷新24／24通过（34秒）。全352程序逐帧资源合成在初轮已通过。Application前轮118／118（6秒），隐藏目标与prepared输入刷新109／109（7秒）；Battle天气／伤害7／7（386毫秒）。导入Memento4／4、Weather Ball6／6；原C对照Memento15场景1099帧及5helper、Weather Ball48生命周期1432帧与256天气掩码，O0／O2一致。Earth57场景1327帧、Scanline58场景444操作131采样亦明确生成并通过同一比较器。旧rock输入pin仅修正恢复文件末尾多出的LF，核对官方固定版本blob，未改变数值预期。

352实际脚本加2个经认证转发身份已覆盖354身份。Future Sight／Doom Desire延迟命中、Hail残留、Focus Punch预备等通用演出仍须逐项确认并闭合；真实扬声器和未执行的原生组合也不由身份数量证明。本轮未新增原生窗口／真实音频验收，生成轨迹与游戏素材继续不入Git。
