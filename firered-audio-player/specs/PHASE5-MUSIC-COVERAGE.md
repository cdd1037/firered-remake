# Phase 5: representative existing-core music/SE coverage

## Decision

**4 of 12 selected songs execute with the existing core; 8 explicitly reject unsupported behavior.** This is useful expansion guidance, not a general-library or fidelity pass. The next required features are track/channel pseudo-echo (XCMD8/9), rhythm-table mapping, CGB programmable-wave synthesis, and CGB noise synthesis. Unknown behavior was not skipped or replaced.

The original reference remains `pret/pokefirered` revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`, ROM SHA-1 `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc`. No original-driver/agbplay comparison, new dependency, runtime edit, or main-game change was performed for this assessment.

## Harness and frozen evidence

Run `sh research-music/run.sh`. The standalone .NET10 project references `core/FireRedAudio.Core.csproj` without linking alternative implementations. It uses the existing installed SDK and only standard-library APIs. `prepare.py` verifies each requested song constant against original `sound/song_table.inc`, its 8-byte linked ROM table entry, the map symbol, and original assembly source. It inventories literal VOICE declarations and their potential key-split/rhythm leaf tones separately from executed coverage.

Evidence is under `results/phase5-music/`:

- `selection.json`: source identity, song IDs/addresses, original player IDs, assembly paths/hashes, source-located tone definitions, potential leaf types, and declared echo commands.
- `coverage.json`: source SHA-256 fingerprints, loaded core assembly hash, per-song results for both block sizes, full first-failure track/tick/cursor/tone details, counts, hashes, and diagnostics.
- Per-song `.events.txt`: accepted note and GOTO trace for the first pass; `run.log`: build and batch summary. No large WAVs were created.

The frozen loaded core SHA-256 is `04f10dcc74d895c55858cd2b7b60113774603181aa89fc2acb3b1e070e411ca9`. The script's recorded runtime source hashes were rechecked after the run and unchanged. Later feature work must be compared with this baseline, rather than treating a rerun with changed source as the same assessment.

Each BGM received a60-second render window; heal/fanfare and SE received20 seconds. Both passes reuse one interleaved stereo float buffer, at48kHz, with512-frame versus257-frame blocks and incremental raw-float SHA-256. Profile is `SourceFiltered`; tracing is enabled only for the first pass. Finite output and runtime clipping counters are checked. MaxPcm is the core's note-time peak; `ObservedMaxActiveVoices` is only a block-boundary observation. No realtime/allocation claim is made with diagnostic tracing enabled.

This is standalone playback. Original Heal uses player2; the harness maps it to available ordinary player1 because core player2 is reserved for its narrow cry implementation. Thus this is not a fanfare/BGM player-routing, preemption, or concurrency test.

## Successful execution

| Original song | ID / header | Window | Notes / GOTO | Max PCM | Result |
|---|---|---:|---:|---:|---|
| `mus_oak_lab` | 301 / `0x086ddfe0` | 60s | 1435 / 18 | 5 | Still looping; peak0.34662 |
| `mus_heal` | 256 / `0x086bccb8` | 20s | 33 / 0 | 5 | Ended; peak0.31013 |
| `se_ball` | 23 / `0x086b5b00` | 20s | 2 / 0 | 0 | Ended; peak0.11523 |
| `se_save` | 48 / `0x086b62a8` | 20s | 12 / 0 | 4 | Ended; peak0.25207 |

All four produce nonzero finite audio, zero clipped samples, and exact raw-float hashes across both block partitions. Accepted-note counts above are first-pass trace counts. Oak Lab's18 GOTO events span all looping tracks, not18 whole-song loops. Voice stealing occurs (Oak Lab633, Heal10, Ball1, Save5); no notes were rejected by the allocator in these runs. Stealing is a measured allocator action, not independently certified musical correctness.

## First explicit rejection for each failed song

Tracks here are zero-based. Each failure message and exact output-frame position matches across both block partitions. A throw can occur partway through a buffer, so `OutputFrames` is the actual core failure position; `CompletedFrames` counts only fully hashed buffers.

| Original song | ID / header | First failure | Exact identity |
|---|---|---|---|
| `mus_route1` | 291 / `0x086d1ddc` | XCMD `0x08`, track4 tick0 | Command `0x086d1a52`: `cd 08 12`; assembly `sound/songs/midi/mus_route1.s:1414` |
| `mus_pewter` | 314 / `0x086e8810` | Rhythm mapping `0x80`, track6 tick0, key36 | Program0 tone `0x0849db28`; `sound/voice_groups.inc:6203`, `voice_keysplit_all voicegroup002` |
| `mus_vs_wild` | 298 / `0x086da810` | Instrument flags `0x0b`, track0 tick0, key74 | Program87 tone `0x0849868c`; `sound/voice_groups.inc:4350`, `voice_programmable_wave_alt`, wave6 |
| `mus_vs_trainer` | 297 / `0x086d9694` | Rhythm mapping `0x80`, track6 tick0, key36 | Program0 tone `0x08497c78`; `sound/voice_groups.inc:4132`, `voice_keysplit_all voicegroup002` |
| `mus_victory_trainer` | 310 / `0x086e56e4` | XCMD `0x08`, track4 tick0 | Command `0x086e4f5c`: `cd 08 12`; assembly `sound/songs/midi/mus_victory_trainer.s:1837` |
| `mus_victory_wild` | 311 / `0x086e5c1c` | XCMD `0x08`, track3 tick9 | Command `0x086e5a12`: `cd 08 08`; assembly `sound/songs/midi/mus_victory_wild.s:692`; output frame8037 (~0.16744s) |
| `se_door` | 241 / `0x086bc670` | Instrument flags `0x0c`, track0 tick0, key53 | Program127 tone `0x0848f368`; `sound/voice_groups.inc:1127`, `voice_noise_alt` |
| `se_pc_on` | 4 / `0x086b56f4` | XCMD `0x08`, track0 tick0 | Command `0x086b56c2`: `cd 08 0a`; original `se_pc_on.s:23` path recorded in selection |

All failures except Wild Victory occur before the first output frame. These are first blockers, not exhaustive per-song requirements. For example Route1 also declares wave/noise/rhythm tones; accepting XCMD8 alone will not make it supported.

## Expansion boundary

1. Implement and separately test XCMD8/9 as per-note pseudo-echo state with original PCM/CGB behavior. Preserve running status; the selected songs contain meaningful nonzero parameters.
2. Implement rhythm mapping with child key replacement and rhythm pan, rather than only accepting composite flag0x80.
3. Add separately validated CGB wave (`0x03/0x0b`) and noise (`0x04/0x0c`) families. `asm/macros/music_voice.inc:91–137` distinguishes these types; current square support cannot stand in for them.
4. Rerun these12 songs and inspect newly exposed blockers. Then add original-driver sampled comparisons and concurrent BGM/SE/fanfare lifecycle cases before choosing a bounded integration set.

Static tone inventory includes all128 possible rhythm keys and key-split alternatives. It is a conservative potential inventory, not evidence that every listed wave/noise tone is reached in these60-second windows. Successful execution alone still leaves pitch/envelope accuracy, track semantics, cross-player priorities, reverb, actual listening quality, and target-device streaming unverified.
