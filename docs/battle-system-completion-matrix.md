# 当前战斗系统矩阵

更新：2026-10-15。本文与 [机器可读清单](battle-system-completion-matrix.json)维护当前实现边界，不累计历史批次或要求恢复已退休测试。普通单／双打规则阶段已达到既定出口，主线及后期业务已经继续推进；不再沿用“第五徽章阶段仍在开发”的历史状态。

<a id="shared-utilityweather-and-ability-commands-2026-10-11"></a>

## 口径

- 354个招式身份已有规则准入；[AllMoveRecipeTests](../tests/Game.Battle.Tests/Turns/AllMoveRecipeTests.cs)为每招的四个真实出战位置生成受控公共回合用例，共1,416行。它验证实际执行而非仅注册，但不能证明全部组合、随机分支、获得途径或特殊模式。
- 76个有效特性身份已准入，不含NONE和未使用的CACOPHONY；身份准入不是所有入场、复制、天气、免疫和残留交互穷尽验证。
- 专属招式脚本41／354，其余313尚未接专属还原。这是实现范围，不是总体完成率，也不是“全部动画写好只差验收”。
- 定义存在、规则执行、应用入口、专属动画、原生像素一致和真实声音输出分别成立；不能相互替代。本次文档整理不产生新测试通过记录。

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

## 专属动画范围

当前分派支持：Pound、Fire Punch、Scratch、Vice Grip、Cut、Slam、Vine Whip、Stomp、Jump Kick、Rolling Kick、Headbutt、Tackle、Body Slam、Take Down、Double Edge、Tail Whip、Ember、Flamethrower、Peck、Low Kick、Counter、Strength、Growth、Poison Powder、Stun Spore、Sleep Powder、Agility、Quick Attack、Harden、Minimize、Withdraw、Self Destruct、Hi Jump Kick、Flash、Explosion、Slash、False Swipe、Vital Throw、Snatch、Aerial Ace、Iron Defense。

其他招式返回 `script-outside-slice`，不启动该专属脚本；普通文字、HP及结果表现仍独立工作。不能因此说招式规则不可用，也不能把通用反馈当原版专属动画。此名单由当前单／双打分派代码核对，JSON逐招标记相同范围。

用户于2026-10-16要求完成除汉化外的单机内容，专属动画与剩余原生表现重新进入当前工作范围。采用共享实现与代表性检查，保持实现范围和已验证范围分开。[Options玩家入口](../src/Game.Godot/CampaignOptionsView.cs)已开放Battle Scene和Sound编辑并接入保存与运行时应用，见[设置说明](battle-scene-sound-options.md)。

## 验证与规则决定

当前执行入口是 [八个维护MSTest项目](../tests/README.md)，不是已删除的历史Console套件。维护用例含受控队伍、HP、位置与随机状态，不能把总通过行数解释为自然玩家路线数量。源向量、逻辑测试、headless Draw、原生窗口及真实扬声器是不同证据；Dummy音频不能关闭实际输出门禁。

已批准的概率／Protect／Conversion 2／双打KO／王者之证／AI与服从规则差异保持不变，详见 [当前规则决定](first-clear-remaining-scope.md#中文与规则决定)。未批准候选不得由整理顺带实现。全游戏功能缺口、中文、通信进化服务待讨论及当前存档谱系边界也统一见该页。
