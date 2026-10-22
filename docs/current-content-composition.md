# 内容定义与准入

`PretCampaignCatalog.LoadCurrent(projectRoot)` 从 `content/generated` 读取当前游戏资源。原始来源获取和转换由离线工具负责，游戏不读取 raw、导入配方或历史资源包。当前构建问题见[验证记录](validation.md)，本页描述数据契约，不表示当前源码已构建通过。

## 共享定义

- 物种、招式、特性和形态机制使用共享定义；物种存在不等于玩家能取得。
- 道具和机器共用业务目录，地区数据只描述真实取得方式、商店货物和表现。原版 50 个 TM 和 7 个可用 HM 保留顺序与兼容性；存在 HM08 数据不代表允许教学。
- 训练家引用共享定义，保留编码姓名、原 ID、队伍顺序和生成参数。自然招式来自共同学招表，自定义招式使用显式槽位，不维护另一份推导证明表。
- 培育、NPC 交换、持物和战斗机制各有明确数据，不通过目录所有者注册表或前驱目录链重建。

## 宝可梦准入

[`PretGamePokemonCatalog`](../src/Game.Content/Catalogs/Pokemon/PretGamePokemonCatalog.cs) 读取 `content/generated/data/pokemon/game/catalog.json`，当前资源 schema 为 2，与存档 schema 3 是不同的版本。

`Definitions` 描述准入物种的稳定 ID、进化和可选 `FormScope`；基础属性与形态差异从 canonical 定义解析。`Owned`、`Opponents` 和 `TrainerTowerOpponents` 分别给出有序用途列表及明确的进化覆盖。读取器拒绝空定义、重复 ID、无效形态和不完整引用。

玩家持有集合、普通对手集合和训练家塔对手集合不可互换。对手能使用某个物种或招式，不代表玩家能捕获或取得；取得还须有合法地图、遇敌表、交换、赠送或进化来源。数据定义在组合时校验，玩家操作再检查当前状态。

## 运行边界

文件名、地图身份、尺寸、遇敌概率和实际脚本分派键属于运行数据。源文件清单、转换回执、历史 hash 和地区累计数量不构成玩法准入条件。拓扑端点和脚本源符号仍有业务含义，不能因为名称含 `source` 就移除。

转换资源按 data、maps、sprites、battle、ui、audio 分类。部分服务仍将道具和图像放在同一包内，这是现有布局限制。详见[资源布局](resources/resource-layout.md)、[共享道具](resources/shared-item-catalog.md)、[机器目录](resources/shared-machine-catalog.md)和[再生成说明](resources/resource-reconstruction-guide.md)。

## 文本与本地化

简体中文是目标语言，选定文本来源为 Xzonn/PretFRLG `a04f63173ca5fcf9c2db18ee223cf057c7f232c0`，选择记录位于 `scripts/recipes/config/localization-zh-Hans.source.json`。汉化文本不改变 pret 原版规则、经验分配或内容准入，也不要求用户提供中文 ROM。

现有调用和布局测试覆盖 Unicode 名称、CJK 换行、Summary 固定窗口和补充字形。`SourceTextPack`、`PresentationTextCatalog` 及共享名称投影已恢复，位于 `Game.Content/Text/`，稀疏覆盖继续校验稳定 ID、参数和控制码。`dev run --language zh-Hans` 检查 `content/generated/localization/zh-Hans/font/cjk-font.json` 并设置 `FIRERED_TEXT_PACK`；配方中提到的部分中文转换工具仍未恢复，不能仅凭 CLI 参数认定完整语言包可重建或已完成中文覆盖。

完整语言包仍需分别验证缺字、覆盖范围和实际窗口。原版标题和图片的选择不因语言包自动替换。历史中文接入记录不作为当前运行通过的证据。

## 存档与验证

当前规则身份为 `firered-current-campaign-v4`，内容身份为 `firered-current-content-v4`，存档是 schema 3 JSON。Continue 严格校验版本与状态；旧档转换和手填孵蛋计数工具已移除。generation-57604 历史样本不能作为当前 Continue 基线，见[存档契约](save-storage.md)。

过去的资源体积、分配量和测试结果已移至[历史重构记录](history/content-and-ownership-refactor.md)。完整冷重建、原生画面、自然路线和实际音频仍须独立验收。

## 场外与物品绑定

共享道具和机器目录提供定义、袋分类、兼容性与实际服务引用。操作保留数量、容量、消耗时机、不可用场景、HM 限制和物种准入；糖果、药品和进化石按当前目录和真实进化边执行，不依赖旧地区人数、目录所有者注册表或前驱投影链。

草地、水域、钓鱼、碎岩、狩猎、游走和固定遭遇分别保留其真实选表、RNG、起源与场景限制。移除来源字段不代表可以合并不同玩法语义；增加地图应复用共享数据与机制。

原版一致性检查与运行数据合法性分别验证：Domain/Battle 检查规则，Application 检查公开命令和存读，Content/ResourceImport 检查引用与生成。受控夹具通过不等于自然完整路线，详见[测试说明](../tests/README.md)。
