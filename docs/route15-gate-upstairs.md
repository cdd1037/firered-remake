# 15号路门楼二层

## 原作与玩法

固定来源：pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`，
`data/maps/Route15_WestEntrance_2F/{map.json,scripts.inc,text.inc}`。
地图复用13×11的 `Entrance_2F` 布局；一层 `(9,10)` 的warp 4与二层
`(10,9)` 的warp 0互通。保留原助手NPC、碰撞、楼梯和两个望远镜背景事件。

- 助手统计关都捕获数，满50种赠送一次学习装置（EXP. SHARE）。
  16号路同名局部常量仍是40，11号路仍是30；编译器按源文件展开别名。
- 拒绝、未满50、背包已满均不发物品或置领取flag；腾位后可重试。
  成功后记录 `FLAG_GOT_EXP_SHARE_FROM_OAKS_AIDE`，再次对话只解释用途。
- 左望远镜先显示大鸟文字，再展示急冻鸟图像，等待A或B，隐藏后仅置Seen。
  不增加Caught、不发宝可梦、不创建遭遇、不消耗战斗RNG。
- 右望远镜只显示远方小岛文字。
- 学习装置使用已有持物和经验分配规则，可以Give/Take，领取flag不要求
  物品永久留在背包。

## 共用边界

现有Fuchsia地图转换器将二层从source-only移入拥有的地图清单；Route12仍复用，
其余六张既有地图的运行几何/事件语义未变。不增加地区运行处理器。
旧转换入口的未定义拓扑变量、Git-only依赖和旧目录输出已修复：默认读取本地
固定raw缓存，经现有renderer和 `publish_map_package` 输出，支持图集后的RGBA复核。
可选固定Git来源仍验证HEAD；运行时不读取raw或转换器。

助手直接进入现有 `PokedexAideCatalog`。学习装置经物品取得配方接入共享物品
定义和图标图集；图标/调色板按上述固定commit补齐，未使用ROM。
急冻鸟复用既有Seafoam肖像/Dex资源，不复制另一套物种/图像元数据。
公共 `showmonpic` 使用已有物种目录，不再限制为三只初始宝可梦；公共图片
按键等待和 `SetSeenMon` 执行原脚本。Oak评分服务只认领自己的脚本来源，
不再拦截其他脚本的公共waitbuttonpress。字体和音频均未修改。

## 验证与限制

使用SDK 10.0.401、host/runtime 10.0.12。先 `source scripts/env.sh`：

```sh
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --filter FullyQualifiedName~Route15GateUpstairsTests
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --filter 'FullyQualifiedName~CyclingRoadFlowTests|FullyQualifiedName~GeneratedOnlyCampaignTests'
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --filter FullyQualifiedName~PokedexAideCatalogTests
dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter FullyQualifiedName~ExpShareRewardTests
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --filter 'FullyQualifiedName~Route15GateContentTests|FullyQualifiedName~CyclingRoadContentTests|FullyQualifiedName~GeneratedConnectedMapProjectionTests|FullyQualifiedName~RegionalWarpValidationTests|FullyQualifiedName~CurrentItemCatalogTests'
python scripts/imports/maps/import-fuchsia-route-region.py --check
python scripts/checks/test-route15-gate-import.py
python scripts/checks/test-current-items-import.py
python scripts/imports/items/import-current-items.py --check
python scripts/imports/maps/import-field-tilesets.py --check
python scripts/imports/maps/import-map-atlases.py --check
python scripts/imports/story/import-story.py --check
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

应用定向11项通过，覆盖40/49/50、拒绝、非关都不计入、满包/重试/重复、Give/Take、
双向楼梯、重进与严格存档/Continue、左右望远镜、A/B及拒绝普通ack。
助手领域13项、学习装置奖励3项通过；奖励检查未参战持有者、参战持有者及濒死
不领取经验，复用实际奖励计算，不增加另一套规则。内容与导入复现另有定向检查。

保留初次失败：左望远镜两例被旧Oak处理器抢占已修复；旧图标测试硬编码数量1
在加入第二个图标后失败，已改为与实际配方逐项核对，非改写golden。
最初调用的两条物品检查路径不存在，随后使用实际 `test-current-items-import.py`。
未做默认全量测试、从新游戏自然到达本楼、原生窗口逐帧或真实音频验收；
受控站位测试不等于完整路线通关。未提交/push、未上传素材、未修改用户存档。

2026-10-16最终定向结果：Route15应用11、CyclingRoad24、generated-only应用2、
助手领域13、学习装置奖励3全部通过。内容定向运行分别19和4项通过（含一个重叠
物品/肖像用例，不累计为23个独立用例）；资源入口定向5项通过，其中新地图
Python检查7项、物品Python检查5项。额外脚本布局8项和运行证据退休检查6项通过。
地图/tilesets/地图图集/物品/story再生检查通过，完整solution构建0 warning/0 error。
地图图集报告 `originalPixelParity=false` 表示已有2736张源PNG已经图集化，
本轮比较440张现存PNG并恢复核对2736张图集图片；不把它表述为全部原始PNG仍存在。
