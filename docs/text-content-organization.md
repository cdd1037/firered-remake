# 文本与内容边界

当前准备汉化的整理保留英文输出，不改变字体、图片或玩法标识。

## 文本权威

- 原作剧情：`import-story.py` 提取源 symbol 到 `TextBank`，运行时由
  `PretCampaignCatalog.TextPages` 处理 `\\n/\\l/\\p/$`。保留这些 symbol；不重新编号剧情。
- 原作战斗：`PretBattleText` 读取 generated `battle/text.json`，源符号、占位符与
  控制指令属于既有战斗适配器。不能把页面字符串当成普通 UI 模板再解释。
- 重制 UI：作者维护的英文分组位于 `src/Game.Content/Text/en/`，随 Content 程序集嵌入。
  `PresentationTextCatalog` 按组和语义 ID 查找，只支持具名字符串参数；当前消费者为
  `CampaignSaveFeedback`。结果枚举仍决定提示，文本不决定行为。参数仅替换一次，
  玩家名字中的花括号保持字面值；缺 ID、缺参、多参和重复参数明确失败。
- 名称／说明：目前仍由物种、招式、道具等实际目录按稳定领域 ID 提供。不要从
  `pret:MOVE_*` 等标识字符串推导翻译，也不要用英文名称验证玩法身份。

作者英文 JSON 是维护输入而非生成快照；原作 raw/generated 与 oracle 继续不纳入 Git。
工程 IR 的 `TextDefinition.Language` 不是当前游戏的 locale 选择器。此次没有增加语言
选项或翻译回退策略；后续语言装载可在相同文本边界接入，不需要改保存的玩法 ID。

## 后续迁移顺序

1. 队伍菜单先把 SUMMARY/CUT 等显示标签与动作身份分开；复用现有领域 MoveId。
2. 集中选项、普通菜单与共享战斗提示，保留源控制码和 typed receipt 决策。
3. 逐项解除实际加载 Content 中英文名称、说明和前缀参与规则校验的耦合；保留
   ID、范围、价格、效果、布局与引用约束。道具名称非空校验集中在 SharedItemCatalog；
   Saffron/Fuchsia 适配器仅保留各自说明文本与玩法约束。原英文忠实性仍由导入检查证明。
4. 再接入翻译与排版验收。中文字体、图片字与真实 IME 另有明确验证范围，当前未实施。

不要将所有文本堆进一个文件，不引入第二份玩法目录、来源指纹或无消费者的 locale 元数据。

首批验证：文本目录与区域物品 30 项、原存读档提示 4 项通过；修改canonical显示名
不会改变物品对象身份／规则，损坏 ID、价格、口袋、效果仍被拒绝。实际云 X11 窗口
从公开 SAVE→YES 显示 `RED saved the game.` 并可返回菜单；音频为 Dummy，未测中文。
