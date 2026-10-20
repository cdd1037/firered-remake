# FireRed 普通工程

以 Godot/.NET 实现的单机 FireRed 研究工程。仓库根目录是开发入口，解决方案为 `FireRed.Current.slnx`，Godot 工程为 `src/Game.Godot/project.godot`。完整目标包含关都主线、可达后期与单机支线，不以首枚徽章或测试数量代表完成。

- [文档导航](docs/README.md)：按开发、功能、资源和验证查找说明。
- [当前实现与剩余范围](docs/first-clear-remaining-scope.md)：区分已接通、未验证与明确后置的功能。
- [当前架构](docs/architecture.md) / [质量与维护](docs/project-quality-review.md) / [实施顺序](docs/implementation-roadmap.md)。

## 环境与构建

需要正式版 .NET 10.0 SDK（10.0.100 起，允许后续功能带及补丁）与官方 Godot 4.7.2 .NET。目标框架固定为 `net10.0`，实际宿主运行时须为 .NET 10；不能以其他引擎或运行时替代。

安装工具并将 `dotnet`、`godot` 加入 PATH，在仓库根目录执行：

```sh
dotnet build FireRed.Current.slnx
dotnet test FireRed.Current.slnx
```

直接命令使用标准 NuGet 还原，无需先运行 `dev env`；首次还原需要网络。默认测试中的资源导入检查使用 Python 3.12 和 `requirements-dev.txt` 的依赖，普通 C# 测试不依赖 Python。测试所需的本地资源及检查点须事先准备，缺失时明确失败，详见[测试说明](tests/README.md)。

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
# 可选：显式加载经验证的当前检查点
python dev run /path/to/current-checkpoint.json
# 音频测试不属于默认七项目测试，按需单独执行
dotnet test tests/Game.Audio.Tests/Game.Audio.Tests.csproj
```

也可在 Godot 打开并运行工程；正常入口不强制要求 FIRERED 环境变量，默认启用玩家入口和合成音频。参数、正常用户存档目录、诊断入口及显式转换见[当前组合](docs/current-game-composition.md)。

默认 `dotnet test` 执行领域、真实内容战斗、应用、内容、表现、资源导入和跨进程七个 MSTest 项目。定向筛选须先指定项目；空套件、空筛选、发现警告与缺失输入均报错。测试项目职责、参考向量和报告命令只维护在[测试说明](tests/README.md)。离线音频测试不证明真实声卡输出。

## 文件与资源边界

- `src/`：游戏模块，包含游戏与工具共用的 `Game.Audio` 合成器。
- `tools/`、`scripts/`：开发与离线导入工具，见 [scripts 说明](scripts/README.md)及[音频说明](docs/audio.md)。
- `tests/`、`docs/`：测试和维护文档。
- `content/raw/`、`content/generated/`：本地原始输入与转换资源，不纳入源码 Git。
- `checkpoints/`：本地代表档和受控测试档，不纳入 Git；用 `FIRERED_TEST_CHECKPOINT` 指定已验证的当前 schema 3 输入。

运行只读取 `content/generated` 及 Godot 自身字体/效果素材，不解析原作 C/ASM、raw、配方或 source-lock。generated 的完整冷重建尚未闭合，不能当作可随时删除的缓存。保留必要私有资源，参见[运行资源边界](docs/resource-layout.md)与[再生成说明](docs/resource-reconstruction-guide.md)。不得用 `git add -f` 将素材、存档或生成证据塞进源码仓库，也不公开上传或分发原作素材。

中文使用固定 Xzonn 文本来源与稀疏覆盖，标题及替换图片保留原版。准备本地 pack 后可运行 `python dev run --language zh-Hans`，详见[中文版](docs/chinese-edition.md)。

## 当前验收与交付限制

- P0 的真实音频输出尚未验收。Dummy/headless、离线 PCM 或测试通过不能关闭此缺口。
- Linux 正常启动与独立包的有限 Save/Continue 流程有既有证据；Windows 尚未实测。已有导出证据不覆盖随后源码变更，本轮不导出新包，Drive 完整工程包也继续暂停。将来导出需匹配的官方模板，详见[Linux 导出验收](docs/linux-export-acceptance.md)。
- 原生窗口仅验证了所记录的代表场景，不代表完整自然通关、所有 UI 状态、逐帧一致或实际帧率。当前回归与原生覆盖统一见[验证与限制](docs/validation.md)。
- 存档保持可读 schema 3 JSON，支持 Unicode 身份与昵称；正常 Continue 拒绝 schema 1/2 和不匹配版本，不自动迁移、不猜测缺失的终身孵蛋数，也不覆盖旧档。generation-57604 历史样本不是当前 Continue 或自然后期验收基线。格式与备份契约见[存档说明](docs/save-storage.md)。

源码版本以 Git 为准。开发工具链、NuGet/编译缓存、日志和测试探针不随普通工程包分发；私有资源与存档另有明确边界。
