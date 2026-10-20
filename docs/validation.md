# 验证与环境恢复

本页维护证据口径、恢复约束和最近的有界验收。测试项目、输入与定向命令见
[测试说明](../tests/README.md)；产品分母见 [范围合同](scope.md) 和
[完整单机分母](full-scope-audit.md)，功能现状见 [当前范围](first-clear-remaining-scope.md)。
已结束的迁移、修复批次和测试布局记录由 Git 历史保留，不另建历史文档副本。

## 工具与执行

基线固定为官方 Godot 4.7.2 .NET、net10.0 和实际运行的 .NET 10；
开发 SDK 为正式 .NET 10.0.100 及以上，允许后续功能带／补丁。工具发现与正常还原见
[README](../README.md)。不得用系统中较旧的 Godot 替代，或把历史 Linux 下载清单
中的 SDK 补丁号当作所有开发机的门槛。

当前 Linux 云环境使用已验证的串行构建，先完成正常依赖还原：

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

该环境的默认并行 MSBuild 曾无诊断失败；不要反复尝试或修改机器配置。
`--no-build` 测试只能复用包含本次改动的产物。记录完整命令、实际 SDK／引擎／
托管运行时、所测程序集及通过／失败／未执行项；不要在正在运行的原生窗口或
测试进程上覆盖程序集。失效缓存、空筛选、缺失夹具和中间失败不能记为通过。

原生窗口检查还须：

- 区分普通公开输入路线、受控状态、独立渲染探针和无窗口检查。记录起点、输入、
  中断／重开、截图或逐帧比较以及所用二进制，不把较早 DLL 的结果重标为后继构建。
- Shell 没有 `DISPLAY` 不代表桌面不存在；使用实际可用的桌面终端及其环境。
  不假定不同执行环境共享 `/tmp`，证据放在双方确认可访问的路径。
- 隔离 HOME／XDG 或明确使用受控档，保留玩家原存档，退出后确认进程已关闭。
  独立成品检查应清除开发路径，核对 CLR 来自包内；需要匹配的官方导出模板。
- 普通运行不捕获完整状态。`FIRERED_VERBOSE=1` 只在需要时打开详细控制台诊断；
  `FIRERED_EVIDENCE` 显式指定详细证据输出，包含完整 ready／command 状态，
  不用于正常游玩。

## 证据与未关闭门禁

定义存在、脚本引用闭合、规则测试、公开命令路线、原生像素／时序和实际声音
分别需要证据。测试数、地图数和代码行数不是完成率；静态未发现新缺口不等于
全游戏已验收。测试通过只覆盖所选案例，不能把未执行项或缺失输入视为跳过后通过。

- 当前 schema 3 严格读取，不自动迁移 schema 1/2，也不猜旧昵称、缺失的终身孵蛋
  总数或历史事件。受控名字、计数和进度仅是测试输入。显式离线转换、原件保护及
  Tower 历史收敛边界见 [当前组合](current-game-composition.md)；
  派生字段省略不改变 schema 或完整性合同，见 [存档](save-storage.md)。
- 后期已完成档不能证明原本的 Fuji 救援／宝可梦之笛、Selphy 救援／回家或
  Champion→Hall 首次路线；仍需可信的事件前检查点。已有受控联盟分支不构成
  一条自然新游戏全流程，不能补造获胜、捕获或玩家历史。
- 全招式身份、共享控制器或源向量覆盖不等于所有运行分支逐帧看过。
  场地／Quest 历史缺少过渡原因时不猜测；未支持的分支继续明确拒绝。
  原生画面对照、自然路线与单项机制限制留在相应功能文档，不从旧审计条目
  自动生成新的缺失功能结论。
- Dummy、离线 PCM 和事件完成均不证明真实扬声器、听感或全场景同步。
  已检查的云桌面没有可用音频设备／服务，真实输出仍是未关闭的 P0 门禁，
  但不阻止已授权的其余开发。音频范围见 [音频说明](audio.md)。
- Linux 原生／导出证据不证明 Windows 已运行。Windows 配置检查、实际导出、
  实机启动与 trimming 发布必须分开报告。

## 最近的有界验证（2026-10-20）

环境：官方 Godot 4.7.2 .NET、SDK 10.0.401、实际 .NET 10.0.12。
最终产品代码为 `80b21f4`；随后文档整理不改变产品程序集。
完整解决方案串行构建、Godot 强制重建及最后的 Summary 时钟修正重建均为
零警告／零错误。

| 实际执行范围 | 通过数 | 本地报告 |
| --- | ---: | --- |
| Content 全项目 | 1,153 | `evidence/visual-continuity-20261020/content-all.trx` |
| Application 定向 | 169 | 同目录 `application.trx` |
| Presentation 定向，含最终时钟修正 | 322 | 同目录 `presentation-final.trx` |

以上合计 1,644 项，零失败／零跳过；不是七个默认测试项目全测。
完整命令及构建日志在 `evidence/visual-continuity-20261020/commands.txt` 与同目录日志。
另有 Windows 导出配置静态检查 8 项通过，本批未导出或运行新的 Windows 包。

前一重构批次的 Process 全项目 1 项与 ResourceImport 定向包装器 4 项通过，
分别见 `evidence/refactor-20261020/process-refactor.trx` 和 `import-refactor.trx`。
这些是先前范围的证据，不重复计入 1,644 项，也不代表全量导入器冷重建。
准入／集合／学习表再生成、训练家卡像素和存档投影的具体范围见
[准入格式](pokemon-admission-format.md)、[训练家卡图集](trainer-card-atlas.md) 与
[存档](save-storage.md)。

### 受控原生检查

以下均使用实际引擎绘制，而非预测截图。各探针保留所测 DLL、运行日志和图片，
对应先后不同的构建：最后 Summary 时钟修正后重跑 UI 探针；不能把其余更早且
功能源码未变的独立样本说成全部已在最后 DLL 重跑。证据目录均为本地忽略产物。

| 范围 | 实际观察与限制 | 本地证据根目录 |
| --- | --- | --- |
| 开场命名 | 旧／新 DLL 各 243 帧；13 个边界精确匹配源图，230 个淡变帧允许每通道最多 1 的混色舍入。另有 2,567 帧公开输入的步进时钟路线、中文预设／自定义劲敌名，以及 999 帧受控女性命名到卧室尾段。未写玩家存档；不是手动实时全流程或硬件调色板等价。 | `/workspace/shared/firered-naming-transition-native/` |
| 开场电影 | 旧／新各 21 个实际绘制样本；五个背景相位、九个耿鬼静止帧各 4,711 个不透明源像素，以及三帧草的各 594 个像素匹配。不是完整电影逐帧验收。 | `/workspace/shared/firered-intro-continuity-native/` |
| 普通连通边 | 步行南／北、跑步、自行车四场景，旧／新各 62 个样本；初始跳位从 16 像素改为 0，按住方向继续下一格。96×160 无人物地图区域接缝像素一致；不覆盖邻接 NPC 生命周期、任意剧情到达或 Quest 接缝像素。 | `/workspace/shared/firered-field-continuity-native/` |
| 负尺寸翻转 | 普通／H／V／双翻转纹理及区域共六例、216 项像素／边界断言。轮船修正结合此引擎证明与源中心坐标，不冒称完整公开航程实测。 | `/workspace/shared/firered-negative-rect-proof/` |
| Summary／图鉴 | 五组实际控制器场景、24 帧。真实 `1/60` 回调翻页偏移为 60／120／180／240／240 像素，30,000 项头部像素比较通过；含输入抑制、快速反向、500 ms 停绘、Help、详情返回、退出重进、换个体及键盘／手柄适配器。受控状态通过严格存读，未写玩家存档。 | `/workspace/shared/firered-ui-continuity-native/` |
| 训练家卡 | 635 次成对渲染比较，含 16 组面板各 36 帧翻面、两所有者共享及释放；59 个逻辑资源共 1,547,712 个 RGBA 像素保持。不是完整公开 UI 操作或性能测试。 | `/workspace/shared/firered-trainer-card-native/` |

UI 最终输出为 `after/result.json`、24 帧及 `native.log`；`build.sh` 仅复制候选
程序集和已验证字体缓存，构建隔离探针，`run.sh` 从桌面终端启动。场地目录的
`before/result.json`、`after/result.json`、`boundary-pixel-checks.json` 分别记录
旧版重现、修正版行为和像素比较。其余源行为与剩余边界见 [开场](opening-presentation.md)、
[电影](intro-movie.md)、[Summary](summary-native-presentation.md)、
[图鉴](pokedex-habitats-and-area.md)、[场地过渡](plain-field-transition-presentation.md)。
所有检查使用禁用音频或 Dummy，不证明实际扬声器、全部语言／控制器、完整自然剧情
或性能提升；本批未导出游戏包。

此前场地检查仍仅保留原有界结论：公开命令覆盖两条新游戏 Oak 入口至初始伙伴选择、
受控起点的 S.S. Anne 离港，以及已完成救援后 Selphy 的请求到期／刷新和奖励。
Selphy 奖励使用已有 Persian 经 PC 转移及实际请求流程，不伪造物种、请求、RNG
或退回已取得旗标。2026-10-19 原生样本覆盖 Route4→MtMoon 预览、
Viridian Forest 名称窗、Mansion SHADE 楼梯、Rock Tunnel 黑暗梯子、
Tower5F 雾与菜单返回，证据在 `/workspace/shared/field-transition-native/`。
这次原生样本不覆盖 Tower 净化白淡变；上述路线也不证明更早的救援或自然整段剧情。
当前机制和剩余范围以 [场地过渡](plain-field-transition-presentation.md) 为准。

初始构建／负例断言失败、旧二进制空筛选、缺失导入缓存、探针自身断言错误，
以及暴露 Summary 量化缺陷的 `after-pre-quantization` 均保留为失败或被替代证据。
最终复测不删除这些记录，也不把早期宽泛控制器通过当作最终像素通过。
依赖缺失当前检查点的后期用例未计入。

## 恢复工作约束

本地工作区曾回退到 `5722153`，后续本地提交对象和部分临时证据丢失。
保留分支 `preservation/pre-recovery-5722153` 记录起点；恢复提交
`024e07ec5aa493a3036f5a0ded6589e03fff64fb` 的 tree 与远端
`e858419be7810ef702c2f6476102320c327d2eb2` 的
`9f0bfb13531163e07378378c12757e08b3d4d64e` 一致。
这证明恢复的文件树，不复造缺失提交或找回不存在的测试日志。

后续重建的共享脚本／移动／文字／菜单与导出配置必须重新验证。
官方工具链放在工程外，`python dev doctor` 核对实际工具；恢复资源／参考向量
使用维护中的独立导入器或源提取器，不能用产品输出改写期望值。
缺失的旧日志、音频录制或事件前存档继续标为缺失，不以恢复说明替代新证据。
