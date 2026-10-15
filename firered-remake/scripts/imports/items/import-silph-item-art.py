#!/usr/bin/env python3
"""Authenticate and decode all 23 Silph pickup/reward item icons; no gameplay activation."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,base64,importlib.util,io,json,pathlib,re,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
sys.path.insert(0, str(ROOT/'scripts/lib'))
from item_icon_exports import item_icon_path, output_materials
spec=importlib.util.spec_from_file_location('silph_art',ROOT/'scripts/imports/presentation/import-silph-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art);meta=art.meta
ITEMS='CARD_KEY MASTER_BALL ULTRA_BALL TM01 TM08 TM41 HYPER_POTION MAX_REVIVE ESCAPE_ROPE FULL_HEAL PROTEIN HP_UP X_SPECIAL CALCIUM CARBOS RARE_CANDY IRON ZINC ELIXIR PP_UP NUGGET MAX_POTION REVIVE'.split()
def build(root):
 lock,s=meta.authenticated(root);rows={r['path']:r for r in lock['files']};out={};icons=[]
 def ref(path,symbol,m=None):return dict(path=path,symbol=symbol,startLine=1 if m is None else s[path].decode().count('\n',0,m.start())+1,endLine=1 if m is None else s[path].decode().count('\n',0,m.end())+1)
 def match(p,pattern):
  matches=list(re.finditer(pattern,s[p].decode(),re.M));meta.require(len(matches)==1,'Nonunique item binding '+pattern);return matches[0]
 for item in sorted(ITEMS):
  table='src/data/item_icon_table.h';m=match(table,r'^    \[ITEM_'+item+r'\]\s*=\s*\{(\w+),\s*(\w+)\},');binding=ref(table,'ITEM_'+item,m);symbols=m.groups();paths=[]
  for symbol in symbols:
   m=match('src/data/graphics/items.h',r'^const u32 '+symbol+r'\[\]\s*=\s*INCBIN_U32\("([^"]+)"\);');paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image,palette=paths;im=Image.open(io.BytesIO(s[image]));meta.require(im.mode=='P' and im.size==(24,24),'Item icon dimensions differ');ix=im.tobytes();lines=s[palette].decode().splitlines();meta.require(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19 and max(ix)<16,'Item palette differs');pal=[tuple((int(c)//8<<3)|(int(c)//8>>2) for c in line.split()) for line in lines[3:]];rgba=bytes(c for i in ix for c in (*pal[i],0 if i==0 else 255));name=item_icon_path(image,palette);out[name]=art.png(24,24,rgba)
  icons.append(dict(item=dict(value='pret:ITEM_' + item), width=24, height=24, pngPath=name))
 out['manifest.json']=meta.canonical(dict(schemaVersion=1,icons=icons,outputs=[dict(path=p) for p,b in sorted(out.items())]));return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();out=build(a.project_root)
 for (base, relative), b in output_materials(a.project_root/'content/generated/data/services/silph-item-art.json',out).items():
  target=meta.safe(base,relative)
  if a.check:meta.require(target.read_bytes()==b,'Item icon differs: '+relative)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(passed=True,icons=len(ITEMS),manifestBytes=len(out['manifest.json']))))
