#!/usr/bin/env python3
"""Offline Tower trainer import. Reads pinned inherited source; never executes upstream code."""
import argparse,json,pathlib,re
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-pokemon-tower-trainers-v1'
TRAINERS=('RIVAL_POKEMON_TOWER_SQUIRTLE', 'RIVAL_POKEMON_TOWER_BULBASAUR', 'RIVAL_POKEMON_TOWER_CHARMANDER', 'CHANNELER_PATRICIA', 'CHANNELER_CARLY', 'CHANNELER_HOPE', 'CHANNELER_PAULA', 'CHANNELER_LAUREL', 'CHANNELER_JODY', 'CHANNELER_TAMMY', 'CHANNELER_RUTH', 'CHANNELER_KARINA', 'CHANNELER_JANAE', 'CHANNELER_ANGELICA', 'CHANNELER_EMILIA', 'CHANNELER_JENNIFER', 'TEAM_ROCKET_GRUNT_19', 'TEAM_ROCKET_GRUNT_20', 'TEAM_ROCKET_GRUNT_21')
SCRIPT_PATHS=('data/maps/PokemonTower_2F/scripts.inc', 'data/maps/PokemonTower_3F/scripts.inc', 'data/maps/PokemonTower_4F/scripts.inc', 'data/maps/PokemonTower_5F/scripts.inc', 'data/maps/PokemonTower_6F/scripts.inc', 'data/maps/PokemonTower_7F/scripts.inc')
def canonical(v):return (json.dumps(v,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n').encode()
def safe(root,p=None):
 if p is not None and (not p or pathlib.PurePosixPath(p).is_absolute() or ':' in p or '\\' in p or any(x in ('','.','..') for x in p.split('/'))):raise ValueError('Unsafe relative path')
 q=pathlib.Path(root).absolute() if p is None else pathlib.Path(root).absolute()/p
 if '..' in q.parts or any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Unsafe root or symlink')
 return q
def trainer_convert(lock,sources):
 text={p:b.decode() for p,b in sources.items() if not p.endswith(('.png','.wav'))};entries={e['path']:e for e in lock['files']}
 def one(pattern,p):
  matches=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(matches)!=1:raise ValueError('Expected exactly one match: '+pattern)
  return matches[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text[p].count('\n',0,m.start())+1,endLine=text[p].count('\n',0,m.end())+1)
 def number(p,s):return int(one(r'^#define '+s+r'\s+(\d+)\b[^\n]*',p)[1])
 def encode(s):
  return [int(one("^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$",'charmap.txt')[1],16) for c in s]
 def expr(s):
  out=0
  for symbol in s.split(' | '):
   if symbol=='F_TRAINER_FEMALE':out|=1<<int(one(r'^#define F_TRAINER_FEMALE\s+\(1 << (\d+)\)[^\n]*','include/constants/trainers.h')[1])
   elif symbol.startswith('AI_SCRIPT_'):
    m=one(r'^#define '+symbol+r'\s+\(1 << (\d+)\)[^\n]*','include/constants/battle_ai.h');out|=1<<int(m[1])
   else:out|=number('include/constants/trainers.h',symbol)
  return out
 battleType=1<<int(one(r'^#define BATTLE_TYPE_TRAINER\s+\(1 << (\d+)\)[^\n]*','include/constants/battle.h')[1])
 for macro,expansion in (
   ('single',r'trainerbattle TRAINER_BATTLE_SINGLE, \trainer, 0, \intro_text, \lose_text'),
   ('no_intro',r'trainerbattle TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT, \trainer, 0, \lose_text')):
  body=one(r'^\t\.macro trainerbattle_'+macro+r' [^\n]+\n(.*?)^\t\.endm','asm/macros/event.inc')[1]
  if '\t'+expansion+'\n' not in body:raise ValueError('Battle macro parameters differ')
 defaultFlags=int(one(r'^#define NO_ITEM_DEFAULT_MOVES\(party\)[^\n]*\.partyFlags = (\d+)[ \t]*$','include/battle.h')[1])
 customFlags=1<<int(one(r'^#define F_TRAINER_PARTY_CUSTOM_MOVESET\s+\(1 << (\d+)\)[^\n]*','include/constants/trainers.h')[1])
 trainers=[];battles=[];resolved=[]
 for short in TRAINERS:
  s='TRAINER_'+short;p='src/data/trainers.h';m=one(r'^    \['+s+r'\] = \{\n(.*?)^    \},',p)
  fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M));name=re.fullmatch(r'_\("([A-Z. ]+)"\)',fields['trainerName'])[1]
  shape,partySymbol=re.fullmatch(r'(NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',fields['party']).groups();custom=shape=='NO_ITEM_CUSTOM_MOVES'
  pp='src/data/trainer_parties.h';party=one(r'^static const struct TrainerMonNoItem'+('Custom' if custom else 'Default')+'Moves '+partySymbol+r'\[\] = \{\n(.*?)^\};',pp)
  rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{([^}]+)\},\n)?    \},\n',party[1]))
  if ''.join(r[0] for r in rows)!=party[1]:raise ValueError('Unsupported party')
  mons=[];natural=[]
  for row in rows:
   iv,lv,sp=int(row[1]),int(row[2]),row[3];np='src/data/text/species_names.h';nm=one(r'^    \['+sp+r'\] = _\("([^"]+)"\),$',np);speciesName=nm[1]
   pointer=one(r'^    \['+sp+r'\] = (\w+),$','src/data/pokemon/level_up_learnset_pointers.h')[1]
   learn=one(r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};','src/data/pokemon/level_up_learnsets.h');moves=[]
   for level,move in re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)',learn[1]):
    if int(level)<=lv and move not in moves:moves=(moves+[move])[-4:]
   slots=row[4].split(', ') if custom else []
   if custom and len(slots)!=4:raise ValueError('Expected four custom slots')
   natural.append(dict(species=sp,level=lv,moves=slots if custom else moves,learnsetSource=ref('src/data/pokemon/level_up_learnsets.h',pointer,learn)))
   mons.append(dict(species=dict(value='pret:'+sp),sourceSpeciesId=number('include/constants/species.h',sp),speciesName=speciesName,encodedSpeciesName=encode(speciesName),level=lv,sourceIv=iv,heldItem='ITEM_NONE',customMoves=[dict(value='pret:'+x) for x in slots],source=ref(pp,partySymbol,party),speciesNameSource=ref(np,sp,nm)))
  if fields['doubleBattle']!='FALSE':raise ValueError('Unexpected double')
  items=[x for x in fields['items'].strip('{}').split(', ') if x]
  if items!=[]:raise ValueError('Unexpected trainer items')
  items+=['ITEM_NONE']*(4-len(items))
  money=one(r'^\s*\{'+fields['trainerClass']+r', (\d+)\},$','src/battle_main.c')
  trainers.append(dict(id=dict(value='pret:'+s),sourceTrainerId=number('include/constants/opponents.h',s),trainerClass=fields['trainerClass'],sourceTrainerClass=number('include/constants/trainers.h',fields['trainerClass']),trainerPic=fields['trainerPic'],sourceTrainerPic=number('include/constants/trainers.h',fields['trainerPic']),sourceTrainerName=name,encodedTrainerName=encode(name),encounterMusicGender=expr(fields['encounterMusic_gender']),items=items,doubleBattle=False,partyFlags=customFlags if custom else defaultFlags,aiFlags=expr(fields['aiFlags']),aiFlagSymbols=fields['aiFlags'].split(' | '),baseMoney=int(money[1]),party=mons,source=ref(p,s,m),moneySource=ref('src/battle_main.c',fields['trainerClass'],money)))
  candidates=[]
  for p in SCRIPT_PATHS:
   for label in re.finditer(r'^(\w+)::\n((?:(?!^\w+::).)*)',text[p],re.M|re.S):
    battle=re.search(r'^\ttrainerbattle_(single|no_intro) '+s+r', ([^\n]+)$',label[2],re.M)
    if battle:candidates.append((p,label,battle))
  if len(candidates)!=1:raise ValueError('Expected one initial trainer battle: '+s)
  p,label,battle=candidates[0];args=battle[2].split(', ');noIntro=battle[1]=='no_intro'
  if len(args) not in ((1,) if noIntro else (2,3)):raise ValueError('Unsupported battle arguments')
  defeat=args[0] if noIntro else args[1];intro='' if noIntro else args[0]
  post=re.search(r'^\tmsgbox (\w+)',label[2][battle.end():],re.M)
  postText=post[1] if post else ''
  if short.startswith('RIVAL_POKEMON_TOWER_'):
   context=one(r'^PokemonTower_2F_EventScript_Rival::\n((?:(?!^\w+::).)*)',p)[1]
   speech=re.findall(r'^\tmsgbox (\w+)$',context,re.M)
   if speech!=['PokemonTower_2F_Text_RivalIntro','PokemonTower_2F_Text_RivalPostBattle']:raise ValueError('Unexpected rival context')
   intro,postText=speech
  battles.append(dict(trainer=dict(value='pret:'+s),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if noIntro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=battleType,scriptEntry=label[1],introText=intro,defeatText=defeat,victoryText='',postBattleText=postText,entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(p,label[1],label),postBattleScript=args[2] if len(args)>2 else None,musicOption=args[3] if len(args)>3 else None))
  resolved.append(dict(trainer=s,party=natural))
 moves=[];moveNames=[]
 for symbol in sorted({m for r in resolved for p in r['party'] for m in p['moves']} - {'MOVE_NONE'}):
  p='src/data/battle_moves.h';m=one(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',p)
  f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
  moves.append(dict(id=dict(value='pret:'+symbol),sourceMoveId=number('include/constants/moves.h',symbol),pp=int(f['pp']),power=int(f['power']),accuracy=int(f['accuracy']),type=f['type'],effect=f['effect'],target=f['target'],secondaryEffectChance=int(f['secondaryEffectChance']),priority=int(f['priority']),battleEffectImplemented=False,flags=f['flags'].split(' | ') if f['flags']!='0' else [],source=ref(p,symbol,m)))
  np='src/data/text/move_names.h';nm=one(r'^    \['+symbol+r'\]\s*= _\("([^"]+)"\),$',np)
  moveNames.append(dict(id=dict(value='pret:'+symbol),name=nm[1],source=ref(np,symbol,nm)))
 return dict(schemaVersion=1,profile='firered-pokemon-tower-trainers-v1',sourceCommit=COMMIT,completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved,moves=moves,moveNames=moveNames,limitations=['The nineteen Pokemon Tower teams with 40 original party members; no field or battle activation.','Rematches, double battles and the complete game trainer roster are not enabled.','Source declarations and party construction do not implement field callbacks, Pokemon Tower progression, trainer portraits or audio.'])

def read_sources(project):
 lock_bytes=safe(ROOT,'scripts/pokemon-tower-trainers.lock.json').read_bytes()
 lock=json.loads(lock_bytes)
 if (lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], lock['gameEdition'], lock['romRevision']) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):raise ValueError('Source identity differs')
 sources={}
 for row in lock['files']:
  b=safe(project,row['storagePath']).read_bytes()
  if row['path'] in sources:raise ValueError('Duplicate source')
  sources[row['path']]=b
 return lock,sources

def rebuild(project,target,check=False):
 lock,sources=read_sources(project)
 doc=trainer_convert(lock,sources)
 if len(doc['trainers'])!=19 or sum(len(t['party']) for t in doc['trainers'])!=40:raise ValueError('Exact nineteen-team forty-member source closure required')
 data=canonical(doc)
 manifest=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(sources),upstreamScriptsExecuted=False,romDownloaded=False,outputs=[dict(path='trainers.json')]))
 for p,b in [('trainers.json',data),('manifest.json',manifest)]:
  dest=safe(target,'content/generated/pokemon-tower-trainers/'+p)
  if check:
   if dest.read_bytes()!=b:raise ValueError('Generated output differs: '+p)
  else:
   dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 return dict(passed=True,sourceCommit=COMMIT,inputs=len(sources),trainers=len(doc['trainers']),members=sum(len(t['party']) for t in doc['trainers']),moves=len(doc['moves']))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--target-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
 print(json.dumps(rebuild(safe(a.project_root),safe(a.target_root),a.check),sort_keys=True))
