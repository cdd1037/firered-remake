# Isolated FireRed sequence/sample player prototype

Original C#/.NET 10 implementation, private work in progress. No agbplay dependency, copied implementation, ARM emulator, recorded-audio substitute, or changes to firered-remake. This is not a certified clean-room project: earlier independent research inspected agbplay. Implementation here is newly authored from explicit data/behavior specifications and the fixed original resource data.

## Current route integration

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

## Build and run

From the workspace root:

```
./firered-audio-player/build.sh
./firered-audio-player/run.sh pack audio-reference-build/pokefirered/pokefirered.gba firered-audio-player/resources/firered.pack
dotnet test firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj --filter TestCategory=Audio
./firered-audio-player/run.sh render firered-audio-player/resources/firered.pack pallet 95 firered-audio-player/results/pallet.wav
./firered-audio-player/run.sh render firered-audio-player/resources/firered.pack select 3 firered-audio-player/results/select.wav
./firered-audio-player/run.sh render firered-audio-player/resources/firered.pack mix 12 firered-audio-player/results/mix.wav
./firered-audio-player/run.sh benchmark firered-audio-player/resources/firered.pack
python3 firered-audio-player/specs/inventory.py
python3 firered-audio-player/specs/audit_sequence.py
python3 firered-audio-player/tools/compare.py
```

The pack command is the only step requiring a complete original ROM. Runtime accepts the immutable raw audio-only `.pack` with fixed SHA-256. Standard-library C# runtime does not need Python, NumPy, SciPy, FFmpeg, mGBA, agbplay or network access. The optional reference-analysis script uses already available NumPy/SciPy; reference WAVs were created earlier with the original driver in mGBA.

The render mode is explicitly modern-dry experimental. It retains source note/sample/square duty/sweep/envelope behavior, but lacks original reverb, low-rate integer mixing, PWM, filtering and exact PSG phase. See `specs/TRADEOFFS.md` and `REPORT.md`; numerical correlation is diagnostic, not a listening or fidelity pass.

`Synth.Render(Span<float>)` writes interleaved stereo into caller-owned buffers. Commands and rendering currently require one owner thread. Warm rendering performs no IO and is allocation-free in the tested workload when tracing is disabled. Start/stop and instrument first-use allocate; realtime command-queue/prewarming API is a future integration task. Trace mode intentionally allocates diagnostic strings.

## Future standalone Godot demo (not implemented)

Use a separate tiny Godot C# scene, not the remake project. An `AudioStreamGenerator` at48kHz would receive preallocated blocks from this module, with a bounded command queue, explicit prewarm and start/stop ownership, buffer-fill/underrun telemetry and repeated switching tests. Do not import any current game's audio router or replace its backend. First audition and correct the offline timbre/tail differences; then validate actual device output, suspend/resume and load contention. Offline cloud throughput does not guarantee target-device realtime operation.

A separately compiled library is in `core/FireRedAudio.Core.csproj`; it links only the three runtime source files and excludes CLI and tests. `build.sh` builds both. `verify.sh` reruns the complete selected offline matrix, exact repeat comparison and negative CLI probes. See `results/package-size.json` for measured component bytes and `results/source-size.json` for normally formatted source counts.

## Bounded phase 2 (separate experimental profiles)

See `PHASE2-REPORT.md`. The default `modern-dry` output is an exact rollback to the first commit. New source-gain/linear/pitch/reverb/bandlimited/filtered profiles isolate reference-derived gain and pitch from explicitly modern DSP. `source-envelope` is a separate diagnostic shadow-clock branch, not the chosen chain. Example:

```
./firered-audio-player/run.sh render firered-audio-player/resources/firered.pack pallet 95 firered-audio-player/results/phase2/pallet-source-filtered.wav source-filtered
./firered-audio-player/verify-phase2.sh
```

Voice stop and natural silence differ when effects are enabled: residual effect tails continue, and `Ended` includes the effects' sub-quantization silence threshold. Phase1 `results/artifact-hashes.json` describes its frozen commit, while phase2 has its own manifest. No product backend or default playback profile was switched.

## Phase 3: one normal cry

`PHASE3-REPORT.md` documents real runtime decoding/playback of the fixed normal Charmander cry and its BGM-duck policy. CLI modes `cry-charmander` and `cry-mix` accept the same optional output profile. Run `verify-phase3.sh` for the complete bounded gate.

Only species4/mode0 with the fixed normal parameters is accepted. A new cry replaces the existing single cry slot; original two-cry overlap and all other modes/species are unsupported. `PrepareNormalCry` predecodes7133 playable samples plus one guard into memory; no reference recording is loaded during playback. `CryPlaying` follows the voice ending near0.679s, while the template remains active until xWAIT140 completes near2.344s. The duck task restores BGM after the voice ends, not after that wait.

Historical `REPORT.md`/`PHASE2-REPORT.md` and their result manifests retain their original scopes. Phase3 has its own report, checksums and benchmark, including separate control-call versus Render allocations.

### Ordinary-cry coverage assessment

`PHASE4-REPORT.md` records the separately compiled offline assessment of386 real species.
`./verify-phase4.sh` checks all decoded assets, actually renders every species with two block sizes,
and preserves the phase3 public API boundary. Research-only `CRY_COVERAGE_RESEARCH` is never defined by the core/CLI projects.
The existing public cry API remains Charmander-only pending explicit catalog promotion.

### Runtime catalog and Godot experiment

Phase5 promotes the authenticated optional386-species normal catalog (see `specs/PHASE5-CATALOG.md`). Run `python3 tools/build_cry_catalog.py`, then `dotnet test firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj --filter FullyQualifiedName~NormalCryCatalogTests` to verify the public API. The historical phase4 PCM hash comparison requires its missing research baseline and is not claimed by the maintained tests.
`PHASE5-REPORT.md` records representative music gaps and the independent `demo/` real PCM stream experiment. The normal core has no Godot dependency; only this separate host project references the installed Godot SDK.
