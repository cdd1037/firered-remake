# Phase 3 independent review: ordinary Charmander cry

## Review status

**Code review and 79-test evidence review: no blocking defect found in the explicitly bounded ordinary-Charmander path.** The implementation author's cry-specific run, its assertions, and the final runtime-source changes have now been inspected. This is not a bit-exact hardware, general cry-library, overlapping-cry, realtime-device or listening certification.

Reviewed `src/Cry.cs`, the dynamic/XCMD additions in `src/Sequence.cs`, compressed sample and cry lifecycle changes in `src/Synth.cs`, global reverb state in `src/Profiles.cs`, and CLI additions. Compared them against `PHASE3-CRY-SPEC.md` and the fixed original `pret/pokefirered` source, map and resources. No third-party player implementation was consulted. This review does not change C# or the GUI.

## Source semantics and bounds

- **DPCM:** The seed, ignored first packed high nibble, first low nibble, subsequent high/low nibble ordering, per-block reset and unchecked signed8 wrap match original `SoundMainRAM_Unk2`. `RequiredBytes(7134)` correctly returns 3679. Sample-count limits, declared-payload length, and immutable-resource address checks precede allocation/reads.
- **Partial final block:** The decoder emits only requested values and never expands nonexistent full-block padding. `PrepareNormalCry` requires the exact fixed header and decodes 7133 playable values plus the genuine guard at 7133. Rendering keeps playable `Length=7133` and uses that guard only for final-interval interpolation. The guard is not played as an extra sample.
- **Independent asset oracle:** The separate Python inventory compared every one of 7134 decoded values with the original unsigned8 source WAV after bias conversion, finding zero differences; it also rejected a truncated payload. The C# decoder's equations are consistent with that independently checked oracle. The C# full-buffer SHA-256 assertion now matches that oracle; see the final addendum.
- **Dynamic memory:** `SequenceImage` clones its byte array and resolves all dynamic reads/jumps within that one address domain. A dynamic GOTO cannot escape to source ROM or original RAM. Track 0 and its GOTO relocate correctly. The inactive track 1 pointer remains unused because chorus 0 creates exactly one track.
- **Fixed API:** `NormalCharmanderCry.Build` rejects species other than 4 and mode other than 0 before a live cry is stopped, a player is replaced, or BGM ducking changes. Other compressed wave addresses and tone flags are rejected by note setup; nonzero XCMD progress and unknown subcommands throw. Runtime ROM/pack loader identity validation remains unchanged.
- **XCMD:** The implemented 7/12/13 subset consumes the correct 1/2/4-byte payloads. Release override affects the note being created. Progress is bounded to 0. Extended wait repeats its own XCMD until timer 140, then clears the timer and consumes EOT/FINE. The host's decrement-at-next-tick wait representation differs internally from the original decrement-at-end-of-tick representation but yields the same tick 0 note and tick 140 EOT.

## Audio/lifecycle behavior

- **Pitch/envelope:** Key 60, TUNE 64, velocity 127, volume 120, pan 0 and priority 10 are correctly established. Type 0x20 remains pitched at 10512 Hz, with no fixed-rate or reverse flag. Attack 255 reaches full envelope immediately; decay 0 is raised to sustain 255 rather than creating a silent frame. Release 0 is immediate when a release occurs.
- **Natural ending:** A non-looped 7133-sample voice ends around 0.67856 s at its 10512 Hz pitch. The sequence remains in xWAIT until about 2.34398 s. `CryPlaying` observes the active cry voice; duck restoration is therefore independent of the longer track duration. `Ended` can remain false while xWAIT is still running, which is consistent with its all-players-complete meaning.
- **Priority/ordering:** Cry shares the five PCM slots with BGM and has priority 10 and original first-track owner 0x03007180. The existing allocation/tie-break code is reused. Frame sequencing BGM→SELECT→cry matches the original initialization order, because original MPlayMain recurses into its previously registered player before processing itself.
- **Ducking:** All BGM tracks get external volume 21 (`85>>2`), a two-frame hold is used, and restoration goes to 64 after natural voice completion. SELECT and cry tracks are not ducked. Starting BGM during an existing duck reapplies 21. Stopping a cry leaves only the bounded update-frame restoration delay, not the 140-tick sequence wait.
- **Reverb:** Cry's header 0 preserves global reverb. A new synth has amount 0; starting Pallet/SELECT sets 50 through header 0xb2; later cry leaves that value alone. Explicit global state fixes the former temptation to interpret cry header 0 as “disable reverb.” Delay history remains in its normal shared effect path.
- **Hot path:** Decompression is cached outside Render; `PrepareNormalCry` can explicitly prewarm it. Subsequent rendering reads cached signed samples and does no sample-file IO or decoding. Start/retrigger remains a control operation that allocates the bounded sequence/player objects; no target-device realtime claim follows from offline speed.

## Intentional limits and documentation nits

1. **Single cry slot:** `StartNormalCry` stops/replaces player 2 on every accepted trigger. Original FireRed has two cry slots and can overlap two cries. The current safe deterministic replacement policy is acceptable for this smaller phase, but must be documented; it is not an exact reproduction of original multi-cry allocation.
2. **Limited command semantics:** The supported template has one VOICE followed by xRELE 0, progress 0 and xWAIT 140. The stored `ReleaseOverride` must not be advertised as a complete arbitrary-XCMD/tone-edit implementation. Generic VOICE-after-XCMD restoration, arbitrary progress, chorus, reversed cries and additional species are outside this review.
3. **Rendering approximations:** The selected source-filtered profile remains a host-rate approximate renderer. Guard correctness and exact source decoding do not imply GBA integer-mixer/PWM/filter equivalence. Hardware onset and effect tails require the separate rendered-reference analysis.
4. **Documentation:** The initial blanket exclusion of cry support and missing CLI modes have been corrected. The phase report should additionally retain the explicit single-slot replacement limitation above.

## Evidence available at initial review

- `results/phase3/build.log`: CLI and core-library builds both succeeded, 0 warnings/errors.
- `results/phase3/existing-tests.txt`: 48 prior bounds, sequence, allocation, timeline and profile tests passed. This log is prior-regression evidence; it is not a replacement for the new cry-specific tests.
- `results/phase3/charmander-first.wav.json`: 4 s, 48000 Hz, 1 maximum PCM voice, 0 clipped samples, 0 steals/rejections; peak 0.271486 in SourceFiltered.
- `results/phase3/cry-mix-first.wav.json`: 12 s, 48000 Hz, 5 maximum PCM voices, 0 clipped samples; SourceFiltered peak 0.353704. Stealing/rejection counts are informational and need the dedicated allocation assertions to classify.
- `cry-mix-first.wav.events.txt`: duck-on at output sample 144000; cry note-on at 144657, tick 0, key60, velocity 127, gate=-1, priority 10; duck-off at 177607. This demonstrably restores after about 0.68646 s of voice/frame activity rather than waiting 140 ticks.
- `specs/phase3-cry-inventory.json`: matching ROM/asset, all 7134 samples equal to original WAV, guard -2 and truncated-input rejection independently verified.

Initial reviewed runtime-source SHA-256 values:

- Cry.cs: `fcdec88657a99947ca548873ea0117fa5a8851c6d451a287d16996d0867921ae`
- Sequence.cs: `7519dc52dbfc5583ce066d446822937ffa39ebfbc9b05e689e3878aea3200bd1`
- Synth.cs: `aa60777b8e2987dbf96aae669b698f89ccbcdde42f0989fc4fe9274326366886`
- Profiles.cs: `ef3ccb2321d8c91d977da10ddb2e510d82fe6b6a231db5fa74a1b135a2ac4af1`

## Final verification addendum

The implementation author's `results/phase3/tests.txt` now records **79 passing tests**, including 31 cry-specific assertions. I inspected `src/CryVerification.cs` and the log rather than inferring coverage from the total. New checks cover all 7134 decoded values through the independent original-WAV hash, exact guard/size, truncated/range/allocation bounds, synthetic nibble order and signed wrapping, unsupported species/mode, dynamic bounds and ROM-escape rejection, unknown XCMD/nonzero progress, no-wait-loop budget, natural sample ending before xWAIT, tick-140 FINE, one-voice/no-clipping solo output, replacement/cancellation/duck restoration, concurrent chunk independence, failed-request duck-state preservation, and zero managed allocation in prepared cry rendering.

The final source changes since initial review add fixed tone validation before playback, trace-only sample-end/FINE events, an internal read-only ReverbAmount observation property, and normal code formatting. They do not change the reviewed decoding or rendering algorithms. Current reviewed hashes after final formatting:

- Cry.cs: `9ac9fe79b1dbb275c2a50d5449d3fc34d6720dc806afbe7aa7215102b6931c3b`
- Sequence.cs: `43039a0fab5f89a35df666f3520f3b0607583ef3a0f3af4980df31dd15a50175`
- Synth.cs: `2a8c6b29ba2c0905e13965b7301d463a00f0910aabf49dd45ea895a2108115bf`
- Profiles.cs: `8f71ddb91c0039221beeda005861d2c1e4f8cfeab417f1fd545970a21e3ec413`

Latest `results/phase3/build.log` shows both builds passed with zero warnings/errors. Final `charmander.wav.events.txt` records natural PCM exhaustion at sample 32571 and tick-140 FINE at sample 112511. In the concurrent `cry-mix.wav.events.txt`, cry starts at sample 144657, naturally ends at 177228, BGM restores at 177607, and its independent tick-140 FINE occurs at 257168. This verifies the important distinction between sound lifetime and score lifetime under concurrent BGM/SELECT playback.

`results/phase3/benchmark.json` reports 20 cry starts over 60 rendered seconds, zero Render allocations, 22720 control-operation allocation bytes, no GC collections, maximum five PCM voices, and no clipping. Its approximately 116x offline throughput is machine/workload-specific and does not establish target-device realtime performance.

`results/phase3/comparison.json` reports correlation 0.995158 against the stripped original-driver output after one fixed 855-sample offset, PCM16 RMSE 99.94, and `exact=false`. It separately labels the earlier full-game-export reference. The method explicitly avoids gain fitting, time stretching, and per-note alignment. These diagnostics support a close bounded approximation; they do not turn this review into a bit-exact or listening pass.

The four additional final assertions were inspected and passed: fresh cry inherits reverb 0; cry after Pallet preserves reverb 50; a priority-10 cry takes exactly one of five occupied BGM PCM slots while total active PCM remains five; and BGM started during a cry inherits external volume 21. These close the two initial targeted coverage recommendations and add a restart check. The failed replacement test directly asserts preserved duck state; existing voice/player preservation is additionally evident from validation occurring before mutation.

I independently verified the three regenerated SourceFiltered rollback WAVs against both the stored baseline WAV bytes and the manifest in fixed commit `284cb45f35b5bd4391bb2086909f730a7fdc8837`. All three are byte-identical:

- Pallet: `c77163484175719953616df505b7c14eafb48464566c06b695f478f80770d23a`
- SELECT: `04035788ba810bf30cd4da42d01a728e108f03ec780d36beacea57917517b733`
- Mixed timeline: `83d3348dc979517d6b196a862aed00a782167b9ccc1795285374e6afcefce90d`

No additional code-review blockers remain for the stated phase scope.

**Conclusion:** approved for this phase's isolated, offline, ordinary-Charmander/single-slot scope, subject to retaining the stated limitations. No C# or GUI files were changed by this reviewer.
