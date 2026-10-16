# 资源导入与再生成

游戏只读取 `content/generated` 和 Godot 自有素材，不读取 pret 仓库或导入配方。
源码仓库保存代码、必要的选择配置和测试；普通工程包另带本地资源。不要把临时
报告、Git checkout、SDK、构建缓存或旧阶段产物塞回运行目录。

## 原始输入

- `content/raw/pokefirered/037335f4/` 按上游相对路径保存固定输入，revision 为
  `037335f4c725d7c9aecdac87066f2002b4bd7e14`，来源 `pret/pokefirered`。
- `content/raw/local/forest-trainer-art/` 是实际使用的三个本地调色板变体，不能
  用上游默认调色板替换。
- `src/Game.Godot/Assets/` 中字体和效果素材随普通工程提供。
- 音频另需现有匹配 `audio/synth/firered.pack` 与 `normal-cries.bin`，见[音频说明](audio.md)。

缺少源文件时，从固定 revision 取得对应路径。不要用最新分支同名文件、旧生成
JSON 或历史证明文件冒充原始输入。新地图／场景服务编译器直接读取 raw；部分尚未
迁移的专用入口仍读取固定版本 Git blob，以该入口实际参数为准。转换不执行上游脚本。

`python scripts/imports/source/materialize-source.py --recipe <配方> --offline`
可以从已有本地输入物化所选源文件；只有显式 `--download` 才联网。
配方描述真实输入选择与转换差异，不承担每次运行的文件回执功能。

资源由 `.gitignore` 排除；不使用 `git add -f` 绕过。转换能力不代表公开分发授权。

## 工具环境

.NET 10 SDK、Godot 4.7.2 .NET、Python 3 和 Pillow。安装及发现入口见工程 README
与 `scripts/env.sh`。以下命令从仓库根目录执行：

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

`--no-restore` 要求此前已完成包恢复。音频测试按需单独运行，不进默认全量测试。

## 最终资源编译入口

```sh
# 全部选定地图、真实布局变体、场地瓦片、地图图集及共享 NPC 图集。
python scripts/imports/rebuild-resources.py world

# 真实场景功能、遇敌、交互图像，以及 NPC 交换／钓竿等服务。
python scripts/imports/rebuild-resources.py field-rules field-services

# 由源码和现有合成素材生成唯一音频 cue 目录。
python scripts/imports/rebuild-resources.py audio-cues

# 原版图鉴证书，两张画面和文本／布局数据。
python scripts/imports/rebuild-resources.py diploma

# 检查而不修改；也可用独立输出目录重建再对照。
python scripts/imports/rebuild-resources.py world field-rules field-services audio-cues diploma help --check
python scripts/imports/rebuild-resources.py world --out /tmp/firered-world-check
```

可用 `--raw` 指定原始输入目录，`--out` 指定最终 generated 根。`--dry-run` 校验
入口和显式音频输入并打印命令，但不保证整个源闭包齐全。任一步失败即停止；不会
跳过缺失工具而报告成功，也不再依赖旧 services/postgame 地域阶段编排。

这个入口明确覆盖所列目标，并非全游戏一键冷重建器。共享物品、物种、故事 IR、
UI 等仍有各自的真实生产入口；不把这些未编排流程隐藏在“全部完成”里。

### 地图

`data/world-maps.json` 明确选择地图、真实变体与少量命名覆盖，普通字段从源数据
派生。唯一运行索引是 `content/generated/maps/world.json`。地图身份不依赖加载
先后或历史区域包。真实变体保留原事件头、NPC 和 warp，只替换对应布局／像素。

统一 world 编译已经在新建空目录验证：414 份地图数据、3296 张逻辑地图／边框
图像、126 组 NPC 图像的 1088 帧，以及开场玩家图像，与原有最终资源对照一致。
比较从最终图集裁剪完整 RGBA，不把 PNG 编码差异当成画面变化。

三之岛通路、海沫岛、宝可梦塔等仍按各自真实游戏条件选择变体；普通房屋、门楼、
火焰山顶与后期区域都由同一地图入口处理，不需要先重放早期开发阶段。

### 游戏数据与故事

```sh
python scripts/imports/items/import-machine-catalog.py --references-only
python scripts/imports/items/import-current-items.py
python scripts/imports/pokemon/import-game-pokemon.py
python scripts/imports/presentation/import-pokemon-collection-presentation.py
python scripts/imports/story/import-story.py
```

运行物种定义共享 canonical 目录，真实选择在 `data/game-pokemon.json`；TM/HM
兼容性共享一份数据，不复制累计地区物种表。物品图标由共享导出工具合入图集。

地图几何与剧情是不同资源：改变可交互对象还需重新编译故事 IR。故事源选择在
`tools/import-story/recipe.json`；`import-story.py` 使用 PATH 的 .NET，也支持
`--dotnet`。NPC 赠送、隐藏物品与剧情条件从源事件及脚本解析，不靠地图成功载入
来证明交互已经实现。

## 验证与恢复边界

- `scripts/checks/test-world-pipeline-oracle.py` 检查源几何、事件顺序、脚本入口和真实变体。
- `tests/Game.ResourceImport.Tests` 运行导入行为测试；局部修改只跑相关族，跨读取层
  修改再跑组合回归。
- [generated-only 运行验证](generated-only-runtime.md)覆盖独立资源根、新游戏和存档加载。
- [地图图集](map-render-atlases.md)、[图像素材](generated-image-assets.md)、[共享机器目录](shared-machine-catalog.md)
  说明对应数据与像素语义。
- pack 提取及普通叫声样本的完整再生仍有独立输入要求；完整 cue 目录已不依赖旧
  地区音频配方，不能将二者混为一谈。

导入等价不等于完整游戏自然通关、全部动画视觉验收或扬声器听感验收。阶段清理
的实际检查范围见[整合记录](script-consolidation.md)。备份恢复重放、Drive 上传
目前均不作为本轮验证步骤。

图鉴证书的独立输入闭包、屏幕块转换与重建检查见 [Pokédex diploma](pokedex-diploma.md)。

情境 HELP 使用 `python scripts/imports/rebuild-resources.py help` 重建单一 `ui/help/help.json`。原作完整文本、主题／问答顺序、解锁条件和地图分类只在导入时解析；见 [HELP 资源](help-content.md)。
