# Genuine four-slot action switching

当前入口：`dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter FullyQualifiedName~FireRed.Tests.Battle.Switching.`。参见[测试组织](../../README.md)。

以下保留原验收与参考向量的历史说明；旧 shell、console DLL、锁定还原命令和计数不再是当前测试入口，历史套件名称也不表示当前仍有对应项目。

See `docs/source-four-slot-action-switches.md` for the shared recipe, original source contract, continuation authority and remaining boundaries.

Run `bash tests/Game.Battle.Tests/Switching/run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_OUTPUT`.

The runner verifies real short complete turns, six bounded original-C source command comparisons, and exact state/event/RNG output against immutable old singles source `92a781d`. It copies source only into the isolated evidence directory, reuses authenticated content and tools, and takes `/tmp/firered-remake-build.lock` only during each build. Failure logs from development are retained separately from final acceptance. The actual four-slot suite requires the compiled source oracle path as its third argument; the parity suite takes content root and a new output JSONL filename.
