# 图鉴叫声与原版排序

## 原作范围纠正

固定来源：pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`，本地 `src/pokedex_screen.c`、`src/strings.c`。

火红没有独立的叫声波形页面。`Task_DexScreen_ShowMonPage` 打开资料页、上下换到另一个已见物种时调用 `PlayCry_NormalNoDucking`；`DexScreen_InputHandler_StartToCry` 在 START 按下时重播同一普通叫声。资料页和分布页都有 CRY 提示。此前“叫声页面”待办是范围描述错误，本切片补资料页真实叫声入口，不制造第三页或波形。

## 玩家入口

- 数值列表仍从开始菜单的 POKéDEX 进入。
- 打开已见物种的资料页会自动播放该物种叫声；上下切换跳过未见物种，并播放新物种。
- Tab／手柄 Start 重播；鼠标点击 `Tab CRY` 同样可用。释放按键、键盘重复事件及双击不会重复触发。
- 返回列表、重置菜单只取消本图鉴播放令牌对应的叫声，不停止后续其他来源的叫声、音乐或音效。
- 未捕获但已见的物种可播放；描述、身高体重、分类和脚印仍按原 caught 门禁显示。未见行不可打开或播放。

图鉴从现有共享物种目录取得内部 SourceSpeciesId，经共享音频 cue 目录选择 normal mode 0；不会拿全国编号当音频 ID。Godot 音频宿主的 `PlayCry` 新增可选 `duckBgm`，图鉴用 false，既有调用默认仍为 true；未复制或更改 `Game.Audio` 合成核心。图鉴叫声不额外压低背景音乐，不代表整个原图鉴全局混音／过渡已还原。

## 搜索排序入口

列表底栏 `R SEARCH` 可由键盘 R 或鼠标进入排序菜单，含：

- 关都编号
- 全国编号，仅真实全国图鉴条件满足时显示
- A TO Z
- TYPE
- LIGHTEST
- SMALLEST

原作 Search 是固定顺序列表，不是自由文字、属性选择器或身高体重区间筛选。A TO Z 包含已见物种，后三种只包含已捕获物种；全国范围由真正的全国图鉴解锁决定，不能因刚浏览关都数值列表而缩小。所有排序沿用 `src/data/pokemon/pokedex_orders.h` 的原表，由 `scripts/imports/presentation/import-pokedex-menu.py` 导入四份 canonical SourceSpeciesId 排列；不会复制名字、属性或度量数据，也不会用通用 OrderBy 替换等重、等高及同属性的源顺序。

取消排序选择会保留原列表与游标；选定新顺序从首行浏览。空结果会显示 `NO ENTRIES`，可继续搜索或返回，不进入不存在的详情。排序详情沿同一结果列表上下移动，返回仍回到该结果列表。浏览不改变 seen/caught、队伍、库存、RNG 或存档。

## 表现与验证边界

仍复用现有列表、资料背景、物种画面及原 small/normal 位图字体。底栏 `Tab CRY`／`R SEARCH` 是当前键鼠提示，没有捏造原 START 图标。排序菜单和原作目录／栖息地层级尚未完全同形；当前排序结果直接进入既有资料页，原 Search→栖息地页→资料页的路径仍缺。分布、栖息地、大小比较不由本切片完成。

维护测试：`tests/Game.Presentation.Tests/PokedexDetailPresentationTests.cs` 检查已见／未捕获门禁、未见与空选择拒绝、Hoenn内部ID和全国范围、上下跳过空洞、底栏可点击范围；`tests/Game.Content.Tests/PokedexSearchOrderTests.cs` 检查原排序表与列表规则。2026-10-16 在合并后程序集重跑上述表现目标，5/5通过；内容侧 `PokedexSearchOrderTests` 与 `GeneratedPokedexCatalogTests` 共17/17通过，均无失败或跳过。

同日隔离 Godot probe 向实际 `CampaignView._UnhandledInput` 投递键盘、手柄 Start 和鼠标事件，并使用真正的 `SynthSourceAudioPlayer`。36项检查通过：自动播放／重播、释放／重复／双击、见过门禁、内部ID277／392／410、取消令牌隔离、BGM不额外压低且既有默认调用仍压低、排序选择与取消、全国选项隐藏、空结果、state／RNG／revision不变。最后一次妙蛙种子叫声检查中，宿主累计生成45056帧PCM、峰值0.268095；确认输出已消费到该叫声结束边界后收到该token的finished事件。工具为 Godot 4.7.2 .NET、SDK 10.0.401、宿主 runtime 10.0.12，显示为headless、音频为Dummy。

这份probe使用受控seen/caught集合，不是从新游戏自然取得物种。首次probe因额外调用图像集合初始化而缺其完整战斗图像前置，尚未进入输入断言即失败；保留失败日志。去掉headless检查不需要的该初始化后重建并通过，未通过改游戏门禁修补测试。初次从命令沙箱另起X服务失败；后续已通过现有云端桌面启动当前工程新游戏窗口。上述36项probe仍是headless证据，不据此声称本切片截图或逐像素验收。

复现本轮命令（工程根目录；probe仅在本地临时工作区，不属于运行资源）：

```sh
source scripts/env.sh
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore \
  --filter FullyQualifiedName~PokedexDetailPresentationTests \
  --logger 'trx;LogFileName=pokedex-cry-presentation-integrated.trx' \
  --results-directory .toolhome/test-results
dotnet build /tmp/firered-pokedex-cry-ui/PokedexProbe.csproj --no-restore \
  -m:1 -nr:false -p:UseSharedCompilation=false
AUDIT_ROOT="$PWD" FIRERED_AUDIO_BACKEND=synth FIRERED_AUDIO=1 "$GODOT" \
  --headless --path /tmp/firered-pokedex-cry-ui --audio-driver Dummy
```

本地检查日志：`/tmp/firered-pokedex-cry-ui/run.log`；首次夹具失败：`run-attempt1-setup-failure.log`。临时probe引用已构建的产品DLL，不替代仓库内维护测试。

Dummy输出、非零PCM、播放事件和结束只能证明程序生成并消费了合成数据，不能证明真实扬声器、听感、音质或全场景同步。P0实际音频输出门禁仍未通过。
