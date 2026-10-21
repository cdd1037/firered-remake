# Evolution and hatch presentation

The shared evolution screen now stages ordinary and stone evolution using imported original moving tilemaps, all source palette-cycle rows, the original sparkle tile and pre/post species portraits (normal or shiny). The reciprocal affine-scale callback is ported from `evolution_graphics.c`: scales start at 256/16, alternate directions, and accelerate from 10 through 128. Spiral, descending-arc, inward-circle and post-evolution spray particles use the source integer sine table, trajectories and reciprocal OAM sizes. The spray emits the source eight-spark burst and 48 single sparks, skips the frame used to begin the return fade, and expires every spark after 128 callbacks. Its private deterministic visual seed never reads or advances gameplay RNG. Intro, morph, reveal, cry and completion fanfare are separate from result/move-learning pages.

The source hatch shadow map, tiles and palette replace the flat green placeholder. Existing nineteen authoritative shard choices, shell crack frames, original velocities, white flash, cry, completion fanfare, nickname choice and naming transaction are retained.

## Ownership and save behavior

- Presentation owns elapsed time and cosmetic pixels only. Ordinary evolution commits through the existing `evolution_choice` command after playback; B stops the pending proposal through that same command with choice 0 only while the reciprocal morph callback is active, matching the source cancellation window. No renderer changes species, stats, moves, inventory or RNG.
- Stone evolution is already committed atomically by the item command. Its screen cannot cancel it and never consumes another item or performs another evolution.
- Saved ordinary result pages resume as results without replaying or recommitting. Saved stone result page zero may replay cosmetic staging; the committed data is unchanged.
- Host stalls are capped at fifteen presentation frames per update so they cannot skip the entire morph cancellation window. Early/late B presses do not cancel; the stop hint is shown only inside the window. Held/echo confirm cannot skip the animation. Result confirmation waits for the fanfare.
- Existing application cancellation, multiple-party evolution ordering, pending move learning, HM retention and inventory validation remain authoritative.

## Source and rebuilding

Pinned source: pret/pokefirered commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`, `src/evolution_scene.c`, `src/evolution_graphics.c`, `src/daycare.c`, `src/trade_scene.c`, and the selected `graphics/evolution_scene` / `graphics/trade` inputs. The importer directly selects these source assets; the runtime reads only converted `content/generated/ui/evolution-scene` files. Two packed atlases contain all background palette variants and share their GPU uploads. They are released when the evolution/hatch scene exits.

Run `python3 scripts/imports/presentation/import-evolution-scene.py`. It validates indexed input geometry and converts indexed 8×8 tile references, horizontal/vertical flips, palette banks and RGB555 colors offline. It does not execute original repository scripts. New checks are `EvolutionScenePresentationTests` and `EvolutionSceneContentTests`.

## Remaining fidelity boundary

This is not a claim of full GBA cycle/pixel parity. The intro now follows actual printer/cry/SE completion and the shared normal-fade scheduler, as described below; the initial terrain entry fade and restoration pixels remain unverified. The sprite whitening and spray flash now use the source normal-fade scheduler: alternating BG/OBJ passes, delta-two coefficients, BG-only delay and five finishing callbacks. Spray start stops visible moving-palette cycling and blacks the moving background before the flash. Evolution installs a dummy HBlank callback and stops the prior scanline effect; its VBlank invokes the shared DMA service but does not configure a separate evolution scanline distortion. No invented scanline effect is added. The confirmed palette/alpha difference is addressed by the shared RGB555 follow-up below; intro/restoration terrain palette matching remains unverified. Hatch shell/shard timing retains its prior bounded 60 Hz implementation. Actual window-level ordinary cancellation, stone playback, hatch visuals, and audible output require native acceptance in addition to deterministic tests.

## Verification (2026-10-16)

Toolchain: .NET SDK 10.0.401, project targets net10.0 and Godot 4.7.2 .NET.

- `dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: passed, zero warnings/errors.
- `dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~EvolutionScene|FullyQualifiedName~OpeningNaming'`: 5 passed (3 evolution, 2 shared opening/naming).
- `dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~EvolutionScene`: 2 passed.
- `dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~EvolutionScene`: 1 passed.
- `python3 scripts/imports/presentation/import-evolution-scene.py --check`: passed without changing generated files. Five-file byte parity and rejection of an invalid source tile before any output are also exercised by the import test.

The seven requested evolution music/effect IDs exist in the converted cue catalog. This is an availability check, not an audible-output check.

## Native stone-evolution check

2026-10-16: a validated isolated Clefairy/Moon Stone fixture was driven through BAG → Moon Stone → Clefairy using actual window input. The scene completed as Clefable; final confirmation returned to BAG and Moon Stone quantity changed from two to one. Cry completion preceded the evolution fanfare (Dummy event evidence only). The run exposed an intro-name bug caused by the already-committed stone transaction; the adapter now captures the exact previous-state nickname rather than displaying the evolved default name, and duplicate intro-music dispatch was removed. That small correction compiled successfully; a new full frame-parity claim is not made.

## Current source-driven refinement

The added deterministic regression coverage checks the cancellation boundaries, split BG/OBJ palette updates and finishing delay, spray population/lifetimes/affine sizes, frozen background during reveal, seed isolation and sample/chunk independence. The shared serial build passed with zero warnings/errors; the subsequent full presentation suite passed 392/392, including all eight evolution tests. Importer regeneration and the independent evolution importer regression also passed directly. No new native-window or audible-output acceptance is implied.

The later combined naming/evolution build also passed with zero warnings/errors, and the focused EvolutionScene/OpeningNaming presentation run passed 13/13 (`/tmp/noncombat-focused-presentation.log`). The separate full-suite rotation/OAM fixture failures remain reported by their owner; they are not hidden by this focused pass.

## Shared RGB555 follow-up (2026-10-18)

Portrait whitening, spray sparkle whitening and moving BG1/BG2 blending now use the same five-bit palette arithmetic as battle composition. The existing scheduler exposes integer coefficients; its timing, particle positions, cancellation and Application commits are unchanged. The source's separate BG/OBJ updates remain visible, including the sparkle OBJ bank during the reveal flash. Two moving opaque layers use 8/8 channel blending before display conversion; the stopped black banks produce the same opaque gray during reveal.

Original pixel assets remain immutable. The scene captures each source crop once, caches coefficient-derived textures, and updates one reusable moving-field texture only when its palette/scroll key changes. Coefficient zero reuses the original portrait texture byte-for-byte, preserving the established RGB8 asset convention. The shared all-white endpoint remains available to NPC trade, whose actual presentation ownership now retains that cache until exit. No new importer schema, source input, scanline effect or universal rendering framework is introduced.

Independent tests cover every RGB555 color, all 17 white coefficients and both opacity states; per-channel alpha rounding/saturation; immutable inputs; transparent-layer behavior; source scroll sampling; and separate portrait/sparkle/background phase coefficients. The maintained importer check also verifies all reachable generated field samples are opaque, exact five-bit recovery, and 2,100 sampled scroll/blend combinations. This does not establish cycle-level hardware timing or native window/audio acceptance.

The first affected Presentation run passed 89/100; all eleven failures explicitly reported three missing ignored local reference files after environment recovery. Their maintained generators restored them. The primary-stat generator additionally exposed an obsolete source digest: its old local `battle_anim_dark.c` had one extra trailing newline. The current file matches the exact pinned Git blob `fbce95e301f5fe5e9e3ff344040595ee230192da`; the generator now verifies those bytes. Running the former generator against the preserved former file and the corrected generator against the pinned file produced identical numerical rows and all other metadata, apart from the corrected source digest. No callback, expected semantic value or tracked generated data changed. The first external comparison attempt used a path too shallow for the generator's repository-root default; relocating the unchanged script corrected that probe-only error.

The next refresh passed 99/100 and exposed the weather material fixture behind the now-restored motion fixture. The existing Weather preparation entrypoint generated the complete local fixture group and passed 43,693 independent assertions. This is explicit setup for affected regressions, not hidden C execution during ordinary tests. All regenerated numerical outputs remain ignored.

Final coherent solution build: zero warnings/errors, 43.77 seconds. Affected Presentation passed 100/100 in 25 seconds, including the 15 evolution scene/pixel cases and shared battle composition/palette/ownership regressions. Content passed 11/11 in 501 milliseconds, including evolution assets and the Easy Chat structural-validation regression. The maintained evolution import check passed with unchanged generated outputs. Godot 4.7.2/.NET 10.0.12 headless startup completed 180 frames and exited zero without runtime errors; MVID `0e401cd6-a24a-46d6-b74c-d8612207ab5d`. Dummy audio and headless startup do not prove real-speaker or window-level scene acceptance.


## Completion-driven intro follow-up (2026-10-18)

The previous fixed 90-frame introduction could start particles before a long cry ended or unnecessarily wait after a short cry. The automatic music selector also started the intro sound before the text/cry sequence. Evolution now has one cue owner: the existing text printer reaches EOS at the configured speed (with A/B acceleration), the previous species cry finishes, both ordinary SE players become idle after `MUS_EVOLUTION_INTRO`, then evolution music and the existing normal fade run before the spiral. Source authority is `evolution_scene.c:675–709`, `battle_message.c:2715` and the existing `sound.c:IsSEPlaying` semantics. The intro song is on source player 2, not a fanfare or background music player.

Each presentation step resamples audio after synchronous cue dispatch, so a multi-step host update cannot reuse a pre-cue idle snapshot. Disabled, failed or missing audio releases its cosmetic wait explicitly through existing fault/missing evidence; no guessed duration substitutes for unavailable playback. The text printer and palette fade still complete. Later particle/affine phases, cancellation window, ordinary evolution command ownership and already-committed item evolution remain unchanged. Initial terrain entry/restoration pixels are still outside this correction, and this is not cycle-level scheduler parity.

New deterministic cases cover printer EOS, short/long cry liveness, sound completion, cue ordering and snapshot refresh, missing/unavailable/faulted audio, text acceleration, ordinary/item cancellation boundaries, host-rate independence and stall caps. Existing phase, pixel and text regressions remain in place; their setup now explicitly drives the introduction instead of assuming 90 elapsed frames.

Validation: the coherent full solution build passed with zero warnings/errors in 36.93 seconds. Focused Presentation passed 37/37 (186 milliseconds), including all 12 new intro cases plus existing evolution scene/pixel and text-printer regressions. Application item/evolution/lifecycle/retention checks passed 47/47 (17 seconds). Godot headless startup ran 180 frames and exited zero without runtime errors, MVID `33b87bb1-8e7e-443d-a817-2a021a3d6565`, .NET 10.0.12. No audio synthesis code changed; actual-speaker and window-level acceptance remain unverified.
