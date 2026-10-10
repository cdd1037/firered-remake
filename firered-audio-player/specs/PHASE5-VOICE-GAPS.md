# Phase 5: bounded echo and rhythm source contract

Reference: original `pret/pokefirered` revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`. This is a source audit and proposed test contract, not implemented support. It follows the frozen representative results in `PHASE5-MUSIC-COVERAGE.md`. No third-party player source was used.

## XCMD8/9 are per-note pseudo-echo state

`src/m4a_tables.c:292–308` dispatches subcommand8 to `ply_xiecv`, and9 to `ply_xiecl`. `src/m4a.c:1523–1528,1591–1600` reads the subcommand, then consumes **one raw unsigned byte** for either track field: `pseudoEchoVolume` or `pseudoEchoLength`. No7-bit mask or unit conversion is applied.

Track initialization clears these fields to0 (`src/m4a_1.s:1215–1230`). Note allocation copies the adjacent volume/length pair into the newly allocated channel (`:1757–1758`). Changing the track afterward therefore affects subsequent notes; it must not overwrite the active voice's parameters or echo countdown.

XCMD establishes running status, just like other commands at or above0xbd (`src/m4a_1.s:1233–1244`). Thus `cd 08 0a 09 08` means XCMD8(value10), then running-status XCMD9(value8). The second command does not need another0xcd. Echo is a low-level held envelope tail on the current oscillator/sample, not a delayed/repeated recording and not global reverb. It must not restart sample position or oscillator phase.

### PCM branch

Exact branch: `src/m4a_1.s:205–251`.

1. If already in IEC state (`status & 0x04`), decrement the stored echo length. Original ARM `subs`/`bhi` keeps the voice only when the previous unsigned byte was greater than1. Previous0 or1 turns it off. Keep the current echo envelope while it lasts.
2. Otherwise, on STOP/release, compute `nextEnv = (env * release) >> 8`. If greater than `pseudoEchoVolume`, continue release. At/below the floor, enter pseudo-echo: set envelope directly to `pseudoEchoVolume`; if this is0, stop instead.
3. Decay reaching a zero sustain also enters this same pseudo-echo branch, without needing an explicit STOP (`:241–248`). Positive sustain follows ordinary sustain behavior.
4. Entering IEC does not decrement the length on that same frame. Therefore length8 gives the entry frame plus7 subsequent active frames and then stops on the next; length0 or1 still has an entry-frame tail and stops on the next call. Natural nonlooped sample exhaustion can stop it earlier.

PCM echo volume is an **absolute 0–255 envelope floor**, not a percentage of its prior envelope and not scaled by channel target volume here. Later ordinary mixer volume/master scaling still applies.

### CGB branch differs

Exact branch: `src/m4a.c:1048–1058,1060–1074,1084–1103,1120–1129`.

- Release finishing, instantaneous release0, or decay reaching sustain0 enters pseudo-echo.
- Echo envelope is `((envelopeGoal * pseudoEchoVolume) + 255) >> 8`, i.e. integer ceiling of goal×volume/256. A nonzero result sets IEC and requests a hardware volume update; zero stops the oscillator.
- For square/noise channels the tail requests envelope direction increment with step time0. Wave-channel gain still passes through `gCgb3Vol` (`src/m4a.c:1205–1225`; `src/m4a_tables.c:168–175`). In particular a CGB-wave envelope of1 maps to hardware volume0, even though driver IEC state remains live. Do not conflate audible silence and channel ownership.
- On later calls, echo length is decremented as a byte, then interpreted as signed8; `signed8((length - 1) & 255) <= 0` stops. This agrees with PCM for the selected small lengths, but differs for large values. For example initial129 expires on the first subsequent call, whereas initial128 becomes127 and continues. Preserve or explicitly bound this behavior rather than assuming an unbounded integer timer.
- The IEC branch jumps straight to envelope completion, bypassing the ordinary every15-frame double-step envelope correction (`:1173–1181`). Echo duration is in sound-frame calls, not64-Hz envelope steps and not tempo-dependent sequence ticks. `SoundMain` processes players then CGB sound once per call (`src/m4a_1.s:51–59`).

### Actual selected command values

Track indices below are zero-based, and source paths are under `sound/songs/midi/`.

| Song / tracks | Source lines | Actual sequence | Effective fields before following notes |
|---|---|---|---|
| Route1 /4,5 | `mus_route1.s:1414–1415,1681–1682` | `xIECV18`, then `xIECV16` | volume16; length remains initial0 |
| Trainer Victory /4,5,6 | `mus_victory_trainer.s:1837–1838,2062–2063,2504–2505` | `xIECV18`, then `xIECV16` | volume16; length remains initial0 |
| Wild Victory /3,4 | `mus_victory_wild.s:692–693,791–792` | `xIECV8`, then `xIECL8` | volume8,length8 |
| PC On /0 | `se_pc_on.s:23–24` | `xIECV10`, then `xIECL8` | volume10,length8 |

The first two rows really contain XCMD8 twice: linked bytes are `cd 08 12 08 10`. Do not silently reinterpret the second8 as XCMD9. The ROM commands for the other rows are respectively `cd 08 08 09 08` and `cd 08 0a 09 08`.

## Rhythm is a lookup plus pitch and pan changes

`asm/macros/music_voice.inc:140–150` defines key-split0x40 and rhythm0x80 (`voice_keysplit_all`). The latter name is potentially misleading: it is a rhythm table indexed directly by the sequence note key. Original resolution is `src/m4a_1.s:1577–1626`:

1. Start with parent track tone and raw sequence key, before transpose/bend.
2. If parent0x40 is set, index=`keySplitTable[rawKey]`; otherwise a composite0x80 uses index=`rawKey`.
3. Child tone address is `parent.data + 12*index`.
4. A child that is itself0x40/0x80 is not recursively expanded: the original returns without allocating a note (`:1603–1607,1806`). A bounded native implementation should explicitly reject unsupported nesting rather than substitute sound.
5. If parent0x80 is set, replace the channel's sounding key with **child ToneData.key**. If child pan/sweep has bit0x80, set rhythm pan to signed8 of `2*(pan_sweep - 0xc0)`; otherwise use0. Key-split-only0x40 keeps the incoming key and no rhythm pan.
6. Copy the child type/wave/ADSR, sounding key and rhythm pan into the channel (`:1742–1758`). Apply ordinary track key/pitch modulation to the sounding key (`:1760–1765`); clamp negative resulting key to0 in this original path.

The original channel separately preserves the **raw MIDI key** through the four-byte gate/key/velocity copy at`:1742–1743`; channel sounding key lives elsewhere. `ply_endtie` matches channel `midiKey`, not sounding key (`:1818–1848`). Preserve both identities for a rhythm note: a key36 drum whose child key is64 must still respond to EOT36.

Rhythm pan is an extra per-note factor combined with track pan/volume, not a replacement track PAN command. `ChnVolSetAsm` (`:1508–1535`) computes, with signed rhythmPan:

- right=`min(255, ((128 + rhythmPan) * velocity * track.volMR) >> 14)`
- left=`min(255, ((127 - rhythmPan) * velocity * track.volML) >> 14)`

The normal centered asymmetry remains. Child pan0xc0 yields rhythmPan0;0xd0 yields+32;0x9d yields−70.

### Concrete selected mappings

Pewter program0 parent`0x0849db28`, Trainer Battle program0 parent`0x08497c78`, and Route1 program0 parent`0x08495bfc` all point to `voicegroup002=0x0848a0d0`. Original definitions are in `sound/voice_groups.inc`.

- Incoming36 → child`0x0848a280`, line136: type0x08, sounding key64, pan0xc0; drum-and-percussion kick.
- Incoming38 → child`0x0848a298`, line138: type0x08, sounding key64, pan0xc0; SD90 solo snare.
- Incoming42 → child`0x0848a2c8`, line142: type0x08, sounding key64, pan0xd0 (+32 rhythm pan); orchestra snare.
- Incoming57 → child`0x0848a37c`, line157: type0x08, sounding key64, pan0x9d (−70 rhythm pan); orchestra cymbal.

Trainer Victory instead points to `voicegroup001=0x08489f74`; incoming36 selects`0x0848a124` (line107), type0x08, sounding key60, centered pan, RND kick. Some original voicegroup labels start at different offsets within contiguous shared descriptor data: do not infer a valid lookup's end solely from the next voicegroup label. The source declaration/ROM address must be validated.

## Bounded tests before widening coverage

1. Decode the four command byte sequences above, including running status. Assert cursor advance, final fields, initial length0, and no state bleed between tracks or start/restart. Unsupported XCMD values must still fail explicitly.
2. Create two overlapping notes before/after changing echo fields. Assert each retains its note-on snapshot and its own countdown. Neither changes the other or global reverb.
3. PCM envelope vectors: release0 with echo0 stops immediately; release0/echo16 enters envelope16; env255/release128/echo16 follows127→63→31→16(IEC). Test sustain0 decay entry and natural sample exhaustion during IEC.
4. CGB vectors: goal15/volume8 gives envelope1; goal0 gives0/off. Test entry plus8-frame tail, lengths0/1, and signed boundary128/129 if general byte values are accepted. Keep CGB-wave hardware-volume quantization distinct from driver state. Do not convert these counters to musical ticks.
5. Verify rhythm36/38/42/57 exact child address, type, sounding key and signed pan above, with positive/negative track key shifts. Include raw-key36→sounding64→EOT36 and a contrasting key-split-only case. Reject out-of-image children and nested composites before mutating active channels.
6. Rerun the frozen12-song matrix and inspect every newly exposed blocker. PC On is a small direct XCMD8/9 candidate; Route1 and victory/battle pieces also need additional voice families, so an echo parser pass is not enough.

## Wave/noise boundary for the subsequent step

Types0x03/0x0b are programmable wave;0x04/0x0c are noise (`asm/macros/music_voice.inc:91–137`). Wild Battle's first rejected0x0b program87 uses `ProgrammableWaveData_6`; Door's0x0c program127 is noise period0. These are separate CGB channels with their own allocation, envelope and hardware register behavior. Original entry points for later audit are `MidiKeyToCgbFreq` (`src/m4a.c:810–853`), wave-RAM loading/noise width (`:995–1023`), pitch quantization/register writes (`:1183–1202`), and `gNoiseTable`/`gCgb3Vol` (`src/m4a_tables.c:149–175`). Their oscillator synthesis is deliberately not specified or implemented here.
