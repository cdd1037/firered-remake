# Godot 表现层

`Game.Godot` 负责游戏组合、输入、窗口绘制、资源生命周期和 PCM 输出。规则与状态事务仍由 Domain/Application 决定，文件夹划分不改变类型、命名空间或程序集边界。整体契约见[架构](../../docs/architecture.md)。

## 目录

| 目录 | 职责 |
| --- | --- |
| `App/` | `Bootstrap` 场景、游戏视图根节点和游玩计时；`Persistence/` 保存平台适配与反馈 |
| `Audio/` | PCM 主机、音乐淡变和动画音频适配；合成算法继续共用 `Game.Audio` |
| `Battle/Views/` | 单打、双打、训练家、教程以外特殊战斗和动画视图 |
| `Battle/Presentation/`、`Battle/Capture/` | 战斗文字、HP、选项与捕获演出 |
| `Battle/Animation/Core/` | 动画程序执行、资源解析、帧合成、调色板、窗口和扫描线 |
| `Battle/Animation/Effects/` | 各动画家族的具体效果 |
| `Field/` | 地图技能、草层、图块、冲浪、旅行和探宝器；`Transitions/` 负责门、梯子、淡变和天气合成 |
| `Input/` | 物理按键、鼠标、模式映射与系统输入 |
| `QuestLog/` | 回顾视图、地图与调色板呈现 |
| `UI/` | 按文本、菜单、开场、命名、队伍、库存、PC、图鉴、邮件、服务等功能分组 |
| `World/Regions/` | 地区剧情视图、专用布局和场景模型 |
| `Rendering/` | 图像解码、图集、纹理缓存和字体 |
| `Resources/` | 表现包读取器及 `PresentationAssets/` 中的资源描述 |
| `Diagnostics/` | 显式工程夹具与诊断输出 |

`CampaignView` 与 `SourceBattleAnimation` 的 partial 文件按功能存放，类型名保持不变。功能特有的教程和剧情差异继续由各自控制器表达，不通过目录名称选择玩法。

## 项目与资源路径

Godot 工程仍从本目录的 `project.godot` 打开，主场景为 `res://App/Bootstrap.tscn`，脚本为 `res://App/Bootstrap.cs`。`res://` 始终相对项目根目录，字体与效果素材仍位于 `Assets/`，不会因脚本移动而改变资源根。

`Game.Godot.csproj` 递归发现 C# 文件。表现测试只链接既有的纯 C# 子集；修改目录后应同步更新显式路径和通配符，并对比 MSBuild 编译输入，不能用扩大引用范围或删除缺失项绕过问题。Godot 生成的 `.cs.uid` 在所有子目录中均由 Git 忽略。

## 检查范围

本次整理只移动 C# 文件并更新引用；441 份源码字节保持一致。Godot 编辑器资源扫描、编译输入核对和受影响的源码路径检查已执行。当前 NuGet 构建阻塞、两个既有缺失链接以及资源再生成的换行差异仍保留，完整命令和结果见[验证记录](../../docs/validation.md#godot-目录整理2026-10-21)。
