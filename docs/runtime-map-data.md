# 运行时地图数据

当前格式是紧凑schema3。地图按实际地区平铺为JSON文件，使用有含义的变体后缀；不再要求每张图独占runtime-map.json目录。具体目录见[资源布局](resource-layout.md)。

地图保留ID、原图业务标识、尺寸、事件、warp、脚本分派路径、16位blocks、属性字和渲染引用。唯一metatile行是[id, attributes]；不存每格来源记录或属性文件索引。不要把实际事件源端点与已经删除的导入证明混为一类。

运行只读取转换数据，不打开原作脚本文本。地图集合只引用实际地图文件，不重复完整地图payload。Seafoam和Trainer Tower等真实布局变体保留，不能仅按同一MapId合并。

## 再生

scripts/lib/map_runtime.py负责格式转换和扁平发布，runtime_map_manifest.py负责实际加载索引。地图导入后按相应说明生成集合、共享tilesets和渲染图集。

```sh
python scripts/imports/maps/import-field-tilesets.py
python scripts/imports/maps/import-field-tilesets.py --check
python scripts/imports/maps/import-map-atlases.py --check
```

maps/tilesets.json保存实际地图引用的完整primary/secondary属性表，包括初始网格未使用但游戏会切换到的图块。生产者由raw data/layouts/layouts.json、src/data/tilesets/headers.h、metatiles.h及引用的属性bin解析；缺少输入或有效地图为空必须报错。数量由当前输入决定，不用历史246/45等数值作为运行门槛。

## 验证边界

维护检查包括schema3生成、合法/错误引用、图层key实际解析、逐像素图集重建、tileset再生及真实loader。普通Gameplay/存读和Godot地图显示各自验证，不能以某一层通过替代其他层，也无需为打包重放全部历史路线。
