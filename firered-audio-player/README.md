# Isolated FireRed sequence/sample player prototype

Original C#/.NET 10 implementation, private work in progress. No agbplay dependency, copied implementation, ARM emulator, recorded-audio substitute, or changes to firered-remake. This is not a certified clean-room project: earlier independent research inspected agbplay. Implementation here is newly authored from explicit data/behavior specifications and the fixed original resource data.

## Historical phase7 integration

Phase7 adds original player slots/pause/gain controls,262 tests and144 admitted route IDs. All111 noncry cues and25 loop cases pass; PHASE7-REPORT.md（历史研究报告，当前工程未携带） separates core acceptance from the main-game default-host acceptance. The user selected custom synthesis as default and excluded converted recordings from backup.

## Previous frozen candidate

Phase6: 386 ordinary cries with authenticated optional catalog, 12 additional representative BGM/SE/fanfare pieces, all five voice families, 178 tests and an isolated 58-second Godot PCM demonstration. See PHASE6-REPORT.md（历史研究报告，当前工程未携带）, INTEGRATION-CANDIDATE.md（历史研究报告，当前工程未携带） and [independent review](specs/PHASE6-REVIEW.md). Earlier phase sections below preserve historical scope. Main-game integration remains separate.

## Original scope and gates
1. Bounds-checked compact external resource reader, fixed SHA-1 source identity, song/voice/sample validation.
2. Live sequence execution and PCM/required PSG synthesis for MUS_PALLET and SE_SELECT; deterministic offline WAVs and event traces, loops, simultaneous players, stop/restart.
3. Negative tests, five PCM voice limit/priority tests, fractional clock consistency, allocation/performance measurements, comparison to preserved mGBA original-driver references.
4. Separate Godot streaming demonstration only after offline gates. No main-game integration.

No full-library, general cry-mode, hardware-bit-perfect, target-device realtime or listening-test claim. Without the optional catalog, the original API supports only normal Charmander cry. With authenticated catalog, all 386 real species have mode0 support; special cry modes remain unsupported. Unsupported opcodes and sample formats are errors, never substituted tones or ignored operations. Original game resources stay external and private; code publication does not authorize publishing those assets.

Fixed source: pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14, ROM SHA-1 41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc. Audio resource address range [0x08489968,0x086fbea4). Toolchain: installed official .NET 10. Build/run scripts use FIRERED_DOTNET when set, otherwise an existing FIRERED_TOOLCHAIN_ROOT/dotnet installation or dotnet on PATH. No packages beyond the SDK/standard library.

## Current build and run

The engineering package keeps one private resource pair in `firered-remake/content/generated/audio/synth/`. The library and CLI accept explicit external paths; they do not discover the game, depend on pret at runtime, or carry their own resource copies. These files remain outside Git and public distribution.

From the workspace root:

```sh
PACK="$PWD/firered-remake/content/generated/audio/synth/firered.pack"
CATALOG="$PWD/firered-remake/content/generated/audio/synth/normal-cries.bin"
./firered-audio-player/build.sh
dotnet test firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj --filter TestCategory=Audio
mkdir -p firered-audio-player/results
./firered-audio-player/run.sh render "$PACK" select 3 firered-audio-player/results/select.wav
./firered-audio-player/run.sh benchmark "$PACK"
./firered-audio-player/verify.sh "$PACK"
./firered-audio-player/demo/build.sh
./firered-audio-player/demo/start.sh --headless --audio-driver Dummy -- "$PACK" "$CATALOG"
```

`verify.sh` is a current monorepo maintenance check: it runs the maintained Audio tests, offline renders, repeat comparison, negative CLI probes and benchmark. It does not claim comparison against missing historical reference recordings or audible-device acceptance. The tests use the canonical engineering resources; the render commands use the pack supplied by the caller.

To create a resource pack from an already available authorized ROM, use `run.sh pack /absolute/path/to/input.gba /absolute/path/to/output.pack`. The catalog generation tool requires its historical phase4 inventory and an explicit output: `python3 firered-audio-player/tools/build_cry_catalog.py /absolute/path/to/normal-cries.bin`. Neither command is required to use the existing canonical pair. Do not create a second maintained copy inside this engineering package.

Standard-library C# runtime does not need Python, NumPy, SciPy, FFmpeg, mGBA, agbplay or network access. Research scripts retain separate ROM/reference prerequisites.

The obsolete `export_game_candidate.py` and `verify-runtime-candidate.sh` entry points were retired: their old copied-core/manifest layout and missing research baselines are not current integration. Main-game composition references the separate Core project and generated audio resources directly.

The render mode is explicitly modern-dry experimental. It retains source note/sample/square duty/sweep/envelope behavior, but lacks original reverb, low-rate integer mixing, PWM, filtering and exact PSG phase. See `specs/TRADEOFFS.md` and `REPORT.md`; numerical correlation is diagnostic, not a listening or fidelity pass.

`Synth.Render(Span<float>)` writes interleaved stereo into caller-owned buffers. Commands and rendering currently require one owner thread. Warm rendering performs no IO and is allocation-free in the tested workload when tracing is disabled. Start/stop and instrument first-use allocate; realtime command-queue/prewarming API is a future integration task. Trace mode intentionally allocates diagnostic strings.

## Historical standalone-demo proposal (superseded by demo/)

Use a separate tiny Godot C# scene, not the remake project. An `AudioStreamGenerator` at48kHz would receive preallocated blocks from this module, with a bounded command queue, explicit prewarm and start/stop ownership, buffer-fill/underrun telemetry and repeated switching tests. Do not import any current game's audio router or replace its backend. First audition and correct the offline timbre/tail differences; then validate actual device output, suspend/resume and load contention. Offline cloud throughput does not guarantee target-device realtime operation.

A separately compiled library is in `core/FireRedAudio.Core.csproj`; it links runtime source files and excludes CLI and tests. `build.sh` builds both. Historical size reports and research result directories are not carried by the current engineering package.

## Historical phase research

The following records describe earlier experimental scopes, not current test counts or completed gates. `verify-phase2.sh`, `verify-phase3.sh`, `verify-phase4.sh`, `research-music/run.sh` and `research-integration/run.sh` now require an explicit pack path as their first argument. They also require their original ROM/reference inputs and historical result baselines, which are not all included here. Missing inputs remain failures; these scripts are not the maintained verification entry point.

## Bounded phase 2 (separate experimental profiles)

See `PHASE2-REPORT.md`. The default `modern-dry` output is an exact rollback to the first commit. New source-gain/linear/pitch/reverb/bandlimited/filtered profiles isolate reference-derived gain and pitch from explicitly modern DSP. `source-envelope` is a separate diagnostic shadow-clock branch, not the chosen chain. Example:

```
./firered-audio-player/run.sh render "$PACK" pallet 95 firered-audio-player/results/phase2/pallet-source-filtered.wav source-filtered
./firered-audio-player/verify-phase2.sh "$PACK"
```

Voice stop and natural silence differ when effects are enabled: residual effect tails continue, and `Ended` includes the effects' sub-quantization silence threshold. Phase1 `results/artifact-hashes.json` describes its frozen commit, while phase2 has its own manifest. No product backend or default playback profile was switched.

## Phase 3: one normal cry

`PHASE3-REPORT.md` documents real runtime decoding/playback of the fixed normal Charmander cry and its BGM-duck policy. CLI modes `cry-charmander` and `cry-mix` accept the same optional output profile. Run `verify-phase3.sh "$PACK"` for the complete bounded gate.

Only species4/mode0 with the fixed normal parameters is accepted. A new cry replaces the existing single cry slot; original two-cry overlap and all other modes/species are unsupported. `PrepareNormalCry` predecodes7133 playable samples plus one guard into memory; no reference recording is loaded during playback. `CryPlaying` follows the voice ending near0.679s, while the template remains active until xWAIT140 completes near2.344s. The duck task restores BGM after the voice ends, not after that wait.

Historical `REPORT.md`/`PHASE2-REPORT.md` and their result manifests retain their original scopes. Phase3 has its own report, checksums and benchmark, including separate control-call versus Render allocations.

### Ordinary-cry coverage assessment

`PHASE4-REPORT.md` records the separately compiled offline assessment of386 real species.
`./verify-phase4.sh "$PACK"` checks all decoded assets, actually renders every species with two block sizes,
and preserves the phase3 public API boundary. Research-only `CRY_COVERAGE_RESEARCH` is never defined by the core/CLI projects.
The existing public cry API remains Charmander-only pending explicit catalog promotion.

### Runtime catalog and Godot experiment

Phase5 promotes the authenticated optional386-species normal catalog (see `specs/PHASE5-CATALOG.md`). With the historical inventory available, run `python3 tools/build_cry_catalog.py /absolute/path/to/normal-cries.bin`, then `dotnet test firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj --filter FullyQualifiedName~NormalCryCatalogTests` to verify the public API. The historical phase4 PCM hash comparison requires its missing research baseline and is not claimed by the maintained tests.
`PHASE5-REPORT.md` records representative music gaps and the independent `demo/` real PCM stream experiment. The normal core has no Godot dependency; only this separate host project references the installed Godot SDK.
