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

这个入口使用正常 OS 用户存档目录；旧 Bash 诊断入口 `scripts/dev/launch-current.sh` 仍使用隔离的 `.play/current`。正常运行默认启用玩家入口和音频，不默认打开完整状态证据；显式设置 FIRERED_EVIDENCE 或 FIRERED_VERBOSE 用于诊断。Linux 无开发变量启动已核验，实际独立导出仍因缺官方模板而未验收。

GameStartupSelection解析参数/环境快照；Godot Bootstrap组合实际游戏。current是正常模式，probe/harness仅为显式工程夹具，不能拿它们通过代替Current可运行。

旧地区启动参数和互相冲突的选择会明确失败。编辑器默认定位工程根，成品默认读取可执行文件旁的 content/generated；FIRERED_PROJECT_ROOT 仅作显式开发覆盖。完整交付边界见[质量审核Q04](project-quality-review.md)。

## 应用与状态

- CampaignSession.CreateCurrent创建当前新游戏。
- CampaignSession.RestoreCurrent恢复已验证状态，不借恢复再次执行地图剧情或消费RNG。
- CampaignCatalogBinding在创建/恢复时绑定当前目录。
- 普通单/双打走共享内核；教程/特殊模式边界见[架构](architecture.md)。

## 存档与显式转换

当前rules ID为firered-current-campaign-v3，content ID为firered-current-content-v4。正常Continue严格匹配版本，不自动猜测或升级旧结构。

需要转换已明确支持的旧检查点时，在工程目录运行：

```sh
dotnet run --project src/Game.Tooling -- convert-current-checkpoint SOURCE NEW_DESTINATION
```

工具拒绝覆盖已有目标；以实际转换器支持的输入为准，不是任意旧存档升级器。原文件保留，输出必须通过当前状态校验。当前转换仅接 content-v4 / campaign-v2 的上一版代表档到 campaign-v3：删除已退休个体证明，保留当前个体、分配器、图鉴统计、特殊事件和名人堂快照；保留 RNG 与 generation。旧的更早格式不再叠加为长期兼容支路。

历史runtime/profile/升级路由已退休。不要依据旧报告重建这些系统；当前回归入口是[测试说明](../tests/README.md)。
