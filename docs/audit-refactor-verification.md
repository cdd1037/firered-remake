# Shared-authority refactor verification

This slice addresses the five architecture findings from the content-restart review. It adds no move roots (166 remain admitted).

## Changes and authority

1. Move invocation matching normalizes the single-battle receipt envelope into the canonical source receipt shape and uses one validator. The actual move definition supplies numeric identity; the resolved script ID and label prove presentation availability. Source-specific timing and target revisits remain explicit.
2. The battle scene owns one sprite registry and OAM order across active scripts and retired raw children. Callbacks and retained materials/palettes remain owner-scoped. Palette transfer is evaluated after tasks while OAM geometry stays latched before them.
3. Live, Quest main-map and Quest neighboring-map edits call the same metatile resolver. Imported package bindings are composed once; historical drawing no longer consults current story flags to choose a region-specific patch route.
4. Generic inventory opcodes share one authenticated suspended-source executor and canonical item catalog. Field-service callgraph ownership and powder currency/vendor callbacks remain separate. Four existing herb-vendor items are admitted through the maintained data recipe with `noFieldUse`; no new item effects or shops are introduced.
5. The obsolete Physical/Elemental pack readers and their support lists are removed. Rendering fixtures use the maintained dependency resolver; Application fixtures provide the actual generated script index and content definitions.

Final callsite inspection found no `SupportedSourceMoveId`, `SourcePhysicalAnimationCatalog`, `SourceElementalAnimationCatalog`, or regional `Try*Patch` implementations. All four production move-dispatch calls use the same resolved-script predicate. All three map-edit entrypoints call `PatchMapLayer`. The inventory service and generic fallback call `TryApplyScriptInventoryEffect`; powder no longer implements those primitives. Active and general animations share the same scene registry.

## Managed and producer evidence

- Full solution build: zero warnings/errors.
- Full Application: 1,662/1,662 passed.
- Full Presentation before the final fixture-only timing correction: 804/807 passed. The three failures were new scene fixtures that ignored the existing `loadspritegfx` wait; all 802 pre-existing tests passed. The correction advances with explicit bounds to actual allocation/OAM milestones and retains the assertions, including an allocated-object prerequisite for cancellation.
- Corrected Presentation focused gate: 88/88 passed, including all five scene tests, shared command/OAM/palette checks, Meteor/Water Pulse/Wish/Hail and all-root resource composition. After the final removal of the obsolete isolated residual APIs, a fresh complete Presentation run passed 807/807 (3.0725 minutes).
- Content metatile resolver: 4/4 passed.
- Maintained layout checks: 28/28 passed.
- Canonical current-items importer: 9/9; item-icon exporter: 5/5. Regenerated item catalog has 173 items, 15 shops and 22 selected icons; atlas regeneration used the existing producer.

The first Application run retained five failures: two fixture assumptions (explicit missing metadata and an unadmitted Enigma Berry) and three powder purchase cases exposing missing canonical herb definitions. The missing-metadata tests now explicitly pass null, Enigma remains rejected, and the vendor definitions use the canonical recipe. The complete corrected Application run above passed.

Native acceptance is recorded separately against the final fixed DLL. Dummy audio does not establish audible acceptance, and bounded Fuchsia/Quest checks do not claim a full campaign or every map's pixel equivalence.

## Bounded native check

The fixed pre-cleanup DLL (`bd8f0b41-a860-471c-a7d5-47b1fcd7907b`, SHA256 `f15cb5cd0500c57522ad5f42aa3762ed32a8ef3f3fc79e8319650f6e8d2eba8b`) passed cloud Linux/X11 Godot 4.7.2/.NET 10 acceptance with Dummy audio:

- Existing corrected Fuchsia fixture: startup field tiles and actors, dynamic fossil NPC, tutor offer → party selector → Back cancellation → normal field. Ten public commands accepted; teaching was not repeated.
- Existing Quest generation-30 fixture: Pallet → house 1F → Pallet historical sequence, then B exit to the saved live field. Sampling used time scale 0.03; this is not a normal-speed timing claim.
- No runtime error/exception, save unchanged, both processes explicitly closed. The fixture did not expose a connected-map seam; neighboring-map visual coverage remains limited to shared resolver/callsite evidence.

Evidence: `/workspace/shared/firered-refactor-native-20261017/REPORT.md`. The final cleanup removes two test-only obsolete residual APIs after this window; native evidence remains attributed to the exact DLL above rather than being relabeled as the later rebuild.

Final cleanup build: zero warnings/errors, 22.96 seconds. Final rebuilt Godot MVID `2f704cf7-eb15-4df7-9469-f88848a827ce`, SHA256 `8c99b1fcd447663b38bb0742c29bbd88cbccd087920efd350f888ff8a2bad49c`. `TickResidualSprites` and `ComposeResidualSprites` are absent from source and tests; headless tests explicitly own the same `SpriteScene` and normal field composer as production. No native rerun is claimed for this API-removal-only rebuild.
