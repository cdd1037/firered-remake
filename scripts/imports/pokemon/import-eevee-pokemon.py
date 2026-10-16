#!/usr/bin/env python3
"""Rebuild Eevee metadata offline from pinned source; retain all five evolution rows."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse,json,pathlib,re,sys
sys.dont_write_bytecode=True
from import_canonical_pokemon import Sources
from pokemon_import_io import safe, load_recipe, read_sources, write_files
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-eevee-pokemon-v1'
NEW_MOVES=('MOVE_BATON_PASS',)
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def authenticated(root):
 lock=load_recipe(safe(root,'scripts/recipes/pokemon/eevee-pokemon.lock.json'),commit=COMMIT)
 if (lock['schemaVersion'],lock['repository'],len(lock['files']))!=(1,'pret/pokefirered',33):raise ValueError('Eevee source scope differs')
 return lock,read_sources(lock,safe(root,'content/raw/pokefirered/037335f4'))

def generate(root):
 lock,raw=authenticated(root);texts={p:b.decode() for p,b in raw.items() if not p.endswith('.png')};inventory={r['path']:r for r in lock['files']}
 src=Sources(texts,{})
 one,ref,constant,block,name=src.one,src.match_ref,src.constant,src.block,src.name
 def ident(s):return dict(value='pret:'+s)
 symbol='SPECIES_EEVEE';species,fields=src.species_definition(symbol)
 assert fields['genderRatio']=='PERCENT_FEMALE(12.5)' and fields['growthRate']=='GROWTH_MEDIUM_FAST' and fields['types']=='{TYPE_NORMAL, TYPE_NORMAL}' and fields['abilities']=='{ABILITY_RUN_AWAY, ABILITY_NONE}'
 level_moves=species['levelUpMoves'];required={row['move']['value'][5:] for row in level_moves}
 assert [(m['level'],m['move']['value'][10:]) for m in level_moves]==[(1,'TACKLE'),(1,'TAIL_WHIP'),(1,'HELPING_HAND'),(8,'SAND_ATTACK'),(16,'GROWL'),(23,'QUICK_ATTACK'),(30,'BITE'),(36,'BATON_PASS'),(42,'TAKE_DOWN')]
 definitions=[]
 for symbol in sorted(required):
  f,r=block('src/data/battle_moves.h',symbol)
  definitions.append(ident(symbol))
 moves=[m for m in definitions if m['value'][5:] in NEW_MOVES]
 names=[name('src/data/text/species_names.h','SPECIES_EEVEE')];move_names=[name('src/data/text/move_names.h',s) for s in NEW_MOVES]
 encoding=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'));encoded=[]
 for n in names+move_names:
  codes=[int(one('charmap.txt',r"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n['name']+'$']
  encoded.append(dict(symbol=n['source']['symbol'],name=n['name'],bytes=codes,source=n['source'],encodingSource=encoding))
 path='src/data/pokemon/evolution.h';ev=one(path,r'^    \[SPECIES_EEVEE\]\s*= (.*?\}\}),');deferred=[]
 for method,param,target in re.findall(r'\{(EVO_\w+), (\w+), (SPECIES_\w+)\}',ev[1]):
  reason='Target and stone-use services are outside this gift profile.' if method=='EVO_ITEM' else 'FireRed has no RTC; src/pokemon.c explicitly disables day/night friendship evolution.'
  deferred.append(dict(species=ident('SPECIES_EEVEE'), method=method, parameter=param, target=ident(target), executable=False, reason=reason))
 assert len(deferred)==5
 gate=one('src/pokemon.c',r'            // FR/LG removed the time of day evolutions due to having no RTC\..*?            case EVO_LEVEL:')
 assert '/*' in gate[0] and gate[0].count('targetSpecies =')==2
 constants=[constant('include/constants/species.h',s) for s in ('SPECIES_EEVEE','SPECIES_JOLTEON','SPECIES_VAPOREON','SPECIES_FLAREON','SPECIES_ESPEON','SPECIES_UMBREON')]+[constant('include/constants/moves.h',s) for s in sorted(required)]+[constant('include/constants/abilities.h','ABILITY_RUN_AWAY')]+[constant('include/constants/items.h',s) for s in ('ITEM_THUNDER_STONE','ITEM_WATER_STONE','ITEM_FIRE_STONE')]
 doc=dict(schemaVersion=1, species=[dict(id=species['id'], evolutions=species['evolutions'])], moves=moves, speciesNames=names, moveNames=[dict(id=row['id'], name=row['name']) for row in move_names], constants=[dict(symbol=row['symbol'], value=row['value']) for row in constants], encodedNames=encoded, deferredEvolutions=deferred, requiredMoves=[ident(s) for s in sorted(required)], naturalLevelLimit=None)
 tm_path='src/data/pokemon/tmhm_learnsets.h';tm=one(tm_path,r'^    \[SPECIES_EEVEE\]\s*= TMHM_LEARNSET\((.*?)\),');machine_symbols=re.findall(r'TMHM\((\w+)\)',tm[1])
 machine=dict(species='pret:SPECIES_EEVEE',sourceSpeciesId=133,machineSymbols=machine_symbols)
 fixtures=[]
 for level in (1,8,16,23,25,30,36,42,100):
  learned=[]
  for row in level_moves:
   move=row['move']['value']
   if row['level']<=level and move not in learned:learned=(learned+[move])[-4:]
  fixtures.append(dict(level=level,moves=learned))
 fixture=dict(schemaVersion=1, expectedSpeciesDelta=1, expectedMoveDelta=1, expectedCampaignSpecies=103, expectedCampaignMoves=241, naturalMoves=fixtures, requiredMoveDefinitions=definitions, initialLevel25Pp=[20, 15, 40, 30], machines=machine, friendshipGate=ref('src/pokemon.c', 'GetEvolutionTargetSpecies', gate))
 return {'content/generated/data/pokemon/eevee.json':runtime_json_bytes(doc, sort_keys=True),'tests/fixtures/eevee/catalog-source-fixtures.json':canonical(fixture),'docs/eevee-catalog-delta.json':canonical(dict(schemaVersion=1, newSpecies=[133], newMoves=list(NEW_MOVES), deferredEvolutions=deferred, machines=machine, friendshipGate=fixture['friendshipGate']))}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
 files=generate(a.project_root)
 if a.check:
  for rel,b in files.items():
   if safe(a.project_root,rel).read_bytes()!=b:raise ValueError('Generated bytes differ: '+rel)
 else:write_files(a.project_root,files)
 for rel,b in files.items():print(len(b),rel)
if __name__=='__main__':main()
