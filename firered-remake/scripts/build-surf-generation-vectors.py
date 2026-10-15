#!/usr/bin/env python3
"""Independent Python transcription of pinned water slot/level and ordinary PID/IV draws.
This creates fixtures, not evidence that the C# implementation or original binary passed.
"""
import argparse,importlib.util,json,pathlib,re,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('surf_meta',ROOT/'scripts/import-surf-pokemon.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
def build(root):
 _,raw=meta.authenticated(root);doc=json.loads((root/'content/generated/surf-encounters/catalog.json').read_bytes())
 tables={t['map']['value']:t for t in doc['tables']};catalog=json.loads((root/'content/generated/surf-pokemon/catalog.json').read_bytes())
 def next_value(seed):seed=(seed*0x41c64e6d+0x6073)&0xffffffff;return seed,seed>>16
 def find(slot,levelroll,bound):
  for seed in range(100000):
   s,r=next_value(seed);s,l=next_value(s)
   if (0 if r%100<60 else 1 if r%100<90 else 2 if r%100<95 else 3 if r%100<99 else 4)==slot and l%bound==levelroll:return seed
  raise ValueError('No seed')
 fixtures=[]
 for mapid,slot,levelroll in [('map.pallet-town',0,0),('map.celadon-city',4,10),('map.safari-zone-west',4,5),('map.fuchsia-city',1,3)]:
  table=tables[mapid];mon=table['slots'][slot];lo,hi=mon['minimumLevel'],mon['maximumLevel'];seed=find(slot,levelroll,hi-lo+1);initial=seed;cursor=0;draws=[]
  def draw(purpose,bound=65536):
   nonlocal seed,cursor
   before=seed;seed,raw=next_value(seed);cursor+=1;value=raw%bound;draws.append(dict(cursor=cursor,purpose=purpose,before=before,after=seed,raw=raw,bound=bound,result=value));return value
  roll=draw('encounter.water-slot',100);level=lo+draw('encounter.level',hi-lo+1);selection=draws.copy();nature=draw('wild.nature',25);attempt=0
  while True:
   attempt+=1;low=draw('wild.personality.'+str(attempt)+'.low16');high=draw('wild.personality.'+str(attempt)+'.high16');pid=low|(high<<16)
   if pid%25==nature:break
  a=draw('wild.ivs.hp-attack-defense');b=draw('wild.ivs.speed-specialAttack-specialDefense');postgeneration=seed;generationcursor=cursor;generation=draws[2:].copy();ivs=[a&31,(a>>5)&31,(a>>10)&31,b&31,(b>>5)&31,(b>>10)&31];heldroll=draw('wild-battle.held-item',100)
  symbol=mon['species']['value'][5:];text=raw['src/data/pokemon/species_info.h'].decode();match=re.search(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',text,re.M|re.S);fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',match[1],re.M))
  bases=[int(fields['base'+name]) for name in ('HP','Attack','Defense','Speed','SpAttack','SpDefense')];stats=[(2*bases[0]+ivs[0])*level//100+level+10]
  for i in range(1,6):
   value=(2*bases[i]+ivs[i])*level//100+5
   if nature//5!=nature%5:
    if i-1==nature//5:value=value*110//100
    elif i-1==nature%5:value=value*90//100
   stats.append(value)
  ptr=re.search(r'^    \['+symbol+r'\] = (\w+),$',raw['src/data/pokemon/level_up_learnset_pointers.h'].decode(),re.M)[1];learn=re.search(r'^static const u16 '+ptr+r'\[\] = \{\n(.*?)^\};',raw['src/data/pokemon/level_up_learnsets.h'].decode(),re.M|re.S)[1];moves=[]
  for lv,move in re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)',learn):
   if int(lv)>level:break
   if move not in moves:moves=(moves+[move])[-4:]
  abilities=re.findall(r'ABILITY_\w+',fields['abilities']);ability=abilities[pid&1] if abilities[1]!='ABILITY_NONE' else abilities[0]
  experience=level**3 if fields['growthRate']=='GROWTH_MEDIUM_FAST' else 5*level**3//4
  meta.require(fields['growthRate'] in ('GROWTH_MEDIUM_FAST','GROWTH_SLOW'),'Fixture growth not transcribed')
  fixtures.append(dict(map=mapid,statistics=stats,moves=['pret:'+m for m in moves],ability=ability,experience=experience,initialGlobalSeed=initial,initialRateSeed=0,initialSteps=8,previousBehavior=21,behavior=21,rateThreshold=table['rawEncounterRate']*16,rateSeedAfter=12345,rateCursorAfter=1,slot=slot,species=mon['species']['value'],level=level,selectionTrace=selection,personality=pid,nature=nature,ivs=ivs,globalSeedAfterGeneration=postgeneration,globalCursorAfterGeneration=generationcursor,generationTrace=generation,heldItem='ITEM_NONE',heldRoll=heldroll,heldTrace=draws[-1],globalSeedAfterHeldItem=seed,globalCursorAfterHeldItem=cursor))
 return meta.canonical(dict(schemaVersion=1,sourceCommit=meta.COMMIT,derivation='Independent Python transcription, not original-C differential execution',sourceFiles=['src/wild_encounter.c','src/data/wild_encounters.json'],vectors=fixtures))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();b=build(a.project_root);target=a.project_root/'tests/Game.SurfContent.Tests/source-generation-vectors.json'
 if a.check:meta.require(target.read_bytes()==b,'Generation vectors differ')
 else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(len(b))
