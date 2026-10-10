#!/usr/bin/env python3
"""Rebuild complete Snorlax metadata offline from exact local, source-locked inputs."""
import argparse,hashlib,json,pathlib,re,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-snorlax-pokemon-v1'
LOCK_SHA='f60a52f8f1bd2a1d71529dfb2f02209bedfaef496cdd77cbd0a913741c39780d'
NEW_MOVES=('MOVE_SNORE','MOVE_SLEEP_TALK','MOVE_BLOCK','MOVE_COVET')
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def read(root,relative):
 p=pathlib.PurePosixPath(relative)
 if p.is_absolute() or '\\' in relative or any(x in ('','.','..') for x in relative.split('/')):raise ValueError('Unsafe source path')
 path=pathlib.Path(root).absolute()/relative
 if any(p.is_symlink() for p in (path,*path.parents)):raise ValueError('Symlink source rejected')
 return path.read_bytes()
def authenticated(root):
 b=read(root,'scripts/snorlax-pokemon.lock.json')
 if sha(b)!=LOCK_SHA:raise ValueError('Snorlax source lock differs')
 lock=json.loads(b)
 if (lock['schemaVersion'],lock['repository'],lock['commit'],len(lock['files']))!=(1,'pret/pokefirered',COMMIT,31):raise ValueError('Snorlax source scope differs')
 source={}
 for row in lock['files']:
  b=read(root,'content/raw/snorlax-pokemon/'+row['path'])
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
 symbol='SPECIES_SNORLAX';fields,source=block('src/data/pokemon/species_info.h',symbol)
 assert fields['genderRatio']=='PERCENT_FEMALE(12.5)' and fields['growthRate']=='GROWTH_SLOW' and fields['types']=='{TYPE_NORMAL, TYPE_NORMAL}' and fields['abilities']=='{ABILITY_IMMUNITY, ABILITY_THICK_FAT}'
 pointer=one('src/data/pokemon/level_up_learnset_pointers.h',r'^    \['+symbol+r'\] = (\w+),$')[1]
 path='src/data/pokemon/level_up_learnsets.h';learn=one(path,r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};')
 level_moves=[];required=set()
 for m in re.finditer(r'^    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),\n',texts[path],re.M):
  if learn.start()<=m.start()<learn.end():level_moves.append(dict(level=int(m[1]),move=ident(m[2]),source=ref(path,pointer,m)));required.add(m[2])
 assert len(level_moves)==14 and not re.search(r'\['+symbol+r'\]',texts['src/data/pokemon/evolution.h'])
 stat_source=('HP','Attack','Defense','Speed','SpAttack','SpDefense');stat_names=('hp','attack','defense','speed','specialAttack','specialDefense')
 species=dict(id=ident(symbol),sourceSpeciesId=constant('include/constants/species.h',symbol)['value'],baseStats={k:int(fields['base'+s]) for k,s in zip(stat_names,stat_source)},type1='TYPE_NORMAL',type2='TYPE_NORMAL',catchRate=int(fields['catchRate']),experienceYield=int(fields['expYield']),evYield={k:int(fields['evYield_'+s]) for k,s in zip(stat_names,stat_source)},growthRate='slow',genderRatio=31,ability1='ABILITY_IMMUNITY',ability2='ABILITY_THICK_FAT',friendship=int(fields['friendship']),levelUpMoves=level_moves,evolutions=[],source=source,learnsetSource=ref(path,pointer,learn))
 definitions=[]
 for symbol in sorted(required):
  f,r=block('src/data/battle_moves.h',symbol)
  definitions.append(dict(id=ident(symbol),sourceMoveId=constant('include/constants/moves.h',symbol)['value'],pp=int(f['pp']),power=int(f['power']),type=f['type'],accuracy=int(f['accuracy']),effect=f['effect'],secondaryEffectChance=int(f['secondaryEffectChance']),target=f['target'],priority=int(f['priority']),flags=[] if f['flags']=='0' else f['flags'].split(' | '),battleEffectImplemented=False,source=r))
 moves=[m for m in definitions if m['id']['value'][5:] in NEW_MOVES]
 names=[name('src/data/text/species_names.h','SPECIES_SNORLAX')];move_names=[name('src/data/text/move_names.h',s) for s in NEW_MOVES]
 encoding=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'));encoded=[]
 for n in names+move_names:
  codes=[int(one('charmap.txt',r"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in n['name']+'$']
  encoded.append(dict(symbol=n['source']['symbol'],name=n['name'],bytes=codes,source=n['source'],encodingSource=encoding))
 constants=[constant('include/constants/species.h','SPECIES_SNORLAX')]+[constant('include/constants/moves.h',s) for s in sorted(required)]+[constant('include/constants/abilities.h',s) for s in ('ABILITY_IMMUNITY','ABILITY_THICK_FAT')]+[constant('include/constants/items.h','ITEM_CHESTO_BERRY')]
 held=dict(species='SPECIES_SNORLAX',common=fields['itemCommon'],rare=fields['itemRare'],source=source)
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],species=[species],moves=moves,speciesNames=names,moveNames=move_names,constants=constants,encodedNames=encoded,deferredEvolutions=[],requiredMoves=[ident(s) for s in sorted(required)],naturalLevelLimit=None,limitations=['The entire 14-entry natural learnset is metadata. Battle execution is admitted independently.','Block, Covet, Rollout and Hyper Beam above level40 remain outside this increment; no artificial growth cap is imposed.','Sleep Talk37 requires the new battle executor or an explicit unsupported-effect boundary.','Munchlax does not exist in FireRed; Snorlax has no evolution.'])
 fixtures=[]
 for level in [1,5,9,13,17,21,25,29,30,33,37,40,41,45,49,53,100]:
  learned=[]
  for row in level_moves:
   move=row['move']['value']
   if row['level']<=level and move not in learned:learned=(learned+[move])[-4:]
  fixtures.append(dict(species=species['id']['value'],level=level,moves=learned))
 tm_path='src/data/pokemon/tmhm_learnsets.h';tm=one(tm_path,r'^    \[SPECIES_SNORLAX\]\s*= TMHM_LEARNSET\((.*?)\),');machine_symbols=re.findall(r'TMHM\((\w+)\)',tm[1])
 machine=dict(species='pret:SPECIES_SNORLAX',sourceSpeciesId=143,machineSymbols=machine_symbols,source=ref(tm_path,'SPECIES_SNORLAX',tm))
 fixture=dict(schemaVersion=1,sourceCommit=COMMIT,sourceFiles=lock['files'],expectedSpeciesDelta=1,expectedMoveDelta=4,expectedCampaignSpecies=102,expectedCampaignMoves=240,naturalMoves=fixtures,requiredMoveDefinitions=definitions,initialLevel30Pp=[15,10,10,15],machines=machine)
 items=json.loads(texts['src/data/items.json']);items=items if isinstance(items,list) else items['items']
 audit=dict(schemaVersion=1,sourceCommit=COMMIT,newSpecies=[143],newMoves=list(NEW_MOVES),metadataAlreadyPresent=sorted(required-set(NEW_MOVES)),newAbilities=['ABILITY_IMMUNITY','ABILITY_THICK_FAT'],wildHeldItems=[held],heldItemAdmissions=['ITEM_CHESTO_BERRY'],constants=constants,itemDefinitions=[i for i in items if i['itemId']=='ITEM_CHESTO_BERRY'],machines=machine,limitations=doc['limitations'])
 return {'content/generated/snorlax-pokemon/catalog.json':canonical(doc),'tests/fixtures/snorlax/catalog-source-fixtures.json':canonical(fixture),'docs/snorlax-catalog-delta.json':canonical(audit)}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
 for rel,b in generate(a.project_root).items():
  path=a.project_root/rel
  if a.check:
   if read(a.project_root,rel)!=b:raise ValueError('Generated bytes differ: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
  print(sha(b),len(b),rel)
if __name__=='__main__':main()
