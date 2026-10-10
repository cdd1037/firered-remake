#!/usr/bin/env python3
"""Fixed original song-table/source identity and declared-tone coverage, no synthesis."""
import argparse
import hashlib
import json
import pathlib
import re
import struct
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
REF = ROOT.parent / 'audio-reference-build/pokefirered'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--out-dir', type=pathlib.Path, default=ROOT / 'results/phase5-music')
OUT = parser.parse_args().out_dir
REV = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git', '-C', str(REF), 'rev-parse', 'HEAD'], text=True).strip() == REV
rom = (REF / 'pokefirered.gba').read_bytes()
assert hashlib.sha1(rom).hexdigest() == '41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc'
link_map = (REF / 'pokefirered.map').read_text()
symbols = {name: int(address, 16) for address, name in re.findall(r'0x([0-9a-f]{8})\s+(\w+)\s*$', link_map, re.M)}
table = [(name, int(ms), int(me)) for name, ms, me in re.findall(
    r'^\s*song (\w+),\s*(\d+),\s*(\d+)', (REF / 'sound/song_table.inc').read_text(), re.M)]
constants = {name.lower(): int(value) for name, value in re.findall(
    r'^#define ((?:MUS|SE)_\w+)\s+(\d+)', (REF / 'include/constants/songs.h').read_text(), re.M)}
chosen = ['mus_route1', 'mus_pewter', 'mus_oak_lab', 'mus_vs_wild', 'mus_vs_trainer',
          'mus_victory_trainer', 'mus_victory_wild', 'mus_heal', 'se_door', 'se_pc_on', 'se_ball', 'se_save']

def read(address, count):
    offset = address - 0x08000000
    assert 0 <= offset and offset + count <= len(rom)
    return rom[offset:offset + count]

def u32(address):
    return struct.unpack('<I', read(address, 4))[0]

voice_source = (REF / 'sound/voice_groups.inc').read_text()
voice_lines = {}
current = None
index = 0
for line_number, line in enumerate(voice_source.splitlines(), 1):
    label = re.match(r'(\w+)::', line)
    if label:
        current = label[1]
        index = 0
    if re.match(r'\s*voice_\w+', line) and current in symbols:
        voice_lines[symbols[current] + index * 12] = {'path': 'sound/voice_groups.inc', 'line': line_number, 'text': line.strip()}
        index += 1

def tone(address):
    typ, key, length, pan, data, attack, decay, sustain, release = struct.unpack('<4BI4B', read(address, 12))
    result = {'address': address, 'type': typ, 'typeHex': f'0x{typ:02x}', 'key': key, 'length': length,
              'panSweep': pan, 'data': data, 'adsr': [attack, decay, sustain, release],
              'source': voice_lines.get(address)}
    if typ & 0x40:
        keymap = u32(address + 8)
        indices = sorted(set(read(keymap, 128)))
        result['keySplitMap'] = keymap
        result['resolvedCandidates'] = [tone(data + i * 12) for i in indices]
    elif typ & 0x80:
        result['rhythmMapping'] = True
        result['resolvedCandidates'] = [tone(data + i * 12) for i in range(128)]
    return result

rows = []
for name in chosen:
    song_id = constants[name]
    assert table[song_id][0] == name
    header = symbols[name]
    entry = symbols['gSongTable'] + song_id * 8
    assert u32(entry) == header
    assert struct.unpack('<HH', read(entry + 4, 4)) == table[song_id][1:]
    paths = list((REF / 'sound/songs').rglob(name + '.s'))
    assert len(paths) == 1
    source = paths[0].read_text()
    # A top-level numbered track can contain inline pattern labels. Preserve
    # its identity across those labels when recording declared loop commands.
    source_tracks = []
    current_track = None
    for line_number, line in enumerate(source.splitlines(), 1):
        match = re.match(rf'{re.escape(name)}_(\d+):$', line)
        if match:
            current_track = {'trackIndex': int(match[1]) - 1, 'entryLine': line_number, 'gotoLines': []}
            source_tracks.append(current_track)
        if current_track is not None and re.search(r'\bGOTO\b', line):
            current_track['gotoLines'].append(line_number)
    programs = sorted({int(value) for value in re.findall(r'\bVOICE\s*,\s*(\d+)', source)})
    group = u32(header + 4)
    group_names = sorted(k for k, v in symbols.items() if v == group and k.startswith('voicegroup'))
    assert len(group_names) == 1
    song_tones = [{'program': program, **tone(group + 12 * program)} for program in programs]
    leaf_types = set()
    def gather(item):
        if 'resolvedCandidates' in item:
            for child in item['resolvedCandidates']:
                gather(child)
        else:
            leaf_types.add(item['type'])
    for item in song_tones:
        gather(item)
    rows.append({'name': name, 'songId': song_id, 'songAddress': header, 'songTableAddress': entry,
                 'originalPlayer': table[song_id][1], 'researchPlayer': 0 if name.startswith('mus_') and name != 'mus_heal' else 1,
                 'seconds': 120 if name in ('mus_pewter', 'mus_vs_trainer') else
                            60 if name.startswith('mus_') and name != 'mus_heal' else 20,
                 'expectedLooping': name.startswith('mus_') and name != 'mus_heal',
                 'trackCount': read(header, 1)[0], 'priority': read(header + 2, 1)[0], 'reverb': read(header + 3, 1)[0],
                 'voicegroup': group, 'voicegroupSymbol': group_names[0],
                 'sourcePath': paths[0].relative_to(REF).as_posix(),
                 'sourceSha256': hashlib.sha256(paths[0].read_bytes()).hexdigest(),
                 'sourceTracks': source_tracks,
                 'declaredEchoCommands': [{'line': n, 'text': line.strip()} for n, line in enumerate(source.splitlines(), 1)
                                          if re.search(r'\bxIEC[VL]\b', line)],
                 'declaredPrograms': programs, 'potentialLeafTypes': sorted(leaf_types), 'declaredTones': song_tones})
OUT.mkdir(parents=True, exist_ok=True)
result = {'fixedRevision': REV, 'romSha1': hashlib.sha1(rom).hexdigest(), 'songs': rows,
          'runtimeSourceSha256': {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest()
                                 for p in ['src/Resources.cs', 'src/Sequence.cs', 'src/Synth.cs', 'src/PsgSynth.cs', 'src/Profiles.cs', 'src/Cry.cs', 'src/CryCatalog.cs']},
          'inventoryScope': 'All literal VOICE declarations in each selected assembly, including all keysplit candidates; not proof every candidate executes.'}
(OUT / 'selection.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps([{'name': row['name'], 'songId': row['songId'], 'potentialLeafTypes': row['potentialLeafTypes']} for row in rows], indent=2))
