#!/usr/bin/env python3
"""Authenticate SE_VEND in the existing pack and emit its one-cue manifest."""
import argparse
import hashlib
import json
import os
import re
import struct
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
LOCK_SHA = 'ac5a9cb86d4b6812d23386bade7c66bc1f237ffd4e58810eff40bbd5ca0569c5'
PACK_SHA = '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d'


def need(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe(root, relative):
    need(relative and not Path(relative).is_absolute() and '\\' not in relative and
         ':' not in relative and not any(p in ('', '.', '..') for p in relative.split('/')),
         'Unsafe vending audio resource path')
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
    lock_bytes = safe(root, 'scripts/vending-sound.lock.json').read_bytes()
    need((len(lock_bytes), sha(lock_bytes)) == (1688, LOCK_SHA), 'Vending audio source lock differs')
    lock = json.loads(lock_bytes)
    need((lock['schemaVersion'], lock['repository'], lock['commit'], lock['tree'], len(lock['files'])) ==
         (1, 'pret/pokefirered', COMMIT, TREE, 5), 'Vending audio source scope differs')
    need(git(reference, 'rev-parse', COMMIT + '^{tree}').decode().strip() == TREE,
         'Pinned reference tree differs')
    source = {}
    for entry in lock['files']:
        data = git(reference, 'show', COMMIT + ':' + entry['path'])
        need((len(data), sha(data), hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest()) ==
             (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']),
             'Source bytes differ: ' + entry['path'])
        source[entry['path']] = data

    resource = lock['resource']
    pack_path = safe(dependency, resource['path'])
    need(pack_path.stat().st_size == resource['byteLength'] == 2565436, 'Synth pack length differs')
    pack = pack_path.read_bytes()
    need(sha(pack) == resource['sha256'] == PACK_SHA, 'Existing synth pack identity differs')
    need(resource['baseAddress'] == 0x08489968 and resource['endAddress'] == 0x086FBEA4 and
         resource['songTableAddress'] == 0x084A32CC,
         'Existing pack address contract differs')

    def offset(address, length):
        need(length > 0 and resource['baseAddress'] <= address and address + length <= resource['endAddress'],
             'Vending audio address exceeds the authenticated pack')
        return address - resource['baseAddress']

    song_id = re.search(r'^#define SE_VEND\s+(\d+)\b', source['include/constants/songs.h'].decode(), re.M)
    need(song_id is not None and int(song_id[1]) == 99, 'Original song ID differs')
    table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source['sound/song_table.inc'].decode(), re.M)
    need(len(table) > 99 and table[99] == ('se_vend', '1', '1'), 'Source song table entry differs')
    config = re.search(r'^se_vend\.mid:\s+(.+)$', source['sound/songs/midi/midi.cfg'].decode(), re.M)
    need(config is not None and config[1].split() == ['-E', '-R50', '-G128', '-V110', '-P4'],
         'Source MIDI build options differ')
    need(source['sound/songs/midi/se_vend.mid'][:8] == b'MThd\x00\x00\x00\x06', 'Source MIDI header differs')
    roof = source['data/maps/CeladonCity_DepartmentStore_Roof/scripts.inc'].decode()
    need(re.search(r'^\s*playse SE_VEND\s*\n\s*msgbox CeladonCity_DepartmentStore_Roof_Text_DrinkCanPoppedOut\s*\n\s*additem VAR_TEMP_0\b', roof, re.M) is not None,
         'Original rooftop vending sound/message/item order differs')
    address, player, secondary = struct.unpack_from('<IHH', pack, offset(resource['songTableAddress'] + 99 * 8, 8))
    need((address, player, secondary) == (0x086B7154, 1, 1), 'Packed song table entry differs')
    count, blocks, priority, reverb, voices = struct.unpack_from('<BBBBI', pack, offset(address, 8))
    need((count, blocks, priority, reverb, voices) == (1, 0, 4, 178, 0x0848C314), 'Packed song header differs')
    tracks = list(struct.unpack_from('<I', pack, offset(address + 8, 4)))
    need(tracks == [0x086B713C], 'Packed track pointer differs')
    offset(voices, 12)
    offset(tracks[0], 1)

    cue = dict(id='SE_VEND', sourceCommit=COMMIT, sourceSymbol='SE_VEND',
               sourcePath='sound/songs/midi/se_vend.mid', sourcePlayer=player,
               priority=priority, sourceCounter=None, songAddress=address,
               species=0, looping=False, cryMode=0)
    manifest = (json.dumps(dict(schemaVersion=1, sourceCommit=COMMIT, cues=[cue]),
                           sort_keys=True, separators=(',', ':')) + '\n').encode()
    identity = ('''// Generated by scripts/import-vending-sound.py; one cue, existing pack.
internal static class VendingSynthManifestIdentity
{
    public const string Path = "content/generated/vending-sound/synth-manifest.json";
    public const string SourceCommit = "%s";
    public const string Sha256 = "%s";
    public const int Bytes = %d;
}
''' % (COMMIT, sha(manifest), len(manifest))).encode()
    outputs = {'content/generated/vending-sound/synth-manifest.json': manifest,
               'src/Game.Godot/VendingSynthManifestIdentity.cs': identity}
    for relative, data in outputs.items():
        destination = safe(root, relative)
        if check:
            need(destination.read_bytes() == data, 'Generated vending output differs: ' + relative)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    return dict(passed=True, sourceCommit=COMMIT, sourceFiles=len(source), songId=99,
                sourceCounter=None, sourcePlayer=player, songAddress=hex(address), trackCount=count,
                trackAddresses=[hex(p) for p in tracks], voicegroupAddress=hex(voices),
                packSha256=PACK_SHA, packRebuilt=False, recordingsWritten=False,
                audibleOutputVerified=False, originalScriptWaitVerified=False,
                outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in outputs.items()])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--dependency-root', type=Path)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root, args.dependency_root or args.project_root, args.reference, args.check), indent=2))
