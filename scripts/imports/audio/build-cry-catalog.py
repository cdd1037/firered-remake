#!/usr/bin/env python3
"""Build normal-cry metadata from matching pret source/assets and an audio pack.

The source directory must include its generated .bin cry assets. This tool reads
files only; it never runs the upstream build or writes into the source tree.
"""
import argparse
from pathlib import Path
import re
import struct
import wave

PACK_BASE = 0x08489968
PACK_END = 0x086FBEA4
CRY_TABLE = 0x0848C914
DELTA = (0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1)


def required_bytes(samples):
    if not 1 <= samples <= 1_000_000:
        raise ValueError('DPCM sample count must be between 1 and 1,000,000')
    full, tail = divmod(samples, 64)
    return full * 33 + (0 if tail == 0 else 1 if tail == 1 else 2 + (tail - 1) // 2)


def decode(payload, samples):
    if len(payload) < required_bytes(samples):
        raise ValueError('Truncated DPCM payload')
    output = bytearray()
    value = 0
    for index in range(samples):
        block, within = divmod(index, 64)
        base = block * 33
        if within == 0:
            value = payload[base]
        else:
            packed = payload[base + 1 + within // 2]
            value = (value + DELTA[packed & 15 if within & 1 else packed >> 4]) & 255
        output.append(value)
    return bytes(output)


def read_pack(pack, address, count):
    offset = address - PACK_BASE
    if offset < 0 or count < 0 or offset + count > len(pack):
        raise ValueError(f'Audio address out of bounds: 0x{address:08x} + {count}')
    return pack[offset:offset + count]


def build_catalog(reference, pack):
    if len(pack) != PACK_END - PACK_BASE:
        raise ValueError('Incorrect audio resource length')
    constants = (reference / 'include/constants/species.h').read_text()
    species = {name: int(value) for name, value in re.findall(
        r'^#define SPECIES_(\w+)\s+(\d+)\s*$', constants, re.M)
        if 1 <= int(value) <= 251 or 277 <= int(value) <= 411}
    cry_constants = {name: int(value) for name, value in re.findall(
        r'\bCRY_(\w+)\s*=\s*(\d+)', (reference / 'include/constants/hoenn_cries.h').read_text())}
    mappings = {species[name]: cry_constants[cry] for name, cry in re.findall(
        r'\[SPECIES_(\w+) - HOENN_MON_SPECIES_START\]\s*=\s*CRY_(\w+)',
        (reference / 'src/data/pokemon/cry_ids.h').read_text())}
    names = re.findall(r'^\s*cry (\w+)\s*$', (reference / 'sound/cry_tables.inc').read_text(), re.M)
    assets = dict(re.findall(r'(\w+)::\s*\.incbin "([^"]+)"',
        (reference / 'sound/direct_sound_data.inc').read_text()))
    if not species or set(mappings) != {value for value in species.values() if value >= 277}:
        raise ValueError('Missing species or Hoenn cry mappings')
    rows = []
    for name, internal_id in sorted(species.items(), key=lambda item: item[1]):
        index = internal_id - 1 if internal_id <= 251 else mappings[internal_id]
        if not 0 <= index < len(names) or names[index][4:].replace('_', '').upper() != name.replace('_', ''):
            raise ValueError(f'Cry table does not match species {name}')
        tone = read_pack(pack, CRY_TABLE + index * 12, 12)
        kind, key, length, pan, address, attack, decay, sustain, release = struct.unpack('<4BI4B', tone)
        if (kind, key, length, pan, attack, decay, sustain, release) != (32, 60, 0, 0, 255, 0, 255, 0):
            raise ValueError(f'Unsupported normal cry tone: {name}')
        asset_path = reference / assets[names[index]]
        asset = asset_path.read_bytes()
        if len(asset) < 17 or read_pack(pack, address, len(asset)) != asset:
            raise ValueError(f'Cry asset does not match the pack: {asset_path}')
        kind, status, frequency, loop, samples = struct.unpack('<HHIII', asset[:16])
        if (kind, status, frequency, loop) != (1, 0, 10512 * 1024, 0):
            raise ValueError(f'Unsupported cry sample header: {asset_path}')
        payload = asset[16:]
        guard_stored = len(payload) >= required_bytes(samples + 1)
        decoded = decode(payload, samples + int(guard_stored))
        if not guard_stored:
            decoded += decoded[-1:]
        with wave.open(str(asset_path.with_suffix('.wav')), 'rb') as source:
            if (source.getnchannels(), source.getsampwidth(), source.getframerate(), source.getnframes()) != (1, 1, 10512, samples + 1):
                raise ValueError(f'Unsupported source WAV: {asset_path.with_suffix(".wav")}')
            expected = bytes((value - 128) & 255 for value in source.readframes(samples + 1))
        if decoded != expected:
            raise ValueError(f'Decoded cry or interpolation guard differs from source WAV: {name}')
        rows.append(struct.pack('<HHIIB3x', internal_id, index, samples, len(payload), guard_stored))
    return b'FRCN0001' + struct.pack('<I', len(rows)) + b''.join(rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', required=True, type=Path, help='Matching pret source with generated cry .bin files and source .wav files')
    parser.add_argument('--pack', required=True, type=Path, help='Existing matching audio pack')
    parser.add_argument('--output', required=True, type=Path, help='Explicit output catalog path')
    args = parser.parse_args()
    catalog = build_catalog(args.reference, args.pack.read_bytes())
    args.output.write_bytes(catalog)
    print(f'Wrote {(len(catalog) - 12) // 16} normal cries to {args.output}')


if __name__ == '__main__':
    main()
