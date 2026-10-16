# 图鉴栖息地、分布与大小

来源仍为 pret/pokefirered 固定提交 `037335f4c725d7c9aecdac87066f2002b4bd7e14`。本轮扩展同一个 `pokedex-menu.json`（schema 4）及 `PretPokedexMenu`，不新增平行物种目录，不改 seen/caught、全国图鉴条件、库存、RNG 或存档。

## 栖息地路径

开始菜单 POKéDEX 现在先到目录。九个类别按 `src/data/pokemon/pokedex_categories.h` 的顺序；未解锁类别为横线，不可打开。每页最多四种，只保留已见且当前图鉴范围允许的物种；全部未见的页跳过。原始页组索引与显示页码分开保留，不把不同源页合并。左右在页内选择，到边缘翻到前/后已解锁页；不循环。Enter 打开资料，Backspace 回原页与游标。

目录末项 SEARCH / NUMERICAL 打开原来的六种排序（全国编号仍受真实条件限制）。数值列表直接进入资料；四种固定搜索结果先进入对应栖息地页，再进入资料。由搜索进入栖息地时，退出恢复原排序、原结果列表与原游标。搜索取消也保留原状态。

资料 Enter 进入分布/大小；分布 Backspace 回资料，Enter 回原数值列表或栖息地页。两种页面的 Tab/Start 均能播放原普通叫声，不增加独立波形页。鼠标点击资料正文进入分布，分布底栏提供 CRY / EXIT / DATA。分类资料页不使用数值列表上下换种语义。

## 大小

四个字段 `pokemonScale`、`pokemonOffset`、`trainerScale`、`trainerOffset` 逐项来自 `pokedex_entries.h`，不用身高反推。比例是 GBA OAM 的反向 8.8 仿射系数，显示倍率为 `256 / coefficient`；按 NORMAL 仿射的 64×64 sprite box 裁切。中心分别为 (40,104) 与 (80,104)，加各自有符号 y 偏移。只在已捕获时显示两幅轮廓，未捕获连训练家轮廓也不显示。轮廓颜色来自原 `silhouette_sprite_pal.pal`，所有非透明色为 (82,82,82)。训练家正面沿用已加载的玩家性别资源。

栖息地每页 1–4 种的画像/信息坐标逐项导入 `sPageIconCoords_*`，画像使用现有 64×64 正面资源。`visual-atlas.png` 在同一菜单资源内保存 Kanto／National 两套背景与资料边框、原 `page_icon_tilemap.bin` 圆形窗口、五组圆形游标调色板、64×40 mini-page 信息框和原 8×8 捕获标记。类别图标直接由固定源 PNG 和其调色板转换；目录右侧使用原 (168,88) 坐标。分布页复用已有物种 icon（Unown 使用已记录形态），不缩小战斗正面画像冒充小图标。18 种属性徽章按 `sMenuInfoIcons` 的原 GBA tile-memory 偏移、32×12 尺寸与 `pokemon_types.pal` 导入；只在已捕获时显示。单属性投影的第二属性为 null，不会查询不存在的徽章。

A／B／START／方向键 glyph 来自 `sKeypadIcons` 与 `keypad_icons.png`，独立裁切，不重新设计字体。键盘映射仍是 Enter／Backspace／Tab／方向键。当前既有数值列表／搜索排序布局及部分文案保留；此工作不声称整个目录与原作滚动列表逐像素相同。

翻页 shader 逐列使用原 `sDexScreenPageTurnColumns[10][30]`，不做平滑水平缩放代替原 tile-column 变换。右翻先收缩旧页，再换源页并淡入；左翻先淡出旧页，再准备前页展开，转场期间拦截浏览输入。资料展开／收起用原 0–6 级 tile-space 边框几何，末级固定为 (0,2,28,14)，只使用表现时钟，不改变 state／revision／RNG。淡出／淡入使用原 palette[7]、5-bit 整数混色，以及 `palette.c` 的 delta=2、BG/OBJ交替与结束等待所导出的23帧斜坡；每侧表现阶段合计33帧。原状态机的空白帧／零幅度淡变等待未全部逐帧复演，GPU shader 在实际窗口上的渲染、完整原作节奏与视觉验收仍须单独检查，不能以资源像素一致或 headless 检查代替。

## 分布与共享区域图集

分布直接导入 `src/wild_pokemon_area.c` 的 MAPSEC→DEX_AREA 关系、FireRed 的野生表、各地图实际 `region_map_section`，不从当前战斗、已捕获记录或本地阶段名单猜测。陆地、水面、钓鱼与碎岩合并为可显示物种；赠送、化石及静态传说战斗不会追加虚构的野生位置。重复楼层落在同一标记时仅绘制一次，与原 OBJ-window 的遮罩并集效果一致。已见未捕获也可查看分布；未见或不在当前图鉴范围的物种不能通过此入口打开。

七岛分别读 `FLAG_WORLD_MAP_ONE_ISLAND` 至 `FLAG_WORLD_MAP_SEVEN_ISLAND`，全国图鉴本身不解锁岛屿地图。只有第一至第三岛时，地图整体向下移 32 像素；第四至第七岛任一实际世界地图旗标开启时回到顶端。Altering Cave 只使用 `VAR_ALTERING_CAVE_WILD_SET` 选择的一张源表，0–8 分别为超音蝠、咩利羊、榛果球、戴鲁比、熊宝宝、长尾怪手、壶壶、惊角鹿、图图犬；大于等于9时按源行为回到0，不同时显示九种。

炎帝／雷公／水君先核对源初始伙伴变量。活动位置来自已有 `CampaignSession.State.Roamer` 及 `CelioRoamerMaps`／`MapDefinition` 权威映射；未初始化、被捕获或击败后的 inactive 状态均无标记。读取不推进移动、抽样、RNG 或任何命令。图鉴页在进入分布时构造一次只读投影并缓存；每帧只画已选区域和唯一标记，不重扫物种集合或世界地图。

原八幅区域图、79个区域坐标和七种椭圆遮罩均来自固定源文件。遮罩按原 OBJ-window 合成：前景系数12/16、背景8/16，逐通道以5-bit计算并饱和，不以普通75%透明度近似。`AREA UNKNOWN` 的原88×16椭圆按原灰度4bpp反色规则转换。88幅已解析图像打包为单张256×144 RGBA无损图集 `area-atlas.png`，3105字节；各窗口和标记使用不可变裁切坐标。旧88张独立PNG经完整RGBA一致性检查后移除。地图及遮罩的像素核对不等于整个页面转场、字体、边框与控制图标的原生逐像素验收。

钓鱼采用明确的数组边界修正（见[原作行为改进](original-behavior-improvements.md)）：固定源 `IsSpeciesOnMap` 未定义 BUGFIX 时，把10项钓鱼数组按 `LAND_WILD_COUNT=12` 读取；本项目严格读取 `FISH_WILD_COUNT=10`，超长或短缺源表直接拒绝。本项目不读取越界的第11、12项，也不把无法从钓鱼数组定义取得的内存值导入为分布标记。

## 验证

`PokedexHabitatTests` 覆盖九类顺序、386种唯一分区、首源页种类顺序、未见页跳过、同页全国门禁、非法 seen/caught 拒绝、捕获大小门禁、原始有符号变换及单/四种页面坐标。Python 导入器已验证重复转换输出相同且与 generated 一致。最终串行全解决方案构建通过，0 warnings／0 errors；当前 `FullyQualifiedName~Pokedex` 内容筛选30/30通过，无失败或跳过。

分布维护测试 `PokedexAreaTests` 包含8项：源坐标/图集边界、已见未捕获门禁、重复楼层、赠送/化石/静态物种未知区域、逐岛门禁和上下偏移、Altering Cave选择与回退、初始伙伴/活动漫游位置、钓鱼来源与恶意裁切拒绝。这8项已包含在上述30项通过结果内。

2026-10-16，`python3 scripts/checks/test-pokedex-area-import.py` 5/5通过：八幅地图完整RGBA、全部79个标记遮罩及5-bit混色、原未知区域椭圆、单文件图集可重复生成、FireRed/LeafGreen隔离、九张Altering Cave源表，以及钓鱼第11项/12项尺寸变异拒绝。独立打包前后88幅完整RGBA比对也通过，包括透明像素内RGB。没有以修改golden追随实现。


同日隔离 Godot probe 以最终产品 DLL 执行27项输入/绘制检查，全部通过且日志无 ERROR／Exception／WARNING：目录→栖息地→资料→分布、资料正文和分布底栏的真实鼠标入口、Tab叫声令牌、未捕获大小隐藏、已捕获变换、数值/搜索返回位置、全国门禁、类别与页边界，以及 state／revision／RNG 不变。使用 Godot 4.7.2 .NET、runtime 10.0.12，product MVID `3bd403b9-1b78-4263-a344-c05e111fbbb9`。显示为 headless，音频为 Dummy，音频进程在本检查中暂停，因此本次不新增PCM或真实声卡输出证据。实际窗口截图与完整原作视觉/转场尚未验收。

复现命令（工程根）：

```sh
source scripts/env.sh
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore \
  --filter 'FullyQualifiedName~Pokedex' \
  --logger 'trx;LogFileName=pokedex-habitat-area-final.trx' --results-directory .toolhome/test-results
python3 scripts/checks/test-pokedex-area-import.py
dotnet build /tmp/firered-pokedex-habitat-ui/PokedexProbe.csproj -m:1 -nr:false -p:UseSharedCompilation=false
AUDIT_ROOT="$PWD" FIRERED_AUDIO_BACKEND=synth FIRERED_AUDIO=1 "$GODOT" \
  --headless --path /tmp/firered-pokedex-habitat-ui --audio-driver Dummy
```

隔离 probe 与日志在 `/tmp/firered-pokedex-habitat-ui/`，不作为运行资源或替代维护测试提交。早先叫声切片的 probe 和失败日志保持在原目录，未覆盖。


## 2026-10-16 原生视觉资源补充

`python3 scripts/checks/test-pokedex-visual-import.py` 4/4 通过：属性徽章 tile-memory 偏移／调色板、两套圆形 tilemap 全像素及 H/V 翻转、原控制 glyph 尺寸与完整像素、翻页列向量和 atlas 可重复生成。此检查通过 `PokedexVisualImportTests` 加入普通资源导入测试项目。既有分布导入检查仍为 5/5。

内容项目 `FullyQualifiedName~Pokedex` 32/32 通过（无失败或跳过），新增 `PokedexVisualTests` 覆盖类别／属性／双调色板图集与全部页面位置的 0–6 级缩放边界。日志 `.toolhome/test-results/pokedex-native-visuals.trx`。本次首次整解构建被同时进行中的其他表现文件编译错误阻断，保留 `/tmp/pokedex-native-build.log`，不把该构建称为成功；随后整合构建／原生窗口结论应按实际验证另行记录。新增 145 个视觉裁切共用一张无损图集，运行时仍只读取 generated。


新隔离 probe `/tmp/firered-pokedex-native-ui/` 使用已复制的整合产品程序集快照（不回编产品）完成 34 项检查，Godot 4.7.2 .NET／runtime 10.0.12，MVID `75b14c6f-5335-414c-8454-ebc32b26e09f`。覆盖实际共享小图标、资料展开、双向源页翻转与输入抑制，加上先前导航／门禁／state/revision/RNG检查。最终 `run-native-final.log` 无 ERROR／Exception／WARNING；首轮通过检查后出现 fixture 跳过 `_Ready` 导致的控制器事件退订错误，原日志保留，修正 fixture 对称安装生命周期事件后复测通过。显示为 headless，音频为 Dummy 且音频进程暂停，peak=0、frames=0；不提供GPU像素或真实音频证据。随后修正shader列偏移的画布缩放公式（使用实际屏幕跨度，而非8个物理像素），此公式仍须实际窗口验证。该34项结果对应淡变扩展前的程序集；23帧淡变扩展的最新构建／运行结果应另行记录，不追溯覆盖此快照。

最终补充：整合解决方案构建已通过（0 warnings／0 errors，`/tmp/firered-presentations-integrated-build.log`）。最新导入检查为 5/5，新增 mini-page 四角及类别图标 padding 的 source index0 全像素透明度检查；这两类是覆盖底层的窗口，不能把 index0 当不透明调色板颜色。最新图集已原子再生成。以重新复制的程序集 MVID `5518e114-c267-4db6-91cb-3a5359964505`、最新图集执行 `/tmp/firered-pokedex-native-ui/run-fade-alpha-final.log`，34项通过，日志无 ERROR／Exception／WARNING。环境仍是 Godot 4.7.2 .NET、runtime10.0.12、headless、Dummy，peak=0、frames=0；原窗口GPU像素和音频听感边界不变。

最终探针命令（工程根；probe 的 Reference 全部指向独立复制目录，不触发产品回编）：

```sh
source scripts/env.sh
dotnet build /tmp/firered-pokedex-native-ui/PokedexProbe.csproj --no-restore \
  -m:1 -nr:false -p:UseSharedCompilation=false
AUDIT_ROOT="$PWD" FIRERED_AUDIO_BACKEND=synth FIRERED_AUDIO=1 "$GODOT" \
  --headless --path /tmp/firered-pokedex-native-ui --audio-driver Dummy
```


原生 Summary 检查发现 Mewtwo 错显 POISON 后修复共享图集：`sMenuInfoIcons`／`sKeypadIcons` 的 offset 是32-byte 4bpp tile序号，不能以 offset×64 索引 PNG 的逐行像素数组。两条导入路径现按 tile 坐标裁切；PSYCHIC 的0x84对应原PNG (32,64)。此前像素检查复用了错误寻址假设，未能发现此错误，不能用此前通过结果为错误图像背书。检查现独立打包4bpp tile-memory，并按固定 `src/blit.c` 的 nibble-address 公式读取，覆盖全部18徽章与13控制 glyph，并另设 PSYCHIC 的明确图像坐标锚点。重新导入后 visual5/5、area5/5通过；放大查看 PSYCHIC 实际字样正确。14:41 的图集替换不改变 C#，运行窗口须重启以清除旧纹理缓存。
