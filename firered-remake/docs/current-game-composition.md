# 当前游戏组合与启动

唯一正常内容入口是PretCampaignCatalog.LoadCurrent。关都主线、七岛和已接通后期功能在同一当前组合中；真实功能缺口见[当前范围](first-clear-remaining-scope.md)，不再维护旧Celio/地区版本作为玩家启动选择。

## 开发入口

在工程目录执行：

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx
bash scripts/dev/launch-current.sh
# 可选：加载明确的当前检查点
bash scripts/dev/launch-current.sh /path/to/current-checkpoint.json
```

该Bash入口设置开发资源根、菜单与音频，使用隔离的.play/current存档目录。正常运行不默认打开完整状态证据；显式设置FIRERED_EVIDENCE或FIRERED_VERBOSE用于诊断。普通用户包与跨平台启动尚待交付收口，不应把以上命令当已发布成品。

GameStartupSelection解析参数/环境快照；Godot Bootstrap组合实际游戏。current是正常模式，probe/harness仅为显式工程夹具，不能拿它们通过代替Current可运行。

旧地区启动参数和互相冲突的选择会明确失败。当前仍需要FIRERED_PROJECT_ROOT；独立可执行包的默认资源定位见[质量审核Q04](project-quality-review.md)。

## 应用与状态

- CampaignSession.CreateCurrent创建当前新游戏。
- CampaignSession.RestoreCurrent恢复已验证状态，不借恢复再次执行地图剧情或消费RNG。
- CampaignCatalogBinding在创建/恢复时绑定当前目录。
- 普通单/双打走共享内核；教程/特殊模式边界见[架构](architecture.md)。

## 存档与显式转换

当前rules ID为firered-current-campaign-v2，content ID为firered-current-content-v4。正常Continue严格匹配版本，不自动猜测或升级旧结构。

需要转换已明确支持的旧检查点时，在工程目录运行：

```sh
dotnet run --project src/Game.Tooling -- convert-current-checkpoint SOURCE NEW_DESTINATION
```

工具拒绝覆盖已有目标；以实际转换器支持的输入为准，不是任意旧存档升级器。原文件保留，输出必须通过当前状态校验。v3到v4的已验证转换只移除旧NPC交换目录指纹，保留业务状态、RNG和generation。

历史runtime/profile/升级路由已退休。不要依据旧报告重建这些系统；当前回归入口是[测试说明](../tests/README.md)。
