# 当前游戏组合与启动

唯一正常内容入口是PretCampaignCatalog.LoadCurrent。关都主线、七岛和已接通后期功能在同一当前组合中；真实功能缺口见[当前范围](first-clear-remaining-scope.md)，不再维护旧Celio/地区版本作为玩家启动选择。

## 开发入口

在工程目录执行：

```sh
python dev build
python dev run
# 可选：加载明确的当前检查点
python dev run /path/to/current-checkpoint.json
```

这个入口使用正常 OS 用户存档目录；旧 Bash 诊断入口 `scripts/dev/launch-current.sh` 仍使用隔离的 `.play/current`。正常运行默认启用玩家入口和音频，不默认打开完整状态证据；显式设置 FIRERED_EVIDENCE 或 FIRERED_VERBOSE 用于诊断。Linux 无开发变量启动已核验，Linux 独立导出与捆绑 .NET 运行时启动已验证；成品窗口全流程和真实音频不在此次范围内。

GameStartupSelection解析参数/环境快照；Godot Bootstrap组合实际游戏。current是正常模式，probe/harness仅为显式工程夹具，不能拿它们通过代替Current可运行。

旧地区启动参数和互相冲突的选择会明确失败。编辑器默认定位工程根，成品默认读取可执行文件旁的 content/generated；FIRERED_PROJECT_ROOT 仅作显式开发覆盖。完整交付边界见[交付与维护边界](project-quality-review.md)。

## 应用与状态

- CampaignSession.CreateCurrent创建当前新游戏。
- CampaignSession.RestoreCurrent恢复已验证状态，不借恢复再次执行地图剧情或消费RNG。
- CampaignCatalogBinding在创建/恢复时绑定当前目录。
- 组合只公开一份 GamePokemon、OpponentPokemon 和 Machines；TrainerTower 的特殊对手目录单独保留。地区资源在单一构造入口装载，不再通过历史 Enabled 开关或前驱版本组合。
- 地图来源符号统一由当前 world registry 解析；可解析的身份不等于已选入可玩地图。普通地区不再以包装对象或前驱解析链传递地图；特殊机关、跨图端点与地方业务仍验证实际选中地图及演员。早期陆地表一次读取组合，保留原有数据校验与独立 Route4 区域检查。
- 行走遇敌只保留当前进程身份，不再接受历史地区阶段作为进程配置；当前序列化 token 不变。Safari 移动适配身份与遇敌进程分开，真实 Repel 200／250 步契约仍保留。
- 普通遇敌必须绑定真实表与候选；物种存在不等于允许任意战斗来源。玩家徽章、剧情和图鉴解锁仍按状态判断。
- 普通单/双打走共享内核；教程/特殊模式边界见[架构](architecture.md)。

## 当前存档边界

当前 schema 为 3，rules ID 为 firered-current-campaign-v4，content ID 为 firered-current-content-v4。Continue 严格匹配版本并验证完整状态，不支持旧存档兼容、转换或补写缺失统计。

旧检查点转换与手填孵蛋总数工具已移除。旧命令明确返回非零状态，不读取或改写来源、目标文件，也不建议使用已删除的转换命令。缺失终身孵蛋数仍拒绝，不从现存个体或回执推算，不默认为零。

现有 generation-57604 历史样本为 schema 1／campaign-v3，保持原文件不变，不能作为当前 Continue 基线。当前新游戏初始化真实统计，并继续使用正常 Save／Continue、checksum、primary／backup／staging 与候选文件指纹边界。移除转换功能不改变格式或版本号，也不压缩存档。

历史 runtime/profile/升级路由已退休。不要依据旧报告重建这些系统；当前回归入口是[测试说明](../tests/README.md)。
