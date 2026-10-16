#!/usr/bin/env python3
"""Offline source Safari items and bounded24-machine metadata."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_source import ItemSource
from item_catalog import inventory_item, item_json
import argparse,io,json,pathlib,re,subprocess,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
from pokemon_import_io import safe, canonical
from presentation_source import need as require, SOURCE_COMMIT as COMMIT
from image_io import png as encode_rgba_png
import machine_catalog as shared_machines

PROFILE='firered-safari-items-v1'
MACHINES=('TM39','TM05','TM09','TM46','TM03','TM45','TM43','TM28','TM31','TM44','HM01','TM34','HM05','TM40','TM19','TM12','TM21','TM49','TM06')

def authenticated(root,reference=None):
 b=safe(root,'scripts/recipes/items/safari-items.lock.json').read_bytes();lock=json.loads(b)
 require((lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile']) == (1, 'pret/pokefirered', COMMIT, PROFILE),'Safari items source scope differs')
 if reference:require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==COMMIT,'Reference HEAD differs')
 raw={}
 for row in lock['files']:
  p=safe(root,'content/raw/pokefirered/037335f4/'+row['path']);b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+row['path']]) if reference else p.read_bytes()
  raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,raw

def generate(root,dependency_root=None):
 lock,raw=authenticated(root);text={p:b.decode() for p,b in raw.items() if not p.endswith('.png')};inv={r['path']:r for r in lock['files']};rules=[]
 shared_item = ItemSource(raw).definition
 def one(p,pattern):
  ms=list(re.finditer(pattern,text[p],re.M|re.S));require(len(ms)==1,'Expected one Safari item match: '+p+' '+pattern);return ms[0]
 def ref(p,s,m=None):return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,endLine=1 if m is None else text[p].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 def number(p,s):
  m=one(p,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');return int(m[1])
 catalog = shared_machines.load_catalog(dependency_root or root)
 outputs={};items=[];icons=[];new_machines=shared_machines.machine_definitions(dependency_root or root, 'content/generated/data/items/safari/machines.json');source_items=json.loads(text['src/data/items.json'])['items']
 for short in ('HM03','HM04','GOLD_TEETH','TM11','TM47','TM32','MAX_POTION','FULL_RESTORE','LEAF_STONE','PROTEIN','QUICK_CLAW','MAX_REVIVE'):
  symbol='ITEM_'+short;item=next(i for i in source_items if i['itemId']==symbol);p='src/data/items.json';im=one(p,r'^    \{\n      "english": "'+re.escape(item['english'])+r'",.*?^    \}');source=ref(p,symbol,im)
  definition=shared_item(symbol)
  declaration=item['description_english'].replace(chr(92)+'n',chr(10));description=declaration
  if short.startswith(('HM','TM')):
   p='src/move_descriptions.c';m=one(p,r'^const u8 gMoveDescription_'+item['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1])
  wrapped=dict(item=definition,description=description);items.append(dict(metadata=wrapped, effectBytes=[]))
  p='src/data/item_icon_table.h';im=one(p,r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,im);image_symbol,palette_symbol=im.groups();paths=[]
  for asset_symbol in (image_symbol,palette_symbol):
   p='src/data/graphics/items.h';am=one(p,r'^const u32 '+asset_symbol+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,asset_symbol,am));paths.append(am[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image_path,palette_path=paths;image=Image.open(io.BytesIO(raw[image_path]));require(image.mode=='P' and image.size==(24,24),'Source icon shape differs');palette=[tuple(map(int,line.split())) for line in text[palette_path].splitlines()[3:]];pixels=bytes(c for i in image.get_flattened_data() for c in (*[(v>>3)*255//31 for v in palette[i]],0 if i==0 else 255));png=encode_rgba_png(24,24,pixels);out='item_'+short.lower()+'.png';outputs[out]=png
  icons.append(dict(item=ident(symbol), width=24, height=24, pngPath=out))
 additions=shared_machines.compatibility_additions(dependency_root or root, 'content/generated/data/items/safari/machines.json')
 itemdoc=dict(schemaVersion=1,items=items,icons=icons,outputs=[dict(path=p) for p,b in sorted(outputs.items())])
 machinedoc=dict(schemaVersion=1,inheritedItemIds=[ident('ITEM_'+item) for item in MACHINES],additions=additions,newMachines=new_machines)
 outputs['items.json']=(json.dumps(itemdoc,sort_keys=True,separators=(',',':'),ensure_ascii=False,default=item_json)+'\n').encode();outputs['machines.json']=canonical(machinedoc)
 return {'content/generated/data/items/safari/'+p:b for p,b in outputs.items()}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',type=pathlib.Path);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference_root:authenticated(a.project_root,a.reference_root)
 for rel,b in generate(a.project_root,a.dependency_root).items():
  path=safe(a.project_root,rel)
  if a.check:require(path.read_bytes()==b,'Safari generated item differs: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
  print(len(b),rel)
