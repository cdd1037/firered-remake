# 导入与维护工具

Python 只用于离线导入、资源重建和检查；游戏运行时读取 `content/generated`，不读取原作仓库或这些配方。

## 组织与职责

- `imports/`：按资源族划分的实际命令入口。共同转换只实现一次；确实不同的玩法资源仍保留各自转换职责。
- `lib/`：源解析、图像、地图、训练家、物种和物品的共用转换实现。
- `recipes/`：必要的输入选择、输出结构和特殊资源绑定。源数据已有的数值直接解析，不再复制累计地区名单或自生成输入清单。
- `checks/`：转换行为、源数据和输出语义检查。
- `dev/` 与 `env.sh`：开发环境和启动内部件。

不保留旧入口软链接或转发脚本。新增地区应复用对应转换器，不复制上一地区 Python。具体入口可用 `--help` 查询。

## 常用统一入口

以下命令在工程根目录运行；读已有 raw 资源，不需要启动游戏。

```sh
# 区域宝可梦展示资源；选择差异由共享配方定义
python scripts/imports/pokemon/import-regional-presentation.py --region cerulean --out /tmp/example/cerulean
python scripts/imports/pokemon/import-evolution-presentation.py --selection stone-evolution --check

# 训练家队伍和展示图
python scripts/imports/trainers/import-trainer-battles.py --recipe scripts/recipes/trainers/bill.json --check
python scripts/imports/trainers/import-trainer-portraits.py --group cerulean --check

# 特殊物种图片和共享机器学习视图
python scripts/imports/presentation/import-species-art.py --profile eevee --check
python scripts/imports/items/import-machine-views.py --region cerulean --out /tmp/example/content/generated/data/machine-learning/cerulean.json
python scripts/imports/items/import-regional-items.py --group ssanne --check

# 地图运行目录按需要的区域直接组合，不必逐个重放历史阶段
python scripts/imports/maps/export-runtime-maps.py --help
```

单文件资源的 `--out` / `--output` 指向 JSON 文件，不是同名目录。训练家导入还维护共享定义；非标准输出位置应显式传 `--generated-root`。物品图标临时输出至 `sprites/item-icons/`，最终用 `python scripts/lib/item_icon_exports.py --project-root <工程>` 合入 `sprites/items.png`。

## 原始输入

```sh
python scripts/imports/source/materialize-source.py --recipe scripts/recipes/source/fetch-story.lock.json --offline
```

输入版本在配方／共用源配置中明确。优先使用已有 raw，按具体转换器支持情况可读取固定版本 Git blobs。不得执行上游仓库脚本。物化工具默认不联网，只有显式 `--download` 才取缺失文件。

现有部分 `.lock.json` 是历史命名的实际输入选择或转换配置，并非包管理器锁文件。不要新增只有文件清单、重复版本或转换回执作用的配方。源码来源和转换信息留在工具层，不混入运行资源。

## 重建与验证边界

`imports/rebuild-services.py` 与 `imports/rebuild-postgame.py` 是范围明确的现有流程编排，仍依赖基础区域资源及各步骤需要的输入；它们不是已验证的完整冷重建器。可单独重建资源族，或在临时输出目录比较 JSON 语义和完整解码像素。PNG 编码不同但像素相同不算资源变化。

部分早期音频增量工具所需的原始 cue 配方仍缺失，本轮没有用最终生成文件伪造这些输入。完整边界见 [资源再生说明](../docs/resource-reconstruction-guide.md)。

```sh
python scripts/checks/test-script-layout.py
python scripts/checks/test-single-file-imports.py
python scripts/checks/test-pokemon-import.py
python scripts/checks/test-trainer-import.py
python scripts/checks/test-trainer-portraits.py
python scripts/checks/test-presentation-pixels.py
python scripts/checks/test-machine-importers.py
```

当前维护检查接入 `Game.ResourceImport.Tests`，音频检查仍按需单独运行。不要为每次局部调整重跑不相关战斗测试；跨层改动再扩大回归范围。

## 环境

验证环境为 Python 3.12、Pillow 12.3.0；依赖见 `requirements-dev.txt`。路径使用 `pathlib`，普通 Python 命令不要求 Windows 安装 Bash。NumPy/SciPy 等音频研究依赖不是普通导入入口要求。
