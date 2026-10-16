# Battle Scene / Sound 设置入口

Options 现可用方向键或行两侧箭头切换 Battle Scene ON/OFF、Sound MONO/STEREO。菜单内修改仍是草稿；A（Enter）、B（Backspace）、鼠标 CANCEL 或右键通过同一个 `options_save` 事务提交。单击标签／值只选中行，不提交。Button Mode/Help 保持待实现。

保存的设置经同一个 `ApplySavedOptions` 路径应用：音频后端创建时、标题入口读取有效存档时，以及会话同步时。Continue、F9 读取、New Game 和接受的 `options_save` 都经过会话同步；新游戏取原作默认 Scene ON / Sound MONO，缺少 Options 的兼容存档也取这些默认值。打开设置菜单或切换草稿不会提前改变当前音频。

Sound 只调用现有 `ISourceAudioBackend.SetStereo`。MONO 使用已有 PCM 主机的左右平均，STEREO 保留左右通道；没有改合成器、声道算法、滤波、资源或驱动。已经排队的 PCM 按原队列播放，新生成块应用新设置。真实设备发声／主观听感仍未验收。

Scene OFF 使用已有 move-animation dispatch gate，仅抑制对应的攻击动画，不改变权威战斗事件、PP、HP、经验、结果或 RNG。Stat-mask 动画继续显示：固定原作 `battle_script_commands.c` 的 `Cmd_playanimation`／`Cmd_playanimation_var` 明确在 NO_ANIMATIONS 判断前处理 B_ANIM_STATS_CHANGE。不能把 OFF 描述为隐藏所有战斗反馈，也不表示全部攻击动画已还原。

## 验证范围

- `OptionEditingTests`：Scene/Sound 双向切换、草稿不可变、Button Mode/CANCEL 不可编辑、边框资源门控。
- `CampaignOptionsTests`：新游戏和缺失设置默认值、场景／声音共同提交、校验后存档往返／Continue、无效枚举不改变状态。
- `SceneOptionBattleTests`：相同公开野生遭遇及相同命令流，在 ON/OFF 下比较除 Options 外的完整最终状态（包含 RNG、PP、EXP、战斗历史），且要求进入终局。

这些自动测试不等于真实键鼠窗口操作或真实音频输出验收；执行结果另行记录。

2026-10-16 定向执行：正式 SDK 10.0.401 下串行全解决方案构建通过，0 warnings / 0 errors；以下测试 7/7 通过：

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~CampaignOptionsTests|FullyQualifiedName~SceneOptionBattleTests'
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~OptionEditingTests
```

另外使用临时隔离 Godot 4.7.2 / .NET 10.0.12 主机检查，18 项断言通过：调用真实共享设置方法和真实 `SynthSourceAudioPlayer`，MONO/STEREO 各产生 5120 个有限非零帧，逐帧核对左右平均／原样立体声，切换设置不重启播放。该检查使用 Dummy 驱动，只验证配置与 PCM 通道；不等同真实发声验收。临时探针不随普通工程交付。


## Native window check

2026-10-16 13:22–13:24 UTC: on the existing cloud X11 desktop, the actual Options menu showed editable Scene and Sound arrows. Keyboard Down/Right selected Scene Off and Stereo; Backspace committed, and reopening retained both. Mouse clicks on the two right arrows restored On/Mono; Enter committed and reopening retained them. Native log records both accepted options_save transactions. Screenshots showed no clipping. The controlled certificate fixture was used only as an idle-field starting point; no disk save or original player checkpoint was overwritten. Log: /workspace/shared/firered-content-visual/options-native.log. Persistence and channel-output checks remain the separately described automated tests; this window used Dummy audio.
