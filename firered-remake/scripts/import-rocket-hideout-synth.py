#!/usr/bin/env python3
"""Authenticate one ordinary cry, two map songs and four missing SE; no recorded output."""
import argparse,importlib.util,json,pathlib,re,struct
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
NEW_CRIES={115:'KANGASKHAN'}
NEW_EFFECTS={'SE_DING_DONG','SE_ELEVATOR','SE_M_METRONOME','SE_M_REVERSAL'}
NEW_SONGS={'MUS_GAME_CORNER','MUS_ROCKET_HIDEOUT'}
NEW_IDS=NEW_EFFECTS|NEW_SONGS|{'CRY_'+n+'_NORMAL' for n in NEW_CRIES.values()}
PRIOR=['synth-audio','route11-synth-audio','rocktunnel-synth-audio','celadon-approach-synth-audio','celadon-gym-synth-audio']
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(base,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe synth path')
 q=pathlib.Path(base)/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Synth symlink rejected')
 return q

def authenticated_inputs(root=ROOT,output_root=None):
 out=output_root or root;b=safe(out,'scripts/rocket-hideout-synth.lock.json').read_bytes()
 lock=json.loads(b)
 if (lock['schemaVersion'], lock['repository'], lock['commit']) != (1, 'pret/pokefirered', COMMIT):raise ValueError('Rocket synth source inventory differs')
 source={};inputs={}
 for entries,base,result in [(lock['files'],safe(out,'content/raw/pokefirered/037335f4'),source),(lock['inputs'],root,inputs)]:
  for e in entries:
   b=safe(base,e['path']).read_bytes()
   if e['path'] in result:raise ValueError('Duplicate synth dependency')
   result[e['path']]=b
 return inputs,source

def derive(inputs,source):
 old={}
 for stem,count in zip(PRIOR,[246,7,15,3,8]):
  d=json.loads(inputs['content/generated/'+stem+'/manifest.json'])
  if d['schemaVersion']!=1 or d['sourceCommit']!=COMMIT or len(d['cues'])!=count:raise ValueError('Prior synth manifest differs')
  for c in d['cues']:
   if c['id'] in old:raise ValueError('Historical cue collision')
   old[c['id']]=c
 if len(old)!=279 or set(old)&NEW_IDS:raise ValueError('Historical279 cue scope differs')
 animations=source['data/battle_anim_scripts.s'].decode();blocks={m[1]:m[2] for m in re.finditer(r'^(\w+):+\n(.*?)(?=^\w+:|\Z)',animations,re.M|re.S)}
 visited=set();effects=set()
 def walk(label):
  if label in visited:return
  visited.add(label);body=blocks[label];effects.update(re.findall(r'\bSE_\w+',body))
  for target in re.findall(r'^\s*call (\w+)',body,re.M):walk(target)
 for move in ['MEGA_PUNCH','COMET_PUNCH','DIZZY_PUNCH','REVERSAL','FAKE_OUT','TAUNT','FRUSTRATION','SNATCH']:walk('Move_'+move)
 required=set(effects)|{'SE_ELEVATOR','SE_DING_DONG','SE_M_RAZOR_WIND2'}
 for name in ['CeladonCity_GameCorner','RocketHideout_B1F','RocketHideout_B2F','RocketHideout_B3F','RocketHideout_B4F','RocketHideout_Elevator']:
  m=json.loads(source['data/maps/'+name+'/map.json']);required.add(m['music']);required.update(re.findall(r'\b(?:SE|MUS)_\w+',source['data/maps/'+name+'/scripts.inc'].decode()))
 if required-set(old)!=NEW_EFFECTS|NEW_SONGS:raise ValueError('Rocket map/animation sound closure differs: '+str(required-set(old)))
 if not {'MUS_VS_TRAINER','MUS_VICTORY_TRAINER','MUS_ENCOUNTER_ROCKET','SE_UNLOCK','SE_M_RAZOR_WIND2'}<=set(old):raise ValueError('Required inherited Rocket cues missing')
 spec=importlib.util.spec_from_file_location('rocket_audio_audit',ROOT/'scripts/audit-early-audio.py');audit=importlib.util.module_from_spec(spec);exec(compile(inputs['scripts/audit-early-audio.py'],'locked-audit-early-audio.py','exec'),audit.__dict__)
 pack=inputs['content/generated/synth-audio/firered.pack'];constants={n:int(v) for n,v in re.findall(r'^#define ((?:MUS|SE)_\w+)\s+(\d+)',source['include/constants/songs.h'].decode(),re.M)}
 table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',source['sound/song_table.inc'].decode(),re.M);cues=[]
 for name in sorted(NEW_EFFECTS|NEW_SONGS):
  sid=constants[name];address,player,second=struct.unpack_from('<IHH',pack,0x084A32CC+sid*8-0x08489968)
  if table[sid]!=(name.lower(),str(player),str(second)) or player not in ((0,) if name in NEW_SONGS else (1,2,3)):raise ValueError('SE source player differs')
  path='sound/songs/midi/'+name.lower()+'.mid';midi=audit.midi(source[path])
  looping=any(m['text'] in ('[',']') for m in midi['markers'])
  if looping!=(name in NEW_SONGS):raise ValueError('Source loop marker differs')
  cues.append(dict(id=name,sourceCommit=COMMIT,sourceSymbol=name,sourcePath=path,sourcePlayer=player,priority=pack[address-0x08489968+2],sourceCounter=None,songAddress=address,species=0,looping=looping))
 species={n:int(v) for n,v in re.findall(r'^#define SPECIES_(\w+)\s+(\d+)',source['include/constants/species.h'].decode(),re.M)}
 names=re.findall(r'^\s*cry Cry_(\w+)\s*$',source['sound/cry_tables.inc'].decode(),re.M)
 priority=int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)',source['include/constants/sound.h'].decode(),re.M)[1])
 cries=inputs['content/generated/synth-audio/normal-cries.bin']
 if cries[:8]!=b'FRCN0001' or len(cries)!=6188 or priority!=10:raise ValueError('Cry table/priority differs')
 rows={struct.unpack_from('<H',cries,o)[0]:struct.unpack_from('<HHIIB3x',cries,o) for o in range(12,len(cries),16)}
 for sid,name in NEW_CRIES.items():
  row=rows[sid];path='sound/direct_sound_samples/cries/'+name.lower()+'.wav';wav,pcm=audit.wav(source[path])
  if species[name]!=sid or names[sid-1].upper()!=name or row[1]!=sid-1 or wav['effectiveEndExclusive']!=row[2] or wav['sampleCount']!=row[2]+1 or wav['loopEnabled'] or wav['agbPitch']!=10764288:raise ValueError('Cry source identity differs: '+name)
  tone=0x0848c914+row[1]*12-0x08489968;wave=struct.unpack_from('<I',pack,tone+4)[0]-0x08489968
  if struct.unpack_from('<HHIII',pack,wave)!=(1,0,10764288,0,row[2]):raise ValueError('Cry sample header differs')
  delta=(0,1,4,9,16,25,36,49,-64,-49,-36,-25,-16,-9,-4,-1);decoded=bytearray()
  for index in range(row[2]+row[4]):
   block,i=divmod(index,64);start=wave+16+block*33
   if i==0:value=pack[start]
   else:
    packed=pack[start+(1 if i==1 else i//2+1)];nibble=packed&15 if i==1 or i%2 else packed>>4;value=(value+delta[nibble])&255
   decoded.append((value+128)&255)
  if decoded!=pcm[:len(decoded)]:raise ValueError('Cry pack samples differ: '+name)
  cues.append(dict(id='CRY_'+name+'_NORMAL',sourceCommit=COMMIT,sourceSymbol='SPECIES_'+name,sourcePath=path,sourcePlayer=4,priority=10,sourceCounter=None,songAddress=0,species=sid,looping=False))
 cues=sorted(cues,key=lambda c:c['id'])
 if len(cues)!=7 or {c['id'] for c in cues}!=NEW_IDS:raise ValueError('New cue inventory differs')
 return encode(dict(schemaVersion=1,sourceCommit=COMMIT,cues=cues)),dict(sourceAnimationLabels=sorted(visited),requiredMoveEffects=sorted(effects),requiredRocketCues=sorted(required),inheritedRocketBattleMusic=True)

def run(root=ROOT,output_root=None,check=False):
 out=output_root or root;inputs,source=authenticated_inputs(root,out);data,evidence=derive(inputs,source)
 for relative,b in [('content/generated/rocket-hideout-synth-audio/manifest.json', data)]:
  p=safe(out,relative)
  if check:
   if p.read_bytes()!=b:raise ValueError('Generated synth differs: '+relative)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,supplementCues=7,supplementNormalCries=1,mergedCues=286,mergedNormalCries=105,manifestBytes=len(data),prior279BytesUnchanged=True,samplePackAndCryTableUnchanged=True,audioCoreUnchanged=True,audioRendered=False,**evidence)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--output-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.output_root,a.check),indent=2))
