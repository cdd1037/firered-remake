# 验证记录与已知限制

本页汇总当前检查状态和历史证据入口。代码、资源、实际路线、Godot 窗口、独立包和真实音频分别验证；一个层面的成功不能代替其他层面。

## 最近一次检查

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
