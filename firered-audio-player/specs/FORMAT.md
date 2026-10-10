# FireRed two-song native audio MVP: original-data specification

This is a fresh behavioral specification from the fixed `pret/pokefirered` source/data, not a translation of another synthesizer. No agbplay implementation was read or copied. Scope is MUS_PALLET and SE_SELECT, including concurrent playback. An approximate native renderer must identify approximations; these facts do not establish bit-exact hardware emulation.

## Provenance and reproducible inventory

- Reference repository: `../audio-reference-build/pokefirered`, revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
- Matching ROM SHA-1: `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc`.
- Primary sources: `include/gba/m4a_internal.h`, `src/m4a.c`, `src/m4a_1.s`, `src/m4a_tables.c`, `sound/MPlayDef.s`, `asm/macros/music_voice.inc`, `sound/voice_groups.inc`, `sound/keysplit_tables.inc`, `sound/songs/midi/{mus_pallet,se_select}.s`, `sound/song_table.inc`, `sound/music_player_table.inc`, and the matching link map/ROM.
- `specs/inventory.py` reads only that original reference, emits header/voice/sample inventories and every note before the first loop. Run from the workspace parent: `python firered-audio-player/specs/inventory.py --out firered-audio-player/specs/inventory.json`.
- Inventory is a read-only structural analysis, not an audio renderer. It intentionally errors on unsupported instructions outside this two-song scope.

## Addresses and song layout

All stored words are little-endian. ROM pointers are absolute GBA addresses; full-ROM offset = address minus `0x08000000`. An audio-only segment container can preserve these virtual addresses and must bounds-check every read. Header size is `8 + 4 * trackCount` bytes:

| Offset | Meaning |
|---|---|
| 0 | u8 trackCount |
| 1 | u8 blockCount, zero for both |
| 2 | u8 song priority |
| 3 | u8 reverb, bit7 means set global reverb; low7 is value |
| 4 | u32 voice table pointer |
| 8 | u32 track pointers, one per track |

| Item | MUS_PALLET | SE_SELECT |
|---|---|---|
| Song number | 300 | 5 |
| Song header | `0x086dd844` | `0x086b5720` |
| Voice table | `0x08498e78` (voicegroup159) | `0x0848bd14` (voicegroup127) |
| Track count / priority | 6 / 0 | 1 / 5 |
| Reverb header | `0xb2` = set 50 | `0xb2` = set 50 |
| Original player | BGM / index0 | SE2 / index2 |
| Tempo byte | 44, meaning tempo88 | 150, meaning tempo300 |

Pallet tracks 1..6 start at `0x086dd010`, `0x086dd15f`, `0x086dd33a`, `0x086dd472`, `0x086dd5e1`, `0x086dd693`. Each starts with KEYSH 0 and loops to start+2. Each loop is 1536 ticks, about 43.8354492 seconds at the GBA refresh clock. First-loop note counts are 115, 78, 115, 68, 66, 78 (520 total).

SELECT track starts `0x086b5700`, ends after 18 ticks. Notes are `(tick,duration,key,velocity,voice)` = `(0,3,94,68,87)`, `(3,3,103,127,87)`, `(6,3,103,68,87)`, `(9,3,103,127,87)`, `(12,6,103,68,88)`. Track volume80, bend+13 with default bend range2. Score duration is roughly 0.1506844 seconds plus release tail and frame-start details.

The song table is `0x084a32cc`, entries `<header:u32,ms:u16,me:u16>` (8 bytes). The music-player table is `0x084a329c`.

## Sequencer instruction decoding

Keep state per track: cursor, wait, runningStatus, last key, last velocity, voice, gate duration, volume, pan, bend, bendRange, keyShift, LFO speed/phase/depth and priority. On first start most fields are zero, except bendRange=2, volume multiplier `volX=64`, LFO speed22. A voice is explicitly selected before all audible notes here.

When the next byte is >=0x80, consume it as opcode. Only explicit opcodes >=0xBD replace runningStatus. A byte <0x80 executes the previous runningStatus without consuming the byte first, so it becomes that command's argument. Waits, GOTO, TEMPO and KEYSH preserve runningStatus. This matters for standalone MOD 0 and repeated pitch-only notes.

The original duration table is:

`[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,28,30,32,36,40,42,44,48,52,54,56,60,64,66,68,72,76,78,80,84,88,90,92,96]`.

- `0x80..0xb0`: set wait to table[opcode-0x80]. W00 advances no time.
- `0xb1 FINE`: disable track and mark its current voices STOP/release, detach them from track (owner address becomes0 while priority is retained). Do not hard-cut release tails. Pallet has unreachable FINE instructions after its GOTOs.
- `0xb2 GOTO`: read little-endian absolute pointer and jump; no state reset.
- `0xbb TEMPO arg`: player's tempoD=arg*2; tempoI=tempoD*tempoU>>8. Affects this player, not the other simultaneous player.
- `0xbc KEYSH arg`: signed8 semitone shift.
- `0xbd VOICE arg`: copy/select voice table[arg], 12 bytes. This sets the instrument for subsequent notes, not the wave/envelope of already running voices.
- `0xbe VOL arg`: unsigned track volume.
- `0xbf PAN arg`, `0xc0 BEND arg`: store arg-64.
- `0xc1 BENDR arg`: semitone bend range.
- `0xc2 LFOS arg`: LFO speed. A zero value resets phase and modulation output.
- `0xc4 MOD arg`: LFO depth. A zero value resets phase and modulation output.
- `0xcf..0xff`: note with duration=table[opcode-0xcf]. `0xcf` is tie (duration0), unused here. For following bytes <0x80, consume up to three positional arguments: key, velocity, duration extension. Omitted key/velocity reuse the prior values. Gate duration is newly set by the opcode every time, including under running status, then optional extension is added.

All required explicit commands for these two songs are listed above. Present note lengths: 3,6,9,12,18,24,36,42,48,60,72. Wait lengths: 1,3,6,9,12,15,18,21,24,36,44,48,60. No PATT/PEND, REPT, MEMACC, PRIO, LFODL, MODT, TUNE, XCMD, EOT or TIE is needed for these streams, though a general MP2K engine needs them.

At each sequencer tick, decrement nonzero gate times of existing track voices first; reaching zero sets STOP. Then consume commands until a positive wait is encountered. Decrement the wait once at the end of that same tick. Thus N03/W03 next notes occur exactly 3 ticks apart. Then update that track's LFO. Process tracks in header order. Live VOL/PAN/BEND/MOD changes affect existing attached voices as well as new notes.

### Tick clock

SoundMain/MPlayMain run once per display frame. Hardware frame rate is `16777216 / 280896`, approximately 59.72750057 Hz. Each player's tempoC is initialized0, tempoI150, tempoU256. Every frame, tempoC+=tempoI. While tempoC>=150, execute one tick and subtract150. Reading TEMPO on the first tick changes subsequent frame additions. SELECT may execute two ticks per frame, Pallet about0.5867. Do not use raw44 as tempoI, and do not assume exactly60 frames/s.

### Modulation and pitch

With no LFODL, each active tick after waits: `phase=(phase+speed)&255`; triangle=`signed8(phase)` when phase<64 or phase>=192, else `128-phase`; `modM=(triangle*depth)>>6` with arithmetic signed shift. Pallet sets LFOS44, and repeatedly enables/disables vibrato via MOD. Default modT0 is pitch modulation.

Pitch in 1/256 semitone units is `(tune + bend*bendRange)*4 + (keyShift<<8) + (keyShiftX<<8) + pitX`, plus `16*modM` for modT0. Track keyM is arithmetic pitch>>8, pitM is low unsigned8. Add keyM to voice's base note key, clamp below0, and interpolate pitch with pitM. Normal PCM/PSG instruments use the incoming sequence note key; do NOT subtract ToneData.key=60 again. This key field matters for rhythm voices, not these plain/key-split instruments.

## Instrument data and assets

12-byte ToneData: `<type:u8,key:u8,length:u8,pan_sweep:u8,wavePointerOrDuty:u32,attack:u8,decay:u8,sustain:u8,release:u8>`.

- type&7=0 is DirectSound PCM; type&7=1 square channel1; =2 square channel2. These songs need no programmable wave or noise.
- type0x40 is key split: +4 points to a child table of 12-byte tones; +8 points to a byte key map. Read map[original sequence key], select that child tone, preserve the sequence key. Only one split level is accepted by original code. Key tables may be biased backwards; don't invent a 0..127 valid-data allocation just because indexing uses MIDI keys.
- type0x80 is rhythm map (unused here): child index equals note key, and child key replaces base pitch.
- PCM type0x08 means no-resample (unused here). PCM type0x10 reverse and0x20 compressed are unused here. PSG types0x09/0x0a use bit3 for hardware-frequency rounding, not PCM fixed-pitch behavior.
- Simple tone pan_sweep is ignored for PCM pitch instruments here. For square1 it is the hardware sweep byte.

Pallet uses the following instruments:

| Voice | Type | Sample / duty | A,D,S,R |
|---|---:|---|---|
| 4 | 0 | detuned EP low, `0x086afb30` | 64,249,0,188 |
| 5 | 0 | detuned EP high, `0x086b2198` | 51,249,0,165 |
| 24 | 0 | nylon guitar, `0x086a18e0` | 85,249,25,127 |
| 48 | 0x40 | child table `0x0848a538`; key map `0x084a309c` | pointers occupy envelope bytes |
| 80 | 9 | square1 duty2, sweep0 | 0,7,0,6 |
| 81 | 10 | square2 duty1 | 1,5,1,6 |
| 83 | 9 | square1 duty0, sweep0 | 1,4,3,6 |

Pallet voice48 uses child0 for keys36..68, child1 for69..80, child2 for81..107. Only child0 and child1 are reached: child0 tone=`0x0848a538`, wave=`0x084d02cc` (string ensemble60); child1 tone=`0x0848a544`, wave=`0x084d3870` (string ensemble72). Both are type0, ADSR255,0,255,196. The song's actual voice48 keys are 66,68,69,70,71,72,73,74,76.

SELECT voice87 is type9, key60,length0,sweep0x77,duty2, ADSR0,0,15,1. Voice88 is identical except sweep0. Their tone addresses are `0x0848c128` and `0x0848c134`.

### PCM headers, frequency and envelope

WaveData is `<type:u16,status:u16,freq:u32,loopStart:u32,size:u32>`, followed by `size` signed8 samples. Looping flag is status&0x4000 (original byte3 bit0x40). Loop interval is [loopStart,size), not [0,size). All five reached PCM samples are type0, status0x4000.

| Wave address | freq word | Playback Hz at key60 | loopStart | size |
|---|---:|---:|---:|---:|
| `0x086afb30` | 13700096 | 13379 | 9406 | 9814 |
| `0x086b2198` | 13700096 | 13379 | 4401 | 5015 |
| `0x086a18e0` | 6850048 | 6689.5 | 5779 | 8029 |
| `0x084d02cc` | 13700096 | 13379 | 4301 | 13715 |
| `0x084d3870` | 6850048 | 6689.5 | 4433 | 8970 |

For a musical approximation: sample playback rate is `freq/1024 * 2^((key-60+fine/256)/12)`. For exact integer engine pitch, original `gScaleTable` at0x08489a08 and `gFreqTable` at0x08489abc give `f(k)=freqTable[k%12] >> (14-k/12)`, clamped key<=178. Interpolate `v=f(k)+((f(k+1)-f(k))*fine>>8)`; return `(wave.freq*v)>>32` using unsigned64 intermediates. The 12 freq constants are in `src/m4a_tables.c`; they are original data. This return value is playback samples/second rounded down.

PCM envelope is updated once per SoundMain frame, not sequencer tick. New note starts envelope0 and immediately adds A on its first mix frame. Attack adds A to a ceiling255, then decay mode begins. Decay applies `(env*D)>>8`, clamps to S and enters sustain once <=S. Gate expiration / FINE enters release, applying `(env*R)>>8` until0. A START and STOP in the same frame kills the not-yet-started sound. Pseudo-echo envelope fields default0 for both songs.

Original mixer is configured with 5 DirectSound channels, masterVolume12, 224 PCM samples per display frame, integer PCM frequency13379, divFreq627. Resampling uses linear interpolation with a 23-bit fractional position and step=`MidiKeyToFreqResult*divFreq`. A 48kHz host renderer using floating steps is a valid MVP approximation but not exact original resampling/quantization.

### Volume and pan

Default external volume multiplier is64. `x=(trackVol*volX)>>5`. For default modulation type0 there is no volume modulation. `y=clamp(2*pan+panX,-128,127)`. Track right=`((y+128)*x)>>8`, left=`((127-y)*x)>>8`. With rhythmPan0, channel right=`(128*velocity*trackRight)>>14`, left=`(127*velocity*trackLeft)>>14`, each capped255. The slight 127/128 asymmetry is intentional original integer behavior.

PCM mix envelope multiplier first uses `((masterVolume+1)*env)>>4`; final right/left gain bytes multiply this by channel right/left and shift8. Original samples are mixed into signed8 DMA buffers, not an unclipped infinite-precision bus.

## PSG square behavior

Square channels are shared globally, one voice each, outside the five PCM slots. Duty encodings0/1/2/3 correspond to12.5%,25%,50%,75%. An anti-aliased host oscillator will differ from hardware output but should retain pitch, duty, envelope, sweep and channel exclusivity.

Original pitch conversion for square1/2: for key<=35 use index0,fine0, else index=key-36 (max130,fine255). Base values are `[-2004,-1891,-1785,-1685,-1591,-1501,-1417,-1337,-1262,-1192,-1125,-1062]`, arithmetic-shift by octave `(index/12)`, interpolate adjacent values with fine/256, then add2048. For these type9/10 instruments and DA_BIT_8 initialization, original hardware register value is rounded `(value+1)&0x7fe`. Square frequency is `131072/(2048-registerValue)` Hz.

Driver sweep writes the supplied value if its bits0x70 are nonzero and bit7 clear; otherwise0x08. SELECT voice87 therefore writes0x77; voice88 writes0x08. Hardware interpretation of0x77 is period7/128sec, increase frequency-register value by shadow>>7 each sweep (pitch goes up), disable on overflow past2047. Use the frequency-register shadow, not multiplicative Hz sweep. This last hardware timing interpretation is not implemented by m4a.c itself; it is the behavior of the GBA register the original driver writes. Hardware sweep/trigger phase details remain a fidelity risk without an APU model.

PSG volume is quantized. Compute channelRight/Left above. If right/2>=left, route only right; if left/2>=right, only left; otherwise route both. envelopeGoal=`(left+right)/16` (integer; cap15 in one-side case), sustainGoal=`(envelopeGoal*S+15)>>4`.

PSG attack/decay/release are step intervals0..7, and sustain0..15. Attack adds one envelope level each A units until envelopeGoal; A0 skips attack immediately. Decay subtracts one each D units until sustainGoal; D0 skips directly to sustain. S0 turns off at that point. Release subtracts one each R units to0; R0 turns off immediately. CgbModVol refreshes goal and sustainGoal at each envelope step without proportionally scaling current envelope volume. Sustain assigns the refreshed sustainGoal and reloads a7-unit counter. Hardware step units are1/64sec. The driver's shadow envelope runs once per frame and performs an extra decrement every15 frames to approximate64Hz; thus a continuous64Hz host envelope is only approximate. Volume-register rewrites may retrigger hardware oscillators; mark any simplified phase model clearly.

## Original channel allocation and interruption

Priority is saturating `songPriority + trackPriority`. The latter starts0 and PRIO is unused here. Track-address order is an intentional tie-break: BGM track i is `0x030020c8+i*0x50`; SELECT's SE2 track is `0x030024d8`.

For a new PCM voice, scan slots in ascending order:

1. Immediately select the first inactive slot, even if a replacement candidate was found earlier.
2. If no free slot, STOP/releasing voices take precedence over all non-STOP candidates, regardless of new priority. Among STOP candidates select lowest priority, then greatest owner track address, then greatest slot index.
3. If no STOP voice, active candidates must have lower priority than the new note, or equal priority and owner track address >= new track address. Choose lowest priority, then greatest owner track address, then greatest slot index.
4. If no candidate, discard the new note. A discarded note still updates parser key/velocity state.

For PSG, use its fixed global hardware slot. Replace if inactive or STOP, or if old priority<new priority, or if equal and old owner track address>=new track address. Otherwise discard the new note. A stolen note is gone; it does not resume when the new sound ends. Thus SELECT can interrupt Pallet square1, while Pallet's PCM and square2 continue. Pallet square1 notes cannot steal an active priority5 SELECT voice, but can steal its release tail.

## Known fidelity boundaries and acceptance checks

Both headers request reverb50. Original reverb is a specific signed8 cross-channel PCM-ring feedback operation, not a generic room reverb; it does not apply to the PSG channels. It is acceptable to leave it explicitly unsupported in this MVP. Other non-bit-exact areas include GBA DAC/PWM/filtering, integer clipping, exact timing of hardware triggers, PSG envelope/sweep phase and resampler precision. Do not claim equivalence from only a nonempty WAV or matching note counts.

Useful independent checks: correct header/voice inventory; Pallet first-loop520 notes and1536 ticks on every track; SELECT5 notes/18ticks with the sequence above; looping without drift across tracks; equal-priority allocation chooses the documented victim; SELECT steals only square1; volume/bend/modulation update already-running notes; PCM sample-loop boundary remains valid; stopping a player is distinguished from FINE (explicit TrackStop hard-kills), and starting a player resets its sequencing state.
