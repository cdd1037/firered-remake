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
- 普通遇敌必须绑定真实表与候选；物种存在不等于允许任意战斗来源。玩家徽章、剧情和图鉴解锁仍按状态判断。
- 普通单/双打走共享内核；教程/特殊模式边界见[架构](architecture.md)。

## 存档与显式转换

当前rules ID为firered-current-campaign-v4，content ID为firered-current-content-v4。正常Continue严格匹配版本，不自动猜测或升级旧结构。

需要转换已明确支持的旧检查点时，在工程目录运行：

```sh
dotnet run --project src/Game.Tooling -- convert-current-checkpoint SOURCE NEW_DESTINATION
```

工具拒绝覆盖已有目标，并保留原文件。仅支持 content-v4 的两条明确路径：

- campaign-v3 → campaign-v4：逐条验证旧 Trainer Tower 回执的顺序 ID、目录描述、结局和出场对手，再只保留最后一条回执；下一场 ID 由这条规范 ID 递增。其他玩法字段不变。
- campaign-v2 → campaign-v4：先显式删除已退休个体证明并收敛当前个体来源／野战记录，验证完整旧 Tower 历史，再执行上述 Tower 收敛。该中间步骤不是当前存档准入。

两条路径都必须通过完整状态校验，保留当前个体、分配器、图鉴统计、特殊事件、名人堂快照、Tower 的四项挑战记录／奖品／计时、RNG 与 generation。v4 输入和未声明的旧版本明确拒绝；不把动态当前版本号冒充旧的 v2→v3 转换，也不自动推进游戏。

终身孵蛋数缺失时，版本转换仍会拒绝，不从回执或存活个体推算。现有 generation-57604 的 v3 代表档缺少可信总数，原文件保持不变，仍不能作为当前 Continue 基线。只有操作者独立知道总数时，才可先使用 `set-known-hatch-count SOURCE NEW_DESTINATION KNOWN_LIFETIME_COUNT` 添加明确的总数；该操作明确支持 campaign-v2/v3/v4 各自原版本内的修正，保留来源 rules、完整 Tower 历史及 v2 退休字段，再按需另行转换版本。v2 修正仅在临时副本上执行既有退休来源校验，不发布其中的收敛结果。测试夹具的固定 1000 仅为受控测试输入，绝不是玩家历史修复。

历史runtime/profile/升级路由已退休。不要依据旧报告重建这些系统；当前回归入口是[测试说明](../tests/README.md)。
