# 原作行为改进

本清单将用户明确批准的改进与原作兼容语义分开；原作素材和脚本不改写为修正版。

## 已批准：水之小径赫拉克罗斯纪录奖励（2026-10-16）

- 原作：比较时先写新纪录；奖励分支先置 `FLAG_GOT_NEST_BALL_FROM_WATER_PATH_HOUSE_1`，然后尝试赠送巢穴球。背包满时仍消耗这次纪录的领奖机会，同纪录不能补领。
- 用户批准：巢穴球实际成功入包才确认新纪录及领奖标记。满包保留旧纪录、旧标记，腾出空间后可以重交同一只。
- 实现：共享大小纪录服务的 `SuccessfulGift` 提交策略。待提交纪录仅存在于不可存档的交互场景；成功时背包、纪录、标记在同一个不可变状态事务内更新。原作提示文本、选择、尺寸比较、首次较小/相等奖励、后续较小/相等无奖语义保持。
- 来源：[House1 脚本](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/SixIsland_WaterPath_House1/scripts.inc)、[共用尺寸算法](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokemon_size_record.c)。

## 待评估：12号道路鲤鱼王纪录奖励

该服务也有先提交纪录/标记、满包丢失捕网球的原作行为。本次批准仅针对赫拉克罗斯，因此鲤鱼王仍使用共享服务的 `Comparison` 原作提交策略。没有擅自扩展修正范围。

## 已实现：图鉴钓鱼分布查询的数组边界（2026-10-16）

- 固定原作 `wild_pokemon_area.c` 的 `IsSpeciesOnMap` 在未定义 `BUGFIX` 时，以 `LAND_WILD_COUNT`（12）遍历只有 `FISH_WILD_COUNT`（10）项的钓鱼数组，存在越界读取；源码自身提供了正确长度的修正分支。
- 重制版离线导入只使用10个已定义钓鱼条目，不复制越界内存内容。这依据用户允许在开发中修复明确问题的总体方向处理，未单独声称用户批准过这一具体条目。
- 修正范围仅是图鉴分布数据读取；钓鱼概率、可遇物种与实际遇敌规则不变。未据源码越界推定特定 ROM 上必然出现哪些错误标记。
- 验证：源表数量／第11、12项变异拒绝及代表钓鱼分布测试。见[图鉴说明](features/pokedex-habitats-and-area.md)。
- 来源：[固定原作查询](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/wild_pokemon_area.c)。

## 已实现：战斗招式换位后的 Mimic PP 归属（2026-10-16）

- 原作固定版本 `battle_controller_player.c:656–659` 仅在换位的起点带有 `mimickedMoves` 标记时移动该标记，没有对终点进行对称处理。
- 可复现影响（以下索引从0开始）：普通 TACKLE 在槽0，Mimic 临时复制的 GROWL 在槽1；在 FIGHT 对槽0按 SELECT，然后选择槽1。实时招式变为 GROWL/TACKLE，队伍持久招式为 MIMIC/TACKLE，但原作临时标记错误地仍留在槽1。随后使用普通 TACKLE 只扣实时 PP，离场恢复该 PP；使用临时 GROWL 却会错误地扣持久 MIMIC 的 PP。以复制招式为换位起点时不出现同一错误。
- 修正：在同一个不可变换位事务中，对已有 `CopyMoves.Slots` 临时招式与持久备份的槽映射做对称置换。没有引入第二套历史位掩码来保存错误。普通招式照常持久扣 PP，Mimic 复制招式照常仅消耗临时 PP。
- 这是依据用户允许修复开发中发现的明确问题的总体方向作出的修正；不声称用户曾单独投票批准此具体缺陷。Transform 的原队伍招式、PP 和 PP Ups 保持独立，不因临时招式换位而改变。
- 验证：`SourceBattleMoveOrderTests.MimicBackupFollowsCopiedSlotInBothDirectionsAndRestoresCorrectly` 覆盖两个选择方向、随后使用两类招式、离场恢复与 JSON 还原；另有 Transform 原样恢复、逆向换位及 PP 使用测试。
- 来源：[固定原作换位处理](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_controller_player.c#L631-L719)、[PP 写回](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_script_commands.c)。

## 已实现：破坏光线合法帧选择与倒放叫声插值边界（2026-10-17）

- 用户明确批准：破坏光线只选择合法动画；倒放插值到边界时使用边界采样值，不复现越界读取。两项均属于表现层安全策略，战斗规则与 gameplay RNG 不变。
- 破坏光线：固定源码 `battle_anim_effects_1.c:255–264` 的大光球动画表有7个指针，`AnimHyperBeamOrb:2376–2381` 却使用 `Random()%8`。紧邻的小光球表不能单凭源码顺序证明零售二进制的第8项行为；本项目不声称已经确认原版可见故障。批准策略在7个合法条目中均匀选择，使用独立视觉随机流的拒绝采样，拒绝16位值65534/65535后重抽。原始源向量与这一明确差异的测试分开保留。
- 倒放叫声：固定 ARM `m4a_1.s:568–602,673–682` 在最后一个反向样本处可将插值 lookahead 变为 `0xffffffff`，随后形成不在提取音频范围内的地址。受控算例中，Sentret 的 Growl 在第11帧（15帧长度内）、Jigglypuff 的 Echo 在第14帧（25帧长度内）到达该边界，包络仍未释放，不能假定这次读取听不见。
- 批准策略由共享反向插值读取器限制端点：保留所有合法样本与既定模式参数；当插值需要起点之外的样本时，使用起点样本。没有按物种或招式伪造声音，没有读取任意主机地址，也不据此宣称宿主音频等同于原硬件的未定义读数。
- 来源：[光球表及回调](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_anim_effects_1.c)、[原版音频汇编](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/m4a_1.s)。HyperBeamAnimationTests 分别验证原始运动与批准选择策略，AnimationCryTests 分别验证合法倒放采样与端点规则；本批 Audio536／536 和修正后 Presentation65／65（含全部184根脚本合成）通过。
- 社区核对边界：本次检索未找到针对这两处精确边界的同题公开缺陷报告。公开的 [`Fix CRY_MODE_ECHO`](https://github.com/pret/pokeemerald/commit/342b7da5d36460bb5780ea60da4b6b5be46ddc38) 是常量命名修正，并非倒放端点修复；不能拿它证明本问题已获上游确认。上述策略依据本项目的源码边界审阅和用户批准，不据此宣称原版零售机必然出现可见／可听故障。

## Magnitude 动画寄存器边界的安全解释（2026-10-17）

固定源码 `include/battle_anim.h:67,86` 将动画参数声明为 8 个 `s16`，`src/battle_anim.c:34` 的定义也使用同一长度。`src/battle_anim_ground.c:713–717` 的 `AnimTask_IsPowerOver99` 却把布尔结果写入下标 15；`data/battle_anim_scripts.s:3386–3390` 随后用两个 `jumpargeq 15` 选择普通／强烈震动分支。`Cmd_jumpargeq` 直接读取该下标。源码中的相邻全局变量不能证明这是合法的 16 槽数组，也不足以证明零售版本出现了何种可见故障。

按照既有现代重写与安全修复方向，重制解释采用一个明确有界的 16 槽脚本寄存器文件，保留每条精灵／任务创建指令最多 8 个参数的原始约束。参数写入仍只覆盖提供的前缀，余下寄存器保留到后续脚本读取；取消时统一清零。下标 16 及以上仍被拒绝。这样无需在通用分支解释器加入 Magnitude 特例，也不会访问或模拟相邻全局内存。

大于 99 的判断只读取已提交的 `SourceMagnitudeEvent.Power`，不重新抽取 Magnitude，不修改威力、伤害或战斗 RNG。原始合法运动／寄存器数值的向量与这一安全存储策略的测试分开；不把扩展后的存储长度称为原版内存布局一致。

## 已实现：共享精灵仿射零轴的源 UBFIX 安全语义（2026-10-18）

Mimic 的原始仿射程序从零尺度开始。固定源码 `sprite.c:1306` 的 ConvertScaleParam 使用 SAFE_DIV；`global.h` 的 UBFIX 分支在分母为零时返回0，未启用时为直接除法。本项目采用这个已存在的安全分支，分别处理两个轴，不模拟除零。非零轴保留原始整数商、符号和宽度。源向量及独立保留状态测试覆盖零轴初始化和随后增长；这只影响表现矩阵，不改变战斗规则或 RNG，也不声称已证明原版可见故障。

## 共享周期正弦采样的有界解释（2026-10-18）

固定 `battle_anim_psychic.c:945–969` 的 Extrasensory 阶段1从索引192开始，每行增加2；普通目标的闭区间65行会在最后一行读取320。`trig.c` 的 `gSineTable` 只定义0–319，其中256–319逐项等于0–63。源码审阅确认存储边界风险，但没有据此推断零售链接布局或宣称原版可见故障。

沿用获准的表现层安全重写方向，共享采样器保留所有合法0–319索引的原值；超出存储范围时按256相位周期取值，因此320映射到64。独立测试分别证明固定源表扩展段、全部合法索引不变和显式越界周期策略。调用者复用这一采样器，不按招式或物种特判，也不读取相邻内存；战斗规则和战斗随机数不变。

## 已批准的污泥弹体、电光与拳击残影安全表现（2026-10-18）

用户明确批准下列三组表现策略。战斗规则、伤害、PP及战斗随机数不变；源码合法路径与批准策略分别测试，不把安全解释称为原版零售机已证实的可见故障。

- Sludge／Sludge Bomb：固定 `battle_anim_poison.c` 的弹体初始化在参数0分支请求动画2，而该模板动画表只有动画0。重制使用唯一合法的动画0，保留原弧线、仿射缩放、持续时间与命中粒子的signed16减速轨迹。
- Spark：固定 `AnimSparkElectricityFlashing` 用闪烁间隔作模数，脚本提供0。零间隔明确表示不闪烁，仍执行全部原始角度、位置、参数变更和后减生命周期；非零间隔保留原行为。共用坐标采样保持signed16收窄，固定电光的直接矩阵也通过已有场景精灵通道绘制。
- Mach Punch／Shadow Punch：`AnimTask_AttackerPunchWithTrace` 不再从攻击者实际偏移减去旧精灵编号；残影使用攻击者当前位置。复制攻击者明确的正常颜色基线及既有变换，再施加源任务的色调，不依赖刚分配调色板槽中的残留颜色。后续五次向外、四次返回的尝试、八回调残影寿命、精灵分配失败、任务等待与清理保留源行为。材料／仿射快照属于每个残影；后续颜色变化或槽复用不能改写已生成残影。

源文件仍固定于 `037335f4c725d7c9aecdac87066f2002b4bd7e14`。`tests/Reference/SludgeSpark` 与 `tests/Reference/PunchTrace` 的显式生成器独立执行提取C，记录原初始化与批准初始化的差别；所有生成轨迹留在本地忽略目录。没有新增每招式渲染器、源内存模拟或随机玩法分支。

## 蛮干震动输入的明确安全解释（2026-10-18）

固定源码 `BattleScript_EffectEndeavor` 经 `setdamagetohealthdifference`、`adjustsetdamage` 进入 `HitFromAtkAnimation`，没有执行 `damagecalc`；动画却两次以 FALSE 请求 `AnimTask_ShakeTargetBasedOnMovePowerOrDmg` 的威力分支。该全局威力寄存器保留先前计算值，AI 的伤害试算也可写入它。这是源码输入归属问题，不据此宣称原版零售机出现确定可见故障。

用户明确批准使用本次蛮干已提交的伤害决定震幅。导入器先核对上述源码结构及两条任务的全部参数，再将两条任务选择器转换为既有的伤害模式。Application 使用真实 HP／替身命中收据的 pre-clamp Damage，不用最终 HP 损失，也不伪造 MovePower；通用震动任务继续使用源整数除以12、signed16收窄及1–16夹限，间隔、次数和轴向原样保留。没有修改伤害、PP或战斗随机数。原始共享任务向量继续验证源公式，批准后的输入选择、上下限、持续时间、替身截断及实际调用叶另有明确回归。
