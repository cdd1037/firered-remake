# Button Mode 与上下文 Help

参考固定 pret `037335f4c725d7c9aecdac87066f2002b4bd7e14` 的
`main.c`、`help_system.c`、`help_system_util.c`、`pokedex_screen.c`。
运行时只读取生成的 Help 内容，导入边界见 [Help 内容](help-content.md)。

## 输入边界

- 键盘 Q/W 为 L/R；原 R 快捷键保持原义。命名输入框获得焦点时 Q/W 仍可输入字母，手柄肩键可打开 Help。
- HELP：肩键打开当前上下文 Help。LR：肩键只在 Summary 翻页、图鉴分类翻页/列表分页和 PC 盒切换中工作，不是全局方向键。
- L=A：L 的按下/按住/松开增加 A 语义，同时保留物理 L。图鉴使用按住 R 加新按 Left/Right 翻分类页。
- Enter、数字键盘 Enter、手柄 A 和 L=A 按各自物理来源保留所有权，最后一个来源松开才释放 A。PC 的 Space 多选、按住 A 扩展矩形、松开 A 抓取沿用原处理器。
- `_Input` 观察物理边沿，`_UnhandledInput` 只分派一次规范化事件；鼠标语义命令不注册成物理按住键。命名框消费之前观察松开；失焦/断连清理按住状态。提交 Button Mode 不凭空产生一次 A 确认。
- Start/Tab 仍先走图鉴叫声，再考虑普通野外菜单。Diploma 使用规范化的 held-A，并保留原有独立键盘 A 别名。

## Help

- 三层：主题、最多七行的问题列表、问答。第一次显示欢迎语，只接受 A；`help_intro_seen` 事务设置 `FLAG_SYS_SAW_HELP_SYSTEM_INTRO`，不直接在界面写存档。
- A 选择，B 返回或退出，L/R 在主题/列表/答案关闭。列表上下移动一项、左右七项；采用源代码的方向相关中点滚动，滚动或返回列表有两帧延迟，返回保留选择位置。
- 内容按地图/Surf、Battle、Bag、Party、Summary 页、Trainer Card 正反面、PC 模式等解析。Battle 保持其上下文，覆盖嵌套 Bag/Party/Summary；内容条件由真实标志、随身物品和关都捕获数提供。
- 打开后暂停游戏和表现回调，累计游玩时间继续；共享音频宿主仍继续播放。BGM 在现有 fade/cry gain 上乘 0.5，关闭恢复，不重启曲目。图鉴遵守原作“不额外降低 Help 音量”例外。共享宿主 reset/关闭清除 Help gain；合成算法未改变。
- 使用现有源字体，问答/描述统一按真实字宽换行；测试穷举生成问答和最长替换姓名，保证字体行距与面板边界。界面是已有字体和绘图部件构成的布局，并非声称逐像素复原原 Help 背景。

## 验证

- `Game.Presentation.Tests` 的 `ButtonModeHelpTests` / `OptionEditingTests`：11 项通过，覆盖混合物理来源、模式重映射、断连、菜单层级、分页/中点/延迟、返回游标和全部导入文字边界。
- `CampaignHelpTests` 检查第一次欢迎标志的单独事务、Save/Continue 与非 HELP 模式拒绝。
- 实际 Godot 4.7.2 / .NET 10.0.12 的隔离 headless 输入探针验证 `_Input`→`_UnhandledInput`、共享音频宿主、Help 暂停与游玩计时、Diploma held-L=A、鼠标、失焦、断连和命名消费前松开。
- Headless 使用 Dummy 音频；不代表真实扬声器听感通过。临时探针在工程外，普通项目不包含测试入口或额外运行依赖。

## Bounded desktop check

2026-10-16 13:52–13:55 UTC, existing cloud X11 desktop: Q opened the first-use greeting and accepted the source introduction flag command. Enter opened context topics, How do I do this opened a seven-row question list, Right paged seven and Enter opened a complete item-use answer. Backspace restored the same selected question and scroll; Q closed back to the unchanged field. Text fit the viewport in these observed paths. Native log includes actual SE_HELP_OPEN and SE_HELP_CLOSE dispatch; Dummy is not audible acceptance. Controlled test checkpoint and separate log `/workspace/shared/firered-pc-visual/help-inventory-native-1351.log`; no disk save of synthetic gameplay.
