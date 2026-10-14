#!/usr/bin/env python3
"""Authenticate one finite fanfare in the existing pack; emit only cue metadata."""
import argparse
import hashlib
import json
import os
import re
import struct
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = 'content/generated/move-deleter-audio/'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
LOCK_SHA = '5e8587b4293f83aad1aa1ae297d1a2495820a4dad0b98b816bc820aa09608192'
PACK_SHA = '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d'


def need(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()


def safe(root, relative):
    need(relative and not Path(relative).is_absolute() and '\\' not in relative and
         ':' not in relative and not any(p in ('', '.', '..') for p in relative.split('/')),
         'Unsafe Move Deleter resource path')
    path = root / relative
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))
    return path


def git(reference, *args):
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull,
               GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')
    return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks',
                                   '-C', str(reference), *args], env=env)


def run(root, dependency, reference, check):
    lock_bytes = safe(root, 'scripts/move-deleter-audio.lock.json').read_bytes()
    need(len(lock_bytes) == 2038 and sha(lock_bytes) == LOCK_SHA, 'Move Deleter source lock differs')
    lock = json.loads(lock_bytes)
    need((lock['schemaVersion'], lock['repository'], lock['commit'], lock['tree'], len(lock['files'])) ==
         (1, 'pret/pokefirered', COMMIT, TREE, 5), 'Move Deleter source scope differs')
    if reference:
        need(git(reference, 'rev-parse', COMMIT + '^{tree}').decode().strip() == TREE,
             'Pinned reference tree differs')
    source = {}
    for entry in lock['files']:
        if reference:
            listing = git(reference, 'ls-tree', '-z', COMMIT, '--', entry['path'])
            expected = ('100644 blob ' + entry['gitBlobSha1'] + '\t' + entry['path'] + '\0').encode()
            need(listing == expected, 'Pinned source object differs: ' + entry['path'])
            data = git(reference, 'cat-file', 'blob', entry['gitBlobSha1'])
        else:
            data = safe(root, entry['rawPath']).read_bytes()
        need((len(data), sha(data), hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest()) ==
             (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']),
             'Source bytes differ: ' + entry['path'])
        source[entry['path']] = data

    resource = lock['resource']
    pack = safe(dependency, resource['path']).read_bytes()
    need((len(pack), sha(pack)) == (resource['byteLength'], PACK_SHA) and resource['sha256'] == PACK_SHA,
         'Existing synth pack identity differs')
    need(resource['baseAddress'] == 0x08489968 and resource['endAddress'] == 0x086FBEA4 and
         resource['songTableAddress'] == 0x084A32CC and len(pack) == resource['endAddress'] - resource['baseAddress'],
         'Existing pack address contract differs')

    def offset(address, length):
        need(length > 0 and resource['baseAddress'] <= address and address + length <= resource['endAddress'],
             'Move Deleter audio address exceeds the authenticated pack')
        return address - resource['baseAddress']

    constants = source['include/constants/songs.h'].decode()
    song_id = re.search(r'^#define MUS_MOVE_DELETED\s+(\d+)\b', constants, re.M)
    need(song_id is not None and int(song_id[1]) == 270, 'Original song ID differs')
    table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source['sound/song_table.inc'].decode(), re.M)
    need(len(table) > 270 and table[270] == ('mus_move_deleted', '2', '2'), 'Source song table entry differs')
    counter = re.search(r'\[FANFARE_MOVE_DELETED\]\s*=\s*\{\s*MUS_MOVE_DELETED,\s*(\d+)\s*\}',
                        source['src/sound.c'].decode())
    need(counter is not None and int(counter[1]) == 180, 'Original fanfare counter differs')
    config = re.search(r'^mus_move_deleted\.mid:\s+(.+)$', source['sound/songs/midi/midi.cfg'].decode(), re.M)
    need(config is not None and config[1].split() == ['-E', '-R50', '-G008', '-V090', '-P5'],
         'Source MIDI build options differ')
    midi = source['sound/songs/midi/mus_move_deleted.mid']
    need(midi[:8] == b'MThd\x00\x00\x00\x06', 'Source MIDI header differs')
    address, player, secondary = struct.unpack_from('<IHH', pack, offset(resource['songTableAddress'] + 270 * 8, 8))
    need((address, player, secondary) == (0x086C0204, 2, 2), 'Packed song table entry differs')
    count, blocks, priority, reverb, voices = struct.unpack_from('<BBBBI', pack, offset(address, 8))
    need((count, blocks, priority, reverb, voices) == (6, 0, 5, 178, 0x0848ABB0), 'Packed song header differs')
    tracks = list(struct.unpack_from('<6I', pack, offset(address + 8, 24)))
    need(tracks == [0x086C0144, 0x086C016B, 0x086C0191, 0x086C01B1, 0x086C01D1, 0x086C01EB],
         'Packed track pointers differ')
    offset(voices, 12)
    for track in tracks:
        offset(track, 1)

    cue = dict(id='MUS_MOVE_DELETED', sourceCommit=COMMIT, sourceSymbol='MUS_MOVE_DELETED',
               sourcePath='sound/songs/midi/mus_move_deleted.mid', sourcePlayer=player,
               priority=priority, sourceCounter=int(counter[1]), songAddress=address,
               species=0, looping=False, cryMode=0)
    manifest = encode(dict(schemaVersion=1, sourceCommit=COMMIT, cues=[cue]))
    identity = ('''// Generated by scripts/import-move-deleter-audio.py; one cue, existing pack.
internal static class MoveDeleterSynthManifestIdentity
{
    public const string Path = "content/generated/move-deleter-audio/synth-manifest.json";
    public const string SourceCommit = "%s";
    public const string Sha256 = "%s";
    public const int Bytes = %d;
}
''' % (COMMIT, sha(manifest), len(manifest))).encode()
    outputs = {BASE + 'synth-manifest.json': manifest,
               'src/Game.Godot/MoveDeleterSynthManifestIdentity.cs': identity}
    for path, data in outputs.items():
        destination = safe(root, path)
        if check:
            need(destination.read_bytes() == data, 'Generated Move Deleter output differs: ' + path)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    if reference and not check:
        for entry in lock['files']:
            destination = safe(root, entry['rawPath'])
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(source[entry['path']])
    return dict(passed=True, sourceCommit=COMMIT, sourceFiles=len(source), songId=270,
                sourceCounter=180, sourcePlayer=player, songAddress=hex(address), trackCount=count,
                trackAddresses=[hex(p) for p in tracks], voicegroupAddress=hex(voices),
                packSha256=PACK_SHA, packRebuilt=False, recordingsWritten=False, audibleOutputVerified=False,
                outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in outputs.items()])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--dependency-root', type=Path)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root, args.dependency_root or args.project_root, args.reference, args.check), indent=2))
