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

This is not a claim of full GBA cycle/pixel parity. The initial text/cry/music delay remains bounded presentation timing rather than the original task scheduler. The sprite whitening and spray flash now use the source normal-fade scheduler: alternating BG/OBJ passes, delta-two coefficients, BG-only delay and five finishing callbacks. Spray start stops visible moving-palette cycling and blacks the moving background before the flash. Evolution installs a dummy HBlank callback and stops the prior scanline effect; its VBlank invokes the shared DMA service but does not configure a separate evolution scanline distortion. No invented scanline effect is added. Host RGBA blending still differs from RGB555 palette arithmetic, and intro/restoration terrain palette matching remains unverified. Hatch shell/shard timing retains its prior bounded 60 Hz implementation. Actual window-level ordinary cancellation, stone playback, hatch visuals, and audible output require native acceptance in addition to deterministic tests.

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
