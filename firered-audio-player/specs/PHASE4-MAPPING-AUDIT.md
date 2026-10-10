# Phase 4: fixed FireRed ordinary-cry mapping audit

## Result and scope

The denominator is **386 ordinary species**, not 412 internal slots or 388 cry-table records. All 386 use the same forward compressed DirectSound tone format and the same normal-cry song template/defaults. However, **190 ordinary assets do not physically store the interpolation guard**. The strict Phase 3 Charmander assumption that every payload decodes `sampleCount + 1` samples does not generalize.

Reference: `pret/pokefirered` revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`, matching ROM SHA-1 `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc`. This audit reads original source, ELF/map/ROM, and `.incbin` assets. No agbplay or other player implementation, installation, or runtime edits were used.

Run `python tools/inventory_normal_cries.py`. Checked manifest: `results/phase4/inventory.json`. Its `species` array contains all 386 numeric `internalId`, `nationalDexId`, `cryIndex`, `toneAddress`, `waveAddress`, `payloadBytes`, `sampleCount`, `type`, and guard-policy fields plus names. `assets` covers all 388 table records, including the two unused ones, with exact extents, headers, hashes, source paths, and checks. Addresses are JSON numbers, not hex strings.

## Species and table mapping

Evidence: `include/constants/species.h`, `include/constants/pokedex.h`, `include/constants/hoenn_cries.h`, `src/data/pokemon/cry_ids.h`, `src/pokemon.c:519–933,5234–5242`, `src/sound.c:374–500`, and `sound/cry_tables.inc`.

- Ordinary internal IDs are **1–251 plus 277–411**. These map one-to-one to national dex IDs 1–386. National dex order differs from Hoenn internal order; Chimecho is internal 411, national 358.
- `SPECIES_NONE=0`; old Unown placeholders occupy 252–276; `SPECIES_EGG=412`, and `NUM_SPECIES` equals that egg sentinel. Additional Unown display-form IDs are 413–439. None, egg, and display forms are outside this audit. Do not pass these directly into an unchecked mapping function.
- `PlayCryInternal` first decrements the real internal species ID. Thus `SpeciesToCryId` takes a **zero-based internal species index**, despite its name.
- For ordinary IDs 1–251, cry index is `internalId - 1`. Old IDs 252–276 map to Unown index 200 and are not extra ordinary species.
- For IDs 277–411, cry index is `sHoennSpeciesIdToCryId[internalId - 277]`. This 135-entry `u16` table is at **0x082539d4**. The audit verifies source constants against the linked ROM table. `sSpeciesToNationalPokedexNum`, at **0x08251fee**, independently verifies the complete national denominator.
- Normal `gCryTable` is at **0x0848c914**. A tone is 12 bytes, so `toneAddress = gCryTable + cryIndex * 12`. The source's four 128-entry selection cases produce this same flat index.
- There are **388** records at indices 0–387. Unused indices **265 (`Cry_Unused265`)** and **268 (`Cry_Unused268`)** are not selected by any ordinary species. The other 386 records select 386 distinct physical sample addresses. `gCryTable_Reverse=0x0848db44` begins immediately after the normal table and is outside normal-mode scope.

Representative nontrivial mappings: Treecko internal277 → cry273; Wailord internal314 → cry381; Deoxys internal410 → cry386; Chimecho internal411 → cry387. The manifest is authoritative for the complete mapping.

## Shared tone, template, and normal parameters

`asm/macros/music_voice.inc:16–28,152–158` defines every normal record using `cry`, which expands to type **0x20**, key60, length0, pan/sweep0, variable wave pointer, ADSR **255/0/255/0**. All 388 actual ROM records were checked. Normal cries require forward, pitched/resampled compressed PCM; no reverse, fixed-rate PCM, PSG, key-split, or rhythm tone appears.

Every wave header is type1, status0, frequency word10764288 (**10512 Hz ×1024**), loopStart0. All are nonlooped. Only sample pointer, data, and playable length vary. Ordinary playable lengths span **1902 (Pidgey, ~0.180936s)** to **23524 (Jynx, ~2.237823s)**; encoded payloads span 981–12130 bytes. All nominal durations are shorter than the normal 140-tick score interval (~2.343979s).

`gPokemonCrySongTemplate=0x08489c20` is the shared 52-byte dynamic song (`src/m4a_tables.c:259–289`). `PlayCryInternal` normal defaults are pitch15360, length140, release0, progress0, chorus0, forward. `PlayCry_Normal` adds volume120 and priority10; pan is caller-supplied (research uses0). `PlayCry_NormalNoDucking` allows caller-supplied volume and priority, so those are API defaults rather than species data.

`SetPokemonCryTone` copies the same song and relocates its track/GOTO pointers (`src/m4a.c:1656–1778`). Chorus0 leaves one active track. Required commands remain TUNE, GOTO, VOICE, VOL, PAN, TIE, EOT/FINE and XCMD **0x0d progress(u32), 0x07 release(u8), 0x0c wait(u16)**. No species requires another template or XCMD type. Stereo1 is the research/global mixer setting, not a per-species value set by `PlayCryInternal`. Header reverb0 does not carry `REVERB_SET`, so existing global reverb remains relevant. Phase 3's sequencing, ownership, and ducking caveats still apply.

## Physical bounds and the missing-guard distinction

The physical asset extent comes from the exact `.incbin` path in `sound/direct_sound_data.inc` and that file's length, verified byte-for-byte against the ROM. The next source-declared symbol only checks the expected `.align 2` gap. Neither `16 + sampleCount` nor an arbitrary next ROM symbol defines payload length.

Every source WAV contains `sampleCount + 1` mono unsigned8 frames at10512 Hz. Its last frame is an interpolation guard, not another playable sample. The original `audio_rules.mk:18–21` invokes `wav2agb -b -c -l 1 --no-pad`; `tools/wav2agb/converter.cpp:214–282` can stage the final even-index high nibble without emitting its byte when there is no following low nibble.

- **196/386 ordinary assets:** the guard is encoded inside the bounded payload (`guardStored=true`). Decode exactly `sampleCount + 1` values, retaining that real guard.
- **190/386 ordinary assets:** every playable sample is present, but the guard nibble is absent (`guardStored=false`). In all such assets the expected guard equals the final playable sample. The original decoder gets delta0 from the high nibble of the following byte: source-declared alignment0 in143 cases, or the verified next `.incbin` wave header's byte0x01 in47 cases.
- For this fixed audited library, safe bounded reconstruction is to decode exactly `sampleCount` values and append the last playable value **only when the manifest marks the guard absent**. Each reconstructed value was independently checked against both the original source WAV and actual adjacent ROM layout. Do not adopt repeat-last as a blanket fallback for arbitrary malformed/truncated DPCM.
- Across all388 records, 192 guards need this policy; the extra two are the unused cries. One-byte truncation of every payload is rejected for the required physically stored decoded samples.

## Verification and limit

All **3,136,081 ordinary playable samples plus386 guards =3,136,467** values match original WAV values after unsigned-to-signed byte conversion: **zero differences**. Including unused records gives3,149,809 checked values, also zero differences. Source/ROM assets, mapping tables, exact alignment, headers, guards, and truncation checks pass.

This proves mapping and source-asset decoding for the fixed library. It does **not** prove rendered timing, pitch interpolation, mixer arithmetic, synthesized-waveform equality, concurrency behavior, or runtime support for all386 species. Those require the separate render/probe assessment before promoting support.
