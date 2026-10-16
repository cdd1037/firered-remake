# FireRed 普通工程

这是普通的 Godot/.NET 工程，仓库根目录就是开发入口。维护解决方案为 `FireRed.Current.slnx`，Godot 工程为 `src/Game.Godot/project.godot`。

- `src/`：游戏源码，包括 `Game.Audio` 自定义合成器；主游戏继续使用合成音频。
- `tools/`、`scripts/`：开发工具与资源导入入口；音频命令行工具位于 `tools/AudioTool/`，见 [音频说明](docs/audio.md)。
- `tests/`、`docs/`：测试与维护说明。
- `content/raw/`、`content/generated/`：本地导入输入与转换资源，不纳入源码 Git。
- `checkpoints/`：代表存档与说明。默认 `current-ownership-v3-20261015/continued-checkpoint.json` 为 generation 57604，不会自动覆盖用户存档槽。

当前 Sticker Man/孵蛋计数切片要求明确的终身孵蛋次数。上述旧代表档缺少此字段，原文件保持不变，暂不能作为当前可继续基线；不会按剩余蛋记录猜测历史次数。已知次数才可显式转换到新文件，详见 [Sticker Man 与存档边界](docs/sticker-man.md)。新游戏从零正确计数。

源码、转换资源和代表存档各有明确边界。提交版本以 Git 为准，不在 README 重复记录历史分支节点。随工程包不含 SDK、Godot、NuGet/编译缓存、运行日志或测试探针。

导入脚本按职责分组，入口、公共模块、配方和检查见 [scripts 说明](scripts/README.md)。

范围与缺口：docs/scope.md、docs/full-scope-audit.md、docs/first-clear-remaining-scope.md。资源再生成：docs/resource-reconstruction-guide.md。当前运行资源边界：[仅转换数据运行](docs/generated-only-runtime.md)。

## 构建与验证（Windows / Linux）

安装正式版 .NET 10.0 SDK（10.0.100 起，允许后续功能带及补丁）和 Godot 4.7.2 .NET。将 `dotnet`、`godot` 加入 PATH 后，在本目录执行：

```powershell
dotnet build
dotnet test
```

默认测试中的资源导入检查使用 Python 3.12 和 requirements-dev.txt 中的 Pillow；普通 C# 测试没有 Python 依赖。定向运行与各项目职责见 [测试说明](tests/README.md)。直接构建和测试使用标准 NuGet 还原，不需要先执行 `dev env`。

需要本机工具诊断或使用 Godot 安装目录内的包时，可选用 `python dev doctor`、`python dev build`。Windows 也可执行 `.\dev.cmd build`；Linux 可执行 `./dev build`。这些开发辅助入口需要 Python，不自动安装工具。目标框架仍为 net10.0，Godot 仍为 4.7.2 .NET。

工具按显式环境变量、`FIRERED_TOOLCHAIN_ROOT` 工具链目录、PATH 的顺序查找。可用 `FIRERED_DOTNET` 指定 dotnet 可执行文件、`GODOT` 指定 Godot 可执行文件；若 Godot 的 PATH 包装器不在安装目录，另设 `FIRERED_GODOT_NUGET_SOURCE` 指向安装目录下 `GodotSharp/Tools/nupkgs`。本地 `.tools/toolchain-root` 配置也继续支持。

`python dev build` 自动设置本机 NuGet 路径，从 Godot 安装目录及 nuget.org 还原依赖，不再使用 `--locked-mode`。直接运行 `dotnet test` 则从 nuget.org 还原官方包，首次运行需要网络；后续可用 `--no-restore`。Godot SDK 版本由项目声明，随安装提供的 Godot 包不使用跨平台共享的内容哈希锁。下载工具链的 `setup` 仍验证下载哈希，且只支持原有 Linux 下载清单；Windows 使用已有安装。`toolchain.lock.json` 中 SDK 10.0.401 / runtime 10.0.12 是原 Linux 下载和验收记录，不是开发机补丁版本门槛。

当前 Linux 云环境的默认并行 MSBuild 已多次出现无诊断的 `Build FAILED`（0 warnings／0 errors）；该环境后续直接使用下面的串行命令，不重复尝试默认并行，也不修改机器配置：

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

此命令保留原 SDK、目标框架与编译检查，只关闭并行节点复用和编译服务共享；首次构建仍须先完成正常还原。

需要直接运行 dotnet 或其他 Bash 工具时，可先 `source scripts/env.sh`；PowerShell 可先导入相同环境：

```powershell
$toolEnv = python dev env --json | ConvertFrom-Json
$toolEnv.psobject.Properties | ForEach-Object { Set-Item -LiteralPath "Env:$($_.Name)" -Value $_.Value }
dotnet build FireRed.Current.slnx --no-restore
```

## 开发启动

先执行 `python dev build`，再执行 `python dev run [current-checkpoint.json]`。也可直接在 Godot 打开并运行工程；正常入口不再强制要求 FIRERED 环境变量，默认启用玩家入口和音频。Linux 已做无 FIRERED 变量、隔离用户目录的启动检查；Windows 尚未实测。Bash 旧入口 `scripts/dev/launch-current.sh` 仍可用于显式开发参数。

Linux 私有成品包入口为 `python dev export linux`，需要在开发工具的 XDG 模板目录安装匹配的官方 Godot 4.7.2 .NET Linux 导出模板。模板缺失时明确失败，不产生冒充完成的包。成品包含可执行文件、PCK、Godot 导出的 .NET 数据目录和旁置 `content/generated`，应整体分发。当前已完成真实 Linux 导出及清空开发环境、隔离 HOME/XDG 的启动检查；实测 .NET 10.0.12，libcoreclr 来自成品目录。窗口完整操作、新游戏/存档/继续及真实音频仍需单独验收。用户存档继续位于原有 user:// 应用目录。

参数与存档边界见 [当前组合](docs/current-game-composition.md)。

## 当前测试入口

`dotnet test` 直接还原、构建并执行默认解决方案中的七个 MSTest 项目，不依赖 Python 调度：领域规则、真实内容战斗、应用、内容契约、表现逻辑、资源导入与跨进程检查。只有资源导入检查需要 Python；只有跨进程哈希检查需要专用 .NET 子进程。

音频测试按需显式运行，不加入默认全测：`dotnet test tests/Game.Audio.Tests/Game.Audio.Tests.csproj`。它覆盖离线合成与播放控制，不代表真实声卡输出已验收。

各项目内部按功能组织源文件和夹具，不再按旧验收套件拆项目。单项筛选先指定项目，例如 `dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --filter FullyQualifiedName~RandomnessTests`。可用 `--no-build` 复用构建，`--logger "trx;LogFilePrefix=tests"` 输出各程序集独立报告。默认存档可用 `FIRERED_TEST_CHECKPOINT` 环境变量覆盖。缺失输入、空项目、空筛选与发现警告均报错。

72 个依赖已删除 API 的历史测试已退休，原因见 docs/test-retirement-20261015.json。普通测试不生成或改写 golden，不再要求两份缺失的早期历史存档。

运行所需资源、默认存档、每项范围和显式参考向量生成方法见 [tests/README.md](tests/README.md)。该维护清单不等同于全游戏路线、原生画面或真实音频输出验收。

## 当前资源边界

运行时只读 `content/generated` 中的转换数据/图像/音频，以及 Godot 项目自身的字体与效果素材，不再读取 pret 原作 C/ASM、`content/raw`、导入配方或 source-lock。

`content/raw/pokefirered/037335f4` 保留固定版本原始导入输入；`content/raw/local` 的三个调色板是实际变体。地图图层、NPC、部分道具/UI 已使用共享图集；原始 RGBA 和玩法字段经过对应检查。地图进入时仍按现有有界缓存还原整层纹理，尚未宣称 GPU 内存或帧率改善。

导入工具保留必要来源版本与配方。工具侧只应保留实际输入选择和转换配方；未闭合的再生边界必须明确，不能把所有generated视为可删缓存。运行边界和再生成说明见 `docs/generated-only-runtime.md`、`docs/resource-reconstruction-guide.md`。

## 验证和已知限制

- 当前游戏可从仅含 generated 的独立资源根加载，新游戏、存档、Continue、移动及 generation 57604 存档验证通过；v4 显式离线转换仅删除六处旧 NPC 交换来源指纹，玩法值、RNG、generation 保持，原 v3 存档保留。
- 原生 Godot 窗口检查覆盖代表存档所在地图、NPC、菜单、背包、TM Case 和选项框图集，并检查正常窗口关闭。音频使用 Dummy，未进行真实扬声器听感验收。
- 未重放整条历史路线。早期自然路线并未据此补写为通关；当前代表档延续此前已披露的受控联盟通关分支，详情保留在 checkpoints 说明中。
- 物种、物品、机器、训练家等共享定义已收敛；当前结构性问题与治理顺序见 `docs/project-quality-review.md`。普通单/双打共享四位置内核；当前实现范围以维护文档为准。
- 各原作导入入口的成熟度不一；本轮完成当前维护的导入检查和指定再生成检查，不等于每个旧开发入口都完成全量冷重建。

仓库根目录即工程根目录。`.gitignore` 将素材、存档与缓存排除，只纳入作者源码、配置和工具。资源由普通工程 ZIP 携带，不应使用 `git add -f` 强制加入 Git。
