# Battle Berry Pouch Toss

The ordinary single/double battle pouch exposes USE / TOSS / EXIT, matching
`CreateNormalContextMenu` in pinned pret FireRed
`037335f4c725d7c9aecdac87066f2002b4bd7e14`, `src/berry_pouch.c`.
The three-option source window is variable window 12: interior tile (22,13),
7×6 tiles; the frame and mouse rows follow those bounds. Source menu navigation
is non-wrapping.

Toss uses the existing field `CampaignBagToss` transaction and presentation.
A temporary battle-origin berry menu holds the immutable battle-owner reference,
selecting actor and owned Pokémon identity. No alternate inventory engine,
battle action, battle hash, saved snapshot or RNG draw is introduced.
The owner supports wild and trainer single presentation and ordinary double
selection, including the second actor with a first actor selection pending.
Execution, results, forced replacement, tutorials and Safari do not own this menu.

Quantity selection precedes confirmation unless only one berry remains.
Confirming shows the result without removing inventory; A or B on the result
removes the chosen quantity and returns to the pouch. Cancelling quantity or
confirmation leaves the inventory and battle unchanged. The modal blocks battle
commands; its command wait identity and normal command revision reject stale
inputs. Closing it drops the temporary ItemMenu rather than entering a field menu.

Bounded application tests use controlled selection fixtures and assert that
all state except the acknowledged inventory transaction is identical, including
frame, pending double action and RNG. These are ownership/transaction tests,
not a claim of natural-route or native-window coverage.

Run after building the current solution:

```sh
source scripts/env.sh
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~BattleBerryTossTests
```

## Verification, 2026-10-16

- Targeted Application.Tests build: zero warnings/errors; 13/13 tests passed.
- Earlier fixture failures are retained in local logs; the final double fixture
  uses `SourceTwinsTrainerEncounter.Begin`, rather than changing a singles layout.
- The headless Godot probe found an existing double-battle resource-loader path
  mismatch. The loader now derives sibling asset paths from its loaded generated
  UI directory, so manifest paths and runtime validation agree.
- Final Godot product rebuild: zero warnings/errors (19.93 seconds).
- An isolated DLL-referencing Godot probe passed 30 checks and 10 real draw
  callbacks through `CampaignView` input routing for single and trainer-double:
  berry action popup, pointer selection of TOSS, quantity clamp, confirmation,
  result, B acknowledgement, last-stack removal and return to the original pouch.
  The double actor remained slot 2 and its partner's pending selection survived.
- Engine 4.7.2-stable, .NET runtime 10.0.12; headless display and Dummy audio.
  Draw-callback success does not verify rendered window pixels or actual audio.
  Product module ID: `64824045-94d3-4a1f-874f-61f454886aef`.
