# TM Case and PC hand presentation

The runtime reads only `content/generated/ui/inventory`. The offline importer uses the retained, pinned pret/pokefirered source at `037335f4c725d7c9aecdac87066f2002b4bd7e14`. No upstream program runs and no assets are uploaded or published.

## Source behavior

- `tm_case.c`, `HandleLoadTMCaseGraphicsAndPalettes`: separate menu background and case foreground, with the player's male/female palette. The disc is drawn between those layers so it disappears into the case rather than fading over it.
- `SetDiscSpritePosition` and `SpriteCB_SwapDisc`: all TM/HM positions use the source 58-position ordering; the previous disc descends 20 pixels in 10-pixel steps, changes type/shape while hidden, then rises. Close lowers the disc. Rapid selection changes replace the requested disc without showing an intermediate wrong type.
- `sWindowTemplates`, `List_ItemPrintFunc`, `PrintMoveInfo`, and `DrawMoveInfoLabels`: five 16-pixel rows at `(80,10)`, source NO/HM glyphs, quantity for TMs only, type/power/accuracy/PP and four-line descriptions. Both gender skins, the selected-description palette, type palettes and menu arrows are converted directly. Keyboard/mouse list hit areas share this geometry and retain a bounded viewport offset. The edge-scroll policy is shared with Move Tutor: hovering or clicking an already visible row never recenters it, crossing a visible keyboard boundary scrolls it, and reopen/row removal resets or clamps it. Teaching, giving, confirmation, compatibility and HM restrictions remain existing application operations.
- The Teachy TV case uses this same renderer with its supplied temporary inventory, its description-shaded state, and the source darkened tutorial disc. It does not mutate the player's case selection or teaching commands.
- `pokemon_storage_system_data.c`, `CreateCursorSprites` / `MonPlaceChange_Grab` / `MonPlaceChange_Place`: the original four 32×32 hand frames, normal/multiple palettes and shadow replace the previous square held marker. The open hand moves down eight single-pixel steps, closes on Grab, and lifts the icon; Place lowers the held icon, opens the hand, and raises it alone. Idle uses the source 30-frame bounce pair.
- `pokemon_storage_system_graphics.c`, `ShiftMons`: 16 steps exchange icons along source sine offsets, with vertical movement on odd steps and draw priority exchanged halfway. The hand returns to the closed frame. Multi-grab/place uses the corresponding eight-pixel lift/drop on the whole rectangle, preserving its holes.

The originally cited storage task lines around 1422 concern item transfer. Creature grab/place/shift animation is in the data/graphics functions above; the creature workflow continues to use its existing authoritative transactions.

## Transaction and lifecycle boundary

`StorageMoveAction` first executes the existing application command. Only an accepted result starts presentation. A rejected or stale command produces the existing notice and creates no animation. Presentation never places, removes or shifts a creature itself.

While a short transfer is visible, the adapter retains only immutable arrays for the currently visible box and party, the affected location/group cells and the two icon selections. It does not retain a campaign snapshot or unrelated boxes, and creates no per-frame copies. These references are released when the transfer ends, the storage view changes, or core presentation is disposed. Input is briefly gated during the committed visual transfer so another action cannot target a half-presented slot; normal controls resume afterward. PC exit/save and held-creature restrictions remain application rules.

## Rebuild and bounded checks

```sh
python scripts/imports/presentation/import-inventory-presentation.py
python scripts/imports/presentation/import-inventory-presentation.py --check
python scripts/checks/test-inventory-presentation.py
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~InventoryPresentationTests
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --filter FullyQualifiedName~InventoryPresentationImportTests
```

The Python oracle independently checks every TM Case tile pixel for both palettes, foreground transparency and the shaded description region, every disc/hand pixel, exact shift sine samples, deterministic generated-only outputs and malformed/changed source rejection. The pure C# tests cover TM/HM positions, disc reselection/Close, grab contact timing, place and group drop, the shift arc/priority midpoint, five-row scrolling/Close hit geometry, the native scrolled TM39 hover→click regression, bounded viewport reset/row removal, and reset/reentry at every animation frame. Existing storage and teaching command tests remain the rules regression boundary.

Native Godot visual/input verification and real audio output are separate acceptance gates. Pure timelines, pixel conversion checks and a successful build alone do not establish those gates or a full natural campaign replay.

## Bounded native review

The controlled current-codec fixture review confirmed the male case layers, five-row stats/list, HM-to-TM disc change, Close clearing, popup/back, and PC Grab/Shift/Place stable poses and exit. It exposed a scrolled hover-to-click selection drift: deriving the viewport from the newly hovered cursor moved the row before confirmation. The shared edge-scroll policy now retains the viewport, and the exact scrolled TM39 pointer sequence passed a subsequent native recheck. Full Presentation passed 430 cases after this correction.

This review sampled stable hand poses rather than exact shift-curve frames. Female/empty-case and group/party native checks remain unperformed; the source pixel/timeline tests cover their bounded automated behavior. The native audio driver was Dummy, so no audible-output claim is made.
