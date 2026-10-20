# Shared field-script primitives

The reconstruction after the workspace reset was checked against pinned pret
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. It reconstructs behavior, not the lost
commit object. Source authority is `src/scrcmd.c`, `src/money.c`,
`src/event_object_movement.c` and the original map scripts.

## Dispatch and behavior

- Magikarp's service handler claims only its own source file. Its 500-money price,
  existing-gift prevention and delivered-gift-before-debit checks remain intact.
  It can no longer claim Mom, the television or the museum's generic opcodes.
- Read-only gender scripts share the player-gender fallback. It validates the
  operand shape and gender range, without assigning ownership to another NPC.
- Every background-story entry initializes `VAR_FACING`, including early-map
  television interactions and walking into signs, not only regional-flavor maps.
- Mt. Moon's actor-copy handler claims only its own source. Other scripts share
  the original live-object-to-template coordinate operation. Inactive or absent
  objects are no-ops; active invisible objects retain their real coordinates.
- Money-box/check/debit fallback is shared. Museum admission remains 50, with the
  original refusal and insufficient-money branches. Ordinary subtraction clamps
  to zero like `RemoveMoney`; service-specific checked transactions remain first.

A fresh static audit found 14 gender operations in 11 source files, 9 money-box
openings in 8 files and 10 actor-copy operations in 7 files. The source owners are
Mom/TV, Route 12, SS Anne, Bill, Tower, Copycat/tutor, Silph, Warden and Magikarp;
Day-Care, Trainer Card, Safari, Bike Shop, Magikarp, rooftop vending, Game Corner
and Museum; and Pallet, Mt. Moon, Victory Road, Selphy's house and Seven Island.
This inventory is research evidence, not a runtime region allowlist.

## Regression boundary

`SharedFieldScriptPrimitiveTests` starts from current new-game content and
controlled positions, then uses public interaction/move/ack commands for Mom,
both TV branches, the sign lady and Museum payment/refusal. Primitive and service
boundary tests check malformed data, active/inactive actor copies, saturating
money removal and the retained Magikarp price/delivery guards. They do not use,
create or claim a late-game acceptance checkpoint. Fresh execution results belong
to the recovery verification record; historical passes are not reused.

## Fresh reconstruction verification — 2026-10-20

The restored official Godot 4.7.2 .NET / .NET 10 toolchain built the complete
solution with zero warnings and zero errors. The newly compiled
`SharedFieldScriptPrimitiveTests` suite passed **24/24**. This includes the public
Mom, TV, sign-lady and Museum branches and the retained salesman guards described
above. An earlier filtered run against the pre-test binary correctly failed for
an empty suite; it is not counted as a pass.

The result is bounded to these fresh fixtures and commands. The separately
validated late-game schema-3 checkpoint is still unavailable in the restored
workspace. These tests neither replace it with a new game nor establish full
late-game/Cycling Road route acceptance, native-window visual correctness or real
audio output. No export was performed for this reconstruction.

Commands executed from the repository root:

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~SharedFieldScriptPrimitiveTests' --logger 'trx;LogFileName=shared-script-final.trx' --results-directory /workspace/shared/firered-recovery-evidence
```

Fresh evidence: `build-final.log` and `shared-script-final.trx` under
`/workspace/shared/firered-recovery-evidence`. These local evidence files are not
runtime resources or an exported game package.

## Native collision finding and bounded correction

A fresh native smoke check found a pre-existing movement fault in the upstairs
bedroom (`map.players-house-2f`): moving west from `(7,4)` toward the TV at `(6,4)`
returned `unsupported-source-behavior`, leaving the player at `(7,4)`. This was a
movement command, not a Mom interaction or the downstairs TV dialogue path.

The target is source metatile 53, `MB_TELEVISION` (`0x86`), collision 1 and
elevation 0. The recovered `ConnectedFieldRules` validated target behavior before
collision. Before this additional correction, the file still exactly matched
baseline `024e07e` (Git blob `5a885fb4d14c65f81506bbea70f795b9a9717f19`). Thus this
was not introduced by the restoration. The user subsequently requested that
encountered problems also be fixed.

Pinned `event_object_movement.c:4830` returns `COLLISION_IMPASSABLE` for solid
collision bits. The source declaration group `MB_COUNTER` through
`MB_TRAINER_TOWER_MONITOR` (`0x80..0xa3`) has 36 explicitly named interaction types
and no reserved holes. The shared movement rule now returns an ordinary blocked
result for this category **only when collision is nonzero**. It does not make
these tiles walkable, alter A-button scripts, add map/region exceptions or treat
arbitrary unknown behavior as a wall. Unknown `0x7f`, `0xa4`, `0xff` and passable
unsupported furniture still fault. Destination validation, north-door dispatch
and matching-ledge override remain intact.

A read-only scan of all 414 generated map/layout files, representing 395 map
identities, found 2,369 solid interaction cells of 33 used types across 265 map
identities. Of these, 1,513 cells across 194 identities use types newly covered by
this bounded collision branch. These are content inventory counts, including
layout variants, not a claim that every cell was reached in a native route.
Peers include bookshelves, shop shelves, PCs, wall maps, cabinets, kitchens,
dressers, machines and windows. No content or source behavior IDs were rewritten.

`SolidInteractionCollisionTests` has 17 cases, covering all 36 declared types in
four directions for collision values 1/2/3 and cycling, plus unknown/passable/
malformed rejection and door/ledge precedence. Five new public application rows
repeat bumps into the actual upstairs TV from three sides, a bookshelf and a
dresser, then retreat normally. They assert unchanged position, steps and RNG
while blocked, no dialogue and no new active script. The prior Mom/TV interaction
tests remain unchanged. Fresh post-correction verification is recorded below;
the earlier 24-case result predates these five additions.

The original native failure evidence remains in
`/workspace/shared/text-menu-native-recovery-20261020/field/runtime.log`.

### Post-correction verification — 2026-10-20

The complete solution rebuilt with **zero warnings and zero errors** after the
solid-interaction correction. Fresh results:

- `SolidInteractionCollisionTests`: **17/17 passed**.
- `SharedFieldScriptPrimitiveTests`: **29/29 passed**, including the five real
  bedroom collision/recovery cases and all 24 earlier script regressions.

Evidence: `build-collision.log`, `collision-domain.trx` / `collision-domain.log`
and `collision-application.trx` / `collision-application.log`, all under
`/workspace/shared/firered-recovery-evidence`. The source correction is included
in commit `2356109`; evidence documentation was updated afterward.

Focused reproduction after the serial build documented above:

```sh
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~SolidInteractionCollisionTests'
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~SharedFieldScriptPrimitiveTests'
```

This closes the automated reproduction of the reported TV collision and its
bounded same-class cases. It does not claim a new full native-window pass,
complete region traversal, late-game checkpoint acceptance, real audio output
or a game export.
