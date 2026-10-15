#!/usr/bin/env python3
"""Offline additive Fuchsia route metadata and complete initial trainer source roster.
No upstream programs are executed; pinned Git blobs can optionally reseed the local cache.
"""
import argparse,  json, pathlib, re, sys
sys.dont_write_bytecode=True
from pokemon_import_io import safe, load_recipe, read_sources, write_files
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-fuchsia-route-pokemon-v1'
NEW_SPECIES=('FARFETCHD','DODUO','DODRIO','DITTO')
NEW_MOVES=('MOVE_SWORDS_DANCE','MOVE_TRANSFORM','MOVE_FURY_CUTTER')
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def authenticated(root,reference=None):
 lock=load_recipe(safe(root,'scripts/fuchsia-route-pokemon.lock.json'),commit=COMMIT,profile=PROFILE)
 if (lock['schemaVersion'],lock['repository'],len(lock['files']))!=(1,'pret/pokefirered',93):raise ValueError('Fuchsia-Route source scope differs')
 raw_root=safe(root,'content/raw/pokefirered/037335f4')
 raw=read_sources(lock,raw_root,reference)
 if reference is not None:write_files(raw_root,raw)
 return lock,raw

def generate(root):
 lock,raw=authenticated(root);text={p:b.decode() for p,b in raw.items() if not p.endswith('.png')};inventory={x['path']:x for x in lock['files']}
 def one(p,pattern):
  ms=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(ms)!=1:raise ValueError(f'Expected one source match ({len(ms)}): {p}: {pattern}')
  return ms[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text[p].count('\n',0,m.start())+1,endLine=text[p].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 def constant(p,s):
  m=one(p,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');return dict(symbol=s,value=int(m[1]),source=ref(p,s,m))
 def number(p,s):return constant(p,s)['value']
 def field(p,s):
  m=one(p,r'^    \['+s+r'\] =\s*\n    \{\n(.*?)^    \},');return dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M)),ref(p,s,m)
 def name(p,s):
  m=one(p,r'^    \['+s+r'\]\s*= _\("([^"\n]+)"\),$');return dict(id=ident(s),name=m[1],source=ref(p,s,m))
 def encode(n):return [int(one('charmap.txt',"^'"+re.escape("\\'" if c=="'" else c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n]
 def learn(sp):
  pointer=one('src/data/pokemon/level_up_learnset_pointers.h',r'^    \['+sp+r'\] = (\w+),$')[1];p='src/data/pokemon/level_up_learnsets.h';m=one(p,r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};')
  rows=[];active=True;stack=[];previous=0
  for row in re.finditer(r'^.*(?:\n|\Z)',text[p],re.M):
   if not(m.start(1)<=row.start()<m.end(1)):continue
   line=row[0].strip()
   if line.startswith('#if defined('):
    which=line=='#if defined(FIRERED)';stack.append((active,which));active=active and which;continue
   if line.startswith('#elif defined('):
    parent,taken=stack[-1];which=line=='#elif defined(FIRERED)';active=parent and not taken and which;stack[-1]=(parent,taken or which);continue
   if line=='#else':parent,taken=stack[-1];active=parent and not taken;stack[-1]=(parent,True);continue
   if line=='#endif':active=stack.pop()[0];continue
   if not active or not line or line=='LEVEL_UP_END':continue
   lm=re.fullmatch(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),',line)
   if not lm:raise ValueError('Unsupported source learn row: '+line)
   lv=int(lm[1]);assert lv>=previous;previous=lv
   rows.append(dict(level=lv,move=ident(lm[2]),source=ref(p,pointer,row)))
  assert rows and not stack
  return rows,ref(p,pointer,m)
 def initial(sp,lv):
  moves=[]
  for row in learn(sp)[0]:
   if row['level']>lv:break
   if row['move'] not in moves:moves=(moves+[row['move']])[-4:]
  return moves
 def move(sp):
  f,r=field('src/data/battle_moves.h',sp)
  return dict(id=ident(sp),sourceMoveId=number('include/constants/moves.h',sp),pp=int(f['pp']),power=int(f['power']),type=f['type'],accuracy=int(f['accuracy']),effect=f['effect'],secondaryEffectChance=int(f['secondaryEffectChance']),target=f['target'],priority=int(f['priority']),flags=[] if f['flags']=='0' else f['flags'].split(' | '),battleEffectImplemented=False,source=r)
 species=[];required=set();names=[];machines=[];held=[]
 for short in NEW_SPECIES:
  sp='SPECIES_'+short;f,sr=field('src/data/pokemon/species_info.h',sp);lr,lref=learn(sp);required.update(x['move']['value'][5:] for x in lr)
  ep='src/data/pokemon/evolution.h';evol=[]
  if short=='DODUO':
   em=one(ep,r'^    \[SPECIES_DODUO\][ \t]*= \{\{EVO_LEVEL, 31, SPECIES_DODRIO\}\},$');evol=[dict(method='level',level=31,target=ident('SPECIES_DODRIO'),source=ref(ep,sp,em))]
  else:assert not re.search(r'^    \['+sp+r'\]',text[ep],re.M)
  keys=('hp','attack','defense','speed','specialAttack','specialDefense');suffix=('HP','Attack','Defense','Speed','SpAttack','SpDefense');types=re.findall(r'TYPE_\w+',f['types']);abilities=re.findall(r'ABILITY_\w+',f['abilities'])
  assert f['growthRate']=='GROWTH_MEDIUM_FAST' and f['genderRatio'] in ('PERCENT_FEMALE(50)','MON_GENDERLESS')
  species.append(dict(id=ident(sp),sourceSpeciesId=number('include/constants/species.h',sp),baseStats={k:int(f['base'+s]) for k,s in zip(keys,suffix)},type1=types[0],type2=types[1],catchRate=int(f['catchRate']),experienceYield=int(f['expYield']),evYield={k:int(f['evYield_'+s]) for k,s in zip(keys,suffix)},growthRate='mediumFast',genderRatio=255 if f['genderRatio']=='MON_GENDERLESS' else 127,ability1=abilities[0],ability2=abilities[1],friendship=int(f['friendship']),levelUpMoves=lr,evolutions=evol,source=sr,learnsetSource=lref))
  names.append(name('src/data/text/species_names.h',sp));held.append(dict(species=ident(sp),common=f['itemCommon'],rare=f['itemRare'],source=sr))
  p='src/data/pokemon/tmhm_learnsets.h';m=one(p,r'^    \['+sp+r'\]\s*= TMHM_LEARNSET\((.*?)\),');machines.append(dict(species=ident(sp),machineSymbols=re.findall(r'TMHM\((\w+)\)',m[1]),source=ref(p,sp,m)))
 move_names=[name('src/data/text/move_names.h',x) for x in NEW_MOVES];encoding=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'))
 encoded=[dict(symbol=n['source']['symbol'],name=n['name'],bytes=encode(n['name']+'$'),source=n['source'],encodingSource=encoding) for n in names+move_names]
 symbols=[('include/constants/species.h','SPECIES_'+s) for s in NEW_SPECIES]+[('include/constants/moves.h',s) for s in sorted(required)]+[('include/constants/abilities.h',s) for s in ('ABILITY_NONE','ABILITY_KEEN_EYE','ABILITY_INNER_FOCUS','ABILITY_RUN_AWAY','ABILITY_EARLY_BIRD','ABILITY_LIMBER')]+[('include/constants/items.h',s) for s in ('ITEM_NONE','ITEM_STICK','ITEM_METAL_POWDER')]+[('include/constants/hold_effects.h','HOLD_EFFECT_METAL_POWDER')]
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],species=species,moves=[move(s) for s in NEW_MOVES],speciesNames=names,moveNames=move_names,constants=[constant(*x) for x in symbols],encodedNames=encoded,requiredMoves=[ident(s) for s in sorted(required)],naturalLevelLimit=None,heldItems=held,machines=machines,limitations=['Metadata does not enable any battle effect, wild table, trainer entry, machine or held-item service.','All four complete source learnsets retained with Doduo level31 evolution; no artificial level cap.','Full initial route roster preserves37 singles and3 original doubles; doubles are not converted into singles.','Koga/FuchsiaGym, Good/Super Rod, Surf, rematches and additional evolution families remain separate increments.'])
 trainers=[];battles=[];resolved=[];actors=[]
 def expr(v):
  result=0
  for s in v.split(' | '):
   if s=='F_TRAINER_FEMALE':result|=128
   elif s.startswith('AI_SCRIPT_'):result|=1<<int(one('include/constants/battle_ai.h',r'^#define '+s+r'\s+\(1 << (\d+)\)[^\n]*')[1])
   else:result|=number('include/constants/trainers.h',s)
  return result
 for mapname in ('Route12','Route13','Route14','Route15'):
  for index,obj in enumerate(json.loads(text[f'data/maps/{mapname}/map.json'])['object_events'],1):
   if obj.get('trainer_type')!='TRAINER_TYPE_NORMAL':continue
   sp='data/scripts/trainers.inc';label=one(sp,r'^'+obj['script']+r'::\n(.*?)(?=^\w+::|\Z)');call=one(sp,r'^'+obj['script']+r'::\n\t(trainerbattle_single|trainerbattle_double) (TRAINER_\w+), ([^\n]+)')
   sym=call[2];double=call[1]=='trainerbattle_double';a=call[3].split(', ');post=re.search(r'^\tmsgbox (\w+)',label[1],re.M)
   b=dict(trainer=ident(sym),mode=4 if double else 0,rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=obj['script'],introText=a[0],defeatText=a[1],victoryText='',postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(sp,obj['script'],label),postBattleScript=None,musicOption=None)
   battles.append(b);actors.append(dict(map=mapname,localId=index,script=obj['script'],trainer=ident(sym),x=obj['x'],y=obj['y'],sight=int(obj['trainer_sight_or_berry_tree_id']),notEnoughPokemonText=a[2] if double else None))
   if any(t['id']==ident(sym) for t in trainers):continue
   p='src/data/trainers.h';m=one(p,r'^    \['+sym+r'\] = \{\n(.*?)^    \},');f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M));form=re.fullmatch(r'(NO_ITEM|ITEM)_(DEFAULT|CUSTOM)_MOVES\((\w+)\)',f['party']);assert form and f['items']=='{}' and (f['doubleBattle']=='TRUE')==double, (sym, f, double)
   custom=form[2]=='CUSTOM';has_item=form[1]=='ITEM';pname=form[3];pp='src/data/trainer_parties.h';pm=one(pp,r'^static const struct TrainerMon'+('Item' if has_item else 'NoItem')+('Custom' if custom else 'Default')+'Moves '+pname+r'\[\] = \{\n(.*?)^\};');party=[];natural=[]
   for row in re.finditer(r'^    \{\n(.*?)^    \},',pm[1],re.M|re.S):
    mf=dict(re.findall(r'^\s*\.(\w+) = (.+),$',row[1],re.M));speciesid=mf['species'];lv=int(mf['lvl']);n=name('src/data/text/species_names.h',speciesid);slots=[ident(s) for s in re.findall(r'MOVE_\w+',mf.get('moves',''))];resolvedslots=slots if custom else initial(speciesid,lv)
    assert (len(slots)==4) if custom else not slots
    party.append(dict(species=ident(speciesid),sourceSpeciesId=number('include/constants/species.h',speciesid),speciesName=n['name'],encodedSpeciesName=encode(n['name']),level=lv,sourceIv=int(mf['iv']),heldItem=mf.get('heldItem','ITEM_NONE'),customMoves=slots,source=ref(pp,pname,pm),speciesNameSource=n['source']))
    natural.append(dict(species=ident(speciesid),level=lv,moves=resolvedslots,learnsetSource=learn(speciesid)[1]))
   tn=re.fullmatch(r'_\("([^"]+)"\)',f['trainerName'])[1];money=one('src/battle_main.c',r'^\s*\{'+f['trainerClass']+r', (\d+)\},$')
   trainers.append(dict(id=ident(sym),sourceTrainerId=number('include/constants/opponents.h',sym),trainerClass=f['trainerClass'],sourceTrainerClass=number('include/constants/trainers.h',f['trainerClass']),trainerPic=f['trainerPic'],sourceTrainerPic=number('include/constants/trainers.h',f['trainerPic']),sourceTrainerName=tn,encodedTrainerName=encode(tn),encounterMusicGender=expr(f['encounterMusic_gender']),items=['ITEM_NONE']*4,doubleBattle=double,partyFlags=int(custom)+2*int(has_item),aiFlags=expr(f['aiFlags']),aiFlagSymbols=f['aiFlags'].split(' | '),baseMoney=int(money[1]),party=party,source=ref(p,sym,m),moneySource=ref('src/battle_main.c',f['trainerClass'],money)))
   resolved.append(dict(trainer=ident(sym),party=natural))
 assert len(trainers)==40 and len(battles)==43 and sum(t['doubleBattle'] for t in trainers)==3
 tdoc=dict(schemaVersion=1,profile='firered-fuchsia-route-trainers-v1',sourceCommit=COMMIT,completeSourceRoster=False,trainers=trainers,battles=battles,actors=actors,resolvedMoves=resolved,limitations=doc['limitations'])
 fixtures=dict(schemaVersion=1,sourceCommit=COMMIT,requiredMoveDefinitions=[move(s) for s in sorted(required)],initialMoves=[dict(species=ident('SPECIES_'+s),level=lv,moves=initial('SPECIES_'+s,lv)) for s in NEW_SPECIES for lv in (1,23,25,26,28,30,31,33,38,40,43,100)],heldItems=held,machines=machines)
 return {'content/generated/fuchsia-route-pokemon/catalog.json':canonical(doc),'content/generated/fuchsia-route-pokemon/trainers.json':canonical(tdoc),'content/generated/fuchsia-route-pokemon/source-fixtures.json':canonical(fixtures)}

def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--project-root',type=pathlib.Path,default=ROOT);ap.add_argument('--reference-root',type=pathlib.Path);ap.add_argument('--check',action='store_true');args=ap.parse_args()
 if args.reference_root:authenticated(args.project_root,args.reference_root)
 files=generate(args.project_root)
 if args.check:
  for rel,b in files.items():
   if safe(args.project_root,rel).read_bytes()!=b:raise ValueError('Generated bytes differ: '+rel)
 else:write_files(args.project_root,files)
 for rel,b in files.items():print(len(b),rel)
if __name__=='__main__':main()
