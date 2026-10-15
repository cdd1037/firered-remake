# 当前C#合成音频接入

运行后端是SynthSourceAudioPlayer；FIRERED_AUDIO=0可关闭，启用时不静默回退到录音。正常开发启动器启用合成音频；独立包默认值尚待交付收口。

运行读取转换后的序列/音色/采样pack、普通叫声目录与实际cue定义，不需要FFmpeg、Python或agbplay运行库。主表位于content/generated/audio/synth，场景cue增量由当前表现接线加载；不要用旧阶段144 IDs/33 cries等数字描述当前完整范围。

## 所有权与时序

音频是表现服务，不改变领域RNG或伤害结算。BGM、SE、fanfare、cry保留各自优先级、停止/恢复及消费完成边界。reset/session替换清理声音与缓冲，不复活旧场景声音。

主线程将合成PCM送到Godot AudioStreamGenerator，当前48kHz、目标5120帧队列。不能把该逻辑缓冲量等同真实设备延迟或无卡顿保证；长主线程阻塞仍可能影响补充。

源码来自项目自身的独立C#音频实现与明确的后续适配。此前研究阅读过agbplay，不宣称clean-room认证。当前不存在必须恢复的逐文件PROVENANCE/hash报告依赖；来源和发布边界见[源基线](source-baseline.md)。

## 再生与已知缺口

固定pret原始输入支持常规场景转换，但从零构造音频pack还需要现有pack或合法自备的匹配ROM。旧cry目录链另依赖未随当前工程提供的inventory/ELF/map等输入；8份早期cue配方缺失。详细输入边界见[资源重建](resource-reconstruction-guide.md)，不能把已有runtime资源视为全部可删缓存。

不下载或公开分发原ROM。普通运行不需要原ROM；离线提取的输入合同与运行合同是两回事。

## 检查

维护音频测试在tests/Game.Audio.Tests，完整范围见[测试说明](../tests/README.md)。检查有限输出、状态/优先级、分块、结束与叫声等行为，不使用已退休阶段runner作为默认入口。

现有云端原生检查用Dummy。非零PCM与正常消费不能代替真实扬声器听感；P0听感仍未完成。专属招式音效/动画覆盖以当前功能矩阵为准，不按旧研究报告累计为全部完成。
