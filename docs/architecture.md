# 当前架构与边界

本页描述实际模块和必须保留的契约，不把建议设计写成已实现。功能范围见[当前范围](first-clear-remaining-scope.md)，待处理结构问题见[质量与维护](project-quality-review.md)。

## 模块与组合

项目是模块化单体，维护六个产品/工具模块：

- `Game.Domain`：仅依赖 BCL 的确定性规则、不可变状态与显式 RNG，不引用其他产品模块。
- `Game.Application`：引用 Domain，负责应用命令、剧情/战斗/场外流程、事务与存档服务。
- `Game.Content`：引用 Domain，读取转换资源、校验引用并组装规则目录。
- `Game.Audio`：共享的 BCL 合成器与音频资源读取；游戏、AudioTool 和音频测试使用同一实现。
- `Game.Godot`：输入、显示、音频与平台适配，组合 Application、Content 和 Audio。
- `Game.Tooling`：引用 Application/Content 的显式开发命令，不是玩家运行入口。

子项目目录职责见[应用层说明](../src/Game.Application/README.md)、[内容层说明](../src/Game.Content/README.md)和[表现层说明](../src/Game.Godot/README.md)；各页同时记录编译输入、资源路径或测试链接的维护要求。

Domain 仍内嵌生成的训练家 AI 程序，因此没有产品 `ProjectReference` 不等于构建无需生成输入。工具链固定为 Godot 4.7.2 .NET / `net10.0` / 实际 .NET 10 宿主；环境入口见[工程 README](../README.md)。

当前只有一份完整游戏组合，由 `PretCampaignCatalog.LoadCurrent` 加载。创建/恢复绑定同一目录；恢复不再次执行剧情或消费 RNG。不保留历史地区开关、前驱目录链或多套物种/机器投影，真实徽章、剧情和图鉴解锁仍按玩家状态判断。见[组合与启动](current-game-composition.md)。

## 状态、命令与时间

- 使用稳定字符串 ID、不可变记录/集合、显式 RNG、带期望 revision 与 rules/content 版本的命令及类型化事件。拒绝过期或不相容请求，只有完成的 Accepted 决定发布新状态。
- `CommandId` 是跟踪标签，不是永久或会话内去重键。一次性奖励、捕获和训练家结算依赖业务状态/flag 防重；相同标签配当前 revision 不必然是重复操作。
- 日常命令不计算整份状态 hash；显式诊断、确定性比较和存档完整性各在自身边界计算。关闭日志不关闭保存校验。
- 普通单/双打共享 `SourceBattleFrame` 四位置内核。首个研究所战斗仍使用 `FirstBattleRules` 教程执行器；狩猎等模式保留不同业务状态，不宣称所有模式同一执行器。
- 普通单打、双打与 Teachy TV 表现直接消费同一 canonical 事件类型，不保留旧事件 DTO 往返适配。事件游标只确认一次；换人文字子页及教程步骤是表现状态，不制造额外战斗回执。显示身体从事件前种子与实际回执推进，奖励按对应级别页面更新，不读取最终 Frame 猜先前状态。
- Godot 渲染、音频和动画不决定伤害、RNG 或结算。游玩时钟按实际收到的逻辑 tick 提交，不用墙钟补算漏帧；表现层只管理演出和必要完成回调。

## 内容与资源

运行只读取 `content/generated` 和 Godot 自身素材，不解析原作 C/ASM、raw 或导入配方。来源版本、文件清单和转换选择留在离线工具；不以历史数量、来源 hash 或上一阶段证明代替业务合法性。

Quest Log 的显示名称可覆盖来源身份，但名称存在不等于地图已挂载。带真实目录的场景与过渡验证必须确认实际地图准入；不带目录的结构检查不声称游戏准入。

不可变定义在加载/组合时校验并解析引用，玩家操作检查当前状态和行为。 普通地区野生个体复用当前生成与初始个体规则，不保留无调用的历史地区工厂；真实地区 origin、精确表／槽与特殊生成规则仍保留，不能用“目录有此物种”代替来源准入。普通训练家按实际调用与业务身份登记/结算；陆地、Surf 和钓鱼绑定实际已验证的地图、表及候选。物种存在不能绕过遇敌来源。新增内容复用共享机制与数据契约，不增加地区白名单、重复数值表或跨地区豁免。

玩家/对手宝可梦定义通过共享定义、有序用途引用及显式进化差异表达，不能把敌方能力扩为玩家取得资格。持物可用集合由实际物品/机器定义在组合时生成，保留机器优先、HM/关键物品/Enigma 限制，邮件独立判断；取得来源必须落入同一库存/持物目录。见[准入定义](current-content-composition.md#宝可梦准入)、[共享道具](resources/shared-item-catalog.md)与[机器目录](resources/shared-machine-catalog.md)。

库存结构先校验，添加/取下/交换/PC/拾取共用原子事务，失败不丢道具或个体。当前队伍、盒子、培育屋和 PC 手持个体构成所有权；一次性履约、图鉴、统计和名人堂快照是独立业务事实，不能因字段名含 History/Receipt 就删除。

资源包读取类型化描述后释放 JSON，像素按需加载；同包 indexed 优先于旧 RGB 后备，不同包冲突仍拒绝。图集复用场景纹理所有权，不增加长期 CPU 像素缓存。静态叫声样本可复用，可变声音、时钟和混音反馈必须独立；退出释放资源，不重新解码。运行边界与未闭合的冷重建条件见[资源说明](resources/resource-layout.md)和[再生成说明](resources/resource-reconstruction-guide.md)。

请求地图的命令内缓存须保留实际适配器顺序。动画依赖的解析结果可按场景生命周期共享，但调色板等可变状态仍由单次调用持有，不能跨次复用后互相污染。

## 存档与交互准入

存档为可读 schema 3 JSON，包含 rules/content/edition、generation、payload 和 checksum。拒绝未知、损坏或语义非法状态；同目录 staged 写入并校验，只用有效 primary 更新 backup，不能让坏主档污染好备份。

普通 Continue 严格拒绝旧 schema 和不匹配版本，不猜测昵称/孵蛋历史。旧档转换与统计补写工具已移除，原文件保持不变；不维护旧档兼容层。写入时仅省略三项可精确重算的派生字段，仍先校验原始 payload；完整状态诊断 hash 与载荷 checksum 不可混用。见[存档格式与完整性](save-storage.md)及[当前存档边界](current-game-composition.md)。

战斗及未稳定交互状态禁止玩家保存，诊断快照不是玩家随时存档权限。菜单准入由 Application 提供字段级判断，Godot 另管动画、淡入淡出和焦点；Help、preflight 与保存不共用一个万能布尔条件。模态覆盖要保留底层阶段与状态，失焦清理按住/待消费输入，不重启文本或菜单。连续性修正和验证边界见[画面连续性检查](validation.md#受控原生检查)。

## 命令版本与防重

`CommandId` 只用于跟踪，不是历史命令去重键。`CommandSession` 只持有当前不可变状态和 `revision`，不保存旧完整状态或固定长度命令队列。

规则版本或期望 `revision` 不匹配会拒绝；只有 `Accepted` 的完整决定发布新状态并递增版本。拒绝或故障不能发布部分业务修改。一次性奖励、捕获、训练家结算和物品所有权由各自业务状态防重，复用跟踪 ID 不等于复用旧结果。

完整状态 hash 用于显式诊断、确定性比较和存档完整性，不是日常命令提交的前提。存档保存业务状态和 generation，Continue 以这些值建立新会话。协议检查维护在 `tests/Game.Application.Tests/Protocols` 与 `Engineering`，使用[标准测试入口](../tests/README.md)。
