# 测试组织与运行

MSTest 项目按产品依赖边界划分，项目内按功能组织独立测试方法和数据行。
公共测试版本、属性与路径辅助源码由 `tests/Directory.Build.props` 管理。
构建环境见 [README](../README.md)，原生／路线／音频证据口径及最近的有界结果见
[验证说明](../docs/validation.md)。

| 项目 | 产品依赖与职责 |
| --- | --- |
| Game.Domain.Tests | 只引用 Domain：随机数、属性、伤害、基础战斗、地图移动、状态校验、成长与捕获 |
| Game.Battle.Tests | 引用 Content（传递依赖 Domain）：真实内容回合、招式、换人、天气／屏障、残留、场地、生命周期、AI 与蛋工厂 |
| Game.Application.Tests | 引用 Application、Content：命令、存档、新游戏／Continue、场景脚本、野战／训练师战及界面事务接线 |
| Game.Content.Tests | 只引用 Content：内容契约、序列化、引用校验与地图格式转换 |
| Game.Audio.Tests | 只引用 Audio，按需运行：合成、PSG、声像、播放、回声、音序、叫声与目录 |
| Game.Presentation.Tests | 引用 Content 并链接纯 C# 表现组件：菜单、文本、时序、资源包和保存反馈；不加载 Godot 引擎 |
| Game.ResourceImport.Tests | 无产品程序集依赖：维护中的 Python 导入检查 |
| Game.Process.Tests | 引用 Application：独立冷进程的 canonical 状态哈希与存档往返；辅助程序在 `Helpers/HashProbe` |

Domain 不依赖内容加载；Battle 检查真实内容下的规则，Application 检查会话接线。
相同功能在不同层的测试职责不同，不为每个功能单建项目。各项独立准备状态，
串行运行，不依赖执行顺序；场景夹具留在功能目录内。
`Shared/TestAssets.cs` 仅共享输入路径、缺文件报错、唯一临时目录和内容缓存，
不另建套件注册表或公共测试框架。

## 运行入口

在仓库根目录执行：

```powershell
# 默认解决方案中的七个测试项目，不含 Audio。
dotnet test

# 局部修改选择相应项目；先指定项目，再发现或筛选。
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --list-tests
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --filter FullyQualifiedName~RandomnessTests
dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter 'Name~SelectedMoveCompletesPublicTurn&Name~MOVE_TACKLE'
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --filter FullyQualifiedName~StoneEvolutionTests

# 音频始终显式执行；导入与跨进程检查也可独立运行。
dotnet test tests/Game.Audio.Tests/Game.Audio.Tests.csproj
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj
dotnet test tests/Game.Process.Tests/Game.Process.Tests.csproj

# 各程序集独立报告，避免互相覆盖。
dotnet test --logger 'trx;LogFilePrefix=tests' --results-directory .toolhome/test-results
```

`dotnet test` 自带还原和构建，不需要先执行 `dev env` 或重复 build。
仅在对应源码已经构建、依赖已还原时使用 `--no-build --no-restore`。
当前 Linux 云环境须先按 [串行构建](../docs/validation.md#工具与执行) 完成构建，
再用以上命令的 `--no-build --no-restore` 形式运行。
共享测试配置或跨边界行为变更应运行默认七项目，音频变更另跑 Audio；
局部检查不记为全套通过。

`test.runsettings` 将空项目、空筛选和发现警告视为错误。不要把仅命中一个项目的
筛选条件用于整个解决方案，否则其余项目会正确报空筛选。
`Game`、`Audio`、`ResourceImport` 分类可在选定项目内使用。
MSTest 负责发现和 TRX，不使用旧 shell／console 套件调度或手工成功计数。
354 招式的四个位置为 1,416 个独立数据行；按需音频测试中的 386 种普通叫声
也独立发现。这些行数不等于完整招式分支或视听验收。

普通游戏／音频测试直接调用产品代码，不启动其他测试程序集或音频 CLI。
只有 Process 项目启动专用 .NET 哈希辅助进程，只有 ResourceImport 项目执行
`scripts/checks/` 的 Python 检查。默认解释器为 Windows `python`、
Linux／macOS `python3`，可用 `FIRERED_PYTHON` 覆盖。资源检查需要 Python 3.12
及 `requirements-dev.txt`；普通 C# 测试不依赖 Python。

## 显式 Godot 资源注册检查

先构建当前 `Game.Godot`，再执行 `python scripts/checks/test-species-registration.py --headless`。
此入口不在默认 MSTest 中；它只编译临时探针，引用已构建产品，在官方 Godot 中检查
继承纹理身份、尺寸／位置、重复键、肖像晋升、几何与释放；缺失完成标记、原生错误或泄漏均失败。
`--output` 可保留证据。headless 通过不证明窗口渲染、完整游戏流程或真实音频输出。

## 输入与存档

- 内容测试使用本地 `content/generated`；依赖原作输入的导入／参考检查使用
  `content/raw/pokefirered/037335f4`。测试不下载素材、安装工具或执行上游脚本。
  资源准备入口见 [scripts](../scripts/README.md) 和
  [资源再生成](../docs/resource-reconstruction-guide.md)。
- 音频测试需要 `content/generated/audio/synth/firered.pack` 与同目录
  `normal-cries.bin`；音频导入另有显式入口，见 [音频说明](../docs/audio.md)。
- 后期应用／Continue 测试默认读取本地忽略的
  `checkpoints/current-schema3/controlled-checkpoint.json`，可用
  `FIRERED_TEST_CHECKPOINT` 指定已单独验证的当前 schema 3 存档。
  默认路径只是约定，不证明文件存在；缺失必须失败。
- 新游戏及不依赖后期状态的用例自行创建会话。
  `CampaignCompositionTests` 验证默认启动、新游戏严格存读／Continue 与公开移动；
  其检查点用例严格读取上述输入，并确认源字节不变，没有可选命令行转换模式。
  `GeneratedOnlyCampaignTests` 另验证隔离 generated 资源加载，不代表长路线。
- 普通测试不转换 schema 1/2，不猜旧昵称或缺失终身孵蛋数。
  受控夹具的名字、计数、个体和进度只是显式测试输入，不是玩家历史。
  旧档原件保留，不能冒充当前 Continue 或事件前检查点；严格存档准入边界见
  [当前组合](../docs/current-game-composition.md)。
- 文件系统用例使用唯一 `.toolhome/mstest` 临时目录；TRX 使用标准结果目录或
  显式 `--results-directory`。失败日志与成功复测分开保留。

## 参考向量与导入检查

生成数值、逐帧轨迹和验证报告只放在本地忽略目录，不纳入 Git；
手写 schema 输入、源输入清单、语义案例和独立生成器继续受控。
普通测试读取已准备数据，不隐式编译 C 或重写 golden。
缺文件必须报错并给出生成命令，按提示显式准备后复测；不能从当前产品输出
生成参考期望，也不能因缺失输入跳过。

`Reference/reference_c.py` 只供显式参考生成器使用，支持已有的 Windows MSVC
和 Linux C 编译器。`Reference/source_extract.py` 由调用方选择固定输入，
不依赖生产导入器；其快速检查不编译或运行原作 C：

```sh
python tests/Reference/test_source_extract.py
python scripts/checks/test-script-layout.py
```

大型动画族默认选择有明确语义／边界意义的代表案例，并保留每个案例从初始化
到销毁的连续逐帧断言，不以稀疏快照替代。具体族的生成入口与范围在
`tests/Reference` 或 `Game.Presentation.Tests/Reference` 文档中。
支持全量模式的生成器使用 `--full` 输出至外部目录，再设
`FIRERED_SOURCE_VECTOR_DIR` 让同一托管比较器逐值检查；指定目录缺文件时
不得回退到较小本地集合。全量迁移对照是显式工程检查，不是每次普通测试。
已移出 Git 的历史完整原件保留在外部迁移备份，不改写 Git 历史。

物理动画导入检查仍完整执行 75 项及独立源突变／blocked／再生成检查；
其 NativePresentation 包装行预算为 10 分钟，其余同类行为 3 分钟。
定向开发可选择已有 unittest 方法，不传方法则运行全套：

```sh
python scripts/checks/test-physical-animation-import.py PhysicalImportTests.test_task_affine_program_is_parsed_as_task_data_not_sprite_affine
```

正例可共享同次运行的不可变生成字节，但各项自行解码；突变和拒绝场景继续独立构建。
不能为了缩短时间把完整源导入检查替换为代表输入或删掉失败分支。

## 覆盖限制

72 个引用已删除 API 的历史项目已退休，机器可读原因保留在
[退休清单](../docs/test-retirement-20261015.json)。这不代表历史对照均有一对一替代；
各功能目录中的旧验收记录也不自动成为当前可执行入口。

历史 `results/phase4/render-coverage.json` 及旧录音输入 `se_m_toxic.qoa`
曾缺失，历史 PCM 哈希等价性与可选 PAN_ORACLE 研究对照不在已验证范围。
离线合成、受控存档、短命令路线和纯 C# 表现测试不能证明实际扬声器、
原生画面一致、Windows 运行或自然完整剧情。当前尚未验证的范围须与通过结果一起报告。
