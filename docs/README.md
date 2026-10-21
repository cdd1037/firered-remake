# 文档导航

从[项目 README](../README.md)了解环境、入口和资源要求。当前契约与历史检查分开阅读：功能页说明实现和限制；带日期的结果只适用于所记录版本，不能作为当前构建或全游戏验收结论。

目录根部只保留项目入口、架构、范围、内容、存档和维护说明。`features/` 存放游戏功能与表现专题，`resources/` 存放资源格式与导入说明，`history/` 保存历史审计、测量和验证记录。

## 开发与维护

| 内容 | 文档 |
| --- | --- |
| 模块、命令事务、RNG 和资源边界 | [架构](architecture.md) |
| 新游戏、启动参数、Continue 和诊断模式 | [游戏组合与启动](current-game-composition.md) |
| 当前问题和开发顺序 | [维护重点](project-quality-review.md)、[优先级](implementation-roadmap.md) |
| 构建、测试、平台状态和证据范围 | [验证记录](validation.md)、[测试入口](../tests/README.md) |
| 存档格式、校验与安全写入 | [存档契约](save-storage.md) |
| 命令版本与旧入口 | [命令与防重](architecture.md#命令版本与防重)、[旧运行入口](current-game-composition.md#诊断与旧入口) |

## 产品范围与来源

| 内容 | 文档 |
| --- | --- |
| 最终目标和排除项 | [范围合同](scope.md)、[完整单机范围](full-scope-audit.md) |
| 功能状态与待验证部分 | [当前功能](first-clear-remaining-scope.md)；以往审计见下方历史记录 |
| 固定来源与规则差异 | [来源基线](source-baseline.md)、[原作行为改进](original-behavior-improvements.md) |
| 战斗规则、特性与动画 | [战斗矩阵与特性验证](features/battle-system-completion-matrix.md)、[动画架构](features/animation-dispatch-architecture.md) |

## 内容与资源

| 内容 | 文档 |
| --- | --- |
| 运行数据和离线转换 | [资源布局](resources/resource-layout.md)、[再生成说明](resources/resource-reconstruction-guide.md)、[工具入口](../scripts/README.md) |
| 物种准入、共享定义和本地化 | [内容组合](current-content-composition.md)、[道具](resources/shared-item-catalog.md)、[机器](resources/shared-machine-catalog.md)、[源常量](resources/shared-source-constants.md) |
| 地图、布局变体和图集 | [世界编译](resources/world-resource-pipeline.md)、[地图图集](resources/map-render-atlases.md)、[地图表现绑定](resources/current-map-presentation-bindings.md)、[图像资源](resources/generated-image-assets.md) |
| 音频运行库和导入 | [音频](resources/audio.md) |
| 状态保留 | [所有权与遇敌历史](features/wild-history-retention.md) |

## 游戏系统与界面

| 主题 | 文档 |
| --- | --- |
| 开场与操作 | [片头](features/intro-movie.md)、[开场](features/opening-presentation.md)、[命名](features/shared-naming-presentation.md)、[按键模式](features/button-mode-help.md)、[Help 内容](features/help-content.md) |
| 背包与设置 | [库存界面](features/inventory-presentation.md)、[PC 物品](features/personal-pc-inventory.md)、[场外物品绑定](current-content-composition.md#场外与物品绑定)、[战斗与声音设置](features/battle-scene-sound-options.md)、[树果丢弃](features/battle-berry-pouch-toss.md) |
| 队伍与成长 | [队伍](features/party-screen.md)、[Summary](features/summary-native-presentation.md)、[进化与孵化](features/evolution-and-hatch-presentation.md)、[NPC 交换](features/npc-trade-native-scene.md)、[PC 矩形移动](features/pc-rectangle-move.md)、[盒子交付对白](features/box-delivery-dialogue-branch-audit.md) |
| 图鉴与收集 | [栖息地与分布](features/pokedex-habitats-and-area.md)、[叫声和排序](features/pokedex-cry-and-orders.md)、[证书](features/pokedex-diploma.md)、[训练家卡和照片](features/trainer-card-and-photo-printer.md)、[贴纸](features/sticker-man.md)、[博物馆](features/museum-fossil-pictures.md)、[徽章话题](features/badge-topic-list-menu.md) |
| 战斗表现 | [捕获球](features/capture-ball-presentation.md)、[替身](features/substitute-move-controller.md)、[逃离战斗](features/move-escape-presentation.md)、[亲密度](features/battle-friendship.md)、[联盟与片尾](features/league-credits-presentation.md)、[动画背景](resources/source-animation-backgrounds.md) |
| 场外行为 | [非战斗技能](features/noncombat-field-moves.md)、[Cut 和树果粉](features/field-cut-and-powder.md)、[冲浪钓鱼](features/surf-fishing.md)、[隐藏拾取](features/hidden-pickups.md)、[探宝器](features/itemfinder-presentation.md)、[随机对白](features/story-random.md) |
| 地图表现与回顾 | [场景过渡](features/plain-field-transition-presentation.md)、[图块编辑](features/shared-metatile-presentation.md)、[跳跃](features/shared-field-jump-presentation.md)、[Quest Log](features/quest-log.md)、[回顾调色板](features/quest-log-palette-presentation.md) |
| 其他服务 | [Easy Chat](features/easy-chat-profile-service.md)、[人物收集器与老虎机](features/fame-and-slot-presentation.md)、[Daisy 与设计师](features/daisy-and-designer.md) |

## 地区与支线

[普通室内与商店](features/ordinary-interiors-and-mart.md)、[15 号路门楼](features/route15-gate-upstairs.md)、[16 号路门楼](features/route16-gate-upstairs.md)、[三岛隧道](features/three-island-tunnel.md)、[宝物海滩](features/treasure-beach.md)、[水之小径民居](features/water-path-houses.md)、[外缘岛与变化洞窟](features/outcast-island-altering-cave.md)。这些专题说明局部行为，不代替完整路线验收。

## 历史记录

以下记录保留当时的测量、失败和验证条件，不提供当前开发顺序：

- [内容重构测量与验证](history/content-and-ownership-refactor.md)
- [导入工具与资源重构](history/script-consolidation.md)
- [早期确定性规则与夹具](history/rule-fixtures.md)
- [单机审计与后续核对（截至 2026-10-19）](history/nonlocalized-single-player-completion-audit.md)
- [2026-10-16 内容窗口检查](history/content-native-visual-check.md)
- [2026-10-16 界面窗口检查](history/ui-native-window-check.md)
- [2026-10-19 Linux 独立包验证](history/linux-export-acceptance.md)
- [移除的测试与原因](history/test-retirement-20261015.json)

“受控测试存档”是为特定检查准备、并通过状态校验的输入，不代表自然游玩历史。“原生窗口检查”指运行真实 Godot 窗口；headless 指无窗口运行；Dummy 音频不能用于真实声卡听感验收。

## 文档维护

README 保留环境和常用入口，架构页维护跨模块契约，功能页说明具体行为与限制。测量或验证记录须标明日期、版本、输入和未覆盖范围；保留失败记录，不累计历史通过数。临时同步、备份或发布安排留在对应任务中，不写成长期项目规则。合并或移除页面时同步修复文件链接和标题锚点。
