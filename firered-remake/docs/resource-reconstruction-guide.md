# 资源导入与再生成

本文描述当前工程的实际边界与可用入口。运行游戏不需要原作源码；重新转换资源需要对应的原始输入。已有普通工程包携带本地资源，代码仓库只保存作者实现、必要配方和测试。

运行资源的实际功能目录见[资源布局](resource-layout.md)。地图与图像由各自发布器写入当前位置，单文件目录已经改为直接 JSON/PNG 目标。

## 输入与输出

- `content/raw/pokefirered/037335f4/`：按上游相对路径保存的原始输入，固定 revision 为 `037335f4c725d7c9aecdac87066f2002b4bd7e14`，来源为 `pret/pokefirered`。
- `content/raw/local/forest-trainer-art/`：三个实际使用的本地调色板变体，不能用上游默认调色板替换。
- `content/generated/`：运行时需要的类型化游戏数据、图集、图块索引和合成音频资源。这里不保存原始 C/ASM 副本或导入来源锁。
- `scripts/recipes/` 中按功能组织的配方和 `config/` 跨族配置：固定版本、输入选择及真实转换设置，仅供工具使用。
- `src/Game.Godot/Assets/`：Godot 项目的字体和效果素材，也需随普通工程提供。

资源、存档、SDK、编译缓存由 `.gitignore` 排除。不要用 `git add -f` 把它们加进源码仓库。来源可追溯或转换成功不等于获得公开分发授权；对外分发资源前仍须自行确认适用权利。

## 准备原始输入

现有导入器有两种输入方式：直接读取上述 raw 目录，或通过 `--reference-repo` 从固定版本的本地 Git checkout 读取 blob。少数 fetch 工具提供 `--cache` / `--offline`。以具体入口的 `--help` 为准，不要仅凭 fetch/import 文件名推断是否联网。

缺少输入时，从官方 `pret/pokefirered` 仓库的固定 revision 取得对应路径，保存到 raw 树的同一相对位置；需要 Git 对象的入口则准备本地 checkout。不要拿最新分支的同名文件混入固定版本，也不要把丢失的来源证明 JSON 当成游戏素材重新构造。文件读取与格式错误应清楚报出缺失路径。

这些转换不需要运行上游游戏代码。普通导入链不以 ROM、模拟器录音或旧的参考构建目录为前提。主游戏使用 `firered-audio-player` 的自定义合成器；其工具与资源说明见该项目 README。

工具布局、离线原始输入物化和维护检查见 [scripts 说明](../scripts/README.md)。重复的薄 fetch／seed 入口已合并为 `scripts/imports/source/materialize-source.py --recipe <配方路径>`；默认离线，支持固定版本 Git blob 和只读目录缓存。

## 工具环境

使用 .NET 10 SDK、Godot 4.7.2 .NET 和 Python 3；图像转换需要 Pillow。工具发现见工程 README 与 `scripts/env.sh`。下面命令均在 `firered-remake/` 执行，`source scripts/env.sh` 是 Bash 示例。

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false
dotnet test FireRed.Current.slnx --no-build --no-restore -m:1 -nr:false
```

`--no-restore` 要求包已恢复。首次环境配置按工程 README 进行，不应把本机工具安装目录写进项目资源。

## 当前再生成入口

以下是已有资源工程上的定向再生成命令，不是一条覆盖所有历史开发包的从零流水线。区域地图、物种、训练师和展示数据先由所属导入器生成，再执行依赖它们的共享转换。

```sh
# 共享 TM/HM 定义及兼容性；区域文件保留紧凑引用。
python3 scripts/imports/items/import-machine-catalog.py --references-only
python3 scripts/imports/items/import-machine-catalog.py --references-only --check

# 运行时地图编号和场地属性；后者依赖已有区域地图元数据。
python3 scripts/imports/maps/import-map-numbers.py
python3 scripts/imports/maps/import-field-tilesets.py

# 战斗文本与演出时长。
python3 tools/import-battle-text.py
python3 tools/import-presentation-cues.py

# 共享 NPC 图集，及依赖已有菜单/初始物种/驱虫文本的故事编译。
python3 scripts/imports/presentation/import-shared-overworld.py
python3 scripts/imports/story/import-story.py
python3 scripts/imports/story/import-story.py --check

# 在区域地图转换完成后重建并校验共享图块页。
python3 scripts/imports/maps/import-map-atlases.py
python3 scripts/imports/maps/import-map-atlases.py --check
```

地图原 PNG 已收进图集时，再打包可读取已有图集；首次清除独立地图 PNG 使用 `--prune`，工具先验证并把原文件备份到工程外。只检查一致性时用 `--check`，不要为验证反复删除资源。图像输出使用共享的精确 RGBA 编码工具，不能因为压缩后没有字节相等就认定像素改变。

`scripts/imports/rebuild-services.py` 和 `scripts/imports/rebuild-postgame.py` 分别是服务／支线及后续区域的有范围编排入口，尚未在完整固定版本 checkout 上执行全量冷重建，不应标成“一键重建整个游戏”。单个族的输入、顺序和已验证范围应由对应工具说明维护，而不是额外生成一份全项目来源回执。

## Pokémon 导入族

区域入口为 `scripts/imports/pokemon/import-<区域>-pokemon.py`，初始物种入口为 `scripts/imports/pokemon/import-pokemon.py`；Celadon/Rocket 的综合目录也调用同一转换核心。共享实现位于 `scripts/lib/pokemon_import.py` 与 `scripts/lib/pokemon_import_io.py`，区域以显式参数提供物种/地图选择，不通过修改另一区域模块的全局变量继承配置。区域的真实数据选择与规则仍由对应入口拥有。

```sh
python3 scripts/checks/test-pokemon-import.py
```

这组检查验证独立重复转换、输入读取及实际字段/图像行为；对照在工程外临时输出完成。工具布局与运行资源布局各自按职责整理；实际发布位置以当前导入器为准。

## 验证边界

- [运行时资源边界](generated-only-runtime.md)：仅 generated 的独立资源根、新游戏/存档/Continue，以及代表存档加载检查。
- [共享机器目录](shared-machine-catalog.md)：区域引用、完整兼容性矩阵与导入测试。
- [地图图集](map-render-atlases.md)和[图像资源](generated-image-assets.md)：像素保持、原生 Godot 解码及缓存边界。
- `tests/Game.ResourceImport.Tests`：当前维护的导入检查；`scripts/checks/test-*.py` 中的族测试补充字段和像素比较。

通过这些检查不等于整条游戏路线重放、每个旧入口的冷重建、真实扬声器听感验收或素材公开授权。缺失的旧研究记录不应通过伪造报告、放宽玩法断言或重新塞回运行时来源依赖来补齐。

Celadon approach 遇敌表由 `python scripts/imports/story/import-celadon-approach-encounters.py` 从固定 raw 的 wild_encounters.json、species_info.h、pokemon.c 重建。`--check` 核对现存 JSON，`--raw` / `--out` 支持隔离输出。两张地图、24 个槽位及携带物品玩法字段与保留资源逐字段等价；当前输出已删除不参与业务的 sourcePath 和 excludedTables。

共享地图属性表使用当前schema3地图及raw的src/data/tilesets/headers.h、metatiles.h和实际属性bin再生。运行import-field-tilesets.py后用--check核对；不能仅保留旧地图文件名对应的layout。

音频从零恢复是独立输入合同：主pack需要现有pack或合法自备ROM，旧normal-cries目录生成还需要对应inventory及其ROM/ELF/map来源。目前八份早期cue配方缺失；在这些闭包补齐前保留现有私有音频资源，不宣称完整冷重建。
