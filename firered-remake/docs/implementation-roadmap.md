# 完整单机目标的实施顺序

状态（2026-10-12）：已进入关都分批扩展，普通共享战斗核心、七徽章应用主线、狩猎地带及 Surf/Strength、西尔佛、火箭队基地、宝可梦塔、两处卡比兽和有限地区服务已分别验收；首次七岛自然应用路线已完成救回迷失女孩、交还陨石和返回红莲，并严格存读。生产表现适配器已完成渡船、三岛地图／城镇地图、F5／继续和训练家开场的93项检查；其发现的Joy胜后对白缺口经窄修与独立11项／4命令回归关闭，实窗／物理音频另列。常青第八道馆已通过真实应用路线：九名训练家实际胜利、第八徽章／TM26、坂木离场与存读，另有130项开场和22项终档的分批headless生产表现检查；后期22号道路劲敌已在真实败退后正常重试取胜并完成重复触发防重与存读（463b9b3）；当前正在实走23号道路／冠军之路，联盟仍未验收。这不代表所有已载入地区交互完成。下表保留阶段门禁定义，实际边界见各阶段证据及覆盖清单。最终范围见 [scope.md](scope.md)，源冻结见 [source-baseline.md](source-baseline.md)。本路线不承诺缺乏测量依据的日历工期。

## 2026-10-12 本轮最终停止点

用户最新明确要求“今天做到重构后就先停下，等我后续安排”（Sentinel_fd7407e28f488191aded15e93aa3fffd）。本轮执行顺序固定为：首次主线通关／名人堂与可继续存档 → 有边界的项目级重构和精简 → GitHub 同步及完整 Drive 备份 → 清理已确认可重建的临时材料 → 暂停开发，等待用户后续安排。

这一决定覆盖此前通关后自动推进七岛后期的安排。下表 P7/P8 保留完整项目的未来范围，但本轮不启动全国图鉴、通关后七岛、强化联盟或其他新阶段。重构集中于已实现主线中的重复地区适配、共同规则接口、临时兼容分支、启动／构建负担及可证明冗余；保留玩法与合法存档迁移边界，不为减行数重写已验证系统，不无限扩充功能。交付时列明精确停止提交、已验收范围和仍未完成的玩法／地区／专属表现。

## 阶段与退出证据

| 阶段 | 工作边界 | 退出证据 |
| --- | --- | --- |
| P0 工具链与最小宿主 | 固定 Godot 4.7.2 .NET/net10.0；自制资源；最小 C# 场景。只做来源审计及独立工程基础，不批量接入游戏内容 | 实际进程/原生导出均记录 Runtime 10；restore/build、真实窗口、中文、输入、音频、修改重载、退出重启的独立证据。缺项明确失败/未验证 |
| P1 确定性规则合同 | 纯 C# 命令、状态、强类型 ID、RNG、原子事务、回放；源行为代表夹具；最小内容 IR | 非法或旧 revision 命令不消耗资源/RNG，业务一次性 flags 保持，规范化 hash 可重复；伤害、成长、捕获/战败代表案例通过 |
| P2 地图剧情纵切 | P0 已通过后，一张源地图及室内连接、NPC/文本、脚本事务、战斗返回、保存恢复；先用原创夹具验证技术 | 同源重复生成字节一致；未知语法/缺引用拒绝；真渲染、碰撞、输入、中文、音频及读档证据 |
| P3 第一徽章 | 自动生成首徽章可达闭包，补全三初始伙伴、支路、成长和异常分支依赖 | 完整玩家路径和异常路径通过；无脚本占位、悬空引用或人工修档；小刚奖励可幂等补领 |
| P4 首个阶段候选版 | 首徽章已完成范围的清洁重建、原生分发测试、迁移与回归 | 新环境复建/运行，保存损坏恢复、输入焦点、画面/音频与所有已有流程回归；此阶段不是完整游戏交付 |
| P5 关都扩展批次 | 月见山/华蓝、枯叶/圣安奴、岩山隧道/紫苑、玉虹/火箭队、金黄/西尔佛、浅红/狩猎地带等按原条件分批展开 | 每批来源→可达增量→缺失机制→内容→场景验收；原版分支顺序保持，已有流程回归持续通过 |
| P6 关都主线完成 | 红莲/双子岛、常青最终道馆、冠军之路、四天王/冠军/名人堂，及原版此时可访问的一至三岛剧情 | 新游戏到首次联盟正常通关和可继续存档；关键可选路、传说遭遇和后期解锁前置状态可验证 |
| P7 完整单机后期 | 全国图鉴门槛、红/蓝宝石任务、四至七岛、华蓝洞、游走传说、强化联盟、繁育及后期单机设施/支线 | 固定 sourceRef 的条件图和可达闭包完整，三初始伙伴后期分支、重复挑战/奖励/读档全部可恢复 |
| P8 完整单机候选版 | 以全游戏可达清单为分母，补齐地图、剧情、系统与表现；清洁原生交付验收 | 全量已实现/已验证矩阵，无阻断缺陷或无说明占位；原版差异、延后通信/配信和权利边界清楚 |

P5/P6 列表是工程批次，不强制玩家路线；七岛初访是否放入某一批取决于参考剧情条件，不能擅自移到联盟之后。通信设施修复剧情在 P7 必做，真实通信能力仍可延后。第一徽章验证的是架构和开发闭环，不改变完整单机终点。

## 2026-10-10 新增机制补齐阶段（第五徽章后、下一主线阶段前）

用户明确要求当前浅红／第五徽章大阶段完成后，先统一补齐第三世代战斗机制，再开始下一主线阶段（Sentinel_76315a35c9a08191b91aa6d95fdcdbe5）。当前第六阶段继续完成原定到达、挥指真实闭包、预设双打和阿桔验收，不因新阶段计划中断正在进行的集成。

新增阶段以 354 个招式及 76 个正式第三世代特性为完整分母。特性表共 78 项，NONE 与未使用的 CACOPHONY 仅保留数据兼容，不计入正式玩法；场外特性效果按火红适用规则实现，不自动加入绿宝石新增或后世代机制。挥指的 336 个直接候选和 342 个含间接结果只是其中一个调用闭包，不意味着其余 12 招未实现，也不等于 354 招已完整验收。

- 按共用效果与状态依赖拆分剩余招式、特性、跨回合、换人、双打和招式×特性交互；先核实已有实现，再列真正缺口，避免另造 called-only 实现。
- 规则完成与表现完成分别统计。该阶段同步梳理并接入对应动画／音效，可用专用测试战直接触发并检查不同体型、站位、单打／双打，无需等待自然遭遇对应物种。
- 通用动画不能算专属表现还原；缺失与临时通用替代逐项可查。特性只做火红原本所需提示、动画与声音，不为每个特性虚构独立特效。
- 验收必须包含实际执行、失败原子性、队伍与个体身份、随机消耗及可恢复中断点。导入定义或单个效果用例通过不能代替完整回合与应用验证。
- 风险与工作量按尚未覆盖的效果组、状态组合、AI、场外规则和表现资源拆分；完成第五徽章时形成基于实际缺口的清单，不承诺未测量的工期。
- 用户随后要求同时盘点战斗其余规则并做有收益的重构（Sentinel_7b21925b20048191909462fbbd26d242）：出手优先级／速度／同速 RNG、回合末调度、双打目标／补位／胜负、切换／逃跑／捕获／道具／携带物、经验／EV／升级／学招／战后结算、训练家 AI／合法行动、状态持久化及表现。矩阵逐项区分已有实现、真正缺口与需要组合验证，避免重做已通过模块。剩余小中项同阶段收齐，大独立块先报告工作量与依赖。
- 重构围绕共用规则复用、删除重复分支和清晰 Domain／Application／Presentation 职责；保留有效回归，规则与对应表现／声音尽量一次接线和验证（Sentinel_7f5c4a1d123481919358d007e14eb344），不做缺乏收益的全系统重写。

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

## 2026-10-10 region-completion schedule

A main-route pass and a complete region interaction set are separate statuses.
After Rocket Hideout and Pokémon Tower, prioritize Celadon, Lavender and their
connecting routes: ordinary NPC dialogue, signs, bookshelves/computers, visible
and hidden pickups, doors and mechanisms. Add each region’s shops, services,
gifts, in-game trades and side rooms according to their dependencies. Recheck
earlier towns in the same pass. Reuse the authenticated script interpreter and
common environment-text dispatch before writing per-object special cases.
High-cost minigames remain explicit later work; imported maps/warps do not count
as implemented interactions. The current inventory is in
[regional-interaction-coverage.md](regional-interaction-coverage.md).


## 2026-10-10 backup cadence and current region milestone

Small verified code increments continue to the private GitHub repository; major playable stages continue to private Drive. Per the latest user decision, backup preparation checks package completeness/integrity and uploaded bytes/hashes, but does not create another local recovery environment or repeat builds, gameplay replay or save/Continue acceptance solely for packaging. Development tests remain selected by the actual code change.

The bounded regional milestone now covers both Route12/16 Snorlax blockers,
ordinary loaded-map dialogue/environmental text, Tea/four gates, Eevee party/PC,
ordinary Department Store 2F purchases, all store floors/stairs/elevator, hotel/
house/restaurant ordinary dialogue and Lavender Name Rater. The store's TM shelf,
Counter tutor, stones/dolls/mail, vitamins/X items and roof vending/exchanges,
Coin Case/currency/prizes, Itemfinder-specific rewards and original preset double
battles remain explicit gaps. Mainline acceptance does not mean every regional
interaction is complete; see the per-floor coverage ledger. Next is a bounded
Route12 south → Route13/14/15 → Fuchsia Center connection, then Koga/fifth badge
and the associated ordinary facilities. Safari and its HM/story dependencies are
planned separately; no fifth-badge acceptance is claimed by this closeout.

## 2026-10-10 Fuchsia arrival increment

The southern corridor now reaches the original Fuchsia Center through a retained
1,046-command natural lineage, including eleven ordinary trainer wins and the
actual Route12 Snorlax removal. Transform's permanent identity, capture/PC and
battle-only projection have separate controlled regressions. The precise current
boundary and evidence are in [Fuchsia arrival](fuchsia-arrival.md).

The next blockers are genuine Metronome closure, preset two-versus-two battles
and Koga's new rules/city services. Source metadata and isolated called-action
recipes are being implemented for reuse by ordinary learned moves; they do not
activate a partial random pool. Region pickups and facilities remain recorded
work rather than being implied by map rendering. This node is an arrival
checkpoint, not fifth-badge or whole-route completion.

## 2026-10-10 第五徽章单打检查点

浅红七训练家、阿桔四只队伍、第五徽章、TM06教学和治疗返程已经形成真实公共命令链；Sheila/Celia保留原阵容完成自然挥指。Godot实际162条阿桔段、原生胜后对白／徽章卡／保存与新进程Continue分别留证。详见 [fuchsia-gym.md](fuchsia-gym.md)。

预设双打仍未接通，是既有承诺的明确余项，不把这次第五徽章单打检查点称完整南路或完整第六阶段。下一机制阶段优先解决四槽状态／目标／替补，并按 [battle-system-completion-matrix.md](battle-system-completion-matrix.md) 的23个依赖包补齐354招式、76特性及其它规则与表现。进入下一主线地区之前先完成本阶段的核心规则与关键回归；按 2026-10-11 的最新阶段出口，专属动画／音效继续并行，不再要求全部专属表现先验收。主线所需表现优先接入，明确的通用效果可以过渡，但真实规则与结算不可省略；不等待备份上传才开工。

## 2026-10-11：规则阶段出口与表现并行

用户明确同意规则完成后并行推进主线（Sentinel_23a5c65a12ac819190dd9441bae2d3fa）。阶段出口是 354 招式、76 个有效第三世代特性在既定普通单打／预设双打中的实际规则及关键交互回归，并包括合法选择、行动与回合末、切换替补、奖励学习、道具／捕获、状态持久化等当前核心合同。不能用定义已导入、recipe准入或测试数代替这一出口。剩余特殊战斗模式与场景依赖按矩阵单列，Safari／漫游等较大独立块要报告其实际缺口，不能借核心出口暗称完成。

核心规则收稳后恢复后续主线；专属动画、声音和视觉对照保持独立并行。主线即将用到的表现优先，尚未专属还原的招式可使用明确登记的通用效果，不阻塞真实结算已完整的可玩链。通用效果不计为专属还原，动画和声音进度仍分别统计。批量播放／资源与结束检查覆盖重复结构，每类代表和特殊单双打构图做实际窗口抽查，不逐招式新建测试工程。

### 2026-10-11 ordinary battle core exit

The shared ordinary-core integration and bounded refactor passed the affected rule, Application, strict provenance-save and actual menu-host checks recorded in [the checkpoint](../evidence/held-obedience-items-20261011/README.md). Mew/Deoxys use ordinary OT/badge obedience; modern-fateful provenance remains save metadata without battle effect. Dedicated animations/audio continue independently.

Resume the campaign with Fuchsia Safari's actual special action/step loop and necessary Surf / Gold Teeth-to-Strength rewards, then Saffron/Silph. Safari capture must have its own source contract rather than reuse ordinary ball behavior. Continue ordinary regional interactions alongside the route. Other declared special modes and later NPC-trade provenance remain content-dependent work, not implicitly completed by this core exit.

### 2026-10-11 Safari rewards and field HM continuation

Safari admission, special capture,600-step exit, Gold Teeth/HM03 and the Warden HM04 exchange form a real public campaign chain; a native natural Rhyhorn catch and fresh-process Continue are separately accepted at28a. See [Safari campaign](safari-campaign.md). Strength at the source Warden boulder and Surf across already loaded mainland/Safari water are the current bounded field-HM successor; their own [Strength](field-strength.md) and [Surf](field-surf.md) records separate source/module, combined and native acceptance. Later boulder holes/current modes remain explicit boundaries.

Next, use the existing Tea/Fuji prerequisites for actual Saffron gate entry, Silph teleport pads/Card Key doors/elevator, rival/Giovanni and Lapras/Master Ball, then Sabrina. Region imports retain source topology and do not imply completed story interactions.

### 2026-10-12 seventh badge and first Sevii continuation

The [Cinnabar Application route](cinnabar-campaign.md) reaches Badge07 and TM38
through original Pallet Surf, Lil/Ian double battle, Mansion switches/fall/Secret
Key, six quizzes and actual four-member Blaine combat. The2,099-command
checkpoint lineage ends after Bill's No and strict save/restore. Production
headless drawing/menu checks pass; native screen/audio remain explicitly open.

Bill's first visit to One Island, Two Island and Three Island now completes its
natural public-command route at the original pre-League time, including
Celio/Meteorite/Tri-Pass, sequential bikers, Joy/Meg, Lostelle/Hypno and the
Cinnabar return with strict saves. See [First Sevii](first-sevii-campaign.md) for
the retained multi-build lineage and separate presentation limits. Continue
Viridian's eighth gym, then Route23/Victory Road and the League.
Seafoam, Cinnabar Lab services and Good/Super Rod remain explicit regional work.
Dedicated move animations remain tracked and deferred under the user's latest
priority, without blocking complete rules.

Per the latest2026-10-12 user direction, continue normal small GitHub source
checkpoints; produce the next complete Drive package after this first Sevii
visit, not a separate seventh-badge package. Backup work remains structural and
byte-integrity checks, with no extra gameplay replay solely for packaging.

## 2026-10-12 主线完工后的项目级重构与精简

用户明确要求主线完工后再做项目级整理（Sentinel_5fca4d441efc8191a4950762c6bfb345）。该阶段合并重复逻辑、地区适配和临时兼容分支，清理可替代的临时实现与冗余资源，梳理共同资源和构建入口。保留必要玩法、来源证据和存档迁移边界，以维护成本与可恢复交付为衡量标准，不以减少行数为目标。当前首次七岛及后续主线继续推进，不提前展开这轮全项目改造；已有普通战斗核心的有界重构不替代此阶段。

这里的主线出口指第八徽章、冠军之路、四天王与冠军、名人堂及可读的通关存档。完成这轮项目级整理后继续通关后七岛等剩余内容；首次联盟前七岛仍按当前剧情时机完成。


## 2026-10-12 backup cadence after the first Sevii visit

The first-Sevii full Drive package proceeds as agreed. The next full Drive backup is scheduled after the first main-story completion and its project-level refactor/simplification, not separately at the eighth badge or Champion. Verified recoverable source checkpoints continue to the private GitHub main branch more frequently, without waiting for an entire badge or large region. Packaging continues to avoid extra local recovery builds or gameplay replay.


## 2026-10-12: late Route22 rival checkpoint

The earned eight-badge party now completes the original six-member late rival,
including a retained actual loss and legal retry, source scene4/removal, single
prize, real return/reentry and strict save. The natural final generation is37105.
See `docs/league-rival-campaign.md`; Route23 eight guards, Surf approach and
Victory Road Strength mechanisms are the next active bounded node, followed by
the League. No separate full Drive package is planned before main-story completion
and the agreed project-level refactor; source checkpoints continue normally.
