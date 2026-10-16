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

# 特殊物种图片、机器图标和区域物品
python scripts/imports/presentation/import-species-art.py --profile eevee --check
python scripts/imports/items/import-machine-views.py --region cerulean --out /tmp/example/content/generated/data/machine-learning/cerulean.json
python scripts/imports/items/import-machine-views.py --region cerulean --check
python scripts/imports/items/import-regional-items.py --group ssanne --check

# 最终地图／场景服务／音频目录，不重放历史地域阶段
python scripts/imports/rebuild-resources.py world field-rules field-services
python scripts/imports/rebuild-resources.py audio-cues --check

# 原版关都／全国图鉴证书画面与文本
python scripts/imports/rebuild-resources.py diploma --check

# 原版情境 HELP 文本、排序、解锁条件与地图分类
python scripts/imports/rebuild-resources.py help --check
```

单文件资源的 `--out` / `--output` 指向 JSON 文件，不是同名目录。训练家导入还维护共享定义；非标准输出位置应显式传 `--generated-root`。物品图标临时输出至 `sprites/item-icons/`，最终用 `python scripts/lib/item_icon_exports.py --project-root <工程>` 合入 `sprites/items.png`。

早期机器图标文档只保存 `schemaVersion` 和 `icons`，不再重复机器兼容性投影。
图标检查比较完整 RGBA 和元数据，也支持已有图集；教学与兼容性仍由共享机器目录负责。

当前可获得物品的完整背包说明和补充图标由 `import-current-items.py` 读取 `current-item-admissions.json` 生成。实际野生持物和 Pickup 奖励也必须进入该选择；允许取得不等于加入商店库存。`icon: true` 通过原作图标绑定表选择 PNG／调色板，生成后仍合入同一物品图集。

## 原始输入

```sh
python scripts/imports/source/materialize-source.py --recipe scripts/recipes/source/fetch-story.lock.json --offline
```

输入版本在配方／共用源配置中明确。默认使用已有 raw；显式 `--reference-root` 时，以固定版本 Git blobs 为准。不得执行上游仓库脚本。物化工具默认不联网，只有显式 `--download` 才取缺失文件。

接受已读字典或 source reader 的转换函数必须只从该输入取得原作数据。物品业务值用 `ItemSource.definition`，仅需描述和稳定引用的投影用 `described_items`；不要借用默认工程的 item cache 或已有 generated 文件。完整机器转换可用 `catalog_from_source`；从序列化机器 JSON 构造 `Catalog` 时须显式提供物品定义。

connected-field、route1-encounter、route1-whiteout 和 wild-battle 的旧 fetch 包装入口已移除；使用同一物化命令并显式选择原有配方。输出不得与参考仓库或缓存目录重叠，输入／输出路径拒绝符号链接。

现有部分 `.lock.json` 是历史命名的实际输入选择或转换配置，并非包管理器锁文件。不要新增只有文件清单、重复版本或转换回执作用的配方。源码来源和转换信息留在工具层，不混入运行资源。

## 重建与验证边界

`imports/rebuild-resources.py` 统一编排明确选择的最终资源目标：world、field-rules、field-services、audio-cues、diploma、help、opening、summary、evolution、capture-balls、overworld-art、party、npc-trade、move-animations。支持 `--raw` / `--out` 与 `--dry-run`。地图由 raw 直接生成最终格式及图集；所有目标支持只读 `--check`；world 在临时空目录生成后做语义／像素比较。旧地域前置编排不再保留。

这个入口不假称覆盖所有游戏资源；物品、物种、故事和特殊 UI 的独立入口仍按真实依赖执行。音频 cue 已由源码与现有 pack/叫声库一次生成，pack 与叫声样本本身的重建要求见[资源再生说明](../docs/resource-reconstruction-guide.md)。

```sh
python scripts/checks/test-script-layout.py
python scripts/checks/test-diploma-import.py
python scripts/checks/test-help-system-import.py
python scripts/checks/test-world-pipeline-oracle.py
python scripts/checks/test-single-file-imports.py
python scripts/checks/test-pokemon-import.py
python scripts/checks/test-trainer-import.py
python scripts/checks/test-trainer-portraits.py
python scripts/checks/test-presentation-pixels.py
python scripts/checks/test-item-ui-library-imports.py
python scripts/checks/test-import-input-closure.py
python scripts/checks/test-item-source-callers.py
python scripts/checks/test-magikarp-contract-import.py
python scripts/checks/test-special-presentation-importers.py
python scripts/checks/verify-snorlax-catalog.py
python scripts/checks/test-machine-importers.py
```

当前维护检查接入 `Game.ResourceImport.Tests`，音频检查仍按需单独运行。不要为每次局部调整重跑不相关战斗测试；跨层改动再扩大回归范围。

## 环境

验证环境为 Python 3.12、Pillow 12.3.0；依赖见 `requirements-dev.txt`。路径使用 `pathlib`，普通 Python 命令不要求 Windows 安装 Bash。NumPy/SciPy 等音频研究依赖不是普通导入入口要求。
