# Source contract: selected CGB wave and noise voices

Original driver/data: `pret/pokefirered` revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`. This extends the 12-song coverage gate after echo/rhythm checkpoint `b093ff8d9909a67035651ca065dd62fab8b9c997`. No emulator/player implementation source is used.

## Verified hardware-format documentation

Wave RAM contains 32 unsigned 4-bit samples packed high nibble first. Nibble clock is 2097152/(2048−register) Hz. A full 32-sample cycle is 65536/(2048−register) Hz; repeated subpatterns can have higher fundamentals. GBA supports mute, quarter, half, three-quarter and full volume. The fixed driver selects one 32-sample bank. [GBATEK, GBA Sound Channel 3 / NR30–34 and Wave RAM](https://rust-console.github.io/gbatek-gbaonly/#gba-sound-channel-3---wave-output).

For noise register NR43, r=bits 0–2, width=bit 3 and shift=bits 4–7. LFSR clock is 262144/(r×2^shift), treating r=0 as 0.5. Shifts 14/15 do not clock. [Pan Docs, NR43](https://gbdev.io/pandocs/Audio_Registers.html#ff22--nr43-channel-4-frequency--randomness) ([maintained documentation source](https://raw.githubusercontent.com/gbdev/pandocs/master/src/Audio_Registers.md)).

Use the documented GBA Galois recurrence: take the old low bit, shift right, XOR 0x6000 (15-bit) or 0x60 (7-bit) when that bit was 1, and output that bit. Reset seeds are 0x4000/0x40; periods are 32767/127. [GBATEK, Noise Random Generator](https://rust-console.github.io/gbatek-gbaonly/#noise-random-generator-aka-polynomial-counter).

Pan Docs instead describes a zero-seeded XNOR/Fibonacci representation. [Pan Docs, CH4](https://gbdev.io/pandocs/Audio_details.html#noise-channel-ch4) ([maintained documentation source](https://raw.githubusercontent.com/gbdev/pandocs/master/src/Audio_details.md)). An independent full-cycle check found complemented, cyclically shifted streams with the same periods. This implementation deliberately chooses the GBA-specific recurrence and deterministic host reset phase. It does not claim original first-bit phase/polarity matching or obscure retrigger quirks.

## Exact fixed-driver parameters

`asm/macros/music_voice.inc:91–137` defines wave types 0x03/0x0b and noise 0x04/0x0c. Wave pointer addresses 16 bytes; noise data is only the width selector 0/1. All selected potential wave/noise tones have length 0 and selected noise uses width 0. Nonzero hardware length counters remain explicitly unsupported in this bounded stage.

`src/m4a.c:810–853` supplies pitch mapping. Wave uses the same CGB semitone/register interpolation as square channels; noise clamps sounding key≤20 to index 0, otherwise uses `min(key−21,59)`, and ignores fractional fine adjustment. Track key shifts and integer pitch changes still affect that sounding key.

`gNoiseTable=0x08489ba0` (`src/m4a_tables.c:149–166`) contains 60 bytes:

`d7 d6 d5 d4 c7 c6 c5 c4 b7 b6 b5 b4 a7 a6 a5 a4 97 96 95 94 87 86 85 84 77 76 75 74 67 66 65 64 57 56 55 54 47 46 45 44 37 36 35 34 27 26 25 24 17 16 15 14 07 06 05 04 03 02 01 00`

The driver writes tone width into NR43 bit 3 at `:1021–1022`, then preserves it while replacing the other pitch bits at `:1197–1200`. FIX 0x08 quantization is applied only to channels 1–3 (`:1187–1194`), not noise. FireRed initializes 8-bit/65536 Hz PWM (`:78–81,471–476`), so alt wave register uses `(register+1)&0x7fe`, the same existing square-alt rule.

`gCgb3Vol=0x08489bdc` (`src/m4a_tables.c:168–175`) is:

`00 00 60 60 60 60 40 40 40 40 80 80 80 80 20 20`

Envelope values 0–1 therefore mute, 2–5 select quarter, 6–9 half, 10–13 three-quarter, 14–15 full. Driver wave-RAM writes load four little-endian words from the tone pointer (`src/m4a.c:1003–1018`); per-frame envelope changes select this volume class (`:1205–1217`). Existing driver-derived goal/sustain, pseudo-echo and source routing policy continue to apply. Noise uses the existing CGB square-style volume envelope without the square duty or sweep oscillator.

## Physical original waveform bounds

`sound/programmable_wave_data.inc` declares 11 separate `.incbin` assets, each exactly 16 bytes, verified against the matching ROM. They start at `0x084a31ec` and advance 16 bytes through `0x084a328c`. Read only one such proven extent; reject unsupported pointers or a truncated 16-byte range before allocating/stealing a voice.

| Index | Original 16-byte payload |
|---:|---|
|1|`01258adefec9631001258adefec96310`|
|2|`02468acefdb9753102468acefdb97531`|
|3|`fedcba9876543210fedcba9876543210`|
|4|`fdb9888877776531fdb9888877776531`|
|5|`048cca86ffed4321048cca86ffed4321`|
|6|`fffffe864303468efffffe864303468e`|
|7|`aaaaa82100000128aaaaa82100000128`|
|8|`ffffffffffee886644330033446688ee`|
|9|`6789abc8989656546789abc898965654`|
|10|`021426384a5c6e7f7e6c5a4836241200`|
|11|`0426384a5c5a48360426384a5c5a4836`|

The selected potential inventory uses waves 1, 2, 3, 5, 6, 7. These data already contain their real timbres; substituting a sine or generic triangle is not permitted.

## Deliberate host rendering policy

- Extend the pool to five PCM slots plus four isolated CGB slots. Wave must only replace wave; noise must only replace noise, subject to existing priority/owner rules.
- Keep full original 32-sample shape and deterministic LFSR state. Advance fractional phase persistently across host buffers. Modern SourceFiltered uses the existing continuous 64-Hz envelope approximation and source volume-class table; this is not a register/APU emulator.
- Use a host-rate averaged/held oscillator rather than model PWM, DAC bias, wave-RAM access races, startup stale-sample behavior or every retrigger side effect. Wave gain uses the five source-selected levels on a centered normalized original shape; low-bit hardware DAC truncation and DC transients are outside this modern profile.
- Preserve every old square/PCM arithmetic path. Unknown type flags and unsupported length modes remain errors, never silent substitutions.

## Required gates

Verify all 11 physical payloads/nibble order; exact noise table and wave-volume classes; known 7/15-bit LFSR prefixes and full-cycle periods; NR43 clocks at r=0/r=1/shift 13/14/15; note reset; malformed/out-of-range/misaligned wave pointers; invalid width/type/length; isolated wave/noise allocation, lower-priority rejection and hard stop; finite/block-independent output. Retarget the four former wave/noise-rejection tests to still-unsupported reverse flags, and add positive tests for the newly supported families.

The final gate passes 178 tests: the 79-test baseline with its invalid PEND assertion corrected, 37 echo/rhythm checks with four newly supported type assertions retargeted to unsupported flags, 55 wave/noise checks, and seven additional EOT/pattern-boundary checks. All 386 catalog hashes, eight old WAV hashes, and five previously passing selected-music hashes remain exact. See `results/phase6-music-final/REPORT.md` and its machine-readable evidence. Passing synthesis is still not a bit-perfect or listening-quality claim.

## Sequence corrections exposed by broader coverage

The original `ply_pend` (`src/m4a_1.s:870–880`) returns without changing the cursor when pattern depth is zero. Many selected songs enter an inline pattern body before later calling it. The previous underflow exception was a baseline bug; this is now a verified no-op. Actual pattern-stack overflow and out-of-resource calls still fail explicitly in this bounded player.

Original `ply_endtie` (`src/m4a_1.s:1818–1856`) updates the remembered track key when an explicit operand appears, then releases only the first eligible matching MIDI key in the newest-first channel list. New channels prepend at `:1727–1739`. Native selection therefore uses greatest voice age, ignores detached/releasing voices, and stops after one match. PCM sustain-zero pseudo-echo can retain ENV bits (`:225–235`), so it remains eligible until STOP. CGB pseudo-echo clears ENV (`src/m4a.c:1062,1125`) and is IEC-only, so it does not match. Focused vectors cover both distinctions, sequential same-key EOT, and omitted-key TIE after explicit EOT.

These corrections leave the original eight regression WAVs and all previously passing selected-song hashes unchanged.
