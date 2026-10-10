#!/usr/bin/env python3
"""Authenticated S.S. Anne item declarations, exact ship pickups, icons and berry pocket semantics, from pinned Git blobs or cache. No upstream execution."""
import argparse, base64, hashlib, importlib.util, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-ssanne-items-v1'
LOCK_SHA256 = '13eb19643061145dcefa8d1d2453c11a5235968c6452298adbaf9a83c61151ce'
INPUT_COUNT = 66
MANIFEST_SHA256 = 'a7602ccf9ab22c2c85dc3fa71bdc712b9583139619296388cd5cad7dc2d4c2b8'
def sha(b): return hashlib.sha256(b).hexdigest()
def encoded(v): return (json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode()
def pairs(rows):
 d={}
 for k,v in rows:
  if k in d: raise ValueError('Duplicate JSON member: '+k)
  d[k]=v
 return d
def safe(root,p=None):
 if p is not None and (not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/'))): raise ValueError('Unsafe relative path')
 if '..' in pathlib.Path(root).parts: raise ValueError('Parent traversal root rejected')
 value=pathlib.Path(root).absolute() if p is None else pathlib.Path(root).absolute()/p
 for q in [value,*value.parents]:
  if q.is_symlink(): raise ValueError('Symlink path rejected')
 return value
def directory(p):
 p=safe(p)
 for q in [p,*p.parents]:
  if q.exists() and not q.is_dir(): raise ValueError('Non-directory path')
 return p
def git(repo,*args): return subprocess.check_output(['git','-C',str(repo),*args])
def lock_read(path=None):
 b=safe(path or ROOT/'scripts/ssanne-items.lock.json').read_bytes()
 if sha(b)!=LOCK_SHA256: raise ValueError('Reviewed S.S. Anne items lock differs')
 d=json.loads(b,object_pairs_hook=pairs)
 if (d['schemaVersion'],d['repository'],d['commit'],d['tree'],d['profile'],len(d['files']))!=(1,'pret/pokefirered',COMMIT,TREE,PROFILE,INPUT_COUNT): raise ValueError('Source identity differs')
 return d
def verify(e,b):
 if len(b)!=e['byteLength'] or sha(b)!=e['sha256'] or hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()!=e['gitBlobSha1']: raise ValueError('Source bytes differ: '+e['path'])
def source_hash(lock): return sha(''.join(e['path']+'\0'+e['sha256']+'\n' for e in lock['files']).encode())
def raw_manifest(lock): return encoded(dict(profile=PROFILE,sourceCommit=COMMIT,tree=TREE,lockSha256=LOCK_SHA256,fullSourceHash=source_hash(lock),inputCount=INPUT_COUNT))
def read_sources(lock,raw,repo=None):
 data={}
 if repo is None:
  if safe(raw,'manifest.json').read_bytes()!=raw_manifest(lock): raise ValueError('Raw manifest differs')
 else:
  safe(repo)
  if git(repo,'rev-parse','HEAD').decode().strip()!=COMMIT or git(repo,'rev-parse',COMMIT+'^{tree}').decode().strip()!=TREE: raise ValueError('Reference revision differs')
 for e in lock['files']:
  if repo is None: b=safe(raw,e['path']).read_bytes()
  else:
   if git(repo,'ls-tree',COMMIT,'--',e['path']).decode()!='100644 blob '+e['gitBlobSha1']+'\t'+e['path']+'\n': raise ValueError('Source not pinned regular blob')
   b=git(repo,'cat-file','blob',e['gitBlobSha1'])
  verify(e,b);data[e['path']]=b
 return data

ITEMS = 'CHERI_BERRY CHESTO_BERRY ETHER GREAT_BALL HM01 HYPER_POTION LAVA_COOKIE PECHA_BERRY SS_TICKET STARDUST SUPER_POTION TM31 TM44 X_ATTACK BERRY_POUCH'.split()

MAPS = ['SSAnne_1F_Corridor', 'SSAnne_1F_Room1', 'SSAnne_1F_Room2', 'SSAnne_1F_Room3', 'SSAnne_1F_Room4', 'SSAnne_1F_Room5', 'SSAnne_1F_Room6', 'SSAnne_1F_Room7', 'SSAnne_2F_Corridor', 'SSAnne_2F_Room1', 'SSAnne_2F_Room2', 'SSAnne_2F_Room3', 'SSAnne_2F_Room4', 'SSAnne_2F_Room5', 'SSAnne_2F_Room6', 'SSAnne_3F_Corridor', 'SSAnne_B1F_Corridor', 'SSAnne_B1F_Room1', 'SSAnne_B1F_Room2', 'SSAnne_B1F_Room3', 'SSAnne_B1F_Room4', 'SSAnne_B1F_Room5', 'SSAnne_CaptainsOffice', 'SSAnne_Deck', 'SSAnne_Exterior', 'SSAnne_Kitchen']

def convert(lock,data):
 text={p:b.decode() for p,b in data.items() if not p.endswith('.png')}; inputs={e['path']:e for e in lock['files']};rules=[]
 def match(p,pat):
  ms=list(re.finditer(pat,text[p],re.M|re.S))
  if len(ms)!=1:raise ValueError('Nonunique source match: '+p+' '+pat)
  return ms[0]
 def ref(p,s,m=None):
  return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,
   endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1,sha256=inputs[p]['sha256'])
 def ident(s):return dict(value='pret:'+s)
 def constant(p,s):
  m=match(p,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');rules.append(ref(p,s,m));return int(m[1])
 all_items=json.loads(text['src/data/items.json'],object_pairs_hook=pairs)['items']
 codec_path=ROOT/'scripts/import-lab-battle-ui.py'
 if sha(safe(codec_path).read_bytes())!='b03954e07eb38a9b2dee1f98cc8ed9f92a1f35b32fe9197a16795db4e527795a':raise ValueError('Reviewed local icon codec differs')
 spec=importlib.util.spec_from_file_location('ssanne_items_existing_png_codec',codec_path);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 effect_constants={m[1]:m[2].split('//')[0].strip() for m in re.finditer(r'^#define (ITEM[3456]_\w+)[ \t]+([^\n]+)',text['include/constants/item_effects.h'],re.M)}
 def expression(e):
  e=e.strip()
  if e.startswith('(') and e.endswith(')'):return expression(e[1:-1])
  if e in effect_constants:return expression(effect_constants[e])
  if '|' in e:
   v=0
   for part in e.split('|'):v|=expression(part)
   return v
  if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',e):return int(e,0)
  raise ValueError('Unsupported effect expression: '+e)
 def effect(name):
  p='src/data/pokemon/item_effects.h';m=match(p,r'^static const u8 sItemEffect_'+name+r'\[(\d+)\] = \{\n(.*?)^\};');arr=[0]*int(m[1]);seen=set()
  body=m[2]
  if 'STAT_BOOST_FRIENDSHIP_CHANGE' in body:
   macro=match(p,r'^#define STAT_BOOST_FRIENDSHIP_CHANGE[^\n]*\n(.*?)(?=^#define|^$)')
   rules.append(ref(p,'STAT_BOOST_FRIENDSHIP_CHANGE',macro))
   body=body.replace('STAT_BOOST_FRIENDSHIP_CHANGE',macro[1].replace('\\',''))
  for line in body.splitlines():
   line=re.sub(r'/\*.*?\*/','',line.split('//')[0]).strip()
   if not line:continue
   part=re.fullmatch(r'\[(\d+)\] = ([^,]+),?',line)
   if part is None:raise ValueError('Unsupported effect syntax')
   index=int(part[1]);value=expression(part[2])
   if index in seen or not 0<=index<len(arr) or not 0<=value<=255:raise ValueError('Invalid effect index/value')
   seen.add(index);arr[index]=value
  return arr,ref(p,'sItemEffect_'+name,m)
 effects={'ETHER':'Ether','SUPER_POTION':'SuperPotion','HYPER_POTION':'HyperPotion','LAVA_COOKIE':'LavaCookie','PECHA_BERRY':'PechaBerry','CHERI_BERRY':'CheriBerry','CHESTO_BERRY':'ChestoBerry','X_ATTACK':'XAttack'}
 defs=[];icons=[];outputs={}
 for name in ITEMS:
  symbol='ITEM_'+name;chosen=[i for i in all_items if i['itemId']==symbol]
  if len(chosen)!=1:raise ValueError('Nonunique item')
  i=chosen[0];p='src/data/items.json';m=match(p,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}');source=ref(p,symbol,m)
  definition=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=constant('include/constants/items.h',symbol),name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=source)
  declaration=i['description_english'].replace('\\n','\n');effect_bytes,effect_source=effect(effects[name]) if name in effects else ([],None)
  if name in effects:
   p='src/data/pokemon/item_effects.h';em=match(p,r'^    \['+symbol+r' - ITEM_POTION\][ \t]*= sItemEffect_'+effects[name]+',');rules.append(ref(p,symbol+' effect binding',em))
  defs.append(dict(metadata=dict(item=definition,description=declaration,declarationDescription=declaration,descriptionSource=source),effectBytes=effect_bytes,effectSource=effect_source))
  p='src/data/item_icon_table.h';m=match(p,r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,m);symbols=m.groups();paths=[]
  for sym in symbols:
   p='src/data/graphics/items.h';g=match(p,r'^const u32 '+sym+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,sym,g));paths.append(g[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image,palette=paths;w,h,idx,_=codec.indexed_png(data[image]);rgba=codec.rgba(idx,codec.palette(data[palette]))
  if (w,h)!=(24,24):raise ValueError('Item icon dimensions differ')
  png=codec.png(w,h,rgba);png_name=symbol.lower()+'.png';outputs[png_name]=png
  icons.append(dict(item=ident(symbol),width=w,height=h,rgbaBase64=base64.b64encode(rgba).decode(),rgbaSha256=sha(rgba),pngPath=png_name,pngSha256=sha(png),source=ref(image,symbols[0]),paletteSource=ref(palette,symbols[1]),bindingSource=binding))
 pickups=[]
 for map_name in MAPS:
  p='data/maps/'+map_name+'/map.json';d=json.loads(text[p],object_pairs_hook=pairs)
  for n,o in enumerate(d['object_events']):
   if o.get('graphics_id')!='OBJ_EVENT_GFX_ITEM_BALL':continue
   sp='data/scripts/item_ball_scripts.inc';m=match(sp,r'^'+o['script']+r'::\n\tfinditem (ITEM_\w+)\n\tend$')
   pickups.append(dict(map=map_name,kind='visible',item=ident(m[1]),quantity=1,localObjectId=n+1,x=o['x'],y=o['y'],elevation=o['elevation'],flag=o['flag'],underfoot=False,script=o['script'],source=ref(p,o['flag']),scriptSource=ref(sp,o['script'],m)))
  for o in d['bg_events']:
   if o['type']!='hidden_item':continue
   pickups.append(dict(map=map_name,kind='hidden',item=ident(o['item']),quantity=o['quantity'],localObjectId=None,x=o['x'],y=o['y'],elevation=o['elevation'],flag=o['flag'],underfoot=o['underfoot'],script=None,source=ref(p,o['flag']),scriptSource=None))
 if len(pickups)!=12 or sum(x['kind']=='hidden' for x in pickups)!=5 or any(x['quantity']!=1 for x in pickups):raise ValueError('Pickup scope differs')
 for p,s,pat in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','CheckBagHasSpace',r'^bool8 CheckBagHasSpace\(.*?^\}'),('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pat)))
 if constant('include/constants/global.h','BAG_BERRIES_COUNT')!=43:raise ValueError('Berry pocket capacity differs')
 for p in ['include/constants/item_effects.h','src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],rulesSources=rules,items=defs[:14],berryPouch=defs[14],icons=icons,pickups=pickups,limitations=[
  'Fourteen exact ship item declarations and one source Berry Pouch dependency; twelve pickups retain original coordinates, flags and local IDs.',
  'Great Ball capture, X Attack battle use and Lava Cookie all-status/confusion use remain deferred; their original metadata and effect bytes are authenticated without claiming execution.',
  'Ship berry storage uses the original 43-slot pocket and automatic Berry Pouch insertion. Application owns FLAG_SYS_GOT_BERRY_POUCH.',
  'Exterior Lava Cookie remains unreachable until source Surf access is supported; its declaration does not unlock water access.',
  'Source imports and pure catalog composition do not imply playable/native/audio acceptance.'])
 outputs['ssanne-items.json']=encoded(doc)
 outputs['manifest.json']=encoded(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,tree=TREE,lockSha256=LOCK_SHA256,fullSourceHash=source_hash(lock),inputCount=INPUT_COUNT,outputs=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(outputs.items())]))
 return outputs

def run(raw,out,repo=None,lock_path=None):
 raw,out=directory(raw),directory(out)
 if raw==out or raw in out.parents or out in raw.parents:raise ValueError('Raw/output overlap')
 if repo is not None:
  r=safe(repo)
  if any(p==r or p in r.parents or r in p.parents for p in [raw,out]):raise ValueError('Reference/output overlap')
 for kind in ['raw','generated']:
  parent=ROOT/'content'/kind
  for protected in parent.iterdir():
   if protected.name=='ssanne-items': continue
   p=protected.absolute()
   if any(q==p or q in p.parents or p in q.parents for q in [raw,out]):raise ValueError('Legacy/dependency output overlap')
 lock=lock_read(lock_path);data=read_sources(lock,raw,repo);outputs=convert(lock,data)
 if sha(outputs['manifest.json'])!=MANIFEST_SHA256:raise ValueError('Reviewed generated hashes differ')
 materials={(out,p):b for p,b in outputs.items()}
 if repo is not None:materials.update({(raw,p):b for p,b in data.items()});materials[(raw,'manifest.json')]=raw_manifest(lock)
 targets=[]
 for (root,p),b in materials.items():
  target=safe(root,p);stage=safe(root,p+'.tmp');directory(target.parent)
  if any(q.exists() and not q.is_file() for q in [target,stage]):raise ValueError('Non-file target')
  targets.append((target,stage,b))
 for target,stage,b in targets:
  target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(b);stage.replace(target)
 return dict(inputCount=len(data),outputCount=len(outputs),catalogSha256=sha(outputs['ssanne-items.json']),manifestSha256=sha(outputs['manifest.json']),fullSourceHash=source_hash(lock))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/ssanne-items');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/ssanne-items');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/ssanne-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True))
