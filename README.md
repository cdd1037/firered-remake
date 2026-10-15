# FireRed 普通工程交付 · 3e329ac

直接解压 FireRed-project-3e329ac.zip（标准 ZIP / DEFLATE，可使用系统解压或 7-Zip），进入 FireRed。没有去重对象库，也不需要恢复脚本。

- firered-remake/：游戏工程。当前解决方案 FireRed.Current.slnx；Godot 编辑器导入 src/Game.Godot/project.godot；C# 项目 src/Game.Godot/Game.Godot.csproj。
- firered-audio-player/：独立自定义合成器开发源与工具；主游戏使用已包含的合成器实现及 content/generated/synth-audio 运行输入。
- checkpoints/：代表存档及说明，最新 continued-checkpoint.json 为 generation 57604，当前规则版本。原始档保留。存档并非自动安装到用户存档槽。

自行安装 .NET SDK 10.0.401 和 Godot 4.7.2 .NET/Mono 版（按自己的操作系统），用编辑器导入项目并编译。源码/资源普通文件可在 Windows 直接解压浏览编辑，不强制 WSL；现有 .sh 开发脚本需要 Bash。既有实际运行验收环境为 Linux，本交付没有新增 Windows 运行验收。具体启动与项目状态见 firered-remake/README.md。

当前程序仍消费部分 content/raw 原作脚本、文本与 scripts 配方，所以同时保留 raw/generated 和字体、RGBA、自定义合成器输入。原 pret 必需采样保持原样；不含转换后录音、SDK、编译缓存、历史 DLL、大执行日志或旧 Git 历史。若重新抓取上游源用于完整再导入，按项目导入器/锁中来源与版本获取 pret/pokefirered；普通运行资源已包含，不需要 Git 对象包。

在最外层 FireRed 目录初始化 Git 即可；随包 .gitignore 会排除运行素材、存档、原作参考与缓存，仅纳入作者源码、配置和工具。不要使用 git add -f 强行加入被忽略的资源。

本地源码固定 3e329acdb9691a918accf196e6734dd56c8134a0；audio 4a6cedc51ba423a117396972d8eed6b96e5fb977。最新精简源码推送被工具阻止，GitHub main 仍为旧 6055f1dc；main 历史未替换。用户可自行认证并推送源码包；backup/pre-cleanup-20261015 仍引用旧历史，如需移除也由用户自行处理。

本轮只静态检查 ZIP 内容/路径与文件完整性，未重新运行剧情或恢复演练。自然历史41923未通关；当前档延续已披露的受控联盟通关后续。

收尾再统一做 generated-only、深入重构与精简（含残留 hash）。高标准验收包括模块边界/重复逻辑、开发运行资源分离，以及启动/切图/内存缓存实测对比；避免无收益防御与过度架构。本次交付后暂停。

## 当前维护与已知限制

默认解决方案仅 FireRed.Current.slnx 的17项目；另保留战斗规则等补充回归作者代码，未累加历史阶段整套测试/执行输出。现有 CanonicalTrainerApplication.Tests 依赖的2份历史输入当前缺失（conversion-run01/current.json、before-gideon.json）；该测试不能开箱完整执行，本次未伪造或替换输入。部分保留设计文档的历史证据链接指向旧阶段包，不是本包内容。

主要入口：firered-remake/dev；Godot：firered-remake/src/Game.Godot/project.godot；C#：firered-remake/FireRed.Current.slnx。余项与已知缺口见 docs/first-clear-remaining-scope.md、docs/full-scope-audit.md 和 docs/battle-system-completion-matrix.md。

补充规则回归未在本轮重新复验，部分涉及历史 API 迁移或缺少历史输入，不承诺全部开箱可跑。已知还缺 fuchsia-center.json 及 freeze-singles-before-full 两份基线。

本次已把重复原作输入集中为 content/raw/pokefirered/037335f4，功能包直接引用共享路径，不需要恢复器。详情见 firered-remake/docs/shared-raw-resources.md。开发验收：Current构建0警告/0错误，13项新游戏/57604 Continue，1821项JSON路径存在，代表导入24输入82输出一致；未额外重放历史路线。

转换资源整理已完成：共享常量、规范紧凑地图、地图来源注册表、PNG单一图像输入；generated从293.60MB减至117.68MB。最新Current构建0警告/0错误，13项新游戏与57604 Continue通过，存档payload/RNG/generation不变。见 firered-remake/docs/generated-resource-unification.md；本包未额外重放路线。
