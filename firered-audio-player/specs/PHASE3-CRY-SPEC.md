# Phase 3: original FireRed ordinary Charmander cry

## Scope and provenance

This is a narrowly bounded specification for ordinary Charmander at the requested parameters: volume 120, pan 0, pitch 15360, length 140, release 0, progress 0, chorus 0, priority 10, stereo 1. It does not establish support for a general cry library, reverse cries, chorus, nonzero progress, or arbitrary XCMD commands. No agbplay source or another player implementation was read or copied for this work.

Original reference: `../audio-reference-build/pokefirered`, revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`, matching ROM SHA-1 `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc`.

Primary evidence:

- `sound/cry_tables.inc:2-6`, `asm/macros/music_voice.inc:16-28,152-158`: Charmander's fourth cry-table entry and compressed-voice flags.
- `sound/direct_sound_data.inc:319-320`: original Charmander sample inclusion.
- `src/m4a_1.s:469-709`: special PCM playback, decoded-position handling, forward/reverse distinction, DPCM block decoder.
- `src/m4a_tables.c:46-65,259-309`: delta table, cry-song template, XCMD dispatch table.
- `include/gba/m4a_internal.h:233-263`: 52-byte `PokemonCrySong` structure and field offsets.
- `src/m4a.c:611-667,1523-1780`: player initialization, XCMD semantics, cry parameter setters and slot selection.
- `src/sound.c:374-470`: normal-cry default pitch, length, release, progress and chorus.
- `src/m4a_1.s:187-270,1140-1410,1509-1541,1742-1809,1820-1855`: envelope, tick processing, volumes, note allocation, EOT.
- `constants/m4a_constants.inc:8-32`: tone and wave flags.
- `audio_rules.mk:18-21`, `tools/wav2agb/converter.cpp:214-302,403-427`: original build's compressed, unpadded physical asset layout.
- Matching link map, ROM, and original `sound/direct_sound_samples/cries/charmander.{bin,wav}`.

The source is available at the [fixed upstream revision](https://github.com/pret/pokefirered/tree/037335f4c725d7c9aecdac87066f2002b4bd7e14). The implementation-independent audit is `tools/inventory_cry.py`; run `python tools/inventory_cry.py` from this project. Its checked output is `specs/phase3-cry-inventory.json`.

## Tone, sample and exact asset bounds

`gCryTable = 0x0848c914`. Charmander is species 4, hence index 3 and tone address `0x0848c938`.

The 12 tone bytes are `20 3c 00 00 38 30 51 08 ff 00 ff 00`:

- Type `0x20`: DirectSound PCM (`type & 7 == 0`), compressed special path (`CMP=0x20`), forward (`REV=0x10` is clear), resampled/pitched (`FIX=0x08` is clear).
- Key 60, length 0, pan/sweep 0.
- Wave pointer `0x08513038` (`Cry_Charmander`).
- Attack 255, decay 0, sustain 255, release 0.
- This is neither a key-split nor rhythm tone. Tone key 60 is not a second pitch transposition to subtract from the sequence key.

WaveData's 16-byte little-endian header is `01 00 00 00 00 40 a4 00 00 00 00 00 dd 1b 00 00`:

- `u16 type = 1`: DPCM data. The special mixer checks nonzero wave type, distinct from the tone's special-path flags.
- `u16 status = 0`: no loop. The original byte-3 loop mask is `0xc0`, equivalently `status & 0xc000`; there are no loop flags here.
- `u32 freq = 10764288 = 10512 * 1024`.
- `u32 loopStart = 0`.
- `u32 size = 7133` playable decoded samples. This is not an encoded-byte count.

Three lengths must be kept separate:

1. Playable decoded length: 7133 signed 8-bit samples.
2. Required decoded buffer including interpolation lookahead: 7134 samples; last guard sample is -2.
3. Physical asset: 16-byte header + 3679 encoded bytes = 3695 bytes. The next symbol is aligned at `0x08513ea8`, giving a 3696-byte ROM span including one alignment byte.

The original source WAV has 7134 mono 8-bit samples at 10512 Hz, plus an `agbl` chunk declaring playable length 7133. The original `-b -c -l 1 --no-pad` rule encodes the 7134 physical source samples without a full final-block pad. Thus the payload is 111 full 33-byte blocks plus one 16-byte partial block. That last block contains 30 decoded values: 29 playable values and the guard.

Do not use `16 + size` as the stored length, `ceil(size/64)*33` as a required physical payload, or pad the decoded length to 7168 and play it. For this deliberately fixed cry, validate the exact address, header and 3679-byte payload and decode exactly 7134 values.

The original ARM block decoder eagerly expands 64 values even for the final partial block. It can read beyond this individual asset into alignment/next-asset bytes, but only the first 30 values of that block are needed here. A native implementation should decode only required values or safely pad its private temporary block, never require/read bytes outside the bounded asset. The real guard at index 7133 must survive.

## DPCM equations, including the unusual first nibble

Delta lookup table `D`:

`[0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1]`

For decoded index `i`, let `block = floor(i/64)`, `j = i % 64`, and `p = 33*block` within the encoded payload.

- `j == 0`: `u = payload[p]`. This absolute seed is the first sample of a new block. Predictor state does not cross block boundaries.
- `j >= 1`: read `b = payload[p + 1 + floor(j/2)]`.
- If `j` is odd, `nibble = b & 15`; if even, `nibble = b >> 4`.
- `u = (u + D[nibble]) & 255`; output `u < 128 ? u : u - 256`.

Therefore byte 1 contributes only its low nibble to sample 1. Its high nibble is ignored. Bytes 2 through 32 supply high nibble then low nibble, yielding samples 2 through 63. The whole block is one absolute seed plus 63 deltas in 32 packed bytes. This is not generic IMA/ADPCM, and not a continuously predicted nibble stream. Addition wraps at eight bits rather than clamps, following `strb` then `ldrsb` in the ARM source.

First decoded values are `0, 36, -28, -53, -37, -73, -109, -93, -84, -68, -59, -43, -42, -17, 32, 48`. Full first-64 and final-16 golden vectors are in the inventory JSON.

The audit independently converted the source WAV's unsigned 8-bit bytes to signed bytes via `(b-128)&255`, then compared all 7134 decoded values: **zero differences**. The SHA-256 of the signed-byte buffer including guard is `e1890eaf6544102dbbbcfd9c217f5502e84a85ab7496aaeb045921e1a1de958f`. Physical asset SHA-256 is `eacf24a915fb03248a6d847d90092cc5adb30505bd77c8cde7a65ff55a2a8006`.

## Original dynamic cry song

The ROM template is at `0x08489c20`, size 52 bytes including two trailing alignment bytes. `SetPokemonCryTone` copies the template state into a selected cry slot, changes the tone pointer, sets track 0 and 1 addresses, and relocates its GOTO. A native implementation may use an independent bounded byte-array address domain; it must not write into immutable source data.

Original first slot: `gPokemonCrySongs = 0x03006f00`. Original first cry track owner: `gPokemonCryTracks = 0x03007180`. This owner is the original first slot's track 0 and is appropriate for original-address channel-allocation comparison. A later allocated slot has another owner; the engine should not claim every possible cry instance owns this same address.

Important offsets within the copied template:

| Offset | Meaning / this invocation |
|---|---|
| 0x00..03 | trackCount=1, blockCount=0, priority=10, reverb=0 |
| 0x04 | tone pointer `0x0848c938` |
| 0x08 | track 0 pointer = slotBase+0x11 |
| 0x0c | track 1 pointer = slotBase+0x18, inactive for chorus 0 |
| 0x11..13 | `TUNE 64`, `GOTO` |
| 0x14 | GOTO pointer = slotBase+0x1a |
| 0x18..19 | track 1 `TUNE 80`; unused |
| 0x1a..1d | `VOICE 0`, `VOL 120` |
| 0x1e..23 | `XCMD 0x0d`, little-endian u32 progress=0 |
| 0x24..26 | `XCMD 0x07`, release=0 |
| 0x27..28 | `PAN 64` = centered pan 0 |
| 0x29..2b | `TIE 60,127` |
| 0x2c..2f | `XCMD 0x0c`, little-endian u16 length=140 (`8c 00`) |
| 0x30..31 | `EOT`, `FINE` |

Parameter conversion matters:

- Volume: `value & 0x7f`.
- Pan: `(value + 64) & 0x7f`.
- Pitch setter: `b = signed16(value + 0x80)`; TIE key=`(b>>8)&0x7f`; TUNE byte=`(b>>1)&0x7f`. At value 15360 these are key 60 and TUNE 64 (zero tuning). Preserve the existing track-1 tune difference when applying the generic setter, although that track stays inactive here.
- Chorus 0 sets trackCount to one. Stereo 1 does not enable the chorus track: it configures global FIFO routing, clears `soundInfo.mode & 1`, routes A to right and B to left, and uses full direct-sound mix.
- Reverb header 0 has no `REVERB_SET` bit. It preserves the global reverb value; it does not mean explicitly disable reverb. A fresh game's initialization and an already-playing song can therefore produce different reverberation environments.

## XCMD subset and timing

Only the following XCMD subcommands are needed. Reject other subcommands rather than silently skipping a guessed payload length.

- `0x0d`: consume four little-endian bytes into `track.unk_3C`. A subsequent PCM note copies it into the channel's initial `count`. Start initialization makes it the decoded starting sample offset, advances the current position by that amount, and sets remaining count to `wave.size - progress`. For this scope require zero. It is not an elapsed time or compressed-byte offset.
- `0x07` (xRELE): consume one byte into `track.tone.release`. This changes the tone used for subsequent notes, so execute it before the TIE; it does not retroactively edit active channels. This cry's original tone release is already zero, but the command still must be consumed.
- `0x0c` (xWAIT): inspect little-endian u16 length at the current parameter pointer. If `track.timer < length`, increment timer, move the command pointer back by two bytes to the XCMD opcode, and set `wait=1`. Otherwise set timer to zero and advance the parameter pointer by two. Do not turn this into a one-byte standard wait; 140 exceeds the ordinary wait table's range.

All sequencing is normal per-player MP2K processing:

- `MPlayStart` resets tempoD=tempoI=150, tempoU=256, tempoC=0. There is no TEMPO in the cry template, so one tick runs per GBA frame.
- Initial track clearing resets timer/progress and ordinary state; volX=64, bendRange=2, default LFO speed22 but modulation depth0.
- At tick 0, TUNE, GOTO, VOICE, VOL, progress, release and PAN all execute; TIE creates a note with gate time 0. xWAIT sees timer0, increments it to1, and establishes the wait.
- The normal tick loop decrements that newly set wait once in this same tick. It becomes0 for the next tick, when the same XCMD is executed again.
- At ticks 0..139 xWAIT raises timer from0 to140. At tick140 it resets timer and falls through to EOT/FINE in that same tick.
- EOT sees next byte FINE (`>=0x80`), so it uses the existing MIDI key60 and sets STOP on the matching live tied note, if any. FINE releases/detaches remaining attached channels and disables the track.

At the hardware refresh rate `16777216 / 280896`, length140 is approximately 2.3439788818359375 seconds from note-on tick to EOT, not 140 ms. The PCM is not looped: at nominal C60 pitch it exhausts naturally after `7133/10512 = 0.6785578386605784` seconds, much earlier than EOT. A voice may stop while its track still executes xWAIT. Therefore playing-state and BGM-restoration decisions should observe the PCM channel ownership/state, not wait for the dynamic song to reach FINE.

The source `IsPokemonCryPlaying` checks `track->chan && track->chan->track == track`. MPlayMain unlinks channels whose active status has ended. Do not conflate this predicate with remaining score length.

## Envelope, pitch, volume and mixer integration

PCM-envelope updates happen once per sound frame, not once per audio output sample:

- Start: envelope=0, attack adds255 immediately; clamp to255 and enter decay. The first rendered frame is at full envelope.
- Next frame: decay computes `255*0 >> 8 = 0`, but because that is below sustain255 it is clamped up to255 and switches to sustain. There is no audible dip to zero.
- Sustain remains255. Natural sample exhaustion ends this voice regardless of tie duration.
- If EOT occurs first in a shortened supported test, release0 makes `env*release >> 8` zero on that sound frame. Pseudo-echo defaults are zero, so the channel is disabled immediately. Existing global reverb is a separate tail.

TUNE64 means signed tune0 and MIDI key60. The original `MidiKeyToFreq(wave,60,0)` evaluates to10512. Type0x20 is still pitched; do not confuse it with fixed-rate type0x08 or force one source sample per output sample. Compressed samples use the same Q23 fractional-position, linear interpolation and decoded-count exhaustion machinery as ordinary pitched PCM. Use the retained guard sample at index7133 when interpolating the last playable interval.

For an original integer mixer, centered pan is slightly asymmetric by construction. With default volX64, volume120, velocity127, rhythmPan0:

- Track scale=`120*64 >> 5 = 240`.
- Track volMR=`128*240 >> 8 = 120`, volML=`127*240 >> 8 = 119`.
- Channel right=`127*128*120 >> 14 = 119`; left=`127*127*119 >> 14 = 117`.
- At masterVolume12 and envelope255, intermediate envelope scale=`13*255 >> 4 = 207`; right envelope multiplier=`119*207 >> 8 = 96`, left=`117*207 >> 8 = 94`.

A modern renderer may deliberately use smooth/preserved float gains, but such a choice is a stated rendering mode difference, not a different source tone or DPCM format. Normal master/reverb state, priority10 channel allocation, and track-owner tie-breaking should integrate with the existing isolated player's mixer. No separate cry WAV should replace the decoded source asset in production playback.

## Verification completed and limits

`tools/inventory_cry.py` checks the fixed tone, header and lengths; verifies the original `.bin` equals the ROM bytes; decodes and compares all 7134 values against the original source WAV; validates the guard; rejects a one-byte-truncated payload; and emits raw original and patched template bytes, original owner, hashes and golden vectors. All checks passed.

The supplied comparison file `../runtime-audio-study/cry-stripped.wav` was only inspected as output data, with no implementation source read: 48000 Hz, stereo signed16, 192879 frames (4.0183125s), first nonzero frame851 and final nonzero frame36335. These are rendered-output observations, not definitions of the compressed format or proof of bit-exact hardware timing. They include startup/tail behavior beyond the nominal source sample duration.

This phase's format work does not independently certify the C# renderer, any GUI, or a runtime mix. Rendered comparison, concurrency, ducking/restoration and regression checks belong to implementation verification.
