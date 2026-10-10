# Authenticated called scalar-damage recipes (B04)

This test project exercises exactly the 25 B04 source move IDs in `members.json`, through a real learned Metronome PP slot whose caller does not know the executed move. It uses the complete authenticated 354-move catalog and the explicit `ResolveCalledMoveAction` contract. It does not select a random Metronome result or run an entire battle turn, and it does not expand ordinary Fuchsia v1 admission.

Pinned source: `pret/pokefirered` commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

The independent C# oracle transcribes the source LCG, command order, exact PP-command position, and integer damage for legal Clefairy/Rattata fixtures without calling production RNG or damage helpers. Every B04 member has a successful, meaningful HP result, exact full raw RNG trace, final seed/cursor, caller PP, source metadata identity, and a validated resulting state. Separate cases cover both sets of all 64 Hidden Power IV-bit patterns, physical/special damage and resulting type, friendship endpoints, HP power scaling, Present's damage and healing boundaries, Psywave rejection, strict OHKO chance/level/Sturdy/immunity/Endure, Jump Kick misses/Protect/crash cap, recoil, False Swipe, Endeavor, Pain Split, Triple Kick hit termination, critical/secondary boundaries, weather/Light Screen, restore/resume, both actors, and atomic overflow rejection.

Source command anchors:

- `data/battle_scripts_1.s`: generic Hit 239, OHKO 761, crash 848, Psywave 1207, Pain Split 1249, Triple Kick 1380, Return/Frustration 1648, Present 1655, Hidden Power 1735, Facade 2247, Smelling Salt 2263, Endeavor 2474, Eruption 2488, recoil 2562, absorption 4041.
- `src/battle_script_commands.c`: accuracy 1008, critical 1170, damage multiplier 1209, type 1270, random/adjust 1558, HP tracking 1744, certain secondary 2780, crash 6465, OHKO 7104, Psywave 7557, Pain Split 7674, friendship 8228, Present 8238, Hidden Power 8503, HP difference/scaling 8973/8986.
- Hidden Power's `F_DYNAMIC_TYPE_1` intentionally keeps metadata Normal for damage-category tracking. Its actual type is used for damage and landed-type history, but Fire Hidden Power does not thaw a frozen target. Charge checks source metadata and does not double Electric Hidden Power. These are pinned source behavior, not later-generation fixes.

## Reproduce

From the repository root:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" restore tests/Game.CalledScalarDamage.Tests/Game.CalledScalarDamage.Tests.csproj --source "$FIRERED_TOOLCHAIN_ROOT/godot/Godot_v4.7.2-stable_mono_linux_x86_64/GodotSharp/Tools/nupkgs" --ignore-failed-sources
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.CalledScalarDamage.Tests/Game.CalledScalarDamage.Tests.csproj --no-restore -m:1 -nr:false
"$DOTNET_ROOT/dotnet" tests/Game.CalledScalarDamage.Tests/bin/Debug/net10.0/Game.CalledScalarDamage.Tests.dll .
```

This uses the already provisioned official .NET 10 toolchain and local package source, without a network restore or new runtime bootstrap.

## Acceptance boundaries

- Metadata authenticated: all 25 members, against the full exact source catalog.
- Handler registered: all 25, in called context only.
- Source success/failure scenarios executed: the matrix above, with precise command/RNG assertions.
- Shared interaction regression: current Protect/Endure, source types/immunity, Water Absorb/Rock Head/Sturdy, weather/Light Screen, caller PP/history, valid serialization/resumption, and atomic rollback are covered.
- Application/native exercised: **no**. The production Metronome selection/full-turn path and natural trainer entry remain disabled until complete closure.

Unresolved cross-batch obligations are explicit: Substitute HP interception and Pain Split/Smelling Salt/False Swipe gates; Lock-On and additional Fly/Dive semi-invulnerability; battle-effective type/ability overlays; actual physical/special damage history for Counter/Mirror Coat; complete faint/replacement/party continuations; new weather/side-condition end-turn integration and full existing-effect regression; item/ability fixtures not admitted by the bounded species/item catalog (for example Focus Band, Pressure, Volt Absorb and Cloud Nine). The helper's Focus Band command path exists but is not certified by an invalid or fabricated legal fixture. Mirror Move's six transitive results, including Counter/Mirror Coat/Struggle, are separate closure work. No successful build or finite move enumeration is presented as full 336/342 gameplay closure.
