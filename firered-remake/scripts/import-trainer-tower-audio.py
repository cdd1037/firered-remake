#!/usr/bin/env python3
"""Authenticate local Tower cries/BGM against pinned source and the unchanged shared synth pack."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import struct
import subprocess
import wave

COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-trainer-tower-audio-v1'
BASE='content/generated/trainer-tower-audio/'
RAW='content/raw/trainer-tower-audio/'
SPECIES=['POLIWRATH','VAPOREON','FLAREON','FERALIGATR','LEDIAN','TOGEPI','AMPHAROS','POLITOED','ESPEON',
    'DUNSPARCE','GRANBULL','QWILFISH','CORSOLA','MANTINE','PORYGON2','SMOOCHUM','MAGBY','BLISSEY']
DEPENDENCIES={
    'content/generated/synth-audio/firered.pack':'00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d',
    'content/generated/synth-audio/normal-cries.bin':'623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e',
    'content/generated/synth-audio/manifest.json':'10906e43fa80b13eb5d54177a93003df3fae5292f5da3a22b5805ff3e21220df',
    'content/generated/celadon-approach-synth-audio/manifest.json':'783f665e6660198e64eb3ea04510d138072b448a63d1581d393b47ef1a6bb5b2',
}
def need(value,message):
    if not value:raise ValueError(message)
def sha(data):return hashlib.sha256(data).hexdigest()
def encode(value):return (json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode()
def build(reference,dependency):
    need(subprocess.check_output(['git','rev-parse',COMMIT+'^{tree}'],cwd=reference).decode().strip()==TREE,'Pinned tree')
    raw,inputs,dependencies={},{},[]
    def read(path):
        if path not in raw:
            b=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference);raw[path]=b
            inputs[path]=dict(path=path,byteLength=len(b),sha256=sha(b),gitBlobSha1=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest())
        return raw[path]
    def text(path):return read(path).decode()
    def reuse(path):
        b=(dependency/path).read_bytes();need(sha(b)==DEPENDENCIES[path],'Inherited audio differs: '+path)
        dependencies.append(dict(path=path,byteLength=len(b),sha256=sha(b)));return b
    pack=reuse('content/generated/synth-audio/firered.pack');cries=reuse('content/generated/synth-audio/normal-cries.bin')
    old=[]
    for p in ['content/generated/synth-audio/manifest.json','content/generated/celadon-approach-synth-audio/manifest.json']:old.extend(json.loads(reuse(p))['cues'])
    need(len(pack)==0x086fbea4-0x08489968 and len(cries)==6188,'Shared pack dimensions')
    constants=text('include/constants/species.h')
    cry_table=re.findall(r'^\s*cry Cry_(\w+)\s*$',text('sound/cry_tables.inc').split('gCryTable::',1)[1].split('gCryTable_Reverse::',1)[0],re.M)
    cry_rows={struct.unpack_from('<H',cries,offset)[0]:struct.unpack_from('<HHIIB3x',cries,offset) for offset in range(12,len(cries),16)}
    priority=int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)',text('include/constants/sound.h'),re.M)[1]);need(priority==10,'Normal cry priority')
    new,inherited,evidence=[],[],[]
    for symbol in SPECIES:
        species=int(re.search(r'^#define SPECIES_'+symbol+r'\s+(\d+)\s*$',constants,re.M)[1]);row=cry_rows[species]
        name=cry_table[row[1]];need(name.upper()==symbol and row[1]==species-1,'Exact species cry index')
        tone=0x0848c914+row[1]*12-0x08489968;wave_offset=struct.unpack_from('<I',pack,tone+4)[0]-0x08489968
        need(struct.unpack_from('<HHIII',pack,wave_offset)==(1,0,10764288,0,row[2]),'Packed cry header')
        path='sound/direct_sound_samples/cries/'+name.lower()+'.wav'
        with wave.open(io.BytesIO(read(path))) as original:
            need((original.getnchannels(),original.getsampwidth(),original.getframerate(),original.getnframes(),original.getcomptype())==(1,1,10512,row[2]+1,'NONE'),'Source WAV format')
            pcm=original.readframes(original.getnframes())
        delta=(0,1,4,9,16,25,36,49,-64,-49,-36,-25,-16,-9,-4,-1);decoded=bytearray()
        for index in range(row[2]+row[4]):
            block,i=divmod(index,64);start=wave_offset+16+block*33
            if i==0:value=pack[start]
            else:
                packed=pack[start+(1 if i==1 else i//2+1)];nibble=packed&15 if i==1 or i%2 else packed>>4
                value=(value+delta[nibble])&255
            decoded.append((value+128)&255)
        need(decoded==pcm[:len(decoded)],'Original source PCM differs: '+name)
        evidence.append(dict(species=species,symbol='SPECIES_'+symbol,cryIndex=row[1],sampleCount=row[2],payloadBytes=row[3],
            guardStored=bool(row[4]),decodedBytes=len(decoded),decodedSha256=sha(decoded),sourcePath=path))
        cue=dict(id='CRY_'+symbol+'_NORMAL',sourceCommit=COMMIT,sourceSymbol='SPECIES_'+symbol,sourcePath=path,
            sourcePlayer=4,priority=10,sourceCounter=None,songAddress=0,species=species,looping=False,cryMode=0)
        matches=[c for c in old if c['id']==cue['id']]
        if symbol in ['POLIWRATH','VAPOREON','FLAREON']:
            need(len(matches)==1 and dict(matches[0],cryMode=matches[0].get('cryMode',0))==cue,'Inherited cry source');inherited.append(cue)
        else:
            need(not matches,'New cry overlap');new.append(cue)
    songs=text('include/constants/songs.h');symbol='MUS_TRAINER_TOWER'
    song_id=int(re.search(r'^#define '+symbol+r'\s+(\d+)',songs,re.M)[1])
    song_table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',text('sound/song_table.inc'),re.M)
    address,player,second=struct.unpack_from('<IHH',pack,0x084A32CC+song_id*8-0x08489968)
    need(song_table[song_id]==(symbol.lower(),str(player),str(second)) and player==0,'Tower song table binding')
    path='sound/songs/midi/mus_trainer_tower.mid';read(path)
    new.append(dict(id=symbol,sourceCommit=COMMIT,sourceSymbol=symbol,sourcePath=path,sourcePlayer=player,
        priority=pack[address-0x08489968+2],sourceCounter=None,songAddress=address,species=0,looping=True,cryMode=0))
    need(len(new)==16 and len(inherited)==3 and len(evidence)==18,'Exact added audio scope')
    synth=encode(dict(schemaVersion=1,sourceCommit=COMMIT,cues=new,inheritedCues=inherited))
    manifest=encode(dict(schemaVersion=1,sourceCommit=COMMIT,sourceTree=TREE,profile=PROFILE,
        originalPackUnchanged=True,newAudioAssets=0,physicalAudioVerified=False,additionalCueCount=16,inheritedCueCount=3,
        additionalCueIds=[c['id'] for c in new],inheritedCueIds=[c['id'] for c in inherited],cries=evidence,
        inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=dependencies,
        synthManifest=dict(path=BASE+'synth-manifest.json',byteLength=len(synth),sha256=sha(synth))))
    files={BASE+'manifest.json':manifest,BASE+'synth-manifest.json':synth,**{RAW+p:b for p,b in raw.items()}}
    lock=encode(dict(sourceCommit=COMMIT,sourceTree=TREE,profile=PROFILE,
        files=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(files.items())]))
    files['scripts/trainer-tower-audio.lock.json']=lock
    return files,dict(sourceInputs=len(inputs),criesVerified=18,decodedSamples=sum(e['decodedBytes'] for e in evidence),newCues=16,inheritedCues=3,
        synthBytes=len(synth),synthSha256=sha(synth),manifestSha256=sha(manifest),lockSha256=sha(lock))
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path);p.add_argument('output',type=Path)
    p.add_argument('--dependency-root',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    files,result=build(a.reference,a.dependency_root or a.output)
    for path,data in files.items():
        destination=a.output/path
        if a.check:need(destination.read_bytes()==data,'Regeneration differs: '+path)
        else:destination.parent.mkdir(parents=True,exist_ok=True);destination.write_bytes(data)
    print(json.dumps(dict(checked=a.check,**result),indent=2))
if __name__=='__main__':main()
