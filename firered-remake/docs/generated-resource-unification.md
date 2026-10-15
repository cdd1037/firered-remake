# 转换资源收敛历史记录（2026-10-15）

> 以下保留旧批次的测量与验证，不能作为当前运行依赖说明。当前已改为 generated-only，见 [运行边界](generated-only-runtime.md)。

基线为 `f76783ae14bda1553a75c9fc765d0e7fc249f277`。原普通工程 ZIP 与资源树仍保留，可恢复本轮之前的输入。此次调整转换资源、读取路径和生成器；Domain、Application、存档模型未改。

## 实际减少

| 范围 | 之前 | 之后 |
| --- | ---: | ---: |
| generated 文件数 | 10,948 | 7,747 |
| generated 实际字节 | 293,597,998 | 117,684,685 |
| 业务源文件 / 行数 | 1,601 / 158,512 | 1,603 / 158,098 |
| 测试源文件 / 行数 | 2,118 / 245,598 | 2,118 / 245,598 |
| 工具源文件 / 行数 | 463 / 74,062 | 467 / 73,803 |

转换资源减少 **175,913,313 字节（约 175.91 MB，59.9%）**、3,201 个文件。业务源码字节从 11,285,265 降至 11,239,514，工具从 5,328,430 降至 5,311,322；测试保持 20,870,999。源代码统计包含 Git 跟踪的 `.cs/.gd/.py/.sh/.c/.h` 及本轮新增代码，测试数含保留的历史测试源码，不等同于当前运行的测试集合。JSON 排版变化不作为结构收敛成果。

共享 raw 本轮未改，仍为 5,452 个普通文件、22,437,332 字节。压缩 ZIP 的大小另由交付打包记录，不把目录字节数当作下载体积。

## 当前结构

- 常量：59 份逐包常量及 Tower 重复来源表收为 `shared-constants/values.json`，6,996 个整数符号。全部 175,238 份旧条目的数值一致；没有按包的覆盖列表或旧格式回退。National Dex 的 22 个动作绑定单独保留。详见 [共享常量](shared-source-constants.md)。
- 地图：统一 schema 2 的 `runtime-map.json`，保留 386 份规范地图或明确变体；累计目录只保路径引用。删除 156 份富地图和 1,127 份累计副本，15 份来源注册表收为一份。脚本文本从共享 raw 原路径读取，不在每张地图重复内嵌；按需从块与属性表计算格子信息。Seafoam 水流与 Trainer Tower 变体保留。详见 [地图格式](runtime-map-data.md)。
- 图像：1,895 对内容相同的 PNG/RGBA 只保 PNG，移除 28,457,664 字节；另删 160 份同图的内嵌 RGBA。Content 使用一个 PNG 解码器提供内存像素，Godot 使用同一声明图片。6 个尚无 PNG 对应的 generated 原始像素文件及 Reference 场地效果仍保留。详见 [图像格式](generated-image-assets.md)。
- 生成器：共享数值发布、紧凑地图生成与目录组合，以及图片导入器同步输出当前格式。没有存储地址 hash、链接恢复器或兼容旧生成资源的执行路径。

运行仍需要 `content/generated`、共享 `content/raw` 的脚本/表及各包实际读取的来源配方，以及 Godot 的字体与 Reference 效果；此项目尚不是仅带 generated 的最小运行发布包。参考 pret Git 仓仅供原始输入补充/再生成，不是当前地图读取的替代路径。

## 有界验证

- 完整 `FireRed.Current.slnx` 的 17 个项目构建通过，0 警告、0 错误。
- `Game.CurrentComposition.Tests` 13 项通过：加载 365 张地图、189 个已准入物种和 354 招式；新游戏可保存、Continue 后可移动；已有 generation 57604 的 Saffron 存档可加载，完整 payload、RNG、generation 保持，源档不改。
- 1,895 张 PNG 解码像素与删除前 RGBA 全部一致，尺寸错误和非法 PNG 头被正常拒绝；新生成 40 张选项框与 2 张道场脚印逐像素一致，16 项继承输入正常读取，没有再次写出配对 RGBA。
- 地图转换核对全部 1,464 份旧 runtime 内容、93,992 个原始格子/边框及 640 处事件结构；7 个短生成检查覆盖地图目录、Seafoam、Tower 与 Route16。常量所有旧值一致。
- 本轮临时迁移曾误按路径合并源码符号数组，已恢复 1,847 个数组，并核剩余误缩短为 0；旧累计包锚点也已改为直接路径，随后完整构建和 Current 通过。修复摘要单独保留，没有把错误生成物作为交付结果。

复验入口（CHECKPOINT 指实际交付关键档，OUT 为新输出目录）：

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false
dotnet tests/Game.CurrentComposition.Tests/bin/Debug/net10.0/Game.CurrentComposition.Tests.dll "$PWD" "$OUT" "$CHECKPOINT"
```

机器可读短结果见 `evidence/generated-resource-unification.json`。本轮没有重走历史战役、没有新增原生窗口或音频验收；地图加载数也不表示每张图的所有事件已开发完毕。完成资源收敛后只更新普通工程交付，不开展新玩法。
