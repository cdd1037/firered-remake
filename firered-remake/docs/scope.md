# 产品范围：完整 FireRed 单机重制

状态：范围合同，非实现报告。参考版本和来源证据见 [source-baseline.md](source-baseline.md)。本文件落实用户的完整单机目标，替代早期设计方案中“首徽章之后再决定是否完成关都”的产品边界。第一徽章是里程碑，不能作为最终交付或缩减完整目标的依据。

## 最终目标

Godot 4.7.2 .NET / net10.0、AI-first 工程，完成英文 FireRed revision 0 的关都主线、地图和剧情、正常单机可到达的七岛及通关后玩法，并能保存、退出和恢复。简体中文为独立表现层。兼容模式保持已确认的原版规则；修正或便利功能使用显式规则版本，不默默改变基线。

“完整单机”是原版正常单机可达功能与分支的完成，不是把仓库里的所有数据无条件开放。完整完成率尚未测量；原始地图数量不能代替可达分母。每条下面的条目都是待实施要求，不表示当前工程已经可玩。

## 1. 必做内容

| 范围 | 最终验收目标 |
| --- | --- |
| 关都主线 | 从新游戏、初始伙伴和包裹，经过八道馆、火箭队事件、联盟四天王与冠军，进入名人堂并正常继续游戏 |
| 完整世界 | 主城、道路、建筑、洞窟和原版正常可达支路，所有关联 NPC、道具、出口、机关和剧情重入条件 |
| 前期七岛 | 正常获得的三岛船票及一至三岛剧情、救回迷路女孩等单机事件；访问顺序按原版条件，不凭“通关后区域”标签统一推迟 |
| 全国图鉴与四至七岛 | 保留图鉴/剧情门槛、红宝石和彩虹通行证、蓝宝石与火箭队仓库等任务链 |
| 单机后期 | 华蓝洞、对应初始伙伴的游走传说、联盟强化队伍、七岛可达支线/迷宫/遗迹与原版可用训练家塔模式 |
| 系统 | 战斗、双打、训练家 AI、捕获、队伍/盒子、成长、进化、繁育/蛋、背包、商店、金钱、恢复、失败恢复、地图能力与存读档 |
| 单机收集与服务 | 固定遭遇、NPC 交换、赠送、化石、可达野生及进化、招式教学/回忆/遗忘、对战搜寻器、狩猎地带、游戏角等原版单机玩法 |
| 操作与表现 | 地图/战斗/菜单、中文、输入焦点、设置、音画反馈、240×160 逻辑画面与清晰像素缩放；实际运行证据另行验收 |

区域可采用增量交付，原版允许的分支顺序不能强行线性化。双打和后期机制可以在后续里程碑接入，但不能永久从最终范围删去。重复游玩、已领取奖励、输掉战斗、进出地图、读档和异常退出均应保持可继续状态。

## 2. 已确认的条件不能被“联机延后”误删

全国图鉴触发涉及首次联盟后的状态、至少 60 种已捕获，以及一岛访问相关 flag。取得全国图鉴后推进红宝石任务，获得彩虹通行证开放四至七岛。交付蓝宝石会设置通关后状态、开放华蓝洞并初始化游走宝可梦；联盟强化队伍也使用关联 flag。通信设施修复是一条单机剧情，不能因为真实通信协议延后就省略。

依据：[真新镇脚本](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/PalletTown/scripts.inc)、[大木研究所](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/PalletTown_ProfessorOaksLab/scripts.inc)、[一岛宝可梦中心](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/OneIsland_PokemonCenter_1F/scripts.inc)、[科拿房间](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/PokemonLeague_LoreleisRoom/scripts.inc)。

NPC 交换是正常单机服务；它与真实玩家通信交换分开。图鉴数据目录包含的物种，不等于每个初始选择的一份存档都能单机集齐。交易进化、版本独占、互斥赠送和配信门槛保持来源约束。不能自行给火红加入昼夜亲密度进化；原运行代码禁用该分支。全国图鉴前非关都进化的限制也须保留。

依据：[NPC 交换数据](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/data/ingame_trades.h)、[进化判定](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokemon.c)、[进化场景门槛](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/evolution_scene.c)。

## 3. 延后与排除边界

- 真实联机交换/对战、无线大厅、跨游戏连接及需要其他玩家的小游戏协议延后。单机玩家进入相关室内时应有清晰可退出的界面，不伪造已联网。
- 依赖外部分发的票据、神秘礼物、诞生之岛、肚脐岩等内容单独登记为配信范围，暂不计普通单机验收。变化洞窟其他遭遇表存在，不等于可以无条件启用。
- 原版 ROM 存档导入导出、原版通信协议互通、云账号和云存档延后。
- LeafGreen、其他世代扩展机制、仓库残留地图/原型和不可达内容不自动纳入 FireRed 正常单机范围。
- 原游戏素材公开发行不是当前任务；来源可读不能替代再分发权利核验。

这不是对未来功能的永久拒绝。新增玩法、开放交易进化或解除配信限制属于显式扩展规则，须与原版兼容模式及覆盖报告分开。

## 4. 第一徽章里程碑的闭包

首个完整流程：真新镇开场和三种初始伙伴分支、研究所劲敌战、常青市包裹、图鉴/精灵球、捕捉与队伍、常青森林、深灰道馆及小刚奖励、保存退出再恢复。包含期间正常可达建筑、NPC、支路和可选战斗。

早期玩家可反复练级，因此可达成长、延迟进化、招式、特性、天气和道具依赖必须展开。禁止隐藏等级上限、跳过原版可发生的进化、无声忽略未实现招式。满队捕获、PC 边界、研究所特殊失败、教程隔离、连续升级、取消学招/进化、战败、中毒行走、背包满补领和奖励幂等均进入验收。

首徽章通过后继续按同一整体目标开发，不再把完整关都或七岛视为未决定的产品范围。P0 未通过时仍不得批量导入。

## 5. 完成的证据定义

每个地图、事件、规则、资源和服务都需 stableId、固定 sourceRef、可达条件、依赖、实现状态和测试证据。全流程以正常操作完成，无编辑器补 flag、人工修档或隐藏调试跳关。可达闭包中零未解释悬空引用、未知操作码和占位行为；未完成项明确列出。

全游戏验收至少覆盖三种初始伙伴和相应后期分支、合法剧情顺序变化、全部徽章/联盟/七岛关键状态、保存重载、长期成长与收集条件。不会要求普通单机玩家完成本就依赖通信的满图鉴或额外小游戏星；必须保留原版真实判定并清楚区分“逻辑已实现”与“单份单机存档可达”。

## Language target and selected Chinese resource

Chinese is a final delivery target. The user selected Xzonn/PretFRLG chinese commit `a04f63173ca5fcf9c2db18ee223cf057c7f232c0` as the later text basis; a user-supplied Chinese ROM is not required for that route. Current priority remains complete gameplay and comparison with the original English game. Only independently extracted, reviewed text/terminology may be imported; original pret rules remain authoritative, including EXP distribution. See [the decision](adr/0003-chinese-text-baseline.md) and `scripts/recipes/config/localization-zh-Hans.source.json`. Source choice does not mean Chinese menus, dialogue, fonts, line layout or name input are already implemented.

## Input parity follow-up (2026-10-08)

Add mouse clicks to stable dialogue choices, menus, bag/party screens and battle options while retaining keyboard controls. Mouse input must reuse current commands and validation, respect presentation/modal locks and prevent click-through or repeated confirmation. Map click movement/pathfinding follows after map behavior is stable; this does not interrupt the current forest/badge work.
