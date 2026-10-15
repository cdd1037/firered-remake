#!/usr/bin/env python3
"""Offline, exact-Git-object Celadon Gym trainer import. Never executes reference code."""
import argparse,json,pathlib,re,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-celadon-gym-trainers-v1'
TRAINERS=('LASS_KAY','BEAUTY_BRIDGET','PICNICKER_TINA','BEAUTY_TAMIA','BEAUTY_LORI','LASS_LISA','COOLTRAINER_MARY','LEADER_ERIKA')
SCRIPT_PATHS=('data/maps/CeladonCity_Gym/scripts.inc',)
PATHS=('charmap.txt', 'include/battle.h', 'include/constants/opponents.h', 'include/constants/trainers.h', 'include/constants/species.h', 'include/constants/battle_ai.h', 'src/data/trainers.h', 'src/data/trainer_parties.h', 'src/data/text/species_names.h', 'src/data/pokemon/level_up_learnsets.h', 'src/data/pokemon/level_up_learnset_pointers.h', 'src/battle_main.c', 'src/battle_setup.c', 'asm/macros/event.inc', 'include/constants/battle_setup.h', 'include/constants/battle.h', 'data/scripts/trainer_battle.inc')+SCRIPT_PATHS+('src/data/battle_moves.h','include/constants/moves.h','src/battle_ai_switch_items.c','src/battle_ai_script_commands.c','data/battle_ai_scripts.s','src/battle_script_commands.c','data/battle_scripts_1.s','src/pokemon.c','src/battle_util.c','src/data/pokemon/item_effects.h','src/data/pokemon/pokedex_entries.h','src/data/text/move_names.h','src/data/pokemon/species_info.h','src/data/pokemon/tmhm_learnsets.h','src/data/items.json','include/constants/items.h','include/constants/battle_move_effects.h','include/constants/abilities.h')
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,p):
 if pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe path')
 q=pathlib.Path(root)/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Symlink rejected')
 return q

def root_path(value):
 p=pathlib.Path(value).absolute()
 if any(x.is_symlink() for x in (p,*p.parents)):raise ValueError('Symlink root rejected')
 return p.resolve()

def convert(lock,sources):
 text={p:b.decode() for p,b in sources.items()};entries={e['path']:e for e in lock['files']}
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
  if items!=(['ITEM_HYPER_POTION','ITEM_FULL_HEAL'] if short=='LEADER_ERIKA' else ['ITEM_SUPER_POTION'] if short=='COOLTRAINER_MARY' else []):raise ValueError('Unexpected trainer items')
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
  if len(args)!=(4 if short=='LEADER_ERIKA' else 2):raise ValueError('Unsupported battle arguments')
  defeat=args[0] if noIntro else args[1];intro='' if noIntro else args[0]
  post=re.search(r'^\tmsgbox (\w+)',label[2][battle.end():],re.M)
  postText=post[1] if post else ''
  if short.startswith('RIVAL_SS_ANNE_'):
   context=one(r'^SSAnne_2F_Corridor_EventScript_RivalTrigger::\n((?:(?!^\w+::).)*)',p)[1]
   speech=re.findall(r'^\tmsgbox (\w+)$',context,re.M)
   if speech!=['SSAnne_2F_Corridor_Text_RivalIntro','SSAnne_2F_Corridor_Text_RivalPostBattle']:raise ValueError('Unexpected rival context')
   intro,postText=speech
  battles.append(dict(trainer=dict(value='pret:'+s),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if noIntro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=battleType,scriptEntry=label[1],introText=intro,defeatText=defeat,victoryText='',postBattleText=postText,entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(p,label[1],label),postBattleScript=args[2] if len(args)>2 else None,musicOption=args[3] if len(args)>3 else None))
  resolved.append(dict(trainer=s,party=natural))
 moves=[];moveNames=[]
 for symbol in sorted({m for r in resolved for p in r['party'] for m in p['moves']}):
  p='src/data/battle_moves.h';m=one(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',p)
  f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
  moves.append(dict(value='pret:' + symbol))
  np='src/data/text/move_names.h';nm=one(r'^    \['+symbol+r'\]\s*= _\("([^"]+)"\),$',np)
  moveNames.append(dict(id=dict(value='pret:'+symbol),name=nm[1],source=ref(np,symbol,nm)))
 return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved,moves=moves,moveNames=moveNames,limitations=['The eight initial Celadon Gym teams with 21 original party members; no field or battle activation.','Rematches, double battles and the complete game trainer roster are not enabled.','Source declarations and party construction do not implement field callbacks, Gym progression, trainer portraits or audio.'])

def run(repo,raw,out):
 raw,out=root_path(raw),root_path(out)
 def overlaps(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 if overlaps(raw,out):raise ValueError('Raw and generated roots must be disjoint')
 for p in ('scripts','src','tests','.git','scripts/import-config'):
  if overlaps(raw,ROOT/p) or overlaps(out,ROOT/p):raise ValueError('Protected project path overlap')
 if repo:
  repo=pathlib.Path(repo).resolve()
  if overlaps(repo,raw) or overlaps(repo,out):raise ValueError('Reference and materialization roots overlap')
 for kind in ('raw','generated'):
  parent=ROOT/'content'/kind
  for directory in parent.iterdir() if parent.exists() else []:
   if directory.is_dir() and directory.name!='celadon-gym-trainers' and (overlaps(raw,directory.resolve()) or overlaps(out,directory.resolve())):
    raise ValueError('Historical content trees are protected')
 lockBytes=safe(ROOT,'scripts/celadon-gym-trainers.lock.json').read_bytes()
 lock=json.loads(lockBytes)
 if (lock['commit'], lock['profile'], lock['repository'], lock['gameEdition'], lock['romRevision'], lock['schemaVersion']) != (COMMIT, PROFILE, 'pret/pokefirered', 'FIRERED', 0, 1) or len(lock['files']) != len(PATHS) or set((e['path'] for e in lock['files'])) != set(PATHS):raise ValueError('Source identity differs')
 sources={}
 for e in lock['files']:
  if repo:
   member=subprocess.check_output(['git','-C',str(repo),'ls-tree',COMMIT,'--',e['path']]).decode().strip()
  b=subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+e['path']]) if repo else safe(raw,e['path']).read_bytes()
  sources[e['path']]=b
 result=canonical(convert(lock,sources))
 material=[(safe(out,'trainers.json'),result)]
 if repo:material.extend((safe(raw,p),b) for p,b in sources.items())
 for f,_ in material:
  if f.exists() and not f.is_file():raise ValueError('Output is not a regular file')
  if any(x.exists() and not x.is_dir() for x in f.parents):raise ValueError('Output parent is not a directory')
 for f,b in material:
  f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
 print(json.dumps(dict(profile=PROFILE,inputCount=len(sources),trainers=len(TRAINERS))))
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reference-repo',type=pathlib.Path);parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/celadon-gym-trainers');args=parser.parse_args();run(args.reference_repo,args.raw,args.out)
