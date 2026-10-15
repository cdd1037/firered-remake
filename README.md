# FireRed 普通工程交付 · 3e329ac

直接解压 FireRed-project-3e329ac.zip（标准 ZIP / DEFLATE，可使用系统解压或 7-Zip），进入 FireRed。没有去重对象库，也不需要恢复脚本。

- firered-remake/：游戏工程。当前解决方案 FireRed.Current.slnx；Godot 编辑器导入 src/Game.Godot/project.godot；C# 项目 src/Game.Godot/Game.Godot.csproj。
- firered-audio-player/：独立自定义合成器开发源与工具；主游戏使用已包含的合成器实现及 content/generated/synth-audio 运行输入。
- checkpoints/：代表存档及说明，最新 continued-checkpoint.json 为 generation 57604，当前规则版本。原始档保留。存档并非自动安装到用户存档槽。

自行安装正式版 .NET 10.0 SDK（10.0.100 起，允许后续功能带和补丁）和 Godot 4.7.2 .NET/Mono 版（按自己的操作系统）。在 firered-remake 执行 `dotnet build`、`dotnet test`。六项资源导入检查需要 Python；普通 C# 测试可按项目运行，无需 Python，见 firered-remake/tests/README.md。本机工具诊断仍可选用 `python dev doctor`。源码/资源普通文件可在 Windows 直接解压浏览编辑，不强制 WSL；其他 .sh 开发脚本需要 Bash。历史游戏运行验收环境为 Linux。具体构建配置、启动与项目状态见 firered-remake/README.md。

当前程序仍消费部分 content/raw 原作脚本、文本与 scripts 配方，所以同时保留 raw/generated 和字体、RGBA、自定义合成器输入。原 pret 必需采样保持原样；不含转换后录音、SDK、编译缓存、历史 DLL、大执行日志或旧 Git 历史。若重新抓取上游源用于完整再导入，按项目导入器/锁中来源与版本获取 pret/pokefirered；普通运行资源已包含，不需要 Git 对象包。

在最外层 FireRed 目录初始化 Git 即可；随包 .gitignore 会排除运行素材、存档、原作参考与缓存，仅纳入作者源码、配置和工具。不要使用 git add -f 强行加入被忽略的资源。

原归档交付的源码基线为 3e329acdb9691a918accf196e6734dd56c8134a0，audio 为 4a6cedc51ba423a117396972d8eed6b96e5fb977。当前开发状态与远端提交以 Git 记录为准；以下 ZIP 验收说明属于原归档交付记录。

本轮只静态检查 ZIP 内容/路径与文件完整性，未重新运行剧情或恢复演练。自然历史41923未通关；当前档延续已披露的受控联盟通关后续。

收尾再统一做 generated-only、深入重构与精简（含残留 hash）。高标准验收包括模块边界/重复逻辑、开发运行资源分离，以及启动/切图/内存缓存实测对比；避免无收益防御与过度架构。本次交付后暂停。

## 当前维护与已知限制

当前解决方案包含五个游戏产品程序集、八个按依赖边界组织的 MSTest 测试项目及两个音频项目。进入 firered-remake 统一运行 `dotnet test`；用例与输入见 firered-remake/tests/README.md。72 个依赖已删除 API 的历史项目已退休并记录原因；缺失的早期历史输入不再作为当前套件前提，原路线/转换证明未伪造恢复。

主要入口：firered-remake/dev；Godot：firered-remake/src/Game.Godot/project.godot；C#：firered-remake/FireRed.Current.slnx。余项与已知缺口见 docs/first-clear-remaining-scope.md、docs/full-scope-audit.md 和 docs/battle-system-completion-matrix.md。

历史设计文档中的旧测试和证据引用属于历史记录，不是当前维护清单。测试不下载素材或生成 golden；本地资源、当前存档及音频 pack 仍需保留。

本次已把重复原作输入集中为 content/raw/pokefirered/037335f4，功能包直接引用共享路径，不需要恢复器。详情见 firered-remake/docs/shared-raw-resources.md。开发验收：Current构建0警告/0错误，13项新游戏/57604 Continue，1821项JSON路径存在，代表导入24输入82输出一致；未额外重放历史路线。

转换资源整理已完成：共享常量、规范紧凑地图、地图来源注册表、PNG单一图像输入；generated从293.60MB减至117.68MB。最新Current构建0警告/0错误，13项新游戏与57604 Continue通过，存档payload/RNG/generation不变。见 firered-remake/docs/generated-resource-unification.md；本包未额外重放路线。
