#!/usr/bin/env python3
"""Offline exact Safari land species metadata; source projection is checked against canonical386."""
import argparse,hashlib,json,pathlib,re,subprocess,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-safari-pokemon-v1'
LOCK_SHA='573fc41300b93c5e68af2b86b536907efe83985d2fba139412f3e4374835b668'
SOURCE_COUNT=51
SPECIES=('CHANSEY','SCYTHER','TAUROS')
CANONICAL_SHA='c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc'
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def require(ok,message):
 if not ok:raise ValueError(message)
def safe(root,rel):
 require(not pathlib.PurePosixPath(rel).is_absolute() and '\\' not in rel and not any(x in ('','.','..') for x in rel.split('/')),'Unsafe relative path')
 p=pathlib.Path(root).absolute()/rel
 require(not any(x.is_symlink() for x in (p,*p.parents)),'Symlink rejected')
 return p
def authenticated(root,reference=None):
 b=safe(root,'scripts/safari-pokemon.lock.json').read_bytes();require(sha(b)==LOCK_SHA,'Safari source lock differs');lock=json.loads(b)
 require((lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'],lock['profile'],len(lock['files']))==(1,'pret/pokefirered',COMMIT,TREE,PROFILE,SOURCE_COUNT),'Safari source scope differs')
 if reference:require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==COMMIT,'Reference HEAD differs')
 raw={}
 for row in lock['files']:
  p=safe(root,'content/raw/safari-pokemon/'+row['path'])
  b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+row['path']]) if reference else p.read_bytes()
  require((len(b),sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())==(row['byteLength'],row['sha256'],row['gitBlobSha1']),'Source bytes differ: '+row['path']);raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,raw

def generate(root,dependency_root=None):
 lock,raw=authenticated(root);texts={p:b.decode() for p,b in raw.items() if not p.endswith(('.png','.wav'))};inv={r['path']:r for r in lock['files']}
 def one(path,pattern):
  ms=list(re.finditer(pattern,texts[path],re.M|re.S));require(len(ms)==1,f'Expected one source match: {path}: {pattern}');return ms[0]
 def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=texts[path].count('\n',0,m.start())+1,endLine=texts[path].count('\n',0,m.end())+1,sha256=inv[path]['sha256'])
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
  cries.append(dict(species=ident(s),sourceSpeciesId=species[-1]['sourceSpeciesId'],cueId='CRY_'+short+'_NORMAL',normalTableIndex=species[-1]['sourceSpeciesId']-1,sourceSymbol='Cry_'+short.title(),sourceSamplePath=sample,sourceSampleSha256=inv[sample]['sha256'],normalSource=ref(cp,'gCryTable['+str(species[-1]['sourceSpeciesId']-1)+']',cm),reverseSource=ref(cp,'gCryTable_Reverse['+str(species[-1]['sourceSpeciesId']-1)+']',tm),sampleBindingSource=ref(dp,'Cry_'+short.title(),dm)))
 dep=pathlib.Path(dependency_root or root);proof=safe(dep,'content/generated/canonical-pokemon/catalog.json').read_bytes();require(len(proof)==1929796 and sha(proof)==CANONICAL_SHA,'Canonical386 proof differs');proof=json.loads(proof)
 for s in species:
  require(s==next(x for x in proof['species'] if x['id']==s['id']),'Source projection differs from canonical386: '+s['id']['value'])
  sm=next(x for x in proof['metadata'] if x['id']==s['id']);require(next(x['sourceEvolutions'] for x in evolutions if x['species']==s['id'])==sm['sourceEvolutions'],'Source evolution differs')
 constants|={('include/constants/moves.h',m) for m in required}
 constants.add(('include/constants/hold_effects.h','HOLD_EFFECT_LUCKY_EGG'))
 encsource=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'));encoded=[]
 for n in names:
  codes=[int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n['name']+'$'];encoded.append(dict(symbol=n['source']['symbol'],name=n['name'],bytes=codes,source=n['source'],encodingSource=encsource))
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],species=species,speciesNames=names,constants=[const(*x) for x in sorted(constants)],encodedNames=encoded,requiredMoves=[ident(m) for m in sorted(required)],naturalLevelLimit=None,heldItems=held,machines=machines,safariRates=rates,sourceEvolutions=evolutions,cries=cries,canonicalCatalogSha256=CANONICAL_SHA,limitations=['Only Chansey, Scyther and Tauros are added; water/fishing Psyduck, Dratini and Dragonair are deferred.','All natural level-up moves are preserved through level100; existing354 authenticated move definitions are required.','Blissey friendship and Scizor Metal Coat trade evolutions are preserved separately, not executed or replaced.','Metadata, machines, held-item slots and cry references do not enable gameplay or audio by themselves.'])
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
   if table.get('map','').startswith('MAP_SAFARI_ZONE_') and table.get('base_label','').endswith('_FireRed'):
    for slot,row in enumerate(table['land_mons']['mons']):
     if row['species'][8:] in SPECIES:
      s=next(s for s in species if s['id']==ident(row['species']));encounters.append(dict(map=table['map'],baseLabel=table['base_label'],slot=slot,species=s['id'],minLevel=row['min_level'],maxLevel=row['max_level'],initialMoves=initial(s,row['min_level'])))
 require(len(encounters)==6,'Safari new-species encounter closure differs')
 fixture=dict(schemaVersion=1,sourceCommit=COMMIT,initialMoves=fixtures,wildInitialMoves=encounters,requiredMoveDefinitions=[move(s) for s in sorted(required)],heldItems=held,safariRates=rates)
 return {'content/generated/safari-pokemon/catalog.json':canonical(doc),'content/generated/safari-pokemon/source-fixtures.json':canonical(fixture)}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',type=pathlib.Path);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference_root:authenticated(a.project_root,a.reference_root)
 for rel,b in generate(a.project_root,a.dependency_root).items():
  path=safe(a.project_root,rel)
  if a.check:require(path.read_bytes()==b,'Generated bytes differ: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
  print(sha(b),len(b),rel)
if __name__=='__main__':main()
