# Original content engineering fixtures

当前入口：`dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --filter FullyQualifiedName~FireRed.Tests.Content.`。参见[测试组织](../../README.md)。

以下保留原验收与参考向量的历史说明；旧 shell、console DLL、锁定还原命令和计数不再是当前测试入口，历史套件名称也不表示当前仍有对应项目。

These two files are authored solely for this project's technical tests. Neither is a FireRed map, character, item, species, dialogue or story. Passing these tests does not count as game-content coverage.

- `original-two-rooms.json`: schema-1 content bundle, two 5×5 rooms with reciprocal exits, two static NPCs, a once-only reward flag, original Chinese text and minimal item/species records. The species record is metadata only, not a playable battle creature.
- `original-pret-map-shape.json`: an original document using the supported subset of the upstream map.json shape. Identifiers such as MAP_ENGINEERING_ROOM are intentionally synthetic. Its unresolved layout/script/map names are reported by the structure importer, never accepted as a playable content bundle.

The `ContentFixtureVerifier.csproj` console test harness verifies the positive fixtures, deterministic round trips and failure cases. It exits nonzero for any failure or an empty suite. Run from the repository root:

`dotnet run --project tests/Game.Content.Tests/Fixtures/ContentFixtureVerifier.csproj -- tests/Game.Content.Tests/Fixtures`

Use the repository's pinned environment before running that command. The verifier changes no external source or fixture and never runs upstream repository scripts.

Schema limits are explicit engineering limits: 8 MiB JSON, depth 64, maps up to 256×256, scripts up to 4096 instructions, static NPCs and finite typed opcodes. Hidden-item source event unions, dynamic warp targets and unknown fields are rejected. A script's structural path to end is checked; runtime still needs a step budget and transactional reward/flag handling. This library does not execute the IR.
