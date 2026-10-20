# 资源导入与再生成

运行目录与格式的唯一边界说明见 [资源布局](resource-layout.md)。本文维护真实输入、
生产入口与恢复限制，不把生成结果、历史证明或前一地区产物冒充原始来源。

## 原始输入

- `content/raw/pokefirered/037335f4/` 按上游相对路径保存 `pret/pokefirered` 的固定输入，
  revision 为 `037335f4c725d7c9aecdac87066f2002b4bd7e14`。缺失文件须来自这个版本，
  不能用最新分支同名文件替代。
- `content/raw/local/forest-trainer-art/` 的三个调色板是实际本地变体，不可替换成上游默认值。
- 输入选择和转换配方位于 `data/`、`scripts/recipes/` 及对应导入工具。部分专用入口仍从
  固定版本 Git blob 读取，以其实际参数为准；任何入口都不得执行上游仓库脚本。
- Godot 的字体和效果素材随项目保留；中文 pack 与字体另见 [中文版](chinese-edition.md)。
- 音频 cue 生成需要已有匹配的 `audio/synth/firered.pack` 与 `normal-cries.bin`。
  pack 提取需要合法已有的匹配 ROM，不下载 ROM；普通叫声目录还需对应生成后的 `.bin`
  样本及原始 WAV。现有 raw 未提供完整的 cry `.bin` 输入，详见 [音频](audio.md)。

原始输入物化默认离线，只有显式 `--download` 才获取缺少的固定版本文件：

```sh
python scripts/imports/source/materialize-source.py --recipe <配方路径> --offline
```

raw、generated 与代表存档不纳入源码 Git；Pokémon 素材不公开上传或分发。恢复前保留
独立输入与可用资源副本，不要先删掉全部 generated 再尝试重建。项目构建也需要生成的训练家 AI 程序。

## 维护入口

以下命令从仓库根目录执行。工具链安装与构建见 [README](../README.md)，Python 依赖见
`requirements-dev.txt`。导入工具组织和各族入口见 [scripts](../scripts/README.md)。

```sh
# 当前完整选定地图、真实布局变体、属性表、共享 NPC 图像与地图图集。
python scripts/imports/rebuild-resources.py world

# 场景规则、遇敌、交互图像，以及城市／NPC 交换／钓竿等服务。
python scripts/imports/rebuild-resources.py field-rules field-services

# 由固定源码与现有匹配音频素材生成 cue 目录，不生成 pack 或叫声样本。
python scripts/imports/rebuild-resources.py audio-cues

# 按需选择界面／表现目标；完整可用目标及参数以 --help 为准。
python scripts/imports/rebuild-resources.py diploma help quest-log
python scripts/imports/rebuild-resources.py --help

# 对已有资源做只读检查；或将 world 完整输出到新的独立目录。
python scripts/imports/rebuild-resources.py world field-rules field-services --check
python scripts/imports/rebuild-resources.py world --out /tmp/firered-world-check
```

统一入口还支持 `intro-movie`、`opening`、`summary`、`evolution`、`capture-balls`、
`overworld-art`、`party`、`npc-trade`、`inventory` 和 `move-animations`。各目标支持
`--check`；`world` 在临时空目录重建后比较 JSON 语义、PNG 全 RGBA 和二进制流。
`quest-log` 需要输出根内的 `maps/world.json`，可与 `world` 一起选择。音频可用
`--pack`／`--cries` 显式指定独立输入，否则从输出根查找。

`--raw` 指定固定原始输入，`--out` 指定 generated 根，二者不得重叠。`--dry-run`
仅检查入口及显式音频文件并打印命令，不证明源闭包齐全。执行失败立即停止，不跳过
缺失工具，也没有隐含的 `all` 或历史地域前置阶段。

### 共享游戏数据与故事

```sh
python scripts/imports/items/import-machine-catalog.py --references-only
python scripts/imports/items/import-current-items.py
python scripts/lib/item_icon_exports.py
python scripts/imports/pokemon/import-game-pokemon.py
python scripts/imports/presentation/import-pokemon-collection-presentation.py
python scripts/imports/story/import-story.py
```

当前物种选择和 breeding 由同一入口维护，准入／各 roster 差异见
[Pokémon 准入格式](pokemon-admission-format.md)。机器兼容性与顺序见
[共享机器目录](shared-machine-catalog.md)；训练家、物品与专用图像族入口见 scripts 说明。

地图几何与故事是不同资源：改可交互对象不能只重建地图，还须编译相应场景规则和故事 IR。
故事选择由 `tools/import-story/recipe.json` 维护，`import-story.py` 使用 PATH 的 .NET，
也支持 `--dotnet`。赠送、隐藏物品和剧情条件从源事件／脚本解析，地图加载成功不证明交互完成。

图鉴证书、HELP、Trainer Card 和各原生 UI 的细节保留在其功能文档，不在本文复制：
[证书](pokedex-diploma.md)、[HELP](help-content.md)、[Trainer Card](trainer-card-atlas.md)、
[图像边界](generated-image-assets.md)。

## 验证

先按 README 完成正常构建，再按变更范围运行：

```sh
python scripts/checks/test-world-pipeline-oracle.py --generated content/generated
python scripts/checks/test-import-input-closure.py
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~GeneratedOnlyCampaignTests|FullyQualifiedName~CampaignCompositionTests'
```

局部修改只跑对应资源族；跨读取／运行层修改再扩大组合回归。独立源 oracle、旧资源临时
对照、生成器 `--check` 和 native 像素检查并不等价，见 [world](world-resource-pipeline.md)
与 [地图图集](map-render-atlases.md)。测试项目与按需音频检查见 [测试说明](../tests/README.md)。

## 尚未闭合的恢复边界

- 统一入口只编排明确列出的目标，不是全游戏一键冷重建器。物品、物种、训练家、故事和
  特殊 UI 仍有独立输入与依赖；不能因部分检查通过就宣布所有 generated 都可删除。
- 音频 cue 重建不解决 pack 提取或普通叫声样本的独立输入要求。
- 双打 UI 原始输入仍缺 `graphics/battle_interface/healthbox_doubles_player.png` 与
  `healthbox_doubles_opponent.png`，不能把现有双打资源可用说成源端完整再生已通过。
- 生成一致性不替代完整自然通关、全部原生视觉检查或真实音频输出验收；Dummy 不算听感验收。
