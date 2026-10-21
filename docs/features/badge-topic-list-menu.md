# 华蓝徽章话题列表

固定来源：pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`。本批补已挂载的华蓝 House1 交互，不新增地图、物种、物品或徽章规则，也不改字体／音频。

## 实现

- 原 NPC、前后门、八种徽章说明和退出对白已在世界／story 资源中；之前未实现的是 `ListMenu`／`ReturnToListMenu` 原生列表回调。
- 从原 `src/field_specials.c` 的标签与配置导入九项顺序、四行可见列表、位置与高度。第九项 Exit 返回 8，B 返回 127；普通话题暂停同一窗口，返回时保留所选项和滚动位置。列表不循环绕回。
- 八种说明仍直接执行原脚本，包括攻击／速度／防御／特攻特防、服从等级与对应场外技能文字；这里不新增或修改这些技能的玩法规则。
- 菜单定义进入既有 story 资源。共用 `StoryListMenuRules` 处理原 special／waitstate 回调，应用适配器仅接管有真实目录定义的 native list；西尔佛电梯继续走独立业务，不按地区名添加准入分支。
- 临时列表状态非空时禁止保存；Exit／B 后清空并恢复场景控制。空列表状态不进入正常存档 JSON。
- 复用既有菜单视图、键鼠输入和字体。四行滚动、源上下阈值、可见行命中与滚动提示通过共同菜单呈现类实现；暂停期间仍显示窗口但只允许原对白继续。

## 验证与边界

- `StoryListMenuTests`：任意来源位置的目录列表、原 special→waitstate 回调、暂停／返回、Exit／B、非法选择原子拒绝、未挂载 native list 保留原处理。
- `BadgeTopicListMenuContentTests`：generated-only 加载、源顺序／布局、无西尔佛列表抢占、坏布局与重复 source ID 拒绝。
- `SourceVerticalMenuTests`：四行滚动阈值、上下边界、指针可见行映射、返回选中项保留滚动；既有静态列表行为回归。
- `BadgeTopicListMenuTests`：受控有效代表档位置，经公开命令完成全部八项、退出／取消、再次交谈、前后门和严格存读；非法选择不得改变状态，悬空菜单上下文不能保存。
- `scripts/checks/test-story-menus.py` 与 `import-story.py --check`：本地固定源导入及生成资源一致性。

验证使用受控有效起点和有界公开命令，不代表从新游戏自然到访、原生窗口逐帧还原或真实扬声器输出已验收。
