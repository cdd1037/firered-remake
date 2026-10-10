# Recovered Route11 tests

Context-reconstructed from the 2026-10-09 battle worker. The old17groups/2519checks
were reported passing before reset; this is not a fresh execution claim.
This reconstruction has those17 test areas plus incoming WaterAbsorb/Damp switching.

Requirements: integrated PretRoute11Pokemon.Load(root) catalog79/base209moves,
Gym overlay210moves, PokemonFactory79, AllowFreezeStatus and Route11 Domain overlay.

From the integrated repository:

```sh
source scripts/env.sh
python tests/Game.Route11Battle.Tests/source_oracle.py /path/to/pokefirered --check
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.Route11Battle.Tests --configfile tests/Game.Route22BattleAudit.Tests/offline.NuGet.Config -p:NuGetAudit=false
flock /tmp/firered-remake-build.lock dotnet build tests/Game.Route11Battle.Tests --no-restore -m:1 -nr:false -p:NuGetAudit=false -p:UseSharedCompilation=false
dotnet tests/Game.Route11Battle.Tests/bin/Debug/net10.0/Game.Route11Battle.Tests.dll "$PWD"
```

The oracle does not import product code. It freshly checks source HEAD, nine source
file hashes and branch anchors, then derives16 small fixed RNG vectors. It explicitly
inherits the prior approved secondary comparison correction<; no other balance change.
Use shared compilation disabled because the previous environment produced a stale
Domain assembly despite a successful incremental build.
