# Canonical wild Application checks

Run against the current content root and a strict current checkpoint produced by the explicit offline converter. The committed `evidence/result.json` records the bounded cases and check counts; fixtures are deliberately controlled and are not a full region replay.

```sh
source "$CONTENT_ROOT/scripts/env.sh"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.CanonicalWildApplication.Tests/Game.CanonicalWildApplication.Tests.csproj --configfile "$FIRERED_TOOLCHAIN_ROOT/NuGet.OfficialLocal.Config" -m:1 -nr:false
dotnet tests/Game.CanonicalWildApplication.Tests/bin/Debug/net10.0/Game.CanonicalWildApplication.Tests.dll "$CONTENT_ROOT" "$CURRENT_CHECKPOINT" tests/Game.CanonicalWildApplication.Tests/evidence/result.json
```

The harness also writes an ignored `field-fixture.json` beside the result for the short Godot view host. It modifies only source-authenticated field position, encounter saturation, roamer location and ordinary ball inventory. Later transient combat fixtures use reflection only for initial setup, then submit public campaign commands. Source-scene fixtures start real source scripts and assert their real completion callbacks. They are not submitted as player saves. No legacy campaign session is instantiated.
