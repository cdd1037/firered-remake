# Independent review: bounded phase-2 audio profiles

Reviewed 2026-10-09 against frozen commit `cf34cbc1ba2e519f199ca1441a7736575d772e48` on `investigate-audio-profile`, including the final `source-pitch` ablation and the three fixes below. Scope is the authenticated resource pack, MUS_PALLET and SE_SELECT, their simultaneous-player timeline, and the named offline experiments. No implementation files were edited by this reviewer; additional probes used a temporary standard-library-only C# harness. No agbplay, new package, GUI, device output, listening test, or main-game code was used.

## Verdict and repaired findings

**No unresolved correctness or safety blocker was found within this bounded offline scope.** The default remains bit-identical for the two frozen songs. The experiments remain source-informed host-rate alternatives, not a true APU or a fidelity-approved replacement.

Three concrete findings were reported, repaired by the implementation owner, and independently rechecked:

1. **Effect tails were excluded from `Synth.Ended`.** After one second of Pallet followed by `Stop(0)`, the initial implementation reported `Ended=true` despite another second of audible feedback. `Ended` now includes `SourceDelay.IsSilent` and `DcBlocker.IsSilent`. Both wet profiles remain unended immediately after stopping; the tested tails settle within three subsequent one-second render blocks. Stopping does not erase the shared PCM effect state. The silence condition is explicitly a sub-quantization state threshold, not mathematical zero.
2. **Undefined public enum values selected accidental profile combinations.** Values −1 and 999 previously rendered successfully. The constructor now rejects both with `ArgumentOutOfRangeException` before creating usable synthesis state. The CLI independently rejects unrecognized profile strings.
3. **The driver-shadow experiment sounded a voice already released before its first mix frame.** The START branch previously took precedence over STOP. A newly noted SELECT voice followed by `ReleaseTrack` before the first `Render` is now inactive and produces zero samples in `SourceEnvelope`, matching the source START+STOP guard.

All three now have permanent regression checks in the working tree’s `ProfileVerification`; this review also exercised them independently. The phase-2 working-tree changes had not yet been committed when this review was written.

## Verification performed

- Fresh Release builds of CLI and core: **zero warnings, zero errors**.
- Fresh `selftest`: **48 checks passed** (30 existing checks plus 18 profile checks).
- Independent **55-second timeline for every one of the eight profiles**: Pallet start; SELECT at 2 and 3 seconds; Pallet stop at 6 seconds and restart at 7 seconds; then play through a restarted loop. Whole event-bounded rendering versus 257-frame blocks produced exactly equal, finite floats for every profile.
- The phase-2 SELECT event traces are byte-identical across all eight profiles; so are the Pallet event traces. DSP selection has not changed the emitted sequence schedule in these artifacts.
- Fresh default renders, existing phase-1 WAVs, and phase-2 `modern-dry` WAVs all match the frozen hashes below. The comparison is full-file SHA-256, not approximate audio comparison.
- Invalid CLI profile, extra argument, missing arguments, invalid benchmark profile, and unsupported `cry` mode all returned exit 1 without creating a WAV.
- All 16 song/profile WAV hashes match their entries in `results/phase2/profile-comparison.json` after the pitch-profile regeneration.

| Default render | Duration | Frozen and rerendered SHA-256 |
|---|---:|---|
| SELECT | 3 s | `ca8bf7b623908a8f6e549bf4c780089d20d3a0792322817a51aab950a02fff88` |
| Pallet | 95 s | `f57475baa754be9642af2a6a52a95d58c963ebd3ef0dc5e977204c0f060cbbd9` |

The ordinary reproducible checks are `./build.sh` and `./run.sh selftest resources/firered.pack`. The additional full-profile timeline, impulse/state, release-boundary, and pitch-table oracle checks above were independent review probes; not all are currently permanent selftests.

## Profile separation and clocks

The effective profile graph is intentional:

- `ModernDryV1`: frozen gain, nearest sample, ideal square, continuous 64 Hz envelope.
- `SourceGain`: source-derived gain/routing and duty-centered square; original continuous pitch and nearest sample remain.
- `SourceLinear`: SourceGain plus safe host-rate linear PCM interpolation.
- `SourcePitch`: SourceLinear plus source table interpolation and integer playback Hz.
- `SourceEnvelope`: SourcePitch plus driver-shadow envelope experiment, **without** reverb or polyBLEP.
- `SourceReverb`: SourcePitch plus shared PCM feedback, retaining the continuous 64 Hz envelope.
- `SourceBandlimited`: SourceReverb plus modern polyBLEP square smoothing.
- `SourceFiltered`: SourceBandlimited plus a modern DC blocker.

In particular, later enum values do not inherit the shadow-envelope clock. `Frame()` invokes it only for `SourceEnvelope`; the global 64 Hz accumulator invokes the old envelope for all other profiles. The exact rational sequence/PCM-frame clock is retained and independent of either PSG-envelope policy. Effect and clock state survive arbitrary render block boundaries and player restarts.

For shadow release R1, an ordinary STOP frame preserves level 9 and resets its counter, and the next frame reduces it to 8. At the global 15-frame compensation boundary the extra step instead reduces it on the STOP frame. Both cases were probed directly, including an old sustain counter that would otherwise contaminate the interval. This checks the chosen shadow model; it does not establish the autonomous hardware-envelope/register-retrigger timing.

## Arithmetic and DSP review

### Gain, routing, and amplitude units

`ChannelVolume` retains the specified staged track and channel integer gain, including the intentional 127/128 asymmetry. PCM master volume 12 affects PCM only. In the source-derived PCM path the expression simplifies to:

`normalized contribution = interpolatedSigned8Sample × gainByte × 3 / 131072`

This is the specified floating alternative to `(sample × gainByte / 256) × 3 / 512`. The source gain-byte truncation is retained; source per-sample integer rounding and signed-byte accumulation wrap are intentionally omitted. These differences are not hidden as exact original arithmetic.

For PSG the duty-centered square and final multiplier simplify to `(u − duty) × envelope × 3 / 128`. At 50% duty this is ±`envelope × 3 / 256`; the exact level-9 test produces `27/256` equally on both sides. Discrete side routing uses the source 2:1 tests; PCM master gain does not enter that branch. Centering non-50% duty is itself a modern DC choice and should remain visible when interpreting comparisons.

### Source pitch and interpolation

The original `MidiKeyToFreq` formula was checked against the fixed local source and resources. `gFreqTable` is at `0x08489ABC`; all 180 bytes of original `gScaleTable` match the implementation's octave/semitone decomposition. An independent oracle reading both original resource tables matched **11,456 key/fine combinations** (keys 0–178, fine offsets 0,4,…,252) exactly, including octave crossings. The explicit quarter-tone test gives 13,577 Hz.

Products use adequate unsigned 64-bit precision. Converting the stored source frequency word to `/1024.0` and back is exact for the 32-bit word. Interpolation and the final product round at the specified integer stages. Host cursor advancement still uses integer Hz divided by 48 kHz, not the original low-rate `F × 627 / 2^23` increment. This remains a host-linear approximation.

The final interpolation neighbor is loopStart for a looping sample or the final value for a one-shot, so no byte beyond the declared sample is read. Loop modulo retains fractional phase and multi-loop overshoot in the intended positive-frequency range. This deliberately differs from the source driver's contiguous byte beyond the declared sample boundary.

### Shared delay: causal and bounded

The 6/7-frame delays are approximately 4,821.8994 and 5,625.5493 host samples. Both fit safely within the 6,000-sample rings, and both reads occur before writing the current wet output. Fractional interpolation uses nonnegative weights summing to one. A one-sided impulse produced:

- first echo at samples 4,821/4,822;
- second echo at 5,625/5,626;
- equal left/right feedback at both delays;
- recirculation starting at 9,642, confirming wet feedback rather than only feed-forward echoes.

The feedback sup-norm factor is `4 × 50/512 = 0.390625`, below one. For bounded input magnitude M the channel magnitude is bounded by `M / (1 − 0.390625)`, about `1.641026M`. A long dual-mono constant-input probe converged to that bound without exceeding it. A fixed ring prevents unbounded memory growth. SELECT remains exactly unchanged between the relevant dry and reverb profiles because PSG bypasses the PCM bus.

`IsSilent` tracks the sum of absolute stored ring values with a `1e-7` threshold. It is conservative relative to PCM16 quantization in this workload. It neither clears the ring nor changes subsequent samples. Very long-running numerical drift in this incremental bookkeeping has not been endurance-tested; it is not a current two-song blocker.

### DC blocker and polyBLEP

The DC blocker is `y[n] = x[n] − x[n−1] + (511/512)y[n−1]`. Its pole is strictly inside the unit circle, its zero is at DC, and its corner is about 15 Hz at 48 kHz. The constant-input test decays below `1e-8` after 10,000 samples. This is a modern high-pass/DC treatment, not a recovered original low-pass, PWM stage, or emulator blip filter.

PSG frequency comes from a bounded register denominator, giving finite positive `step`; the edge helper does not divide by zero for valid reached data. A 32-frame-resolution scan across both full songs observed maximum step 0.06502 for SELECT and 0.03690 for Pallet, comfortably below Nyquist. The two edge corrections are applied with the appropriate opposing signs and wrap. The finite checks and long profile timelines passed. This does not certify the oscillator's anti-aliasing quality for unsupported above-Nyquist notes or arbitrary externally mutated voice state.

## Startup, failure behavior, and API limits

Unknown CLI profiles fail before `File.Create`, and no mode silently becomes Pallet. Public enum validation now fails closed as well. The immutable authenticated resource checks, unsupported-format/opcode exceptions, shared voice limits, and single-owner-thread contract remain intact. The new frequency-table reads are bounded and constructor-time; DSP buffers are also allocated at construction, not per sample.

Startup and trace-enabled rendering still allocate; the warmed trace-disabled benchmark cannot establish cold-start or audio-callback readiness. The benchmark report now identifies its profile. No concurrency, device underrun, suspend/resume, or target-machine latency claim follows from offline throughput. A render-time exception can still leave a partial WAV under the pre-existing CLI workflow; a failing exit code must not be interpreted as a completed artifact.

## Claims versus measured evidence

The comparison scripts use frozen reference crops (43 samples for SELECT and 852 for Pallet), fixed windows and no per-profile phase/gain fitting. The quick comparison JSON is in **PCM16 units**; the detailed analysis normalizes by 32768. Those units should not be mixed when reporting thresholds or gains.

The final source-filtered candidate improves amplitude/envelope agreement, DC, and selected high-band metrics. It does **not** generally improve sample-wise waveform agreement:

- SELECT: waveform RMSE is about **437.26 versus 423.02** for the frozen baseline, despite correlation increasing from 0.6332 to 0.6563.
- Pallet: waveform RMSE is about **1372.24 versus 1225.27**, and correlation is **0.7202 versus 0.7612**. Its active 10 ms envelope RMSE is nevertheless about **274.75 versus 579.15**.
- Source-pitch modestly changes Pallet correlation (source-linear 0.6912 to source-pitch 0.6949); table-correct arithmetic is not proof of an overall fidelity gain.

SELECT's continuous-clock release remains approximately 9.2 ms early; the shadow candidate shifts it differently and is not validated as a superior replacement. The longer original-driver transient/filter residual is still absent. Low-rate integer mixing, signed-byte wrap, exact APU trigger/envelope/sweep phase, DAC/PWM and original filtering remain outside the implementation.

Therefore the supported outcome is an isolated, deterministic set of audition candidates with measured improvements in specific metrics. No listening occurred, and neither perceptual preference, original-hardware equivalence, whole-library/cry coverage, nor permission to integrate into the main game is established by this review.

The implementation owner preserved the independent temporary harness and its final output as `results/phase2/review-probes.cs.txt` and `results/phase2/review-probes.txt`. The harness is evidence, not runtime code; its local resource path needs adapting when reproduced elsewhere.
