# Field direction cadence

Reference: pinned pret `037335f4` `field_player_avatar.c`, `bike.c`,
`field_control_avatar.c` and `event_object_movement.c`.

`FieldDirectionCadence` tracks logical direction input and the remaining duration
of the accepted field action. `CampaignFieldDirectionView` is its only live-field
adapter; application commands and authoritative state remain unchanged.

- Foot/Surf turns wait 8 callbacks; bicycle face turns wait 1. Walking remains 16
  frames, running/Surf 8, ordinary cycling 6, with existing stairs and ledge timing.
- A direction change during an active move changes the next intent without
  restarting a whole step. Releasing/repressing does not erase active cooldown.
- Physical edges are collected before one field dispatch. All held directions
  resolve Up, Down, Left, Right, independent of event arrival order.
- Ordinary bumps use foot/Surf 32 and bicycle 16 frames. Cycling Road resolves the
  actual slope action: a source slope turn is 1, downhill movement 4 and uphill
  movement 16. A blocked slope has no ordinary bump animation and retries after 1.
- Cooldown time advances before modal early returns. Modal input clears remove
  logical direction ownership but do not cancel accepted time. Map loads reset
  both, preventing the old map's action from leaking into the new map.
- Failed Apply clears held intent and adds no invented cooldown. A failed automatic
  slope pull is latched until new d-pad/Shift input or a map reset, avoiding a
  per-frame failed-command loop. Rejected operations never become accepted motion.

Obsolete `_repeat` resets were removed from CampaignView partials. Independent
Teachy TV and animation repeat controllers were not changed. Pure tests cover
cadence, priority, turn/move distinction, failure and latch/reset behavior, with a
small view-wiring check for deferred physical edge dispatch. These are not a claim
of real native-window timing or a complete Cycling Road route replay. Existing
late-game route tests still require their separately validated checkpoint.

## Fresh reconstruction verification — 2026-10-20

The restored official Godot 4.7.2 .NET / .NET 10 toolchain built the complete
solution with zero warnings and zero errors. The fresh combined presentation
regression passed **186/186**, including **16/16** direction-cadence cases. The
new shared field-script suite separately passed **24/24**; these overlapping
workflow results are not added into a claimed full-game acceptance total.

The cadence cases cover existing action time through direction changes and
release/repress, source D-pad priority, modal-time advancement, map reset, failed
Apply, failed idle-pull recovery and actual Cycling Road action classification.
The view-wiring check proves input edges defer dispatch; it is not a measurement
of the native host's timing. No native Cycling Road route replay is claimed. Its
existing late-game application scenarios still require the separately validated
schema-3 checkpoint, which is unavailable in this restored workspace. New-game
fixtures do not replace that checkpoint. No export or audible-output acceptance
is implied.

The exact build command and source-script test evidence are recorded in
[shared field-script primitives](shared-field-script-primitives.md). The combined
presentation result is recorded in
`/workspace/shared/firered-recovery-evidence/presentation-final.trx`.
For a focused reproduction of the 16 cadence cases after that build:

```sh
source scripts/env.sh
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~FieldDirectionCadenceTests'
```

The 186-pass figure above refers to the executed combined presentation filter,
not this smaller reproduction filter.

### Subsequent collision correction

The native smoke check also exposed an older TV-tile collision fault, separate
from input cadence. The source-grounded bounded fix and inventory audit are in
[shared field-script primitives](shared-field-script-primitives.md#native-collision-finding-and-bounded-correction).
Its fresh complete-solution build had zero warnings/errors; its Domain regression
passed 17/17 and the expanded application suite passed 29/29. Those counts replace
the earlier 24-case shared-script scope for the additional collision work, not
the separate 16 cadence cases. The fix returns an accepted blocked movement,
allowing the existing 32-frame foot bump cadence to apply, without treating
unknown or unsupported passable tiles as ordinary walls. No input code was changed
after the verified cadence reconstruction.
