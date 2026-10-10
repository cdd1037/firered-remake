# Independent runtime safety review

Reviewed 2026-10-09. Scope: this isolated C# prototype, its supplied format specification, compact resource pack, and existing results. No agbplay implementation, other application source, downloads, GUI, or audio listening was used. No implementation files were changed by this review.

## Outcome

No memory-access or unbounded-sequence blocker was found for the authenticated resource pack and the two declared songs. This is a bounded, genuine sequence/sample synthesizer suitable for continued isolated testing. It is not a fidelity acceptance, whole-library/cry support claim, or realtime integration approval.

All four concrete issues identified during review were repaired by the implementation owner and rechecked:

- Unknown CLI modes/commands previously fell through to Pallet. `Program.cs` now rejects unsupported commands, argument counts and modes. Direct probes for `cry` mode and an unknown command both returned exit 1 without creating an output file.
- Sustained PSG notes previously kept their original volume goals. `Synth.PsgEnvelope` now refreshes the goal/sustain target and models a separate sustain stage. The added live-VOL regression passes. Exact hardware update timing remains outside this modern-dry profile.
- FINE previously retained the old owner for arbitration. `ReleaseTrack` now marks a releasing voice detached, and the ownership key becomes zero. The added regression passes. The prior behavior was not shown to change these two songs: Pallet's FINE is unreachable and SELECT's ended track does not issue later controls.
- Sample/duty validation previously followed live-slot mutation. All asset metadata validation now completes before slot selection or mutation (`Synth.Note`, lines 91–110); a malformed header cannot leave a partially initialized replacement voice through those paths. This ordering was verified by inspection. The final suite does not yet include a dedicated malformed-asset atomicity regression.

## Safety and implementation findings

- `Resources` clones its input and checks all byte/word/sample reads against the fixed virtual-address range. The span check uses a 64-bit end calculation and rejects negative lengths. Stored addresses never become native pointers. Pack loading checks SHA-256; ROM loading checks the declared SHA-1 before extraction.
- Song track counts are limited to 1..16; track/voice pointers are checked when consumed. Sample data extent and loop interval are checked before playback. Unsupported types, rhythm maps, nested composites, missing running status and unknown instructions throw rather than substitute audio. Final narrow inspection confirmed explicit rhythm-map rejection and key-split lookup using the original sequence key (`track.Key`) before KEYSH; the declared pair uses KEYSH 0. `Sequence.cs` declaration wrapping does not change field defaults or execution behavior.
- Each track tick permits at most 4095 decoded commands before throwing. Pattern depth is limited to three. Sequence tempo bytes cap the ordinary frame's tick count; zero tempo is rejected. The no-wait GOTO regression terminates with the intended error.
- The five PCM slots are structural. Inspection confirms free-slot precedence; otherwise releasing voices precede active candidates, with priority, owner address and final slot-index tie breaks. PSG uses two separate exclusive slots. Denied notes still leave parser key/velocity state updated.
- PCM output reads signed samples from the resource image at runtime; PSG output is generated from duty, pitch, envelope and sweep state. No runtime recording loader, WAV playback substitute, native emulator or external package dependency is present. The comparison script's recordings are analysis inputs only.
- The rational frame accumulator and player tempo state persist across render calls. The wait-counter implementation differs in placement from the prose specification but preserves the observed event timing.

## Verification performed

- Fresh Release build: zero warnings, zero errors.
- Fresh final selftest: **30/30 checks passed**. This includes live-VOL, FINE detachment, identical-owner highest-slot arbitration, free-slot precedence, ranking among release tails, and pre/post SELECT snapshots of PCM and an already-live square2. The total counts two loop-reached checks, one for each timeline render.
- A **55-second** start/SFX/restart/stop timeline was rendered twice: whole event-bounded segments versus 257-frame blocks. Both pass the restarted-loop trace assertion; every output float is exactly equal. Scheduled actions occur at 2, 3, 6 and 7 seconds, and the restarted BGM crosses its first full loop before the comparison ends.
- Independently compared existing event traces with `inventory.json`: all **520 Pallet first-loop notes** and **5 SELECT notes** match track, tick, gate, key, velocity and program.
- Independently checked every note/GOTO trace timestamp against integer rational-clock formulas: no mismatches. All six Pallet tracks loop together at ticks **1536** and **3072**, output samples **2104760** and **4208715**.
- At the initial review snapshot, existing `mix.wav` and `mix-repeat.wav` and their event traces were byte-identical. These were pre-PSG-repair artifacts. Final WAV regeneration and artifact-hash verification are being performed by the implementation owner and are not claimed by this review; no WAV was rendered by this reviewer.
- The reported zero-allocation benchmark is a warmed, trace-disabled render test. Default trace-enabled rendering allocates. The benchmark does not establish startup allocation behavior, audio-device latency or target-device realtime safety.

## Remaining boundaries and follow-up tests

1. The single-owner-thread requirement for Start/Stop/Render is now documented in source. A future audio callback integration must honor it or introduce serialized commands. No concurrent-use or device-output test was performed.
2. Malformed sample headers/loops, invalid jump targets, pattern overflow, source-hash rejection, supported extra opcodes, ties and key-split edge cases are not comprehensively covered by this suite. In particular, prevalidation ordering was inspected but not exercised through a malformed-sample atomicity regression. Do not infer full-library support from partial implementation.
3. Reported comparison metrics explicitly reject fidelity equivalence. Reverb/PWM omission, host resampling/mixing, PSG pan/phase/envelope/sweep details and gain differences remain audible/behavioral boundaries. Note-count equality and nonzero WAVs are structural evidence, not fidelity evidence.
4. The benchmark remains warmed and trace-disabled, on this host only. A future streaming demo needs cold-start/load measurements, a callback/underrun test and target-device validation. No whole-library, cry, bit-perfect, listening-test or production-readiness claim is justified.

## Final reviewed source identities

The final expanded/formatted source, including explicit rhythm-map rejection and the original-key key-split correction, was rebuilt successfully with zero warnings/errors and passed all 30 checks after final hash reconciliation. SHA-256:

- `src/Program.cs`: `a825a59629dcb2c5d0478552d3e00b1d27baa98ec52ccaae81519db52281f923`
- `src/Resources.cs`: `0846dccd72f2c0f446aaa82fc2c5846ea9e6b58eaa66cae9ea653d71c1f26295`
- `src/Sequence.cs`: `aec19f11a0d1eebc5e6cc321b978cbd00a6b890f0a3a19aa183f87b707242774`
- `src/Synth.cs`: `853aff4abff9a1f331b6659e4c1cc44a0bc32a513919e379d43eb9a2bef31ac1`
- `src/Verification.cs`: `b861125398b5148b4ed62c0b48602fd775dd4e75b93da435a7d6b1ae58f9ca71`
- `resources/firered.pack`: `00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d`

Later edits require rerunning affected checks. No unresolved correctness blocker was found within the declared authenticated-pack, two-song, single-threaded modern-dry offline scope.
