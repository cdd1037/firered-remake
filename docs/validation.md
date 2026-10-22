# 验证记录与已知限制

本页汇总当前检查状态和历史证据入口。代码、资源、实际路线、Godot 窗口、独立包和真实音频分别验证；一个层面的成功不能代替其他层面。

## 最近一次检查

2026-10-21，在 `efadbcf1` 基线上修复不完整重构导致的缺失定义与开发入口还原错误。以下结果来自 Windows 本机，使用 .NET SDK 10.0.402、官方 Godot `4.7.2.stable.mono.official.ed1daf0bf`。

| 检查 | 实际结果 |
| --- | --- |
| `python dev doctor` | 通过；SDK、Godot 和本机 Godot NuGet 包目录可用 |
| 标准解决方案串行构建 | 通过，0 警告、0 错误 |
| `python dev build` | 还原与构建均通过，0 警告、0 错误；使用生成的 `.tools/nuget.config` 声明本机 Godot 包源和 nuget.org |
| Domain 完整测试项目 | 377 项通过，0 失败、0 跳过 |
| Battle 完整测试项目 | 1806 项通过，0 失败、0 跳过 |
| Content 定向回归 | 86 项通过；覆盖普通训练家、地区加载、早期遇敌、名称和稀疏文本覆盖 |
| Presentation 定向回归 | 93 项通过；覆盖选项、队伍动作、文字打印、输入、保存反馈、方向时序和事件 HP 投影 |
| Process 完整测试项目 | 1 项通过；三个独立冷进程实际 CLR major 为 10，canonical hash 与存档往返一致 |
| Application 定向回归 | 51 项中 50 项通过、1 项失败；失败项为受控存档检查，本地缺少 `checkpoints/current-schema3/controlled-checkpoint.json` |
| Pokémon 准入导入检查 | 12 项通过；继续使用共享定义和显式 roster 引用 |

未执行默认七项目的完整测试、音频测试、真实 Godot 游戏宿主 CLR、原生窗口、导出或真实声卡验收。受控存档失败保持为失败，没有生成替代档、转换旧档或推断玩家统计。交换场景图像检查按本次任务要求不重复，本轮曾尝试的相关修改已撤回。

### 修复取舍

- 补齐 `OrdinaryTrainerCatalog`、`RegionalTrainerDocument` 与 `RegionalTrainerLoader`：共享定义与脚本出场位置分别保存，构造时校验，普通与特殊战斗仍有明确边界。继续使用共享训练家生成器，不恢复已被替代的地区工厂。
- 从本地 Git 历史核对并恢复仍有调用的 Unicode 名称规则、文本包、名称投影、控制码展开、菜单准入和 Godot 文本／输入适配。恢复后的演出适配使用当前 canonical `SourceTowerHpEvent`，没有恢复旧战斗 DTO 层。测试中的源码路径随目录更新。
- 补齐当前早期遇敌和 Lost Cave 目录读取器，保留既有内容约束。Lost Cave 直接解析现有转换资源并引用共享训练家定义，不依赖再战目录重建初次对战内容。
- 恢复仍被准入编译器调用的 `game_pokemon_admissions.py`，继续解析共享 schema 2 定义、用途引用与显式进化差异。

### 执行命令

```powershell
python dev doctor
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
python dev build
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --no-build --no-restore
dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --no-build --no-restore
dotnet test tests/Game.Process.Tests/Game.Process.Tests.csproj --no-build --no-restore
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~OrdinaryTrainerCatalogTests|FullyQualifiedName~RegionalTrainerLoaderTests|FullyQualifiedName~SourceNameTextPackTests|FullyQualifiedName~SourceTrainerDisplayNamesTests|FullyQualifiedName~EarlyLandCompositionTests'
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~OptionEditingTests|FullyQualifiedName~PartyPresentationTests|FullyQualifiedName~BattleTextPrinterTests|FullyQualifiedName~SourceTextInputTests|FullyQualifiedName~SaveFeedbackTests|FullyQualifiedName~FieldDirectionCadenceTests|FullyQualifiedName~CanonicalBattleHpPresentationTests'
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~CampaignCompositionTests|FullyQualifiedName~TrainerDisplayTextTests|FullyQualifiedName~CampaignFieldMenuAdmissionTests'
python scripts/checks/test-game-pokemon.py
```

### 本地输入与失败记录

本轮修复最初将参考仓库拉取到工程外的 `../pokefirered-reference`，随后按任务要求迁为 `reference/pokefirered/` 子模块，固定提交与 tree 保持不变。当前初始化方式见[参考源说明](source-baseline.md#本地参考子模块)。以下保留迁移前的拉取命令记录，远端为 `https://github.com/pret/pokefirered.git`：

```powershell
git -C ../pokefirered-reference fetch --depth=1 origin 037335f4c725d7c9aecdac87066f2002b4bd7e14
git -C ../pokefirered-reference rev-parse FETCH_HEAD
```

只从该固定版本物化缺失输入，没有执行上游脚本或下载 ROM。补入的 `src/data/text/nature_names.h` SHA-256 为 `a8e82bf571e9eab767f7bcc654befc8fe1782c34250fe89e2542ef2894e55031`。现有导入器更新了过期的本地道具描述、训练家职业名称、Pokémon 准入、Route 1 和老人教学资源；Summary 仅更新文字和显示名称 JSON。旧物种表现与游戏厅菜单元数据通过现有 `species_art.build` 和 `import-game-corner-prizes.py` 的 `generate` 重新投影，没有修改源码读取器去接受旧字段，也没有重写 PNG。

主要资源准备命令：

```powershell
python scripts/imports/items/import-item-catalog.py
python scripts/imports/items/import-item-catalog.py --check
python scripts/imports/trainers/import-shared-trainers.py
python scripts/imports/pokemon/import-game-pokemon.py
python scripts/imports/pokemon/import-route1-pokemon.py
python scripts/imports/story/import-old-man-tutorial.py --repo ../pokefirered-reference
```

Domain 初次检查为 375 通过、2 项因参考向量缺失而失败；Battle 初次完整检查为 1786 通过、20 项因向量缺失而失败。用以下项目自有生成器补齐缺失数值向量后复测通过。生成器核验固定来源，以独立 Python 算术或 MSVC 编译的有界 C 参考程序计算，不读取 C# 测试结果，不覆盖不同的已有期望；没有执行参考仓库的构建脚本。

```powershell
python tests/Game.Domain.Tests/GrowthCapture/reference_vectors.py content/raw/pokefirered/037335f4 tests/Game.Domain.Tests/GrowthCapture/Fixtures
python tests/Game.Battle.Tests/Turns/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Turns/Fixtures --fixtures-only
python tests/Game.Battle.Tests/Switching/build-source-oracle.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Switching/Fixtures --fixtures-only
python tests/Game.Battle.Tests/Residuals/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Residuals/Fixtures --fixtures-only
python tests/Game.Battle.Tests/Moves/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Moves --fixtures-only
python tests/Game.Battle.Tests/Items/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Items/Fixtures --fixtures-only
python tests/Game.Battle.Tests/FieldEffects/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/FieldEffects/Fixtures --fixtures-only
python tests/Game.Battle.Tests/Breeding/probe_source_iv.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Breeding/Fixtures/iv-vectors.json
```

首次 Turns 向量生成因缺少 `include/battle_util.h` 失败，从同一固定提交补齐该头文件后生成通过。回归期间暴露的旧资源格式、缺失资源和旧测试路径失败也保留在本地日志中；一次宽范围执行在共同的内容加载错误明确后中止，不计为完整回归。本轮日志位于忽略目录 `evidence/repair-*`，raw、generated、向量与参考源码不纳入产品源码提交。

## 文档整理时的基线检查（2026-10-21）

2026-10-21，Windows 文档整理期间，对修改前的源码基线 `4234fdea` 执行以下检查。本次仅编辑文档，未修复下述源码或工具问题。

| 命令 | 实际结果 |
| --- | --- |
| `python dev doctor` | 通过；.NET SDK 10.0.402、Godot `4.7.2.stable.mono.official.ed1daf0bf`，Godot NuGet 包目录存在 |
| `python dev build` | 失败于依赖还原；NU1301 将 `https://api.nuget.org/v3/index.json` 解释为工程下的本地路径，未进入完整解决方案编译 |
| `dotnet build src/Game.Content/Game.Content.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | 失败；Domain 的 `World/CampaignCatalog.cs` 找不到 `OrdinaryTrainerCatalog`，CS0246，0 警告、1 错误 |

`dev build` 实际执行的还原命令为：

```powershell
dotnet restore FireRed.Current.slnx --disable-parallel --source C:/home/game_developer/godot/GodotSharp/Tools/nupkgs --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false -m:1 -nr:false
```

本机输出保存在忽略目录 `evidence/doctor.json`、`evidence/restore.log` 和 `evidence/docs-content-build.log`。工具可用不代表游戏已启动；本次没有核验实际游戏宿主 CLR、运行测试、打开游戏窗口、导出游戏或验证音频。

源码检查另发现 `SourceTextPack` 和 `PresentationTextCatalog` 的调用仍存在，但当前受控源码缺少其定义。这个观察与首个编译错误分开记录，尚不能视为完整编译错误清单。相关功能说明见[内容与本地化](current-content-composition.md#文本与本地化)。

## Godot 目录整理（2026-10-21）

在源码基线 `c9e970af` 上整理 `Game.Godot`。441 个 C# 文件按职责移动，文件内容逐一做 SHA-256 比较并保持一致；主场景移至 `App/Bootstrap.tscn`，相关路径和子目录 UID 忽略规则同步更新。目录职责见[Godot 说明](../src/Game.Godot/README.md)。

环境诊断再次确认 SDK 10.0.402 和 Godot 4.7.2 .NET。移动前后分别执行以下 MSBuild 查询，Godot 的 441 项编译输入和表现测试的 502 项编译输入在路径映射后完全一致，包含测试的虚拟链接名称：

```powershell
dotnet msbuild src/Game.Godot/Game.Godot.csproj -getItem:Compile
dotnet msbuild tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj -getItem:Compile
```

表现测试原有两个缺失链接仍存在：`CampaignOptionPresentation.cs` 和 `CampaignFieldPartyActions.cs`，仅更新了对应目录，未删除引用或声称已恢复定义。

| 检查 | 实际结果 |
| --- | --- |
| Godot 编辑器扫描与资源导入 | 退出 0，扫描 441 个脚本，日志无 ERROR；未启动玩家游戏 |
| 交换场景源码路径检查 | `test_no_raw_runtime_or_transaction_side_effect`，1 项通过 |
| 交换场景完整导入检查 | 2 项中 1 项失败；再生成 JSON 使用 CRLF，现有资源使用 LF，字节比较不一致；未修改生成资源或参考期望 |
| Godot 项目构建，移动前后各一次 | 均失败于同一 NU1301 路径问题，各 0 警告、6 错误 |
| 表现测试定向执行 | 被 NU1301 阻塞，未进入测试执行，不计为通过 |

实际命令：

```powershell
& C:/home/game_developer/godot/godot.cmd --headless --editor --path src/Game.Godot --import --quit
python scripts/checks/test-npc-trade-scene-import.py
python scripts/checks/test-npc-trade-scene-import.py TradeImportTests.test_no_raw_runtime_or_transaction_side_effect
dotnet build src/Game.Godot/Game.Godot.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~BattleTextPrinterTests|FullyQualifiedName~SourceTextInputTests|FullyQualifiedName~ItemTransferReceiptPresentationTests'
```

本地日志位于忽略目录 `evidence/godot-layout-*`。编译输入查询和编辑器导入不代表完整构建、真实游戏宿主 CLR、玩家流程或实际音频验收；既有构建问题仍需单独修复。

## Application 目录整理（2026-10-21）

在源码基线 `500620b4` 上整理 Application 的 324 个 C# 文件，逐一比较 SHA-256，内容保持一致。类型、命名空间、项目依赖、运行资源和存档格式均未改动。目录职责见[应用层说明](../src/Game.Application/README.md)。

`python dev doctor` 确认 SDK 10.0.402 和 Godot `4.7.2.stable.mono.official.ed1daf0bf`。这是开发工具可用性检查，未核验实际游戏宿主 CLR。

移动前后分别执行以下查询，对比经路径映射后的完整文件集合和测试 `Link` 元数据；路径派生目录及文件时间不作为等价比较条件：

```powershell
dotnet msbuild src/Game.Application/Game.Application.csproj -getItem:Compile,EmbeddedResource
dotnet msbuild tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj -getItem:Compile,EmbeddedResource
dotnet msbuild tests/Game.Application.Tests/Game.Application.Tests.csproj -getItem:Compile,EmbeddedResource
```

Application 324 项、Presentation 测试 502 项、Application 测试 300 项编译输入在路径映射后一致，测试虚拟链接名保持不变。三个查询项目的嵌入资源输入均为 0 项，移动前后一致。

实际构建及资源检查命令：

```powershell
dotnet build src/Game.Application/Game.Application.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
python scripts/checks/test-teaching-catalog-imports.py
python scripts/imports/story/import-move-tutors.py --check
```

构建在移动前后均失败于 Domain 的 `World/CampaignCatalog.cs(47,20)`：缺少 `OrdinaryTrainerCatalog`，CS0246，各 0 警告、1 错误；未进入 Application 的完整编译。教学资源检查 5 项通过；招式教学导入器只读检查退出 0，33 个输出、1029877 字节一致，绑定代码检查使用新路径 `Services/CampaignMoveTutorBindings.cs`。

MSBuild 查询和构建日志位于本地忽略目录 `evidence/application-content-layout-*`。本次未执行 .NET 测试、玩家流程、导出或音频验收；编译输入等价和 Python 检查不能替代完整构建。Godot 整理记录中的既有缺失测试链接仍未修复。

## Content 目录整理（2026-10-21）

Content 的 374 个 C# 文件按职责归档，逐一比较 SHA-256，内容保持一致；比较基线为 `500620b4`。类型、命名空间、项目依赖和运行资源均未改动。目录职责见[内容层说明](../src/Game.Content/README.md)，工具环境与验证限制同[Application 记录](#application-目录整理2026-10-21)。

移动前后分别执行：

```powershell
dotnet msbuild src/Game.Content/Game.Content.csproj -getItem:Compile,EmbeddedResource
dotnet msbuild tests/Game.Content.Tests/Game.Content.Tests.csproj -getItem:Compile,EmbeddedResource
dotnet build src/Game.Content/Game.Content.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

Content 的 374 项和 Content 测试的 121 项编译输入在路径映射后一致，无遗漏或重复。两个项目的嵌入资源输入均为 0 项，移动前后一致；保留 `Text/en/*.json` 规则，未据此声称文本资源完整。

构建在移动前后均失败于同一 Domain `OrdinaryTrainerCatalog` 缺失错误，CS0246，各 0 警告、1 错误；未进入 Content 的完整编译。日志位于 `evidence/application-content-layout-*`。本次未执行 .NET 测试或游戏验收，未修改生成资源和参考期望。

## 工具与执行

要求官方 Godot 4.7.2 .NET、稳定 .NET 10 SDK（至少 10.0.100）、目标 `net10.0` 和实际 .NET 10 游戏宿主。Python 仅用于开发辅助和离线转换，不决定游戏规则。

标准构建与测试入口在[README](../README.md)和[测试说明](../tests/README.md)。`python dev env --json` 导出辅助工具设置，Bash 可用 `source scripts/env.sh`。环境诊断不会自动安装工具。

历史 Linux 云环境出现过默认并行 MSBuild 无诊断失败，后续在该环境使用：

```sh
source scripts/env.sh
# 首次仍须完成正常还原。
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

它只关闭并行、节点复用和共享编译，不改变 SDK、目标框架或编译检查。音频测试不在默认解决方案测试中，音频变更须显式运行对应项目。

## 受控原生检查

已有记录覆盖开场、菜单、图鉴、训练家卡、道具、部分战斗、地图过渡和 Save/Continue 的代表场景。受控测试存档和临时探针不代表从新游戏自然取得这些状态，也不能替代全部 UI 状态或逐帧对照。

历史记录中的黑屏截图和退出异常仍保留在[Quest Log](features/quest-log.md)、[单机审计](history/nonlocalized-single-player-completion-audit.md)及[跳跃表现](features/shared-field-jump-presentation.md)等专题中；后续局部通过不能自动关闭未解释现象。

具体证据见[内容窗口检查](history/content-native-visual-check.md)、[界面窗口检查](history/ui-native-window-check.md)、[片头](features/intro-movie.md)、[开场](features/opening-presentation.md)、[Summary](features/summary-native-presentation.md)和[地图过渡](features/plain-field-transition-presentation.md)。

## Windows

2026-10-21 已验证开发工具可发现，但上述构建失败，不能宣称当前 Windows 游戏运行或成品通过。历史 Windows 导出配置检查不等于运行可执行文件；真实窗口、独立包依赖、Save/Continue 和声卡输出仍需实际验收。

Linux 独立包的证据属于 2026-10-19 的版本，见[导出记录](history/linux-export-acceptance.md)。该次使用 Dummy 音频，未完成真实扬声器检查，也不代表当前源码已重新导出。

## 未关闭的验收项

- 修复当前构建阻塞，并按改动范围重新运行相应测试。
- P0 的实际音频输出；Dummy、离线 PCM 和 ready 信号不能关闭此项。
- 从新游戏开始的完整自然路线，以及全部关键分支、长期养成和保存恢复。
- 尚未覆盖的原生界面、逐帧动画和不同语言布局。
- 全量转换资源的冷重建；现有资源不能全部视为可删除缓存。

功能范围见[当前功能状态](first-clear-remaining-scope.md)。历史重构数据见[内容重构记录](history/content-and-ownership-refactor.md)和[导入工具记录](history/script-consolidation.md)，不累计其中的通过数作为当前验收结果。
