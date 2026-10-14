#!/usr/bin/env python3
"""Derive Teachy TV's one missing music binding from pinned local source and the existing pack.

REFERENCE OUTPUT --dependency PROJECT [--check]. No reference programs, audio
recordings, duplicate pack or source blobs are written. The existing Teachy TV
catalog already preserves its dedicated menu MIDI as an authenticated input.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
LOCK_SHA256 = 'fbe5ed7ab6a826c6c3b747ea57082352ee66a0033a50f00a640a224ba6db9a54'
BASE, END, TABLE = 0x08489968, 0x086FBEA4, 0x084A32CC
OUTPUT = 'content/generated/teachy-tv-audio/'
IDS = {'MUS_TEACHY_TV_MENU': 346, 'MUS_FOLLOW_ME': 272, 'SE_SELECT': 5}


def need(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()


def safe(root, path):
    need(path and not Path(path).is_absolute() and '\\' not in path and ':' not in path and
         all(part not in ('', '.', '..') for part in path.split('/')), 'Unsafe resource path')
    result = root / path
    need(not any(p.is_symlink() for p in (result, *result.parents)), 'Symlink resource rejected: ' + path)
    return result


def generate(reference, dependency, lock_path):
    lock_bytes = lock_path.read_bytes()
    need(sha(lock_bytes) == LOCK_SHA256, 'Teachy TV audio lock identity differs')
    lock = json.loads(lock_bytes)
    need((lock['sourceCommit'], lock['sourceTree']) == (COMMIT, TREE), 'Pinned source revision differs')
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull,
               GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')

    def git(*args):
        return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks',
                                       '-C', str(reference), *args], env=env)

    need(git('rev-parse', COMMIT + '^{tree}').decode().strip() == TREE, 'Pinned source tree differs')
    sources, dependencies = {}, {}
    for row in lock['inputs']:
        path, blob = row['path'], row['gitBlobSha1']
        need(git('ls-tree', '-z', COMMIT, '--', path) ==
             f'100644 blob {blob}\t{path}\0'.encode(), 'Pinned regular Git blob differs: ' + path)
        data = git('cat-file', 'blob', blob)
        need((len(data), sha(data), hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest()) ==
             (row['byteLength'], row['sha256'], blob), 'Pinned source bytes differ: ' + path)
        sources[path] = data
    for row in lock['inheritedInputs']:
        data = safe(dependency, row['path']).read_bytes()
        need((len(data), sha(data)) == (row['byteLength'], row['sha256']),
             'Inherited input identity differs: ' + row['path'])
        dependencies[row['path']] = data
    pack = dependencies['content/generated/synth-audio/firered.pack']
    need(len(pack) == END - BASE, 'Original audio address range differs')
    baseline = json.loads(dependencies['content/generated/synth-audio/manifest.json'])
    need(baseline['schemaVersion'] == 1 and baseline['sourceCommit'] == COMMIT and
         len(baseline['cues']) == 246, 'Inherited cue manifest contract differs')

    def offset(address, length):
        need(length > 0 and BASE <= address < END and address + length <= END,
             'Audio address exceeds authenticated pack')
        return address - BASE

    def function(text, name):
        matches = list(re.finditer(r'^(?:static )?[\w *]+\b' + name +
                                  r'\([^;]*?\)\n\{(.*?)\n\}', text, re.M | re.S))
        need(len(matches) == 1, 'Source function differs: ' + name)
        return matches[0][0]

    outer = sources['src/teachy_tv.c'].decode()
    text_source = sources['src/text.c'].decode()
    need(set(re.findall(r'PlaySE\((SE_\w+)\)', outer)) == {'SE_SELECT'}, 'Teachy TV direct effects differ')
    need(set(re.findall(r'PlayNewMapMusic\((MUS_\w+)\)', outer)) ==
         {'MUS_TEACHY_TV_MENU', 'MUS_FOLLOW_ME'}, 'Teachy TV original music differs')
    music_uses = {
        'TeachyTvMainCallback': 'MUS_TEACHY_TV_MENU',
        'TTVcmd_TransitionRenderBg2TeachyTvGraphicInitNpcPos': 'MUS_FOLLOW_ME',
        'TTVcmd_End': 'MUS_TEACHY_TV_MENU',
        'TeachyTvRestorePlayerPartyCallback': 'MUS_FOLLOW_ME'}
    for name, symbol in music_uses.items():
        need('PlayNewMapMusic(' + symbol + ')' in function(outer, name), 'Source music use differs: ' + name)
    for name in ['TextPrinterWait', 'TextPrinterWaitWithDownArrow']:
        body = function(text_source, name)
        need('PlaySE(SE_SELECT)' in body and 'JOY_NEW(A_BUTTON | B_BUTTON)' in body,
             'Text acknowledgement effect differs: ' + name)
    grass = '\n'.join(function(outer, name) for name in
                      ['TeachyTvGrassAnimationMain', 'TeachyTvGrassAnimationObjCallback',
                       'TeachyTvGrassAnimationCheckIfNeedsToGenerateGrassObj'])
    need(not re.search(r'Play(?:SE|Fanfare|BGM)|\bSE_\w+|FieldEffectStart', grass),
         'Custom Teachy grass acquired an unaccounted sound effect')
    need('PlayMapChosenOrBattleBGM(MUS_DUMMY)' in function(outer, 'TeachyTvPrepBattle') and
         'Overworld_PlaySpecialMapMusic()' in function(outer, 'TeachyTvQuitFadeControlAndTaskDel'),
         'Battle/field music handoff differs')
    need(not re.search(r'\{(?:PLAY_SE|PLAY_BGM|PLAY_MUSIC|PAUSE_MUSIC|RESUME_MUSIC)\b',
                       sources['src/data/text/teachy_tv.h'].decode()), 'Unexpected text-embedded music/effect')

    constants = sources['include/constants/songs.h'].decode()
    table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', sources['sound/song_table.inc'].decode(), re.M)
    cfg = sources['sound/songs/midi/midi.cfg'].decode()
    cues, headers = [], []
    for symbol, expected_id in IDS.items():
        match = re.search(r'^#define ' + symbol + r'\s+(\d+)\b', constants, re.M)
        need(match is not None and int(match[1]) == expected_id, 'Original sound ID differs: ' + symbol)
        label, player, player2 = table[expected_id]
        need(label == symbol.lower() and player == player2 and int(player) == (2 if symbol == 'SE_SELECT' else 0),
             'Original player routing differs: ' + symbol)
        path = 'sound/songs/midi/' + label + '.mid'
        midi = sources[path]
        need(midi[:8] == b'MThd\x00\x00\x00\x06', 'MIDI header differs: ' + symbol)
        loop = b'\xff\x06\x01[' in midi and b'\xff\x06\x01]' in midi
        need(loop == symbol.startswith('MUS_'), 'Source MIDI loop markers differ: ' + symbol)
        config = re.search(r'^' + label + r'\.mid:\s+(.+)$', cfg, re.M)
        need(config is not None, 'MIDI build options missing: ' + symbol)
        priorities = re.findall(r'(?:^|\s)-P(\d+)(?:\s|$)', config[1])
        need(len(priorities) <= 1, 'MIDI priority is ambiguous: ' + symbol)
        expected_priority = int(priorities[0]) if priorities else 0
        address, packed_player, packed_player2 = struct.unpack_from('<IHH', pack, offset(TABLE + expected_id * 8, 8))
        need((packed_player, packed_player2) == (int(player), int(player2)), 'Packed player differs: ' + symbol)
        count, blocks, priority, reverb, voices = struct.unpack_from('<BBBBI', pack, offset(address, 8))
        need(1 <= count <= 16 and blocks == 0 and priority == expected_priority, 'Packed song header differs: ' + symbol)
        tracks = list(struct.unpack_from('<' + str(count) + 'I', pack, offset(address + 8, count * 4)))
        offset(voices, 12)
        for track in tracks:
            offset(track, 1)
        cue = dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath=path,
                   sourcePlayer=int(player), priority=priority, sourceCounter=None,
                   songAddress=address, species=0, looping=loop)
        inherited = [c for c in baseline['cues'] if c['id'] == symbol]
        need(inherited == ([] if symbol == 'MUS_TEACHY_TV_MENU' else [cue]), 'Inherited cue scope differs: ' + symbol)
        cues.append(cue)
        headers.append(dict(sourceId=expected_id, sourceSymbol=symbol, songAddress=hex(address),
                            trackAddresses=[hex(x) for x in tracks], voicegroupAddress=hex(voices),
                            trackCount=count, blockCount=blocks, reverb=reverb,
                            sourcePlayer=int(player), priority=priority, midiOptions=config[1].split(),
                            sourceUseLines=[i for i, line in enumerate(outer.splitlines(), 1)
                                            if '(' + symbol + ')' in line]))
    need(len(cues) == 3, 'Empty or incomplete Teachy TV cue proof')
    manifest = dict(schemaVersion=1, sourceCommit=COMMIT, cues=[cues[0]])
    proof = dict(passed=True, sourceRepository='pret/pokefirered', sourceCommit=COMMIT,
                 sourceTree=TREE, sourceFiles=lock['inputs'], inheritedInputs=lock['inheritedInputs'],
                 cues=cues, songHeaders=headers, musicUses=music_uses,
                 menuAndTextSound='SE_SELECT', grassAnimationSound=None,
                 unusedMusicNotAdmitted='MUS_TEACHY_TV_SHOW', packRebuilt=False,
                 recordingsWritten=False, audibleOutputVerified=False)
    return {'synth-manifest.json': encode(manifest), 'source-proof.json': encode(proof)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--dependency', required=True, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output = generate(args.reference, args.dependency, Path(__file__).with_name('teachy-tv-audio.lock.json'))
    for name, data in output.items():
        path = safe(args.output, OUTPUT + name)
        if args.check:
            need(path.read_bytes() == data, 'Generated Teachy TV audio differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(passed=True, checkOnly=args.check, sourceCommit=COMMIT,
                          outputs=[dict(path=OUTPUT + p, byteLength=len(b), sha256=sha(b)) for p, b in output.items()],
                          newCues=1, inheritedCues=2, packRebuilt=False, recordingsWritten=False)))
