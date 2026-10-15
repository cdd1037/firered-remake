# MSTest 测试项目整理 · 2026-10-15

保留 MSTest 4.5.1，按依赖边界从一个总项目拆为八个项目，默认仍统一执行 `dotnet test`。项目职责与定向命令见 [tests/README.md](../tests/README.md)。五个产品程序集和两个音频项目没有因测试目录整理改变职责。

## 组织变化

- Domain.Tests 只依赖 Domain；真实内容支持的战斗场景归 Battle.Tests。Application、Content、Audio、Presentation、ResourceImport 和 Process 各自声明所需依赖。
- 旧的二十多个套件目录收敛为八个项目。功能使用 Turns、Moves、Residuals、Switching、FieldEffects、Lifecycle、FieldItems、WildBattles、TrainerBattles、Engineering 等目录，不再以验收批次或旧 console 项目命名。
- DomainTests 拆成标识、随机数、属性、类型、伤害、基础战斗命令、地图移动和状态校验；Growth/Capture、Integration、Content、天气／屏障、野战和音频序列也按功能拆文件。公共场景准备仅留在对应功能内，MSTest 标准初始化负责每项独立状态。
- 构建配置和小型 TestAssets 路径辅助源码集中共享；音频内部 API 只对 Game.Audio.Tests 开放。数值／冻结夹具及显式参考生成器随功能移动。七个 C 参考生成器均能从新路径导入公共 reference_c，未执行生成或替换期望值。内容 JSON 夹具的源路径元数据随目录更新。
- 跨进程哈希检查独立放在 Game.Process.Tests，Helpers/HashProbe 只链接同一份 HashProbeScenario，不再链接包含存档存储辅助类的整份 AuditFixtures，也不依赖 MSTest。Python 仅用于必要的资源导入检查和显式参考向量工具。
- 删除旧总项目及功能目录内残留的 NuGet.Config；音频 verify 脚本与说明改用独立音频测试项目。废弃总项目缓存、内容夹具内嵌套缓存和空目录已移入回收站。

## 验证

实际工具：.NET SDK 10.0.102，运行时 10.0.2；目标仍是 net10.0，Godot SDK 仍是 4.7.2。

完整回归命令：

```powershell
dotnet test --no-build -m:1 -nr:false --logger 'trx;LogFilePrefix=layout' --results-directory .toolhome/test-layout/verified
```

八个 TRX 合计 **2,379 通过、零失败、零跳过**：

| 项目 | 通过数 |
| --- | ---: |
| Application | 92 |
| Audio | 574 |
| Battle | 1,547 |
| Content | 48 |
| Domain | 101 |
| Presentation | 10 |
| Process | 1 |
| ResourceImport | 6 |

完整运行后仅调整工程会话目录／命名空间和排版；最终 `dotnet build --no-restore -m:1 -nr:false` 为 **零警告、零错误**，并复跑受命名空间调整影响的工程会话 17 项，全部通过。构建日志：`.toolhome/test-layout-final-build.log`。

定向筛选证明（均指定对应项目并使用 `--no-build`）：

- `FullyQualifiedName~FireRed.Tests.Application.Engineering.`：17 项通过。
- `Name~SelectedMoveCompletesPublicTurn&Name~MOVE_TACKLE`：仅四个 actor 数据行通过。
- `FullyQualifiedName=FireRed.Tests.Domain.RandomnessTests.rng_unsigned_overflow`：仅一项通过。

定向报告在 `.toolhome/test-layout/selected`。Roslyn 语法核对确认原有 558 个测试方法及其数据属性、断言均保留；差异仅为路径和辅助类引用。总用例数与重组前一致。所有参考 Python 文件通过语法检查，七个 C 生成器的公共模块导入也验证通过。`git diff --check` 无空白错误。

迁移中的旧缓存误编译、辅助类型引用和提前使用 no-build 的失败记录保留在 `.toolhome/test-layout-discovery.log`、`.toolhome/test-layout-run.log`，它们不作为通过证据。其他中间编译诊断见本次任务工具记录。曾与运行中的 testhost 重叠的构建因 Windows 文件锁失败；最终构建在测试结束后重新执行并通过。

## 覆盖边界

没有恢复退休的领域 API，没有按产品输出生成新 golden，也没有宣称自然完整剧情、原生画面或真实声卡输出通过。继承之前迁移说明中的输入限制和研究对照缺口。

## 提交前审核

修正两处跨平台问题：Linux／macOS 的资源检查默认使用 python3，Windows 使用 python，FIRERED_PYTHON 仍可覆盖；仓库为 shell 脚本声明 LF，防止 Windows 检出破坏 Bash 入口。还将 dev env --json 的输出收敛到工具变量和 PATH，避免将其他进程环境或凭据写进诊断输出；用哨兵环境变量验证了 JSON 与 shell 导出均不会泄露无关环境。

受影响的六项资源检查再次全部通过，报告位于 `.toolhome/test-layout/review`。`python dev doctor` 检查 SDK、Godot 和本地包均通过。其余代码与输入未改变，复用本次已通过的 2,379 项完整回归、最终零警告构建和定向筛选结果。

音频 build/run 脚本也改为接受 FIRERED_DOTNET、既有工具链或 PATH，不再强制不存在的历史工具目录；实际 Release 构建零警告／零错误，默认与显式 dotnet 路径的 CLI 启动检查均通过。为 dev 与维护中的音频 shell 入口补上 Git 可执行位，并通过 Git Bash 的 shell 语法检查。
