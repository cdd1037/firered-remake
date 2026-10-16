#!/usr/bin/env python3
"""Offline exact Safari land species metadata; source projection is checked against canonical386."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,json,pathlib,re,sys
sys.dont_write_bytecode=True
from import_canonical_pokemon import Sources
from pokemon_import_io import safe, load_recipe, read_sources, write_files
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-safari-pokemon-v1'
SOURCE_COUNT=51
SPECIES=('CHANSEY','SCYTHER','TAUROS')
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def require(ok,message):
 if not ok:raise ValueError(message)
def authenticated(root,reference=None):
 lock=load_recipe(safe(root,'scripts/recipes/pokemon/safari-pokemon.lock.json'),commit=COMMIT,profile=PROFILE)
 if (lock['schemaVersion'],lock['repository'],len(lock['files']))!=(1,'pret/pokefirered',SOURCE_COUNT):raise ValueError('Safari source scope differs')
 raw_root=safe(root,'content/raw/pokefirered/037335f4')
 raw=read_sources(lock,raw_root,reference)
 if reference is not None:write_files(raw_root,raw)
 return lock,raw

def generate(root,dependency_root=None):
 lock,raw=authenticated(root);texts={p:b.decode() for p,b in raw.items() if not p.endswith(('.png','.wav'))};inv={r['path']:r for r in lock['files']}
 src=Sources(texts,{})
 one,ref,const,fields,name,learn=src.one,src.match_ref,src.constant,src.block,src.name,src.learnset
 def ident(s):return dict(value='pret:'+s)
 def number(path,s):return const(path,s)['value']
 def move(s):
  f,r=fields('src/data/battle_moves.h',s);return ident(s)
 species=[];held=[];machines=[];rates=[];evolutions=[];required=set();names=[];constants=set();cries=[]
 for short in SPECIES:
  s='SPECIES_'+short;definition,f=src.species_definition(s);lr=definition['levelUpMoves'];required.update(r['move']['value'][5:] for r in lr)
  abilities=[definition['ability1'],definition['ability2']]
  species.append(definition)
  held.append(dict(species=ident(s),common=f['itemCommon'],rare=f['itemRare']));rates.append(dict(species=ident(s),catchRate=int(f['catchRate']),fleeRate=int(f['safariZoneFleeRate'])));names.append(name('src/data/text/species_names.h',s))
  p='src/data/pokemon/tmhm_learnsets.h';m=one(p,r'^    \['+s+r'\]\s*= TMHM_LEARNSET\((.*?)\),');machines.append(dict(species=ident(s),machineSymbols=re.findall(r'TMHM\((\w+)\)',m[1])))
  ep='src/data/pokemon/evolution.h';em=re.search(r'^    \['+s+r'\][ \t]*= \{\{(EVO_\w+), (\w+), (SPECIES_\w+)\}\},$',texts[ep],re.M)
  original=[]
  if em:
   method,param,target=em.groups();original=[dict(method=method, sourceMethodId=number('include/constants/pokemon.h', method), parameter=param, parameterValue=int(param) if param.isdigit() else number('include/constants/items.h', param), target=ident(target))]
  evolutions.append(dict(species=ident(s),sourceEvolutions=original))
  constants|={('include/constants/species.h',s),*[('include/constants/abilities.h',a) for a in abilities],*[('include/constants/items.h',i) for i in (f['itemCommon'],f['itemRare'])]}
  cp='sound/cry_tables.inc';cm=one(cp,r'^\tcry Cry_'+short.title()+r'$');tm=one(cp,r'^\tcry_reverse Cry_'+short.title()+r'$');dp='sound/direct_sound_data.inc';dm=one(dp,r'^Cry_'+short.title()+r'::\n\t.incbin "(sound/direct_sound_samples/cries/'+short.lower()+r'\.bin)"');sample='sound/direct_sound_samples/cries/'+short.lower()+'.wav'
  cries.append(dict(species=ident(s),sourceSpeciesId=species[-1]['sourceSpeciesId'],cueId='CRY_'+short+'_NORMAL',normalTableIndex=species[-1]['sourceSpeciesId']-1,sourceSymbol='Cry_'+short.title(),sourceSamplePath=sample,normalSource=ref(cp,'gCryTable['+str(species[-1]['sourceSpeciesId']-1)+']',cm),reverseSource=ref(cp,'gCryTable_Reverse['+str(species[-1]['sourceSpeciesId']-1)+']',tm),sampleBindingSource=ref(dp,'Cry_'+short.title(),dm)))
 dep=pathlib.Path(dependency_root or root);proof=safe(dep,'content/generated/data/pokemon/canonical/catalog.json').read_bytes();proof=json.loads(proof)
 for s in species:
  require(s==next(x for x in proof['species'] if x['id']==s['id']),'Source projection differs from canonical386: '+s['id']['value'])
  sm=next(x for x in proof['metadata'] if x['id']==s['id']);require(next(x['sourceEvolutions'] for x in evolutions if x['species']==s['id'])==sm['sourceEvolutions'],'Source evolution differs')
 constants|={('include/constants/moves.h',m) for m in required}
 constants.add(('include/constants/hold_effects.h','HOLD_EFFECT_LUCKY_EGG'))
 encsource=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'));encoded=[]
 for n in names:
  codes=[int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n['name']+'$'];encoded.append(dict(symbol=n['source']['symbol'],name=n['name'],bytes=codes,source=n['source'],encodingSource=encsource))
 doc=dict(schemaVersion=1, species=[dict(id=row['id'], evolutions=row['evolutions']) for row in species], speciesNames=names, constants=[dict(symbol=row['symbol'], value=row['value']) for row in [const(*x) for x in sorted(constants)]], encodedNames=encoded, requiredMoves=[ident(m) for m in sorted(required)], naturalLevelLimit=None, heldItems=held, machines=machines, safariRates=rates, sourceEvolutions=evolutions, cries=cries)
 wild=json.loads(texts['src/data/wild_encounters.json']);encounters=[]
 for group in wild['wild_encounter_groups']:
  for table in group.get('encounters',[]):
   if table.get('map','').startswith('MAP_SAFARI_ZONE_') and table.get('base_label','').endswith('_FireRed'):
    for slot,row in enumerate(table['land_mons']['mons']):
     if row['species'][8:] in SPECIES:
      s=next(s for s in species if s['id']==ident(row['species']));encounters.append(dict(map=table['map'],baseLabel=table['base_label'],slot=slot,species=s['id'],minLevel=row['min_level'],maxLevel=row['max_level']))
 require(len(encounters)==6,'Safari new-species encounter closure differs')
 # Validate required source moves without emitting unused proof fixtures.
 for symbol in sorted(required):move(symbol)
 return {'content/generated/data/pokemon/safari.json':canonical(doc)}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',type=pathlib.Path);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference_root:authenticated(a.project_root,a.reference_root)
 files=generate(a.project_root,a.dependency_root)
 if a.check:
  for rel,b in files.items():require(safe(a.project_root,rel).read_bytes()==b,'Generated bytes differ: '+rel)
 else:write_files(a.project_root,files)
 for rel,b in files.items():print(len(b),rel)
if __name__=='__main__':main()
