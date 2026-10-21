# 图鉴栖息地、分布与大小

来源仍为 pret/pokefirered 固定提交 `037335f4c725d7c9aecdac87066f2002b4bd7e14`。使用同一个 `pokedex-menu.json`（schema 4）及 `PretPokedexMenu`，不新增平行物种目录，不改 seen/caught、全国图鉴条件、库存、RNG 或存档。

## 栖息地路径

开始菜单 POKéDEX 先到目录。九个类别按 `src/data/pokemon/pokedex_categories.h` 的顺序；未解锁类别为横线，不可打开。每页最多四种，只保留已见且当前图鉴范围允许的物种；全部未见的页跳过。原始页组索引与显示页码分开保留，不把不同源页合并。左右在页内选择，到边缘翻到前/后已解锁页；不循环。Enter 打开资料，Backspace 回原页与游标。

目录末项 SEARCH / NUMERICAL 打开原来的六种排序（全国编号仍受真实条件限制）。数值列表直接进入资料；四种固定搜索结果先进入对应栖息地页，再进入资料。由搜索进入栖息地时，退出恢复原排序、原结果列表与原游标。搜索取消也保留原状态。

资料 Enter 进入分布/大小；分布 Backspace 回资料，Enter 回原数值列表或栖息地页。两种页面的 Tab/Start 均能播放原普通叫声，不增加独立波形页。鼠标点击资料正文进入分布，分布底栏提供 CRY / EXIT / DATA。分类资料页不使用数值列表上下换种语义。

## 大小

四个字段 `pokemonScale`、`pokemonOffset`、`trainerScale`、`trainerOffset` 逐项来自 `pokedex_entries.h`，不用身高反推。比例是 GBA OAM 的反向 8.8 仿射系数，显示倍率为 `256 / coefficient`；按 NORMAL 仿射的 64×64 sprite box 裁切。中心分别为 (40,104) 与 (80,104)，加各自有符号 y 偏移。只在已捕获时显示两幅轮廓，未捕获连训练家轮廓也不显示。轮廓颜色来自原 `silhouette_sprite_pal.pal`，所有非透明色为 (82,82,82)。训练家正面沿用已加载的玩家性别资源。

栖息地每页 1–4 种的画像/信息坐标逐项导入 `sPageIconCoords_*`，画像使用现有 64×64 正面资源。`visual-atlas.png` 在同一菜单资源内保存 Kanto／National 两套背景与资料边框、原 `page_icon_tilemap.bin` 圆形窗口、五组圆形游标调色板、64×40 mini-page 信息框和原 8×8 捕获标记。类别图标直接由固定源 PNG 和其调色板转换；目录右侧使用原 (168,88) 坐标。分布页复用已有物种 icon（Unown 使用已记录形态），不缩小战斗正面画像冒充小图标。18 种属性徽章按 `sMenuInfoIcons` 的原 GBA tile-memory 偏移、32×12 尺寸与 `pokemon_types.pal` 导入；只在已捕获时显示。单属性投影的第二属性为 null，不会查询不存在的徽章。

A／B／START／方向键 glyph 来自 `sKeypadIcons` 与 `keypad_icons.png`，独立裁切，不重新设计字体。键盘映射仍是 Enter／Backspace／Tab／方向键。当前既有数值列表／搜索排序布局及部分文案保留；此工作不声称整个目录与原作滚动列表逐像素相同。

翻页 shader 逐列使用原 `sDexScreenPageTurnColumns[10][30]`，不做平滑水平缩放代替原 tile-column 变换。右翻先收缩旧页，再换源页并淡入；左翻先淡出旧页，再准备前页展开，转场期间拦截浏览输入。资料展开／收起用原 0–6 级 tile-space 边框几何，末级固定为 (0,2,28,14)，只使用表现时钟，不改变 state／revision／RNG。淡出／淡入使用原 palette[7]、5-bit 整数混色，以及 `palette.c` 的 delta=2、BG/OBJ交替与结束等待所导出的23帧斜坡；每侧表现阶段合计33帧。原状态机的空白帧／零幅度淡变等待未全部逐帧复演，受控原生翻页／缩放已单独检查，范围见[验证](../validation.md)；这不等于完整原作节奏或所有页面逐像素验收，也不能以资源像素一致或 headless 检查代替。

## 分布与共享区域图集

分布直接导入 `src/wild_pokemon_area.c` 的 MAPSEC→DEX_AREA 关系、FireRed 的野生表、各地图实际 `region_map_section`，不从当前战斗、已捕获记录或本地阶段名单猜测。陆地、水面、钓鱼与碎岩合并为可显示物种；赠送、化石及静态传说战斗不会追加虚构的野生位置。重复楼层落在同一标记时仅绘制一次，与原 OBJ-window 的遮罩并集效果一致。已见未捕获也可查看分布；未见或不在当前图鉴范围的物种不能通过此入口打开。

七岛分别读 `FLAG_WORLD_MAP_ONE_ISLAND` 至 `FLAG_WORLD_MAP_SEVEN_ISLAND`，全国图鉴本身不解锁岛屿地图。只有第一至第三岛时，地图整体向下移 32 像素；第四至第七岛任一实际世界地图旗标开启时回到顶端。Altering Cave 只使用 `VAR_ALTERING_CAVE_WILD_SET` 选择的一张源表，0–8 分别为超音蝠、咩利羊、榛果球、戴鲁比、熊宝宝、长尾怪手、壶壶、惊角鹿、图图犬；大于等于9时按源行为回到0，不同时显示九种。

炎帝／雷公／水君先核对源初始伙伴变量。活动位置来自已有 `CampaignSession.State.Roamer` 及 `CelioRoamerMaps`／`MapDefinition` 权威映射；未初始化、被捕获或击败后的 inactive 状态均无标记。读取不推进移动、抽样、RNG 或任何命令。图鉴页在进入分布时构造一次只读投影并缓存；每帧只画已选区域和唯一标记，不重扫物种集合或世界地图。

原八幅区域图、79个区域坐标和七种椭圆遮罩均来自固定源文件。遮罩按原 OBJ-window 合成：前景系数12/16、背景8/16，逐通道以5-bit计算并饱和，不以普通75%透明度近似。`AREA UNKNOWN` 的原88×16椭圆按原灰度4bpp反色规则转换。88幅已解析图像打包为单张256×144 RGBA无损图集 `area-atlas.png`；各窗口和标记使用不可变裁切坐标。地图及遮罩的像素核对不等于整个页面转场、字体、边框与控制图标的原生逐像素验收。

钓鱼采用明确的数组边界修正（见[原作行为改进](../original-behavior-improvements.md)）：固定源 `IsSpeciesOnMap` 未定义 BUGFIX 时，把10项钓鱼数组按 `LAND_WILD_COUNT=12` 读取；本项目严格读取 `FISH_WILD_COUNT=10`，超长或短缺源表直接拒绝。本项目不读取越界的第11、12项，也不把无法从钓鱼数组定义取得的内存值导入为分布标记。

## 转场所有权与共享图集约束

翻页和缩放由 process 驱动的 60 Hz 表现时钟推进；Help 暂停时钟，返回后不补算墙钟时间。屏幕复制 `ColorRect` 只在实际图鉴绘制分支可见，不能在 Help 或其他屏幕上继续变换像素。重置／关闭菜单（包括 Tab）立即取消待执行的翻页／缩放回调并隐藏覆盖层。键盘、鼠标和肩键都遵循同一转场锁；HELP、LR、L=A 模式见[输入与 Help](button-mode-help.md)。

缩放框右边保持源 `DexScreen_DexPageZoomEffectFrame` 的 `left + 1 + width` tile 位置。Godot 负宽度只翻转 UV，不移动矩形原点，不能再给右边框添加 8 像素。Shader 列偏移按实际屏幕跨度换算，不把 8 个逻辑像素当成物理像素。mini-page 四角和类别图标 padding 的源 index 0 必须透明，才能露出底层窗口。

`sMenuInfoIcons`／`sKeypadIcons` 的 offset 是 32-byte 4bpp tile 序号，不是 PNG 逐行像素数组的 `offset × 64`。独立检查先打包 4bpp tile-memory，再按 `src/blit.c` 的 nibble-address 公式读取全部属性徽章和控制 glyph；PSYCHIC 的 `0x84` 另固定锚定原 PNG `(32,64)`，防止实现和 oracle 共享错误寻址假设。资源更新后须重启运行窗口，清除已有纹理缓存。

## 再生成与验证

```sh
python3 scripts/imports/presentation/import-pokedex-menu.py
python3 scripts/checks/test-pokedex-area-import.py
python3 scripts/checks/test-pokedex-visual-import.py
```

- `PokedexHabitatTests`：源类别／物种唯一分区、原始页顺序、未见跳页、全国门禁、非法 seen/caught 拒绝、捕获大小与有符号变换。
- `PokedexAreaTests`：源坐标／裁切边界、未知区域、重复楼层、逐岛门禁、Altering Cave、漫游位置和钓鱼边界。
- `PokedexVisualTests`：双调色板图集、类别／属性、全部页面的 0–6 级缩放几何。
- `PokedexTransitionClockTests`：四种转场程序、Help 暂停／恢复、关闭／取消、分块无关逻辑时间和非法输入。
- 两个 Python 检查分别用源像素／tile-memory 验证地图、遮罩、5-bit 混色、未知区域反色、徽章／控制图标、tile H/V 翻转、透明 padding、翻页列和可重复生成；恶意裁切及钓鱼第 11／12 项不能被导入。

导入器支持 `--raw`／`--out` 指定隔离输入输出；它没有 `--check` 参数，字节一致性和独立像素检查由上述维护测试执行。

当前实际控制器检查覆盖翻页／缩放的 Help 暂停、覆盖层所有权、退出取消和各肩键适配入口，结果集中在[验证](../validation.md)。其受控状态通过严格存档校验，不推进 state／revision／RNG，不创建玩家存档。数值列表／搜索布局、原状态机部分空白帧和零幅度淡变仍是明确的逼近；不是所有消费者、语言、实体控制器、完整自然通关或真实音频验收。
