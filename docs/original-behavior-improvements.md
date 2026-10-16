# 原作行为改进

本清单将用户明确批准的改进与原作兼容语义分开；原作素材和脚本不改写为修正版。

## 已批准：水之小径赫拉克罗斯纪录奖励（2026-10-16）

- 原作：比较时先写新纪录；奖励分支先置 `FLAG_GOT_NEST_BALL_FROM_WATER_PATH_HOUSE_1`，然后尝试赠送巢穴球。背包满时仍消耗这次纪录的领奖机会，同纪录不能补领。
- 用户批准：巢穴球实际成功入包才确认新纪录及领奖标记。满包保留旧纪录、旧标记，腾出空间后可以重交同一只。
- 实现：共享大小纪录服务的 `SuccessfulGift` 提交策略。待提交纪录仅存在于不可存档的交互场景；成功时背包、纪录、标记在同一个不可变状态事务内更新。原作提示文本、选择、尺寸比较、首次较小/相等奖励、后续较小/相等无奖语义保持。
- 来源：[House1 脚本](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/data/maps/SixIsland_WaterPath_House1/scripts.inc)、[共用尺寸算法](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokemon_size_record.c)。

## 待评估：12号道路鲤鱼王纪录奖励

该服务也有先提交纪录/标记、满包丢失捕网球的原作行为。本次批准仅针对赫拉克罗斯，因此鲤鱼王仍使用共享服务的 `Comparison` 原作提交策略。没有擅自扩展修正范围。

## 已实现：图鉴钓鱼分布查询的数组边界（2026-10-16）

- 固定原作 `wild_pokemon_area.c` 的 `IsSpeciesOnMap` 在未定义 `BUGFIX` 时，以 `LAND_WILD_COUNT`（12）遍历只有 `FISH_WILD_COUNT`（10）项的钓鱼数组，存在越界读取；源码自身提供了正确长度的修正分支。
- 重制版离线导入只使用10个已定义钓鱼条目，不复制越界内存内容。这依据用户允许在开发中修复明确问题的总体方向处理，未单独声称用户批准过这一具体条目。
- 修正范围仅是图鉴分布数据读取；钓鱼概率、可遇物种与实际遇敌规则不变。未据源码越界推定特定 ROM 上必然出现哪些错误标记。
- 验证：源表数量／第11、12项变异拒绝及代表钓鱼分布测试。见[图鉴说明](pokedex-habitats-and-area.md)。
- 来源：[固定原作查询](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/wild_pokemon_area.c)。
