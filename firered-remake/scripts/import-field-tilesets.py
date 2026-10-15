#!/usr/bin/env python3
"""Convert field tile behavior definitions once; runtime never reads upstream layouts/binaries."""
import argparse,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def run(root,check=False):
 generated=root/'content/generated';raw=root/'content/raw/pokefirered/037335f4'
 symbols={};used=set()
 for p in generated.rglob('*.json'):
  if p.name=='field-tilesets.json':continue
  d=json.loads(p.read_bytes())
  if isinstance(d,dict):
   if isinstance(d.get('tilesets'),dict):
    for k,v in d['tilesets'].items():
     if isinstance(v,str):
      if k in symbols and symbols[k]!=v:raise ValueError('Conflicting tileset '+k)
      symbols[k]=v
   if p.name=='runtime-map.json':used.add(d['events']['layout'])
 layouts={};tables={}
 for layout in json.loads((raw/'data/layouts/layouts.json').read_bytes())['layouts']:
  if layout.get('id') not in used:continue
  if not all(layout[k] in symbols for k in ('primary_tileset','secondary_tileset')):continue
  pair=[]
  for key in ('primary_tileset','secondary_tileset'):
   name=symbols[layout[key]];pair.append(name)
   if name not in tables:
    data=(raw/'data/tilesets'/name/'metatile_attributes.bin').read_bytes()
    if len(data)%4:raise ValueError('Invalid tile attribute length')
    tables[name]=[[v&511,(v>>9)&31,(v>>24)&7,(v>>29)&3] for v, in struct.iter_unpack('<I',data)]
  layouts[layout['id']]=pair
 data=(json.dumps({'layouts':layouts,'tilesets':tables},sort_keys=True,separators=(',',':'))+'\n').encode()
 out=generated/'field-tilesets.json'
 if check:
  if out.read_bytes()!=data:raise ValueError('Regenerate field tile definitions')
 else:out.write_bytes(data)
 print(json.dumps({'layouts':len(layouts),'tilesets':len(tables),'bytes':len(data)}))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();run(a.root,a.check)
