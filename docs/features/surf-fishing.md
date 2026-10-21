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

## 2026-10-19 跨连接岸边上水／离水

普通连接目的格现在由 `ConnectedFieldRules.ResolveDestination` 统一解析，Surf 与普通步行复用源连接段顺序。碰撞、占位和移动模式准入仍由各自命令负责。固定源 `field_player_avatar.c:1209–1226,568–605` 的两个跳跃均不计走路步数；`event_object_movement.c:4830–4843` 与 `fieldmap.c:574–628,656–716` 提供跨连接位置换算依据。

Surf 跳跃期间保持原地图的位置、对象和编辑状态，表现目的地使用原地图坐标系中的相邻一格；相关完成回调重新验证真实目的地，释放 Surf effect 后走共享地图进入流程。这样不会在跳跃中把骑手／底座拉过整幅目标地图，也不会让目标地图脚本在旧 effect 锁内运行。目标对象、进入脚本、音乐和源 roamer 转移继续执行；没有额外步行、遭遇抽取或步数。完成时记录既有 ConnectedEdge/Immediate/Plain 证据；Quest 使用同一跳跃轨迹和真实地图投影。

验证采用现有真实 HM／徽章／捕获来源的检查点，在 Cinnabar Center 出口受控放置后通过公开移动到北岸两格，分别覆盖交互及 party field-move 入口、32 帧上水、反向离水、水上存读、到达脚本和 Quest 回放。FieldMove 按源规则不能启动新 recap，因此测试先以真实 party_swap 开启记录。没有伪造剧情胜利或新增地图特判。

- 全解决方案构建：0 warning / 0 error，95.35 秒。
- Domain 连接／SurfFishing：27/27；既有 TreasureBeach／OutcastIsland 公开 Surf 回归：29/29，21 秒。
- 新增 ConnectedSurfRouteTests：6/6，9 秒，覆盖两条公开往返和四方向显示投影。
- Godot 4.7.2 / .NET 10.0.12 headless 主场景：8 条命令全部 Accepted，Cinnabar (14,0) → Route21 South (14,49) → Cinnabar (14,0)，相关显示完成回调自然到达，正常关闭，无错误或泄漏警告。
- 初次外部探针把省略的 null/false JSON 字段当作必填，及初次测试遗漏 Center 南向箭头第二步／recap 启动前提的失败证据保留；只修正夹具，未放松产品断言。

这是源规则、公开命令、严格存读和 headless 主场景证据；原生 GPU 图像、扬声器音频和所有连接地图逐一验收仍未执行。
