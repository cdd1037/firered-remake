# NPC trade scene

## Source and ownership

Pinned `pret/pokefirered` revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`, principally `src/trade_scene.c`, `src/pokeball.c`, and `src/data.c`.

An important source distinction: `CB2_InitInGameTrade` allocates a zeroed scene and does not set `isCableTrade`. Consequently NPC trades execute `DoTradeAnim_Wireless`, even though this is an entirely local single-player exchange. The wireless-adapter imagery here does not add network functionality. The source's cable option is a multiplayer presentation branch, not the correct default NPC scene.

The existing Application transaction still exchanges the two exact instances once. The view only reads the recorded outgoing/incoming instances, renders them, and sends the ordinary acknowledgement after the ending A/Enter input. Neither drawing nor elapsed time creates, replaces, or re-grants a Pokemon. All nine trade definitions continue through the same scene. Source text retains its original OT/outgoing/incoming variable binding.

## Implemented presentation

- Shared converted trade/hatch shadow layer and 60-frame, three-pixel slide-in
- Source send/bye/received/take-care text order and battle message box
- Shared standard Poké Ball opening/closing atlas and native particle pixels; returning/emerging Pokemon
- The trade-specific 12-frame spinning ball atlas and exact imported 108-entry signed bounce velocity table
- GBA affine zoom and vertical scrolling, adapter image and screen flash
- Outbound/inbound source signal palettes and the original 101-frame signal task sequence
- Link-mon travel, glow/shadow, opposing crossing portraits and opening/closing white center column
- Arrival bounces, release, received-message hold, take-care hold and final explicit A/Enter

`NpcTradeSceneTimeline` is pure presentation data, independent of Godot and all party/RNG/inventory state. It exposes phase-start positions for audio coordination and a readiness boundary of 1,665 logical frames. It folds state-only main-callback handshakes into adjacent presentation phases; this is not a claim of cycle-exact GBA emulation. Cry duration additionally belongs to the audio owner. Final field acknowledgement must not use the old 360-tick automatic timer.

## Generated resources and regeneration

Run:

```
python scripts/imports/presentation/import-npc-trade-scene.py
python scripts/imports/presentation/import-npc-trade-scene.py --check
python scripts/checks/test-npc-trade-scene-import.py
```

Raw inputs stay in the pinned local source tree. Runtime consumes only `content/generated/ui/npc-trade-scene/scene.json` and `atlas.png`, plus existing shared Pokemon/capture art. The eleven atlas regions include two 4×4 signal-palette sheets; they are source palette states, not one image per animation tick. No runtime source paths, hashes, inventory locks, or original code execution were added.

The importer also corrects the GBA 512×256 shadow-map screen-block arrangement. The shared `scripts/lib/presentation_pixels.py` tilemap decoder and palette conversion are invoked offline only. Source files downloaded for the initial cable investigation remain raw research inputs; the generated console and affine maps select the actual wireless NPC variant.

## Verification on 2026-10-16

- `source scripts/env.sh && dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: passed, zero warnings/errors, 73.51 seconds.
- SDK `10.0.401`; installed target runtime `Microsoft.NETCore.App 10.0.12`.
- `dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~NpcTradeScenePresentationTests -m:1 -nr:false`: 3 passed, zero failed/skipped.
- `python scripts/checks/test-npc-trade-scene-import.py`: 2 passed. Recreated atlas/JSON are byte-identical; source signal/velocity data, source NPC branch and visual transaction isolation checked.
- Atlas visually inspected as a local PNG. This is asset inspection, not native scene playback.

Audio phase cues, incoming-cry completion gating, final fanfare gating, canonical portrait-facing flags and original species Y offsets have since been integrated; these final changes await the next integration build. Native gameplay timing/render/input acceptance and actual audio output need parent integration evidence. Tests above do not certify a natural playthrough or full pixel/timing parity. The original nine exchange-transaction tests are separate from this presentation-only suite.

### Isolated native-check fixture

`/workspace/shared/firered-npc-trade-visual/TEST-SYNTHETIC-HATCH-1000-before-reyley.json` is a separate synthetic test fixture, not user progress. It derives from the already disclosed synthetic lifetime-hatch-count-1000 fixture. Shared storage Grab/Shift/Place rules put existing Abra instance 67 (PRIZE) in party slot 0 and preserve Mewtwo in box 0 slot 2. No Pokemon was created, no completion flag was cleared, and all original save files remain unchanged. The test location is Route 2 House `(7,3)`, north toward Reyley `(7,2)`.

`ValidatePlayerSave` passed. An isolated executable with copied product references then used public commands `interact`, two `ack(Choice:1)` steps, and `npc_trade_choose(PartyIndex:0,TargetInstanceId:67)` to reach the genuine presentation wait. It did not save that post-exchange state. Native controls: A to interact/advance, YES, first party member. `/tmp/npc-trade-fixture.log` records the probe. Existing `NpcTrade` Application tests separately passed 4/4.

## Actual window check

2026-10-16, MVID `64302ba8-7caa-4c08-a564-211869c5b329`: from the explicitly prepared pre-Reyley checkpoint, public interaction selected Abra PRIZE. Native scene progressed through all logged phases to Ready, B did not cancel, final A returned to the original post-trade dialogue and field. Party UI showed MIMIEN in the first slot afterward. Cry completion preceded MUS_EVOLVED and the field music resumed on return; this is Dummy event evidence, not audible output. The initial slide-in and final received/care scenes were visually inspected; intermediate transitions have phase logs but not exhaustive pixel screenshots. No original checkpoint was modified.
