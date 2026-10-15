#!/usr/bin/env python3
"""Add three source cry IDs to the immutable original sample pack; never render audio."""
import argparse,  importlib.util, json, re, struct, sys
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SPECIES = {113: 'CHANSEY', 123: 'SCYTHER', 128: 'TAUROS'}
def build(root,dependency_root=None):
    spec = importlib.util.spec_from_file_location('safari_metadata', ROOT / 'scripts/import-safari-pokemon.py')
    meta = importlib.util.module_from_spec(spec); spec.loader.exec_module(meta)
    _, source = meta.authenticated(root)
    pack = meta.safe(dependency_root or root, 'content/generated/synth-audio/firered.pack').read_bytes()
    cry = meta.safe(dependency_root or root, 'content/generated/synth-audio/normal-cries.bin').read_bytes()
    if len(pack) != 141541028 - 138975592 or len(cry) != 6188:
        raise ValueError('Authenticated original cry resources differ')
    constants = {n:int(v) for n,v in re.findall(r'^#define SPECIES_(\w+)\s+(\d+)\s*$',source['include/constants/species.h'].decode(),re.M)}
    table = source['sound/cry_tables.inc'].decode().split('gCryTable::',1)[1].split('gCryTable_Reverse::',1)[0]
    names = re.findall(r'^\s*cry Cry_(\w+)\s*$',table,re.M)
    rows = {struct.unpack_from('<H',cry,o)[0]:struct.unpack_from('<HHIIB3x',cry,o) for o in range(12,len(cry),16)}
    cues=[]
    for sid,name in SPECIES.items():
        row=rows[sid]
        if constants[name] != sid or names[sid-1].upper() != name or row[1] != sid-1:
            raise ValueError('Source species/normal cry table differs: '+name)
        tone=0x0848c914+row[1]*12-0x08489968
        wave=struct.unpack_from('<I',pack,tone+4)[0]-0x08489968
        if struct.unpack_from('<HHIII',pack,wave)!=(1,0,10764288,0,row[2]):
            raise ValueError('Authenticated original cry sample header differs: '+name)
        cues.append(dict(id='CRY_'+name+'_NORMAL',sourceCommit=COMMIT,sourceSymbol='SPECIES_'+name,
            sourcePath='sound/direct_sound_samples/cries/'+name.lower()+'.wav',sourcePlayer=4,priority=10,
            sourceCounter=None,songAddress=0,species=sid,looping=False,cryMode=0))
    data=(json.dumps(dict(schemaVersion=1,sourceCommit=COMMIT,cues=sorted(cues,key=lambda c:c['id'])),indent=2)+'\n').encode()
    return data
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=Path,default=ROOT);p.add_argument('--dependency-root',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    data=build(a.project_root,a.dependency_root)
    for rel,value in [('content/generated/safari-synth-audio/manifest.json', data)]:
        target=a.project_root/rel
        if a.check:
            if target.read_bytes()!=value:raise ValueError('Generated Safari audio output differs: '+rel)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(value)
    print(json.dumps(dict(passed=True, newCues=3, sourceSpecies=list(SPECIES), originalPackUnchanged=True, renderedAudioWritten=False)))
