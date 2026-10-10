# Shared source kernel executable tests

Run from the repository root using its installed SDK and local package cache:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.SourceSharedKernel.Tests/Game.SourceSharedKernel.Tests.csproj --configfile tests/Game.SourceSharedKernel.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceSharedKernel.Tests/Game.SourceSharedKernel.Tests.csproj --no-restore -m:1
dotnet tests/Game.SourceSharedKernel.Tests/bin/Debug/net10.0/Game.SourceSharedKernel.Tests.dll "$PWD" "$PWD"
```

Arguments are the project content root and authenticated canonical content root. Both are normally the repository root; the second argument is not the pinned C-source checkout. No SDK, content, or assets are duplicated by this project.

The program exits nonzero on failure and prints each group plus one JSON summary. It exercises eleven groups, including 660 exact singles action comparisons (state, event sequence, and RNG trace), true four-slot/ally identity, identical-species separation, full-field weather, authentic Pressure donors, shared KO, malformed payloads, catalog authentication and atomic unsupported paths. It does not regenerate golden expectations.

The original complete-singles regression remains independently runnable:

```sh
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceMechanicsParity.Tests/Game.SourceMechanicsParity.Tests.csproj --no-restore -m:1
dotnet tests/Game.SourceMechanicsParity.Tests/bin/Debug/net10.0/Game.SourceMechanicsParity.Tests.dll "$PWD" "$PWD/tests/Game.SourceMechanicsParity.Tests/baseline-1579dfc.json"
```

`1579dfc` identifies the historical source commit of that immutable baseline, not the number of cases.

Integrated weather combinations add nine exact action comparisons for permanent rain/sun/sandstorm through damage and recovery recipes, preserving serialized state, events and RNG. `./dev test-source-kernel` runs the already-built focused suite.
