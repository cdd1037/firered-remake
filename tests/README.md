# 测试组织与运行

保留 MSTest，按产品依赖边界划分项目；项目内按功能拆文件，测试方法和数据行可以独立发现、筛选。公共版本、测试属性和路径辅助源文件由 `Directory.Build.props` 管理，各项目声明自身依赖。普通用例直接调用产品代码。

| 项目 | 产品依赖与职责 |
| --- | --- |
| Game.Domain.Tests | 只引用 Domain：标识、随机数、属性、伤害、基础战斗命令、地图移动、状态校验、成长和捕获 |
| Game.Battle.Tests | 引用 Content（传递依赖 Domain）：使用真实内容的回合、招式、树果、换人、天气／屏障、残留、场地陷阱、生命周期、训练师策略和蛋工厂 |
| Game.Application.Tests | 引用 Application、Content：命令与存档协议、工程会话、新游戏／Continue、野战、训练师战、场景脚本和背包接线 |
| Game.Content.Tests | 只引用 Content：内容契约、序列化、引用校验和原版地图形状转换 |
| Game.Audio.Tests | 只引用 Game.Audio（按需运行）：合成、PSG、声像、播放控制、回声、音序、叫声和目录 |
| Game.Presentation.Tests | 引用 Content，并链接纯 C# 表现组件：资源包、图鉴索引、菜单、文本分页、保存反馈与诊断；不加载 Godot 引擎 |
| Game.ResourceImport.Tests | 无产品程序集依赖：维护的 Python 资源导入检查 |
| Game.Process.Tests | 引用 Application：新进程之间的状态哈希一致性、存档往返；辅助程序在 Helpers/HashProbe |

Domain 的输入矩阵不依赖内容加载。Battle 使用经过认证的内容验证领域行为，Application 验证会话接线；同一功能出现在不同层时，各自承担不同职责。不会为了每个功能建立一个项目。

`Shared/TestAssets.cs` 只提供仓库／输入路径、缺失生成数据的明确报错、唯一临时目录和内容缓存，以源码链接方式共享。场景夹具留在各功能目录内；没有额外公共测试框架或套件注册表。`Reference/reference_c.py` 供显式参考向量生成器使用，不参与普通测试执行。

## 运行入口

在仓库根目录执行：

```powershell
# 默认七个项目；资源导入检查需要 Python 3。
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

# 音频不在默认全测中，需要显式指定项目；其余检查也可独立执行。
dotnet test tests/Game.Audio.Tests/Game.Audio.Tests.csproj
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj
dotnet test tests/Game.Process.Tests/Game.Process.Tests.csproj

# 各程序集使用独立报告名，避免相互覆盖。
dotnet test --logger 'trx;LogFilePrefix=tests' --results-directory .toolhome/test-results
```

`dotnet test` 自带构建，无需先重复 build；`--no-build` 可复用已有产物。共享测试配置或跨边界行为变更运行默认解决方案中的七个项目，涉及音频时再显式运行 `Game.Audio.Tests`；局部改动选择相关项目即可。框架和适配器均使用公共配置中的固定版本，不限制开发 SDK 的功能带或补丁。

筛选时先指定项目：`test.runsettings` 会让空测试项目、空筛选和发现警告报错，因此对整个解决方案使用只命中某一项目的过滤条件，其余项目会报空筛选。`Game`、`Audio`、`ResourceImport` 分类仍可在选定项目内使用。

全部 354 招式的四个位置共 1,416 行，按需运行音频测试时，386 种普通叫声也分别发现；Repel、进化石、Everstone 和生命周期变体保留参数化数据行。MSTest 负责结果与 TRX，不再维护手工成功计数或套件级 JSON/XML。普通游戏／音频测试不启动其他测试程序集或音频 CLI。

仅 `Game.Process.Tests` 启动专用 .NET 辅助进程验证跨进程确定性；仅 `Game.ResourceImport.Tests` 直接调用 `scripts/checks/` 中的 Python 检查。脚本结构与离线物化回归可独立执行 `python scripts/checks/test-script-layout.py`。可用 `FIRERED_PYTHON` 指定 Python。当前测试按原有隔离要求串行运行，各用例独立准备状态，不依赖执行顺序。

## 输入和覆盖边界

- 游戏资源需已在 `content/raw` 和 `content/generated`；导入器读取 `content/raw/pokefirered/037335f4`，测试不下载素材或安装编译器。
- 音频需要 `content/generated/audio/synth/firered.pack` 和同目录的 `normal-cries.bin`（相对主游戏根目录）。
- 应用与 Continue 默认使用 `checkpoints/current-ownership-v3-20261015/continued-checkpoint.json`，可用 `FIRERED_TEST_CHECKPOINT` 指定有效的当前存档。部分用例在内存中准备位置、HP、PP、EXP 等受控状态，不代表自然路线重放或历史缺失证明已恢复。
- 生成的参考数值、逐帧轨迹和验证报告仅存放在本地忽略目录，不纳入 Git；源码、明确语义用例、独立生成器仍受版本控制。手写的 schema 输入和固定源版本清单是测试定义，不是生成结果。
- 普通测试读取已准备的本地数据，不启动 C 编译器，不自动下载，也不会因缺失数据跳过。缺失时明确失败并给出对应生成命令；先按提示显式准备，再重跑所选测试。不要以当前产品输出重新生成参考期望。
- 大型动画族的生成器默认选择有明确分支/边界意义的代表案例，保留所选案例初始化到销毁的每一帧。`--full` 在外部输出目录生成完整组合，`FIRERED_SOURCE_VECTOR_DIR` 指向该目录后，同一托管比较器逐值检查全量数据。指定目录缺文件时不得回退到较小本地数据。具体命令与覆盖身份写在对应族的 `tests/Reference` 或 `Game.Presentation.Tests/Reference` 文档中。
- 全量迁移对照是显式工程检查，不属于每次普通测试；保留的局部规则断言、完整根播放、取消和资源所有权测试照常运行。历史完整证据保留在外部迁移备份，移出索引不重写 Git 历史。
- 文件系统用例使用唯一的 `.toolhome/mstest` 临时目录；TRX 使用标准结果目录或显式 `--results-directory`。

72 个依赖已删除 API 的历史项目退休清单见 `docs/test-retirement-20261015.json`，不代表所有历史对照都有一对一替代。功能目录里的旧验收说明仅供历史证据参考，旧 shell／console 命令不再是运行入口。

音频离线测试不证明真实声卡输出。历史 `results/phase4/render-coverage.json` 缺失，PCM 历史哈希等价性不在已验证范围；可选 PAN_ORACLE 研究比较也未计入。自然完整剧情、画面对照和实际扬声器输出仍需单独验收。

本次重组与实际验证见 [测试项目整理记录](../docs/test-layout-20261015.md)；之前原生 MSTest 迁移的验证保留在 [迁移记录](../docs/mstest-migration-20261015.md)。

## 生成证据治理（2026-10-18）

111份生成对照数据与报告（158,372,572字节）退出版本控制，本地文件和外部迁移原件保留。Git中的测试数据仅保留手写schema输入、固定源输入定义和获取来源说明；不以压缩或换扩展名继续跟踪轨迹。

20个大型族改为显式代表／边界输入，常规本地结果合计16,419,741字节；每个选中案例保留连续逐帧断言。其余生成结果同样忽略，尚未全部缩减组合。完整原始组合由独立生成器显式生成，复用同一托管比较器，迁移全量72/72通过；完整数值与外部原件逐值核对。两个历史单回合snapshot改为明确HP、PP、身份字段和随机调用规则用例，原hash仅留外部迁移对照。

同一25类常规筛选：原196例墙钟233.868秒，治理后203例墙钟193.502秒（新增7项用例选择检查，约减少17.25%）。额外Presentation40、Battle83、Domain22、Application154和按需Audio1例通过。解决方案构建0警告0错误。此计时不代表所有测试或真实游戏性能；普通测试不隐式编译C，缺数据的命令提示和指定全量目录禁止回退均有测试。
