# 当前战斗系统与表现边界

维护整理：2026-10-19。本文记录当前所有者、已接通的契约和仍未验收的边界，
不是测试运行报告或总体完成率。逐批成功计数、临时编译日志及旧阶段名单由 Git 历史保存；
本次整理没有重新运行战斗、源 C、Godot 窗口或音频验收。

## 当前范围与权威

- 普通单打与预置双打使用同一四位置规则内核。354 个招式身份有规则准入；
  [AllMoveRecipeTests](../tests/Game.Battle.Tests/Turns/AllMoveRecipeTests.cs)
  从实际目录为每招四个出战位置产生 1,416 行受控公共回合用例。
  测试定义存在不等于本次重新执行，也不证明全部交互、随机分支或自然获得途径。
- 76 个有效特性身份已准入，不含 NONE 和未使用的 CACOPHONY。
  特性身份闭包由[规范宝可梦目录](../src/Game.Content/PretCanonicalPokemonCatalog.cs)校验；
  身份准入不能替代入场、复制、天气、免疫和残留的情境验证。
- 专属招式程序为 352 个；Nature Power、Mirror Move 按原作转入经认证的叶招式，
  合计覆盖 354 个身份。转发不虚增独立根，也不保证所有上下文均可播放。
- 另有 10 个命名通用程序：持物、Castform 变化、Wish 恢复、雨／晴／沙暴／冰雹持续、
  Future Sight／Doom Desire 延迟命中及 Focus Punch 预备。
  [替身控制器](substitute-move-controller.md)另外使用两条 Special 程序负责揭示与恢复，
  不计入招式根或上述 10 个通用程序。
- Safari、游走、实验室教程／脚本遭遇和 Trainer Tower 有各自的应用所有者；
  普通内核的结果不能代替这些模式的全部分支。动态 Enigma 内容、玩家通信、外部 e-Reader、
  原版存档互换等不由普通单机战斗闭包证明。中文和其他延后范围见[当前范围与规则决定](first-clear-remaining-scope.md)。

不再维护另一份逐招／逐特性 JSON 状态表。数值身份、效果和目标来自实际内容目录；
规则准入来自 [SourceBattleKernel.SupportsRecipe](../src/Game.Domain/Battle/SourceBattleKernel.cs)；
是否存在可播放脚本由[规范资源解析器](../src/Game.Godot/SourceBattleAnimation.Resources.cs)判断。
没有第二份地区／招式支持白名单，也不以历史总数作为资源运行门禁。

| 责任 | 当前所有者与维护检查 | 不能扩大为 |
| --- | --- | --- |
| 命令、回合、换人、残留 | [共享 Frame](../src/Game.Domain/Battle/SourceBattleFrame.cs)、[生命周期测试](../tests/Game.Battle.Tests/Lifecycle/BattleLifecycleTests.cs)、[残留测试](../tests/Game.Battle.Tests/Residuals/EndTurnResidualTests.cs) | 全部招式／特性／物品组合或所有特殊模式 |
| AI、训练家与捕获 | [AI 策略](../tests/Game.Battle.Tests/TrainerPolicy/TrainerPolicyTests.cs)、[训练家接线](../tests/Game.Application.Tests/TrainerBattles/TrainerBattleTests.cs)、[野生捕获](../tests/Game.Application.Tests/WildBattles/WildCaptureTests.cs) | 自然完整主线和每个训练家分支验收 |
| 调用、回执与结果时序 | [分派契约](animation-dispatch-architecture.md)、[专属入口](../src/Game.Application/CampaignMoveAnimationDispatch.cs)、[通用入口](../src/Game.Application/CampaignGeneralAnimationDispatch.cs) | 由最终 Frame 猜测成功、PP、目标或动画参数 |
| 图层、材质与逐帧执行 | [背景／窗口／扫描线契约](source-animation-backgrounds.md)、[动画运行时](../src/Game.Godot/SourceBattleAnimation.cs)、[完整资源合成检查](../tests/Game.Presentation.Tests/SourceAnimationResourceTests.cs) | 原生 GPU、任意并发寄存器所有者或硬件周期一致 |
| 当前组合与玩家保存 | [组合测试](../tests/Game.Application.Tests/Campaign/CampaignCompositionTests.cs)、[generated-only 测试](../tests/Game.Application.Tests/Campaign/GeneratedOnlyCampaignTests.cs) | 可在活动战斗中存档或已重放历史通关路线 |

## 调用与回执契约

详细语法与家族所有权维护在[分派架构](animation-dispatch-architecture.md)，这里保留跨家族约束。

- 单／双打共用规范化调用验证器。真实定义必须给出正确 SourceMoveId，资源脚本还须匹配
  `Move_*` 身份。缺元数据、缺脚本、错误 label、错误 PP 所有者／槽位／成本、重复或乱序回执均拒绝。
  `script-outside-slice` 仍是缺可用脚本时的失败原因，不再表示一份尚未补齐的旧招式名单。
- 攻击文字、调用者文字、保留攻击文字拥有各自页面 ACK；命中、恢复、状态结果拥有者播放后
  回到同一回执，由既有门控防重播，不额外推进战斗。每次已提交命中保留原脚本粒子数量，
  不能用动画结束替代命中数或命中检查。
- Double Slap 等随机多段、固定两击、Twineedle、Triple Kick 依据效果及真实命中序号分派；
  提前 KO、接触截断、替身破裂和零损失 Endure 不伪造后续击。
  Beat Up 使用贡献者／命中对及实际执行序号，跳过队员不增加视觉序号，不伪造多段汇总。
- 双目标遍历保留第二次 PP 访问的正 CommandCost 与不扣 PP 语义；首目标阻挡、封顶、替身或 KO
  不否定后续合法目标。Earthquake／Magnitude 的全他位遍历拥有一条 PP 命令，保留搭档 Pressure。
  不能依据最终 HP 损失或最后场面重新选择播放目标。
- 主能力、成组能力和次要能力分别认证真实结果；部分封顶／阻挡、Rage、White Herb、Lum 等后缀
  不能冒充或擦去原招式结果。Ancient Power 五项源顺序只拥有一波，封顶消息仍可见；
  Belly Drum 与非 Ghost Curse 没有源 General_StatsChange，不额外添加波纹。
  Cosmic Power 保留 Defense 后 SpDef 的实际命令；部分封顶可清除 LastLandedMove 而保留
  LastHitByType，这是实际规则／历史效果，不能视作仅视觉修改。
- 伤害前缀区分主阻挡与成功命中后的次要 NoEffect／Sticky Hold；Thief 与 Knock Off 的目标存活条件
  保持各自原义。1 HP Endure／Focus Band 的零损失成功仍须真实伤害调整与存活回执。
- 动态威力、最终动画威力、伤害、友好度、Fury Cutter／Rollout 计数、Water Spout 原队伍 HP 分段、
  Weather Ball 控制器天气均由命令时点的真实证据提供。伤害与 HP 截断不同；Transform 显示尺寸
  不替代原身体最大 HP。第一目标倒下可改变后续实际威力，不能用最终状态重算较早输入。
- 调用前奏与叶招式分别认证：Metronome、Sleep Talk、Assist 保留选择／转移／PP 链；
  Nature Power 保留九种地形叶；Mirror Move 的 Counter／Mirror Coat 或 Follow Me 重选目标
  需要真实生产者目标回执。调用描述器仅准入以上五种调用者；任意反射／Snatch、入场／持物来源
  的能力波和跨家族组合仍须单独证明。Thrash、Bide、Rollout 的保留攻击不虚构 PP 或攻击文字。
- 蓄力设置可没有攻击文字；释放须拥有实际 release 和零消费 PP 证据。Solar Beam 晴天路径使用
  早期 PP／实际 charge/canceller 前缀，不从最终天气猜测。失败或状态取消的隐藏保持到真正 clear
  批次 ACK；当前协议没有单独原生 MOVEEND 可视回执，这仍是保守时序边界。
- Transform／Substitute 材料和 Castform 形态来自前向事件时点投影，不由最终天气或最终 Frame 反推。
  Transform 使用行动者原 shiny 身份、供体图片／形态和源 white6 基色；额外怪物图片使用真实
  party 身份，Unown 的位图选择与基础坐标、Deoxys 的源 frame0 分开处理。

### 已接通的结果与控制器时序

- Rest 在睡眠文字之后、真实恢复之前播放；后续 Lum／Chesto 不消除已提交睡眠。普通恢复使用
  已提交 HP，不重算天气。Wish 预约与到期恢复分别认证，满 HP 只显示源消息，零 HP 到期静默；
  单独 resolved 形状不是零 HP 证明。低层仍 Protected 的 ResolveAction 重组见下方限制。
- Future Sight／Doom Desire 设置与延迟命中是不同程序。延迟命中在当前文字 ACK 后播放，
  认证相邻成功 HP／替身回执，包括真实 1 HP Focus Band 零损失分支；失败结果不借用别的命中。
- 天气持续使用独立 field-phase 回执，永久天气也可续演；临时计时拥有文字，续演回执只作控制器，
  到期不播放。Focus Punch 预备在设置文字之前，成功命中仍由其专属根负责。
- Castform 通用变化保留 Forecast／相邻 overlay、不可变前后材料、换图与结果 ACK 的归属；
  Scene Off 仍执行控制器，替身直接更新而不转场，已知隐藏活体立即换图，未知隐藏证据拒绝。
  马赛克按屏幕原点对齐，每三回调增至 15，零起算 46 换图／palette／Y，92 清理。
- 替身揭示／招式／恢复共用一个原回执与 ACK。已实现自然 Baton Pass→Ditto Transform，
  不能继续把“Transform 在既有替身后”列为缺口。控制器从 move command 接受后的 case0 开始；
  更早的命令分派和 sound-busy 等待不在该切片。launch-only、脚本回调、OAM 与最终 ACK
  分属不同阶段；多段剩余数取计划次数而非最终实际次数。详见[替身控制器](substitute-move-controller.md)。
- Teleport 的逃离文字遗漏已由拥有施放者／招式来源的终止回执修复，确认文字后才进入无声同步；
  Roar／Whirlwind 不借用它的文字。离场姿态与残留精灵独立，真实换人、新动画、取消或退出才释放。
  修复及其验证边界见[项目审计修复](full-project-audit-repairs.md)。

## 共享显示机制与源解释

源语义依赖固定 `037335f4c725d7c9aecdac87066f2002b4bd7e14` 的 C／ASM／图像。
以下是维护约束，不是增加另一份程序准入表；具体回调向量、边界与完整根组合保留在
[Presentation 测试](../tests/Game.Presentation.Tests)及[独立源生成器](../tests/Reference)。

### 调度、控制流与所有权

- 使用一个 64 槽 sprite 场景，原始子精灵与计数精灵共享容量。实时升序遍历：高槽新生对象可同帧回调，
  低槽等下次；当前槽销毁后再分配只推进替代对象的帧／仿射阶段。
  CreateSpriteAndAnimate 立即初始化后仍遵循后续槽扫描。数值本地槽号不冒充 GBA 身份。
- BuildOamBuffer 锁存与 task 阶段独立：sprite 回调新生的对象可在本帧显示，后置 task 分配到下一
  sprite pass 才进 OAM。task 遍历按实时优先级后继，插在当前／之后可本轮执行，插在之前等下一轮；
  已销毁当前 task 后创建不属于准入闭包。task palette 写入可影响当前合成，priority 写入则等下次
  OAM 锁存；源脚本 delay 使月牙间隔为 12 帧而非 10。重复快照不推进状态，历史材料不受后续槽复用改写。
- 原始子对象可在根程序结束后残留，由同一场景继续推进并保留原调用材料。Hail 冲击分配失败保留
  源未释放计数，不虚构补救；Torment、烟雾、轨迹、雨滴等保留各自真实父计数与分配失败语义。
  容量以已审阅可达路径和现有保留槽证明；新增残留／并发消费者必须重新审查。
- VM 验证完整无环调用路径，允许跳回较早的终止指令，拒绝落出程序、循环、不支持的嵌套调用及溢出。
  不以任意 64 指令预算插入假帧；Flatter 的 19 次 helper 在同一脚本 pass 创建 38 片纸屑。
  FireRed 恒假的 Contest 路径须由导入器先验证再折叠／重定位，不能允许绕过判断的标签入口。
- raw x/y、x2/y2、逻辑隐藏、sprite 隐藏和 OAM 可见锁存分离；monbg 副本不等于原 OBJ。
  END、回调替换、帧／仿射 END、延迟销毁各自保留，不能统一成“计时归零就释放”。
  源 END 停在标记前、托管玩家停在 END 的编码差异在比较器中显式处理，不改写向量。

### 图像、坐标、仿射和 palette

- 整数算式保留 signed16／u8 收窄、算术右移、定点分子与源参数前缀写入。BASE／PIC 选择可以由
  参数高／低字节分别提供；隐藏搭档的基准坐标与可见性选择不能混用。monbg 的 byte 参数 0／2
  选 actor，其他值选 target；priority／byte subpriority 与 OAM 顺序共用一个所有者。
- 帧 LOOP、SeekSpriteAnim 直接 FRAME、仿射 LOOP／暂停／重启／保留 scale-rotation 和直接矩阵
  都进入共用播放器。Swords Dance 六次线性更新到 −31、Sharpen 的 callback-only 声音与递增闪烁、
  Horn 的最后定位、Fury Swipes 的帧 END 销毁及 Arm Thrust 独立计时均保留源差异。
- normal／faded palette 分开，源颜色变换逐次 RGB555 舍入。Music 的直接写入只改 faded；释放槽保留
  颜色，后来的调用不能重染已退休材料。恢复 normal 不自动改 faded；实际重新加载才清除双方状态。
  palette 索引、真实 bank 与透明像素来源不能由相同 RGB 值反推。
- BLDALPHA 保留 5 位值（例如 Overheat 12/18），像素算术分别饱和到 16；亮度依据真正最上层。
  背景 partial-load 保留未覆盖 map／palette，Moonlight 的稀疏写保留各 bank entry0。
  清 overlay、重载地形、恢复 blend 和取消是不同操作，不擅自恢复源未恢复的优先级寄存器。
- BG1 overlay 与 monbg 的未审阅并发、Moonlight／monbg 顺序、任意 affine subsprite 和并发 emitter
  不由共享功能自动准入。非仿射 subsprite 保留 piece 顺序、flip、坐标回绕和独立 priority，
  不增加逻辑精灵或视觉计数。水／雾／扭曲没有第二套合成器。
- WIN0→WIN1→OBJ-window union→outside 共用遮罩；有界矩形可独立选择 BG1／BG2，
  BG0／BG3／OBJ 保持启用。倒置／回绕窗口、任意 layer-disable 和未审阅寄存器冲突仍拒绝。
  WIN1 的 120–159 HUD 行有寄存器几何验证，240×112 战斗场合成不能替代完整 HUD 像素验证。
- 扫描线使用两个 960-word buffer、显式 DMA bank、延迟停止、逐行 alpha／偏移及先采样后裁剪。
  首行 dirty 写与 VBlank 使用明确帧阶段模型；源 VCOUNT 160／161 的实际周期边界未由控制流证明。
  行 1–159 和终止传输遵循所选 bank，不能称作 CPU 周期级原机一致。
  详细所有权与限制见[背景／窗口说明](source-animation-backgrounds.md)。

### 特殊结果仍由共享契约表达

- Present 使用已提交伤害寄存器的正负谓词；Endure／Focus Band 留下零伤害时仍保留源恢复外观，
  不修改战斗结果。Pursuit 的拦截伤害翻倍不把目录威力 40 改成 80；视觉 turn 不拥有额外 ACK。
- Protect／Endure 同时要求成功 attempt 和相邻 owned marker；Foresight 合法 1→1、Charge 合法 2→2
  不能因状态值未变而丢失。Spikes、Imprison、Perish Song 保留各自额外 Pressure／PP 归属。
- Trick／Recycle 的 typed 命令结果独立于可选 party 写回；Mimic 临时复制与 Sketch 永久复制保留
  不同 PP／PP-Up／持久化规则。活动同步的四槽临时招式掩码不能掩盖伪造 PP 退款。
- Party cure 保留 Soundproof／健康队伍行为，Refresh 需要实际治愈；Baton Pass 拥有真实 suspended
  invocation；Secret Power 在同一调用、局部随机流和帧中转入目录标签，不展平或重复计数叶脚本。
- Memento 认证 PP、自损、目标能力／阻挡、faint 与同步；Protect 消耗自身但跳过动画，替身失败在
  动画后，已最低能力对在自损前失败。原作无目标半无敌门禁，因此 present 的 Fly／Bounce／Dig／Dive
  目标仍拥有根；隐藏目标不复制／显示贴图，保留其 BG 偏移和隐藏 priority。不能据此发明隐藏 actor 路径。
- Weather Ball 从扣 body／substitute HP 前冻结的控制器天气位按 Sun→Rain→Sand→Hail 选分支，
  包括已 faint 的 Cloud Nine／Air Lock 所有者；不重新决定玩法类型。
  Castform、低 HP 威力及混合天气同样不能由最终场面倒推。

### 动画随机数与音频

视觉 Random 保留原 uint32 LCG `1103515245×state+24691` 的高 16 位和回调取数顺序，
但明确不复现 GBA 玩法／VBlank 共用流的具体值。瞬态种子采用固定 FNV-1a32，输入为已有 BattleId、
回执 Sequence、招式／参与者、可空命中序号和通用标签／参数；UTF-8 长度、整数均固定小端，
可空值有存在位，版本域为 `firered-source-animation-v1`。不读时间、进程哈希或 gameplay RNG，
不增加存档种子。实际调用随机回调才要求种子；缺失拒绝，未调用 helper 仍作结构校验。
快照／合成／销毁不取随机，取消丢弃流；没有把尚无消费者的 Random2 合并进来。

例如 Dirt 按 X／Y 取数，Random Fist 按 X／Y／sign／sign 并仅在负帧参数时追加一次，
Meteor 子星在首次回调取数，Rain 在分配尝试前取数；不能为分配失败统一修改抽样顺序。

叫声使用实际原队伍物种及共享解码缓存；Transform 不把 donor 物种误作原叫声身份。
预设、全局 cry liveness、token-owned 取消独立，Howl 不人为等待结束。
普通 PlaySE 保留歌曲自身 pan，panned wrapper 才写 pan；`panse_adjustall` 保留 signed8 增量、
signed16 累加和源端点，无额外 63 夹限。源向量、host PCM 和真实扬声器是不同证据。

## 原作边界与已批准差异

既有概率、Protect、Conversion 2、双打 KO 补位、王者之证、AI 与服从决定继续以
[当前规则决定](first-clear-remaining-scope.md#中文与规则决定)为准；本文不授予新改法。
[原作行为改进](original-behavior-improvements.md)分别保存来源、授权层级和独立策略测试：

- Hyper Beam 只在七个合法帧中拒绝采样选择，拒绝 65534／65535；反向叫声插值使用合法端点。
- Magnitude 使用明确的 16 槽脚本寄存器，但每条创建命令仍最多八个 operands；不是原内存布局证明。
- 仿射零轴采用源 UBFIX；周期正弦保留合法 0–319，越界按 256 相位周期解释。
- Sludge 使用唯一合法动画；Spark 零间隔不闪烁；拳击残影使用 actor 实际坐标和 detached normal 色基线。
- Endeavor 使用本次已提交、HP 截断前的 Damage 驱动既有震幅公式，不能读取残留威力。

另有两条已批准的通用演出解释见[命名结果契约](animation-dispatch-architecture.md#named-result-owned-general-animations)：
Doom Desire 最后越界坐标读取改为保持最后合法位置一回调、下一回调清理；天气在 field 阶段入口
冻结第一个 source-present 位置作稳定控制器，而不继承过期 Safeguard setter。
Rain pan 和 Sunny Day actor-relative 副本在该边缘可不同。原 C 定义范围停止在非法读之前，
终点 hold 用单独策略断言；两者均不改天气、伤害或战斗 RNG。
这些安全解释不证明某个零售 ROM 必然有可见／可听故障，原始材料和原始合法轨迹不能改写为策略结果。

## Metronome、获得途径与未完验证

[原始调用选择器](../src/Game.Domain/WildBattle/WildBattleSourceCallers.cs)是实际候选权威。
保留旧清单的三个覆盖类别，防止删除逐招 JSON 后把规则准入误读为 Metronome 可达：

- 336 个 direct 选择。
- 6 个 `indirect-mirror-only`：Counter、Struggle、Thief、Mirror Coat、Trick、Covet。
  它们不在直接池中；历史间接 Mirror Move 分类不能代替本次实际历史／目标／叶结果认证。
- 12 个 `outside-metronome-closure`：Mimic、Metronome、Sketch、Protect、Destiny Bond、Detect、
  Endure、Sleep Talk、Focus Punch、Follow Me、Helping Hand、Snatch。
  不能为测试发明不可能的 Metronome 选择；这不表示它们没有规则或自己的专属程序。

仍须保留的具体边界：

1. Wish 的普通完整回合调度已有检查；人为在仍 Protected 的自身状态上重组低层 ResolveAction
   缺少导出的原生 animation-prevention flag。该低层上下文未宣称忠实准入。
2. 失败蓄力释放使用 clear 批次 ACK 的保守隐藏边界；Substitute controller 的更早命令／声音等待、
   未审阅并发资源、扫描线首行周期、窗口与 HUD 范围均如上限定。共享原语存在不证明任意组合合法。
3. Front Spinda 的 PID 斑点仍缺所需 indexed 输入／实现，但当前未建立普通单机自然来源或对手路径；
   不加入不可取得物种来凑覆盖。已闭合的 Transform-behind-Substitute 不再与它捆绑列为缺失。
4. Dive 测试使用 HM08-compatible 受控 Wailmer；当前 campaign 不准入 HM08，也没有自然 level-up／egg
   Dive learner。Volt Tackle 没有当前学习数据的自然直接入口，受控直接执行与真实 Metronome 调用分开。
   对方多队员 Beat Up action-only、单打复用双打 Skill Swap／换人所得 starter-ability 身体同属受控证据，
   不伪造自然训练家或获得来源。
5. 身份、规则、脚本、原生像素与真实音频分别成立。未执行的合法组合、自然整线、Castform／替身／
   命名通用程序的原生表现不能由源向量或完整根合成次数代替。玩家稳定保存与严格 Continue 仍与
   受控在战斗中序列化测试分开。

## 原生观察、失败证据与再生边界

2026-10-16 有界原生观察使用已验证场地存档，从 Route 1 普通遇敌进入战斗：敌方 Tackle 后，
我方 Thief 切换 Dark 背景，结束后草地恢复且菜单可继续，无错误日志。这只证明该调用路径，
不是全部背景逐帧或后续二进制验收；持物通用程序仍没有实际原生路径采样。

同日 Double Slap 场地样本记录两次真实命中各自开始／结束，均未额外 ACK，随后正确显示命中数与
敌方 HP 归零；但第二击期间一张 1180×812 截图全黑，包含 HUD 和外边框，邻帧正常且日志无异常。
尚未确定是渲染还是捕获瞬态，不能接受该路径视觉一致性。须保留引擎视口与桌面截图配对复查。
证据：`/workspace/shared/firered-multihit-visual/native-double-slap-2002.log`；
MVID `e7ca54ed-3ee5-4d8a-962c-9dea810eb49f`；Sequence8 在 126311→126874ms 播放 40 帧，
Sequence9 在 127317→127903ms 播放 34 帧，两次 `acknowledged=false`。没有用相邻成功图替换失败图。

真实非 Dummy 输出仍是独立未完成门禁。Host PCM、source pan／cry 向量、headless Draw／ready、
干净退出和场地的其他原生样本都不能代替实际扬声器验证。其他素材／场景验收见
[当前范围](first-clear-remaining-scope.md)和[完整审计的当前剩余项](nonlocalized-single-player-completion-audit.md)。

源证据需要保留以下出处与纠错边界：

- 原始 C／ASM 来自上述固定版本，只提取明确片段并编译作者的有界 harness，不执行上游脚本。
  软件几何、callback／scheduler 数值、原始材料 RGBA 和实际窗口截图是不同层证据。
- Meteor／Water Pulse 的人工保留槽最初未初始化虚拟帧／仿射玩家；早期整套 O0/O2+UBSan 一致
  结论已撤回。暂停这些惰性占位后的严格 UBSan／O0/O2 复核才是修正证据，原紧凑数值不变；
  原复核日志 `/tmp/paused-reservation-oracle-verification.log`，不得把旧失败重标为通过。
- Water/root observer 后加的 raw OAM matrix bits 用于区分 base flip 与 BeginAnim 写入的 OAM flip；
  移除该观察列可还原原文件，原 callback 数值不变。对应源编码与托管编码差异应在比较器中表达。
- Psychic palette-harness 的宏作用域曾错误，必须使用独立修正后生成的 oracle；旧文件不是等价证据。
  ElectricWisp 的 CreateSpriteAt 首 tile、FocusSound 的真实 idle battler／slot 初始化也曾补正，
  旧文件保留作对照，不能用产品输出作为期望。Rock 输入修复仅移除恢复文件多出的末尾 LF，
  已核对官方固定 blob，未改变数值期望。
- 原 ASan／UBSan 运行中的 LeakSanitizer 因 ptrace 环境限制关闭，只能引用实际完成的检查，
  不能把它写成 leak-check 通过。缺本地 fixture 的失败不等于 skip，也不能以之前另一快照的全套
  结果充当当前全套通过；逐批修复／复测和失败记录保留在 Git 历史与原外部日志。
- raw、generated、逐帧 oracle 和验证报告均为本地／外部忽略输入，不强塞进 Git，也不公开素材。
  [测试说明](../tests/README.md)规定显式独立生成、缺输入失败、完整目录禁止回退及代表用例每帧比较。
  普通测试不启动 C 编译器，不据产品实现重写 golden。整个 generated 仍不能视作可随意删除的缓存。

## 维护检查入口

按改动选择对应边界，不为文档整理重跑所有数值或声卡测试：

- 规则／公共回合：[AllMoveRecipeTests](../tests/Game.Battle.Tests/Turns/AllMoveRecipeTests.cs)、
  [Switching](../tests/Game.Battle.Tests/Switching/SwitchingTests.cs)、[Items](../tests/Game.Battle.Tests/Items/BerryItemTests.cs)。
- 调用与结果：[Application WildBattles](../tests/Game.Application.Tests/WildBattles)、
  [共享测试资源身份](../tests/Game.Application.Tests/AnimationDispatchFixture.cs)。
- VM／场景／素材：[SharedAnimationCommandTests](../tests/Game.Presentation.Tests/SharedAnimationCommandTests.cs)、
  [SourceAnimationResourceTests](../tests/Game.Presentation.Tests/SourceAnimationResourceTests.cs)、
  [SpriteOamPhaseTests](../tests/Game.Presentation.Tests/SpriteOamPhaseTests.cs)、
  [SourceTaskTraversalTests](../tests/Game.Presentation.Tests/SourceTaskTraversalTests.cs)、
  [VisualRandomTests](../tests/Game.Presentation.Tests/VisualRandomTests.cs)。
- 命名程序／替身：[NamedGeneralAnimationTests](../tests/Game.Presentation.Tests/NamedGeneralAnimationTests.cs)、
  [NamedGeneralSourceOracleTests](../tests/Game.Presentation.Tests/NamedGeneralSourceOracleTests.cs)、
  [SubstituteMoveControllerTests](../tests/Game.Presentation.Tests/SubstituteMoveControllerTests.cs)、
  [SubstituteSwapAnimationTests](../tests/Game.Presentation.Tests/SubstituteSwapAnimationTests.cs)。
- 导入及原像素：[资源工具说明](../scripts/README.md)、[维护导入检查](../tests/Game.ResourceImport.Tests)；
  音频只在涉及它时显式运行[音频测试项目](../tests/Game.Audio.Tests)，仍不表示实际输出验收。

新内容应进入同一调用、资源、场景和结果所有权。具体回调差异保留在其实现、源生成器与语义断言中，
不再以追加“第 N 根／本批全绿”的文档段落建立第二份运行权威。
