# 完整单机目标的实施顺序

状态：路线和验收门禁。P0 尚在取证；后续阶段均未据此宣称完成。最终范围见 [scope.md](scope.md)，源冻结见 [source-baseline.md](source-baseline.md)。本路线不承诺缺乏测量依据的日历工期。

## 阶段与退出证据

| 阶段 | 工作边界 | 退出证据 |
| --- | --- | --- |
| P0 工具链与最小宿主 | 固定 Godot 4.7.2 .NET/net10.0；自制资源；最小 C# 场景。只做来源审计及独立工程基础，不批量接入游戏内容 | 实际进程/原生导出均记录 Runtime 10；restore/build、真实窗口、中文、输入、音频、修改重载、退出重启的独立证据。缺项明确失败/未验证 |
| P1 确定性规则合同 | 纯 C# 命令、状态、强类型 ID、RNG、原子事务、回放；源行为代表夹具；最小内容 IR | 非法命令不消耗资源/RNG，重复命令幂等，规范化 hash 可重复；伤害、成长、捕获/战败代表案例通过 |
| P2 地图剧情纵切 | P0 已通过后，一张源地图及室内连接、NPC/文本、脚本事务、战斗返回、保存恢复；先用原创夹具验证技术 | 同源重复生成字节一致；未知语法/缺引用拒绝；真渲染、碰撞、输入、中文、音频及读档证据 |
| P3 第一徽章 | 自动生成首徽章可达闭包，补全三初始伙伴、支路、成长和异常分支依赖 | 完整玩家路径和异常路径通过；无脚本占位、悬空引用或人工修档；小刚奖励可幂等补领 |
| P4 首个阶段候选版 | 首徽章已完成范围的清洁重建、原生分发测试、迁移与回归 | 新环境复建/运行，保存损坏恢复、输入焦点、画面/音频与所有已有流程回归；此阶段不是完整游戏交付 |
| P5 关都扩展批次 | 月见山/华蓝、枯叶/圣安奴、岩山隧道/紫苑、玉虹/火箭队、金黄/西尔佛、浅红/狩猎地带等按原条件分批展开 | 每批来源→可达增量→缺失机制→内容→场景验收；原版分支顺序保持，已有流程回归持续通过 |
| P6 关都主线完成 | 红莲/双子岛、常青最终道馆、冠军之路、四天王/冠军/名人堂，及原版此时可访问的一至三岛剧情 | 新游戏到首次联盟正常通关和可继续存档；关键可选路、传说遭遇和后期解锁前置状态可验证 |
| P7 完整单机后期 | 全国图鉴门槛、红/蓝宝石任务、四至七岛、华蓝洞、游走传说、强化联盟、繁育及后期单机设施/支线 | 固定 sourceRef 的条件图和可达闭包完整，三初始伙伴后期分支、重复挑战/奖励/读档全部可恢复 |
| P8 完整单机候选版 | 以全游戏可达清单为分母，补齐地图、剧情、系统与表现；清洁原生交付验收 | 全量已实现/已验证矩阵，无阻断缺陷或无说明占位；原版差异、延后通信/配信和权利边界清楚 |

P5/P6 列表是工程批次，不强制玩家路线；七岛初访是否放入某一批取决于参考剧情条件，不能擅自移到联盟之后。通信设施修复剧情在 P7 必做，真实通信能力仍可延后。第一徽章验证的是架构和开发闭环，不改变完整单机终点。

## 每批内容的一致流程

1. 读取固定 source lock，明确具体地图、脚本、规则/资源及来源符号。
2. 求带条件的可达闭包：版本、初始选择、剧情 flags/vars、移动能力、物品渠道、成长/进化/繁育。避免混合互斥分支和配信条件。
3. 先实现新增规则及异常夹具，再允许依赖内容成为可玩状态。新增系统须有合同/ADR，不能在表现层补游戏规则。
4. 只读源解析到 IR，严格拒绝未知字段/opcode/special 与悬空引用；规范化生成两次字节一致。
5. 运行静态内容检查、领域/无界面场景、真实 Godot 及原生导出相关门禁。截屏、日志、回放与相同构建/内容版本关联。
6. 更新覆盖矩阵、已知差异、保存迁移和下一批前置条件；保留上一已通过提交与内容包，便于成对回滚。

## P0 期间可先推进的工作

- 源基线、路径/数据格式、权利元数据与范围清单。
- 与 Godot 无关的合同、确定性工具和严格数据校验器。
- 明确标为原创的技术夹具，例如两房间、对话/奖励事务，用于验证接口；它不是 FireRed 地图或剧情，也不能计入游戏完成率。
- 单张源 map.json 的结构接入研究；只输出已理解的结构和待转换引用，不能标记脚本、图块或内容包可玩。

P0 缺少音频设备、真实输入或原生运行证据时，保留准确阻断项；不伪造通过，不静默更换引擎/.NET 基线，不以 headless 代替真实体验。

## AI-first 交付合同

单次任务应提供：目标/非目标、前置门禁、允许文件、输入来源、公共 API、固定夹具、实际命令、通过/失败/未运行项和回滚办法。并行工作只修改不重叠模块；共享合同变更先协调。审查者独立查看来源和执行证据，不能仅相信实现者总结。

每个阶段的完成声明必须能追到构建版本、sourceCommit、内容包 hash、规则版本和相应日志/截图/回放。空测试集、跳过项或原创技术夹具通过，不能推导 FireRed 内容已完成。待可达分母生成前，不发布游戏内容完成百分比。

## Authorized resource optimization after Route22

The user explicitly approved handling atlases, caches and JSON together. After freezing the current Route22 gameplay milestone, implement compact runtime map data with development provenance separated, a bounded map/texture cache with ownership of patched layers, and consistent asset IDs, paths, source regions and reproducible importers; separate baked UI text from background art where localization needs it. Keep atlases grouped by actual use and measure loading/render costs before packing. A move from current precomposed map layers to TileMapLayer is a separate evaluation, not part of the current Route22 increment. Original English title and the five identified interface PNGs remain unchanged per the user’s decision.
