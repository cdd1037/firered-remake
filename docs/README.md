# 文档导航

从[工程 README](../README.md)开始构建与启动。本目录按职责维护当前说明；Git 保存修改历史。旧批次记录只证明当时实际执行的检查，不是当前待办或完成率。

## 开发与维护

- [架构与契约](architecture.md)：模块依赖、命令、RNG、资源、模态准入和存档边界。
- [当前组合与启动](current-game-composition.md)：唯一内容入口、参数、用户存档与显式版本转换。
- [项目质量与维护](project-quality-review.md)：仍需处理的结构问题及每批改动标准。
- [实施顺序](implementation-roadmap.md)：当前优先级、Git 同步和暂停的交付事项。
- [测试入口](../tests/README.md) / [导入工具入口](../scripts/README.md)：命令、输入、项目职责及失败边界。

## 产品范围

- [范围合同](scope.md) / [完整单机分母](full-scope-audit.md)：最终目标、单机与通信/外部输入的区别。
- [当前实现与剩余范围](first-clear-remaining-scope.md)：功能和表现的当前状态，避免按历史缺失表重复开发。
- [非汉化单机审计](nonlocalized-single-player-completion-audit.md)：源入口对照、已查范围与待验证项。
- [战斗矩阵](battle-system-completion-matrix.md) / [动画分派契约](animation-dispatch-architecture.md)：规则、身份覆盖和专属表现的不同口径。
- [中文版](chinese-edition.md)：文本覆盖、生成与运行边界。

## 数据与资源

- [资源再生成](resource-reconstruction-guide.md) / [世界资源管线](world-resource-pipeline.md)：已维护重建目标及尚未闭合的依赖。
- [资源布局与运行边界](resource-layout.md) / [固定来源](source-baseline.md)：raw/generated 职责与研究输入，不授予素材再分发权限。
- [宝可梦准入](pokemon-admission-format.md) / [共享道具](shared-item-catalog.md) / [共享机器](shared-machine-catalog.md)：目录的单一权威与用途差异。
- [地图图集](map-render-atlases.md) / [训练家卡图集](trainer-card-atlas.md)：图像组织与纹理边界。
- [音频](audio.md)：共用合成器、开发工具和按需音频检查。
- [存档格式与完整性](save-storage.md)：schema 3、派生字段、checksum 与主档/备份契约。

## 验证与交付

- [验证与环境恢复](validation.md)：构建、回归、原生覆盖、失败记录与未关闭门禁的唯一汇总。
- 表现契约：[开场](opening-presentation.md)、[概要与学招](summary-native-presentation.md)、[图鉴](pokedex-habitats-and-area.md)、[场景过渡](plain-field-transition-presentation.md)。
- [Linux 独立包验收](linux-export-acceptance.md) / [Windows 状态](windows-export-acceptance.md)：分平台、分版本的实际证据。

专门玩法/画面契约从[当前范围](first-clear-remaining-scope.md)的对应条目进入，避免在导航重抄完整功能清单。当前真实音频、完整自然路线、全部原生状态和 Windows 尚未全面验收；本轮未导出新包。测试通过不能扩大这些结论。
