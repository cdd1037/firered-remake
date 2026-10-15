#!/usr/bin/env python3
"""Rebuild complete Snorlax metadata offline from exact local, source-locked inputs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse,json,pathlib,re,sys
sys.dont_write_bytecode=True
from pokemon_import_io import safe, load_recipe, read_sources, write_files
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-snorlax-pokemon-v1'
NEW_MOVES=('MOVE_SNORE','MOVE_SLEEP_TALK','MOVE_BLOCK','MOVE_COVET')
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def authenticated(root):
 lock=load_recipe(safe(root,'scripts/recipes/pokemon/snorlax-pokemon.lock.json'),commit=COMMIT)
 if (lock['schemaVersion'],lock['repository'],len(lock['files']))!=(1,'pret/pokefirered',31):raise ValueError('Snorlax source scope differs')
 return lock,read_sources(lock,safe(root,'content/raw/pokefirered/037335f4'))

def generate(root):
 lock,raw=authenticated(root);texts={p:b.decode() for p,b in raw.items() if not p.endswith('.png')};inventory={r['path']:r for r in lock['files']}
 def one(path,pattern):
  found=list(re.finditer(pattern,texts[path],re.M|re.S))
  if len(found)!=1:raise ValueError('Expected one source match: '+path+': '+pattern)
  return found[0]
 def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=texts[path].count('\n',0,m.start())+1,endLine=texts[path].count('\n',0,m.end())+1)
 def constant(path,symbol):
  m=one(path,r'^#define '+symbol+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');return dict(symbol=symbol,value=int(m[1]),source=ref(path,symbol,m))
 def ident(s):return dict(value='pret:'+s)
 def block(path,symbol):
  m=one(path,r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},');return dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M)),ref(path,symbol,m)
 def name(path,symbol):
  m=one(path,r'^    \['+symbol+r'\]\s*= _\("([A-Z ]+)"\),$');return dict(id=ident(symbol),name=m[1],source=ref(path,symbol,m))
 symbol='SPECIES_SNORLAX';fields,source=block('src/data/pokemon/species_info.h',symbol)
 assert fields['genderRatio']=='PERCENT_FEMALE(12.5)' and fields['growthRate']=='GROWTH_SLOW' and fields['types']=='{TYPE_NORMAL, TYPE_NORMAL}' and fields['abilities']=='{ABILITY_IMMUNITY, ABILITY_THICK_FAT}'
 pointer=one('src/data/pokemon/level_up_learnset_pointers.h',r'^    \['+symbol+r'\] = (\w+),$')[1]
 path='src/data/pokemon/level_up_learnsets.h';learn=one(path,r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};')
 level_moves=[];required=set()
 for m in re.finditer(r'^    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),\n',texts[path],re.M):
  if learn.start()<=m.start()<learn.end():level_moves.append(dict(level=int(m[1]),move=ident(m[2])));required.add(m[2])
 assert len(level_moves)==14 and not re.search(r'\['+symbol+r'\]',texts['src/data/pokemon/evolution.h'])
 stat_source=('HP','Attack','Defense','Speed','SpAttack','SpDefense');stat_names=('hp','attack','defense','speed','specialAttack','specialDefense')
 species=dict(id=ident(symbol), sourceSpeciesId=constant('include/constants/species.h', symbol)['value'], baseStats={k: int(fields['base' + s]) for k, s in zip(stat_names, stat_source)}, type1='TYPE_NORMAL', type2='TYPE_NORMAL', catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']), evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_names, stat_source)}, growthRate='slow', genderRatio=31, ability1='ABILITY_IMMUNITY', ability2='ABILITY_THICK_FAT', friendship=int(fields['friendship']), levelUpMoves=level_moves, evolutions=[])
 definitions=[]
 for symbol in sorted(required):
  f,r=block('src/data/battle_moves.h',symbol)
  definitions.append(ident(symbol))
 moves=[m for m in definitions if m['value'][5:] in NEW_MOVES]
 names=[name('src/data/text/species_names.h','SPECIES_SNORLAX')];move_names=[name('src/data/text/move_names.h',s) for s in NEW_MOVES]
 encoding=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'));encoded=[]
 for n in names+move_names:
  codes=[int(one('charmap.txt',r"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n['name']+'$']
  encoded.append(dict(symbol=n['source']['symbol'],name=n['name'],bytes=codes,source=n['source'],encodingSource=encoding))
 constants=[constant('include/constants/species.h','SPECIES_SNORLAX')]+[constant('include/constants/moves.h',s) for s in sorted(required)]+[constant('include/constants/abilities.h',s) for s in ('ABILITY_IMMUNITY','ABILITY_THICK_FAT')]+[constant('include/constants/items.h','ITEM_CHESTO_BERRY')]
 held=dict(species='SPECIES_SNORLAX',common=fields['itemCommon'],rare=fields['itemRare'])
 doc=dict(schemaVersion=1, species=[dict(id=species['id'], evolutions=species['evolutions'])], moves=moves, speciesNames=names, moveNames=[dict(id=row['id'], name=row['name']) for row in move_names], constants=[dict(symbol=row['symbol'], value=row['value']) for row in constants], encodedNames=encoded, deferredEvolutions=[], requiredMoves=[ident(s) for s in sorted(required)], naturalLevelLimit=None)
 fixtures=[]
 for level in [1,5,9,13,17,21,25,29,30,33,37,40,41,45,49,53,100]:
  learned=[]
  for row in level_moves:
   move=row['move']['value']
   if row['level']<=level and move not in learned:learned=(learned+[move])[-4:]
  fixtures.append(dict(species=species['id']['value'],level=level,moves=learned))
 tm_path='src/data/pokemon/tmhm_learnsets.h';tm=one(tm_path,r'^    \[SPECIES_SNORLAX\]\s*= TMHM_LEARNSET\((.*?)\),');machine_symbols=re.findall(r'TMHM\((\w+)\)',tm[1])
 machine=dict(species='pret:SPECIES_SNORLAX',sourceSpeciesId=143,machineSymbols=machine_symbols)
 fixture=dict(schemaVersion=1, sourceFiles=lock['files'], expectedSpeciesDelta=1, expectedMoveDelta=4, expectedCampaignSpecies=102, expectedCampaignMoves=240, naturalMoves=fixtures, requiredMoveDefinitions=definitions, initialLevel30Pp=[15, 10, 10, 15], machines=machine)
 items=json.loads(texts['src/data/items.json']);items=items if isinstance(items,list) else items['items']
 audit=dict(schemaVersion=1, newSpecies=[143], newMoves=list(NEW_MOVES), metadataAlreadyPresent=sorted(required - set(NEW_MOVES)), newAbilities=['ABILITY_IMMUNITY', 'ABILITY_THICK_FAT'], wildHeldItems=[held], heldItemAdmissions=['ITEM_CHESTO_BERRY'], constants=constants, itemDefinitions=[i for i in items if i['itemId'] == 'ITEM_CHESTO_BERRY'], machines=machine)
 return {'content/generated/data/pokemon/snorlax.json':runtime_json_bytes(doc, sort_keys=True),'tests/fixtures/snorlax/catalog-source-fixtures.json':canonical(fixture),'docs/snorlax-catalog-delta.json':canonical(audit)}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
 files=generate(a.project_root)
 if a.check:
  for rel,b in files.items():
   if safe(a.project_root,rel).read_bytes()!=b:raise ValueError('Generated bytes differ: '+rel)
 else:write_files(a.project_root,files)
 for rel,b in files.items():print(len(b),rel)
if __name__=='__main__':main()
