# Phase 2 modern candidates: fixed-alignment measurements

2026-10-09. This supplements, and does not replace, `PHASE2-ANALYSIS.md` or its
frozen baseline JSON. Reproduce with `python tools/analyze_modern.py`; complete
measurements and WAV hashes are in `results/phase2/modern-analysis.json`.

The source-bandlimited candidate adds modern polyBLEP square antialiasing to
source-reverb. Source-filtered adds the fixed DC blocker pole **511/512**, about
15 Hz at 48 kHz. These are explicit modern alternatives, not bit-perfect GBA output.
Source-envelope is a separate diagnostic branch, not part of that filtering chain.
This analysis changes no C#, parameters, audio files or references.

This revision reports the final regenerated candidates after the PCM-pitch correction:
source-reverb, source-envelope, source-bandlimited and source-filtered now use the
original frequency-table interpolation and integer-Hz PCM rates. Source-linear
remains the power-formula control. Pallet measurements below reflect that small
change; SELECT is pure PSG and its measurements are unchanged. The frozen phase-1
baseline is still untouched.

## Identical measurement conditions

- Retain the original global reference crops: SELECT **43 samples**, Pallet **852**.
- SELECT spectral window **112–145 ms**; all five pitch windows are identical to
  baseline. Pallet spectra and DC cover the common **95 seconds**, with pitch
  regions measured at **100–300 ms**.
- Stereo spectral power is the mean of both channel powers, not a mono downmix.
- Release uses the same centered 2 ms AC-RMS thresholds, alongside actual waveform
  extrema and last-nonzero-sample measurements. No per-note phase alignment.
- There is no gain fitting, delay re-estimation, parameter search, new reference
  render or listening test. The frozen baseline JSON hash is verified unchanged.

## High-frequency energy and DC

| Profile | SELECT >16 kHz power | SELECT centroid | Pallet >16 kHz power | Pallet centroid |
|---|---:|---:|---:|---:|
| Original-driver reference | 0.2836% | 3831 Hz | 0.04610% | 748 Hz |
| Frozen phase-1 baseline | 3.5670% | 4877 Hz | 0.27737% | 809 Hz |
| source-reverb | 3.5670% | 4877 Hz | 0.21472% | 787 Hz |
| source-envelope, separate branch | 3.5670% | 4877 Hz | 0.23168% | 786 Hz |
| source-bandlimited | 0.5200% | 3938 Hz | 0.04424% | 727 Hz |
| source-filtered | 0.5203% | 3938 Hz | 0.04443% | 729 Hz |

| Profile | Pallet DC left | Pallet DC right |
|---|---:|---:|
| Original-driver reference | −0.000000560 | −0.000000614 |
| Frozen phase-1 baseline | −0.008313490 | −0.007384380 |
| source-reverb | −0.001490951 | −0.001305547 |
| source-envelope, separate branch | −0.000951716 | −0.000766306 |
| source-bandlimited | −0.001492468 | −0.001307064 |
| source-filtered | +0.000000371 | +0.000000304 |

PolyBLEP substantially reduces high-band power: Pallet's >16 kHz fraction is now
close to the reference, while SELECT retains about **1.83×** the reference fraction.
The DC blocker removes the measured Pallet bias. Neither aggregate spectral
similarity nor near-zero DC establishes waveform fidelity or preferred sound.

Pallet RMS is 0.056110 reference, 0.056082 source-reverb, 0.055928 bandlimited and
0.055845 filtered. Their left/right balances are 1.357, 1.336, 1.344 and 1.341 dB,
respectively. The final SELECT stable note's AC RMS is 0.053002 reference,
0.058594 source-reverb, 0.054927 bandlimited and 0.054978 filtered. These are
measurements only; no gain was adjusted to obtain them.

## Tone frequencies

SELECT retains approximately **1872.46 Hz**, then **3120.76 Hz**, in both modern
candidates and the reference. The polyBLEP spectral-peak estimates differ from the
reference by less than 0.001 Hz in these particular windows, but the short windows
do not provide sub-Hz independent resolution. The baseline's ≤0.24 Hz fitted-bin
differences likewise did not establish a tuning error; antialiasing changes the
peak-estimator bias as well as the spectral shape.

Pallet bandlimited/filtered square1's third-harmonic region implies about
**1310.675 Hz**, versus **1310.711 Hz** reference; square2's fundamental region is
about **109.938 Hz**, versus **109.965 Hz** reference. These tiny differences are
within mixed-spectrum/window uncertainty. They do not prove isolated voice pitch
or amplitude: PCM masks square1's fundamental in this section.

## Release clocks and digital tails remain distinct

All times below are after the same frozen reference crop. The two step columns
use AC-envelope midpoint detection; the final-cycle and silence columns use raw
sample/extremum evidence. A midpoint near the end of a 2 ms analysis window can
occur after the last actual oscillating sample.

| Profile | First release step midpoint | Final release step midpoint | Last clear cycle feature | Exact PCM16 silence starts |
|---|---:|---:|---:|---:|
| Original-driver reference | 165.600 ms | 228.709 ms | 227.854 ms | 299.750 ms |
| Frozen phase-1 baseline | 156.307 ms | 219.240 ms | 218.708 ms | 218.750 ms |
| source-reverb | 156.306 ms | 219.241 ms | 218.708 ms | 218.750 ms |
| source-envelope, separate branch | 167.481 ms | 234.910 ms | 234.313 ms | 234.417 ms |
| source-bandlimited | 156.322 ms | 219.246 ms | 218.729 ms | 218.750 ms |
| source-filtered | 156.321 ms | 219.247 ms | 218.729 ms | 226.396 ms |

The two modern filters preserve the original candidate's approximately **9.2 ms
early release staircase**. Filtering does not correct that clock difference.
The separate source-envelope branch instead uses about 16.75 ms step intervals
here: it starts later and diverges further from the reference through the release.
It is not validated as a replacement envelope schedule by these measurements.

The reference's **240–280 ms** residual has RMS **0.001702**, mean **−0.001213**,
and peak absolute amplitude **0.004669**. Every measured candidate is exactly zero
over that same window. Source-filtered creates only a much shorter low-level
post-oscillation residual, ending at 226.396 ms. Thus removing long-run DC has not
reproduced the reference's trigger/filter tail. That reference residual must not be
misclassified as a 300 ms musical note.

Pallet ends during ongoing playback, so there is no natural end-of-song release
measurement. Six overlapping sources also prevent reliable per-voice release
estimation from these two-channel recordings alone.

## Decision supported

Source-filtered is a useful **modern spectral/DC candidate for audition**. Its
reduced high-band energy and DC are measured improvements in those narrow metrics.
SELECT release timing and the original-driver transient response remain unresolved.
No candidate has passed a listening-quality or hardware-equivalence gate.
