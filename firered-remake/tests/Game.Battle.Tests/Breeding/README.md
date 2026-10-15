# Egg factory bounded runner

当前入口：`dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter FullyQualifiedName~FireRed.Tests.Battle.Breeding.`。参见[测试组织](../../README.md)。

以下保留原验收与参考向量的历史说明；旧 shell、console DLL、锁定还原命令和计数不再是当前测试入口，历史套件名称也不表示当前仍有对应项目。

Use the official environment from `scripts/env.sh`. Generate the original-source
oracle with `python3 tests/Game.Battle.Tests/Breeding/probe_source_iv.py REFERENCE_GIT
OUTPUT_JSON`. The script reads the pinned original C function and compiles only
the bounded oracle; its generated file is not an authored source dependency.

Build this project against the current Domain/Content and run its DLL with
`PRODUCT_ROOT PRODUCT_ROOT OUTPUT_JSON RESULT_JSON`. The current Content must
include the breeding species and presentation packages. The runner uses the
current authenticated owner and does not need a historical 174-species build.
The original component result and the adapted-current result are separate
cohorts in the integration evidence.
