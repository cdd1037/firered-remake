# 资源来源、重建与未来公开仓库边界

更新：2026-10-09。本文是工程获取/重建指南和发布准备清单，不是法律意见、素材授权证明或“可直接公开”的承诺。未来 GitHub 应以经过审查的原创实现、工具、来源锁和合成测试为主要内容；原作资源由使用者自行准备符合其适用法律及授权条件的输入，在本地生成。

**当前结论：主仓库不是独立、从零可重建的公开发行包。** 图像/字体/地图已有分批导入工具；音频已完成本地原引擎渲染和冻结导入，但渲染器与参考构建工具仍在仓库外。部分验证依赖本地冻结存档、截图或 golden。不要通过公开恢复归档、整个工作目录或当前 Git 历史来补齐这些依赖。

## 1. 发布边界：代码、工具许可与游戏素材分开

| 类别 | 拟进入公开源码分发的内容 | 本地保留/另行核查的内容 |
| --- | --- | --- |
| 项目原创实现 | `src/` 中原创 C# 规则、应用、Godot 适配；原创 Python/shell 工具；结构说明 | 仍须审阅复制的源片段、嵌入对白、资源字节、生成表与作者归属，不能只因扩展名是 `.cs` 就放行 |
| 来源索引 | 固定 repository/commit、相对路径、hash、schema、转换步骤 | 来源锁若带完整原文、样本数据、截图或机器路径应拆分；hash 验证不是权利许可 |
| 上游工具/库 | 优先记录固定获取地址与版本；确需分发时保留各组件许可和通知 | pret 的工具局部许可不覆盖游戏代码/素材；mGBA、agbcc、Godot、.NET 与依赖各自审核，不把整套 source/build/vendor 目录直接搬入 |
| ROM 与参考构建 | 版本标识、输入校验方法 | `.gba`、匹配 `.elf`/`.map`、multiboot 构建二进制及参考源码完整副本不随本项目公开包发布 |
| 原作美术/文本/声音 | 转换器和格式说明 | 原 PNG、图块/调色板、地图、脚本/对白、字形、MIDI、sample、cry、渲染 WAV，以及从它们生成的图集/JSON/PCM 均按原作衍生材料处理 |
| 字体 | 许可已核对的独立字体可成为单独审核条目 | `content/generated/source-font` 是原游戏字形；`src/Game.Godot/Assets/ProbeFont.ttc` 是独立 Noto 探针字体，两者不能混为一谈。后者已有 `FONT-LICENSE.txt`，仍须匹配实际文件来源、版本和许可义务 |
| 测试 | 可证明原创的合成地图、占位画面、规则单元测试、生成合成 fixture 的工具 | 原作截图、mGBA state、真实玩家存档、native trace、完整对白、冻结 checkpoint、golden 和失败 dump 默认不直接发布；JSON/压缩日志也可能携带这些材料 |
| 环境 | 可移植配置模板、锁定工具版本 | `.tools`、`.toolhome`、`.play`、`bin/obj/.godot`、工具链/缓存、导出物、私人绝对路径、账号/凭据、云存储链接不随源码包发布 |

“其他仓库公开可读”不代表其游戏资源可以重新分发。新增来源必须按组件记录作者、许可路径/版本、所需 notices、是否原作衍生、拟公开范围及未决问题。无法建立许可依据的资源保持外置或用独立原创资源替代。本次未替项目选择开源许可证；目前也未发现仓库根级 `LICENSE`/`COPYING`。代码开源许可不能替素材清权。

## 2. 固定输入与工作目录

唯一游戏基线：英文 FireRed revision 0，`FIRERED / REVISION=0 / ENGLISH / MODERN=0`。

- pret/pokefirered commit：`037335f4c725d7c9aecdac87066f2002b4bd7e14`
- tree：`7515cd4067ea55f8cdbc73624478afd2736c58b9`
- ROM：16,777,216 bytes；SHA-1 `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc`
- ROM SHA-256：`3d0c79f1627022e18765766f6cb5ea067f6b5bf7dca115552189ad65a5c3a8ac`
- 参考编译器 pret/agbcc commit：`da598c1d918402c42c0c0d7128ba14567f3175e9`
- 官方 mGBA：0.10.5，commit `26b7884bc25a5933960f3cdcd98bac1ae14d42e2`；源码 archive SHA-256 `91d6fbd32abcbdf030d58d3f562de25ebbc9d56040d513ff8e5c19bee9dacf14`

上游定位：[固定 pret 源码](https://github.com/pret/pokefirered/tree/037335f4c725d7c9aecdac87066f2002b4bd7e14)、[固定安装说明](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/INSTALL.md)、[固定 agbcc](https://github.com/pret/agbcc/tree/da598c1d918402c42c0c0d7128ba14567f3175e9)、[官方 mGBA 0.10.5](https://github.com/mgba-emu/mgba/tree/0.10.5)。这些是版本/来源入口，不是游戏素材授权。本文不给 ROM 下载站；使用者自备合法且版本匹配的输入。合法取得、提取、构建和再分发是分别需要判断的问题。

当前工作区的恢复参考源相对位置如下，不是未来用户必须照搬的路径：

```sh
# 从 firered-remake 根目录执行；这些目录必须已经存在。
export FIRERED_ORIGINAL_REFERENCE="$(cd ../restore-stage1-reference/extracted/firered-stage1-5ae6af4-2026-10-08/reference/pokefirered && pwd)"
export FIRERED_TOOLCHAIN_ROOT="$(cd ../firered-toolchains && pwd)"
git -C "$FIRERED_ORIGINAL_REFERENCE" rev-parse HEAD
git -C "$FIRERED_ORIGINAL_REFERENCE" rev-parse '037335f4c725d7c9aecdac87066f2002b4bd7e14^{tree}'
./dev doctor
```

`.tools/reference-root`、`.tools/toolchain-root` 当前保存本机路径，不应提交。`scripts/env.sh` 支持上述变量；默认工具目录也支持 `/workspace/shared/firered-toolchains`。主工具链由 `toolchain.lock.json` 锁定：Godot 4.7.2 .NET、.NET SDK 10.0.401；实测运行时为 10.0.12。已有工具可直接校验；`./dev setup` 是显式下载/安装操作，本次文档工作没有运行它。

## 3. 两条输入链，不能混称“导入 ROM”

### A. 固定源码/源素材 → 有界转换 → Godot

大多数现有地图、文本、精灵、字形导入器读固定 Git blob 或已校验的源文件，不读 ROM，不执行上游游戏源码。典型链路：

1. 来源 lock 指定 commit、文件白名单、Git blob、字节数与适用的 SHA-256。
2. fetch/读取阶段核验输入，写入本地 `content/raw/<profile>`。
3. 项目原创转换器处理图块、调色板、source ID、字体 advance、地图层/事件与受支持脚本。
4. 输出 `content/generated/<profile>` 的图像、规范化 JSON、manifest/hash；其中可能嵌入原文，仍是外置生成资源。
5. `Game.Content` 类型化读取与验证；`Game.Godot` 做展示，不把动画时序当作领域规则。
6. 源数据、负向、应用规则与真实 native 流程分别验收。

下列是真实存在的入口，**不是覆盖全游戏的一键批处理**。首次执行会生成本地资源；这里记录命令，不表示本次重新跑过。

```sh
# Pallet：cache 缺文件时会联网固定 raw.githubusercontent.com URL。
# 该旧入口没有 --offline；离线要求先准备完整 cache，勿假称其强制离线。
python3 scripts/fetch-pallet.py --cache "$FIRERED_ORIGINAL_REFERENCE"
python3 scripts/import-pallet.py
python3 tests/Game.Map.Tests/test_import.py

# 已支持 cache + 强制离线的原字体与 starter 素材。
python3 scripts/fetch-source-font.py --cache "$FIRERED_ORIGINAL_REFERENCE" --offline
python3 scripts/import-source-font.py
python3 scripts/fetch-starter-art.py --cache "$FIRERED_ORIGINAL_REFERENCE" --offline
python3 scripts/import-starter-art.py

# 后续地图入口直接读取固定本地 Git 对象，包含其本地采集/转换阶段。
python3 scripts/fetch-viridian-region.py --reference-repo "$FIRERED_ORIGINAL_REFERENCE"
python3 scripts/import-viridian-region.py
python3 scripts/import-forest-region.py --reference-repo "$FIRERED_ORIGINAL_REFERENCE"
python3 scripts/import-pewter-region.py --reference-repo "$FIRERED_ORIGINAL_REFERENCE"
```

依赖和 profile 细节见 [地图测试说明](../tests/Game.Map.Tests/README.md)、`scripts/*lock.json` 及各脚本 `--help`/实现。部分入口默认位置、cache 语义和网络行为不同；一些 importer 也含采集逻辑，不要仅按 fetch/import 文件名前缀推定权限或行为。已有图像流程有 Pillow 依赖，当前尚无统一的跨平台依赖安装及完整依赖拓扑执行器。

### B. 本地参考构建 → 原声驱动离线渲染 → PCM 导入 → Godot

音频不能把 MIDI 配通用音色库就当原作，也不能把 source sample WAV 直接当最终 cry。

真实本地流程在 `../audio-reference-build/`：

- `pokefirered/`：固定源码的隔离副本，构建出 hash 匹配的 `.gba/.elf/.map`。
- `agbcc/`、`sysroot/`、`build-packages/`：固定编译器和本地依赖；不塞进主仓库。
- `emulator/mgba-0.10.5/`：官方原源码，没有修改原 mGBA 源码。
- `emulator/build-existing-gcc.py`：项目侧构建适配器，读取官方 CMakeLists 源清单，使用已有 GCC 构建核心；不是修改/伪造 mGBA。
- `render/render_audio.c`：原创 headless 适配层；运行原 ROM 的 m4a/cry 函数，从官方核心取得采样。
- `render/audio_call.s`：一次性 Thumb 调用 helper；磁盘上的基线 ROM 保持不变。
- `render/render_batch.py`：批量渲染、原序列时钟 trace、loop 与 manifest；依赖同目录 renderer 二进制、symbols，以及相邻主仓库的 source lock。

参考构建的真实手动核心步骤是：先在隔离副本按 `REBUILD-REFERENCE.md` 准备依赖，构建/安装固定 agbcc，再在其 `pokefirered` 目录执行：

```sh
make -C tools/gbagfx LIBS='-lpng -lz -lm'
make -C tools/rsfont LIBS='-lpng -lz -lm'
make -j4 compare GAME_VERSION=FIRERED GAME_REVISION=0 GAME_LANGUAGE=ENGLISH MODERN=0
sha1sum pokefirered.gba
sha256sum pokefirered.gba
```

这些命令会执行第三方构建代码，**不是**只读 fetch；需先完成来源/依赖审阅并取得适用授权，不由主项目测试或本文自动触发。完整当前构建事实以仓库外 `CURRENT-STATUS.md`、`current-build-verification.json`、`logs/` 为准，历史 `REBUILD-REFERENCE.md` 的旧“未重跑”状态不能覆盖新的成功记录。不要更改恢复原件。已有记录显示 `make compare: OK`，ROM hash 和尺寸匹配；本次编写指南没有重跑构建。

渲染和导入的真实命令：

```sh
# 主仓库：校验 source lock，不写新 lock。
python3 scripts/audit-early-audio.py --reference-root "$FIRERED_ORIGINAL_REFERENCE"

# 完成上述外部工具与 symbols 准备后，在独立新输出目录渲染。
(cd ../audio-reference-build/render && \
  python3 render_batch.py --all --sound-mode mono --output candidate-mono-output)

# 当前产品只接受已审阅冻结批次；candidate 不是自动替代品。
python3 tests/Game.AudioSourceAudit.Tests/verify_rendered_audio.py \
  --render-root ../audio-reference-build/render/mono-output \
  --repeat-root ../audio-reference-build/render/repeat-output
python3 scripts/import-early-audio.py \
  --render-dir ../audio-reference-build/render/mono-output \
  --rom ../audio-reference-build/pokefirered/pokefirered.gba
python3 tests/Game.EarlyAudioContent.Tests/test_import_early_audio.py \
  --rom ../audio-reference-build/pokefirered/pokefirered.gba
```

`candidate-mono-output` 是渲染器现有 `--output` 参数的示例新目录；尚未建立自动 review/freeze/promotion 命令。不要覆盖 `mono-output`，不要为让测试变绿而重写 golden/digest。当前 importer 还锁定 FROZEN、verification、renderer evidence 与二进制身份；在另一编译器/平台上重新得到音频，不保证能通过旧批次冻结身份校验。应独立复核语义、PCM 差异和溯源后另建版本。

当前冻结输出：134 个 clip（31 music/fanfare、73 SE、30 normal cry）、48 kHz PCM16 双声道容器，原游戏 MONO 路由，277 个导入文件、WAV 共 289,233,620 bytes。Manifest SHA-256：`cd4d857bb6e220925834302119ae66efb1cfdc4a014f49053dbee1956a62aaec`。输出是 `content/generated/early-audio/`；不会导入 ROM、emulator 可执行文件或旧 STEREO 试验批次。

**可移植性缺口：** 外部 renderer、构建适配器、符号生成与冻结操作尚未作为完整可审计工具包进入主仓库。其源码快照在本地生成材料中存在，不等于公开 clone 能取得整套工具。路径仍假设兄弟目录名。未来应迁入仅原创工具源码及独立许可清单，外置所有原作输入/输出，并统一显式路径参数；不要用提交 ROM 或整包 renderer-output 来解决这个问题。

## 4. 验收状态与不可混淆的“重建成功”

| 层级 | 现有证据/状态 | 尚不能据此声称 |
| --- | --- | --- |
| 固定参考构建 | 当前外部记录有 hash-matched ROM、ELF/map 和 make compare 成功 | 任意新机器/操作系统上从公开 clone 零输入全自动复现 |
| 原声离线核心 | 官方 mGBA core 成功产出非零 PCM；134 clip 完整批次已生成 | 扬声器实际有声音、原作所有混音交互一致 |
| 导入与重复 | 277 文件重复导入校验；代表性 heal/Select/Charmander 三条独立进程 rerender 字节一致 | 全部 134 条都已独立冷重渲染，或跨平台二进制可重现 |
| 音频精度 | 23 条 loop 的原序列 VBlank 边界被记录 | sample-exact 无缝 loop；已知最大 wrap 跳变 7,021 PCM16 单位，共享硬件声道/混响竞争与特殊 cry 模式仍待验证 |
| 原生游戏 | 多批应用/原生流程证据与本地 checkpoint 存在 | 单靠 source-only clone 能跑完全部参考回归；全部游戏已完成 |
| 公共分发 | 本文和只读审计工具已新增 | Git 历史/LFS/材料权利已清理，或当前工程 public-ready |

现有集成入口：`./dev build`、`./dev test`、`./dev test-reference`、`./dev test-presentation`、`bash scripts/launch-current.sh`。`test-reference` 需要外置转换产物及部分冻结材料；native 脚本也可能依赖本地存档/窗口环境。`./dev verify p0` 在实际音频输出验收未完成时仍应报告不通过。源码测试、Dummy/headless 播放请求不等于听音验收。

以后应把测试分为：可公开的纯合成回归；用户本地资源重建回归；需要真实 native/音频设备及合法参考材料的独立验收。对缺资源必须明确报缺项，不能静默跳过后标为通过。

## 5. 本次 Git 初步审计与只读脚本

本次对当前 index 和本地 `--all` 可达对象执行只读审计，基线 HEAD `f5b4ea4645784591e761ca212232f0dcb35ca28c`，76 个本地可达 commit，非 shallow。观察到 2,609 个 index 条目；其后并行开发可能改变数量。

- 已跟踪 570 个 `.log`、995 个 `.json`、3 个 `.jsonl`，不能按“都是文本”直接允许发布。
- 已跟踪 5 张 `evidence/` PNG；文件名是 harness/probe 相关，可能为原创占位画面，但仍需逐张看图和来源审阅，不能误报为确定侵权，也不能直接放行。
- 已跟踪 `ProbeFont.ttc`（超过默认 2 MiB 内容扫描阈值）和字体许可记录；文件许可关系需复核。
- 已跟踪 `tests/Game.StorageNative.Tests/evidence/failed-initial-compilation.json.gz`；压缩内部未展开审计。
- index 加历史对象分类中，1,074 个记录出现机器绝对路径标记，96 个记录出现结构化游戏/玩家状态字段；这些是需复核信号，不等于 1,074 份秘密或 96 份真实用户存档。
- 本次未在扫描记录里发现 LFS pointer 或 submodule；未检查远端 LFS payload、其他远端 refs、历史重命名全部路径、reflog/不可达对象。不能据此说“LFS/历史全通过”。

新增工具：`scripts/audit-public-resource-boundary.py`，仅 Python 标准库与 Git；不联网、不下载、不删除、不上传、不修改 index/历史、不展开压缩包、不跟随符号链接。它检查 **index 的 Git blob**，不是把当前工作树当已经提交的内容；`--history` 补扫本地可达历史 blob。报告只输出风险类别/路径/object ID，不抄出命中的私密文本。

```sh
python3 scripts/audit-public-resource-boundary.py --self-test
python3 scripts/audit-public-resource-boundary.py
python3 scripts/audit-public-resource-boundary.py --history > /tmp/firered-public-boundary-audit.json
```

`--self-test` 是 13 个纯内存 fixture；退出 0 表示该自检通过。普通审计成功退出 **2** 并明确 `publicReady=false` / `manual_review_required`；退出 1 表示审计本身不完整。它不提供“版权全绿”结果。可用 `--max-bytes` 调整单 blob 内容扫描上限；大文件被标 `content_not_scanned`，不被默认为安全。

覆盖还包括媒体魔数、LFS pointer、嵌入源内容字段、日志/fixture/golden 路径、license 记录及本地路径标记；未命中也保留 `unclassified_review`。历史 object 可能只有一个代表路径，不恢复全部 rename；分类可重复、不能简单相加当唯一文件数。报告会暴露文件名与本地结构，原始报告暂作本地材料。

本次完整报告在上述 `/tmp` 路径，仅供本地复查，不作为公开仓库附件。未扫描 unstaged 修改、未跟踪/被忽略文件的内容；未审远端、未做法律清权或完整 secret scan。脚本本身与本文为新增未跟踪文件，也需要未来发布审查。

## 6. 未来按阶段完善，不抢先承诺已实现

1. **已实现：** 固定来源锁、多个有界 fetch/import、类型化资源验证、原字体转换、音频 source audit/冻结批次 importer、本文的只读公开边界审计。现有负向测试拒绝错误 hash、缺失/损坏素材等；各模块能力不完全一致。
2. **下一步：统一输入契约。** 新增一个可移植资源清单，逐项列 repository/commit/license、用户输入类型、精确 hash、依赖、输出、转换器版本、是否需要 ROM、网络权限与再分发分类。建立强制 `--offline` 和统一 `--source-root/--rom/--output`；缺项给出获取说明，不自动查 ROM 网站。
3. **下一步：迁移音频工具。** 仅迁移原创 renderer/helper/batch/build-adapter 源码，核查上游头文件/链接许可，保留官方源码未改事实。补 symbols 生成、依赖检测、构建与冻结审核命令，消除目录硬编码。任何下载/安装/第三方程序执行保持显式。
4. **后续：统一资源 DAG。** 地图/tileset/palette、精灵/动画、字体、文本/脚本、音乐/SE/cry 分开插件化；按固定依赖闭包采集→提取→规范化→导入 Godot。失败不污染已有输出，输出 manifest 保留输入/工具 hash。新增“其他仓库”来源先评估许可与格式，不能混进默认 pret baseline。
5. **后续：公开纯合成测试。** 从私人 native checkpoint/golden 中提炼不含原作材料的断言/场景生成器；原作像素与声音对照继续是使用者本地验收。记录哪些 gate 因缺输入未运行。
6. **最终：干净环境验收。** 在空目录只给审阅后的源码、固定工具与用户合法输入，断网冷重建两次，比较规范化输出、资源完整性与 native 运行；音频另做真实输出/loop/混音验收。此端到端干净分发重建尚未实际跑过。

## 7. GitHub 发布前的独立停止线

`.gitignore` 只影响未跟踪文件，不会删除已跟踪内容、历史或 LFS 数据。未来建议从明确允许清单生成**新的干净分发目录/仓库**，先审查再决定是否发布；不要自动执行 `filter-repo`、force push、删除历史或把现有恢复归档公开。

发布准备必须包含：

- 内容审查当前 index/工作树、commit/tag 历史、LFS 和大文件、子模块、压缩物与 fixture 内嵌数据；另做真实 secret/privacy scan。
- 建立原创代码作者/许可清单，保留每项第三方 notices；不要用项目新 LICENSE 覆盖原作素材。
- 从白名单导出源码和合成测试；不整体 `git archive HEAD` 后当已清理，也不复制当前 `.git`。
- 在干净导出物中再次运行审计并人工处理每项；确认 CI/artifact/releases 不重新上传外置资源或包含资源的可执行导出。
- README 明确“用户自行准备输入”、当前缺口、可运行的重建步骤、未通过验收，以及如何运行无需原作资源的测试。
- 发布动作、仓库目标及材料范围单独确认。本次仅新增文档/只读审计，没有公开、清理、改历史或迁移外部 renderer。

相关文档：[早期来源研究（保留历史阶段语境）](source-baseline.md)、[音频渲染事实与限制](audio-pipeline-source.md)、[冻结音频导入合同](early-audio-content.md)、[Godot 音频适配](audio-playback-adapter.md)。

## Battle-medicine acceptance fixtures (0604388)

The medicine campaign suite requires three private saves, including the historical `tests/Game.BasicMenuIntegrityAudit.Tests/fixtures/before-liam-shift.json`. Its SHA256 is `3d55f9c358f9a0e8c44da2faaa41a19cfafea833f66904d6673c62d2f2374cea`; the same bytes are retained at `tests/Game.FieldPickupAudit.Tests/fixtures/source-storage.json`. The other two are fixed FieldPickups acceptance outputs (`picked-1001.json` and `upgraded-storage.json`). See the medicine suite README for all hashes and the existing BasicMenu `prepare-fixtures.py` workflow. They are not obtainable from a bare source clone without the authenticated local inputs or retained private archive.
