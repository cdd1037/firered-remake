#!/usr/bin/env python3
"""Bind original SE_M_FLY to the existing authenticated synth pack; no PCM output."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

import argparse,  importlib.util, json, re, struct
from pathlib import Path

ROOT = _PROJECT_ROOT
spec = importlib.util.spec_from_file_location('source_sound_helpers', ROOT/'scripts/imports/audio/import-vending-sound.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
need,  safe, git = helpers.need,  helpers.safe, helpers.git

def run(root, dependency, reference, check):
    locked = safe(root, 'scripts/recipes/audio/field-fly-sound.lock.json').read_bytes()
    lock = json.loads(locked)
    need((lock['schemaVersion'], lock['commit'], lock['profile']) == (1, helpers.COMMIT, 'firered-field-fly-sound-v1'), 'Fly sound profile differs')
    source = {}
    for row in lock['files']:
        data = git(reference, 'show', helpers.COMMIT+':'+row['path'])
        source[row['path']] = data
    need(len(source) == 5, 'Fly sound source scope differs')
    resource = lock['resource']; pack = safe(dependency, resource['path']).read_bytes()
    need(len(pack) == 2565436, 'Existing pack differs')
    need(resource['baseAddress'] == 0x08489968 and resource['songTableAddress'] == 0x084A32CC and
         resource['endAddress'] == 0x086FBEA4, 'Pack addresses differ')
    def offset(address, count):
        need(count > 0 and resource['baseAddress'] <= address and address+count <= resource['endAddress'], 'Fly audio range')
        return address-resource['baseAddress']
    need(re.search(r'^#define SE_M_FLY\s+151\b', source['include/constants/songs.h'].decode(), re.M), 'Original Fly song ID')
    table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source['sound/song_table.inc'].decode(), re.M)
    need(table[151] == ('se_m_fly', '1', '1'), 'Original Fly song table')
    config = re.search(r'^se_m_fly\.mid:\s+(.+)$', source['sound/songs/midi/midi.cfg'].decode(), re.M)
    need(config and config[1].split() == ['-E','-R50','-G128','-V110','-P4'], 'Original Fly build options')
    need(source['sound/songs/midi/se_m_fly.mid'][:8] == b'MThd\x00\x00\x00\x06', 'Original Fly MIDI')
    need(re.search(r'PlaySE\(SE_M_FLY\);\s*StartFlyBirdSwoopDown\(task->tBirdSpriteId\);',
                   source['src/field_effect.c'].decode()), 'Original departure swoop sound order')
    address, player, secondary = struct.unpack_from('<IHH', pack, offset(resource['songTableAddress']+151*8, 8))
    need((address,player,secondary) == (0x086B89CC,1,1), 'Packed Fly song entry')
    header = struct.unpack_from('<BBBBI', pack, offset(address,8))
    need(header == (2,0,4,178,0x0848C314), 'Packed Fly header')
    tracks = struct.unpack_from('<II', pack, offset(address+8,8))
    need(tracks == (0x086B8878,0x086B896C), 'Packed Fly tracks')
    for track in tracks: offset(track,1)
    offset(header[4],12)
    cue = dict(id='SE_M_FLY', sourceSymbol='SE_M_FLY', sourcePlayer=1, priority=4, sourceCounter=None, songAddress=address, species=0, looping=False, cryMode=0)
    manifest = runtime_manifest_bytes(dict(schemaVersion=1, cues=[cue]))
    outputs = {'content/generated/audio/cues/field-fly.json': manifest}
    for path,data in outputs.items():
        target = safe(root,path)
        if check: need(target.read_bytes() == data, 'Fly sound output differs: '+path)
        else: target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    return dict(passed=True,songId=151,songAddress=hex(address),sourcePlayer=1,tracks=[hex(t) for t in tracks],
                packRebuilt=False,recordingsWritten=False,audibleOutputVerified=False,
                outputs=[dict(path=p) for p,b in outputs.items()])

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root',type=Path,default=ROOT)
    parser.add_argument('--dependency-root',type=Path)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--check',action='store_true');args=parser.parse_args()
    print(json.dumps(run(args.project_root,args.dependency_root or args.project_root,args.reference,args.check),indent=2))
