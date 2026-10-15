#!/usr/bin/env python3
"""Bounded MtMoon items/TMs from authenticated local Git blobs or cache. No upstream execution."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse, base64,  importlib.util, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
import sys
sys.path.insert(0, str(ROOT / "scripts"))
from machine_catalog import load_catalog, machine_reference, compatibility_reference
sys.path.insert(0, str(ROOT/'scripts'))
from item_icon_exports import item_icon_path, output_materials
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-mtmoon-items-v1'
ITEMS = 'REVIVE ETHER RARE_CANDY MOON_STONE TINY_MUSHROOM BIG_MUSHROOM STAR_PIECE HELIX_FOSSIL DOME_FOSSIL TM05 TM09 TM46'.split()
SPECIES = ('BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE '
 'CATERPIE METAPOD BUTTERFREE WEEDLE KAKUNA BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE '
 'PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX SPEAROW FEAROW MANKEY '
 'EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF MAGIKARP GYARADOS '
 'ARBOK CLEFAIRY ZUBAT GOLBAT ODDISH PARAS PARASECT BELLSPROUT MAGNEMITE GRIMER VOLTORB KOFFING NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF').split()
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
 b=safe(path or ROOT/'scripts/mtmoon-items.lock.json').read_bytes()
 d=json.loads(b,object_pairs_hook=pairs)
 if (d['schemaVersion'], d['repository'], d['commit'], d['profile'], len(d['files'])) != (1, 'pret/pokefirered', COMMIT, PROFILE, 45): raise ValueError('Source identity differs')
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

def convert(lock,data):
 text={p:b.decode() for p,b in data.items() if not p.endswith('.png')}; inputs={e['path']:e for e in lock['files']};rules=[]
 def match(p,pat):
  ms=list(re.finditer(pat,text[p],re.M|re.S))
  if len(ms)!=1: raise ValueError('Nonunique source match: '+p+' '+pat)
  return ms[0]
 def ref(p,s,m=None):
  return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,
   endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1)
 def constant(p,s):
  m=match(p,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');rules.append(ref(p,s,m));return int(m[1])
 def ident(s):return dict(value='pret:'+s)
 all_items=json.loads(text['src/data/items.json'],object_pairs_hook=pairs)['items']
 defs=[];icons=[];outputs={};machines=[]
 codec_spec=importlib.util.spec_from_file_location('mtmoon_items_existing_png_codec',ROOT/'scripts/import-lab-battle-ui.py')
 codec=importlib.util.module_from_spec(codec_spec);codec_spec.loader.exec_module(codec)
 effect_constants={m[1]:m[2].split('//')[0].strip() for m in re.finditer(r'^#define (ITEM[3456]_\w+)[ \t]+([^\n]+)',text['include/constants/item_effects.h'],re.M)}
 def expression(e):
  e=e.strip()
  if e in effect_constants:return expression(effect_constants[e])
  if re.fullmatch(r'\(\(u8\) -\d+\)',e):return int(re.search(r'-\d+',e)[0])&255
  if e.startswith('(') and e.endswith(')'):return expression(e[1:-1])
  if '|' in e:
   v=0
   for part in e.split('|'):v|=expression(part)
   return v
  if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',e):return int(e,0)
  raise ValueError('Unsupported effect expression: '+e)
 def effect(name):
  p='src/data/pokemon/item_effects.h';m=match(p,r'^static const u8 sItemEffect_'+name+r'\[(\d+)\] = \{\n(.*?)^\};')
  arr=[0]*int(m[1]);seen=set()
  for raw in m[2].splitlines():
   line=raw.split('//')[0].strip()
   if not line:continue
   part=re.fullmatch(r'\[(\d+)\] = (.*),',line)
   if part:
    index=int(part[1]);v=expression(part[2])
    if index in seen or not 0<=index<len(arr):raise ValueError('Invalid effect index')
    arr[index]=v;seen.add(index)
   else:
    vm=re.fullmatch(r'VITAMIN_FRIENDSHIP_CHANGE\((\d+)\),',line)
    if not vm:raise ValueError('Unsupported effect array syntax')
    macro=match(p,r'^#define VITAMIN_FRIENDSHIP_CHANGE\(i\)[^\n]*(?:\n[^\n]+){3}')
    vals=re.findall(r'\[\(i\) \+ (\d+)\]\s*=\s*(\d+)' ,macro[0])
    if len(vals)!=3:raise ValueError('Friendship macro shape differs')
    for off,value in vals:arr[int(vm[1])+int(off or '0')]=int(value)
    rules.append(ref(p,'VITAMIN_FRIENDSHIP_CHANGE',macro))
  return arr,ref(p,'sItemEffect_'+name,m)
 effects={'REVIVE':('Revive','fieldRevive'),'ETHER':('Ether','fieldEther'),'RARE_CANDY':('RareCandy','deferredRareCandy'),'MOON_STONE':('MoonStone','separateEvolution')}
 for name in ITEMS:
  symbol='ITEM_'+name;i=next(i for i in all_items if i['itemId']==symbol)
  p='src/data/items.json';m=match(p,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}')
  source=ref(p,symbol,m);definition=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=constant('include/constants/items.h',symbol),
   name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),
   holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],
   battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=source)
  declaration=i['description_english'].replace('\\n','\n');description=declaration;description_source=source
  effect_bytes,effect_source=effect(effects[name][0]) if name in effects else ([],None)
  support=effects[name][1] if name in effects else ('fossil' if 'FOSSIL' in name else 'noFieldUse')
  if name.startswith('TM'):
   p='src/move_descriptions.c';m=match(p,r'^const u8 gMoveDescription_'+i['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
   description=json.loads(m[1]);description_source=ref(p,'gMoveDescription_'+i['moveId'],m)
  wrapped=dict(item=definition,description=description,declarationDescription=declaration,descriptionSource=description_source)
  defs.append(dict(metadata=wrapped,effectBytes=effect_bytes,effectSource=effect_source,useSupport=support))
  p='src/data/item_icon_table.h';m=match(p,r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,m);paths=[]
  for sym in m.groups():
   p='src/data/graphics/items.h';g=match(p,r'^const u32 '+sym+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,sym,g));paths.append(g[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image,palette=paths;w,h,idx,_=codec.indexed_png(data[image]);rgba=codec.rgba(idx,codec.palette(data[palette]))
  if (w,h)!=(24,24):raise ValueError('Icon size differs')
  png=codec.png(w,h,rgba);file=item_icon_path(image,palette);outputs[file]=png
  icons.append(dict(item=ident(symbol),width=w,height=h,pngPath=file,source=ref(image,m[1]),paletteSource=ref(palette,m[2]),bindingSource=binding))
 shared = load_catalog(ROOT)
 def compatibility(tm):
  return [compatibility_reference(shared, 'pret:ITEM_' + tm[:4], 'pret:SPECIES_' + species) for species in SPECIES]
 machines = [machine_reference(shared, 'pret:ITEM_' + item, ['pret:SPECIES_' + species for species in SPECIES]) for item in ['TM05','TM09','TM46']]
 pickups=[]
 for map_name in ['MtMoon_1F','MtMoon_B1F','MtMoon_B2F','Route4']:
  p='data/maps/'+map_name+'/map.json';d=json.loads(text[p],object_pairs_hook=pairs)
  for n,o in enumerate(d['object_events']):
   if o.get('graphics_id')!='OBJ_EVENT_GFX_ITEM_BALL':continue
   sp='data/scripts/item_ball_scripts.inc';m=match(sp,r'^'+o['script']+r'::\n\tfinditem (ITEM_\w+)\n\tend$')
   pickups.append(dict(map=map_name,kind='visible',item=ident(m[1]),quantity=1,localObjectId=n+1,x=o['x'],y=o['y'],elevation=o['elevation'],flag=o['flag'],underfoot=False,script=o['script'],source=ref(p,o['flag']),scriptSource=ref(sp,o['script'],m)))
  if map_name=='Route4':continue
  for o in d['bg_events']:
   if o['type']!='hidden_item':continue
   pickups.append(dict(map=map_name,kind='hidden',item=ident(o['item']),quantity=o['quantity'],localObjectId=None,x=o['x'],y=o['y'],elevation=o['elevation'],flag=o['flag'],underfoot=o['underfoot'],script=None,source=ref(p,o['flag']),scriptSource=None))
 if len(pickups)!=19 or sum(x['kind']=='hidden' for x in pickups)!=8 or any(x['quantity']!=1 for x in pickups):raise ValueError('Pickup scope differs')
 fossils=[];p='data/maps/MtMoon_B2F/scripts.inc'
 for name in ['Dome','Helix']:
  label='MtMoon_B2F_EventScript_'+name+'Fossil';m=match(p,r'^'+label+r'::\n(.*?)(?=^\w+::|\Z)');item='ITEM_'+name.upper()+'_FOSSIL'
  if '\tgiveitem_msg MtMoon_B2F_Text_Obtained'+name+'Fossil, '+item+', 1, MUS_OBTAIN_KEY_ITEM\n' not in m[1]:raise ValueError('Fossil reward differs')
  fossils.append(dict(item=ident(item),script=label,source=ref(p,label,m)))
 for p,s,pat in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','CheckBagHasSpace',r'^bool8 CheckBagHasSpace\(.*?^\}'),('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}'),('src/pokemon.c','AdjustFriendship',r'^void AdjustFriendship\([^;]*?\n\{.*?^\}'),('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['include/constants/item_effects.h','src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],items=defs,machines=machines,tm39Compatibility=compatibility('TM39_ROCK_TOMB'),icons=icons,pickups=pickups,fossils=fossils,
  limitations=['Eighteen MtMoon pickups, two mutually exclusive fossil dependencies, and Route4 east TM05 only; application owns scripts and flags.', 'Nine additions to the original eight-item shop catalog preserve original stock; three TMs remain in the machine pocket.', 'Rare Candy and Escape Rope execution remains deferred; Moon Stone execution is a separately integrated Domain transaction.', 'Revive and Ether pure field use only; no new battle item routing.', 'Four bounded TMs with exact 56-species compatibility; no other TM/HM or fossil revival.', 'Source imports and pure rules do not imply playable/native/audio acceptance.'])
 outputs['mtmoon-items.json']=encoded(doc)
 outputs['manifest.json']=encoded(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=45,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
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
   if protected.name=='mtmoon-items': continue
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
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/mtmoon-items');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/mtmoon-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True))
