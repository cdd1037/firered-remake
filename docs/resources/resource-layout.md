# 运行资源边界与格式

本文是 raw、generated 与运行时职责的统一说明；重建命令和恢复限制见
[资源再生成](resource-reconstruction-guide.md)。

## 输入、运行与发布

- 游戏只读 `content/generated` 中的转换数据、图像和音频，以及
  `src/Game.Godot/Assets/` 内的字体与效果素材；不读取 `content/raw`、pret C/ASM、
  导入配方或 source-lock，不执行上游代码。部分类型名中的 `Pret` 不代表运行时源解析。
- 原始来源版本、输入选择和转换差异由离线工具维护。玩法值、事件端点、脚本分派键、
  尺寸与渲染绑定是实际运行数据，不能当作来源证明删除。原始文件清单、历史哈希、
  转换回执和先前地区完成标记不属于运行契约。
- `generated` 不是可任意删除的缓存：完整冷重建尚不完整，且 `Game.Domain` 构建时
  内嵌 `content/generated/battle/trainer-ai.json`。只拿源码不等于具备构建和运行输入。
- raw、generated 与代表存档是私有本地资源，不随源码 Git 提交，不用 `git add -f`
  绕过忽略规则。禁止下载 ROM 或公开上传／分发 Pokémon 素材；转换工具可运行不代表
  获得素材分发授权。

## 目录与共享定义

`content/generated` 按消费职责组织：

- `data/`：物种、物品、训练家、遇敌、服务与故事数据。
- `maps/`：地图文件、唯一 world 索引、图块属性和地图渲染图集。
- `sprites/`：宝可梦、训练家、物品和场景角色图像。
- `battle/`：战斗文本、AI 程序、动画和表现数据。
- `ui/`：界面与交互表现。
- `audio/`：合成素材与 cue 绑定。

单文档目录使用直接文件，例如 `data/trainers.json`；没有旧路径别名、软链接或
运行时迁移查找表。独立图像族保留自己的实际消费边界，不建立全局内容哈希仓库。
物种／技能、物品／机器与训练家共享定义，地区资源仅表达真实选择和差异。当前准入
不等于 canonical 定义存在，详见 [Pokémon 准入](../current-content-composition.md#宝可梦准入) 与
[内容组合](../current-content-composition.md)。部分场景服务仍将物品和展示文档放在一起，
这不构成每个包都已最小化的声明。

## 地图格式

地图 schema 3 按地区平铺，如 `maps/regions/viridian/route-1.json`；真实布局变体保留
有含义的后缀。`maps/world.json` 是唯一当前地图选择索引，其条目引用实际文件。
原作地图符号到稳定 MapId 的映射与加载顺序无关，详见 [world 管线](world-resource-pipeline.md)。

地图保留身份、尺寸、原图事件及顺序、warp、脚本分派路径、16 位 blocks／border blocks、
数值 metatile 属性与渲染引用。metatile 行为 `[id, attributes]`，不存每格来源记录。
`sourceMapSymbol`、`sourceMapName` 与脚本路径是实际分派身份；故事 IR 保留所需符号和行号，
但运行时不重新打开原作脚本。读取器拒绝旧 schema，不在加载时迁移。

`maps/tilesets.json` 保留引用布局的完整 primary／secondary 属性表，包括初始网格未用、
但 CUT 或其他玩法会切换到的图块。源解析由布局、tileset 声明和属性二进制驱动；缺失输入、
空有效地图或错误引用必须报错，不以历史地图／tileset 数量作为门槛。

地图逻辑图像路径由 [渲染图集](map-render-atlases.md) 解析。原事件头、NPC 和 warp 相同，
不代表布局或像素相同：海沫岛停流、训练家之塔对战模式和三之岛挖通隧道均是实际变体。
开场／常青地区的 `sprites/overworld/opening-player.png` 也保留原有玩家调色板和九帧图像，
不能用通用 Red 动画条直接替换。其他图像与颜色契约见 [图像边界](generated-image-assets.md)。

## 验证边界

`GeneratedOnlyCampaignTests` 仅复制 generated 到隔离资源根，验证加载、新游戏、保存、
Continue 与移动；后期加载使用受控当前 schema 测试档。其合成孵蛋总数不是对旧玩家档的修复，
旧代表档不能直接作为当前 Continue 基线。存档规则见 [当前组合](../current-game-composition.md)。

生成器一致性、独立源 oracle、Content 读取、实际 Godot 图像与正常关窗分别验证不同层。
图集重打包自洽不等于原作像素一致；文件减少不等于 GPU 内存或帧率改善。上述检查不替代
自然新游戏至后期的完整路线，也不证明真实扬声器输出。定向命令见
[重建检查](resource-reconstruction-guide.md#验证)，当前验收范围见 [测试说明](../../tests/README.md)。
