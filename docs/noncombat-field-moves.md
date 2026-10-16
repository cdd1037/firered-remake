# Noncombat field moves

## Current implementation (2026-10-16)

The Party action menu now exposes known Dig, Teleport, Sweet Scent, Soft-Boiled, and Milk Drink, alongside the existing HMs. Field use does not require or consume PP. Eggs cannot supply a field move. Dig, Teleport, and Sweet Scent do not require the user Pokémon to have HP.

- **Dig** uses the same generated escape-route eligibility and arrival resolution as Escape Rope. Its confirmation, show-Pokémon cue, departure, map entry, and arrival are correlated phases. It does not consume an item.
- **Teleport** uses the source town/city/route/ocean-route map types and the recorded last healing location's exterior warp. All 20 generated heal destinations remain available regardless of Fly-region or visited-town menu filtering. Teleport neither heals nor changes PP.
- **Sweet Scent** has the shared show-Pokémon cue, red palette transition, source 64-frame hold, and reverse fade on failure. Encounter generation requires a source wild header and the current tile's land/water encounter attribute. It honors the Tanoby unlock, tries the roaming Pokémon before the ordinary terrain table, and reuses normal land/water/Safari battle construction. It bypasses walking encounter rate, cooldown, Repel and ability checks without taking a step. Starting the battle resets walking encounter immunity as the source battle-start task does. Failure uses the imported `EventScript_FailSweetScent` message.
- **Soft-Boiled / Milk Drink** share one donor/recipient implementation. The donor must have strictly more than `maxHP / 5`; exactly that amount is deducted, while recipient healing is capped at missing HP. Self, egg, fainted, and full-HP recipients are rejected. Status and PP remain unchanged. The Party UI highlights the donor and animates donor loss then recipient healing at one HP per frame, with the two source item sounds.

## Shared boundaries

`NoncombatFieldMoveRules` owns move identity and HP-transfer rules. `SweetScentEncounterRules` adds only the source no-rate candidate generation path; existing battle factories remain authoritative. Dig and Escape Rope share `EscapeRopePolicy.ResolveArrival`. Escape Rope, Dig, Teleport and Fly share travel reset/warp behavior and existing generated destination definitions. No map-ID eligibility allowlist was introduced.

Party reordering, Fly and the five noncombat moves share one stable-field admission predicate. A field-effect callback carries its expected phase wait ID, owner identity and origin. Healing requires the selected recipient's instance identity, while the command revision protects both party slots; an old recipient selection or old revision cannot silently heal another Pokémon or apply a transfer twice. The renderer retains the donor's instance identity during target selection.

The runtime consumes the generated content catalog. No source parser, network access, or raw source lookup is used at runtime.

## Source evidence

Pinned reference: `pret/pokefirered` commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

- `src/party_menu.c`: `SetUpFieldMove_*`, Dig/Teleport confirmation, healing target restrictions and HP update callbacks.
- `src/fldeff_dig.c`, `src/fldeff_teleport.c`, `src/fldeff_softboiled.c`, `src/fldeff_sweetscent.c`: field effect setup and callback behavior.
- `src/overworld.c`: `Overworld_MapTypeAllowsTeleportAndFly`, travel resets and `lastHealLocation` warp.
- `src/field_effect.c`: departure/arrival tasks and show-Pokémon effects.
- `src/wild_encounter.c`: `SweetScentWildEncounter`, ordinary candidate generation and Tanoby/roamer ordering.
- `src/battle_setup.c`: `Task_BattleStart` encounter-immunity reset.
- `src/palette.c`: normal fade update phases/delay; `src/data/heal_locations.json`: exterior destinations.

## Verification

Focused tests cover confirmation/cancel, all imported Teleport destinations, Dig/Escape Rope destination equivalence, item/PP/HP preservation, blocked source map types, stale callbacks and recipient commands, both healing rule variants, Sweet Scent land/water/Safari/roamer/failure branches, Repel bypass, and bounded presentation clocks.

The combined full solution build passed with zero warnings and errors (61.20 s). The application regression run passed **103/103**, comprising **12 new noncombat cases** and 91 existing Dunsparce Tunnel, Outcast Island, Pattern Bush, Treasure Beach and Cycling Road cases (49.04 s). Two initial test failures were invalid indoor fixture map IDs, corrected to the existing `map.players-house-1f`; the test project then rebuilt cleanly before the passing run. The existing domain inventory/Escape Rope regression class also passed **6/6** with `--no-build`.

Commands: `dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`; the application test command used `--no-build --no-restore` and a class filter for `NoncombatFieldMoveTests`, `DunsparceTunnelTests`, `OutcastIslandFlowTests`, `PatternBushFlowTests`, `TreasureBeachTests` and `CyclingRoadFlowTests`.

The travel renderer is a bounded source-task projection, not a claim of pixel- and scheduler-identical emulation. Native screenshots/interaction inspection for these newly added effects remain a separate presentation check until recorded here.
