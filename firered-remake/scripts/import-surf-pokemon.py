#!/usr/bin/env python3
"""Offline exact Surf land species metadata; source projection is checked against canonical386."""
import argparse,json,pathlib,re,subprocess,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-surf-pokemon-v1'
SOURCE_COUNT=46
SPECIES=('PSYDUCK','GOLDUCK')
WATER_MAPS={'sCeladonCity_FireRed': ('map.celadon-city', 'MAP_CELADON_CITY'), 'sCeruleanCity_FireRed': ('map.cerulean-city', 'MAP_CERULEAN_CITY'), 'sFuchsiaCity_FireRed': ('map.fuchsia-city', 'MAP_FUCHSIA_CITY'), 'sPalletTown_FireRed': ('map.pallet-town', 'MAP_PALLET_TOWN'), 'sRoute10_FireRed': ('map.route-10', 'MAP_ROUTE10'), 'sRoute11_FireRed': ('map.route-11', 'MAP_ROUTE11'), 'sRoute12_FireRed': ('map.route-12', 'MAP_ROUTE12'), 'sRoute13_FireRed': ('map.route-13', 'MAP_ROUTE13'), 'sRoute22_FireRed': ('map.route-22', 'MAP_ROUTE22'), 'sRoute23_FireRed': ('map.route-23', 'MAP_ROUTE23'), 'sRoute24_FireRed': ('map.route-24', 'MAP_ROUTE24'), 'sRoute25_FireRed': ('map.route-25', 'MAP_ROUTE25'), 'sRoute4_FireRed': ('map.route-4', 'MAP_ROUTE4'), 'sRoute6_FireRed': ('map.route-6', 'MAP_ROUTE6'), 'sSafariZoneCenter_FireRed': ('map.safari-zone-center', 'MAP_SAFARI_ZONE_CENTER'), 'sSafariZoneEast_FireRed': ('map.safari-zone-east', 'MAP_SAFARI_ZONE_EAST'), 'sSafariZoneNorth_FireRed': ('map.safari-zone-north', 'MAP_SAFARI_ZONE_NORTH'), 'sSafariZoneWest_FireRed': ('map.safari-zone-west', 'MAP_SAFARI_ZONE_WEST'), 'sSSAnneExterior_FireRed': ('map.ssanne-exterior', 'MAP_SSANNE_EXTERIOR'), 'sVermilionCity_FireRed': ('map.vermilion-city', 'MAP_VERMILION_CITY'), 'sViridianCity_FireRed': ('map.viridian-city', 'MAP_VIRIDIAN_CITY')}
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def require(ok,message):
 if not ok:raise ValueError(message)
def safe(root,rel):
 require(not pathlib.PurePosixPath(rel).is_absolute() and '\\' not in rel and not any(x in ('','.','..') for x in rel.split('/')),'Unsafe relative path')
 p=pathlib.Path(root).absolute()/rel
 require(not any(x.is_symlink() for x in (p,*p.parents)),'Symlink rejected')
 return p
def authenticated(root,reference=None):
 b=safe(root,'scripts/surf-pokemon.lock.json').read_bytes();lock=json.loads(b)
 require((lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], len(lock['files'])) == (1, 'pret/pokefirered', COMMIT, PROFILE, SOURCE_COUNT),'Surf source scope differs')
 if reference:require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==COMMIT,'Reference HEAD differs')
 raw={}
 for row in lock['files']:
  p=safe(root,'content/raw/pokefirered/037335f4/'+row['path'])
  b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+row['path']]) if reference else p.read_bytes()
  raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,raw

def generate(root,dependency_root=None):
 lock,raw=authenticated(root);texts={p:b.decode() for p,b in raw.items() if not p.endswith(('.png','.wav'))};inv={r['path']:r for r in lock['files']}
 def one(path,pattern):
  ms=list(re.finditer(pattern,texts[path],re.M|re.S));require(len(ms)==1,f'Expected one source match: {path}: {pattern}');return ms[0]
 def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=texts[path].count('\n',0,m.start())+1,endLine=texts[path].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 def const(path,s):
  m=one(path,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');return dict(symbol=s,value=int(m[1]),source=ref(path,s,m))
 def number(path,s):return const(path,s)['value']
 def fields(path,s):
  m=one(path,r'^    \['+s+r'\] =\s*\n    \{\n(.*?)^    \},');return dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M)),ref(path,s,m)
 def name(path,s):
  m=one(path,r'^    \['+s+r'\]\s*= _\("([^"\n]+)"\),$');return dict(id=ident(s),name=m[1],source=ref(path,s,m))
 def learn(s):
  pointer=one('src/data/pokemon/level_up_learnset_pointers.h',r'^    \['+s+r'\] = (\w+),$')[1];p='src/data/pokemon/level_up_learnsets.h';m=one(p,r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};');rows=[]
  require('#' not in m[1],'Unexpected conditional learnset')
  for row in re.finditer(r'^    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),$',texts[p],re.M):
   if m.start(1)<=row.start()<m.end(1):rows.append(dict(level=int(row[1]),move=ident(row[2]),source=ref(p,pointer,row)))
  require(len(rows)==m[1].count('LEVEL_UP_MOVE(') and '    LEVEL_UP_END\n' in m[1] and rows,'Incomplete learnset')
  return rows,ref(p,pointer,m)
 def move(s):
  f,r=fields('src/data/battle_moves.h',s);return dict(id=ident(s),sourceMoveId=number('include/constants/moves.h',s),pp=int(f['pp']),power=int(f['power']),type=f['type'],accuracy=int(f['accuracy']),effect=f['effect'],secondaryEffectChance=int(f['secondaryEffectChance']),target=f['target'],priority=int(f['priority']),flags=[] if f['flags']=='0' else f['flags'].split(' | '),battleEffectImplemented=False,source=r)
 species=[];held=[];machines=[];rates=[];evolutions=[];required=set();names=[];constants=set();cries=[]
 for short in SPECIES:
  s='SPECIES_'+short;f,sr=fields('src/data/pokemon/species_info.h',s);lr,lref=learn(s);required.update(r['move']['value'][5:] for r in lr)
  statkeys=('hp','attack','defense','speed','specialAttack','specialDefense');suffix=('HP','Attack','Defense','Speed','SpAttack','SpDefense');types=re.findall(r'TYPE_\w+',f['types']);abilities=re.findall(r'ABILITY_\w+',f['abilities'])
  species.append(dict(id=ident(s),sourceSpeciesId=number('include/constants/species.h',s),baseStats={k:int(f['base'+v]) for k,v in zip(statkeys,suffix)},type1=types[0],type2=types[1],catchRate=int(f['catchRate']),experienceYield=int(f['expYield']),evYield={k:int(f['evYield_'+v]) for k,v in zip(statkeys,suffix)},growthRate={'GROWTH_FAST':'fast','GROWTH_MEDIUM_FAST':'mediumFast','GROWTH_SLOW':'slow'}[f['growthRate']],genderRatio={'MON_FEMALE':254,'MON_MALE':0,'PERCENT_FEMALE(50)':127}[f['genderRatio']],ability1=abilities[0],ability2=abilities[1],friendship=int(f['friendship']),levelUpMoves=lr,evolutions=[],source=sr,learnsetSource=lref))
  held.append(dict(species=ident(s),common=f['itemCommon'],rare=f['itemRare'],source=sr));rates.append(dict(species=ident(s),catchRate=int(f['catchRate']),fleeRate=int(f['safariZoneFleeRate']),source=sr));names.append(name('src/data/text/species_names.h',s))
  p='src/data/pokemon/tmhm_learnsets.h';m=one(p,r'^    \['+s+r'\]\s*= TMHM_LEARNSET\((.*?)\),');machines.append(dict(species=ident(s),machineSymbols=re.findall(r'TMHM\((\w+)\)',m[1]),source=ref(p,s,m)))
  ep='src/data/pokemon/evolution.h';em=re.search(r'^    \['+s+r'\][ \t]*= \{\{(EVO_\w+), (\w+), (SPECIES_\w+)\}\},$',texts[ep],re.M)
  original=[]
  if em:
   method,param,target=em.groups();original=[dict(method=method,sourceMethodId=number('include/constants/pokemon.h',method),parameter=param,parameterValue=int(param) if param.isdigit() else number('include/constants/items.h',param),target=ident(target),source=ref(ep,s,em))]
  evolutions.append(dict(species=ident(s),sourceEvolutions=original))
  constants|={('include/constants/species.h',s),*[('include/constants/abilities.h',a) for a in abilities],*[('include/constants/items.h',i) for i in (f['itemCommon'],f['itemRare'])]}
  cp='sound/cry_tables.inc';cm=one(cp,r'^\tcry Cry_'+short.title()+r'$');tm=one(cp,r'^\tcry_reverse Cry_'+short.title()+r'$');dp='sound/direct_sound_data.inc';dm=one(dp,r'^Cry_'+short.title()+r'::\n\t.incbin "(sound/direct_sound_samples/cries/'+short.lower()+r'\.bin)"');sample='sound/direct_sound_samples/cries/'+short.lower()+'.wav'
  cries.append(dict(species=ident(s),sourceSpeciesId=species[-1]['sourceSpeciesId'],cueId='CRY_'+short+'_NORMAL',normalTableIndex=species[-1]['sourceSpeciesId']-1,sourceSymbol='Cry_'+short.title(),sourceSamplePath=sample,normalSource=ref(cp,'gCryTable['+str(species[-1]['sourceSpeciesId']-1)+']',cm),reverseSource=ref(cp,'gCryTable_Reverse['+str(species[-1]['sourceSpeciesId']-1)+']',tm),sampleBindingSource=ref(dp,'Cry_'+short.title(),dm)))
 dep=pathlib.Path(dependency_root or root);proof=safe(dep,'content/generated/canonical-pokemon/catalog.json').read_bytes();require(len(proof) == 1929796,'Canonical386 proof differs');proof=json.loads(proof)
 for s in species:
  require(s==next(x for x in proof['species'] if x['id']==s['id']),'Source projection differs from canonical386: '+s['id']['value'])
  sm=next(x for x in proof['metadata'] if x['id']==s['id']);require(next(x['sourceEvolutions'] for x in evolutions if x['species']==s['id'])==sm['sourceEvolutions'],'Source evolution differs')
 constants|={('include/constants/moves.h',m) for m in required}

 encsource=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'));encoded=[]
 for n in names:
  codes=[int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n['name']+'$'];encoded.append(dict(symbol=n['source']['symbol'],name=n['name'],bytes=codes,source=n['source'],encodingSource=encsource))
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],species=species,speciesNames=names,constants=[const(*x) for x in sorted(constants)],encodedNames=encoded,requiredMoves=[ident(m) for m in sorted(required)],naturalLevelLimit=None,heldItems=held,machines=machines,surfRates=rates,sourceEvolutions=evolutions,cries=cries,limitations=['Only Psyduck and its natural level33 Golduck evolution are added; Dratini/Dragonair fishing is deferred.','All natural level-up moves are preserved through level100; existing354 authenticated move definitions are required.','Psyduck source EVO_LEVEL33 Golduck is retained as an original triple; the Content projection admits this precise ordinary level evolution.','Metadata, machines, held-item slots and cry references do not enable gameplay or audio by themselves.'])
 fixtures=[]
 def initial(s,lv):
  moves=[]
  for row in s['levelUpMoves']:
   if row['level']>lv:break
   if row['move'] not in moves:moves=(moves+[row['move']])[-4:]
  return moves
 for s in species:
  for lv in range(1,101):fixtures.append(dict(species=s['id'],level=lv,moves=initial(s,lv)))
 wild=json.loads(texts['src/data/wild_encounters.json']);encounters=[]
 for group in wild['wild_encounter_groups']:
  for table in group.get('encounters',[]):
   if table.get('base_label') in WATER_MAPS:
    for slot,row in enumerate(table['water_mons']['mons']):
     if row['species'][8:] in SPECIES:
      s=next(s for s in species if s['id']==ident(row['species']));encounters.append(dict(map=table['map'],baseLabel=table['base_label'],slot=slot,species=s['id'],minLevel=row['min_level'],maxLevel=row['max_level'],initialMoves=initial(s,row['min_level'])))
 require(encounters,'Surf new-species encounter closure missing')
 fixture=dict(schemaVersion=1,sourceCommit=COMMIT,initialMoves=fixtures,wildInitialMoves=encounters,requiredMoveDefinitions=[move(s) for s in sorted(required)],heldItems=held,surfRates=rates)
 return {'content/generated/surf-pokemon/catalog.json':canonical(doc),'content/generated/surf-pokemon/source-fixtures.json':canonical(fixture)}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',type=pathlib.Path);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference_root:authenticated(a.project_root,a.reference_root)
 for rel,b in generate(a.project_root,a.dependency_root).items():
  path=safe(a.project_root,rel)
  if a.check:require(path.read_bytes()==b,'Generated bytes differ: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
  print(len(b),rel)
if __name__=='__main__':main()
