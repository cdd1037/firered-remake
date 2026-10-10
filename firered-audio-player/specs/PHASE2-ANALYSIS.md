# Phase 2: frozen baseline waveform diagnosis

2026-10-09. This report concerns the unchanged phase-1 `results/select.wav` and
`results/pallet.wav`, compared with the existing original-driver stripped-bootstrap
recordings in `../runtime-audio-study/{se,bgm}-stripped.wav`. It does **not** describe
the newer profile experiments. No new reference rendering, synthesizer changes,
agbplay reading, listening test, or product integration was performed for this analysis.

## Reproduce and interpret

Run `python tools/analyze_differences.py`. The complete numerical result, source WAV
SHA-256 hashes, methods and limitations are in `results/phase2-baseline-analysis.json`.
The script uses the already-installed NumPy and SciPy, not a runtime dependency.

The phase-1 global delays are frozen: discard **43 reference samples for SELECT**
(0.895833 ms) and **852 for Pallet** (17.75 ms). Every diagnostic window uses that
same pairwise alignment. There is no new delay search, per-note phase optimization,
time warping, least-squares gain fitting, or waveform modification. The baseline
SELECT and Pallet SHA-256 values still match `results/artifact-hashes.json`.

RMS/AC-RMS ratios measure amplitude mismatch; they are not a prescription to scale
all voices together. Spectral high-band energy and normalized sample differences
are roughness proxies, not a psychoacoustic roughness score or fidelity acceptance.

## Amplitude and stereo

| Measurement | Prototype | Reference | Interpretation |
|---|---:|---:|---|
| SELECT stable final note, AC RMS | 0.025665 | 0.053002 | 0.4842×, −6.30 dB; equivalent local AC-RMS gain 2.0651 |
| SELECT entire aligned 3 s, RMS | 0.007409 | 0.016257 | 0.4557×, −6.83 dB; tail/DC differences contaminate an overall gain estimate |
| Pallet aligned 95 s, RMS | 0.039940 | 0.056110 | 0.7118×, −2.95 dB; aggregate gain 1.4049 |
| Pallet left/right RMS balance | +1.853 dB | +1.357 dB | Prototype is 0.496 dB more left-heavy |
| Pallet side/mid RMS ratio | 0.3571 | 0.3319 | Prototype is slightly wider by this measure |
| Pallet channel correlation | 0.7831 | 0.8113 | Consistent with the width difference |

SELECT is exactly dual mono in both files. Its four high-pitch stable plateaus all
have local AC-RMS matching gains 2.065–2.068. The first lower-pitch note yields 2.181,
with a stronger reference startup/DC transient and different spectral filtering.
Do not mistake the whole-SELECT gain 2.194 for a clean stationary PSG calibration.

Pallet's aggregate level difference stays near −2.94 dB across both complete loops.
Its raw DC means are **−0.0083135 left / −0.0073844 right**, versus roughly
**−0.00000056 / −0.00000061** in the reference. This is a separate output-filter/DC
issue. Mixed Pallet WAVs cannot uniquely determine isolated PCM versus PSG gain.

## SELECT onset, note schedule and release

The source event schedule is exactly ticks **0, 3, 6, 9, 12**, at output samples
**0, 1608, 2411, 4019, 4822**. The first four exact source onset intervals are
33.500, 16.729, 33.500 and 16.729 ms. Measured corresponding amplitude edges differ
by only 0.04–0.45 ms after the frozen alignment, inside the roughly 1 ms resolution
of the 2 ms AC-envelope diagnostic. There is no evidence here of a one-frame note
schedule error in those first four intervals.

The documented clock places tick-18 FINE at frame 9, sample **7233 / 150.6875 ms**.
This is a source scheduling fact, not a directly measured waveform gate. Envelope
changes occur later:

- Prototype exact magnitude steps: **156.250, 171.875, 187.500, 203.125, 218.750 ms**.
  The last is exact digital silence, with 15.625 ms spacing.
- Reference last full-amplitude cycle before its first drop: peak **165.2708 ms**,
  trough **165.4167 ms**. First clearly reduced cycle: peak **165.5833 ms**, trough
  **165.6875 ms**. These are filtered audible events, not exact register-write times.
- The same 2 ms AC-envelope midpoint detector gives prototype/reference step times
  **156.307/165.600**, **171.948/181.051**, **187.601/196.711**,
  **203.287/212.467**, **219.240/228.709 ms**. Reference steps are approximately
  **9.23 ms later** overall; intervals remain around 15.625 ms.
- Reference final clear oscillation peak: **227.750 ms**; final trough:
  **227.854 ms**. Its final >0.001 full-scale sample difference spans
  **227.854–227.875 ms**. After roughly **227.96 ms**, only a smooth decaying
  residual remains. Prototype is already exactly silent at 218.750 ms.
- Reference last nonzero PCM16 sample is **299.729 ms**, with exact silence from
  **299.750 ms**. The 240–280 ms residual fits a decaying DC exponential with
  **10.666 ms time constant, log-fit R² 0.9993**. It is not a 300 ms held PSG note.

The actionable discrepancy is the release staircase's clock/phase relative to
STOP/FINE, distinct from the output filter's later DC tail. A 9.23 ms numeric delay
should not be hard-coded as a correction: investigate the documented driver envelope
clock and hardware-trigger timing. These WAVs alone cannot identify that mechanism.

## Pitch and spectral shape

SELECT reference fundamentals are approximately **1872.46 Hz** on the first note
and **3120.76 Hz** on the others. Prototype peak estimates are within **0.24 Hz**
in the fixed stationary windows. That small fitted-bin difference is below the
independent resolution of these short windows; no meaningful tuning failure is
established. No per-note phase was optimized.

For Pallet's first 0.1–0.3 s, square1's ~1310.7 Hz fundamental overlaps stronger PCM
energy near 1321.7 Hz. Its resolved third-harmonic region is ~3932.17/3932.13 Hz,
consistent with the same ~1310.72 Hz square pitch. The square2 fundamental region
is ~109.94/109.97 Hz. These mixed-region observations do not constitute isolation
of either source or proof of the whole track's pitch behavior.

| Spectral proxy | Prototype | Reference |
|---|---:|---:|
| SELECT final stable note: centroid | 4877 Hz | 3831 Hz |
| SELECT power above 8 kHz | 17.233% | 8.988% |
| SELECT power above 16 kHz | 3.567% | 0.284% |
| SELECT normalized first-difference RMS | 0.7197 | 0.5511 |
| Pallet whole output: centroid | 809 Hz | 748 Hz |
| Pallet power above 8 kHz | 0.8207% | 0.5824% |
| Pallet power above 16 kHz | 0.2774% | 0.0461% |

Both comparisons have excess prototype high-frequency energy. Filtering/resampling
changes can address this independently of gain. Pallet's mixed envelope correlation
in fixed 10 ms RMS bins is 0.8937. Individual Pallet note gates/releases cannot be
uniquely recovered from a six-track mix, and its 95 s file end is not natural silence.

## Next bounded checks

1. Keep this baseline frozen while checking source-derived PSG/PCM gain and pan.
2. Investigate SELECT STOP/FINE release clock separately from the filter/DC tail.
3. Compare low-rate linear resampling and source-shaped reverb/filter profiles with
   identical sequencing and fixed alignment; do not optimize arbitrary phase.
4. Audition named profiles privately before declaring a sound-quality improvement.
