#!/usr/bin/env python3
"""Offline source-authenticated Rocket Hideout species, moves, trainer teams and art.
The complete source learnset is metadata; no battle effect is enabled by importing it.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
import argparse,json,pathlib,re,sys
import pokemon_import as pokemon
from pokemon_import import learn_rows
from pokemon_import_io import safe, load_recipe, read_sources
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-rocket-hideout-pokemon-v1'
NEW_SPECIES=('Kangaskhan',)
ART_SPECIES=(('Kangaskhan',115),)
TEACHING_MOVES=('MOVE_TAUNT','MOVE_FRUSTRATION','MOVE_SNATCH')
TRAINERS=tuple('TEAM_ROCKET_GRUNT_'+str(i) for i in range(7,19))+('BOSS_GIOVANNI',)
SCRIPT_PATHS=tuple('data/maps/'+m+'/scripts.inc' for m in ('CeladonCity_GameCorner','RocketHideout_B1F','RocketHideout_B2F','RocketHideout_B3F','RocketHideout_B4F'))
def source_symbol(name):return name.upper()
def canonical(v):return (json.dumps(v,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n').encode()

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
  if short.startswith('RIVAL_SS_ANNE_'):
   context=one(r'^SSAnne_2F_Corridor_EventScript_RivalTrigger::\n((?:(?!^\w+::).)*)',p)[1]
   speech=re.findall(r'^\tmsgbox (\w+)$',context,re.M)
   if speech!=['SSAnne_2F_Corridor_Text_RivalIntro','SSAnne_2F_Corridor_Text_RivalPostBattle']:raise ValueError('Unexpected rival context')
   intro,postText=speech
  battles.append(dict(trainer=dict(value='pret:'+s),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if noIntro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=battleType,scriptEntry=label[1],introText=intro,defeatText=defeat,victoryText='',postBattleText=postText,entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(p,label[1],label),postBattleScript=args[2] if len(args)>2 else None,musicOption=args[3] if len(args)>3 else None))
  resolved.append(dict(trainer=s,party=natural))
 moves=[];moveNames=[]
 for symbol in sorted({m for r in resolved for p in r['party'] for m in p['moves']} - {'MOVE_NONE'}):
  p='src/data/battle_moves.h';m=one(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',p)
  f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
  moves.append(dict(value='pret:' + symbol))
  np='src/data/text/move_names.h';nm=one(r'^    \['+symbol+r'\]\s*= _\("([^"]+)"\),$',np)
  moveNames.append(dict(id=dict(value='pret:'+symbol),name=nm[1],source=ref(np,symbol,nm)))
 return dict(schemaVersion=1, completeSourceRoster=False, trainers=trainers, battles=battles, resolvedMoves=resolved, moves=moves, moveNames=[dict(id=row['id'], name=row['name']) for row in moveNames])


def rebuild(project,target,check=False):
 prior=json.loads(safe(project,'content/generated/sprites/pokemon/celadon-gym/supplement.json').read_bytes())
 previous_species=tuple(w['speciesId'][13:] for w in prior['weights'])
 if len(previous_species)!=98:raise ValueError('Exact 98 prior species required')
 results=[]
 for stem in ('rocket-hideout-pokemon','rocket-hideout-trainers'):
  lock = load_recipe(ROOT / ('scripts/recipes/' + ('pokemon' if stem.endswith('pokemon') else 'trainers' if stem.endswith('trainers') else 'items') + '/' + stem + '.lock.json'), commit=COMMIT, profile='firered-' + stem + '-v1')
  sources = read_sources(lock, target / 'content/raw/pokefirered/037335f4')
  trainers=trainer_convert(lock,sources)
  if len(trainers['trainers'])!=13 or sum(len(t['party']) for t in trainers['trainers'])!=36 or len(trainers['moves'])!=38:raise ValueError('Exact source trainer closure differs')
  if stem.endswith('pokemon'):
   outputs = pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
       limitation='Kangaskhan original front/back frame zero, normal and shiny palettes.')
   # This file historically writes the art document in compact JSON form.
   outputs['battle-art.json'] = canonical(json.loads(outputs['battle-art.json']))
   outputs.update(pokemon.supplement(lock, sources, profile=PROFILE, species=ART_SPECIES,
       weight_species=previous_species + tuple(name.upper() for name in NEW_SPECIES), natural_level_limit=None))
  else:outputs={'trainers.json':trainer_json(target/'content/generated', trainers, check=check)}
  profile='firered-'+stem+'-v1'
  manifest=canonical(dict(schemaVersion=1, inputCount=len(sources), outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
  outputs['manifest.json']=manifest
  raw_manifest=canonical(dict(schemaVersion=1, inputCount=len(sources)))
  directory = 'content/generated/data/' + ('pokemon/rocket-hideout' if stem.endswith('pokemon') else 'trainer-battles/rocket-hideout')
  materials={directory+'/'+p:b for p,b in outputs.items()}
  for p,b in materials.items():
   dest=safe(target,p)
   if check:
    if dest.read_bytes()!=b:raise ValueError('Generated bytes differ: '+p)
   else:
    dest.parent.mkdir(parents=True,exist_ok=True);stage=safe(target,p+'.tmp');stage.write_bytes(b);stage.replace(dest)
  results.append(dict(stem=stem,inputCount=len(sources),outputCount=len(outputs)-1))
 return results
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--target-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
 print(json.dumps(rebuild(safe(a.project_root),safe(a.target_root),a.check),sort_keys=True))
