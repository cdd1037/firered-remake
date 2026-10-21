# FireRed Remake

使用 Godot 4.7.2 .NET 和 C# 开发的 FireRed 单机重制项目。目标包括关都主线、正常单机可达的七岛、通关后玩法与支线。仓库根目录是开发入口，解决方案为 `FireRed.Current.slnx`，Godot 工程为 `src/Game.Godot/project.godot`。

当前源码存在构建阻塞，最近一次检查见[验证记录](docs/validation.md#最近一次检查)。下列命令是维护入口，不表示当前版本已通过构建或全游戏验收。

- [文档导航](docs/README.md)：按开发、功能、资源和验证查找说明。
- [功能状态](docs/first-clear-remaining-scope.md)：区分已接入、未验证与明确后置的功能。
- [当前架构](docs/architecture.md) / [质量与维护](docs/project-quality-review.md) / [实施顺序](docs/implementation-roadmap.md)。

## 环境与构建

需要正式版 .NET 10.0 SDK（10.0.100 起，允许后续功能带及补丁）与官方 Godot 4.7.2 .NET。目标框架固定为 `net10.0`，实际宿主运行时须为 .NET 10；不能以其他引擎或运行时替代。

安装工具并将 `dotnet`、`godot` 加入 PATH，在仓库根目录执行：

```sh
dotnet build FireRed.Current.slnx
dotnet test FireRed.Current.slnx
```

直接命令使用标准 NuGet 还原，无需先运行 `dev env`；首次还原需要网络。源码仓库不包含构建和运行所需的全部转换资源，须先准备本地输入，见[资源再生成](docs/resources/resource-reconstruction-guide.md)。默认测试中的资源导入检查使用 Python 3.12 和 `requirements-dev.txt` 的依赖，普通 C# 测试不依赖 Python。测试使用的资源和受控测试存档缺失时明确失败，详见[测试说明](tests/README.md)。

可选开发工具：`python dev doctor` 诊断环境，`python dev build` 配置本机 Godot NuGet 来源并构建。Windows 可用 `.\dev.cmd build`，Linux 可用 `./dev build`。这些辅助入口需要 Python，不自动安装工具。

工具按显式环境变量、`FIRERED_TOOLCHAIN_ROOT`、PATH 查找，也支持本地 `.tools/toolchain-root`：

- `FIRERED_DOTNET`：dotnet 可执行文件；`GODOT`：Godot 可执行文件。
- `FIRERED_GODOT_NUGET_SOURCE`：当 PATH 中只是 Godot 包装器时，指定安装目录下的 `GodotSharp/Tools/nupkgs`。
- `python dev env --json` 或 Bash 的 `source scripts/env.sh`：导出相同开发环境。

`python dev build` 从本机 Godot 包目录及 nuget.org 还原，不使用跨平台内容哈希锁或 `--locked-mode`。`toolchain.lock.json` 是 Linux 下载清单，仍验证下载哈希；其中具体 SDK/runtime 补丁不是开发机门槛。`setup` 只支持该 Linux 清单，Windows 使用已有安装。

当前 Linux 云环境的默认并行 MSBuild 出现过无诊断失败，后续使用已验证的串行命令，不修改机器配置或反复尝试默认并行：

```sh
source scripts/env.sh
# 首次先完成正常还原；该命令只关闭并行、节点复用和编译服务共享。
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

## 启动与测试

```sh
python dev build
python dev run
# 可选：显式加载当前格式的受控测试存档
python dev run /path/to/current-checkpoint.json
# 音频测试不属于默认七项目测试，按需单独执行
dotnet test tests/Game.Audio.Tests/Game.Audio.Tests.csproj
```

也可在 Godot 打开并运行工程；正常入口不强制要求 FIRERED 环境变量，默认启用玩家入口和合成音频。参数、正常用户存档目录、诊断入口及严格存档边界见[当前组合](docs/current-game-composition.md)。

默认 `dotnet test` 执行领域、真实内容战斗、应用、内容、表现、资源导入和跨进程七个 MSTest 项目。定向筛选须先指定项目；空套件、空筛选、发现警告与缺失输入均报错。测试项目职责、参考向量和报告命令只维护在[测试说明](tests/README.md)。离线音频测试不证明真实声卡输出。

## 文件与资源边界

- `src/`：游戏模块，包含游戏与工具共用的 `Game.Audio` 合成器。
- `tools/`、`scripts/`：开发与离线导入工具，见 [scripts 说明](scripts/README.md)及[音频说明](docs/resources/audio.md)。
- `tests/`、`docs/`：测试和维护文档。
- `content/raw/`、`content/generated/`：本地原始输入与转换资源，不纳入 Git。
- `checkpoints/`：本地受控测试存档和历史样本，不纳入 Git；用 `FIRERED_TEST_CHECKPOINT` 指定当前 schema 3 测试输入。

运行只读取 `content/generated` 及 Godot 自身字体/效果素材，不解析原作 C/ASM、raw、配方或 source-lock。generated 的完整冷重建尚不完整，不能当作可随时删除的缓存。保留必要私有资源，参见[运行资源边界](docs/resources/resource-layout.md)与[再生成说明](docs/resources/resource-reconstruction-guide.md)。不得用 `git add -f` 将素材、存档或生成证据塞进源码仓库，也不公开上传或分发原作素材。

简体中文是目标语言，文本来源、现有字体布局和待恢复的语言包实现见[本地化状态](docs/current-content-composition.md#文本与本地化)。现存 `--language` 参数不能单独证明语言包可用。

## 当前验证边界

- P0 的真实音频输出尚未验收。Dummy/headless、离线 PCM 或测试通过不能关闭此缺口。
- Linux 正常启动与独立包有历史 Save/Continue 记录；Windows 游戏启动与独立包尚未验收。历史导出不覆盖后续源码。导出需要匹配的官方模板，见[Linux 导出记录](docs/history/linux-export-acceptance.md)和[平台状态](docs/validation.md#windows)。
- 原生窗口仅验证了所记录的代表场景，不代表完整自然通关、所有 UI 状态、逐帧一致或实际帧率。当前回归与原生覆盖统一见[验证与限制](docs/validation.md)。
- 存档保持可读 schema 3 JSON，支持 Unicode 身份与昵称；正常 Continue 拒绝 schema 1/2 和不匹配版本，不自动迁移、不猜测缺失的终身孵蛋数，也不覆盖旧档。generation-57604 历史样本不是当前 Continue 或自然后期验收基线。格式与备份契约见[存档说明](docs/save-storage.md)。

源码版本以 Git 为准。开发工具链、NuGet/编译缓存、日志和测试探针不属于源码交付内容；本地素材与存档单独管理。
