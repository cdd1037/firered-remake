#!/usr/bin/env python3
"""Bounded Cerulean Mart and Super Potion from authenticated local Git blobs or cache. No upstream execution."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse, base64,  importlib.util, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT/'scripts'))
from item_icon_exports import item_icon_path, output_materials
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-cerulean-items-v1'
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
 b=safe(path or ROOT/'scripts/cerulean-items.lock.json').read_bytes()
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
 definition=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=int(cm[1]),name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=source)
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
 item=dict(metadata=dict(item=definition,description=declaration),effectBytes=effect,effectSource=effect_source,useSupport='potion')
 p='data/maps/CeruleanCity_Mart/scripts.inc';m=match(p,r'^CeruleanCity_Mart_Items::\n((?:\t\.2byte ITEM_\w+\n)+)');stock=re.findall(r'\.2byte (ITEM_\w+)',m[1]);expected=['ITEM_'+n for n in 'POKE_BALL SUPER_POTION POTION ANTIDOTE PARALYZE_HEAL AWAKENING BURN_HEAL ESCAPE_ROPE REPEL NONE'.split()]
 if stock!=expected:raise ValueError('Cerulean source stock/order/terminator differs')
 shop=dict(id='CeruleanCity_Mart_Items',items=[ident(x) for x in stock[:-1]],source=ref(p,'CeruleanCity_Mart_Items',m))
 m=match(p,r'^CeruleanCity_Mart_EventScript_Clerk::\n(.*?)(?=^\t\.align)')
 if '\tpokemart CeruleanCity_Mart_Items\n' not in m[1]:raise ValueError('Cerulean clerk binding differs')
 rules.append(ref(p,'CeruleanCity_Mart_EventScript_Clerk',m))
 p='src/data/item_icon_table.h';m=match(p,r'^    \[ITEM_SUPER_POTION\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,m);symbols=m.groups();paths=[]
 for sym in symbols:
  p='src/data/graphics/items.h';g=match(p,r'^const u32 '+sym+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,sym,g));paths.append(g[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
 codec_path=ROOT/'scripts/import-lab-battle-ui.py'
 spec=importlib.util.spec_from_file_location('cerulean_items_existing_png_codec',codec_path);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 image,palette=paths;w,h,idx,_=codec.indexed_png(data[image]);rgba=codec.rgba(idx,codec.palette(data[palette]))
 if (w,h)!=(24,24):raise ValueError('Super Potion icon dimensions differ')
 png=codec.png(w,h,rgba);png_name=item_icon_path(image,palette)
 icon=dict(item=ident(symbol),width=w,height=h,pngPath=png_name,source=ref(image,symbols[0]),paletteSource=ref(palette,symbols[1]),bindingSource=binding)
 for p,s,pat in [('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}'),('src/item_use.c','FieldUseFunc_Medicine',r'^void FieldUseFunc_Medicine\([^;]*?\n\{.*?^\}'),('src/item_use.c','BattleUseFunc_Medicine',r'^void BattleUseFunc_Medicine\([^;]*?\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],item=item,shop=shop,icon=icon,limitations=['Only Super Potion is added to the seventeen-item MtMoon catalog; original two Marts stay unchanged.', 'Cerulean Mart preserves nine source stock entries and the ITEM_NONE terminator.', 'Super Potion restores up to 50 HP to a living injured owned Pokemon; it neither revives nor cures status.', 'No TM03, TM28, new generic item engine, source audio, or native/playable acceptance is claimed.'])
 outputs={png_name:png,'cerulean-items.json':encoded(doc)}
 outputs['manifest.json']=encoded(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=14,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
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
   if protected.name=='cerulean-items': continue
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
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/cerulean-items');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/cerulean-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True))
