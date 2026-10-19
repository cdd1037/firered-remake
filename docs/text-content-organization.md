# 文本与内容边界

当前准备汉化的整理保留英文输出，不改变字体、图片或玩法标识。

## 文本权威

- 原作剧情：`import-story.py` 提取源 symbol 到 `TextBank`，运行时由
  `PretCampaignCatalog.TextPages` 处理 `\\n/\\l/\\p/$`。保留这些 symbol；不重新编号剧情。
- 原作战斗：`PretBattleText` 读取 generated `battle/text.json`，源符号、占位符与
  控制指令属于既有战斗适配器。不能把页面字符串当成普通 UI 模板再解释。
- 重制 UI：作者维护的英文分组位于 `src/Game.Content/Text/en/`，随 Content 程序集嵌入。
  `PresentationTextCatalog` 按组和语义 ID 查找，只支持具名字符串参数；当前消费者为
  `CampaignSaveFeedback`、Start／队伍动作菜单、Options、物品菜单与 PC 通知／帮助。
  结果枚举／稳定动作 ID 决定行为。参数仅替换一次，
  玩家名字中的花括号保持字面值；缺 ID、缺参、多参和重复参数明确失败。
- 名称／说明：目前仍由物种、招式、道具等实际目录按稳定领域 ID 提供。不要从
  `pret:MOVE_*` 等标识字符串推导翻译，也不要用英文名称验证玩法身份。

作者英文 JSON 是维护输入而非生成快照；原作 raw/generated 与 oracle 继续不纳入 Git。
工程 IR 的 `TextDefinition.Language` 不是当前游戏的 locale 选择器。作者 UI 已增加显式本地覆盖装载（见下），没有新增游戏语言选项或保存字段。
后续原作文本／名称覆盖仍需使用各自的语义边界，不需要改保存的玩法 ID。

## 后续迁移顺序

1. 队伍菜单已把 SUMMARY/CUT 等显示标签与动作身份分开：普通动作使用语义 ID，
   野外招式使用既有 MoveId；动作由 ID 分派，显示由文本／招式名称目录提供。
2. Start／Options 已按语义 ID 集中；选项枚举与行号仍决定操作，标签独立。
   下一步迁移其他普通菜单与共享战斗提示，保留源控制码和 typed receipt 决策。
3. 逐项解除实际加载 Content 中英文名称、说明和前缀参与规则校验的耦合；保留
   ID、范围、价格、效果、布局与引用约束。道具名称非空校验集中在 SharedItemCatalog；
   Saffron/Fuchsia 适配器仅保留各自说明文本与玩法约束。原英文忠实性仍由导入检查证明。
4. 再接入翻译与排版验收。中文字体、图片字与真实 IME 另有明确验证范围，当前未实施。

不要将所有文本堆进一个文件，不引入第二份玩法目录、来源指纹或无消费者的 locale 元数据。

首批验证：文本目录与区域物品 30 项、原存读档提示 4 项通过；修改canonical显示名
不会改变物品对象身份／规则，损坏 ID、价格、口袋、效果仍被拒绝。实际云 X11 窗口
从公开 SAVE→YES 显示 `RED saved the game.` 并可返回菜单；音频为 Dummy，未测中文。

队伍动作批验证：表现 18 项（含已有存读档 4 项）及 NoncombatFieldMove 18 项通过，
覆盖重复招式顺序、显示名碰撞、蛋／邮件／内容门槛、ID-only命令与原公开行动路径。
Domain 不再提供英文字面 Label 作为支持判断；输入、鼠标行数与取消路径保持原结构。

物种展示包已解除显示名→物种 ID 的反向拼接：进化、繁殖、礼物蛋及游戏角五个
加载器显式接收 canonical PokemonCatalog，用稳定 ID／数值 ID 绑定素材。显示名仍
须非空；拥有名册、图鉴元数据、布局与素材引用约束保留。不得类推删除训练师的
编码姓名：该字段参与原作 personality 生成，属于规则输入而非普通显示标签。

该批 Content 29 项、Presentation 36 项通过，三个受影响项目编译零警告／错误。
包括可改名称、错误物种／几何／空名拒绝，以及 Options 全枚举英文、具名参数和
原队伍动作／存档回归。删除的两个旧摘要方法已无调用；当前正式摘要渲染保持。

实际云 X11 窗口抽验：公开 Start→POKéMON→成员动作菜单→返回→OPTION→返回，
队伍提示与动作标签、选项标题／六项值／CANCEL均清晰显示。该验收仅证明当前英文
渲染与代表输入路径，非中文布局或真实音频验收。

物品目录后续整理：Quest 四种基础物品、两个公开 Gym 校验器和 Town Map 不再以英文
名称判定身份；仍拒绝空名称和错误 ID／效果／容量／能力。四个区域拾取共用 canonical
物品名称，移除其导入输出中的 27 个重复 `english` 字段；事件和脚本 contracts 字节不变。
受影响 Content 74 项、维护导入正负例 11 项通过，四区 producer `--check` 通过。
新增字段没有进入存档，玩法和输入命令不变；区域资源减少 587 字节并继续保持 Git 忽略。

战斗选择界面直接消费已有原作符号：`SourceBattleSelectionText` 在加载时解析四格菜单、
行动提示、Foe/Wild 前缀与 PP/TYPE 标签。三个实际战斗视图共用同一实例；已知布局控制
严格解码，插入的玩家／宝可梦名字只作为字面参数，不再重新解释成源控制码。事件分页
仍属于 `PretBattleText`；没有把原作字符串复制进作者英文目录。固定 UI 标签无参数时
直接返回文本，避免每帧构建无用参数集合；没有新增全局文本缓存。

新增测试按变体参数化：五种物种包与四区拾取分别共用夹具；三处 JSON 编辑、两处本地
依赖复制统一使用已有 TestAssets 的两个小助手。保留各测试显式依赖和所有负例。

## 当前边界说明

早期批次的昵称／原作覆盖待办已由下文独立批次接续。当前剩余范围以文末为准，
不将历史待办当作仍未实现。训练师编码姓名、邮件作者与词ID属于真实规则／保存身份，
不能通过显示覆盖改写；中文字体、图片字、排版和真实IME仍需独立工作。

## 作者 UI 的部分覆盖

设置 `FIRERED_TEXT_PACK` 为一个本地目录即可替换已收敛的作者 UI。目录中的 `save.json`、
`field-menu.json`、`options.json` 使用同组英文语义 ID，例如 options.json 可只包含
`{"title":"SETTINGS"}`。未提供组或条目回落内嵌英文；无需复制整份默认文件。
未设置环境变量时只用默认英文；显式目录不存在、未知组／ID、空文本、重复 JSON 键、
具名参数缺少或增加均明确报错。允许参数重排或重复，但插入值不会再次当控制码解析。

View 在启动时加载一次，将实例传给存档提示、队伍动作和选项表现；无需重新编译即可
使用本地替换包。覆盖只影响显示，没有存档／动作 ID 改动。顶层使用作者UI模板语法；下文的source子目录保留原作控制语法和既有名称ID。

部分覆盖验证：Content 17 项、Presentation 27 项通过；Content／Presentation／Godot 编译
零警告零错误。真实云窗口用仅五个条目的本地包显示 SETTINGS、DETAILS、NORMAL，未覆盖
标签仍为英文；DETAILS 继续打开真实摘要，Options 返回正常。两张截图人工检查无截断。
本批没有验证中文排版、IME 或真实音频输出。

## 名称与物品菜单的共享边界

`SourceDisplayNames` 只增加实际类型／特性名称和持有物为空时的源标签；物品名称直接
引用已有 `SharedItemCatalog`。没有第二份物品目录。Summary 导入器读取原作名称表，
保留 FIGHT、ELECTR、PSYCHC、COMPOUNDEYES、LIGHTNINGROD 等真实源拼写。
战斗视图／消息和摘要按规则 ID 查显示名；名称本身不再决定资源或动作身份。
旧区域持有物名称白名单曾使合法 CHOICE_BAND 拍落事件无法命名，现由同一目录承接。
该修复不改变事件生产、伤害或道具准入；具体拍落云窗口尚未抽样。

Bill／Sevii／Route 1 的显示英文门槛改为实际成员 ID、数值和非空文本验证。八个区域
图鉴素材只保留物种 ID、高度、重量；名称／分类／说明读取全图鉴。删除 63 个重复生成
字段，共 3,314 字节，126 张 PNG 字节不变。原始训练师编码姓名与个体生成规则未改。

Bag、个人物品 PC、携带道具和关键物品菜单用 `InventoryMenuAction` 分派；显示标签可
相同也不会让 GIVE/TOSS/CANCEL 混淆。复用 item-ui 和 story 源文本，九条缺失源符号由
既有 story recipe 引入；没有复制源英文进作者目录。Toss 状态只保留事务数据，显示层
按已有阶段读取源提示；数量、来源、取消目的与交易命令不变。18 条作者反馈归入
`inventory.json`，接入同一部分覆盖机制。源占位符只替换一次，插入名字不再解释成控制码。

早期七个捕获页也已统一投影全图鉴名称、分类、说明及格式化测量文本，并复用启动时
已有的图鉴／森林资源实例。原捕获布局和艺术引用不变，仍验证物种、图鉴编号、高度、
重量和图片绑定；旧局部文本不能覆盖全图鉴。该小片没有重做两个历史捕获资源的导入
结构，因此它们仍存有未消费的重复文字，不能把本轮描述为全部 content 去重完成。

这一整合批的定向自动验证：Content 179 项（14 秒）、Presentation 80 项（1 秒）、
Application 70 项（17 秒）通过；四项目编译零警告零错误。Summary 导入 11 项与共享
区域像素导入 13 项通过；story 重生和 `--check` 通过。测试复用参数化区域／字段矩阵，
没有新增生成 oracle 或逐帧快照。原生窗口结果单独记录，不由这些测试替代。

该批真实云窗口（2026-10-19，固定 DLL）确认 ANTIDOTE 的 USE/GIVE/TOSS/CANCEL，
CANCEL 和 GIVE 后返回保持原 Bag 选择；队伍 ITEM 的 TAKE 空持有物提示及取消正常。
摘要实际显示 NONE、PRESSURE 和说明；两张截图人工检查无截断。公开操作前后世界、
RNG、队伍和背包不变，仅 PlayTime 正常推进，原检查点文件未改，正常退出且无运行错误。
最初在无 X11 的 shell 启动失败，随后通过自身云桌面终端成功启动；未将该技术失败算作通过。
没有抽验实际非空持有物转移或 CHOICE_BAND 拍落；Dummy 音频、中文及 IME 不在本次验收内。

PC 的 29 条作者通知和桌面操作提示归入 `storage.json`，源 MOVE/CANCEL/YES/NO 继续
读取现有 TextBank。移动框底部帮助过去按可见 EMPTY/CANCEL 字样选择分支；现在按真实
队伍取消行、是否持有和格子占用状态选择，标签相同或改写不会改变帮助的含义。
纯展示 helper 只有三个结果，没有新增动作状态机；现有 PC 命令、保存、群组所有权不变。

该片定向验证：文本装载 17 项、库存／PC 展示 52 项、既有矩形移动及公开 PC 往返／
严格存读档 5 项全部通过；Content、Presentation、Godot 编译零警告零错误。
公开 PC 用例使用现有受控合法检查点，不是完整自然路线重放。该片未新增原生 PC
窗口抽验，原生 Bag／摘要验收不能替代 PC 画面覆盖。


摘要／战斗换人／日托弹窗共享源文本与稳定动作。旧 SUMMARY/CANCEL 字样不再决定
换人或寄存；五个动作 ID 显式分派。已有 battle text 中的 17 个源符号供弹窗、标题、
EXP/NEXT LV、EGG 和控制标签使用，PP 复用已有战斗标签。只解码实际消费的 A_BUTTON
及 DPAD_UPDOWN 前缀，未知控制仍报错，不按英文单词截取。摘要的 DETAILS/BACK 是
当前桌面布局的作者标签，与两条选择反馈一起归入四项 `summary.json`。

邮件复用 inventory 的目标变化、持有物标题及 canonical 物品名称，YES/NO 复用现有
源符号。两个邮件错误加到 inventory，个人档案编辑失败归入一项 `easy-chat.json`；
没有复制邮件作者、保存词组或源正文，也没有新增只为测试而用的 formatter。

摘要／邮件批验证：Content 27 项、Presentation 95 项、Application 27 项通过，包含
现有日托公开路由、战斗摘要／学习权限以及 Mail／profile 事务；三项目编译零警告错误。
真实云窗口使用仅两条覆盖的 summary 包，KNOWN MOVES 页清楚显示 INSPECT／RETURN，
其余源标题、招式、PP 回落英文；进入 Details 后选第一槽、移到第二槽，再取消草稿，
招式顺序及 PP 不变，显示恢复 SWITCH/CANCEL。两张截图人工检查通过，正常退出 0，
世界／RNG／队伍／背包及原检查点不变，仅 PlayTime 推进；无保存、无实音或 IME 验收。

性格显示现在从固定源 `gNatureNamePointers` 的数字身份和实际字符串导入既有
`display-names.json`，不再由 C# enum 拼写生成英文。Summary memo 使用一次模板绑定，
名称中恰好出现 `{DYNAMIC 0x01}` 或 `{LV_2}` 时保留字面值，不会再次展开；未知地点的
“a trade”也来自已有源文本。此片没有改变昵称、性格规则或存档。

验证：Summary 导入 13 项、Content 名称／memo 48 项通过；Content 测试项目和 Godot
串行编译零警告错误。生成差异仅为性格目录与 memo 的 ATrade，所有既有 PNG 不变。
英文行为由源文本和 memo 分支测试核对；本片未新增原生窗口、实音或 IME 验收。


物种显示与玩家昵称使用不同语义：PartyPokemon 只保存必需但可为 null 的 CustomNickname。
null 从现有全图鉴目录读取当前物种名；任何非空白命名提交都保存字面自定义名，即使恰好
等于英文物种名。空白提交保留原 null／自定义状态。进化不再按英文相等猜测默认昵称，
蛋由 IsEgg 显示源 EGG。战斗、PC、摘要、命名、培养服务及事件文字共用同一解析入口。
CampaignPokemonContent 不再复制物种显示名称，也不把显示名长度／字母表作为物种准入。
训练师个体生成用的 EncodedSpeciesName／EncodedTrainerName 保留原规则语义。

HOF 记录物种身份和 nullable 自定义名；删除无消费用途的重复十字节昵称编码及其自身
重编码校验。历史成员的物种、个体、等级和自定义名仍是独立快照，当前队伍改名不会
改写档案；默认名称可随同一目录显示。

SaveCodec schema2 明确拒绝 schema1。新字段必须显式存在，不能忽略旧 Nickname 后
静默变成默认。旧玩家档没有默认／自定义意图，正常加载不猜测；即使有真实孵蛋总数，
原有孵蛋或 Tower 规则转换命令也不能跨这个 schema 边界。

维护测试档一次性在工程外转换到新的本地忽略文件，保留旧原件：所有旧非蛋名字明确
作为 custom 保留，蛋按原系统规则为 null，合成孵蛋总数1000继续只属于受控测试输入。
这保留旧显示并不恢复历史昵称意图。普通测试现在仅严格加载当前档，不再执行旧昵称、
Tower 或孵蛋修补；迁移脚本和旧编码逻辑不进入产品或常规测试。真正默认名、同英文
自定义名、进化及空白取消由新构造的语义用例验证。

昵称批验证：Domain 33、Battle 46、Content 81、Application 127 与独立 Name Rater
公开事务 4、Presentation 68 项通过；Summary 导入 14 项通过。相关项目串行编译零警告
错误。Name Rater 同一参数用例核对 null、同英文 literal、自定义 literal 的空白保留，
以及显式输入默认名后保存 custom 但源脚本仍走 SameName；队伍其他字段、存储和 RNG 不变。

真实云窗口核对默认 MEWTWO 与自定义 EMBER，后者摘要仍在物种栏显示 CHARIZARD；
Moves、DETAILS 草稿取消和返回完成，截图人工检查。正常退出 0，五个运行 DLL、两份
检查点及覆盖包未变；初末玩法状态仅 PlayTime 推进，无保存。两项覆盖 INSPECT／RETURN
仍有效。此验收使用明确指定昵称意图的受控副本，不证明旧档可自动迁移；实音与真实 IME
仍未验收。

## 原作文本与名称的部分覆盖

`FIRERED_TEXT_PACK` 顶层仍是七组作者UI（132条英文、6,944字节）；`source/` 下是
已有生成目录的可选覆盖，没有复制整份英文默认目录。未设置包使用英文，缺少组／条目
回落原生成值；显式目录不存在、未知组／ID及空值报错。运行时一次选择并读取不可变包，
更换语言需重新启动；无热重载、版本锁、来源hash或第二份规则目录。

分组及实际身份：

- `story`：Story.TextBank源symbol；`story-menus`：编译菜单Options.TextSymbol。
  相同符号的各菜单行一起替换，选项ID／位置／脚本不变。
- `battle`：原作symbol，贯通事件分页、战斗选择菜单及摘要源UI标签。
- `species-names`、`species-categories`、`species-descriptions`：既有SourceSpeciesId十进制；
  `pokedex-ui`：原作UI symbol。默认昵称跟随物种名，自定义昵称保持字面值。
- `item-names`与`move-names`：现有`pret:ITEM_*`／`pret:MOVE_*`；`type-names`与
  `ability-names`：源`TYPE_*`／`ABILITY_*`；`nature-names`：既有Nature数值ID；
  `display-labels`仅现有EggName／NoHeldItem。
- `item-descriptions`：共享物品目录全部307个实际ID；`machine-descriptions`：现有机器及TM CASE共58项。
  均使用物品ID。描述覆盖不扩展道具准入或可教学机器；机器UI仍使用其独立源说明语义。
- `summary-moves`／`summary-abilities`：源MOVE_*／ABILITY_*；`summary-memos`与
  `summary-locations`：当前摘要目录键。
- `trainer-names`：既有TrainerId；`trainer-class-names`：源TRAINER_CLASS_*。原始编码、
  业务定义和素材校验不改。saved RivalName在实际对手显示中优先于目录覆盖。
- `easy-chat-words`：1006个非物种／招式词的既有ushort十进制ID；807个名称词复用当前
  物种／招式目录，不能在这里重复覆盖。`easy-chat-groups`使用EC_GROUP_*及alphabet:数值ID，
  只替换22个类别和27个字母桶标题，词值、解锁和源排序不变。

示例：`source/species-names.json`只需`{"150":"MIND CAT"}`，
`source/battle.json`可只包含`{"gText_MenuPokemon":"CREATURES"}`（键必须真实存在）。
任何规则、Encoded字节、价格／效果、存档身份均不在覆盖选择范围。

剧情、菜单、战斗、摘要备忘及图鉴UI保留原控制token、操作数、顺序、分页和控制相邻／
结尾锚点；实际换行和源转义不混淆，`$`必须处于原终端位置。首版保守保持占位符顺序，
不猜测控制码重排是否安全。普通剧情先处理原模板控制再单次绑定名字／buffer；战斗模板
也只扫描一次，插入值不会成为新的控制或另一个参数。

本批同步解除FossilLab、Four／Five／Six Island拾取及区域图鉴组合的显示等值门槛，
改查现有ID／数值／规则；捕获图鉴共享canonical显示投影，保留区域素材路径。点字窗口
使用当前剧情文字，素材只检查源label及已有glyph，缺字明确拒绝。Quest袋
读取canonical物品名称，Teachy TV／Secret Key／事件票据不会因名称替换而拒绝加载。

### 仍未接入的有限文本边界

- 本批已接Trainer 604记录／48个使用中阶级、Card八字段、Mail实际35个源symbol及
  编辑器动作行、EasyChat词和分组、糖果六能力标签与进化取消提示。源阶级表保留其107个
  实际声明供动态设施阶级复用，未建立历史数量准入。
- 仍待独立闭合的专用入口包括Fame Checker事实／标签、Quest Log模板／地名、Tower动态
  个人名及其设施speech词显示。它们不是字体问题，也不因Story／Battle已覆盖而自动覆盖。
- 图鉴栖息地标题、测量显示、早期素材内无人消费的重复显示字段尚未继续迁移。
- 目录排序仍使用既有源索引，未提供按目标语言重新排序。SourceGameFont不自动提供
  缺失字形。实际中文翻译、字体／图片、布局、真实IME均未做。

Game Corner 已改用现有 SourceMenuLabelPresentation，取消重复解析器及 NO THANKS
英文门槛；取消行为始终由行号／127决定。未增加每地图规则或另一文本缓存。

新增测试已使用参数行、共用 TestAssets 临时输出与 JSON 编辑入口。Domain 昵称规则、
实际进化／孵蛋规则、Content 目录投影及 Application 事务分别验证不同边界，不因文件
数多而合并成一个巨型夹具；今后删除无消费者名称图时应同步删除对应过时名称测试。
实际中文翻译、字体排版和真实 IME 仍未实施，Dummy 音频也不代表实音验收。

单遍战斗绑定／奖品菜单收尾验证：Content 10、Presentation 45 项通过，涵盖字面参数
包含另一参数名及反斜杠、原分页滚动、三个等待控制与六种错误输入；既有通用价格标签
测试验证替换取消标签及不支持控制拒绝。Content、Presentation、Godot 编译零警告错误。
这两个小改动未再启动原生窗口；前述昵称窗口验收对应其自身冻结 DLL。

无消费者名称清理验证：三个重生文件除删除名称字段外逐字段完全相同。
Route1 7,645→2,753字节，OldMan 5,598→4,448字节，Story 3,372,213→3,360,097字节，
合计减少18,158字节（生成资源仍本地忽略）。Story 仍为4,237个程序及4,293个文本符号。
两项现有导入回归（包含原有多产物子用例）、story重生校验、Content 16 与 Battle 12
项通过；导入工具及三个相关项目编译零警告错误。一次隔离fixture同时替换三个不同阶段
招式名，当前组合直接跟随canonical，玩法定义和story字节不变。删除105行旧本地名称
测试，引用与进化负例并入既有GeneratedLearnsetTests；未新增测试目录或生成数据。

原作覆盖批验证：Content 128、Application 29、Presentation 101项通过；相关项目串行
编译零警告／错误。包括真实Story／Battle／编译菜单部分替换、未知ID和控制边界负例、
名称与规则分离、三岛拾取、捕获图鉴投影、字面插值及点字label／glyph约束。
普通剧情新增单遍绑定，点字素材不再拥有第二份显示文字。未重生或提交原始／生成资源。

单次真实云X11窗口显示MINDSTAR默认名、EMBER自定义名及FLAREWING物种栏、SOFT性格，
SOURCE三页标题／ONWARD、作者INSPECT／RETURN及四行Psychic说明，所抽样内容无裁切。
附加item／story／story-menu包经过实际启动组装，未声称逐项画面验收。正常退出0，
五个冻结DLL、检查点和包前后未变；玩法状态仅PlayTime推进，无保存。保留V-Sync和
crossed_circle光标诊断。窗口发生于后续点字窄修之前；点字仅定向自动验证，非原生验证。
音频Dummy，未验中文、IME、实际战斗文本或完整剧情。证据保存在工作区外部验收目录，
截图和状态副本不进入Git。


最终页绑定修复：单打、双打和Tower最终页只做两行滚动分页，不再删除控制样式的字面值。
源结果页在Application生产边界经CampaignSourceText先准备原控制、再绑定名字／buffer；
普通剧情使用同一入口，音频仍读取原Dialogue.Pages。初战、Itemfinder、VsSeeker、袋中提示、
PC提示与旧ReferenceWorld显示复用单遍绑定，VsSeeker的源分页展开也先于名字插入。
保留未知token可诊断，不扩大合法昵称字符限制。Application49、Presentation91项通过，
相关三项目编译零警告错误；本窄修没有新增原生窗口验收。

养成与特殊菜单共用当前catalog的一份battle文本bank；SourceBattleText分页器从Content移到
Domain，原有Godot、Application和测试使用同一实现，Content只负责装载及覆盖。进化、
糖果和奖励学习结果使用既有源symbol，介绍文字恢复源换行“What?\\n{name} is evolving!”。
第一轮审计曾把两处Added续学页误称为手动换招页；实际Replaced事件原先被过滤且没有结果页，
现由同一成功模板生成结果页，学习决定与PP/招式规则不变。8处Godot已按Reward重绘的旧文本
不计作本批新增可见覆盖。

船票／化石菜单的13个唯一标签使用既有源symbol（取消为gOtherText_Exit）。事件岛访问
校验使用目的地9/10与真实票据／旗标，不再依赖显示字符串，重名覆盖不改变选项身份。

本批验证：Application 67项／22秒，Content 36项／457毫秒，Presentation 74项／252毫秒，
无跳过；四个相关项目编译零警告错误。已有公开进化、糖果、野战及训练家学招用例增加
英文／替换包参数化，真实Replaced回执对应成功页；菜单19行参数化覆盖全部已接入上下文、
重名标签和票据负例。未再做原生画面或实音抽验；进化介绍源换行由源文本及分页断言验证。

共享物品说明收尾：items.json schema2在既有307个物品定义旁保存一份按物品ID索引的显示说明。
使用现有源提取器，307个业务定义及原179项普通说明保持逐字节一致；新增32,016字节
本地生成内容，不入Git。CurrentItemCatalog不再对子集重复应用覆盖。机器目录保留
TM36／HM06／HM07原有不同说明选择，TM CASE显示由机器目录负责。

所有区域口袋行装配完成后统一投影名称／说明，覆盖字段袋、单双打球袋、教学袋和存储
回退；不逐区域添加语言表。Old Man的Potion及PC默认选中名改读共享物品目录。说明已是
最终显示值，普通袋与存储不再二次展开反斜杠或裁去末尾美元符号。Tea校验保留实际物品
身份、口袋、权限、用途和像素布局，删除与固定英文副本的等值条件。

本批121项受影响Content测试通过（4秒），Content与Godot编译零警告／错误；源导入
8项、普通物品10项、调用者6项、输入闭包7项检查通过。实际稀疏替换包启动发现
Potion／Super Potion仍有两个英文身份门槛，现保留ID、效果和权限校验而允许替换名称；
两行参数化测试保留错误身份／效果／空名负例。修后完整headless组合到达ready并退出0，
无画面／实音结论。该探针使用--quit-after强制结束，绕过正常RequestQuit音频退休流程，
所以退出的AudioStreamGeneratorPlayback引用诊断属于既有强退验收边界；不是新发现的
普通退出泄漏。此前正常WM退出已验证无该警告，详见plain-field-transition-presentation.md。

有限显示身份复查发现Tower的脚本Marowak选择器还同时检查英文名称；现只按物种105
认证，替换名称与gText_Ghost仅影响显示。商店说明直接使用已投影值，不再次展开或裁剪。
Fame Checker短标签复用其原有单遍控制解码器，删除另一串联替换入口；插入的名字是字面
文本，原始分页、控制字符和Unicode合法性检查仍保留。该修复不等于专用Fame Checker
事实目录已经接入语言覆盖。

Trainer显示使用604个既有TrainerId和源阶级ID，保留原训练家定义、编码及区域素材绑定。
导入107条实际阶级声明（当前定义使用48类），删除普通／双打重复阶级分派和废弃区域
显示缓存；源SWIMMER♂／♀恢复性别符号。玩家RivalName仍优先。Mail的35个实际UI
symbol和编辑器动作行复用battle bank，名字绑定单遍；EasyChat的1006普通词、22类别和
27字母桶标题稀疏覆盖，807名称词共享现有名称目录。Card八字段、六能力标签和进化提示
也复用源symbol，三个重制辅助提示进入已有作者组；没有新增英文默认词表。

最后投影批验证：Content117项／2秒、Application18项／8秒、Presentation22项／244毫秒；
四个相关项目编译零警告错误，源阶级导入3项与producer --check通过。新目录4,126字节
仅本地生成，所有604训练家／1549成员定义字节保持。带稀疏替换包的headless完整组装到ready，
正常WM close经RequestQuit退出0，verbose无错误、警告或泄漏。此为实际引擎组合／退出验证，
不是新原生画面、实音或中文验收。

### 专用目录与公共 UI 合并收口

本轮按真实消费者接线，而非新增一份英文总表。Help 的 368 个引用 ID、Opening 的 24 个
文本及六个建议名、Teachy TV 的 42 个文本、Quest Log 的 125 个模板／414 个地图 ID／
离场标签使用各自现有身份。地图三种视图共用 Fly 目录的 83 个 MAPSEC 身份（81 个不同
英文值），同名区域仍保留不同 ID。新增 source 分组为 help、opening、opening-names、
teachy-tv、quest-log、quest-log-map-names、quest-log-departures、map-section-names、
trainer-tower-names；没有复制默认英文文件。

其他目录直接复用已有权威：Fame Checker 的 342 个符号分别来自 battle（10）、story
（320）和明确 TrainerId（12）；常规入口、四种命名标题、Old Man、壁纸、教学服务、
NPC 交换提示、奖品、Hall PC、Diploma 和片尾字幕均复用 battle。导入器保留原先丢失的
文字 symbol，移除不再消费的壁纸／奖品／片尾英文副本。Tower 的 53 个预设人名按楼层
符号／训练师索引投影，speech 按原词 ID 使用同一个 Easy Chat 名称／普通词目录。

公共菜单、蛋摘要、Safari、电梯、图鉴导航、命名辅助提示、地图提示和战斗辅助页接入
源标签或现有作者组。物品皮肤与 Quest 口袋在统一组合点接收同一 bank；物品参数和
Quest 回顾参数单遍绑定，玩家值里的花括号、反斜杠和美元符号不再被二次解释。
已无调用的旧蛋摘要和重复开场绘制分支移除。源本来为 sharply rose!、Safari throw?、
No use. 等的少量提示恢复源措辞，选择身份、交易和战斗结果不变。

覆盖包仍只接受已存在 ID，缺项回落英文，未知 ID／控制不匹配报错。Opening 的实际
form-feed 分页只对 opening 分组允许且必须保留位置语义；其他组仍拒绝该控制。
现有源占位符顺序、控制指令及空段锚点保持。这是安全的渐进覆盖边界，尚不支持任意
重排原作控制骨架。作者模板仍支持具名参数重排与字面插入。

明确保留的身份与未来工作：
- 九组 NPC 交换固定昵称已作为 CustomNickname 保存，OT 属于原始身份／所有权校验，
  DONTAE 邮件另含编码作者。它们不是普通文本覆盖入口；后续若需本地化这些预设，须
  明确创建时身份语义，不能依据已保存英文名猜测。所有自定义玩家／宝可梦名字原样显示。
- 箱名 null 表示默认 BOXn，但当前重命名输入等于默认名也会折叠为 null；本批不猜测
  历史输入意图，默认箱名仍为现有规则。旧 Quest 回顾已记录的参数字符串保留原值。
- 开场建议名仍受现有七字符／字符表输入约束。中文翻译、输入字符集、字体、排版、
  IME、排序和图片内文字需实际目标语言验收。没有把这些边界标成已通过。
- 原始规则编码、脚本身份、诊断错误码和开发状态不是显示翻译目录。玩家可见的错误
  提示则使用现有作者组，原有文字保持；正常 RequestQuit 与强制退出诊断继续区分。

本批验证：Content 152、Presentation 233、Application 23 项定向测试全部通过；四个
项目串行构建零警告／错误。Opening 的维护导入检查、Storage／Prize 的固定输入重建
及五个服务包检查通过。Prize 完整重建在外部目录验证，运行目录只更新文字 manifest，
未用旧独立图标输出覆盖现有共享图集。正常 headless WM-close 启动／退出通过。
一次自有云窗口抽样确认 Help 英文回落、返回现场、作者覆盖 SATCHEL 和 source 覆盖
TOOLS 的实际 Bag 绘制；本批未逐屏原生复验全部目录，Dummy 音频不代表试听。
