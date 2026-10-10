#!/usr/bin/env python3
"""Rebuild Eevee metadata offline from pinned source; retain all five evolution rows."""
import argparse,hashlib,json,pathlib,re,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-eevee-pokemon-v1'
LOCK_SHA='4d397c4ad8178088e26913b68cc18f9a8c0f830700197bf02577fa7e60090c0c'
NEW_MOVES=('MOVE_BATON_PASS',)
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def read(root,relative):
 p=pathlib.PurePosixPath(relative)
 if p.is_absolute() or '\\' in relative or any(x in ('','.','..') for x in relative.split('/')):raise ValueError('Unsafe source path')
 path=pathlib.Path(root).absolute()/relative
 if any(p.is_symlink() for p in (path,*path.parents)):raise ValueError('Symlink source rejected')
 return path.read_bytes()
def authenticated(root):
 b=read(root,'scripts/eevee-pokemon.lock.json')
 if sha(b)!=LOCK_SHA:raise ValueError('Eevee source lock differs')
 lock=json.loads(b)
 if (lock['schemaVersion'],lock['repository'],lock['commit'],len(lock['files']))!=(1,'pret/pokefirered',COMMIT,33):raise ValueError('Eevee source scope differs')
 source={}
 for row in lock['files']:
  b=read(root,'content/raw/eevee-pokemon/'+row['path'])
  if (len(b),sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())!=(row['byteLength'],row['sha256'],row['gitBlobSha1']):raise ValueError('Authenticated source changed: '+row['path'])
  source[row['path']]=b
 return lock,source

def generate(root):
 lock,raw=authenticated(root);texts={p:b.decode() for p,b in raw.items() if not p.endswith('.png')};inventory={r['path']:r for r in lock['files']}
 def one(path,pattern):
  found=list(re.finditer(pattern,texts[path],re.M|re.S))
  if len(found)!=1:raise ValueError('Expected one source match: '+path+': '+pattern)
  return found[0]
 def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=texts[path].count('\n',0,m.start())+1,endLine=texts[path].count('\n',0,m.end())+1,sha256=inventory[path]['sha256'])
 def constant(path,symbol):
  m=one(path,r'^#define '+symbol+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');return dict(symbol=symbol,value=int(m[1]),source=ref(path,symbol,m))
 def ident(s):return dict(value='pret:'+s)
 def block(path,symbol):
  m=one(path,r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},');return dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M)),ref(path,symbol,m)
 def name(path,symbol):
  m=one(path,r'^    \['+symbol+r'\]\s*= _\("([A-Z ]+)"\),$');return dict(id=ident(symbol),name=m[1],source=ref(path,symbol,m))
 symbol='SPECIES_EEVEE';fields,source=block('src/data/pokemon/species_info.h',symbol)
 assert fields['genderRatio']=='PERCENT_FEMALE(12.5)' and fields['growthRate']=='GROWTH_MEDIUM_FAST' and fields['types']=='{TYPE_NORMAL, TYPE_NORMAL}' and fields['abilities']=='{ABILITY_RUN_AWAY, ABILITY_NONE}'
 pointer=one('src/data/pokemon/level_up_learnset_pointers.h',r'^    \['+symbol+r'\] = (\w+),$')[1]
 path='src/data/pokemon/level_up_learnsets.h';learn=one(path,r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};')
 level_moves=[];required=set()
 for m in re.finditer(r'^    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),\n',texts[path],re.M):
  if learn.start()<=m.start()<learn.end():level_moves.append(dict(level=int(m[1]),move=ident(m[2]),source=ref(path,pointer,m)));required.add(m[2])
 assert [(m['level'],m['move']['value'][10:]) for m in level_moves]==[(1,'TACKLE'),(1,'TAIL_WHIP'),(1,'HELPING_HAND'),(8,'SAND_ATTACK'),(16,'GROWL'),(23,'QUICK_ATTACK'),(30,'BITE'),(36,'BATON_PASS'),(42,'TAKE_DOWN')]
 stat_source=('HP','Attack','Defense','Speed','SpAttack','SpDefense');stat_names=('hp','attack','defense','speed','specialAttack','specialDefense')
 species=dict(id=ident(symbol),sourceSpeciesId=constant('include/constants/species.h',symbol)['value'],baseStats={k:int(fields['base'+s]) for k,s in zip(stat_names,stat_source)},type1='TYPE_NORMAL',type2='TYPE_NORMAL',catchRate=int(fields['catchRate']),experienceYield=int(fields['expYield']),evYield={k:int(fields['evYield_'+s]) for k,s in zip(stat_names,stat_source)},growthRate='mediumFast',genderRatio=31,ability1='ABILITY_RUN_AWAY',ability2='ABILITY_NONE',friendship=int(fields['friendship']),levelUpMoves=level_moves,evolutions=[],source=source,learnsetSource=ref(path,pointer,learn))
 definitions=[]
 for symbol in sorted(required):
  f,r=block('src/data/battle_moves.h',symbol)
  definitions.append(dict(id=ident(symbol),sourceMoveId=constant('include/constants/moves.h',symbol)['value'],pp=int(f['pp']),power=int(f['power']),type=f['type'],accuracy=int(f['accuracy']),effect=f['effect'],secondaryEffectChance=int(f['secondaryEffectChance']),target=f['target'],priority=int(f['priority']),flags=[] if f['flags']=='0' else f['flags'].split(' | '),battleEffectImplemented=False,source=r))
 moves=[m for m in definitions if m['id']['value'][5:] in NEW_MOVES]
 names=[name('src/data/text/species_names.h','SPECIES_EEVEE')];move_names=[name('src/data/text/move_names.h',s) for s in NEW_MOVES]
 encoding=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'));encoded=[]
 for n in names+move_names:
  codes=[int(one('charmap.txt',r"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n['name']+'$']
  encoded.append(dict(symbol=n['source']['symbol'],name=n['name'],bytes=codes,source=n['source'],encodingSource=encoding))
 path='src/data/pokemon/evolution.h';ev=one(path,r'^    \[SPECIES_EEVEE\]\s*= (.*?\}\}),');deferred=[]
 for method,param,target in re.findall(r'\{(EVO_\w+), (\w+), (SPECIES_\w+)\}',ev[1]):
  reason='Target and stone-use services are outside this gift profile.' if method=='EVO_ITEM' else 'FireRed has no RTC; src/pokemon.c explicitly disables day/night friendship evolution.'
  deferred.append(dict(species=ident('SPECIES_EEVEE'),method=method,parameter=param,target=ident(target),source=ref(path,'SPECIES_EEVEE',ev),executable=False,reason=reason))
 assert len(deferred)==5
 gate=one('src/pokemon.c',r'            // FR/LG removed the time of day evolutions due to having no RTC\..*?            case EVO_LEVEL:')
 assert '/*' in gate[0] and gate[0].count('targetSpecies =')==2
 constants=[constant('include/constants/species.h',s) for s in ('SPECIES_EEVEE','SPECIES_JOLTEON','SPECIES_VAPOREON','SPECIES_FLAREON','SPECIES_ESPEON','SPECIES_UMBREON')]+[constant('include/constants/moves.h',s) for s in sorted(required)]+[constant('include/constants/abilities.h','ABILITY_RUN_AWAY')]+[constant('include/constants/items.h',s) for s in ('ITEM_THUNDER_STONE','ITEM_WATER_STONE','ITEM_FIRE_STONE')]
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],species=[species],moves=moves,speciesNames=names,moveNames=move_names,constants=constants,encodedNames=encoded,deferredEvolutions=deferred,requiredMoves=[ident(s) for s in sorted(required)],naturalLevelLimit=None,limitations=['All nine natural learnset entries and all five evolution rows are retained as source metadata.','Baton Pass at level36 and Take Down at level42 remain unsupported battle effects; the current profile requires an explicit decline without replacing moves or spending their PP.','The three Kanto stone evolutions require later species and item-service admission.','Espeon and Umbreon day/night evolution is disabled in FireRed source, regardless of friendship or National Dex.'])
 tm_path='src/data/pokemon/tmhm_learnsets.h';tm=one(tm_path,r'^    \[SPECIES_EEVEE\]\s*= TMHM_LEARNSET\((.*?)\),');machine_symbols=re.findall(r'TMHM\((\w+)\)',tm[1])
 machine=dict(species='pret:SPECIES_EEVEE',sourceSpeciesId=133,machineSymbols=machine_symbols,source=ref(tm_path,'SPECIES_EEVEE',tm))
 fixtures=[]
 for level in (1,8,16,23,25,30,36,42,100):
  learned=[]
  for row in level_moves:
   move=row['move']['value']
   if row['level']<=level and move not in learned:learned=(learned+[move])[-4:]
  fixtures.append(dict(level=level,moves=learned))
 fixture=dict(schemaVersion=1,sourceCommit=COMMIT,expectedSpeciesDelta=1,expectedMoveDelta=1,expectedCampaignSpecies=103,expectedCampaignMoves=241,naturalMoves=fixtures,requiredMoveDefinitions=definitions,initialLevel25Pp=[20,15,40,30],machines=machine,friendshipGate=ref('src/pokemon.c','GetEvolutionTargetSpecies',gate))
 return {'content/generated/eevee-pokemon/catalog.json':canonical(doc),'tests/fixtures/eevee/catalog-source-fixtures.json':canonical(fixture),'docs/eevee-catalog-delta.json':canonical(dict(schemaVersion=1,sourceCommit=COMMIT,newSpecies=[133],newMoves=list(NEW_MOVES),deferredEvolutions=deferred,machines=machine,friendshipGate=fixture['friendshipGate'],limitations=doc['limitations']))}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
 for rel,b in generate(a.project_root).items():
  path=a.project_root/rel
  if a.check:
   if read(a.project_root,rel)!=b:raise ValueError('Generated bytes differ: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
  print(sha(b),len(b),rel)
if __name__=='__main__':main()
