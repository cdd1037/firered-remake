# 测试组织与运行

保留 MSTest，按产品依赖边界划分项目；项目内按功能拆文件，测试方法和数据行可以独立发现、筛选。公共版本、测试属性和路径辅助源文件由 `Directory.Build.props` 管理，各项目声明自身依赖。普通用例直接调用产品代码。

| 项目 | 产品依赖与职责 |
| --- | --- |
| Game.Domain.Tests | 只引用 Domain：标识、随机数、属性、伤害、基础战斗命令、地图移动、状态校验、成长和捕获 |
| Game.Battle.Tests | 引用 Content（传递依赖 Domain）：使用真实内容的回合、招式、树果、换人、天气／屏障、残留、场地陷阱、生命周期、训练师策略和蛋工厂 |
| Game.Application.Tests | 引用 Application、Content：命令与存档协议、工程会话、新游戏／Continue、野战、训练师战、场景脚本和背包接线 |
| Game.Content.Tests | 只引用 Content：内容契约、序列化、引用校验和原版地图形状转换 |
| Game.Audio.Tests | 只引用 Audio.Core：合成、PSG、声像、播放控制、回声、音序、叫声和目录 |
| Game.Presentation.Tests | 源码链接纯 C# 的 SourceTextProgress：文本分页、帧推进和输入状态，不加载 Godot 引擎 |
| Game.ResourceImport.Tests | 无产品程序集依赖：维护的 Python 资源导入检查 |
| Game.Process.Tests | 引用 Application：新进程之间的状态哈希一致性、存档往返；辅助程序在 Helpers/HashProbe |

Domain 的输入矩阵不依赖内容加载。Battle 使用经过认证的内容验证领域行为，Application 验证会话接线；同一功能出现在不同层时，各自承担不同职责。不会为了每个功能建立一个项目。

`Shared/TestAssets.cs` 只提供仓库／输入路径、唯一临时目录和内容缓存，以源码链接方式共享。场景夹具留在各功能目录内；没有额外公共测试框架或套件注册表。`Reference/reference_c.py` 供显式参考向量生成器使用，不参与普通测试执行。

## 运行入口

在 `firered-remake` 执行：

```powershell
# 全部八个项目；资源导入检查需要 Python 3。
dotnet test

# 局部修改只运行对应依赖边界。
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj
dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj

# 指定项目后发现、筛选具体功能或数据行。
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --list-tests
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --filter FullyQualifiedName~RandomnessTests
dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter 'Name~SelectedMoveCompletesPublicTurn&Name~MOVE_TACKLE'
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --filter FullyQualifiedName~StoneEvolutionTests

# 音频、资源导入和跨进程检查可以各自独立执行。
dotnet test tests/Game.Audio.Tests/Game.Audio.Tests.csproj
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj
dotnet test tests/Game.Process.Tests/Game.Process.Tests.csproj

# 各程序集使用独立报告名，避免相互覆盖。
dotnet test --logger 'trx;LogFilePrefix=tests' --results-directory .toolhome/test-results
```

`dotnet test` 自带构建，无需先重复 build；`--no-build` 可复用已有产物。共享测试配置或跨边界行为变更运行完整解决方案，局部改动选择相关项目即可。框架和适配器均使用公共配置中的固定版本，不限制开发 SDK 的功能带或补丁。

筛选时先指定项目：`test.runsettings` 会让空测试项目、空筛选和发现警告报错，因此对整个解决方案使用只命中某一项目的过滤条件，其余项目会报空筛选。`Game`、`Audio`、`ResourceImport` 分类仍可在选定项目内使用。

全部 354 招式的四个位置共 1,416 行，386 种普通叫声也分别发现；Repel、进化石、Everstone 和生命周期变体保留参数化数据行。MSTest 负责结果与 TRX，不再维护手工成功计数或套件级 JSON/XML。普通游戏／音频测试不启动其他测试程序集或音频 CLI。

仅 `Game.Process.Tests` 启动专用 .NET 辅助进程验证跨进程确定性；仅 `Game.ResourceImport.Tests` 调用 Python。可用 `FIRERED_PYTHON` 指定 Python。当前测试按原有隔离要求串行运行，各用例独立准备状态，不依赖执行顺序。

## 输入和覆盖边界

- 游戏资源需已在 `content/raw` 和 `content/generated`；导入器读取 `content/raw/pokefirered/037335f4`，测试不下载素材或安装编译器。
- 音频需要 `../firered-audio-player/resources/firered.pack` 和普通叫声目录。
- 应用与 Continue 默认使用 `../checkpoints/current-composition-20261015/continued-checkpoint.json`，可用 `FIRERED_TEST_CHECKPOINT` 指定有效的当前存档。部分用例在内存中准备位置、HP、PP、EXP 等受控状态，不代表自然路线重放或历史缺失证明已恢复。
- 数值与冻结夹具放在对应功能的 `Fixtures` 或 `fixtures` 目录，普通测试只读取。Python 独立计算参考值，以及从固定原版 C 函数提取的参考向量，保留显式生成器；生成候选需人工审阅，不允许按产品输出重写预期。
- 文件系统用例使用唯一的 `.toolhome/mstest` 临时目录；TRX 使用标准结果目录或显式 `--results-directory`。

72 个依赖已删除 API 的历史项目退休清单见 `docs/test-retirement-20261015.json`，不代表所有历史对照都有一对一替代。功能目录里的旧验收说明仅供历史证据参考，旧 shell／console 命令不再是运行入口。

音频离线测试不证明真实声卡输出。历史 `results/phase4/render-coverage.json` 缺失，PCM 历史哈希等价性不在已验证范围；可选 PAN_ORACLE 研究比较也未计入。自然完整剧情、画面对照和实际扬声器输出仍需单独验收。

本次重组与实际验证见 [测试项目整理记录](../docs/test-layout-20261015.md)；之前原生 MSTest 迁移的验证保留在 [迁移记录](../docs/mstest-migration-20261015.md)。
