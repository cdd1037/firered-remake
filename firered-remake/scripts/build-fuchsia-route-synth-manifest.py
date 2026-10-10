#!/usr/bin/env python3
"""Add four source cry IDs to the immutable original sample pack; never render audio."""
import argparse, hashlib, importlib.util, json, re, struct, sys
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SPECIES = {83: 'FARFETCHD', 84: 'DODUO', 85: 'DODRIO', 132: 'DITTO'}
PACK_SHA = '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d'
CRY_SHA = '623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e'
def sha(data): return hashlib.sha256(data).hexdigest()
def build(root):
    spec = importlib.util.spec_from_file_location('fuchsia_metadata', ROOT / 'scripts/import-fuchsia-route-pokemon.py')
    meta = importlib.util.module_from_spec(spec); spec.loader.exec_module(meta)
    _, source = meta.authenticated(root)
    pack = meta.safe(root, 'content/generated/synth-audio/firered.pack').read_bytes()
    cry = meta.safe(root, 'content/generated/synth-audio/normal-cries.bin').read_bytes()
    if len(pack) != 0x086fbea4 - 0x08489968 or sha(pack) != PACK_SHA or len(cry) != 6188 or sha(cry) != CRY_SHA:
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
    identity=f'''// Four ID-only source cries; original sample pack and synthesis core remain unchanged.
internal static class FuchsiaRouteSynthManifestIdentity
{{
    public const string Sha256 = "{sha(data)}";
    public const int Bytes = {len(data)};
    public const int CueCount = 4;
    public const string SourceCommit = "{COMMIT}";
}}
'''.encode()
    return data,identity
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
    data,identity=build(a.project_root)
    for rel,value in [('content/generated/fuchsia-route-synth-audio/manifest.json',data),('src/Game.Godot/FuchsiaRouteSynthManifestIdentity.cs',identity)]:
        target=a.project_root/rel
        if a.check:
            if target.read_bytes()!=value:raise ValueError('Generated Fuchsia audio output differs: '+rel)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(value)
    print(json.dumps(dict(passed=True,newCues=4,sourceSpecies=list(SPECIES),manifestSha256=sha(data),originalPackUnchanged=True,renderedAudioWritten=False)))
