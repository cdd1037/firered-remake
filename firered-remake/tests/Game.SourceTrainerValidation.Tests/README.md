# Source trainer validation boundary

This focused suite authenticates the actual seven Fuchsia Gym teams and 37 Route12–15
single teams. It validates actual called results, base identities under Transform,
source history, type/ability overlays, toxic party status, EXP-first faint/replacement
boundaries, source item masks and malformed party mirrors. It does not claim a full
Metronome distribution, whole campaign route, native UI or audio verification.

Run from the repository root with the pinned official SDK and local package source:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.SourceTrainerValidation.Tests/Game.SourceTrainerValidation.Tests.csproj --configfile .toolhome/recovery-local-only.config -m:1 -nr:false
"$DOTNET_ROOT/dotnet" tests/Game.SourceTrainerValidation.Tests/bin/Debug/net10.0/Game.SourceTrainerValidation.Tests.dll .
```

The trainer and called-action contracts deliberately use different faint timing.
The actual trainer phase, HP, reserves, masks and mirrors are checked first. A
validation-only kernel view changes outcome/replacement fields to the immediate
whole-party phase while retaining both live combatants and every party member.
Existing pending switch data is validated as supplied. It is never repaired.

Only after those checks may a legacy trainer roster validator see an immutable
base projection. Fuchsia-route and new Gym rosters have independent exact per-slot
identity checks, including the cumulative source personality construction. A
changed held item is mutable source battle data, while original species, level,
IVs, EVs, personality, stats, friendship and original move identities remain strict.

Replacement AI also has an internal structural boundary. After a genuine player
switch or completed EXP transaction, the owner may still be choosing the next
published phase. The AI boundary authenticates actual party data, counters, masks,
kernel mechanics and exact trainer identities while leaving final phase/HP
relationships to the public command validator. The suite runs actual source
player replacements in Kirk and Koga fixtures, checks that their intermediate
state cannot be published, and verifies that AI can select a reserve without
altering HP, phase, masks or roster. Corrupt masks and immutable reserves still fail.

Final focused result: 12 groups, 139 assertions, zero failures on actual .NET
10.0.12; build has zero warnings/errors. `results-first.log` retains the earlier
pre-queue-hook pass. `build-initial-transcript.log` retains the first failed build's
tool transcript while a parallel AI file was incomplete; that result is not a pass.
