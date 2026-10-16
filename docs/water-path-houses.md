# 水之小径两栋民居

固定来源：pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`，`SixIsland_WaterPath_House1` 与 `House2`。

- House1：原作 House4 室内、Beauty (3,4)、纪录牌 (9,1)、出口 (3,7)，连接水之小径 warp 0。赫拉克罗斯大小服务使用共享队伍选择、实例身份校验、原作 PID/IV 尺寸算法与原作脚本分支，奖励巢穴球。
- House2：原作 House3 室内、Man (7,4)、出口 (4,7)，连接水之小径 warp 1。只有邻居量身高的原作对话，没有额外赠送或服务。
- 共用 `PokemonSizeRecordRules` 与 `CampaignSizeRecord` 接入原鲤鱼王和新赫拉克罗斯；键盘和鼠标共用同一选队界面。纪录使用原 u16 脚本变量，交互结束后的正常 Save/Continue 保存纪录和标记。
- 满包行为的明确获批变更与仍保持的鲤鱼王兼容行为见 [原作行为改进](original-behavior-improvements.md)。

验证由 `PokemonSizeRecordRulesTests`、`WaterPathSizeRecordTests`、`WaterPathHouseContentTests` 与 `WaterPathHouseImportTests`覆盖；受控测试不代表从新游戏自然重走七岛全流程。原生窗口/声音验收没有由这些测试替代。

## 本轮验证（2026-10-16）

环境：Godot `4.7.2.stable.mono.official.ed1daf0bf`、SDK 10.0.401、.NET runtime 10.0.12。定向应用 21 项、领域尺寸 2 项、内容 5 项、资源导入 1 项（内含 3 项 Python 检查）通过。随后默认七程序集全测 2601 项通过，零失败、零跳过；包含 Godot 的全解构建零警告、零错误。

```bash
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal
dotnet test FireRed.Current.slnx --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFilePrefix=water-path-full' --results-directory .toolhome/test-results/water-path-full
```

保留失败证据：首次默认并行构建仅返回 Build FAILED、0 errors/0 warnings；串行重试成功。应用早期失败为合成个体缺少获取凭证、门抵达坐标和重复转向的测试夹具问题；改为可重放的孵化个体及真实门/朝向行为后定向通过，没有放宽产品校验。全测只执行一次；以上失败不当作通过，也不推断机器故障原因。
