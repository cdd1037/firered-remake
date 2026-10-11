# Selected volatile recipes

Run with the official recovered .NET 10 toolchain. The command owns its own
bin/obj and .toolhome, reads the authenticated main content and local-only NuGet
config without modifying them, and serializes builds on the shared lock:

```sh
FIRERED_TOOLCHAIN_ROOT=/workspace/scratch/18c02ed4ed0d/firered-toolchains-recovery-20261011 \
 bash tests/Game.SharedSelectedVolatiles.Tests/run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_EVIDENCE_DIR
```

Required authored dependencies are Program.cs, source_probe.py,
source-prefix.h, source-main.c, the csproj and packages.lock.json. The frozen
singles-baseline.json and authenticated source-vectors.json are required and
nonempty. The runner recompiles unchanged pinned C bodies and compares its
result to the saved vectors. Do not replace singles-baseline.json to match an
edited implementation; its capture path rejects the migrated Domain assembly.

The suite covers 13 focused groups and 12 exact pre-edit singles result hashes.
It includes all four selected positions, permanent/copy gender, Oblivious,
Protect/Pressure/Substitute, fast/slow Taunt, repeated and overwritten moves,
Torment chosen caller history, Spite live PP and copy restoration, actual Rollout
cancellation, ordinary switch, an actual KO/EXP/replacement continuation, and
atomic malformed/stale commands. It does not enumerate combinations or rerun
campaign/native tests. The source probe has 21 command boundaries and makes no
claim to execute the whole original battle engine. Controller, RNG and gender
lookup are bounded stubs; permanent party lookup is executed unchanged.
