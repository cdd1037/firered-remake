#!/usr/bin/env python3
"""Bounded MtMoon items/TMs from authenticated local Git blobs or cache. No upstream execution."""
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
import sys
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from machine_catalog import catalog_from_source, machine_reference, compatibility_reference
sys.path.insert(0, str(ROOT/'scripts/lib'))
from item_icon_exports import output_materials
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-mtmoon-items-v1'
ITEMS = 'REVIVE ETHER RARE_CANDY MOON_STONE TINY_MUSHROOM BIG_MUSHROOM STAR_PIECE HELIX_FOSSIL DOME_FOSSIL TM05 TM09 TM46'.split()
SPECIES = ('BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE '
 'CATERPIE METAPOD BUTTERFREE WEEDLE KAKUNA BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE '
 'PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX SPEAROW FEAROW MANKEY '
 'EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF MAGIKARP GYARADOS '
 'ARBOK CLEFAIRY ZUBAT GOLBAT ODDISH PARAS PARASECT BELLSPROUT MAGNEMITE GRIMER VOLTORB KOFFING NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF').split()
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
 b=safe(path or ROOT/'scripts/recipes/items/mtmoon-items.lock.json').read_bytes()
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
 item_source=ItemSource(data)
 source_effects=read_effects_text(text['include/constants/item_effects.h'],text['src/data/pokemon/item_effects.h'])
 effects={'REVIVE':('Revive','fieldRevive'),'ETHER':('Ether','fieldEther'),'RARE_CANDY':('RareCandy','deferredRareCandy'),'MOON_STONE':('MoonStone','separateEvolution')}
 for name in ITEMS:
  symbol='ITEM_'+name;i=next(i for i in all_items if i['itemId']==symbol)
  p='src/data/items.json';m=match(p,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}')
  source=ref(p,symbol,m);definition=item_source.definition(symbol)
  declaration=i['description_english'].replace('\\n','\n');description=declaration
  effect_bytes=source_effects[symbol] if name in effects else []
  support=effects[name][1] if name in effects else ('fossil' if 'FOSSIL' in name else 'noFieldUse')
  if name.startswith('TM'):
   p='src/move_descriptions.c';m=match(p,r'^const u8 gMoveDescription_'+i['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
   description=json.loads(m[1])
  wrapped=dict(item=definition,description=description)
  defs.append(dict(metadata=wrapped, effectBytes=effect_bytes, useSupport=support))
  icon,icon_png=item_source.icon(symbol)
  icons.append(icon);outputs[icon['pngPath']]=icon_png
 shared = catalog_from_source(data)
 def compatibility(tm):
  return [compatibility_reference(shared, 'pret:ITEM_' + tm[:4], 'pret:SPECIES_' + species) for species in SPECIES]
 machines = [machine_reference(shared, 'pret:ITEM_' + item, ['pret:SPECIES_' + species for species in SPECIES]) for item in ['TM05','TM09','TM46']]
 pickups=item_source.pickups(['MtMoon_1F','MtMoon_B1F','MtMoon_B2F','Route4'], hidden_maps=['MtMoon_1F','MtMoon_B1F','MtMoon_B2F'])
 if len(pickups)!=19 or sum(x['kind']=='hidden' for x in pickups)!=8 or any(x['quantity']!=1 for x in pickups):raise ValueError('Pickup scope differs')
 fossils=[];p='data/maps/MtMoon_B2F/scripts.inc'
 for name in ['Dome','Helix']:
  label='MtMoon_B2F_EventScript_'+name+'Fossil';m=match(p,r'^'+label+r'::\n(.*?)(?=^\w+::|\Z)');item='ITEM_'+name.upper()+'_FOSSIL'
  if '\tgiveitem_msg MtMoon_B2F_Text_Obtained'+name+'Fossil, '+item+', 1, MUS_OBTAIN_KEY_ITEM\n' not in m[1]:raise ValueError('Fossil reward differs')
  fossils.append(dict(item=ident(item),script=label,source=ref(p,label,m)))
 for p,s,pat in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','CheckBagHasSpace',r'^bool8 CheckBagHasSpace\(.*?^\}'),('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}'),('src/pokemon.c','AdjustFriendship',r'^void AdjustFriendship\([^;]*?\n\{.*?^\}'),('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['include/constants/item_effects.h','src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 doc=dict(schemaVersion=1,items=defs,machines=machines,tm39Compatibility=compatibility('TM39_ROCK_TOMB'),icons=icons,pickups=pickups,fossils=fossils)
 outputs['mtmoon-items.json']=runtime_json_bytes(doc, default=item_json)
 outputs['manifest.json']=encoded(dict(schemaVersion=1,inputCount=45,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
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
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/items/mtmoon');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/items/mtmoon-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True, default=item_json))
