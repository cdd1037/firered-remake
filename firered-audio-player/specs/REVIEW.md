# Independent review of the two-song native prototype

This review uses the fixed original `pret/pokefirered` driver/data and this project's fresh C# source. No agbplay implementation was read. It is a scoped implementation review, not an audio-fidelity certification. Modern replacements are allowed; differences must remain visible rather than being presented as original-hardware reproduction.

## Verified structure and sequencing

- Original-ROM inventory supplies all required headers, instrument records, reached PCM assets and per-track event data in `inventory.json`.
- Independent comparison of the existing isolated `pallet.wav.events.txt` and `select.wav.events.txt` against that inventory finds an exact match for every first-loop track/tick/gate/key/velocity/program tuple: 520 Pallet notes and 5 SELECT notes. See `sequence-audit.json` for trace hashes.
- `audit_sequence.py` can rerun that check after regenerating the WAV/event outputs. It checks emitted notes and timing, not resulting oscillator/envelope fidelity.
- The first-loop Pallet tracks all span1536 ticks. Each starts with KEYSH then loops to the following address, retaining other live track state.
- Current parser tempo uses byte*2 with a150 accumulator threshold and tempo150 before the first tick. Its decrement-wait-at-start representation gives the same note spacing as the original decrement-wait-at-end representation for these streams.
- Current allocation implements the original5PCM cap plus separate square1/2, priority and owner-track/slot tie-breaks. Per-track gate processing now occurs immediately before that track's new commands, preserving original allocation ordering.
- Current PCM pitch uses correct sample-frequency-word/1024 and key60 reference. It is a floating equal-temperament approximation to the original integer frequency-table interpolation.

## Defects identified and subsequently addressed in source

The parent changed implementation; this reviewer did not edit C# files.

1. Unconditionally resetting LFO phase at every note was incorrect for Pallet. Original delay0 preserves phase; only MOD0, LFOS0 and a nonzero per-note delay reset it. Source now preserves phase appropriately.
2. Global per-player gate processing before all tracks could expose late-track release candidates too early. Source now updates gates per track in original order.
3. PSG envelopes originally attacked toward15 for every note and multiplied volume later. Source now computes quantized per-note goal and sustainGoal from original channel volume, preserving attack duration at note onset.
4. SELECT sweep initialization originally discarded pitch controls and used an ideal unquantized note. Source now uses the original CGB pitch table and type9/10 register rounding. For this particular effect, register values are1978 for key94 and2006 for key103; its bend+13 yields fine104 but happens to round to those same values.
5. PCM reaching sustain0 originally stayed active silently. Source now deactivates it, preserving subsequent voice-allocation opportunities.
6. Loop-flag acceptance was wider than original. Source now checks exactly status&0x4000.
7. FINE now marks release voices detached, stops sequencer gate updates, and makes their logical owner address0 for PCM tie-breaking. Original RealClearChain sets channel.track=NULL. The current source retains its old Track object for sound parameter reads; for the selected pair that ended track performs no later commands and its parameters remain stable. An extension exposing mutable stopped-track controls would need immutable detached voice parameters.

## Live PSG volume gap found and repaired

The initial implementation cached PSG goal and sustainGoal only when a note started. The original engine responds to VOL/PAN changes on already-running channels and recalculates CgbModVol during envelope progression. Current source now refreshes these targets at envelope steps, preserving current envelope amplitude, and has a separate sustain stage refreshed every7 step-clock units. Pallet's square1 track2 uses64 VOL commands per loop (including running-status commands), with extended fades on sustained notes. Square2 track4 has only its initial VOL50 at tick0. Track2's volume changes begin during the second half; track6 also fades, but it is PCM and the current mixer reads its volume continuously.

A clear test case is track2 note `(tick768, gate48, key86, velocity40, voice83)`. Original VOL moves88 at780,76 at792,62 at798,45 at804,25 at810, all before that note's gate ends816. A renderer caching only the note-start goal misses this sequence's dynamic envelope target changes. The source change now addresses that mechanism; an audio comparison remains necessary to quantify perceptual closeness.

Full track2 VOL `(tick:value)` list, as consumed from original bytecode:

0:88, 780:88, 792:76, 798:62, 804:45, 810:25, 816:100, 828:88, 840:76, 846:62, 852:100, 876:88, 888:76, 894:62, 900:45, 906:25, 912:100, 996:88, 1008:100, 1023:88, 1032:79, 1038:66, 1044:100, 1068:88, 1080:81, 1086:62, 1092:46, 1098:32, 1110:100, 1164:88, 1176:84, 1182:64, 1188:50, 1194:33, 1200:100, 1212:88, 1224:100, 1236:88, 1248:100, 1260:88, 1272:81, 1278:62, 1284:45, 1290:33, 1296:100, 1359:88, 1368:81, 1374:62, 1380:44, 1386:27, 1392:100, 1404:83, 1416:100, 1425:88, 1431:63, 1440:50, 1458:68, 1464:86, 1473:100, 1488:100, 1500:88, 1512:73, 1518:43, 1527:99

These are decoded values after assembler expression scaling (e.g. `112*100/127` becomes88). Counting literal `VOL` lines in the assembly is insufficient because running status supplies additional events.

## Deliberate or known modern-rendering differences

- Both songs request global PCM reverb50. This profile is dry. This is an intentional experiment and is not source-equivalent tail behavior.
- PCM samples are nearest-neighbor sampled directly into48kHz floating output. Original interpolates linearly with a23-bit fractional cursor into224 signed8 samples/frame. Removing original low-rate output is a legitimate design option; it remains a documented resampling difference.
- PCM frequency uses floating `pow(2, semitones/12)` instead of the original table interpolation/truncation. Square register conversion now follows the original table and rounding.
- PCM gain uses modern normalized floating volume/velocity/pan and a chosen0.22 scale. Original uses several asymmetric127/128 integer factors, masterVolume12 and signed8 accumulation. Relative balance, clipping and quantization are not identical.
- PSG uses an ideal square oscillator with duty cycles12.5/25/50/75%, a chosen0.35 relative scale, and final floating mixing. It does not implement original GBA DAC/PWM/output filtering or analog behavior.
- PSG routing in the mixer uses continuous stereo pan. Original CgbPan quantizes output to left-only, right-only or both based on a2:1 ratio test. Current songs use per-track static pan but the routing model still differs.
- PSG envelope clock is a global continuous64Hz counter. Original software shadow envelope updates each display frame, with an extra decrement every15 frames, while hardware envelope timing and phase also matter. Current code does not recreate envelope trigger phase, release-counter reload timing, or register-write-induced hardware retrigger details exactly.
- PSG sweep is modeled by a register shadow and period/shift updates, not a full APU. Hardware trigger checks, second-overflow checks, shadow/register interactions and sweep-clock phase have not been certified. SELECT's first four notes are shorter than one7/128s sweep period, so trigger-time and retrigger behavior are particularly important when comparing it.
- A restarted voice resets its ideal-oscillator phase. Exact hardware phase behavior is not proven by event tests.
- The external resource pack holds the full original audio data block. This experiment covers exactly two songs; unsupported types/commands must error, and this result does not imply cry or full-library coverage.

## Acceptance interpretation

Exact event equivalence, allocation checks, finite output, deterministic block-independent PCM, stop/restart tests and measured performance establish a functioning experimental player. They do not establish perceptual equivalence or an approved product-audio replacement. The intentional dry/48kHz/modern-DAC choices can be compared through named A/B profiles; no broad hardware-emulation commitment is needed to report this prototype honestly.

## Final rerun

After source changes for PCM zero-envelope deactivation, PSG live goals, sustain stage and FINE logical detachment were present, the regenerated event files were checked again with `python specs/audit_sequence.py`: both complete first-loop event comparisons passed. Trace hashes are recorded in `sequence-audit.json`. This remains a sequencing check, not a DSP equality assertion.
