# Ordinary field transition presentation

This guide defines the shared live/recorded field presentation boundaries. Source references use local pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`. Admission comes from accepted typed movement receipts, actual endpoint metadata and generated art, never a map-name allowlist. Gameplay commits before presentation: detached origin/destination projections use the same map/actor/palette renderer as recap, without rerunning movement, damage, RNG or scripts. Input/saves remain locked while a controller owns the transition; observed play time is committed after release.

## Connected edges and camera ownership

Only an exact accepted `ConnectedEdge` receipt with a matching loaded source connection may rebase the pre-step player pose into destination coordinates. Ordinary walking/running/Surf/bicycle then uses the existing pixel-step sampler and unclamped player-following `OrdinaryFieldCamera`; a map change does not snap an extra tile or recenter a small map. Map/weather/cache initialization and menu cleanup still run, but held directions and movement cadence survive this ordinary step. Source `field_camera.c:CameraUpdate` retains sub-tile offsets; `fieldmap.c:SetPositionFromConnection/CameraMove` rebases coordinate origins.

Completed connected Surf mount/dismount jumps are excluded: their map switch follows the existing 32-frame jump, so another interpolation would move backward and replay movement. Authored arrival `Motions` retain their controller. Connected Quest Log replay remains an immediate marker, not newly reconstructed pixel motion. Arbitrary authored arrival motion and neighbor-NPC persistence are not generalized by this correction.

Ordinary actors and live camera share the same floored tile position; Surf retains the walking sprite's baseline. Stair sprite x2/y2 offsets do not move the logical camera. Door, teleport and fall controllers own their covered switches/endpoints. Bill/Navel Rock camera objects return and retire before normal input/battle ownership. Full-screen return snapshots remain logical 240×160. These ownership constraints preserve intentional jump/shake/fade motion.

Seagallop ferry and wake keep the source center in both directions. Godot negative-width draws flip UVs without moving the destination origin, so eastbound drawing must not add the sprite width. `seagallop.c:CreateFerrySprite/CreateWakeSprite` supplies center coordinates and eastbound `hFlip`; speed, wake timing, palette/audio and voyage commands are unchanged. Engine mirror proof is separate from a full natural voyage test.

## Shared palette, music and ordinary warp

`FieldWarpPresentation` is the palette scheduler shared by ordinary live warps, common-fall and recorded playback. BG and OBJ banks update on separate callbacks. Departure waits for both palette completion and actual requested music-stop completion: an unchanged song is ready while playing, and absent/faulted audio cannot strand the cosmetic controller. Source indoor/outdoor music-fade speeds remain 2/4. Minimum immediate departure and normal plain arrival each consume 24 callbacks; arrival retains exit/weather/palette ordering.

The base same-section class requires a nonempty matching region section and NONE/SUNNY map headers, proving no preview request under `fldeff_flash.c`. Broader cave, preview, stair and weather classes below have their own explicit predicates. Walking-avatar checks use the origin as well as destination, so an eager destination bicycle/Surf reset cannot disguise an unsupported source gait.

Recap UI retains its source hue but participates in all-palette black fade and final window mask. Playback visits source callbacks even across slow host frames so phase cues are not lost. Source callback counts are not decompression/DMA duration or wall-clock load guarantees.

## Door programs and material lookup

### Animated doors

`FieldDoorPresentation` follows `field_fadetransition.c`, `field_door.c`, held-walk, palette and barn-door callbacks. Departure opens, walks north, hides the player, closes, requests fade/music, then transfers after both readiness gates: minimum 81 callbacks, later if music still stops. Arrival also takes 81: delay-3 palette/outward window reveal starts before opening at callback 26; southward walk begins 46; close begins 60 and overlaps the final two walking pixels; release is 81. Live and recap share frames, visibility, displacement and wipe.

Both animated endpoints require generated art and stable walking-avatar states. Runtime looks up `(layout, metatile)`, with three complete replacement BG layers per open frame and source-selected normal/sliding sound. The recipe includes the 23 real small-door styles, including the dynamically unlocked Rocket Warehouse; source-unused Fan Club art is excluded because its metatile was reused for the non-door S.S. Anne harbor warp. No invented fallback art is allowed.

Normal held walk uses eight callbacks per foot/standing command. Arrival resets the source gait seed. Departure inherits the general field renderer's missing alternating-foot command-index state: cadence/displacement are defined, but inherited left/right foot fidelity is not. Historical recordings do not guess it.

`From` is south of the departure door. Gameplay has already committed arrival `To` south of the door; `ArrivalAnchor` is the authentic door cell, from which detached rendering walks to `To` without moving saved state twice. Representative public routes are Pallet rival house `(15,8)→(15,7)`, Viridian house `(25,12)→(25,11)` and Center `(26,27)→(26,26)`; authentic arrival flags distinguish already-visited from first-arrival captions.

### 1×2 and nonanimated doors

Normal S.S. Anne and the five source elevator styles reuse the same controller/lookup with 16×32 frames. The upper metatile is at `anchorY−1`, lower at the anchor; all replacement layers cover both cells, and closing restores captured layers. Source large/small frame durations match; large offsets advance eight tiles rather than four. Sharing dimensions alone does not admit cable-club/teleporter procedures.

`BuildDoorTiles`' large lower call passes `paletteNums+4`, exceeding its declared eight-byte array for the blank tile0 layer. The importer requires that primary tile0 be entirely transparent and emits transparent black only for that undefined lower layer; it never reads adjacent memory. Defined pixels and the upper blank layer keep their real palettes. This is an invisible-layer safety interpretation, not a visible retail-bug claim. Editing palettes above 15 follow pinned gbagfx's low-four-bit conversion before applying the tile's runtime map palette.

Ordinary nonanimated same-section NONE/SUNNY doors use the accepted step anchor/direction, not previous facing or independently chosen arrival facing. `field_control_avatar.c` finishes the normal step before `DoWarp`: 16 pixel callbacks, fade/music/SE_EXIT request at 17, earliest transfer 40. `Task_ExitNonAnimDoor` hides at 1, finishes palette 22, obtains weather readiness 23, reveals/walks at 24, reaches final pixel 39 and releases 41. Only departure extends for music; no opening sound or barn-door wipe is added. Immediate `DoWarp` also emits SE_EXIT.

Optional existing door anchors represent entered/automatic-arrival cells. Caption time continues during the initial 16 walking callbacks, pauses only over authenticated control lock and clears visually at map reconstruction. Cinnabar Lab `(13,6)→(13,5)` and Lounge `(7,9)` exercise this route without new artwork.

## Recording, captions and compatibility

Only the exact accepted receipt and its newly emitted provisional map marker may receive a completed observation. The producer's exact same-command arrival/departure caption may be authenticated with its payload, arguments, provisional start, scene/receipt owner and committed flags. Reconciliation occurs in the next accepted transaction or a pure immediate-save projection; rejected commands do not publish it. There is no direct state setter or separate revision.

Timing keeps its eight-byte logical base allocation. A non-plain arrival adds one logical byte; each optional typed door anchor adds four, with north-facing departure and south-facing arrival geometry validated against the recorded segment player including intervening movement. These are logical budget units, not JSON size. Arrival kind is independent of departure cause; default/absent fields remain omitted. Newly observed stepped plain departures/stair behavior facts are explicit, never inferred from destination identity. Authentic same-map warps can record a marker.

Overflow closes recording with the whole original unknown marker/caption intact rather than truncating actual audio wait or partial geometry. Older, unobserved and unsupported transitions remain unknown and switch immediately on replay. Live/recap share phase samplers, but replay uses the recorded switch boundary rather than current audio. Multiple captions, unsupported script/input/movement tails and source-disabled maps do not acquire fabricated timing.

`overworld.c:1420–1444` runs Quest playback only when player controls unlock, except scene ending. The exact first-arrival caption moves by `EndFrame − old marker Frames`, so it begins at transition end, not switch; its original 127-frame record and source-selected visible duration remain intact. `CampaignQuestLogTimeline` consumes caption duration only outside observed lock intervals, defers eligible starts inside a lock, extends existing captions across locks and serializes later captions. Connected/unknown markers supply no lock evidence.

At map switch, `quest_log.c:512–536` clears the old description window without resetting its remaining timer. Visibility therefore ends at switch while the next caption still waits for timer completion. Map switches, actor tracks, serialized timestamps and unsupported scenes remain unchanged; this is not a persistent global clock or a guessed pause.

## Dynamic and scripted arrival ownership

Department Store, Silph, Rocket Hideout and Trainer Tower roof manual returns publish the same producer-owned receipt as ordinary south-arrow warps. Existing destination/floor/menu/entry validation remains in its producer. Numbered dynamic warps resolve event coordinates; otherwise stored explicit coordinates apply. Source arrow dispatch is immediate `DoWarp`, not another walking step. Actual destination tile chooses arrival: center elevator doors animate, Rocket side return cells stay plain. Landing elevation comes from the committed post-arrival cell. Save/restore retains the dynamic destination but clears transient receipts.

All four elevator cabins are Quest-disabled in source. Natural entry closes recording; the live receipt can still animate, but cannot manufacture a recap observation without its pending marker. Bill, Silph/Saffron pads, Seafoam currents, holes/falls and field-move travel retain dedicated owners. FirstSevii/forced Seafoam script warps and Safari post-battle `setwarp` are not silently replaced by ordinary `warp`.

Generic `warp` and authenticated Tower lobby warp share destination/receipt production and source context reset (`scrcmd.c:719–730`, `overworld.c:1568–1575`). Entry runs OnTransition before InitMap/OnLoad (`overworld.c:807–812`, `fieldmap.c:90–93`), and initial direction before OnWarp (`overworld.c:2144–2148`). This preserves explicit entry turns and Tower's `VAR_TEMP_D` barrier logic without map-specific workarounds.

A private transient arrival snapshot captures real entry state/facing/audio after entry programs but before destination OnFrame/camera-step work. Only the exact accepted origin/receipt may expose it; ordinary and scripted entry both use it, same-map included. Script preflight must not populate it, because throwaway future traversal could overwrite the current arrival. At map switch the view loads that snapshot and entry-audio prefix, completes arrival, then synchronizes already-committed destination motions/effects. It does not invent a script wait, gameplay command, saved field or renderer-owned continuation.

Tower's public B1F sequence retains `waitse`, 171 elevator ticks, delay 25 and source walk to `(2,5)`; the final ACK emits immediate departure, lobby door anchor `(17,8)` and post-door `(17,9)`. Its precommitted OnFrame motion to `(17,11)` becomes visible only after the shared 81-callback arrival. Normal ACKs finish walks, close/click barrier, clear `VAR_TEMP_D` and unlock. Pending exit states remain unsavable. Tower VBlank time continues accumulating through presentation and commits via its existing command after release, not a wall-clock restore.

Oak's hidden-player warp normalizes visibility only in admission validation; the detached origin remains hidden. The transient receipt captures `FLAG_DONT_TRANSITION_MUSIC` set inside the final ACK, not stale pre-ACK state. Arrival `SavedMusic` initializes field music, retaining MUS_OAK until explicit `fadedefaultbgm`. Ordinary origin policy and explicit script music are unchanged. The S.S. Anne departure route likewise retains source sprite X=88, 1,136 cutscene callbacks, delay 50, nine upward steps, Vermilion `(23,34)` warp and existing OnFrame two-step arrival; cross-section presentation still requires its own metadata admission.

## Directional stairs and cave ladders

On-foot indoor stairs in the same named section use source behavior `0x6c–0x6f` from the receipt, not coordinate guesses. `field_fadetransition.c:794–970` starts walk-in-place/music at 1, drifts 12 callbacks before palette fade, then waits for palette/music (minimum departure 36). Signed five-bit speeds are `(16,-10),(-17,-10),(17,3),(-17,3)`, including delayed upward Y. Arrival recovers from 16 speed units over 16 callbacks; duplicate BeginNormalPaletteFade cannot restart an active fade. NONE/SUNNY releases at 24. Shared SHADE weather, including its setup/phase-0 duplicate `WarpFadeInScreen`, releases at 19 after offset recovery. Sprite offsets never move camera/position.

New observations carry endpoint behavior facts; old records and Continue alone do not create animations. The fresh sprite seed convention matches doors, not inherited-foot/OAM-cycle equivalence. Cycling delay-12/dismount, outdoor stairs, other weather and unsupported preview/gait combinations remain outside this predicate. SHADE departure copies the gamma-shifted faded palette before blending black (`field_weather.c:735–742`); other SHADE door-arrival kinds are not implicitly admitted.

Same-section on-foot UNDERGROUND ladder→ladder requires behavior `0x61` at both endpoints and normal NONE/SUNNY weather. Source `field_control_avatar.c:856–906` completes the 16-pixel step before `DoWarp`; `SetUpWarpExitTask` selects plain arrival on the destination ladder. `fldeff_flash.c` has no UNDERGROUND→UNDERGROUND table entry, so normal black departure/arrival apply: step 16, warp minimum 24, arrival 24. Changed cave music uses outdoor speed 4. Lock/SE_EXIT begins after the step, not before. Mt Moon `(5,6)↔(3,3)` is the source route; no new controller or saved schema is needed.

## Flash visibility

Map-entry gameplay keeps illumination across underground floors and clears it outdoors. Detached live/recap snapshots own nullable stable Flash level: explicit 0=lit and 4=unlit are preserved; missing legacy evidence remains unknown. A present fact costs one logical byte. Today's flag or cave metadata cannot reconstruct a prior scene. Existing Flash field-move scene-closing behavior is unchanged; this is not an illumination-action replay timeline.

`field_screen_effect.c:SetFlashScanlineEffectWindowBoundaries` supplies the shared integer circle raster centered `(120,80)`, clamped 0..255 right-exclusive boundaries. Darkness draws after field/actors but before recap BG0 UI, matching WINOUT; source primary backdrop 0 is black. This supports the admitted black fades, not arbitrary white darkness. Same-section Rock Tunnel ladders reuse ordinary source phases regardless of lit/unlit level.

Ordinary on-foot Flash endpoints use existing cave entry/exit rules. Route 10 entrance field departure and cave arrival both fade black. Exit retains captured dark/lit cave only through black departure; generic Exit is unmasked, and outdoors has actual producer-owned level 0 before white arrival. `fldeff_flash.c:209–235` disables prior windows before interludes; the map-load/destination-window order prevents old masks leaking into either screen.

A stepped PlainWarp ladder departure plus NonAnimatedDoor arrival is explicitly supported, including Rock Tunnel north and Mt Moon B1F→Route 4. Do not conflate warp-anchor elevation with landing elevation. Ready source totals: north exit 119, south exit 103; entrance 241/161 first/visited, earliest held-B 121; recorded entrance suppresses preview and takes 92. Actual music wait and whole-record overflow rules still apply.

## Cave interludes and region previews

### Generic exit and entry

`fldeff_flash.c:237–255` checks the destination preview registry before its map-type table; visiting a cave does not remove its preview eligibility. The immutable importer/catalog, not map names or current visited state, selects it. On-foot UNDERGROUND→TOWN/CITY/ROUTE/INDOOR with NONE/SUNNY, compatible plain/nonanimated arrival and no destination preview uses black departure, generic Exit and white arrival.

Exit uses shared indexed 256×256 art and RGB555 banks, sampling the first 240×160 pixels. Source task: setup at 2, alpha 0..17 at 3..20 (saturating at 16), decreasing palette writes 21..28, white backdrop 29, completion 38. Live/recap retain indexed palette write order and saturated blending, not a post-composite opacity shortcut. Generic Enter takes 28: setup 2, expanding writes 3..10, blend activation 11, alpha 16→0 at 12..28. Generic entry without preview uses white departure/black arrival.

### Cave-type previews

Cross-section preview eligibility is independent of visited state. The registry's 20 cave-type sections share 14 pictures. Duration reads the origin flag before destination OnTransition: 120/40 with source `++counter > duration`, hence 121/41 hold callbacks. Held B can skip only during hold; physical press/release, focus loss and disconnect remain effective through acquisition. Skip is presentation-only.

A preview hands directly to Enter_1, producing a 27-callback tail rather than generic Enter 28. Black-in delay −1 and white-out delay −2 use source increments 3/4; task-local and global palette updates share transfer-pending ownership. With ready resources, complete preview+tail is 177 first, 97 visited, 57 with B at the first eligible hold callback. Graphics/window readiness remain explicit, not estimated DMA time.

Source banks 13/14 and nibble-zero backdrop are retained. The centered name window is 104×16; text colors use actual indices 17/20/19. Source requests 48 colors from a 32-color picture palette, but tilemaps reference only the two real banks: no synthetic third bank is read. The source zero-length local label-color array is represented by its three legitimate colors, consistent with BUGFIX intent.

Quest suppresses previews. Stored observations retain actual switch/end and live wrapper span; a detached playback projection removes that span consistently from later actions, captions and locks. Ordinary→UNDERGROUND substitutes Enter 28 for preview+Enter 27. Ordinary→ordinary preview destinations (including indoor Tower) lack a map-type fallback and omit the whole live wrapper; both keep black departure. Unknown records remain untouched. Admission uses on-foot plain/nonanimated receipt classes and normal weather, with snapshot-owned Flash where applicable; cycling/Surf, unsupported map classes and weather do not inherit permission.

## Forest previews and SHADE

FOREST registry type covers eight sections, not only places named forests. At `overworld.c:1914–1928`, destination BG0 preview runs priority 0 before arrival 10, weather 80 and global palette. It waits for graphics/window-copy readiness, requests black fade, waits for weather, holds 121/41, then alternates EVB increment/EVA decrement/hold across 47 callbacks. Cleanup waits for background copy; there is no B shortcut.

The normal/SHADE palette host supplies plain arrival 24/18 callbacks. Repeated forest FadeInFromBlack resets weather readiness, refuses to restart active normal fade, and still fills both banks black. Ready full forest arrival is 193/113 normal or 187/107 SHADE (first/visited); release may occur on cleanup itself. Live observation stores only the extra arrival wait (169/89 ready), which Quest suppresses after the actual map switch while keeping the ordinary arrival palette.

Duration uses the actual unconditional `setworldmapflag` entry producer, not a similarly named registry flag. Rocket Warehouse's registry references Berry Forest, but its entry script sets Warehouse. Import validates callback closure; runtime stores typed eligibility and the actual business flag. Same-section maps without producer retain null, never inventing a duration. Picture catalog stores only type/name/indexed art.

Static SHADE has no particles. NORMAL gamma 3 is `floor(channel × 13 / 16)`; ordinary source actor ALT palettes and reserved preview/name BG13/14 remain unchanged. Ordinary and snapshot map rendering share this transform. Preview picture/name share BG0: transparent 4bpp zero exposes field; RGB555 additive blending retains sum 17/saturation. Screen-copy shader blends after field including the name window. CanvasItem COLOR already contains texture, so do not multiply twice; transparent fragments must discard, retaining previously drawn BG0 primitives and solid label background. Cache recoloring while coefficient is unchanged.

Admission covers on-foot plain/nonanimated departures, plain forest arrival, normal/static SHADE, no Flash, and ordinary return. Special scripts, cycling/Surf, nonplain forest arrivals and other weather keep separate owners. The same-section SHADE stair composition is explicitly handled above, not a blanket expansion to all SHADE arrivals.

## Horizontal fog

Metadata `WEATHER_FOG_HORIZONTAL` selects Tower 3F–7F and Lost Cave rooms; entrances remain ordinary weather. Existing 64×64 art is reused. `field_weather_effects.c:1308–1397` and `field_weather.c` supply synchronous 93-call InitAll, steady 12/8 alpha and scroll every four callbacks. This is not a 93-visible-frame fade. Source task order yields 19 arrival callbacks, with readiness observed on the following arrival pass. Plain/stair samplers preserve post-arrival phase. Full map loads, including same-map wrong turns, start a fresh scene; camera samples before the current movement update.

Live/detached/recap fields own immutable fog state. Composition is lower map→explicit backbuffer→fog→actors/top/UI. Per-channel RGB555 is clamped `(fog × 12 + lower × 8) >> 4`; palette fade and recap grayscale apply to the fog palette before composition. Teardown clears fog draw commands before disposing textures. Non-fog draws retain their ordinary path.

## Field return from menus and battles

A transient display owner distinguishes Start/Save overlays from full-screen menu/battle absence. Party→Summary→Party or Bag nested screens are one absence; battle submenus retain battle ownership. Help preserves owner. Field moves/accepted transitions retain their controllers. Effective weather is script override, else header; NONE/SUNNY, SHADE and optional fog use their existing engines.

Source `ReturnToFieldLocal` resumes map/starts weather. Fog therefore resets scroll/init but retains total camera movement: first callback uses 0/0, second restores relative camera and Y−40. Start/Save overlays continue the old clock. Source return destinations differ:

- Normal: alternating BG/OBJ updates finish before weather-ready 23; scripts enable 24, Start accepts input 25.
- SHADE: ready 17, scripts 18, Start input 19.
- Fog: ready 18, scripts 19 (actual script work 20), menu task initializes 19 and input 20.

These are callback counts, not wall-clock loading. The existing palette/return controller locks input and renders snapshot field banks. Start window/text share BG fade; reserved UI banks 13–15 use GAMMA_NONE, not map SHADE. Fog ApplyFogBlend covers all BG colors. Snapshot cache distinguishes shaded map/UI textures, retains one recolored font-mask variant during return and disposes it with the snapshot. No global black overlay substitutes for palette ownership.

Bag returns to Start only after accepted top-level cancel when opened from Start. Nested screens retain that origin; direct Bag shortcut, battle Bag, rejected commands and field-item use do not reopen Start. No saved state or command-driven gameplay delay is added.

## In-place white fades

Tower 5F purification uses the shared in-place palette controller, not a post-composite white rectangle/fixed waits. Entry movement finishes before lockall's task/native/bytecode handoff. Fog/camera phase continues. Detached RGB555 white blending happens in BG/OBJ palettes before fog; dialogue windows/font masks share BG coefficient, so early text does not appear full-color over unfinished fade. Snapshots retain actual effective weather.

`script.c`, `scrcmd.c`, `event_object_lock.c`, `palette.c` and `field_weather.c` establish TO_WHITE→resumed healing at 23 callbacks. FROM_WHITE resumes bytecode after 2 while fog still fades: message admits at coefficient 14 before weather makes it 13; coefficient 0 occurs 15, readiness 16. Native-wait completion and visual completion are distinct. Existing healing/guard/coordinates/steps/RNG remain authoritative. Synthetic marked-OBJ palette probes establish arithmetic only; runtime membership of marked field-effect/reflection palettes remains open.

## Reconstruction and verification boundaries

Run maintained local import checks from the repository root; source assets/generated resources stay ignored and runtime never reads raw C:

```sh
python3 scripts/checks/test-field-door-art.py
python3 scripts/checks/test-field-cave-art.py
python3 scripts/checks/test-field-map-preview-art.py
```

Comparison fixtures are independently generated from pinned original callbacks, not rewritten from the product model. They remain local/ignored. Missing/empty fixtures must fail with the corresponding generation command; normal managed tests do not compile reference C. Explicit regeneration:

```sh
python tests/Reference/generate_field_warp_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-warp-source-vectors.json
python tests/Reference/generate_field_door_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-door-source-vectors.json
python tests/Reference/generate_field_stair_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-stair-source-vectors.json
python tests/Reference/generate_field_cave_exit_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-cave-exit-source-vectors.json
python tests/Reference/generate_field_cave_entry_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-cave-entry-source-vectors.json
python tests/Reference/generate_field_forest_preview_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-forest-preview-source-vectors.json
python tests/Reference/generate_field_flash_window_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-flash-window-source-vectors.json
python tests/Reference/generate_field_fog_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-fog-source-vectors.json
python tests/Reference/generate_field_return_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-return-source-vectors.json
python tests/Reference/generate_field_screen_fade_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-inplace-white-fade-source-vectors.json
```

Source extraction preserves scheduler ordering, palette banks, held-walk/avatar state, window and weather callbacks; unrelated hardware/world services are stubbed. Import checks independently reconstruct material layers/pixels, palette selection, tile addressing, transparency and malformed-input failure. Managed tests cover source rows, live/recorded parity, real receipts, identity/save projection, budgets, malformed fields, delayed music, strict Continue and old-record behavior. `ConnectedFieldStepPresentationTests` also covers all directions, signed offsets, unequal/small maps, 16/8/6-frame cadence, unrelated receipts, Surf/script exclusions and actual connection geometry. None of these alone is a rendered or audible result.

Current bounded native routes, before/after pixel checks and failures retained as counterexamples belong in [validation](../validation.md). Native coverage includes representative cave/forest preview, SHADE stairs, dark ladder, fog/menu return and connected modes, not every route or recap combination; Tower purification was not included in that field-window sample. Audio used Dummy. No actual speaker, Windows run, natural complete campaign or performance gain is established.

Public route evidence must retain truthful earned state. Fresh-game Oak reaches stable starter selection; ship and Selphy continuations use controlled entrance placement with maintained earned checkpoints. Selphy expiry/reward does not validate her prior rescue/return-home warp. Fuji rescue/Flute and Champion→Hall lack truthful pre-event fixtures in the maintained local set; do not rewind flags or clone victory receipts to manufacture acceptance. Missing route evidence is not proof of broken progression.

Remaining fidelity limits include inherited departure foot/OAM seed, cycling/Surf door/stair gait, unsupported special/weather/map combinations, dynamic weather and marked field-effect palette membership, arbitrary entry motions, neighbor-NPC persistence and connected Quest replay pixel parity. Dedicated special controllers are not removed or certified by this ordinary guide. Cable-club/link behavior is outside single-player scope.
