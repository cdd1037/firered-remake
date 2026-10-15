# FireRed 普通工程

这是可直接解压的普通工程，第一轮资源与运行时整理已提交到 GitHub main：`7b238a251823a44fa7c223b36d72aac5db63dfe0`（基于 `9a0c6c3c0fac85a1e414cbd56fb2712470384d2e`）。后续改动以 Git 历史和工作树状态为准。没有去重对象库、恢复器或旧 Git 历史。

- `firered-remake/`：游戏工程。维护解决方案 `FireRed.Current.slnx`；Godot 工程 `src/Game.Godot/project.godot`。
- `firered-audio-player/`：自定义合成器源码、开发工具和必要资源。主游戏继续使用合成音频。
- `checkpoints/`：保留的代表存档与说明。当前 `current-composition-20261015/continued-checkpoint.json` 为 generation 57604；不会自动覆盖用户存档槽。

## 开发和运行

安装正式版 .NET 10.0 SDK 与 Godot 4.7.2 .NET/Mono，进入 `firered-remake` 执行 `dotnet build`、`dotnet test`。导入工具和资源导入测试需要 Python 3/Pillow；普通 C# 测试可以单独运行。首次用 Godot 编辑器打开工程以导入字体等项目资源；也可按工程 README 使用现有导入辅助命令。

跨平台工具发现、启动参数、测试范围见 `firered-remake/README.md` 和 `firered-remake/tests/README.md`。随包不含 SDK、Godot、NuGet/编译缓存、运行日志或测试探针。

## 当前资源边界

运行时只读 `content/generated` 中的转换数据/图像/音频，以及 Godot 项目自身的字体与效果素材，不再读取 pret 原作 C/ASM、`content/raw`、导入配方或 source-lock。

`content/raw/pokefirered/037335f4` 保留固定版本原始导入输入；`content/raw/local` 的三个调色板是实际变体。地图图层、NPC、部分道具/UI 已使用共享图集；原始 RGBA 和玩法字段经过对应检查。地图进入时仍按现有有界缓存还原整层纹理，尚未宣称 GPU 内存或帧率改善。

导入工具保留必要来源版本与配方。已删除 81 个无消费者的工具锁/回执；仍有 199 个工具侧配方文件，不代表已消除所有历史工程结构。运行边界和再生成说明见 `firered-remake/docs/generated-only-runtime.md`、`firered-remake/docs/resource-reconstruction-guide.md`。

## 验证和已知限制

- 当前游戏可从仅含 generated 的独立资源根加载，新游戏、存档、Continue、移动及 generation 57604 存档验证通过；原存档 payload、RNG、generation 保持。
- 原生 Godot 窗口检查覆盖代表存档所在地图、NPC、菜单、背包、TM Case 和选项框图集，并检查正常窗口关闭。音频使用 Dummy，未进行真实扬声器听感验收。
- 未重放整条历史路线。早期自然路线并未据此补写为通关；当前代表档延续此前已披露的受控联盟通关分支，详情保留在 checkpoints 说明中。
- 部分 Domain 类型仍保留原作来源/所有权证明字段与历史命名；后续需要成组精简，不能误删命令、存档和玩法实际使用的标识。故事 IR 已将共享来源路径提到程序级，逐指令仅保留行号和必要例外；运行时重建相同语义。
- 各原作导入入口的成熟度不一；本轮完成当前维护的导入检查和指定再生成检查，不等于每个旧开发入口都完成全量冷重建。

在外层目录初始化 Git 即可。`.gitignore` 将素材、存档与缓存排除，只纳入作者源码、配置和工具。资源由普通工程 ZIP 携带，不应使用 `git add -f` 强制加入 Git。
