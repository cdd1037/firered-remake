# 冲浪状态下钓鱼

2026-10-16。此入口沿用现有 `FishingTaskProfile`、三钓竿定义及普通／狩猎两类战斗结算，没有地图名单或新增源表。字段状态仍由 `CampaignState.IsSurfing` 持有；钓鱼任务不切换移动模式，也不新增存档格式。

## 原作规则与实现

固定原始输入 `037335f4` 的 `src/item_use.c::CanFish` 区分岸边与冲浪：

- 瀑布、潜水状态拒绝。
- 岸边沿用玩家 elevation 3、水 terrain 2、计算碰撞为 elevation mismatch。
- 冲浪只检查前方 `MetatileBehavior_IsSurfable` 和原始地图 collision=0，不用岸边的 elevation／人物碰撞结果。
- 原作 `MetatileBehavior_IsBridge` 恒为 false，因此不增加桥例外。

`OldRodCastContext.FrontMapCollision` 明确区别于 `FrontCollision`。原作全局 surfable 判定集中在 `FieldWaterRules`；原 `SeafoamFieldRules.Surfable` 仅委托共同判定，保持原语义。

任务推进、鱼咬钩、个体生成与 RNG 顺序不因 Surf 增加分支。普通捕获继续使用普通球与奖励；狩猎钓鱼使用狩猎球，不增加经验。现有回场结算保留站位和 Surf 标志，并恢复 `FieldTravelMusic`。

Godot 钓鱼姿势替换骑手而保留冲浪底座，继续推进底座动画并叠加原作底座相位的一个像素偏移；无咬钩或入战恢复普通骑手的既有任务边界不变。没有字体、音频或资源修改。

## 验证

SDK 10.0.401，目标／测试运行时 net10.0。入口：

```sh
source scripts/env.sh
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --no-restore -v minimal
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~SharedFishingFlowTests|FullyQualifiedName~SafariFishingFlowTests' -v minimal
dotnet build src/Game.Godot/Game.Godot.csproj --no-restore -v minimal
```

最终实跑：Domain 全项目 231/231、两个 Application 钓鱼类 37/37；Godot 构建 0 warning / 0 error。没有跳过测试。

- `SurfFishingTests` 覆盖原作全部 surfable metatile、瀑布／潜水／缺竿／阻挡拒绝，明确计算 collision 不替代原始 collision；三钓竿有鱼／无鱼任务逐步比较岸边和 Surf RNG。
- `SharedFishingFlowTests` 增加三钓竿 Surf 公开背包入口、实际普通捕获、站位／Surf／音乐回场和严格存读。
- `SafariFishingFlowTests` 增加 Surf 旧／超级钓竿实际捕获及好钓竿逃跑回场，保持 Safari 资源、无普通奖励和退休后存读。
- 初次新增 Domain fixture 使用空 table 集合，三行被内容约束正确拒绝；已改为有效表加无鱼地图，而非放松产品验证。

这些是受控站位和合法命令流，不是从新游戏自然取得钓竿并走完路线。未进行本轮原生窗口截图、逐帧画面对照或实际音频验收；未据此声称所有地图位置、海上路线或未加载地图已开放。
