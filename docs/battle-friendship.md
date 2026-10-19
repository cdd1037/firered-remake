# 战斗亲密度的来源与归属

普通单／双打的战斗亲密度由 Domain 的 `SourceBattleFriendshipRules` 计算。Application 只把实际永久队伍投影回玩家队伍，不再看最终 HP 推断是否刚刚倒下。

## 原作时点与数值

固定来源为 `content/raw/pokefirered/037335f4`：

- `src/battle_main.c:711–713` 在训练家队伍生成后、战斗体初始化前对玩家队伍调用联盟战事件；`src/pokemon.c:5440–5510` 仅允许道馆馆主、四天王和冠军类别。所有非蛋成员都增加，包括已经倒下的后备成员。亲密度 0–99／100–199／200–255 的基础增量为 +3／+2／+1。
- 正向增量复用既有 `SourceItemFriendshipRules`：先对基础值应用安抚之铃的 150% 向下取整，再加豪华球与相同相遇地图区域各 +1，最后钳制至 255。真实球种与相遇区域来自既有奖励元数据；符合类别却缺失这些事实时，入口原子拒绝，不能假定没有加成。
- `src/battle_script_commands.c:2872–2878` 在每次实际玩家倒下时调用 `AdjustFriendshipOnBattleFaint`。`src/battle_util2.c:78–107` 比较当前战斗体等级：单打用敌方左位置，双打用敌方两位置中较高等级；已经倒下但保留的敌方战斗体仍参与比较。
- 对手高出至少 30 级时，亲密度低于 200 扣 5，否则扣 10；其余情况扣 1。负向变化没有安抚之铃、豪华球或相遇区域加成，最低为 0。数值表见 `src/pokemon.c:1617–1630`。

当前内容中 22 个联盟进度相关训练家定义全部为单打；普通及再挑战单打均经过 `SourceTrainerEncounterEntry`。现有双打没有遗漏的正向入口类别，不增加地区或训练家 ID 例外表。研究所首战保留独立教学结算：双方固定等级 5，原有倒下 −1 正确。

## 共享事件边界

招式倒下由 `SourceFrameRecipeContext.ForestFaint` 负责，残留伤害与入场陷阱由 `SourceFrameEndTurnContext.FaintAs` 负责。两处在实际倒下命令内修改同一个永久成员，既有队伍变化回执随后携带结果。没有新增 RNG 调用、回执类型、持久防重集合或另一个数值权威。

旧 `FaintedMembers` 按成员 ID 永久标记本场已倒下者，导致复活后再次倒下不扣亲密度；同时单打只扣固定 1，双打没有相同扣减。该字段、全部入口初始化与两个 Application 扣减分支已经删除。新处理随每次真实倒下回执发生，旧 continuation 重放仍由现有 revision／事件序号边界拒绝。

正常入口与四位置生命周期要求每个布局内位置保留战斗体。底层独立招式内核可以接受不完整位置；如果倒下计算缺少对面位置的真实等级，就原子拒绝该操作，不把缺失等级猜成零。

## 战斗体与验证范围

Return／Frustration 的原作伤害和动画读取战斗体缓存的亲密度。入口变化发生在初始战斗体复制前；倒下处理只修改已经死亡的战斗体及其原始永久成员。Transform 的借用对象快照和敌方亲密度不变，复活／换人从永久队伍重新加载。既有药品／升级路径对存活战斗体缓存的同步未在本次扩大重构。

新增 `SourceBattleFriendshipTests` 为 18 个展开用例，覆盖三个入口类别、阈值和正向加成、倒下后备、真实赠送蛋、单／双打、残留、复活草复活后再倒下、Transform 原始所有者、入口后真实击倒与升级顺序、过期重放及缺失对面等级的原子失败。受控 HP／EXP／阶段仅为内核测试，不代表自然剧情通关。

验证：完整解决方案构建 0 warning / 0 error（94.35 秒）；新增亲密度与 TrainerPolicy／QuestOpponentFaint 筛选 29/29（5 秒），扩展 Lifecycle／Residuals／Hazards／Switching 115/115（6 秒）。Application 的相关战斗回归通过；同批 Day-Care 新夹具的独立失败另行处理。Presentation Summary／FriendshipContact 33/33（6 秒）。初跑的两个测试前提错误分别是裁剪队伍未裁剪 reward metadata，以及携蛋入口误用没有 breeding 数据的目录；修正夹具后保留全部行为断言。扩展回归初跑缺少本地 source-switch-vectors，按已有生成器生成 126 项后全绿，文件仍被 Git 忽略。没有原生画面或扬声器验收。新增测试筛选为 Battle 项目的 `FullyQualifiedName~SourceBattleFriendshipTests`；回归覆盖 `TrainerPolicyTests`、`QuestOpponentFaintTests`，以及 Application 的 `WildBattleFlowTests`、`TrainerBattleTests`、`BatonPassAnimationKernelTests`、`FriendshipContactAnimationKernelTests`。未改写 golden 或历史存档。
