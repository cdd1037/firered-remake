# Independent phase 6 candidate review

2026-10-09. Reviewed the frozen working-tree wave/noise implementation, sequence corrections, tests and final music evidence. Core Release SHA-256: `f9f56e06fb3eb61850a567fa2e177d349d3fe8fe85e150a6cc296b2463b2624f`. No runtime source was edited during review.

## Decision

**No blocking defect found for this bounded standalone runtime candidate.** It is suitable for isolated integration/listening trials with the documented host-oscillator behavior. This is a functional/data-integrity assessment, not bit-perfect, all-library, physical-device or subjective listening acceptance. Default product audio replacement is a separate decision.

## Independently verified evidence

- All **47 source/evidence hashes** in `results/phase6-music-final/checkpoint-hashes.json` match current files, including the exact core DLL above.
- Re-ran the current executable: **178 self-tests pass**; re-ran all **386 catalog render hashes**, exact against the prior research output. This review's catalog run took5.0335652s; timings are host observations, not a real-time deadline guarantee.
- Verified the final12-song evidence against each original source file and its hash. Independently split original top-level track labels and matched GOTO-bearing tracks to actual trace entries: **12/12 successful,57/57 source-declared looping tracks covered,5/5 finite pieces ended**.
- Both512/257-frame passes have matching raw-float hashes, finite output, zero clipping and complete requested frame counts. Total duration is640s per partition. Pewter and Trainer Battle correctly use120s windows so their late first loops are actually reached.
- The five previously passing song hashes remain exact. Independently hashed all eight rerendered legacy WAVs in `results/phase6-independent-run` and the corresponding original baseline WAVs: all match the saved expected hashes.
- Independently checked all11 source-declared programmable-wave `.incbin` files: each is exactly16 bytes and equals its fixed ROM extent.

This review did not run a second entire1,280-second music matrix; it verified its authenticated source/evidence, traces and invariants, while independently rerunning the focused tests and catalog gate.

## Wave/noise code and source contract

`PrepareWave` validates a complete16-byte resource span and an aligned member of the fixed11-record whitelist before selecting or replacing a live channel. It decodes the original high nibble then low nibble into32 values. No PCM-header guess, next-symbol extent guess, sine substitution or recorded WAV is involved. The original noise pitch table and wave volume-class table are checked against the immutable pack by the tests.

Wave clock/register handling and its volume classes match the bounded source contract. Host-sample integration averages the original held nibble values over each fractional step and keeps phase across block boundaries. The source shape is retained; DAC truncation, startup stale samples, PWM timing and wave-RAM access behavior remain intentionally unmodeled. The reviewed format/clock rules agree with [GBATEK's wave-channel documentation](https://rust-console.github.io/gbatek-gbaonly/#gba-sound-channel-3---wave-output).

Noise uses the documented low-bit/right-shift/XOR Galois recurrence, seeds0x4000/0x40 and15-/7-bit periods32767/127. The tests cover both complete cycles, output balance and known prefixes. Its divider-zero case and high-shift clock stop follow [GBATEK's GBA noise description](https://rust-console.github.io/gbatek-gbaonly/#noise-random-generator-aka-polynomial-counter) and [Pan Docs' NR43 definition](https://raw.githubusercontent.com/gbdev/pandocs/master/src/Audio_Registers.md). The explicit initial output, phase and polarity are host conventions, not a claim to original first-bit/retrigger identity.

Pool allocation remains five PCM slots plus four separate CGB slots. Wave owns index7 and noise index8; higher-priority replacement cannot steal an unrelated family. Invalid wave pointer/alignment, invalid noise width, nonzero hardware length and unsupported flags reject before channel mutation. Tests verify preserved active voices on rejection, family-isolated stealing, lower-priority wave rejection, hard stop, noise reset and finite/block-independent output for types3/11/4/12.

The new paths do not modify old square/PCM arithmetic. Echo and both PSG-envelope loops include the two added slots. Wave hardware volume may mute a low envelope while its voice remains owned, which is correctly distinct from voice lifetime. The selected matrix reaches all five source voice families; it does not demonstrate every possible original instrument or length mode.

## Sequence corrections close the previous findings

The earlier independent EOT fixtures were rerun against the final executable:

- `TIE60; wait; EOT61; wait; TIE` now remembers61 and gives the final omitted-key note MIDI key61.
- Two overlapping TIE60 notes followed by one EOT60 release exactly one voice, the newest eligible one. A later EOT can release the next eligible note.

Code/tests preserve detached and already-releasing exclusions, retain eligibility for a PCM zero-sustain echo carrying ENV state, and skip a CGB IEC-only tail. This closes the two inherited findings in `PHASE5-ECHO-RHYTHM-REVIEW.md` for the stated model.

Depth-zero PEND is now the original no-op, matching `src/m4a_1.s:870–884`. Actual pattern overflow, escaped pointers and zero-wait instruction loops remain bounded explicit errors. Replacing the old underflow-rejection assertion was justified by original behavior, rather than suppressing a failure to inflate coverage.

## Remaining acceptance boundaries

The five original-driver comparisons retain command-frame alignment, lag0 and unadjusted gain. I checked their actual native/reference file hashes. Whole-window native/reference RMS ratios are approximately1.01037,0.99490,0.96181,0.98326 and0.98852 for PC On, Door, Route1, Pewter and Wild Battle. All are non-exact; envelope RMSE, peaks and spectral bands show real remaining differences. Similar whole-window RMS is not proof of matching note/envelope behavior, and noise waveform correlation is not a perceptual score.

Observed first nonzero samples, without fitting, are native/reference: PC On1/33, Door0/59, Route1 0/122, Pewter0/103, Wild Battle0/117. The earlier isolated Charmander files begin1/851. A plausible contributing mechanism is the original split pipeline: `SoundMain` updates CGB registers, then mixes PCM into buffers, while `m4aSoundVSync` schedules PCM DMA (`src/m4a_1.s:51–78,1070–1127`; the probe polls VSync before SoundMain). That is consistent with a roughly frame-sized additional PCM startup latency, but has not been isolated as a universal correction. Mixed PCM/PSG streams may therefore have family-relative onset differences. **Do not borrow the cry's855-frame fitted lag or chase each component's phase.**

The finite/frame checks do not prove allocation-free rendering. A first-used wave creates its32-byte cache and dictionary entry in the note/render path; existing instrument/control/tracing allocations also remain. Cache source bytes are bounded, but prewarming and target-host deadline testing are still relevant before a strict real-time callback deployment.

As a separate practical streaming observation, I inspected the final58.0099s Godot result/log: all-family mask31, zero clipping, baseline0 skips, zero unplanned skips after deliberate-stall recovery,8192 muted frames with RMS0, and clean shutdown messages. There were4 startup skips and67 deliberate-stall-period skips; it is incorrect to call the entire run underrun-free. The5120-frame target is about106.7ms of queued48kHz audio. This is isolated Dummy/headless streaming evidence, not audible-device latency or listening approval.
