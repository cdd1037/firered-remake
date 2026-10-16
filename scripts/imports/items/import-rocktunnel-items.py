#!/usr/bin/env python3
"""Authenticated RockTunnel item declarations, exact ship pickups, icons and berry pocket semantics, from pinned Git blobs or cache. No upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from item_effects import read_effects_text
from item_source import ItemSource
from item_catalog import item_json
import argparse, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT/'scripts/lib'))
from item_icon_exports import output_materials
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
 b=safe(path or ROOT/'scripts/recipes/items/rocktunnel-items.lock.json').read_bytes()
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
 item_source=ItemSource(data)
 source_effects=read_effects_text(text['include/constants/item_effects.h'],text['src/data/pokemon/item_effects.h'])
 effects={'MAX_ETHER':'MaxEther','PERSIM_BERRY':'PersimBerry','REVIVE':'Revive'}
 defs=[];icons=[];outputs={}
 for name in ITEMS:
  symbol='ITEM_'+name;chosen=[i for i in all_items if i['itemId']==symbol]
  if len(chosen)!=1:raise ValueError('Nonunique item')
  i=chosen[0];p='src/data/items.json';m=match(p,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}');source=ref(p,symbol,m)
  definition=item_source.definition(symbol)
  declaration=i['description_english'].replace('\\n','\n');effect_bytes=source_effects[symbol] if name in effects else []
  if name in effects:
   p='src/data/pokemon/item_effects.h';em=match(p,r'^    \['+symbol+r' - ITEM_POTION\][ \t]*= sItemEffect_'+effects[name]+',');rules.append(ref(p,symbol+' effect binding',em))
  defs.append(dict(metadata=dict(item=definition,description=declaration), effectBytes=effect_bytes))
  icon,icon_png=item_source.icon(symbol)
  icons.append(icon);outputs[icon['pngPath']]=icon_png
 pickups=item_source.pickups(MAPS)
 if len(pickups)!=15 or sum(x['kind']=='hidden' for x in pickups)!=8 or any(x['quantity']!=1 for x in pickups):raise ValueError('Pickup scope differs')
 for p,s,pat in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','CheckBagHasSpace',r'^bool8 CheckBagHasSpace\(.*?^\}'),('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['include/constants/item_effects.h','src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 for p,s in [('data/maps/Route10_PokemonCenter_1F/scripts.inc','Route10_PokemonCenter_1F_EventScript_Aide'),('data/scripts/aide.inc','Aide shared source'),('src/prof_pc.c','GetPokedexCount'),('src/overworld.c','UpdateEscapeWarp')]:rules.append(ref(p,s))
 aide_text=text['data/maps/Route10_PokemonCenter_1F/scripts.inc']
 if '.equ REQUIRED_OWNED_MONS, 20' not in aide_text or 'checkitemspace ITEM_EVERSTONE' not in aide_text or 'setflag FLAG_GOT_EVERSTONE_FROM_OAKS_AIDE' not in aide_text:raise ValueError('Everstone aide source differs')
 doc=dict(schemaVersion=1,items=defs[:-1],icons=icons[:-1],pickups=pickups)
 outputs['rocktunnel-items.json']=runtime_json_bytes(doc, default=item_json)
 machine_doc=dict(schemaVersion=1,icons=[icons[-1]])
 outputs['rocktunnel-machines.json']=encoded(machine_doc)
 outputs['manifest.json']=encoded(dict(schemaVersion=1,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
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
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/items/rocktunnel');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/items/rocktunnel-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True, default=item_json))
