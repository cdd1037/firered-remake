# 普通房屋与浅红商店

固定来源：pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`。本批不改字体，不上传或公开游戏素材。

## 实现范围

- 浅红商店：原三名角色、门与返回；按原顺序出售高级球、超级球、好伤药、活力碎片、万灵药、黄金喷雾。买卖、数量限制、钱与背包边界、取消和存读沿用共同事务。
- 关都：常青住家、枯叶House3、金黄住家。保留波波／烈雀叫声、信件与告示；文字只陈述原通信内容，不启用真实通信。
- 一岛：House1／House2。
- 三岛：House1／House3／House4／House5。Lostelle只在原隐藏旗标允许时出现；性别称呼沿用共同文本缓冲，House5按救援旗标分支；展示柜仍是原招牌交互。
- 五岛：House1／House2。
- 六岛：House自然／历史对白。

上述12间房屋与既有常青学校、浅红House1、三岛House2共用一个来源目录驱动的室内包；无新增逐房运行时分支、坐标副表或数量准入门槛。室外门、事件和地图所有者不变。原动画／音频的自然整条路线仍未验收；科拿房间、贴纸人、训练家卡背面、Photo Printer、通信及e-Reader不在本批。

## 共同接缝

普通商店在组合时根据已加载地图角色、角色的可执行pokemart指令和已准入货物目录绑定。运行时按地图和角色查找，不再维护普通城镇商店名字开关；百货楼层、两岛条件商店与训练家塔继续保留各自业务差异。

普通交互保留源选择顺序：装饰角色的脚本为空时仍记录选择对象，再尝试同格背景事件，避免信件被装饰夹板遮蔽。

室内转换器从pinned raw读取实际布局、事件及图像，核对既有室外地图事件与每扇往返门。加载时再校验生成目录身份、角色图像引用、无室外连接的室内形状和双向门契约，不读取raw。波波与烈雀帧进入共同宝可梦overworld图集。

## 验证入口与边界

- `FuchsiaMartContentTests`、`FuchsiaMartFlowTests`、`OrdinaryMartCatalogTests`：原货物／角色绑定与事务行为。
- `OrdinaryInteriorBatchContentTests`、`OrdinaryInteriorBatchFlowTests`：15间室内的来源／出入／存读回归，新增普通对白、信件／展示柜、叫声，以及Lostelle隐藏／性别和救援旗标分支。
- `OrdinaryInteriorsImportTests`：源图像重新渲染并逐像素比对，已打包图像从图集读回。
- `import-school-house-interiors.py --check`、`import-fuchsia-route-region.py --check`、`import-field-tilesets.py --check`、`import-shared-overworld.py --check`：来源与共同资源再生成。
- 共享地图图集检查区分新渲染原图与已打包旧图的一致性。既有图像只从图集读回时，不声称独立原图证明。

应用流程测试使用受控起点和公开命令。当前档兼容分支执行严格存读；无有效代表档的救援前与女性身份仅用隔离分支测试，不宣称这些分支已严格存读。不能据此宣称从新游戏自然游历全部房屋、原生窗口逐帧或真实扬声器输出通过。
