# 合成音频

音频现为主工程的 `Game.Audio` 模块，命名空间仍为 `FireRedAudio`。不再维护独立音频工程、复制版 Core、阶段研究工程或第二个 Godot 示例主机。实际输出由主游戏的 `SynthSourceAudioPlayer` 负责，启用时不静默回退到录音；`FIRERED_AUDIO=0` 可关闭。

## 目录与入口

- `src/Game.Audio/`：序列、PCM/PSG、叫声、资源格式的唯一运行实现
- `src/Game.Godot/SynthSourceAudioPlayer.cs`：主游戏 Godot PCM 主机与场景 cue 绑定
- `tools/AudioTool/`：pack 提取、离线 WAV 渲染、性能测量
- `scripts/imports/audio/build-cue-catalog.py`：由固定源码与 pack 一次生成完整运行 cue 目录
- `scripts/imports/audio/build-cry-catalog.py`：直接生成普通叫声目录
- `tests/Game.Audio.Tests/`：按需运行的合成器测试
- `scripts/checks/test-audio-catalog-import.py`：按需运行的导入器单元测试

`Game.Audio` 和 `AudioTool` 随正常解决方案构建。音频测试不在默认 `dotnet test` 范围内；修改音频时明确运行以下命令，均从工程根目录执行：

```sh
dotnet test tests/Game.Audio.Tests/Game.Audio.Tests.csproj
python3 scripts/checks/test-audio-catalog-import.py
```

离线渲染和性能检查：

```sh
dotnet run --project tools/AudioTool -c Release -- render content/generated/audio/synth/firered.pack select 3 /tmp/select.wav source-filtered
dotnet run --project tools/AudioTool -c Release -- benchmark content/generated/audio/synth/firered.pack source-filtered
```

CLI 默认 `modern-dry`，主游戏选用 `SourceFiltered`。输出文件由调用者选择，不写阶段报告或项目内备份目录。开发工具与 NuGet 环境沿用工程 `dev` / `scripts/env.sh`，不另设音频构建入口。

源码由本项目编写；前期研究曾阅读 agbplay，不宣称 clean-room 认证。原资源保持私有，不下载或公开分发 ROM/Pokémon 素材。

## 运行库与检查

### 单一实现

`src/Game.Audio/Game.Audio.csproj` 是唯一合成实现；主游戏、AudioTool 和按需运行的 Game.Audio.Tests 通过 ProjectReference 使用它。游戏内的 encounter cry 扩展已保留在 Core，避免独立测试只验证旧副本。

Core 只依赖 .NET 标准库。游戏 cue 选择、场景路由、fanfare 等表现策略仍由主游戏负责。音频不改变领域 RNG 或战斗结算。库不包含游戏资源路径、Godot 输出设备或后台线程。

### 所有权与接口

`Synth.Render(Span<float>)` 写入调用者拥有的 48 kHz 交错立体声缓冲。控制方法与 Render 必须由同一所有者线程调用。热 Render 在关闭 tracing 的已测工作负载中不分配或读取文件；创建对象、首次音色/叫声准备和诊断 tracing 可以分配。宿主应提前 `PrepareNormalCry(species)`，不要把云端吞吐等同设备实时性。

播放器槽：0 BGM、1 SE1、2 cry、3 SE2、4 SE3。共同使用五个 PCM 声道及四个独立 PSG 声道（square1、square2、wave、noise）。序列保留原作分数帧时钟、轨道顺序、优先级和抢占语义，不以任意无限复音替代。

- `Start` 验证新歌曲头后替换对应槽；`Stop` 清理该槽。
- `PausePlayer` 停止相连声道但保留序列位置；`ResumePlayer` 不复活旧音符。
- `SetPlayerGain` 是独立宿主增益，跨 Start/Stop 保留；既有混响尾音共享输出历史。
- `SetPlayerPan` 对选中的有效轨道设置外部 pan，不改变序列 PAN，也不复活已结束轨道。
- `Ended` 包括序列、声道及有效效果尾音；不能把 FINE、硬停止和效果自然衰减混为一谈。

未加载目录时正常叫声 API 只支持 Charmander；有目录时通过公开物种 API 播放普通 mode 0。encounter mode 2 的独立入口保留原作固定参数，并按已加载的 canonical 叫声目录校验物种能力。新增 doubles mode 1 的独立入口仅将普通叫声的长度改为 20、release 改为 225，保留 pitch 15360、chorus 0 和 volume 120，接受原作有符号 pan；默认不压低 BGM。三个入口共享播放控制，不改变正常/遭遇模式原有契约，也不逐物种放行。这不是所有特殊 cry mode 的支持声明。不支持的物种/模式在改变现有播放前失败。

### 输出配置

主游戏使用 `AudioProfile.SourceFiltered`。CLI 默认 `modern-dry`；可显式选择 `source-gain`、`source-linear`、`source-pitch`、`source-envelope`、`source-reverb`、`source-bandlimited`、`source-filtered` 比较各级实现。参数对应 `src/Game.Audio/Profiles.cs`；配置名称不表示硬件精确认证。

输出保留序列、原采样、音高/包络和所需 PSG 行为，但 48 kHz 浮点合成、插值/滤波、混响及 PSG 相位与原硬件整数混音/PWM 存在差异。没有录音兜底或忽略不支持操作的静默成功。


## 音频资源与导入

### 运行素材

主工程的 `content/generated/audio/synth/firered.pack` 和 `normal-cries.bin` 是唯一维护副本。二者不随源码提交或公开分发。主游戏仅加载 `content/generated/audio/cues.json`，不再读取地区增量、前驱证明或展示资源中的 synth manifest。目录在初始化时验证歌曲头和普通/遭遇叫声能力，播放时不读源码。

`firered.pack` 保留固定 FireRed 布局的地址区间 `[0x08489968, 0x086fbea4)`，共 2,565,436 字节，所有多字节整数为 little-endian。序列和采样保存虚拟原作地址；读取器把它转换为数组偏移并校验地址、长度、溢出及实际格式，不解引用原生指针。歌曲头包含轨道数、block 标志、优先级、reverb 参数、音色表和轨道指针。每个音色记录 12 字节；PCM 波形头 16 字节，包含类型、状态、频率、循环起点和样本数。具体支持的指令和音色在读取/执行时校验，不支持的数据会报错。

`normal-cries.bin` 使用以下布局：

- 8 字节 magic/version：ASCII `FRCN0001`
- uint32 记录数，必须与文件长度对应，且至少有一条记录
- 每条 16 字节：uint16 species，uint16 cry index，uint32 sample count，uint32 payload bytes，uint8 guard stored，3 字节保留区

保留区由写入器置零，当前读取器忽略。物种使用 FireRed 内部 ID，排除 NONE、旧 Unown 占位、EGG 和附加形态；cry index 排除未使用槽。重复物种、非法计数或不足的 DPCM 数据长度会失败。资源加载不以整文件指纹或历史表总数决定兼容性；常规发布素材仍覆盖全部 386 普通物种，测试直接遍历这些物种。采样准备时继续核对音色/波形头与目录记录、包内地址和有效 payload，不能借合法目录越界读取。

DPCM 按 64 样本分块：块头有一个直接样本，后续按原格式 nibble 顺序累加有符号增量。插值需要尾部 guard。部分原始资产不保存 guard nibble，目录明确标记此情况，解码后重复最后一个有效样本，不读取相邻资产。

### 生成 pack

固定来源为 `pret/pokefirered` revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`。已有合法匹配 ROM 时：

```sh
dotnet run --project tools/AudioTool -c Release -- pack /absolute/path/to/input.gba /absolute/path/to/output.pack
```

ROM 版本检查仅属于提取工具；Core 的运行时入口只读取 pack。没有匹配 ROM 时保留现有 pack，不下载 ROM，也不把源 WAV 当作 pack 的替代品。

### 生成运行 cue 目录

```sh
python3 scripts/imports/audio/build-cue-catalog.py
python3 scripts/imports/audio/build-cue-catalog.py --check
```

入口默认读取固定 raw 版本中的 songs.h、song_table.inc、species.h、sound.c，以及现有 pack 和 normal-cries.bin。歌曲 ID/播放器来自源常量/表并与 pack 对照；地址/优先级来自包内歌曲头；fanfare 计数直接来自 sound.c。循环标记通过包内 M4A 指令控制流、running status 和 pattern 栈推导，现有 MIDI 的循环 marker 另行交叉核对。raw 缺少部分 MIDI，不影响完整歌曲控制流检查。

目前输出 345 首歌曲与 386 个物种各两种叫声，共 1117 个 cue。mode 1 的 CRY_<species>_DOUBLES 在宿主按需从已有普通物种 binding 派生，不另存重复元数据、不修改目录；PlayDoublesCry 遵循原作 PlayCry_ByMode 的无 BGM ducking 语义。开场 Nidorino 使用 pan 63，详见 [开场电影](../features/intro-movie.md)。MUS_DUMMY 是停止哨兵；MUS_RS_VS_TRAINER 含当前 Core 未实现的 MEMACC 指令，明确不注册。其它未知指令、越界地址或缺失来源会失败，不猜测循环。该入口不读取任何旧 cue JSON，也不以复制旧中间目录作为权威输入。

地区音频 CLI、配方及累加 manifests 已删除。normal-cries.bin 的独立原始样本再生边界仍见下节；此入口不重建 pack 或修改 DSP。

### 生成普通叫声目录

```sh
python3 scripts/imports/audio/build-cry-catalog.py \
  --reference /absolute/path/to/matching-pokefirered \
  --pack /absolute/path/to/firered.pack \
  --output /absolute/path/to/normal-cries.bin
```

输入目录需要 species/Hoenn cry 常量、cry ID 表、cry/direct-sound 声明、对应生成后的 `.bin` 样本和原始 `.wav`。工具直接读取这些输入，对照 pack 内的音色与资产，检查解码样本/guard 与源 WAV，然后写出目录；无需旧 inventory JSON、ELF、map 或阶段报告。它不执行上游构建。项目本地的 raw 目录带有源 WAV，但没有生成后的 cry `.bin`；因此目前没有宣称整条目录冷重建已完成。现有 catalog 保持不变。

工具单元测试使用人工构造的小样本；全物种运行测试使用现有 canonical pair。二者都不能替代缺失原始构建输入的完整再生成验收。

## 验证边界

音频测试覆盖有限输出、状态/优先级、声像、分块确定性、普通叫声和原主游戏 encounter cry。资源检查保留真实格式与边界约束，运行库不再以历史文件指纹、阶段报告或重复来源清单作为播放门禁。

主游戏 PCM 主机以 48 kHz 生产，目标队列为 5120 帧，约 106.67ms；不能等同最终设备延迟或无卡顿保证。长主线程阻塞仍可能影响补充。音频不改变领域 RNG 或战斗结算。

原共享 Core 合并已对 17 组代表场景进行 PCM 字节与控制状态比较，包括 BGM/SE、两种 profile、普通叫声、9 种既有 encounter cry 和播放控制；输出相同。新目录迁移另对旧目录的 460 个唯一 cue 逐字段比较，保持语义相同。单独音频测试仍需要在改动后运行，不纳入默认全量测试。

已有云端原生检查使用 Dummy，非零 PCM 与正常消费不能替代真实扬声器听感；P0 听感仍未完成。专属招式音效/动画和整条游戏路线覆盖以当前功能范围为准，不能按旧研究报告累计为全部完成。
