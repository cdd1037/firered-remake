# 导入与维护工具

本目录按职责组织，不保留旧扁平路径的软链接或转发入口。

- `imports/maps/`：地图读取、紧凑运行格式、图块与地图图集
- `imports/pokemon/`：物种、学习表、遇敌物种及图像转换
- `imports/trainers/`：共享训练家目录、区域队伍和训练家图像
- `imports/items/`：物品、机器、商店和混合物种／物品目录
- `imports/battle/`：战斗动画与效果源转换
- `imports/presentation/`：UI、角色图像、文本外观及展示资源
- `imports/audio/`：音频目录和合成配方
- `imports/story/`：事件、服务、玩法规则和故事编译
- `imports/source/`：原始输入选择、读取及物化
- `lib/`：可复用转换逻辑；`project_paths.py` 只定位实际工程根
- `checks/`：当前维护的导入与转换检查
- `recipes/`：按功能组织的原始输入选择和转换配方；`config/` 存放跨族配置
- `dev/`：Bash 启动内部件；顶层只保留可供 shell 导入的 `env.sh`

普通开发继续使用工程根的 `dev`、`dev.cmd`、`dotnet build` 和 `dotnet test`。`Game.ResourceImport.Tests` 直接调用 `checks/` 内的维护检查，不经过旧路径代理。全部命令均可使用 Python 3，路径使用 `pathlib`，不要求 Windows 上有 Bash。

## 原始输入物化

重复的早期故事／UI／物种 fetch 和 Eevee／Snorlax seed 脚本已合为一个显式配方入口。例如在工程根执行：

```sh
python scripts/imports/source/materialize-source.py --recipe scripts/recipes/source/fetch-story.lock.json --offline
python scripts/imports/source/materialize-source.py --recipe scripts/recipes/pokemon/eevee-pokemon.lock.json --reference-root /path/to/pinned-pokefirered
```

优先读取已有 raw 文件；`--reference-root` 读取固定版本 Git blob，`--cache` 读取只读目录缓存。默认不联网，只有显式 `--download` 才下载缺失输入。输入选择为空、重复、越界或版本不符均报错；预检完成后才写入。其他有独立转换职责的 fetch 工具仍保留，以各自帮助说明为准。

## 有范围的重建与检查

`imports/rebuild-services.py` 编排服务、物品、进化及支线转换；`imports/rebuild-postgame.py` 编排后续区域。它们依赖已有基础区域资源和固定版本本地 Git checkout，不是完整游戏的冷重建器。只输出步骤日志和失败状态，不保存工程文件哈希回执。

```sh
python scripts/checks/test-script-layout.py
python scripts/checks/test-single-file-imports.py
python scripts/checks/test-pokemon-import.py
python scripts/checks/test-image-io.py
python scripts/checks/test-importer-png-semantics.py
```

只服务已退休测试项目或冻结窗口证据的 26 个历史入口已删除；当前数值夹具的显式生成器保留。9 个重复输入物化入口已合并，真实选择配方仍在。19 个无消费者的重复 contract 回执不再写出。需要实际业务约束的 contracts、输入版本和来源路径仍由所属转换器使用。

部分早期音频增量转换依赖当前工程未携带的 `recipes/audio/synth-*-cues.json`。这些文件原本就缺失，未伪造或用现有最终音频目录替代；相关早期冷重建仍不在已验证范围。完整边界见[资源再生成说明](../docs/resource-reconstruction-guide.md)。

单文件资源转换的 `--out` / `--output` 现在指定 JSON 文件，而不是同名目录。训练家转换同时发布共享定义；输出通常放在 `<generated>/data/trainer-battles/<区域>.json`，任意其他文件位置须用 `--generated-root` 显式指定共享目录。临时独立道具图标位于 `sprites/item-icons/`，最终精确图集为 `sprites/items.png`；执行 `python scripts/lib/item_icon_exports.py --project-root <工程>` 完成部分重建合并，不会创建 `.png` 目录。

## Python 环境

当前验证组合为Python 3.12与Pillow 12.3.0。可在普通venv内执行`python -m pip install -r requirements-dev.txt`；从工程目录运行。该依赖声明只服务导入/资源测试，C#规则测试不需要Python。NumPy/SciPy等音频研究依赖不属于普通维护入口。
