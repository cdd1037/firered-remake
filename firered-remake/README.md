# FireRed 当前工程

请先阅读上一级 README.md。维护入口为 FireRed.Current.slnx 和 src/Game.Godot/project.godot。

导入脚本按职责分组，入口、公共模块、配方和检查见 [scripts 说明](scripts/README.md)。

范围与缺口：docs/scope.md、docs/full-scope-audit.md、docs/first-clear-remaining-scope.md。资源再生成：docs/resource-reconstruction-guide.md。当前运行资源边界：[仅转换数据运行](docs/generated-only-runtime.md)。

## 构建与验证（Windows / Linux）

安装正式版 .NET 10.0 SDK（10.0.100 起，允许后续功能带及补丁）和 Godot 4.7.2 .NET。将 `dotnet`、`godot` 加入 PATH 后，在本目录执行：

```powershell
dotnet build
dotnet test
```

完整测试中的资源导入检查使用 Python 3.12 和 requirements-dev.txt 中的 Pillow；普通 C# 测试没有 Python 依赖。定向运行与各项目职责见 [测试说明](tests/README.md)。直接构建和测试使用标准 NuGet 还原，不需要先执行 `dev env`。

需要本机工具诊断或使用 Godot 安装目录内的包时，可选用 `python dev doctor`、`python dev build`。Windows 也可执行 `.\dev.cmd build`；Linux 可执行 `./dev build`。这些开发辅助入口需要 Python，不自动安装工具。目标框架仍为 net10.0，Godot 仍为 4.7.2 .NET。

工具按显式环境变量、`FIRERED_TOOLCHAIN_ROOT` 工具链目录、PATH 的顺序查找。可用 `FIRERED_DOTNET` 指定 dotnet 可执行文件、`GODOT` 指定 Godot 可执行文件；若 Godot 的 PATH 包装器不在安装目录，另设 `FIRERED_GODOT_NUGET_SOURCE` 指向安装目录下 `GodotSharp/Tools/nupkgs`。本地 `.tools/toolchain-root` 配置也继续支持。

`python dev build` 自动设置本机 NuGet 路径，从 Godot 安装目录及 nuget.org 还原依赖，不再使用 `--locked-mode`。直接运行 `dotnet test` 则从 nuget.org 还原官方包，首次运行需要网络；后续可用 `--no-restore`。Godot SDK 版本由项目声明，随安装提供的 Godot 包不使用跨平台共享的内容哈希锁。下载工具链的 `setup` 仍验证下载哈希，且只支持原有 Linux 下载清单；Windows 使用已有安装。`toolchain.lock.json` 中 SDK 10.0.401 / runtime 10.0.12 是原 Linux 下载和验收记录，不是开发机补丁版本门槛。

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

`dotnet test` 直接还原、构建并执行全部维护测试，不依赖 Python 调度。测试按依赖边界拆成八个 MSTest 项目：领域规则、真实内容战斗、应用、内容契约、音频、表现逻辑、资源导入与跨进程检查。只有资源导入检查需要 Python；只有跨进程哈希检查需要专用 .NET 子进程。

各项目内部按功能组织源文件和夹具，不再按旧验收套件拆项目。单项筛选先指定项目，例如 `dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --filter FullyQualifiedName~RandomnessTests`。可用 `--no-build` 复用构建，`--logger "trx;LogFilePrefix=tests"` 输出各程序集独立报告。默认存档可用 `FIRERED_TEST_CHECKPOINT` 环境变量覆盖。缺失输入、空项目、空筛选与发现警告均报错。

72 个依赖已删除 API 的历史测试已退休，原因见 docs/test-retirement-20261015.json。普通测试不生成或改写 golden，不再要求两份缺失的早期历史存档。

运行所需资源、默认存档、每项范围和显式参考向量生成方法见 [tests/README.md](tests/README.md)。该维护清单不等同于全游戏路线、原生画面或真实音频输出验收。
