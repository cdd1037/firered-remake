# 参考源基线与接入约束

核查日期：2026-10-08 UTC。状态：P0 期间的只读来源研究，尚未导入可玩内容。

本文件与 `scripts/recipes/config/source.json` 冻结研究依据；它们不能证明游戏已实现、工具链已通过或素材已获得再分发许可。产品最终范围见 [scope.md](scope.md)，实施门禁见 [implementation-roadmap.md](implementation-roadmap.md)。第一徽章仅为首个完整玩家流程里程碑。

## 1. 唯一参考版本

| 字段 | 冻结值与证据 |
| --- | --- |
| 维护项目 | pret/pokefirered，英文 FireRed/LeafGreen 的社区反编译项目；不是原游戏发行商的官方源码 |
| 源提交 | `037335f4c725d7c9aecdac87066f2002b4bd7e14` |
| Git tree | `7515cd4067ea55f8cdbc73624478afd2736c58b9` |
| 提交时间/主题 | 2026-09-26T20:54:25Z，Audio improvements (#774) |
| 游戏选择 | `GAME_VERSION=FIRERED`、`GAME_REVISION=0`、`GAME_LANGUAGE=ENGLISH`、`MODERN=0` |
| 编译选择语义 | C/汇编使用 `FIRERED=1`、`REVISION=0`、`ENGLISH=1`、`MODERN=0`；这些是解析分支的依据，本次不执行构建 |
| 游戏代码 | `BPRE`；区域只记录被这个标识和英文版本证据覆盖的范围，不凭语言推定更窄发行国家 |
| ROM 身份参考 | `pokefirered.gba`，SHA-1 `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc` |
| ROM 取得/验证 | 未下载 ROM，未重算 ROM hash；上述值来自维护者 README 与 firered.sha1，只作版本标识 |
| 展示语言 | 简体中文独立语言包；不能更换源规则或让译名影响数值生成 |

一手依据：[固定提交](https://github.com/pret/pokefirered/commit/037335f4c725d7c9aecdac87066f2002b4bd7e14)、[README](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/README.md)、[config.mk](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/config.mk)、[Makefile](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/Makefile)、[include/config.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/config.h)。

README 同时列出 LeafGreen、revision 1 和 Switch/revision 10。它们的存在不授权混用数据或修复行为。今后更换版本必须另建 source lock、列出差异、更新内容版本并重跑相关规则与场景。`master` 只可用于发现更新，不能成为构建时移动依赖。

## 2. 此次确实验证了什么

- 从 GitHub 维护仓库核验 commit 及其 tree。完整 recursive tree 返回 `truncated=false`，包含 11,434 个树/文件条目；这个数量不是资源数量或游戏覆盖分母。
- 按固定提交只读取得 77 份文本参考文件，并逐一核对 Git blob SHA-1 与固定 tree。另计算原始文本字节 SHA-256。可复核结果全部记录在 source lock，不把文本内容打包进工程。
- 读取地图组、布局目录、代表地图/脚本及数据生成规则，核对以下格式与路径。没有遍历每张地图的事件语义，也没有生成玩家可达闭包。
- 对真新镇布局和 tileset 只记录部分二进制/图片文件的 tree 元数据；未取得这些素材字节。不能据此声称图像、碰撞、调色板或动画已显示正确。
- 未运行上游 Makefile、shell、编译器、转换工具；未构建 ROM；未批量生成本项目内容包；未公开上传源素材。

source lock 中 `evidenceFiles` 的含义是“字节来源验证”，不是“这个文件的全部规则已审计”。`assetMetadataOnly` 更弱，只证明路径、Git blob ID 和大小存在。

## 3. 原始目录规模，不是完成率

对固定版本 JSON 进行直接解析，得到：

| 测量项 | 原始数量 | 不能推出的结论 |
| --- | ---: | --- |
| map_groups.json 的 group_order | 43 组 | 组并不等同于剧情阶段 |
| 按 group_order 展开的地图条目 | 425 个，均唯一 | 不等于 425 张正常单机可达地图 |
| tree 中 data/maps/*/map.json | 425 份 | 文件存在不代表地图入口和脚本已验证 |
| layouts.json 的 layouts 数组槽位 | 383 个 | 不等于 383 个有效布局 |
| 其中有 id 的布局记录 | 365 个 | 可包含共享或残留布局，不等于地图数 |
| 其中空对象槽位 | 18 个 | 不能压缩数组后让原始位置编号漂移 |

组中包含联机设施、配信岛屿、废弃原型；布局中还有沿用的旧资源。物种、训练家和遭遇表也有非本版、不可达或残留项。未来实现分母必须由固定版本、剧情条件和玩家能力约束下的可达闭包生成。目前 `eligibleSinglePlayerMaps`、规则数、脚本数和完成率均为 `null`，表示尚未测量。

目录证据：[地图组](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/map_groups.json)、[布局目录](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/layouts/layouts.json)。

## 4. 数据格式和真实入口

以下路径均针对固定 commit。路径模板中的 `*` 表示目录类别，不表示已逐文件审核。

| 内容 | 实际源入口 | 接入合同 |
| --- | --- | --- |
| 地图身份/排序 | `data/maps/map_groups.json` | 保留源组、源序号及符号；运行时稳定 ID 不使用数组下标 |
| 地图与事件 | `data/maps/*/map.json` | JSON 包含 layout、连接、对象、warp、坐标触发、背景事件及地图属性；保留字符串常量和类型 |
| 布局 | `data/layouts/layouts.json`、`data/layouts/*/map.bin`、`border.bin` | JSON 指定宽高、主/副 tileset、二进制路径；正确处理空布局槽位 |
| Tileset 绑定 | `src/data/tilesets/headers.h`、`graphics.h`、`metatiles.h` | C 初始化器与 INCBIN 引用把渲染、属性、调色板和动画回调连接起来 |
| 图块源 | `data/tilesets/primary/*`、`secondary/*` | PNG、文本调色板、metatiles.bin、metatile_attributes.bin；不把 PNG 当完整碰撞地图 |
| 物种 | `src/data/pokemon/species_info.h`、`include/constants/species.h` | C 指定初始化器、宏和保留条目；source ID 不等同于全国图鉴号 |
| 成长 | `src/data/pokemon/level_up_learnsets.h`、`level_up_learnset_pointers.h`、`evolution.h`、`tmhm_learnsets.h`、`egg_moves.h` | 展开学招/进化规则时仍需运行逻辑、全国图鉴门槛与取得渠道约束 |
| 招式 | `src/data/battle_moves.h`、`include/constants/moves.h` | effect/type/target/priority/flags 是语义入口；不能仅凭威力字段实现招式 |
| 遭遇 | `src/data/wild_encounters.json` 与 `.json.txt` 模板 | JSON 含陆地、水面、碎岩、钓鱼；模板按 FireRed/LeafGreen 标签条件生成 C |
| 道具 | `src/data/items.json` 与 `.json.txt` 模板、`include/constants/items.h` | JSON 的字段/战斗使用函数名称要映射到审阅过的处理器；不能执行字符串 |
| 训练家 | `src/data/trainers.h`、`src/data/trainer_parties.h` | 队伍宏区分持有物/自定义招式，保留 AI、双打和版本条件；数值生成还依赖原文名字编码 |
| NPC 交换 | `src/data/ingame_trades.h` | 正常单机内容；固定个体值、OT、持有物和版本分支不等同于联网协议 |
| 场景脚本 | `data/maps/*/scripts.inc`、`data/event_scripts.s`、`data/scripts/*` | GAS 风格宏、标签、控制流；需要识别 include 和版本条件，不是可直接执行的 C# |
| 脚本语义 | `asm/macros/event.inc`、`data/script_cmd_table.inc`、`src/scrcmd.c` | 三者分别约束参数编码、opcode 对应关系及执行行为；未知命令必须报错 |
| 原生 special | `data/specials.inc` 及对应 `src/*.c` | 不是普通脚本文本；每项 special 建立具名领域实现及源映射 |
| flags/vars | `include/constants/flags.h`、`vars.h`、`src/event_data.c` | 临时状态、长期剧情、重入与保存语义分开处理 |
| 战斗语义 | `data/battle_scripts_1.s`、`src/battle_script_commands.c`、`battle_main.c`、`battle_util.c`、`pokemon.c` | 表、脚本和 C 顺序共同决定行为；静态数据解析成功不等于规则正确 |
| 原文/控制码 | `data/maps/*/text.inc`、`src/data/text/*`、`charmap.txt` | 源文件字符与游戏编码分开；参数、换页、等待、音效控制需进入文本 IR |
| 音频 | `sound/song_table.inc`、`sound/voice_groups.inc`、`sound/songs/*`、`sound/direct_sound_samples/*` | 汇编/序列、MIDI、WAV 及音色/采样关联；不能把文件名清单当可播放音频包 |

### 4.1 不能沿用旧教程中的路径

这个 commit 没有 `src/data/pokemon/base_stats.h`；入口是 `species_info.h`。遭遇源在 `src/data/wild_encounters.json`，不在 `data/wild_encounters.json`。tileset 绑定位于 `src/data/tilesets/*.h`，不是 `data/tilesets/*.inc`。

`src/data/items.h`、`src/data/wild_encounters.h`、`include/constants/map_groups.h`、`layouts.h` 和 `map_event_ids.h` 是上游构建生成项，不是可直接 fetch 的已提交输入。应读取 JSON/模板和规则，不能因 404 换到另一分支抄文件，更不能为了得到生成项直接运行未知上游工具。

生成规则证据：[map_data_rules.mk](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/map_data_rules.mk)、[json_data_rules.mk](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/json_data_rules.mk)。

### 4.2 地图二进制不能套用别作格式

`map.bin` 的格子是 u16：低 10 位 metatile ID，中间 2 位碰撞，高 4 位高度。布局中的边界宽高也必须读取。真新镇为 24×20；tree 中 map.bin 为 960 字节，border 为 2×2、8 字节，仅在元数据层面符合 u16 尺寸。

FireRed 的 `metatile_attributes.bin` 由 INCBIN_U32 引用。`src/fieldmap.c` 中行为掩码为 `0x000001ff`，terrain 位于 9–13 位，encounter type 位于 24–26 位，layer type 位于 29–30 位。必须保留完整 32 位属性和已命名/未命名字段，不套用其他作品的 16 位属性或把碰撞等同于行为。实际小端读取、metatile 层组合、tile 翻转/调色板和 primary/secondary 分界仍需 P0 后的解析夹具与真渲染对照。

证据：[global.fieldmap.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/global.fieldmap.h)、[fieldmap.c](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/fieldmap.c)、[metatiles.h](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/data/tilesets/metatiles.h)。

### 4.3 JSON、C 和脚本各有不同解析责任

JSON parser 不会自动处理 C 宏、条件编译或脚本 opcode。遭遇模板明确区分版本；同一地图还可能受钓竿、冲浪、剧情 flag 或变量选表影响。`evolution.h` 中出现的进化条目也不保证 FireRed 运行逻辑会启用它。接入器应采用有界解析器、显式宏规则和未知语法拒绝，不能用一个正则扫描器宣称支持整个仓库，也不能动态执行仓库文本。

## 5. 权利与再分发登记

固定 tree 根目录没有 LICENSE/COPYING，README 未提供仓库级许可授权。树中若干 tools 子目录各有 LICENSE/COPYING；已读 `tools/gbagfx/LICENSE` 为 MIT 文本，该许可不能外推到游戏代码、规则数据、对白、图形、音频或标识。此次不是完整法律清权审计，因此仓库级权利记录为 `not_established`，不武断标为 MIT、公共领域或“可任意商用”。

公开可读与重新分发是不同判断；GitHub 的[许可说明](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)也区分了两者。工程处理是保留来源与许可范围，发布前单独取得足够权利依据或替换相应内容，不将这段记录当法律意见。

| 来源类别 | 当前允许的工程处理 | 发布状态 |
| --- | --- | --- |
| pret 文本源码 | 固定版本只读研究、记录事实和 sourceRef；本次不把全文并入产品 | 游戏源代码/数据再分发未清权 |
| 上游工具 | 阅读实现和本地许可；此阶段未编译/执行 | 如后续引入，按具体组件许可及依赖逐项核查 |
| 原游戏图像、对白、声音、商标 | 建立外部来源记录；P0 使用自制测试资源 | 未公开上传，未视为获得授权 |
| 简体中文、字体与自制替代素材 | 单独记录作者/来源、许可版本和使用范围 | 许可核验后才纳入交付清单 |
| 合法原版行为记录 | 后续用于规则对照，记录采样版本与取得依据 | 不附带 ROM，不默认公开原始素材 |

## 6. sourceRef 与后续接入合同

每个未来内容项至少保留：

- 项目 `stableId`，内容类型，源符号/source ID。
- repository、完整 commit、相对文件路径、符号；行范围可辅助阅读，不能独自作为身份。
- 输入 SHA-256 与 Git blob SHA-1，已采用的版本条件。
- `reachabilityReason`、前置 flag/var/道具/成长路径，分支互斥条件。
- `requires`、实现状态、兼容差异 ID、测试 fixture ID 与证据路径。
- 原文/图形/音频/代码分开的权利来源与分发状态。

项目稳定 ID 与源 numeric ID 分开，存档只依赖稳定 ID。译名不参与 ID、规则比较或训练家 personality 生成。未知 opcode、special、宏、flags、图块及缺字都返回含路径/符号的诊断；没有无声占位。

### P0 通过后的最小接入顺序

1. 验证 lock 对应 commit/tree；在产品目录外建只读来源缓存，按白名单抓取所需文件并比对 hash。拒绝路径逃逸、未解析 include 和来源漂移。
2. 先锁定 IR schema 与稳定 ID，写坏数据、条件分支、重复 ID 和缺失引用夹具。采用本项目可审计解析器；不执行上游脚本，也不需要 ROM。
3. 一次只接入一张地图、一个物种和一段脚本。地图所需依赖可以读取，但尚未宣称其他区域可玩。
4. 两次冷生成的规范化产物字节一致，输出 sourceRef 与依赖清单。此时再验证 tile/碰撞/事件/文本真实表现，不能以静态编译替代。
5. 建立首徽章可达闭包，再按区域增量扩展至完整单机目标。每批先列缺失机制，再导入内容；每次变动都保留保存/回放兼容决策。

当前停止线：只交付清单和来源证据。上述接入器、全游戏分母和规则一致性验证均待实施；P0 未通过时不批量扩内容。
