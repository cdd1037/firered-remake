# 内容层

`Game.Content` 只引用 `Game.Domain`，读取转换资源、校验不可变定义并组装当前游戏目录。运行时不解析 raw、来源仓库或导入配方。内容准入与资源边界见[内容组合](../../docs/current-content-composition.md)和[架构](../../docs/architecture.md)。

## 目录

| 目录 | 职责 |
| --- | --- |
| `Composition/` | 当前游戏、世界及 `PretCampaignCatalog` 的组合入口 |
| `Catalogs/` | 共享道具、机器、训练家和机制目录；`Pokemon/` 保存物种及用途目录 |
| `Serialization/` | JSON 读取、模型、校验、资源文件访问和脚本序列化 |
| `Story/` | 剧情程序读取、编译与导入工具访问边界 |
| `World/Maps/` | 地图格式、运行图块、世界连接、布局和表现绑定 |
| `World/Regions/` | 按地区归档目录 partial、场景定义、物品、训练家与专用资源读取器 |
| `World/` | 地区共享服务、室内、对白、旅行与再战内容 |
| `Field/` | 场外技能、遇敌表、拾取、钓鱼和地图旅行数据 |
| `Pokemon/Storage/`、`Pokemon/Trades/` | PC 内容与 NPC 交换内容 |
| `Inventory/` | 邮件、关键物品、机器取得方式与赠送契约 |
| `Services/` | 商店、治疗、招式服务、游戏厅和名人堂等共享内容 |
| `Presentation/` | 资源描述、图像区域与演出数据；`Maps/` 保存地图图块和过渡素材读取器 |
| `UI/` | 开场、选项、图鉴、地图、档案与收集界面数据 |
| `QuestLog/`、`Tutorials/` | 回顾资源、老人教学和 Teachy TV 内容 |
| `Text/` | 原有嵌入文本资源位置，嵌入规则保持不变 |
| `Properties/` | 程序集级声明 |

地区目录用于查找现有内容，不决定玩法准入。跨地区共享定义仍在共享目录校验，不能在地区文件夹复制规则表或添加验证例外。`PretCampaignCatalog` 等 partial 类型保持原来的命名空间与程序集。

## 资源与验证

项目文件保留在根目录。C# 源码由 SDK 递归发现，嵌入资源仍使用 `Text/en/*.json`；`content/generated` 的资源路径与格式均不因源码移动而变化。

本次仅整理目录与路径，未修改 C# 源码内容或运行资源。移动前后检查、完整命令及构建阻塞见[验证记录](../../docs/validation.md#content-目录整理2026-10-21)。
