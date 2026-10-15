#!/usr/bin/env python3
"""Register two source animation sounds from the original synth pack."""
import argparse,json,re,struct,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def build(reference):
    lock_data=(ROOT/'scripts/source-explosion-synth.lock.json').read_bytes()
    lock=json.loads(lock_data);raw={}
    if lock['sourceCommit']!=COMMIT:raise ValueError('Explosion synth revision differs')
    for row in lock['files']:
        path=row['path'];data=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+path])
        raw[path]=data
    row=lock['pack'];path=ROOT/row['path']
    pack=path.read_bytes()
    constants={k:int(v) for k,v in re.findall(r'^#define (SE_\w+)\s+(\d+)',raw['include/constants/songs.h'].decode(),re.M)}
    table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',raw['sound/song_table.inc'].decode(),re.M)
    cues=[]
    for cue in ['SE_M_SELF_DESTRUCT','SE_M_EXPLOSION']:
        sid=constants[cue];address,player,secondary=struct.unpack_from('<IHH',pack,0x084A32CC+sid*8-0x08489968)
        if table[sid]!=(cue.lower(),str(player),str(secondary)) or player not in (1,2):raise ValueError('Source SE player/table differs')
        path='sound/songs/midi/'+cue.lower()+'.mid'
        if raw[path][:4]!=b'MThd':raise ValueError('Missing locked original MIDI')
        cues.append(dict(id=cue,sourceCommit=COMMIT,sourceSymbol=cue,sourcePath=path,sourcePlayer=player,priority=pack[address-0x08489968+2],sourceCounter=None,songAddress=address,species=0,looping=False))
    return (json.dumps(dict(schemaVersion=1,sourceCommit=COMMIT,cues=cues),indent=2)+'\n').encode()
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data=build(a.reference)
    output=ROOT/'content/generated/source-explosion-animations/synth-manifest.json'
    if a.check:
        if output.read_bytes()!=data:raise SystemExit('Explosion synth manifest differs')
    else:output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(data)
    print(json.dumps(dict(passed=True, bytes=len(data), cues=2, packUnchanged=True)))
