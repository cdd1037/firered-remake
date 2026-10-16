#!/usr/bin/env python3
"""Build the runtime cue catalog from pinned source identities and the original audio pack.

No generated cue manifests, regional recipes, ROM or upstream build scripts are inputs.
The pack's sequence control flow supplies loop metadata even when source MIDI is absent.
"""
import argparse
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[3]
REFERENCE = ROOT / 'content/raw/pokefirered/037335f4'
PACK_BASE, PACK_END, SONG_TABLE = 0x08489968, 0x086FBEA4, 0x084A32CC

class Pack:
    def __init__(self, data):
        if len(data) != PACK_END - PACK_BASE:
            raise ValueError('Audio pack length differs')
        self.data = data
    def read(self, address, size):
        offset = address - PACK_BASE
        if offset < 0 or size < 0 or offset + size > len(self.data):
            raise ValueError(f'Audio pointer outside pack: {address:#x}')
        return self.data[offset:offset + size]
    def byte(self, address):
        return self.read(address, 1)[0]
    def pointer(self, address):
        value = struct.unpack('<I', self.read(address, 4))[0]
        self.read(value, 1)
        return value


def track_loops(pack, cursor):
    """Follow M4A control flow, including running status and bounded pattern returns.

A repeated instruction/return-stack/status state proves an infinite sequence loop.
This reads instructions, never scans arbitrary operand bytes for a GOTO opcode.
"""
    status, returns, visited = 0, [], set()
    for _ in range(200000):
        state = (cursor, status, tuple(returns))
        if state in visited:
            return True
        visited.add(state)
        op = pack.byte(cursor)
        if op >= 0x80:
            cursor += 1
            if op >= 0xBD:
                status = op
        else:
            op = status
            if op < 0xBD:
                raise ValueError('Missing sequence running status')
        if 0x80 <= op <= 0xB0:
            continue
        if op >= 0xCF:
            for _ in range(2 if op == 0xCF else 3):
                if pack.byte(cursor) >= 0x80:
                    break
                cursor += 1
        elif op == 0xB1:
            return False
        elif op == 0xB2:
            cursor = pack.pointer(cursor)
        elif op == 0xB3:
            if len(returns) >= 3:
                raise ValueError('Sequence pattern stack overflow')
            returns.append(cursor + 4)
            cursor = pack.pointer(cursor)
        elif op == 0xB4:
            if returns:
                cursor = returns.pop()
        elif op in (*range(0xBA, 0xC6), 0xC8):
            pack.read(cursor, 1)
            cursor += 1
        elif op == 0xCE:
            if pack.byte(cursor) < 0x80:
                cursor += 1
        elif op == 0xCD:
            command = pack.byte(cursor)
            size = {7: 1, 8: 1, 9: 1, 12: 2, 13: 4}.get(command)
            if size is None:
                raise ValueError(f'Unsupported sequence XCMD {command}')
            pack.read(cursor + 1, size)
            cursor += 1 + size
        else:
            raise ValueError(f'Unsupported sequence opcode {op:#x}')
    raise ValueError('Sequence analysis budget exceeded')


def build_catalog(reference, pack_bytes, cries):
    pack = Pack(pack_bytes)
    constants = [(s, int(n)) for s, n in re.findall(r'^#define ((?:MUS|SE)_\w+)\s+(\d+)\b',
        (reference / 'include/constants/songs.h').read_text(), re.M)]
    table = re.findall(r'^\s*song\s+(\w+),\s*(\d+),\s*(\d+)',
        (reference / 'sound/song_table.inc').read_text(), re.M)
    fanfare_text = (reference / 'src/sound.c').read_text().split('sFanfares[] = {', 1)[1].split('};', 1)[0]
    counters = {symbol: int(n) for symbol, n in re.findall(r'\{\s*(MUS_\w+),\s*(\d+)\s*\}', fanfare_text)}
    if not constants or not counters or len(set(s for s, _ in constants)) != len(constants):
        raise ValueError('Missing or duplicated source song identities')
    cues = []
    for symbol, index in constants:
        if symbol == 'MUS_DUMMY':  # Source stop/silence sentinel, never a playable song.
            continue
        label, player, player2 = table[index]
        player, player2 = int(player), int(player2)
        if label != symbol.lower() or player != player2 or player not in range(4):
            raise ValueError('Source song table differs: ' + symbol)
        address, packed_player, packed_player2 = struct.unpack('<IHH', pack.read(SONG_TABLE + index * 8, 8))
        if (packed_player, packed_player2) != (player, player2):
            raise ValueError('Packed song table differs: ' + symbol)
        tracks, blocks, priority, reverb = pack.read(address, 4)
        if not 1 <= tracks <= 16 or blocks:
            raise ValueError('Invalid song header: ' + symbol)
        pack.read(pack.pointer(address + 4), 12)
        try:
            loops = [track_loops(pack, pack.pointer(address + 8 + n * 4)) for n in range(tracks)]
        except ValueError as error:
            if symbol == "MUS_RS_VS_TRAINER" and str(error) == "Unsupported sequence opcode 0xb9":
                continue  # MEMACC is not implemented by the shared runtime.
            raise ValueError(symbol + ": " + str(error)) from error
        looping = any(loops)
        midi = reference / 'sound/songs/midi' / (label + '.mid')
        if midi.exists():
            data = midi.read_bytes()
            if not data.startswith(b'MThd'):
                raise ValueError('Invalid MIDI: ' + symbol)
            markers = b'\xff\x06\x01[' in data and b'\xff\x06\x01]' in data
            if markers != looping:
                raise ValueError('MIDI/packed sequence loop disagreement: ' + symbol)
        cues.append(dict(id=symbol, sourceSymbol=symbol, sourcePlayer=player, priority=priority,
                         sourceCounter=counters.get(symbol), songAddress=address, species=0, looping=looping))
    species = {int(n): s for s, n in re.findall(r'^#define (SPECIES_\w+)\s+(\d+)\b',
        (reference / 'include/constants/species.h').read_text(), re.M)}
    if cries[:8] != b'FRCN0001' or len(cries) < 12:
        raise ValueError('Invalid normal cry catalog')
    count = struct.unpack_from('<I', cries, 8)[0]
    if not count or len(cries) != 12 + count * 16:
        raise ValueError('Normal cry catalog length differs')
    seen = set()
    for offset in range(12, len(cries), 16):
        sid = struct.unpack_from('<H', cries, offset)[0]
        symbol = species.get(sid)
        if sid in seen or not symbol or symbol == 'SPECIES_NONE' or 'OLD_UNOWN' in symbol or symbol == 'SPECIES_EGG':
            raise ValueError('Invalid canonical cry species')
        seen.add(sid)
        for mode, suffix in ((0, 'NORMAL'), (2, 'ENCOUNTER')):
            cues.append(dict(id='CRY_' + symbol.removeprefix('SPECIES_') + '_' + suffix,
                sourceSymbol=symbol, sourcePlayer=4, priority=10, sourceCounter=None,
                songAddress=0, species=sid, looping=False, cryMode=mode))
    return dict(schemaVersion=1, cues=sorted(cues, key=lambda cue: cue['id']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=REFERENCE)
    parser.add_argument('--pack', type=Path, default=ROOT / 'content/generated/audio/synth/firered.pack')
    parser.add_argument('--cries', type=Path, default=ROOT / 'content/generated/audio/synth/normal-cries.bin')
    parser.add_argument('--output', type=Path, default=ROOT / 'content/generated/audio/cues.json')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    catalog = build_catalog(args.reference, args.pack.read_bytes(), args.cries.read_bytes())
    data = (json.dumps(catalog, ensure_ascii=False, separators=(',', ':')) + '\n').encode()
    if args.check:
        if args.output.read_bytes() != data:
            raise ValueError('Generated audio cue catalog differs')
    else:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_bytes(data)
    print(json.dumps(dict(cues=len(catalog['cues']), output=str(args.output), checked=args.check)))

if __name__ == '__main__':
    main()
