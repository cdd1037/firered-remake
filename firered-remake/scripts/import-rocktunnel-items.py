#!/usr/bin/env python3
"""Authenticated RockTunnel item declarations, exact ship pickups, icons and berry pocket semantics, from pinned Git blobs or cache. No upstream execution."""
from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse, base64,  importlib.util, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT/'scripts'))
from item_icon_exports import item_icon_path, output_materials
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-rocktunnel-items-v1'
def encoded(v): return (json.dumps(v,ensure_ascii=False,indent=2, default=item_json)+'\n').encode()
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
 b=safe(path or ROOT/'scripts/rocktunnel-items.lock.json').read_bytes()
 d=json.loads(b,object_pairs_hook=pairs)
 if (d['schemaVersion'], d['repository'], d['commit'], d['profile']) != (1, 'pret/pokefirered', COMMIT, PROFILE): raise ValueError('Source identity differs')
 return d
def read_sources(lock,raw,repo=None):
 data={}
 if repo is not None:
     safe(repo)
     if git(repo, 'rev-parse', 'HEAD').decode().strip() != COMMIT:
         raise ValueError('Reference revision differs')
 for e in lock['files']:
  if repo is None: b=safe(raw,e['path']).read_bytes()
  else:
   b=git(repo,'cat-file','blob',COMMIT + ':' + e['path'])
  data[e['path']]=b
 return data

ITEMS = ['MAX_ETHER', 'PERSIM_BERRY', 'NANAB_BERRY', 'REVIVE', 'EVERSTONE', 'PEARL', 'HARD_STONE', 'TM40']

MAPS = ['Route9', 'Route10', 'RockTunnel_1F', 'RockTunnel_B1F']

def convert(lock,data):
 text={p:b.decode() for p,b in data.items() if not p.endswith('.png')}; inputs={e['path']:e for e in lock['files']};rules=[]
 def match(p,pat):
  ms=list(re.finditer(pat,text[p],re.M|re.S))
  if len(ms)!=1:raise ValueError('Nonunique source match: '+p+' '+pat)
  return ms[0]
 def ref(p,s,m=None):
  return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,
   endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 def constant(p,s):
  m=match(p,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');rules.append(ref(p,s,m));return int(m[1])
 all_items=json.loads(text['src/data/items.json'],object_pairs_hook=pairs)['items']
 codec_path=ROOT/'scripts/import-lab-battle-ui.py'
 spec=importlib.util.spec_from_file_location('ssanne_items_existing_png_codec',codec_path);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 effect_constants={m[1]:m[2].split('//')[0].strip() for m in re.finditer(r'^#define (ITEM[3456]_\w+)[ \t]+([^\n]+)',text['include/constants/item_effects.h'],re.M)}
 def expression(e):
  e=e.strip()
  if re.fullmatch(r'\(u8\) -[123]',e):return int(e[5:]) & 255
  if e.startswith('(') and e.endswith(')'):return expression(e[1:-1])
  if e in effect_constants:return expression(effect_constants[e])
  if '<<' in e:
   left,right=e.split('<<')
   return expression(left) << expression(right)
  if '|' in e:
   v=0
   for part in e.split('|'):v|=expression(part)
   return v
  if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',e):return int(e,0)
  raise ValueError('Unsupported effect expression: '+e)
 def effect(name):
  p='src/data/pokemon/item_effects.h';m=match(p,r'^static const u8 sItemEffect_'+name+r'\[(\d+)\] = \{\n(.*?)^\};');arr=[0]*int(m[1]);seen=set()
  body=m[2].replace('STAT_BOOST_FRIENDSHIP_CHANGE,','STAT_BOOST_FRIENDSHIP_CHANGE')
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
 effects={'MAX_ETHER':'MaxEther','PERSIM_BERRY':'PersimBerry','REVIVE':'Revive'}
 defs=[];icons=[];outputs={}
 for name in ITEMS:
  symbol='ITEM_'+name;chosen=[i for i in all_items if i['itemId']==symbol]
  if len(chosen)!=1:raise ValueError('Nonunique item')
  i=chosen[0];p='src/data/items.json';m=match(p,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}');source=ref(p,symbol,m)
  definition=shared_item(symbol)
  declaration=i['description_english'].replace('\\n','\n');effect_bytes,effect_source=effect(effects[name]) if name in effects else ([],None)
  if name in effects:
   p='src/data/pokemon/item_effects.h';em=match(p,r'^    \['+symbol+r' - ITEM_POTION\][ \t]*= sItemEffect_'+effects[name]+',');rules.append(ref(p,symbol+' effect binding',em))
  defs.append(dict(metadata=dict(item=definition,description=declaration), effectBytes=effect_bytes))
  p='src/data/item_icon_table.h';m=match(p,r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,m);symbols=m.groups();paths=[]
  for sym in symbols:
   p='src/data/graphics/items.h';g=match(p,r'^const u32 '+sym+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,sym,g));paths.append(g[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image,palette=paths;w,h,idx,_=codec.indexed_png(data[image]);rgba=codec.rgba(idx,codec.palette(data[palette]))
  if (w,h)!=(24,24):raise ValueError('Item icon dimensions differ')
  png=codec.png(w,h,rgba);png_name=item_icon_path(image,palette);outputs[png_name]=png
  icons.append(dict(item=ident(symbol),width=w,height=h,pngPath=png_name,source=ref(image,symbols[0]),paletteSource=ref(palette,symbols[1]),bindingSource=binding))
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
 if len(pickups)!=15 or sum(x['kind']=='hidden' for x in pickups)!=8 or any(x['quantity']!=1 for x in pickups):raise ValueError('Pickup scope differs')
 for p,s,pat in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','CheckBagHasSpace',r'^bool8 CheckBagHasSpace\(.*?^\}'),('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['include/constants/item_effects.h','src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 for p,s in [('data/maps/Route10_PokemonCenter_1F/scripts.inc','Route10_PokemonCenter_1F_EventScript_Aide'),('data/scripts/aide.inc','Aide shared source'),('src/prof_pc.c','GetPokedexCount'),('src/overworld.c','UpdateEscapeWarp')]:rules.append(ref(p,s))
 aide_text=text['data/maps/Route10_PokemonCenter_1F/scripts.inc']
 if '.equ REQUIRED_OWNED_MONS, 20' not in aide_text or 'checkitemspace ITEM_EVERSTONE' not in aide_text or 'setflag FLAG_GOT_EVERSTONE_FROM_OAKS_AIDE' not in aide_text:raise ValueError('Everstone aide source differs')
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],items=defs[:-1],icons=icons[:-1],pickups=pickups,limitations=[
  'Six additions to the unchanged 32-item Route11 inventory: Max Ether, Persim Berry, Nanab Berry, Everstone, Pearl and Hard Stone. Revive is re-authenticated but inherited with its existing field-use support.',
  'Fifteen pickups preserve original coordinates, flags, quantities, object IDs and scripts; existing Chesto Berry is inherited. No Max Revive pickup exists here.',
  'New active item effects remain unsupported; source effect bytes and held metadata do not enable them. Existing medicine behavior is retained.',
  'The Route10 aide requires 20 caught Kanto species and capacity before its one-time Everstone grant. Application reward integration is separate.',
  'Source imports and catalog composition do not certify application, presentation or audible audio.'])
 outputs['rocktunnel-items.json']=encoded(doc)
 machine_codec=ROOT/'scripts/import-ssanne-machines.py'
 spec=importlib.util.spec_from_file_location('rocktunnel_existing_machine_codec',machine_codec);machines=importlib.util.module_from_spec(spec);spec.loader.exec_module(machines)
 machines.PROFILE='firered-rocktunnel-machines-v1'
 machines.SPECIES += 'DIGLETT DUGTRIO MAGNETON POLIWAG POLIWHIRL VULPIX CUBONE ELECTRODE HYPNO LICKITUNG MACHOKE MAROWAK PRIMEAPE RHYHORN SEADRA SEAKING TENTACRUEL VENOMOTH VENONAT WEEZING'.split()
 machines.MACHINES += [('TM34','SHOCK_WAVE',351,20),('HM05','FLASH',148,20),('TM40','AERIAL_ACE',332,20)]
 machine_outputs=machines.convert(lock,data)
 machine_doc=json.loads(machine_outputs['ssanne-machines.json'],object_pairs_hook=pairs)
 machine_doc['icons']=[icons[-1]]
 machine_doc['limitations']=['Fourteen original machines over exactly 93 species; the previous13 machines and their first79 rows remain unchanged.', 'TM40 Aerial Ace consumes one TM and sets original PP; HM01 and HM05 never consume and remain protected from normal replacement.', 'Source teaching compatibility does not activate field abilities or application rewards.']
 outputs['rocktunnel-machines.json']=encoded(machine_doc)
 outputs['manifest.json']=encoded(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
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
   if protected.name=='rocktunnel-items': continue
   p=protected.absolute()
   if any(q==p or q in p.parents or p in q.parents for q in [raw,out]):raise ValueError('Legacy/dependency output overlap')
 lock=lock_read(lock_path);data=read_sources(lock,raw,repo);outputs=convert(lock,data)
 materials=output_materials(out,outputs)
 if repo is not None:materials.update({(raw,p):b for p,b in data.items()})
 targets=[]
 for (root,p),b in materials.items():
  target=safe(root,p);stage=safe(root,p+'.tmp');directory(target.parent)
  if any(q.exists() and not q.is_file() for q in [target,stage]):raise ValueError('Non-file target')
  targets.append((target,stage,b))
 for target,stage,b in targets:
  target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(b);stage.replace(target)
 return dict(inputCount=len(data),outputCount=len(outputs))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/rocktunnel-items');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/rocktunnel-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True, default=item_json))
