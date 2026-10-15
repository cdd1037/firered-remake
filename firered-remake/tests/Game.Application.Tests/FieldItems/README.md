# FieldItems

背包与进化石接线：以当前存档为输入，在内存中准备场景，覆盖道具消耗、Repel、学习器、进化石、Everstone 与错误请求的原子拒绝。

```powershell
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --filter FullyQualifiedName~FireRed.Tests.Application.FieldItems.
```

输入路径和 `FIRERED_TEST_CHECKPOINT` 配置见[测试说明](../../README.md)。
