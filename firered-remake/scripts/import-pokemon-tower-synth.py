#!/usr/bin/env python3
"""Authenticate Tower sequence/cry additions against the unchanged custom synth pack."""
import argparse,importlib.util,json,pathlib,re,struct,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1];COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
NEW_CRIES={92:'GASTLY',93:'HAUNTER',54:'PSYDUCK'}
NEW_EFFECTS={'SE_M_NIGHTMARE','SE_M_LICK','SE_M_BONEMERANG','SE_M_MIST','SE_M_MINIMIZE','SE_ITEMFINDER'}
NEW_SONGS={'MUS_POKE_TOWER','MUS_POKE_FLUTE'}
NEW_IDS=NEW_EFFECTS|NEW_SONGS|{'CRY_'+n+'_NORMAL' for n in NEW_CRIES.values()}
PRIOR=['synth-audio','route11-synth-audio','rocktunnel-synth-audio','celadon-approach-synth-audio','celadon-gym-synth-audio','rocket-hideout-synth-audio']
MOVES=['BONEMERANG','CONFUSE_RAY','CURSE','DESTINY_BOND','DREAM_EATER','HYPNOSIS','LICK','MEAN_LOOK','NIGHTMARE','NIGHT_SHADE','SHADOW_BALL','SHADOW_PUNCH','SPITE']
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe Tower synth path')
 q=root/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Tower synth symlink rejected')
 return q
def resolve(roots,p):
 for root in roots:
  q=safe(root,p)
  if q.exists():return q
 raise FileNotFoundError(p)
def authenticated(roots):
 raw=safe(roots[0],'scripts/pokemon-tower-synth.lock.json').read_bytes()
 lock=json.loads(raw)
 if (lock['schemaVersion'], lock['repository'], lock['commit']) != (1, 'pret/pokefirered', COMMIT):raise ValueError('Tower synth inventory differs')
 source={};inputs={}
 for entries,target in [(lock['files'],source),(lock['inputs'],inputs)]:
  for row in entries:
   b=resolve(roots,row.get('localPath',row['path'])).read_bytes()
   target[row['path']]=b
 return inputs,source

def derive(inputs,source):
 old={}
 for stem,count in zip(PRIOR,[246,7,15,3,8,7]):
  d=json.loads(inputs['content/generated/'+stem+'/manifest.json'])
  if d['schemaVersion']!=1 or d['sourceCommit']!=COMMIT or len(d['cues'])!=count:raise ValueError('Prior synth manifest differs')
  for cue in d['cues']:
   if cue['id'] in old:raise ValueError('Historical cue collision')
   old[cue['id']]=cue
 if len(old)!=286 or set(old)&NEW_IDS:raise ValueError('Historical 286 cue scope differs')
 animations=source['data/battle_anim_scripts.s'].decode();blocks={m[1]:m[2] for m in re.finditer(r'^(\w+):+\n(.*?)(?=^\w+:|\Z)',animations,re.M|re.S)};visited=set();effects=set()
 def walk(label):
  if label in visited:return
  visited.add(label);body=blocks[label];effects.update(re.findall(r'\bSE_\w+',body))
  for target in re.findall(r'^\s*call (\w+)',body,re.M):walk(target)
 for move in MOVES:walk('Move_'+move)
 for label in ['General_SilphScoped','General_GhostGetOut','General_MonScared']:walk(label)
 required=set(effects)|{'SE_ITEMFINDER','MUS_POKE_FLUTE','MUS_ENCOUNTER_ROCKET','MUS_VS_TRAINER','MUS_VS_WILD','MUS_VICTORY_TRAINER','MUS_VICTORY_WILD','MUS_OBTAIN_ITEM','MUS_OBTAIN_KEY_ITEM','MUS_LAVENDER'}
 for floor in range(1,8):
  name=f'PokemonTower_{floor}F';m=json.loads(source['data/maps/'+name+'/map.json']);required.add(m['music']);required.update(re.findall(r'\b(?:SE|MUS)_\w+',source['data/maps/'+name+'/scripts.inc'].decode()))
 required.update(re.findall(r'\b(?:SE|MUS)_\w+',source['data/maps/LavenderTown_VolunteerPokemonHouse/scripts.inc'].decode()))
 if required-set(old)!=NEW_EFFECTS|NEW_SONGS:raise ValueError('Tower sound closure differs: '+str(required-set(old)))
 if 'PlaySE(SE_ITEMFINDER)' not in source['src/itemfinder.c'].decode():raise ValueError('Itemfinder source sound differs')
 counter=re.search(r'\[FANFARE_POKE_FLUTE\]\s*=\s*\{\s*MUS_POKE_FLUTE,\s*(\d+)',source['src/sound.c'].decode())
 if counter is None or int(counter[1])!=450:raise ValueError('Flute source fanfare counter differs')
 spec=importlib.util.spec_from_file_location('tower_audio_audit',ROOT/'scripts/audit-early-audio.py');audit=importlib.util.module_from_spec(spec);exec(compile(inputs['scripts/audit-early-audio.py'],'locked-audit-early-audio.py','exec'),audit.__dict__)
 pack=inputs['content/generated/synth-audio/firered.pack'];constants={n:int(v) for n,v in re.findall(r'^#define ((?:MUS|SE)_\w+)\s+(\d+)',source['include/constants/songs.h'].decode(),re.M)}
 table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',source['sound/song_table.inc'].decode(),re.M);cues=[];numeric_ids={}
 for name in sorted(NEW_EFFECTS|NEW_SONGS):
  sid=constants[name];numeric_ids[name]=sid;address,player,second=struct.unpack_from('<IHH',pack,0x084A32CC+sid*8-0x08489968)
  if table[sid]!=(name.lower(),str(player),str(second)) or player not in range(4):raise ValueError('Tower source player differs')
  path='sound/songs/midi/'+name.lower()+'.mid';midi=audit.midi(source[path]);looping=any(m['text'] in ('[',']') for m in midi['markers'])
  if looping!=(name=='MUS_POKE_TOWER'):raise ValueError('Tower loop marker differs')
  cues.append(dict(id=name,sourceCommit=COMMIT,sourceSymbol=name,sourcePath=path,sourcePlayer=player,priority=pack[address-0x08489968+2],sourceCounter=450 if name=='MUS_POKE_FLUTE' else None,songAddress=address,species=0,looping=looping))
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
 if len(cues)!=11 or {c['id'] for c in cues}!=NEW_IDS:raise ValueError('New cue inventory differs')
 return encode(dict(schemaVersion=1,sourceCommit=COMMIT,cues=cues)),dict(sourceAnimationLabels=sorted(visited),requiredMoveEffects=sorted(effects),requiredTowerCues=sorted(required),sourceNumericIds=numeric_ids)

def run(root=ROOT,dependencies=(),check=False):
 inputs,source=authenticated([root,*dependencies]);data,evidence=derive(inputs,source)
 for rel,b in [('content/generated/pokemon-tower-synth-audio/manifest.json', data)]:
  p=safe(root,rel)
  if check:
   if p.read_bytes()!=b:raise ValueError('Tower synth output differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,newCues=11,newCries=3,mergedCues=297,mergedCries=108,manifestBytes=len(data),historical286CuesUnchanged=True,samplePackAndCryTableUnchanged=True,audioCoreUnchanged=True,renderedRecordingsWritten=False,**evidence)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',action='append',type=pathlib.Path,default=[]);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.dependency_root,a.check),indent=2))
