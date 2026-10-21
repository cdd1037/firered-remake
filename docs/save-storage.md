# 存档格式与安全写入

此页描述 `SaveCodec`、`SaveService` 和 `PlayerSaveOperations` 的现行契约。启动和版本选择见[游戏组合](current-game-composition.md)，状态事务见[架构](architecture.md)。

## 格式与版本

存档是可读、未压缩的 JSON。外层 `SaveEnvelope` 包含：

| 字段 | 含义 |
| --- | --- |
| `SchemaVersion` | 当前为 3；旧 schema 不自动转换 |
| `RulesVersion` | 当前规则身份 `firered-current-campaign-v4` |
| `ContentVersion` | 当前内容身份 `firered-current-content-v4` |
| `GameEdition` | `firered` |
| `Generation` | 非负存档代次 |
| `PayloadHash` | 规范化载荷的校验值 |
| `Payload` | 游戏状态 |

读取时检查必要外层字段、版本、代次和载荷校验值，再反序列化并执行传入的业务校验。校验值正确不代表剧情、所有权或个体状态一定合法。

写入只省略三个能从其他字段精确还原的属性：`SixStats.Values`、`PokemonInstance.IsShiny` 和 `CampaignActor.Visible`。这个规则仅用于存档编码，不改变完整状态诊断 hash 或内容序列化。

## 主档、临时文件与备份

1. 校验状态，编码后做一次读取校验。
2. 经存储适配器写入同目录的 `<slot>.tmp`，读回并检查字节完全一致。
3. 检查已有主档。只有通过当前版本、完整性和业务校验的主档才允许更新 `<slot>.bak`；损坏主档不能覆盖有效备份。
4. 调用适配器提交主档。IO 或权限失败须返回明确结果；一旦开始提交，不能在没有证据时声称文件完全未变。

加载按主档、备份的顺序尝试，返回实际选中的来源。主档不可读、损坏或无效时可以尝试备份；加载不写回、不自动修复原文件。Continue 还会读回选中的候选并比较字节指纹，防止检查期间文件被替换。

玩家仅能在允许保存的稳定状态保存。诊断快照不提供战斗中任意存档权限。

## 旧档与测试输入

Continue 拒绝 schema 1/2、不匹配版本和缺失必要统计的状态。离线转换与手填孵蛋总数工具已移除；不得从现有蛋、队伍、盒子或残留记录猜测玩家的终身孵蛋次数。

generation-57604 样本是历史输入，不能作为当前 Continue 基线。测试中的合成计数仅用于受控夹具，不能补写玩家历史。缺失输入应报错，不能跳过后称为通过。

实现入口：[编码与存储服务](../src/Game.Application/Persistence/SaveCodec.cs)、[玩家存读操作](../src/Game.Application/Persistence/PlayerSaveOperations.cs)、[错误结果](../src/Game.Application/Persistence/SaveOperationResult.cs)。检查命令和输入要求见[测试说明](../tests/README.md)。
