# 当前战斗系统矩阵

更新：2026-10-16。本文与 [机器可读清单](battle-system-completion-matrix.json)维护当前实现边界，不累计历史批次或要求恢复已退休测试。普通单／双打规则阶段已达到既定出口，主线及后期业务已经继续推进；不再沿用“第五徽章阶段仍在开发”的历史状态。

<a id="shared-utilityweather-and-ability-commands-2026-10-11"></a>

## 口径

- 354个招式身份已有规则准入；[AllMoveRecipeTests](../tests/Game.Battle.Tests/Turns/AllMoveRecipeTests.cs)为每招的四个真实出战位置生成受控公共回合用例，共1,416行。它验证实际执行而非仅注册，但不能证明全部组合、随机分支、获得途径或特殊模式。
- 76个有效特性身份已准入，不含NONE和未使用的CACOPHONY；身份准入不是所有入场、复制、天气、免疫和残留交互穷尽验证。
- 专属招式脚本137／354，其余217尚未接专属还原。这是实现范围，不是总体完成率，也不是“全部动画写好只差验收”。
- 定义存在、规则执行、应用入口、专属动画、原生像素一致和真实声音输出分别成立；不能相互替代。当前137招范围通过Presentation全套581／581、应用专属分派316／316；这些受控检查不等于原生逐帧或实际扬声器验收。

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

这不覆盖其他多能力招式、入场／道具／次要效果的全部波纹时序。Cosmic Power、Calm Mind、Dragon Dance、Memento、Superpower、Overheat、全能力次要提升、Curse、Belly Drum、Flatter等仍需各自真实结果脚本闭包；调用／复制／Snatch／反射结果也须满足明确调用上下文，不能由最终Frame推断。通用渲染器支持组合参数不等于这些调用路径已完成。

## 专属动画范围

当前分派支持：Pound、Karate Chop、Double Slap、Comet Punch、Fire Punch、Ice Punch、Thunder Punch、Scratch、Vice Grip、Cut、Gust、Wing Attack、Bind、Slam、Vine Whip、Stomp、Jump Kick、Rolling Kick、Headbutt、Horn Attack、Fury Attack、Horn Drill、Tackle、Body Slam、Wrap、Take Down、Double Edge、Tail Whip、Poison Sting、Twineedle、Bite、Supersonic、Acid、Ember、Flamethrower、Mist、Water Gun、Hydro Pump、Ice Beam、Psybeam、Bubble Beam、Peck、Submission、Low Kick、Counter、Strength、Absorb、Mega Drain、Growth、Razor Leaf、Poison Powder、Stun Spore、Sleep Powder、Dragon Rage、Fire Spin、Thunder Shock、Thunderbolt、Thunder Wave、Thunder、Rock Throw、Toxic、Confusion、Psychic、Hypnosis、Agility、Quick Attack、Screech、Recover、Harden、Minimize、Withdraw、Defense Curl、Barrier、Light Screen、Reflect、Focus Energy、Self Destruct、Egg Bomb、Smog、Bone Club、Fire Blast、Clamp、Swift、Spike Cannon、Hi Jump Kick、Dream Eater、Leech Life、Bubble、Spore、Flash、Psywave、Splash、Crabhammer、Explosion、Rock Slide、Slash、Triple Kick、Thief、Flame Wheel、Cotton Spore、Powder Snow、Octazooka、Zap Cannon、Bone Rush、Outrage、Giga Drain、Endure、Charm、False Swipe、Swagger、Steel Wing、Dynamic Punch、Dragon Breath、Iron Tail、Metal Claw、Synthesis、Vital Throw、Crunch、Mirror Coat、Whirlpool、Magic Coat、Snatch、Mud Sport、Slack Off、Crush Claw、Metal Sound、Tickle、Cosmic Power、Signal Beam、Sand Tomb、Aerial Ace、Dragon Claw、Iron Defense、Bulk Up、Mud Shot、Poison Tail、Covet。

Double Slap／Comet Punch按每个真实HP或替身命中回执播放，使用明确的零起始命中序号选择源分支；结束保留同一回执，不再提交ACK。18个应用用例含原版随机向量、提前KO、Rough Skin接触截断、替身破裂后的本体命中及零伤害命中；8个表现用例含全部256字节分支输入和缺失上下文拒绝。Triple Kick另以固定三次计划命中契约接入相同逐回执生命周期，13个应用用例验证逐击命中检查、提前截断及10／20／30威力；4个表现用例含1,024个原C条件分支向量。Beat Up等不同调用上下文未据此准入。

Rock Throw／Rock Slide共用原版落石与地形摇动回调；BG3纵向寄存器按16位值变化并恢复各控制器捕获的原值，重叠控制器及非零恢复向量已验证。画面从保留的原版512×256索引地图采样，拒绝用裁平的可见纹理假装滚动；健康栏与文字仍在240×112场地图层之后固定绘制。

Metal Claw／Crush Claw／Dragon Claw复用共享爪击帧播放器及原版地形／OBJ坐标偏移控制器；全局偏移仅投影到启用的OBJ，不移动monbg副本或HUD。528组原C向量覆盖爪击与四种寄存器摇动模式，显式SE1包装器保留即时播放、参数前缀写入及零视觉任务计数。共享素材读取器支持原版128像素宽图块存储，取消与自然结束分别验证；禁用的金属背景寄存器快照不被误当活动图层。

Thunder／Cosmic Power复用保留索引地图的滑动背景任务；摇动读取实时共享参数，非攻击者调色覆盖四个OBJ调色板组并保留原参数移位。SE1／SE2包装器使用源歌曲播放器身份校验；测试覆盖即刻执行、无视觉任务计数、TRUE／FALSE及非法标志。该批通过完整Battle 1,709／1,709及应用组合494／494；最后一次完整Application 1,202／1,202仍属于此前129招快照，未扩大为本批的全套应用证据。

Poison Sting／Twineedle／Spike Cannon共用整数BIOS角度、既有线性平移和命中图元；Twineedle按固定两次计划、Spike Cannon按2–5次计划逐真实回执播放。角度检查器可复现786,440次对照，仅保留60个紧凑边界向量；104组提取原C回调覆盖位置、友方目标、生命周期及255／256／257位移的16位分子截断。现有图块复用，无新增像素素材。

Horn Attack／Fury Attack／Horn Drill共用原版7位定点Horn回调，保留signed16累加溢出、持续帧夹限和倒数第二帧定位。Fury按真实2–5击回执继续；OHKO检查涵盖命中阈值、Sturdy、等级、Protect及Ghost阻断。导入器核对FireRed的`jumpifcontest`函数仅前进5字节后，折叠零时间死分支并重定位可执行标签；不导入比赛背景、不保留死分支运行时元数据。当前应用组合542／542通过；完整Application与Battle证据仍按各自快照限定。

其他招式返回 `script-outside-slice`，不启动该专属脚本；普通文字、HP及结果表现仍独立工作。不能因此说招式规则不可用，也不能把通用反馈当原版专属动画。此名单由当前单／双打分派代码核对，JSON逐招标记相同范围。

用户于2026-10-16要求完成除汉化外的单机内容，专属动画与剩余原生表现重新进入当前工作范围。采用共享实现与代表性检查，保持实现范围和已验证范围分开。[Options玩家入口](../src/Game.Godot/CampaignOptionsView.cs)已开放Battle Scene和Sound编辑并接入保存与运行时应用，见[设置说明](battle-scene-sound-options.md)。

Cosmic Power的规则修正限定在现有`EFFECT_COSMIC_POWER`分支：保留双项封顶的提前失败，按原作执行Defense再SpDef两条命令。单项封顶现在产生真实最大值回执，表现层跳过该文字；这也经现有历史归约使`LastLandedMove`无效，并保留既有`LastHitByType`，不是仅表现改动。HP、PP、最终能力阶级及RNG保持既定结果。依据固定源`battle_scripts_1.s:2679–2702`与`battle_script_commands.c:6809`，没有新增招式专用历史例外。公共回合检查含两种部分封顶顺序、四个出战位置及非空既有类型历史。

调用者复核：BasicStats还接入Swords Dance、Sand Attack、Tail Whip、Leer、Growl、Growth、String Shot、Agility、Screech、Double Team、Harden、Smokescreen、Withdraw、Focus Energy、Kinesis、Flash、Sweet Scent；这17个其他分支及共享`BasicStatChange`均未修改，未将Cosmic的两命令顺序推广到它们。

## 验证与规则决定

当前执行入口是 [八个维护MSTest项目](../tests/README.md)，不是已删除的历史Console套件。维护用例含受控队伍、HP、位置与随机状态，不能把总通过行数解释为自然玩家路线数量。源向量、逻辑测试、headless Draw、原生窗口及真实扬声器是不同证据；Dummy音频不能关闭实际输出门禁。

已批准的概率／Protect／Conversion 2／双打KO／王者之证／AI与服从规则差异保持不变，详见 [当前规则决定](first-clear-remaining-scope.md#中文与规则决定)。未批准候选不得由整理顺带实现。全游戏功能缺口、中文、通信进化服务待讨论及当前存档谱系边界也统一见该页。

Gust／Wing Attack／Steel Wing共用索引GUST材质、椭圆／目标平移回调及原版调色板轮换任务；帧／仿射结束交接、共享参数改写、六个出战位置与Steel Wing金属材质依赖均通过受控表现检查。

Bone Club／Bone Rush共用原版骨棒线性／仿射回调，Bone Rush复用2～5次真实命中回执；32组／352行原C向量与20个应用用例通过。普通专属入口同时统一收紧直接调用的选中招式、PP所有者／槽位／正成本与严格事件顺序，保留Pressure及耗尽时的扣减截断；范围仍不包含调用／复制等不同上下文。

Mist／Smog共用索引雾片与原版旋转平移回调，保留初始化即步进、隐藏搭档BASE坐标平均及OAM优先级3→0回绕；`splitbgprio_all`无条件写入BG1／BG2优先级，清除图层不擅自恢复寄存器。Mist按成功的五回合场地状态回执播放，已有状态／错误来源回执不播放。

已验证滑动背景基础层（不增加招式计数）：有符号8.8累加、原版256／512地图模式、独立优先级5无计数任务与共享参数停止／复位。六个新增背景的393,216像素独立核对按地图实际调色板银行读取，bank0保留原文字调色板、bank2使用招式调色板；既有背景／地形数据块逐字节保持不变。该基础层仍不等于其依赖招式已接入。
