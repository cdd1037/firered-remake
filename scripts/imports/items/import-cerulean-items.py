#!/usr/bin/env python3
"""Bounded Cerulean Mart and Super Potion from authenticated local Git blobs or cache. No upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from item_catalog import item_json

import argparse, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT/'scripts/lib'))
from item_icon_exports import item_icon_path, output_materials
from image_io import indexed_png, png
from item_source import ItemSource
from ui_tiles import palette, rgba
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-cerulean-items-v1'
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
 b=safe(path or ROOT/'scripts/recipes/items/cerulean-items.lock.json').read_bytes()
 d=json.loads(b,object_pairs_hook=pairs)
 if (d['schemaVersion'], d['repository'], d['commit'], d['profile'], len(d['files'])) != (1, 'pret/pokefirered', COMMIT, PROFILE, 14): raise ValueError('Source identity differs')
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
  if len(ms)!=1:raise ValueError('Nonunique source match: '+p+' '+pat)
  return ms[0]
 def ref(p,s,m=None):
  return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,
   endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 symbol='ITEM_SUPER_POTION';p='src/data/items.json'
 items=json.loads(text[p],object_pairs_hook=pairs)['items'];chosen=[i for i in items if i['itemId']==symbol]
 if len(chosen)!=1:raise ValueError('Nonunique Super Potion')
 i=chosen[0];m=match(p,r'^    \{\n      "english": "SUPER POTION",.*?^    \}')
 source=ref(p,symbol,m)
 cp='include/constants/items.h';cm=match(cp,r'^#define ITEM_SUPER_POTION[ \t]+(\d+)[ \t]*$');rules.append(ref(cp,symbol,cm))
 definition=ItemSource(data).definition(symbol)
 if (definition['sourceItemId'],definition['price'],definition['holdEffectParam'])!=(22,700,50):raise ValueError('Super Potion values differ')
 declaration=i['description_english'].replace('\\n','\n')
 p='include/constants/item_effects.h';m=match(p,r'^#define ITEM4_HEAL_HP[ \t]+(0x[0-9A-Fa-f]+|\d+)[ \t]*(?://[^\n]*)?$');flag=int(m[1],0);rules.append(ref(p,'ITEM4_HEAL_HP',m))
 p='src/data/pokemon/item_effects.h';m=match(p,r'^static const u8 sItemEffect_SuperPotion\[7\] = \{\n(.*?)^\};');effect_source=ref(p,'sItemEffect_SuperPotion',m);effect=[0]*7;seen=set()
 for line in m[1].splitlines():
  line=line.split('//')[0].strip()
  if not line:continue
  part=re.fullmatch(r'\[(\d+)\] = (ITEM4_HEAL_HP|\d+),',line)
  if part is None:raise ValueError('Unsupported Super Potion effect expression')
  index=int(part[1])
  if index in seen or not 0<=index<7:raise ValueError('Invalid effect index')
  seen.add(index);effect[index]=flag if part[2]=='ITEM4_HEAL_HP' else int(part[2])
 if effect!=[0,0,0,0,4,0,50]:raise ValueError('Super Potion healing bytes differ')
 m=match(p,r'^    \[ITEM_SUPER_POTION - ITEM_POTION\][ \t]*= sItemEffect_SuperPotion,');rules.append(ref(p,'ITEM_SUPER_POTION effect binding',m))
 item=dict(metadata=dict(item=definition,description=declaration), effectBytes=effect, useSupport='potion')
 p='data/maps/CeruleanCity_Mart/scripts.inc';m=match(p,r'^CeruleanCity_Mart_Items::\n((?:\t\.2byte ITEM_\w+\n)+)');stock=re.findall(r'\.2byte (ITEM_\w+)',m[1]);expected=['ITEM_'+n for n in 'POKE_BALL SUPER_POTION POTION ANTIDOTE PARALYZE_HEAL AWAKENING BURN_HEAL ESCAPE_ROPE REPEL NONE'.split()]
 if stock!=expected:raise ValueError('Cerulean source stock/order/terminator differs')
 shop=dict(id='CeruleanCity_Mart_Items',items=[ident(x) for x in stock[:-1]])
 m=match(p,r'^CeruleanCity_Mart_EventScript_Clerk::\n(.*?)(?=^\t\.align)')
 if '\tpokemart CeruleanCity_Mart_Items\n' not in m[1]:raise ValueError('Cerulean clerk binding differs')
 rules.append(ref(p,'CeruleanCity_Mart_EventScript_Clerk',m))
 image,palette_path=ItemSource(data).icon_sources(symbol);w,h,idx,_=indexed_png(data[image]);pixels=rgba(idx,palette(data[palette_path]))
 if (w,h)!=(24,24):raise ValueError('Super Potion icon dimensions differ')
 image_bytes=png(w,h,pixels);png_name=item_icon_path(image,palette_path)
 icon=dict(item=ident(symbol), width=w, height=h, pngPath=png_name)
 for p,s,pat in [('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}'),('src/item_use.c','FieldUseFunc_Medicine',r'^void FieldUseFunc_Medicine\([^;]*?\n\{.*?^\}'),('src/item_use.c','BattleUseFunc_Medicine',r'^void BattleUseFunc_Medicine\([^;]*?\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 doc=dict(schemaVersion=1,item=item,shop=shop,icon=icon)
 outputs={png_name:image_bytes,'cerulean-items.json':runtime_json_bytes(doc, default=item_json)}
 outputs['manifest.json']=encoded(dict(schemaVersion=1,inputCount=14,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
 return outputs

def run(raw,out,repo=None,lock_path=None):
 raw,out=directory(raw),directory(out)
 if raw==out or raw in out.parents or out in raw.parents:raise ValueError('Raw/output overlap')
 if repo is not None:
  r=safe(repo)
  if any(p==r or p in r.parents or r in p.parents for p in [raw,out]):raise ValueError('Reference/output overlap')
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
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/items/cerulean');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/items/cerulean-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True, default=item_json))
