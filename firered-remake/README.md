# FireRed recreation — implementation workspace

Goal: a complete, local single-player recreation of the English original-revision FireRed. Kanto, story, ordinary single-player side content and reachable post-game are the target. The first badge is an intermediate acceptance milestone. See [scope](docs/scope.md) and [full scope audit](docs/full-scope-audit.md).

## Current recovered state — 2026-10-10

The accepted continuation reaches four badges, Celadon Rocket Hideout's Silph Scope,
Pokémon Tower's Mr. Fuji/Poké Flute, and the two bounded Route12/16 Snorlax events.
The source/application checks include capture into party/PC and non-victory callbacks;
the focused real-window Snorlax check covers No, wake, escape, removal, save and a
second-process normal Continue. See [Snorlax scope and evidence](docs/snorlax-campaign.md).

The regional-interaction successor adds ordinary NPC branches and shared environmental
text in25 loaded Celadon/Lavender/Tower/Route7–8/tunnel maps. It retains the complete
Snorlax gameplay state through the explicit `save upgrade-regional INPUT NEW_OUTPUT`
command. Run `bash scripts/launch-regional.sh`; its save directory is `.play/regional`.
Earlier launchers and their isolated saves remain available. See
[regional coverage](docs/regional-interaction-coverage.md) and
[profile/migration checks](tests/Game.RegionalProfile.Tests/README.md).

Regional interaction completeness is still in progress. Tea/Eevee and missing interiors,
Department Store services, Name Rater, coin systems, Itemfinder and Route8's genuine
preset double battle are separate gaps. Route12/16 access does not open Cycling Road
or the full southern routes. Source-only GitHub checkpoints and asset-complete private
Drive packages have separate records; the latest complete Drive base is the four-badge
223c93 checkpoint. This is an active complete-FireRed project, not a finished game.

## Earlier first-badge checkpoint

The current launcher now exposes functional New Game/Continue, canonical player/rival names and gender, a source Green walking avatar, validated backup recovery and a first-save different-adventure confirmation. The source catalogs, isolated Godot input/file checks, and a fixed-executable native two-process New Game/save/Continue check pass. Original title/Oak introduction, original naming keyboard and persistent play time remain open. See [entry flow](docs/player-entry-application.md). Battle medicine now includes five ordinary items on current/reserve members; [focused native Potion proof](evidence/battle-medicine-native-review.md) retains actual exit/restart recovery.

This is an active implementation, not a completed FireRed game. The current playable profile reaches Pewter/Brock from the original opening with all three starter branches, bounded early party growth/moves, field menus and 14×30 PC storage. Actual native checks cover seventh capture, storage deposit/withdrawal and separate-process file recovery. The basic menus include a numerical regional Pokédex, trainer-card front, persisted text speed/battle style/frame options and real field text printing; source/application/headless and focused two-process native checks pass. Full menu modes and broader original presentation remain unfinished. See [current first-badge gaps](docs/first-badge-progress.md).

The earlier Route22 profile includes Daisy's Town Map, Oak/PC rating, the early Route22 rival and new wild species, and the bounded Boulder/Cascade guards in Route23. Four actual native processes verified the rival, guards, Continue and captured-Mankey menus; see [Route22 integration](docs/route22-application.md) and [retained native summary](evidence/route22-native-review.md). This does not open the later badge gates or northern Route23.

Historical profile checkpoints retain their original rules. Use `dotnet run --project src/Game.Tooling -- save upgrade-route22 OLD-OAK-RATING-SAVE NEW-SAVE` for the explicit full-payload-preserving upgrade to a new file. Earlier `upgrade-oak-rating`, `upgrade-town-map`, `upgrade-repel` and `upgrade-field-pickups` commands remain available for their exact source profiles. Saves are never silently relabeled.

The latest launcher enables [compact map data](docs/runtime-map-data.md) and [bounded shared textures](docs/map-resource-cache.md). The 24 map descriptions are 449,579 B including metadata registry, compared with 16,100,416 B of rich development JSON. Original source evidence remains available for rebuilding. Current map textures use a three-map/24 MiB soft budget, with active/last-frame pins preserved; source region rectangles and source pixels remain unchanged. This is not a TileMap or wholesale atlas conversion.

The user-selected default audio backend now synthesizes original sequences/samples in C#, using a 2,623,222 B private resource package. [Audio integration](docs/synth-audio-integration.md) documents 144 admitted current-route IDs, source counters, buffering, approximations and exact validation limits. `FIRERED_AUDIO=0` disables presentation audio; `FIRERED_AUDIO_BACKEND=recorded` is an explicit local comparison requiring the old separately retained recordings. Default gameplay does not silently fall back to those recordings. The next recovery package excludes pre-rendered/converted audio; necessary original samples, their hashes, source code and rebuild instructions remain. Godot PCM/control and command-state comparisons pass; physical audible listening remains unverified. Two real default-synth processes have now saved and continued with identical state/file hashes; see [the native evidence](evidence/synth-audio-native.md). Final compressed-package recovery is a separate gate.

The [current recovery contract](docs/current-checkpoint-recovery.md) separates the selected final checks, historical native evidence and upcoming archive restore. [Menu pointer controls](docs/menu-pointer-controls.md) now add hover, wheel and context-aware right-click; Tab remains available.

The foundation milestones below retain their original scope; later increments are described in the current gap ledger.

- Official Godot **4.7.2 .NET**, Godot.NET.Sdk **4.7.2**, SDK **10.0.401** are pinned; actual hosted runtime **10.0.12** was measured, not inferred from the target framework.
- Linux native self-contained export runs .NET **10.0.12** from its own output directory. An actual X11 window renders bundled Chinese glyphs and accepts keyboard events. The cloud renderer is Mesa llvmpipe, not hardware-GPU performance evidence.
- The cloud machine has no usable audio output device; Godot falls back to Dummy. Audio playback requests are observable, but actual audible output is **not verified**. Audible-output acceptance remains **incomplete**; the authorized single-player implementation continues with that limitation recorded.
- Five product assemblies are implemented. Pure rule/transaction/save tests run without Godot. The original engineering arena remains a diagnostic fixture.
- The initial region foundation connected eight real source maps, including Route 1, Viridian City and its Mart. The original five-map foundation contains: Pallet Town, player house 1F/2F, rival house and Oak's lab. Their 1,030 cells, layers, collision/elevation, 14 warp records and 11 NPC sprite classes are source-verified. Raw/reference outputs remain gitignored.
- The source-driven headless opening route starts at the original upstairs coordinate, descends and exits the house, triggers Oak, follows the original movement/dialogue scripts to the lab and reaches starter-selection scene 2. An added assertion checks Oak is present and can be spoken to again. The same 76 commands (including speaking to Oak again) have now been exercised through an actual native Godot window and checked against the separately reviewed golden.
- The native window has verified Pallet Town rendering, movement, a real source sign, stable save/load and explicit unsupported boundaries. The source opening is also presented in the native window with real NPC sheets, walking paths, paging and correlated waits. The native source route now also previews/declines/accepts starters, names the owned creature, waits through the source fanfare counter, follows the rival counterpick to scene 3 and saves/loads that stable state. The application route additionally reaches lab scene 4 through the source first battle, win/loss rewards and callback; the native window has now verified both victory from a valid field checkpoint and a full new-game-to-defeat route, including returning to the lab and stable save/load. Original message/door/naming art and audio remain incomplete. See [lab battle integration](docs/lab-battle-integration.md).
- A complete native new-game route now reaches Viridian City: starter choice, original rival defeat callback, natural Route 1 wild victory, actual EXP/EV and persistent HP/PP, the clerk's one-time Potion, two successful escapes, and stable save/load. Its frozen 8f079dd window/command evidence is recorded separately from the application tests.
- The next parcel-quest profile executes the original Mart entry/counter edits, Oak's Parcel handoff, return to Oak, Pokédex scene and five Poké Balls. Application tests cover all starter choices and four Oak approach directions. Native parcel delivery, inventory pockets and same-map repeated save/load are now verified; the discovered Mart counter reload defect was fixed and rechecked against all six source layers.
- The capture profile now accepts Poké Balls, preserves the actual wounded wild instance, displays the first-catch Pokédex page, assigns nicknames and adds party members. Forced replacement and two-member field save/load pass application and actual Godot-node tests. Native capture has now completed: a real failed throw followed by success, dex/nickname, party delivery, save, process exit and a new-process F9 load with the same state hash. See the [stage acceptance](docs/stage1-acceptance.md) and [recovery notes](docs/stage1-resume.md). This is the user-defined stage-one archive checkpoint. After the Drive archive is uploaded and verified, development continues toward the full single-player goal; the packaging candidate remains frozen during backup.
- The opt-in Viridian Center increment (`FIRERED_VIRIDIAN_CENTER=1`) now passes source-driven healing and registered whiteout return, independent application review and a real-window nurse/save/load route. Original step order, No/B, whole-party identity and PP restoration are retained; the upper floor remains unsupported; the later storage profile now implements the ordinary Center PC. See [center application](docs/viridian-center-application.md).
- The arena, test creatures and graphics are original placeholders for integration testing. They do **not** count as original maps, story, creature coverage or a first-badge milestone.

## Commands

Python 3 and the locked official tool archives are sufficient for the launcher. Toolchains live outside this repository, default `/workspace/shared/firered-toolchains`; override with `FIRERED_TOOLCHAIN_ROOT`. `./dev setup` is the explicit official-download/install step, never an implicit effect of doctor.

```sh
./dev doctor
./dev setup                    # explicit; official URLs and hashes only
./dev build
./dev test
./dev test-reference           # requires the local reference conversions
./dev test-presentation        # actual Godot headless view checks, not native acceptance
./dev scenario smoke
./dev probe                    # real Godot/C# headless hosting; not a window/audio test
./dev export linux             # release export and independent headless launch
./dev verify p0                # deliberately exits 2 while audio acceptance is missing
./dev content validate tests/fixtures/content/original-two-rooms.json
./dev content import-pret-map-fixture tests/fixtures/content/original-pret-map-shape.json
```

For a desktop with a display:

```sh
./scripts/launch-probe.sh
./scripts/launch-probe.sh -- --harness
./scripts/launch-export.sh
./scripts/launch-export.sh -- --harness
./scripts/launch-story.sh          # historical opening profile, requires reference inputs
bash scripts/launch-current.sh     # latest accepted bounded continuation
```

Probe keys: arrows/Enter produce input evidence, Enter requests a generated 440 Hz test tone, F12 saves the actual rendered viewport, Escape exits. Harness keys: arrows move, B starts the supported basic battle fixture, Enter attacks or claims the one-time reward, F5 saves, F9 loads (both rejected during battle), F12 captures, Escape exits.

The export currently targets Linux x86_64 only. No web, Windows or macOS acceptance is claimed. Its self-contained runtime does not require the installed SDK. Launchers write evidence inside this workspace; normal save files are under Godot's project user-data directory.

## Modules

- `Game.Domain`: immutable current-profile battle, encounter, party/growth, inventory and field rules, stable IDs, and explicit Gen3 LCG state/trace. Admitted effects and historical rule profiles remain explicit. No Godot, filesystem, network or wall clock.
- `Game.Application`: authoritative campaign/script transactions, revision guards, canonical state hashes, versioned/checksummed player saves and backup recovery. The small synthetic integration session remains a diagnostic fixture.
- `Game.Content`: strict typed IR, references and source provenance, deterministic serialization, source-map structure adapter. Unknown operations fail explicitly.
- `Game.Godot`: native input/rendering, menus, bitmap fonts, custom audio synthesis/playback, persistence adapters and composition root. UI never calculates damage; the original font/sound probe remains available.
- `Game.Tooling`: command-line validations and repeatable synthetic scenarios.

Pure test executables intentionally use no third-party test framework; nonzero exit codes and nonempty result lists are required. See `docs/rule-fixtures.md` for exact covered and excluded mechanics. A large test count is not content coverage.

## Evidence and limitations

`evidence/` contains exact tool metadata, restore/build logs, measured runtime JSON, native screenshots, test output and the P0 gate result. Failed attempts are retained with a failure label. The first export attempt failed because Godot requires a `.sln` beside the C# project; this was fixed and independently re-exported. Default parallel MSBuild fails without diagnostics in this sandbox; the launcher uses tested `-m:1 -nr:false`.

Current save checksum detects corruption, not tampering. Commands keep the expected-revision/version guards and serialized atomic commit, with no historical receipt or command-ID ledger. Stale requests return the current state unchanged; a reused trace ID with the current revision is a new request evaluated by game rules. Saved business reward flags preserve one-time rewards independently. See [the revision contract](docs/command-revision-only.md). Historical profile upgrades are explicit tools. Same-directory staging/flush/rename is exercised; absolute power-loss guarantees and clean-machine packaging remain unverified. Source maps, event IR and diagnostics do not imply all FireRed scripts or tile behaviors execute yet.

No ROM was downloaded or included in the recovery archive. Local reference inputs are retained for private development and the user-authorized private Drive recovery archive; no public publication is authorized. Public source availability is not a blanket asset license. See `docs/source-baseline.md` and `content/source-lock.json` before any future import.

Player saves are permitted only outside battle. The generic codec and domain fixtures can serialize battle snapshots for deterministic diagnostics; that capability is not exposed as a player save feature. An early native engineering-only checkpoint test is retained as historical diagnostic evidence and is not original-game save acceptance.

## Source-world development route

Run the pinned fetch/import scripts listed in `tests/Game.Map.Tests/README.md`, `docs/pokemon-instance-rules.md` and `scripts/fetch-story-source.py`. They read a bounded source set, verify hashes and never execute upstream scripts. Run `./scripts/launch-pallet.sh` for the locally rendered reference map. Source art and screenshots must not be publicly distributed.

The typed story IR preserves source labels, line numbers, aliases and bounded zero-argument movement macros. Native state-changing effects yield internally, commit to the application state, and resume before later instructions. Unknown operations fault instead of being skipped. A `waitmessage` acknowledgement signals text rendering completion; message visibility lasts until its source close/release. Dialogue/choice waits, visual presentation waits and native effects are distinct. See `docs/pallet-warp-rules.md` for real door/arrow/stair behavior.

The opening view uses Enter for dialogue, arrows for movement, F5 for an outside-event save and F9 to restore a validated stable save. In battle, arrows select actions/moves, Enter confirms and Backspace returns; Bag and the bedroom PC support the original Potion workflow. Original revision 0 has also been run in mGBA for visual comparison; several opening PC/battle pages match the complete captured frame. New region/quest visuals, animation and timing remain partial; that independent gate is tracked in `evidence/original-visual-comparison.json`. Its nickname panel is a provisional text-input adapter, with source character constraints and a separate callback wait. `scripts/verify-story-window.py` checks the captured 76 native commands against a separately reviewed fixed local golden, not a newly generated expected result. `tests/Game.StoryAudit.Tests` preserves transaction/warp/negative-boundary audits in `./dev test-reference`.

Native starter evidence: `scripts/verify-starter-window.py` verifies one complete upstairs-to-gift keyboard run and a separate corrected-widget run from a real scene-2 player save. Optional `FIRERED_CAMPAIGN_CHECKPOINT` is a local QA input passed through the same strict save codec/semantic validator; it does not inject arbitrary event state. Fetch the additional fixed presentation timing source with `python3 scripts/fetch-presentation-cues.py`. The 170-counter fanfare wait is observed, but sound playback remains unverified.
