#!/usr/bin/env python3
"""Offline, exact-Git-object RockTunnel trainer import. Never executes reference code."""
import argparse,hashlib,json,pathlib,re,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-rocktunnel-trainers-v1'
LOCK_SHA256 = '369db76f2a3e9833547f86345dfe78764a155feb434770744034efd2b59224b2'
GENERATED_SHA256='585e9b998bca091752cdcb8011f0158d157fcfe6b382bc041428a159b2c31a9d'
TRAINERS=('PICNICKER_ALICIA', 'HIKER_JEREMY', 'HIKER_ALAN', 'CAMPER_CHRIS', 'BUG_CATCHER_BRENT', 'BUG_CATCHER_CONNER', 'HIKER_BRICE', 'PICNICKER_CAITLIN', 'CAMPER_DREW', 'PICNICKER_CAROL', 'HIKER_CLARK', 'POKEMANIAC_HERMAN', 'HIKER_TRENT', 'POKEMANIAC_MARK', 'PICNICKER_HEIDI', 'PICNICKER_DANA', 'PICNICKER_ARIANA', 'PICNICKER_LEAH', 'HIKER_LUCAS', 'HIKER_OLIVER', 'HIKER_LENNY', 'POKEMANIAC_ASHTON', 'POKEMANIAC_COOPER', 'HIKER_DUDLEY', 'PICNICKER_SOFIA', 'HIKER_ALLEN', 'HIKER_ERIC', 'POKEMANIAC_STEVE', 'PICNICKER_MARTHA', 'POKEMANIAC_WINSTON')
MAPS=(('Route9', 'MAP_ROUTE9', 9), ('Route10', 'MAP_ROUTE10', 6), ('RockTunnel_1F', 'MAP_ROCK_TUNNEL_1F', 7), ('RockTunnel_B1F', 'MAP_ROCK_TUNNEL_B1F', 8))
PATHS=('asm/macros/event.inc', 'charmap.txt', 'data/battle_ai_scripts.s', 'data/maps/RockTunnel_1F/map.json', 'data/maps/RockTunnel_1F/scripts.inc', 'data/maps/RockTunnel_B1F/map.json', 'data/maps/RockTunnel_B1F/scripts.inc', 'data/maps/Route10/map.json', 'data/maps/Route10/scripts.inc', 'data/maps/Route9/map.json', 'data/maps/Route9/scripts.inc', 'data/scripts/trainer_battle.inc', 'data/scripts/trainers.inc', 'include/battle.h', 'include/constants/battle.h', 'include/constants/battle_ai.h', 'include/constants/battle_setup.h', 'include/constants/opponents.h', 'include/constants/species.h', 'include/constants/trainers.h', 'src/battle_ai_switch_items.c', 'src/battle_main.c', 'src/battle_setup.c', 'src/data/pokemon/level_up_learnset_pointers.h', 'src/data/pokemon/level_up_learnsets.h', 'src/data/text/species_names.h', 'src/data/trainer_parties.h', 'src/data/trainers.h', 'src/pokemon.c')
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,p):
 if pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe path')
 q=pathlib.Path(root)/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Symlink rejected')
 return q

def learnset_rows(body):
 """Select the FireRed branch without executing a C preprocessor or source code."""
 active=True; stack=[]; rows=[]; previous=0; ended=False
 for line in body.splitlines():
  line=line.strip()
  if line=='#if defined(FIRERED)':
   stack.append((active,True))
  elif line=='#if defined(LEAFGREEN)':
   stack.append((active,False));active=False
  elif line in ('#elif defined(FIRERED)','#elif defined(LEAFGREEN)'):
   if not stack:raise ValueError('Unmatched learnset edition branch')
   parent,taken=stack[-1];branch=line=='#elif defined(FIRERED)';active=parent and not taken and branch;stack[-1]=(parent,taken or branch)
  elif line=='#else':
   if not stack:raise ValueError('Unmatched learnset else')
   parent,taken=stack[-1];active=parent and not taken;stack[-1]=(parent,True)
  elif line=='#endif':
   if not stack:raise ValueError('Unmatched learnset endif')
   active=stack.pop()[0]
  elif line.startswith('#'):raise ValueError('Unsupported learnset directive: '+line)
  elif not line:continue
  elif line=='LEVEL_UP_END':
   if active:ended=True
  else:
   m=re.fullmatch(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),',line)
   if m is None:raise ValueError('Unsupported learnset row: '+line)
   if active:
    level=int(m[1])
    if ended or level<previous:raise ValueError('Unordered or terminated learnset')
    rows.append((level,m[2]));previous=level
 if stack or not ended:raise ValueError('Unclosed or unterminated learnset')
 return rows

def convert(lock,sources):
 text={p:b.decode() for p,b in sources.items()};entries={e['path']:e for e in lock['files']}
 def one(pattern,p):
  matches=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(matches)!=1:raise ValueError('Expected exactly one match: '+pattern)
  return matches[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text[p].count('\n',0,m.start())+1,endLine=text[p].count('\n',0,m.end())+1,sha256=entries[p]['sha256'])
 def number(p,s):return int(one(r'^#define '+s+r'\s+(\d+)\b[^\n]*',p)[1])
 def encode(s):
  return [int(one("^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$",'charmap.txt')[1],16) for c in s]
 def expr(s):
  out=0
  for symbol in s.split(' | '):
   if symbol=='F_TRAINER_FEMALE':out|=128
   elif symbol.startswith('AI_SCRIPT_'):
    m=one(r'^#define '+symbol+r'\s+\(1 << (\d+)\)[^\n]*','include/constants/battle_ai.h');out|=1<<int(m[1])
   else:out|=number('include/constants/trainers.h',symbol)
  return out
 mapObjects=[]
 for name,mapId,count in MAPS:
  mapData=json.loads(text[f'data/maps/{name}/map.json'])
  objects=[o for o in mapData['object_events'] if o['trainer_type']=='TRAINER_TYPE_NORMAL']
  if mapData['id']!=mapId or len(objects)!=count:raise ValueError('RockTunnel source map roster differs: '+name)
  mapObjects.extend(objects)
 trainers=[];battles=[];resolved=[]
 for short in TRAINERS:
  s='TRAINER_'+short;p='src/data/trainers.h';m=one(r'^    \['+s+r'\] = \{\n(.*?)^    \},',p)
  fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M));name=re.fullmatch(r'_\("([A-Z]+)"\)',fields['trainerName'])[1]
  form=re.fullmatch(r'NO_ITEM_(DEFAULT|CUSTOM)_MOVES\((\w+)\)',fields['party'])
  if form is None:raise ValueError('Only source no-held-item parties are admitted')
  partySymbol=form[2];custom=form[1]=='CUSTOM'
  if set(fields)!={'trainerClass','encounterMusic_gender','trainerPic','trainerName','items','doubleBattle','aiFlags','party'}:raise ValueError('Unexpected trainer fields')
  if fields['encounterMusic_gender'] not in ('TRAINER_ENCOUNTER_MUSIC_MALE','TRAINER_ENCOUNTER_MUSIC_HIKER','TRAINER_ENCOUNTER_MUSIC_SUSPICIOUS','TRAINER_ENCOUNTER_MUSIC_GIRL | F_TRAINER_FEMALE') or fields['aiFlags']!='AI_SCRIPT_CHECK_BAD_MOVE':raise ValueError('Unexpected encounter or AI flags')
  pp='src/data/trainer_parties.h';party=one(r'^static const struct TrainerMonNoItem'+('Custom' if custom else 'Default')+'Moves '+partySymbol+r'\[\] = \{\n(.*?)^\};',pp)
  rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{([^}]+)\},\n)?    \},\n',party[1]))
  if ''.join(r[0] for r in rows)!=party[1]:raise ValueError('Unsupported party')
  mons=[];natural=[]
  for row in rows:
   iv,lv,sp=int(row[1]),int(row[2]),row[3];np='src/data/text/species_names.h';nm=one(r'^    \['+sp+r'\] = _\("([^"]+)"\),$',np);speciesName=nm[1]
   pointer=one(r'^    \['+sp+r'\] = (\w+),$','src/data/pokemon/level_up_learnset_pointers.h')[1]
   learn=one(r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};','src/data/pokemon/level_up_learnsets.h');moves=[]
   for level,move in learnset_rows(learn[1]):
    if level>lv:break
    if move not in moves:moves=(moves+[move])[-4:]
   if bool(row[4])!=custom:raise ValueError('Party macro and custom move field differ')
   slots=row[4].split(', ') if custom else []
   if custom and (len(slots)!=4 or any(re.fullmatch(r'MOVE_\w+',x) is None for x in slots)):raise ValueError('Expected four custom slots')
   if 'MOVE_NONE' in slots and any(x!='MOVE_NONE' for x in slots[slots.index('MOVE_NONE'):]):raise ValueError('Nontrailing empty move slot')
   natural.append(dict(species=sp,level=lv,moves=slots if custom else moves,learnsetSource=ref('src/data/pokemon/level_up_learnsets.h',pointer,learn)))
   mons.append(dict(species=dict(value='pret:'+sp),sourceSpeciesId=number('include/constants/species.h',sp),speciesName=speciesName,encodedSpeciesName=encode(speciesName),level=lv,sourceIv=iv,heldItem='ITEM_NONE',customMoves=[dict(value='pret:'+x) for x in slots],source=ref(pp,partySymbol,party),speciesNameSource=ref(np,sp,nm)))
  if fields['items']!='{}' or fields['doubleBattle']!='FALSE':raise ValueError('Unexpected item/double')
  money=one(r'^\s*\{'+fields['trainerClass']+r', (\d+)\},$','src/battle_main.c')
  trainers.append(dict(id=dict(value='pret:'+s),sourceTrainerId=number('include/constants/opponents.h',s),trainerClass=fields['trainerClass'],sourceTrainerClass=number('include/constants/trainers.h',fields['trainerClass']),trainerPic=fields['trainerPic'],sourceTrainerPic=number('include/constants/trainers.h',fields['trainerPic']),sourceTrainerName=name,encodedTrainerName=encode(name),encounterMusicGender=expr(fields['encounterMusic_gender']),items=['ITEM_NONE']*4,doubleBattle=False,partyFlags=int(custom),aiFlags=expr(fields['aiFlags']),aiFlagSymbols=fields['aiFlags'].split(' | '),baseMoney=int(money[1]),party=mons,source=ref(p,s,m),moneySource=ref('src/battle_main.c',fields['trainerClass'],money)))
  candidates=[]
  for p in ('data/scripts/trainers.inc',*[f'data/maps/{name}/scripts.inc' for name,_,_ in MAPS]):
   for label in re.finditer(r'^(\w+)::\n((?:(?!^\w+::).)*)',text[p],re.M|re.S):
    battle=re.search(r'^\ttrainerbattle_(single|no_intro) '+s+r', ([^\n]+)$',label[2],re.M)
    if battle:candidates.append((p,label,battle))
  if len(candidates)!=1:raise ValueError('Expected one initial trainer battle: '+s)
  p,label,battle=candidates[0];args=battle[2].split(', ');noIntro=battle[1]=='no_intro'
  if noIntro or len(args)!=2:raise ValueError('Unsupported RockTunnel initial battle mode')
  if label[1]!=mapObjects[len(trainers)-1]['script']:raise ValueError('Map trainer and battle identity differ')
  defeat=args[0] if noIntro else args[1];intro='' if noIntro else args[0]
  post=re.search(r'^\tmsgbox (\w+)',label[2][battle.end():],re.M)
  battles.append(dict(trainer=dict(value='pret:'+s),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=1<<int(one(r'^#define BATTLE_TYPE_TRAINER[ \t]+\(1 << (\d+)\)[ \t]*$','include/constants/battle.h')[1]),scriptEntry=label[1],introText=intro,defeatText=defeat,victoryText='',postBattleText=post[1] if post else '',entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(p,label[1],label),postBattleScript=None,musicOption=None))
  resolved.append(dict(trainer=s,party=natural))
 if len(trainers)!=30 or sum(len(t['party']) for t in trainers)!=72:raise ValueError('RockTunnel source roster differs')
 return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved,limitations=['All thirty initial Route9, Route10 and Rock Tunnel teams with seventy-two Pokemon; Route10 Mark is retained even though his field location requires Surf.','Rematches, double battles and full roster are not enabled.','Trainer portrait rendering, map events and battle execution are separate integration gates.'])

def run(repo,raw,out):
 raw,out=pathlib.Path(raw).absolute(),pathlib.Path(out).absolute()
 if any(x.is_symlink() for p in (raw,out) for x in (p,*p.parents)):raise ValueError('Symlink materialization root rejected')
 raw,out=raw.resolve(),out.resolve()
 def overlaps(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 if overlaps(raw,out):raise ValueError('Raw and generated roots must be disjoint')
 if repo:
  repo=pathlib.Path(repo).resolve()
  if overlaps(repo,raw) or overlaps(repo,out):raise ValueError('Reference and materialization roots overlap')
  if subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD']).decode().strip()!=COMMIT:raise ValueError('Reference HEAD differs')
  if subprocess.check_output(['git','-C',str(repo),'rev-parse',COMMIT+'^{tree}']).decode().strip()!=TREE:raise ValueError('Pinned Git tree differs')
 for kind in ('raw','generated'):
  parent=ROOT/'content'/kind
  for directory in parent.iterdir():
   if directory.is_dir() and directory.name!='rocktunnel-trainers' and (overlaps(raw,directory.resolve()) or overlaps(out,directory.resolve())):
    raise ValueError('Historical content trees are protected')
 lockBytes=safe(ROOT,'scripts/rocktunnel-trainers.lock.json').read_bytes()
 if sha(lockBytes)!=LOCK_SHA256:raise ValueError('Trainer source lock differs')
 lock=json.loads(lockBytes)
 if (lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'],lock['profile'],lock['gameEdition'],lock['romRevision'])!=(1,'pret/pokefirered',COMMIT,TREE,PROFILE,'FIRERED',0) or len(lock['files'])!=len(PATHS) or set(e['path'] for e in lock['files'])!=set(PATHS):raise ValueError('Source identity differs')
 sources={}
 for e in lock['files']:
  b=subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+e['path']]) if repo else safe(raw,e['path']).read_bytes()
  if len(b)!=e['byteLength'] or sha(b)!=e['sha256'] or hashlib.sha1(('blob '+str(len(b))+'\0').encode()+b).hexdigest()!=e['gitBlobSha1']:raise ValueError('Pinned input differs: '+e['path'])
  sources[e['path']]=b
 result=canonical(convert(lock,sources))
 if sha(result)!=GENERATED_SHA256:raise ValueError('Pinned trainer output differs')
 material=[(safe(out,'trainers.json'),result)]
 if repo:material.extend((safe(raw,p),b) for p,b in sources.items())
 for f,_ in material:
  if f.exists() and not f.is_file():raise ValueError('Output is not a regular file')
  if any(x.exists() and not x.is_dir() for x in f.parents):raise ValueError('Output parent is not a directory')
 for f,b in material:
  f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
 print(json.dumps(dict(profile=PROFILE,inputCount=len(sources),trainers=30,partyMembers=72,sha256=sha(result))))
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reference-repo',type=pathlib.Path);parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/rocktunnel-trainers');parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/rocktunnel-trainers');args=parser.parse_args();run(args.reference_repo,args.raw,args.out)
