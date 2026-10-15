# Default original C# audio backend

Custom live synthesis is the only supported audio backend. `FIRERED_AUDIO=1` with no backend override selects `SynthSourceAudioPlayer`; `FIRERED_AUDIO=0` disables presentation audio. `FIRERED_AUDIO_BACKEND` may be unset or `synth`; other values report an audio fault. The obsolete recorded backend, encoded-clip loaders, historical delivery catalogs, and their import/study tools have been removed. Audio never silently falls back to a recording, and reports unknown IDs or runtime faults.

## Bounded inventory

144 admitted IDs: 143 unique IDs from the four prior route catalogs plus previously missing `SE_BOO`. There are 111 noncry cues and 33 ordinary cries. Among noncries, 86 are finite and 25 loop. Every noncry was rendered for 20 s at 512/257-frame partitions; all 25 loops were additionally rendered for 120 s and every source-declared looping track executed GOTO. No silence, nonfinite values, clipping, exceptions or block-size differences occurred. The extended five-slot core retains all272 corresponding baseline hashes.

The current route uses 23 BGM,80 SE (including three musical one-shots routed to SE2 and the new SE_BOO), eight independent fanfares and 33 ordinary mode0 cries. All 386 species are implemented in the core's authenticated cry catalog, but the host deliberately admits only these 33 route IDs. Special cry modes, broader move-animation sound timelines and additional route IDs remain outside this checkpoint.

## Resource and source ownership

Only `content/generated/synth-audio/{firered.pack,normal-cries.bin,manifest.json}` is required for audio at runtime:2,623,222 B including the 144-ID manifest. The pack is the fixed original sequence/voice/sample resource extent, not a rendered recording or full ROM. Runtime authenticates exact length and pinned SHA-256 before parsing. The normal-cry table and cue manifest are also fixed authenticated inputs.

`OriginalAudioCore_*.cs` originated as a byte-identical snapshot of our isolated C# project. The later bounded Snorlax mode2 extension is an explicitly recorded derived main-project patch in `OriginalAudioCore/PROVENANCE.json`; it leaves the independent original-player repository unchanged. See [Snorlax presentation](snorlax-presentation.md). `OriginalAudioCore/PROVENANCE.json` records individual source hashes and the baseline commit. There is no external project path, FFmpeg, agbplay dependency, or new NuGet package. Earlier independent research inspected agbplay; this is not certified clean-room provenance. This implementation is newly authored from explicit source-data/behavior specifications and primary format documentation.

Rebuild/export from the private original-player workspace with `python3 tools/export_game_candidate.py ../firered-remake`. The script requires verified offline coverage and authenticated local resource inputs. It installs nothing and downloads nothing. The fixed pret revision is 037335f4c725d7c9aecdac87066f2002b4bd7e14; ROM SHA-1 41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc. Original resources are private and are not authorized for public distribution.

## Host contract

The live synthesizer implements `ISourceAudioBackend`, whose presentation contracts and source timing constants are in `SourceAudioContracts.cs`. All game-facing methods remain presentation-only; they do not mutate Domain, command history, save payloads or RNG. Source player 0 maps to core 0, SE1 → 1, SE2 → 3, SE3 → 4, cry → 2. All players share five PCM channels and four family-specific PSG channels, so the adapter does not multiply the original voice budget by instantiating a synth per effect.

SE1/2 retain priority protection, SE3 replaces unconditionally. Unknown IDs return zero with a missing event; runtime arithmetic/format errors clear queued audio and tasks, emit a fault event, and let the campaign's wait guard fail visibly rather than strand gameplay. No silent recorded fallback is used.

Fanfare task counters are independent of sound duration: N decrements plus the separate zero-counter frame, token-bound cancellation and BGM restoration. Source pause silences still-linked channels while preserving track cursor/waits/tempo; continue resumes future sequence notes. It is intentionally different from freezing a pre-rendered waveform. A new BGM cancels stale fanfare/cry ownership.

Cry onset is distinguished from completion: a pending dynamic template must execute at least one original driver frame before voice-empty can mean ended. Voice completion (not the template's long idle wait) restores future rendered BGM gain; public cry state/wait completion follows consumption of that boundary. This avoids prematurely ending a cry or adding a second whole queue of ducked BGM. The endpoint is block-quantized to at most512/48000 seconds.

Reset/session replacement clears voices, counters, shared effect tails and the entire output ring. Scene detach also resets control state. Re-adding a node cannot resurrect old sounds. The existing campaign shutdown waits for the AudioServer to retire stopped playback; no new domain lifecycle hook is required.

## PCM buffering and sound boundaries

48 kHz float stereo PCM feeds Godot's explicitly Custom 48 kHz AudioStreamGenerator. The generator has 8191 usable frames on the measured host; production target 5120 frames is 106.67 ms of queued audio, before device latency. Smaller tested targets underrun in this cloud environment. This is not a low-latency-device acceptance claim.

SourceFiltered retains original sample data, pitches, source-shaped feedback, PSG wave/noise and five-PCM priority behavior with modern host-sample integration. Tail shapes, family-relative onset/pipeline phases, hardware DAC/PWM and exact PSG retriggers remain approximate. Five original-driver A/B comparisons have level/timbre evidence but are not perceptual or bit-perfect certification.

The cloud device is Dummy. Actual engine consumption/nonzero PCM, muting, queue recovery and disposal can be tested; physical audible listening remains unverified. First instrument use and scene controls may allocate. Prepared core render benchmark is distinct from the allocating diagnostic capture/probe and from full Godot process memory.

## Route3 recovery extension, 2026-10-09

The current recovered Route3 profile adds seven ordinary cries (Ekans, Nidoran F, Nidorina, Nidorino, Jigglypuff, Magikarp and Gyarados): **151 admitted IDs, 40 ordinary cries**. The source pack, normal-cry table and seven core implementation files are unchanged. The 144-ID counts and campaign evidence above describe the earlier fixed checkpoint. Regenerate the current manifest with `python3 scripts/export-route3-synth-manifest.py`, or verify it with `--check`; the recipe and output identity are pinned independently from the historical exporter.

`Game.Route3SynthRecovery.Tests` renders all seven added cries at two block sizes, verifies finite nonzero output, exact block partition parity and natural completion, and writes only compact metadata. Original source cry hashes are recorded in its `source-identities.json`. This is signal/termination evidence, not audible listening. Move-animation SE scheduling remains incomplete.

## Verification entrypoints

- Isolated actual Godot host: `python3 tests/Game.SynthAudio.Tests/run.py`.
- Current playable-route on/off comparison: `FIRERED_COMPACT_MAPS=1 ROUTE22_UI_AUDIO=synth python3 tests/Game.Route22Ui.Tests/verify_godot.py NEW-RUN-NAME`; repeat with `ROUTE22_UI_AUDIO=off` and compare final canonical state hash and all original per-command oracles.
- Original-player 262 unit/control checks, 386 cry hashes, 111 noncry/25 loop matrix are retained in the separate audio project's phase7 evidence.

Tests retain failures as evidence rather than rewriting them as success. See the final recorded run summaries and independent host review for exact accepted source hashes.

### Accepted campaign dependency gate

Three runs on identical product source hashes passed all 392 original command oracles, 71 draws and 184 input callbacks: custom default, audio disabled and a fresh runtime content root with no WAV/OGG/QOA/MP3/FLAC paths. Their final canonical state hash, including RNG, is `2a364a2c6729915871d1fab874d58d233bc6926e9cae82024fe7d60cf3a3d499`. The recording-free run consumed 196,608 nonzero PCM frames and started SE_BOO. `tests/Game.SynthAudio.Tests/evidence/campaign-parity.json` binds the three reports.

The fresh dependency root hard-links 3,679 retained source/content files while omitting 577 audio-related files. This proves visible runtime independence from recordings; it is not yet the final compressed-backup restore test. Original source WAVs were also omitted for this runtime gate without reclassifying them as converted recordings.

Earlier failures remain recorded: old UI asset fixtures violated the new map pin/pose-restoration contract and were corrected without weakening product guards. An initial temporary-root creation hit a cross-device hard-link error. One implicit-driver run completed all route commands but its diagnostic audio capture stayed silent; its immediate failure shutdown then timed out. The runner now selects Dummy explicitly, retains timeout stdout/stderr and resets/disposes audio before failure exit. The accepted explicit-Dummy rerun has actual nonzero output. No failed run is treated as a pass.

## Recorded backend retirement

The historical `early-audio`, `audio-delivery`, `small-audio-delivery`, `repel-audio`, `town-route-audio`, and `route22-cry-audio` generated trees are no longer runtime or import dependencies. The live synth packs and original sequence/sample resources are retained unchanged. `scripts/audit-early-audio.py` remains an active shared MIDI reader and source-audit tool; it re-derives its audit from the pinned Git source and can write an explicitly requested `--report`; the redundant committed audio snapshot has been removed. Repel presentation retains its source sequencer stop at VBlank 27, independently of audio rendering.
