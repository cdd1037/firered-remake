# 当前架构与边界

项目是模块化单体，产品模块按职责组织：

- Game.Domain：BCL规则、状态与显式RNG，不引用Content、Application或Godot。
- Game.Application：应用命令、剧情／战斗／场外流程、事务与存档服务，引用Domain。
- Game.Content：读取转换资源并组装规则目录，引用Domain。
- Game.Audio：共享的BCL音频合成与资源读取，游戏、AudioTool和按需音频测试引用同一实现。
- Game.Godot：输入、显示、音频与平台接线，组合上述模块。
- Game.Tooling：显式开发工具，不是玩家运行必需入口。

Domain当前仍内嵌生成的训练家AI程序，因此“无产品ProjectReference”不等于“构建不需要生成输入”。资源恢复条件见[重建说明](resource-reconstruction-guide.md)。

## 状态与命令

领域状态采用不可变记录/集合。命令带期望revision与rules/content版本，拒绝过期或不相容的请求；只有完成的Accepted决定发布新状态。CommandId是跟踪标签，不是永久或会话内去重键，不保留旧命令状态队列。

一次性奖励、捕获与训练家结算依赖各自业务状态/flag防重。相同trace ID配上当前revision可能表达新的操作，不能把传输层重试规则误当业务幂等。

日常命令不应为了取证计算整份状态Hash。显式诊断、确定性比较与存档完整性在各自边界计算；保存校验不会随关闭日志而关闭。

## 战斗与时间

普通单/双打使用共享SourceBattleFrame与四位置模型。首个研究所战斗仍有独立FirstBattleRules教程执行器，狩猎等特殊模式也有各自业务状态；不要把普通内核统一写成“所有模式均已同一执行器”。

渲染不能决定伤害、RNG或战斗结算。当前游玩时钟按实际收到的逻辑tick提交；不会用墙钟补算漏帧。音频与动画负责表现及必要完成回调，不代替领域规则。

## 资源与内容

运行读取content/generated，不解析原作C/ASM或导入配方。规则目录尽量在加载时校验，玩家操作校验当前状态和合法行为。实际地图/脚本/物种/招式ID与事件分派路径保留；来源清单、历史阶段计数不能替代业务合法性。

普通训练家入口与结算使用注册的实际调用和业务身份；普通陆地遇敌从已加载表、地图属性和物种目录建立绑定。三种钓竿共享任务和表目录，普通与狩猎保留各自战斗语义，新增钓鱼地图由同一份转换数据接入。

当前仍有历史内容门槛、区域型服务和partial控制类需要收敛，见[质量审核](project-quality-review.md)。本页描述实际边界，不把建议架构写成已实现。

## 存档

存档包含schema、rules/content/edition、generation、payload和checksum。未知/损坏或语义非法状态应明确拒绝；同目录staged写入、验证有效primary后保留backup，不用坏primary污染好backup。

玩家在战斗或未稳定交互状态不能保存；诊断快照能力不是玩家随时存档许可。正常Continue不自动转换旧schema。显式离线转换写新路径并保留原件，见[当前组合](current-game-composition.md)。

完整功能与验证边界以[当前范围](first-clear-remaining-scope.md)为准；测试通过不代表自然全流程或真实音频已经验收。
