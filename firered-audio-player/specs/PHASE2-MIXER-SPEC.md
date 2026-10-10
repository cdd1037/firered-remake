# Phase 2 mixer behavior: independent source-derived specification

Scope: the isolated original C# MUS_PALLET / SE_SELECT prototype. This describes observable arithmetic and sound state for a newly authored approximation; it is not a translation of the original ARM/Thumb driver or an APU implementation. No agbplay implementation was consulted. The existing `modern-dry` profile should remain unchanged for A/B comparison.

## Evidence and boundaries

Primary source is the fixed local `audio-reference-build/pokefirered`, revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`, matching ROM SHA-1 `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc`. Paths below are relative to that repository unless otherwise stated.

- `src/m4a.c`: initialization (71–82, 257–275, 350–425), track volume (765–792), PSG pan/envelope (878–1231), stereo selection (1758–1776).
- `src/m4a_1.s`: frame/buffer selection (20–75), reverb (88–118), PCM envelope/gain (171–277), uncompressed linear resampling (396–435), channel gain (1508–1536).
- `include/gba/m4a_internal.h` and `constants/m4a_constants.inc`: field meanings, 1584-byte channel-buffer capacity, mode values. `include/gba/io_reg.h:614–632`: named hardware routing/ratio constants.
- `sound/songs/midi/{se_select,mus_pallet}.s`, original voice tables and source-data inventory establish the instruments actually reached.
- Existing `runtime-audio-study/boot.s` calls original initialization, then `SetPokemonCryStereo(1)`, then SoundVSync / SoundMain once per frame. `runtime-audio-study/probe.c:90–96` sets 48 kHz output, emulator volume256, and warms up60 frames before starting capture.

Additional narrow reference inspection was explicitly authorized for output-unit conversion: existing mGBA0.10.5 source, tag `26b7884bc25a5933960f3cdcd98bac1ae14d42e2`, in `audio-reference-build/emulator/mgba-0.10.5/src/{gba,gb}/audio.c`. Only its numeric PSG/FIFO-to-output scaling was used below. No blip, envelope, oscillator or APU implementation is adopted. These scaling facts describe this reference exporter, not a claim that a native waveform reproduces hardware.

All `>>` operations below on potentially negative values mean arithmetic right shift (floor division by a power of two). Intermediate precision must be sufficient; source-data reads remain bounds checked.

## 1. PCM gain and pan

Compute source track gain in integer stages; do not replace these stages with a single velocity/127 × volume/127 multiplication:

    x = (trackVolume * externalVolume) >> 5
    y = clamp(2 * trackPan + externalPan, -128, 127)
    trackRight = ((y + 128) * x) >> 8
    trackLeft  = ((127 - y) * x) >> 8

For this scope, externalVolume=64 and externalPan=0. TrackPan is the signed PAN parameter after subtracting64. These songs use pitch modulation; no extra volume/pan modulation term is active. Track volume changes affect attached sounding voices, not only subsequent notes.

The plain voices reached here have rhythmPan=0. More generally, the source channel stage is:

    channelRight = min(255, ((128 + rhythmPan) * velocity * trackRight) >> 14)
    channelLeft  = min(255, ((127 - rhythmPan) * velocity * trackLeft) >> 14)

The small 127/128 asymmetry is intentional. With current PCM envelope env in0..255 and masterVolume=12:

    scaledEnvelope = ((masterVolume + 1) * env) >> 4
    gainRight = (scaledEnvelope * channelRight) >> 8
    gainLeft  = (scaledEnvelope * channelLeft) >> 8
    contributionSide = (interpolatedSigned8Sample * gainSide) >> 8

The masterVolume multiplier belongs to PCM only. It must not also attenuate PSG. Source PCM accumulates into signed8 storage with modulo256 wrap, rather than saturating each voice or retaining an infinite-precision bus. A saturating/float host mix is a deliberate alternative, not the exact operation. The ARM packed-word optimization is infrastructure: a normal scalar mixer can reproduce its resulting per-sample gain and wrap without reproducing the packing.

The original PCM envelope advances once per SoundMain frame. Attack addsA toward255; decay applies `(env*D)>>8` towardS; release applies `(env*R)>>8` toward0. A newly started voice begins at0 and applies its first attack increment in the same mix frame. A voice already STOP before START is processed never becomes audible. Pseudo-echo is zero for these songs.

## 2. Low-rate timing and interpolation

Initialization selects five PCM slots,224 mixed PCM samples per video frame, integer pcmFreq13379 and reciprocal constant divFreq627. The hardware timer interval is1254 CPU cycles:

    frameRate = 16777216 / 280896 = 59.7275005696... Hz
    actualPcmRate = 16777216 / 1254 = 13378.96012759... Hz
    actualPcmRate = 224 * frameRate

The source PCM cursor has integer sample position plus a23-bit fractional part f. Source pitch conversion returns an integer rate F; the per-low-rate-sample phase increment is `F * 627`, in units of1/2^23 source samples. With adjacent signed8 values a and b:

    interpolated = a + ((f * (b - a)) >> 23)
    phase += F * 627

The integer part of phase advances the source cursor; the fractional part survives blocks. A new note starts with zero fractional position. Preserve phase across rendering blocks and gain/pitch changes. Loop the integer cursor into `[loopStart,size)` after crossing size, retaining fractional position and overshoot, including jumps spanning more than one loop.

This is linear interpolation quantized back to integer source-sample units before channel gain. It is not nearest neighbor. Using F/actualPcmRate instead of F*627/2^23 is very close but is another rounding choice. Using continuous exponential pitch instead of the original pitch tables is also an independent approximation.

Boundary caveat: the original reads the next contiguous byte at sample[size] when interpolating from the final declared sample, then wraps after integer advancement. That byte is outside the declared sample data and may be alignment padding or adjacent data. A safe native renderer can explicitly use loopStart as the final interpolation neighbor (or repeat the final value for a one-shot), but should document the difference rather than performing an unchecked read.

For the planned phase2 host-linear profile, interpolation can instead run directly at48 kHz, with continuous source position advanced by playbackHz/48000. That removes the original low-rate sample-and-hold/quantization and is intentionally modern. Do not label it an exact224-sample/frame mixer. Keep the exact rational sequencer frame clock independently.

## 3. Reverb50 is shared PCM feedback

Both song headers set the global reverb value to50. It is applied to the PCM buffer, before adding new PCM voices. PSG bypasses it. Consequently isolated SELECT has no source PCM reverb tail, although starting SELECT can change the global setting affecting concurrently playing PCM music.

At this sample-rate setting, each side has declared capacity1584 bytes but `floor(1584/224)=7` active frame blocks, totaling1568 samples. The remaining16 bytes do not create an eighth block or extend the feedback delay. The current block and next block are read before overwriting the current block. In chronological output terms these hold audio from7 and6 frames earlier, respectively:

    delay7 = 1568 samples = 0.117198944091796875 s
    delay6 = 1344 samples = 0.100456237792968750 s
    sum = oldLeft7 + oldRight7 + oldLeft6 + oldRight6
    q = (sum * 50) >> 9
    seed = q + (q < 0 ? 1 : 0)

Both sides receive the same seed, then their own new dry PCM contributions are added. Feedback contains previous wet output, so it recirculates; this is not a pair of feed-forward echoes of the dry input. It cross-mixes the old stereo channels into a mono feedback seed while preserving the new dry stereo image. Zero reverb instead clears the target PCM block.

The negative correction is unusual: it adds1 even when a negative product divides512 exactly. Examples: sum256→25, sum−256→−24, sum0→0. It is not ordinary integer truncation toward zero. Under the bounded sum at reverb50, the original test of result bit7 is equivalent to `q<0`.

A continuous48 kHz approximation can use delays of6/7 rational video-frame lengths and feedback coefficient50/512 on each of the four terms, with floating interpolation for fractional host delay positions. Omit signed8 quantization/negative bias only explicitly. For a mono signal present equally on both sides, each delayed stereo sum contributes50/256 of that side's previous signal; the combined DC feedback factor is100/256, not50%. Keep one shared effect state across simultaneous players and their stops/starts. Cutting a player need not erase its already-ringing PCM feedback.

## 4. PSG routing, ratio and absolute reference gain

Original stereo mode:

- SOUNDCNT_H low bits are0x000e: PSG100%, FIFO A100%, FIFO B100%.
- FIFO A routes to right; FIFO B routes to left. Buffer base stores right, base+1584 stores left.
- NR50=0x77: maximum PSG left and right master settings (7 each).
- Per-channel NR51 routing comes from source channelRight/channelLeft. If right/2≥left, right only; if left/2≥right, left only; otherwise both. Only the channel's own bit pair is changed (square1 mask0x11, square2 mask0x22).

PSG pan is therefore discrete routing, not a continuous equal-power or linear pan multiplier. Once routed to both sides, the same oscillator signal reaches both. The PCM master12 does not affect it.

Source PSG target levels:

    envelopeGoal = floor((channelLeft + channelRight) / 16)
    sustainGoal = (envelopeGoal * sustainParameter + 15) >> 4

One-side routing clamps envelopeGoal at15; all reached levels are within0..15. SELECT volume80, velocity68 gives channelLeft41/right42 and envelopeGoal5; velocity127 gives77/79 and envelopeGoal9. Both route to both sides. Its five initial levels are5,9,5,9,5.

Reference-exporter numeric scale, independently derived from the allowed mGBA source:

- `src/gb/audio.c:727–777`: one square is a0/1 duty value u times envelope level e, multiplied by8, then by `1+NR50Volume` (8 here).
- `src/gba/audio.c:393–421`: full PSG ratio shifts that result by2, giving `16*u*e` in the GBA mixing domain; a full-ratio signed8 FIFO sample p contributes `4*p`.
- `src/gba/audio.c:376–383`: bias512 is added, the domain is clamped to0..1023, then bias is removed. With probe masterVolume256 the result is multiplied by48 to obtain pre-blip PCM16.

Thus for unclipped signals, normalized float (PCM16/32768) has:

    PCM contribution = p * 3/512
    PSG high-low difference = e * 3/128
    50%-duty centered PSG amplitude = e * 3/256

The full mixing-domain alternative is `float = clamp(domainSum,-512,511)*3/2048`, before host resampling/filtering. The original domain uses unipolar PSG values, so its transient DC and clipping differ from a zero-centered native oscillator. For a center-corrected duty d, `PSG=(u-d)*e*3/128` preserves the same high-low difference; for d=0.5 it is ±e*3/256. A symmetric ± oscillator at other duties has DC unless an explicit high-pass removes it.

This establishes the PSG/PCM balance and an exporter-relative absolute scale without fitting a waveform. It does not reproduce mGBA's blip filtering, hardware PWM, startup offset or phase. The old arbitrary PSG gain `0.35*0.22/15` is0.0051333 per envelope level; source/export-informed centered gain3/256 is2.282873 times larger. Changing that gain is a clear isolated A/B difference, not evidence of better waveform correlation.

## 5. PSG envelope state, especially release

Reached square instruments (A,D,S,R): SELECT voice87/88=(0,0,15,1); Pallet voice80=(0,7,0,6), voice81=(1,5,1,6), voice83=(1,4,3,6). These are interval/level parameters, not the PCM multiplicative ADSR coefficients.

- Attack0 skips attack immediately. Otherwise begin at envelope0 and increase by1 per attack interval until envelopeGoal.
- Decay0 immediately enters sustain; otherwise decrease by1 per decay interval until sustainGoal. Sustain parameter0 ends the voice at that boundary (pseudo-echo is unused).
- Sustain uses sustainGoal, and the driver's bookkeeping counter reloads7. The hardware envelope mode at sustain is period0/increase, used here to hold a level with the DAC enabled.
- First observation of STOP while in attack/decay/sustain switches to release, sets the counter toR, preserves the current level, and requests a volume-register rewrite with decrease direction. R0 cuts off immediately. Later release steps decrease by1; reaching0 turns the voice off.
- Crucially, STOP does not inherit an attack/sustain timer, and R1 does not automatically decrement the volume immediately on that STOP frame. The counter is reloaded before the frame-end decrement. The next ordinary driver frame performs the first envelope decrease. A global extra bookkeeping step can make that happen on the same frame only at its15-frame boundary.

The driver's shadow counter and actual hardware envelope are separate clocks. The shadow is serviced once per SoundMain frame, with an extra decrement/possible transition every15 frames. Its global c15 is initialized0; each SoundMain first maps0→14 or decrements it, and the frame where it reaches0 performs the extra step. It continues while silent and is not reset for each note or song. Its average rate is frameRate*16/15≈63.7093 steps/s, whereas the hardware envelope runs at64 Hz.

Hardware register writes carry the current4-bit level, direction and interval. Attack writes increase/A; decay writes decrease/D; release writes decrease/R; sustain writes increase/0. Volume updates retrigger the channel register, while ordinary hardware envelope steps occur autonomously. Replacing this with either a continuous64 Hz software envelope or the driver's shadow alone is an approximation; record the selected policy and reset the release interval correctly. The reference bootstrap's60 warm-up frames and exact write timing affect the phases, so waveform phase chasing is not a sound requirement for this profile.

SELECT's first four notes use sweep0x77, last uses source-disabled sweep0x08. Sweep changes the hardware frequency-register shadow; preserve this audible effect. Given square frequency `131072/(2048-register)`, an increasing register raises pitch. The earlier FORMAT.md phrase “pitch goes down” was a prose error; its formula and current increment direction are consistent with rising pitch. All reached length parameters are0, so no hardware length-counter expiration needs to be modeled for these notes.

## 6. Sound behavior to retain versus infrastructure to omit

Retain: original notes/tempo/loops/gates; sample identity and loop points; integer-aware gain/pan and master balance; PCM and stepped PSG envelope shapes; duty and SELECT sweep; five shared PCM voices plus fixed shared square1/square2 slots; priority/stealing behavior; independent BGM and SE players; PCM-only shared6/7-frame feedback; explicit start/stop/restart and release tails. Use deterministic clocks/state across arbitrary host render block sizes.

Omit for this bounded modern approximation: ARM/Thumb execution, RAM addresses as actual pointers, packed-word mixer tricks, BIOS helpers, DMA/FIFO transport, CPU/scanline busy waits, copied-to-IWRAM routines, hardware register emulation, unneeded PSG wave/noise channels, PCM compressed/reverse modes, cries and unused sequencer commands. Keep source ownership/order semantics as logical IDs. DAC/PWM/filter and exact retrigger phase can remain named fidelity limits; adding a generic filter must not be called a recovered original filter.

No source recordings need to become runtime assets for this phase. An isolated reference WAV remains a test oracle. Reverb/gain/tail differences are meaningful even when sample-wise phase correlation remains poor.

## 7. Small independent acceptance checks

1. SELECT levels begin5,9,5,9,5; both sides route equally; applying PCM master12 must not change its PSG output.
2. Centered50%-duty PSG e=5 has nominal amplitude15/256; e=9 has27/256, before filtering. A PCM buffer byte16 has nominal output48/512. These are numeric-domain checks, not waveform similarity thresholds.
3. A new releaseR1 preserves its level on its first ordinary STOP frame, decreases on the next, and is unaffected by the old sustain counter. Add a separate test at the15-frame compensation boundary.
4. A PCM-only impulse creates feedback at6 and7 frame delays, crosses into both sides, and recirculates. A PSG-only impulse does not. Test that stopping one player does not clear another player's echo.
5. Source-integer reverb tests include256→25 and−256→−24; source PCM gain includes negative arithmetic rounding and signed8 wrap. Float-profile tests should instead document their intentionally different rounding.
6. Linear interpolation of a=−10,b=10 at half phase gives0. Crossing a sample loop retains fractional phase and overshoot. Rendering in one block or irregular small blocks produces the same output for the chosen profile.
7. Preserve the old profile bit-for-bit in its existing tests. Report phase2 as source-informed host-linear/feedback approximation, with the remaining low-rate, APU-envelope and filtering limits explicit.
