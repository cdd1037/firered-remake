# 16号路门楼二层

## 玩法与来源

固定来源为 pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`：

- `data/maps/Route16_NorthEntrance_2F/map.json`、`scripts.inc`、`text.inc`
- `data/layouts/Entrance_2F`，13×11；楼梯连接一层 warp 4
- 三名原NPC：男孩、女孩、大木博士助手；两个望远镜背景交互
- 助手使用 `GetPokedexCount` 的关都捕获数（选择器0），满40种赠送护符金币
- 拒绝检查、未满40、背包已满均不发放、不置取得flag；成功后持久标记
  `FLAG_GOT_AMULET_COIN_FROM_OAKS_AIDE`，再次对话只解释用途
- 物品可交给宝可梦；取得标记不要求物品永久留在背包

脚本blob：`c96c1c5893d8483ac0593e27428ee0b843792c50`；
文本blob：`a7a344a74b6272cb814b7d5a6acff118db6a2789`。
护符金币原图blob：`2d6b3d5691596c98cf10f7c7b932701d4a0c8029`；
调色板blob：`b1d8233038dd41259ca7e6fa52e9f3fb44734588`。
这些仅是导入来源记录；运行不检查或读取原始文件。

## 共用接入

地图由现有 Cycling Road 转换器输出，加载器读取地图清单及符号映射。
不增加楼层专用传送或碰撞分支。骑行场景在门楼内保持的有效范围，
由设置场景的原坐标触发器及室内warp闭包生成，移除了此前按楼层列举的检查。

`PokedexAideCatalog` 在组合时从原NPC绑定及已编译故事IR提取捕获阈值、
物品、数量、取得flag和可达效果。公共助手处理器执行计数、文本缓冲及普通
背包事务，拒绝无效脚本／物品绑定。现有助手处理器保留原分派顺序；
未新增地区名白名单、另一份阈值表或存档验证豁免。

护符金币通过现有物品取得清单加入，定义复用共享物品目录。
可选图标选择同样属于取得配方；通用图标导入器解析原图标表与调色板，
输出由既有背包表现读取的图标记录并合入共享图集。没有楼层专用UI加载器。
物品的 `HOLD_EFFECT_DOUBLE_PRIZE` 继续使用已有战斗奖金规则。

公开Give测试同时暴露旧持物投影会再次加入已存在的TM CASE。已删除这层过时
投影／合并，让Give、Take、邮件和PC持物操作直接使用经过验证的完整库存；
不为护符金币绕过重复栈或容量检查。

## 验证范围

应用测试用受控站位和公开命令检查双向楼梯、道路场景保留、严格存档／继续、
两名游客、两个望远镜、39/40捕获边界、拒绝、满包／腾位重试、重复领取及携带物品。
领域测试检查无关地图名和不同阈值／数量，以及错误脚本、容量分支、物品定义拒绝。
内容与导入检查包括地图几何、NPC绑定、共用库存、图标RGBA及生成资源单独加载。

这不代表从新游戏自然走完整条路线，也不代表原生窗口逐帧显示或实际音频验收。
字体与音频未修改；没有下载ROM、公开上传素材或修改用户存档。

复现入口（工程根，先 `source scripts/env.sh`）：

```sh
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --filter FullyQualifiedName~PokedexAideCatalogTests
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --filter FullyQualifiedName~CyclingRoadFlowTests
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj
python scripts/imports/maps/import-world.py --check
python scripts/imports/items/import-current-items.py --check
python scripts/imports/maps/import-field-tilesets.py --check
python scripts/imports/maps/import-map-atlases.py --check
python scripts/imports/story/import-story.py --check
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

2026-10-16本地结果：SDK 10.0.401、host/runtime 10.0.12；完整solution构建
0 warning／0 error。Application全套272、Content全套220、ResourceImport全套25、
独立助手领域13项全部通过，无失败或跳过。最后补充Take断言后定向应用24项再次通过；
该24项属于272项中的重跑，不累计。地图、物品图标、tilesets、地图图集及story
再生检查通过。初次失败记录仍保留：缺物品接入、测试站位／楼梯方向、旧总数断言、
测试API调用，以及实际持物重复TM CASE错误均已修复再测。
