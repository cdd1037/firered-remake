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

## 共享 Summary 路由与绘制锚点

LR 肩键通过同一个 active-Summary 分派器进入野外、PC、战斗和培育屋已有处理器，保留各自身份、退出、Egg、详情和转场锁。源依据为 `pokemon_summary_screen.c:IsPageFlipInput` 与 `party_menu.c` 的普通 Summary 入口，不新设分页规则或应用命令。学习替换、删除和 Reminder 候选是固定的 move-selection 页面：源 `PSS_MODE_SELECT_MOVE`／`PSS_MODE_FORGET_MOVE` 从 `PSS_PAGE_MOVES_INFO` 开始并锁住 moves，不因肩键增加分页。详情中的 Left/Right 也不离开当前选择。完整访问／淡变契约见[概要](summary-native-presentation.md)。

共享绘制遵循源精灵中心，而非给翻转图额外加宽高。Godot 4.7.2 的负尺寸只改变 UV：

- Easy Chat 矩形光标：两个 64×32 精灵中心为共享 X ± 32；右半左上角为共享 X，而非 X + 64。源 `easy_chat_3.c:CreateRedRectangularCursorSpritePair`。
- Easy Chat 下箭头：中心 Y=156、尺寸 16，故 top=148，不是 164。源 `CreateVerticalScrollArrowSprites`。
- Fame Checker 下箭头：中心 Y=100，故 top=92 加既有 bounce，不是 108 加 bounce。源 `fame_checker.c:FC_CreateScrollIndicatorArrowPair` 及导入滚动偏移。

Party 选择器的绘制与命中共用引擎无关几何；Storage 手部和 TM 光盘偏移仍为有意动画。每个共享 `DrawSetTransform` 路径在返回前恢复 identity。原生文字表面保留自己的 viewport／texture，按页面边界裁切，并使用当前行偏移；这些约束不要求替换字体、画布缩放或资源格式。

## 验证边界

`ButtonModeHelpTests`／`OptionEditingTests` 检查混合物理来源、模式重映射、断连、菜单层级、中点滚动／分页／延迟、返回游标及所有导入文本边界。`CampaignHelpTests` 检查独立欢迎标志事务、Save/Continue 和非 HELP 模式拒绝。实际 `_Input`→`_UnhandledInput` 的适配、共享音频 gain、暂停／计时、鼠标、失焦、断连及命名消费前松开需要独立宿主检查。

原生 Help 代表路径、Summary／图鉴转场检查和六种负尺寸纹理／区域像素检查统一记在[验证](validation.md)。源坐标证明与引擎镜像行为证明互相独立。受控路径不覆盖每个实际消费者／实体手柄或完整 Help 背景逐像素一致；Dummy 音频不代表扬声器听感验收。临时探针在工程外，不成为运行入口或依赖。
