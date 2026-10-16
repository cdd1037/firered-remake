#!/usr/bin/env python3
"""Rebuild the exact Max Repel pixels and source Gym trainer class strings."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse,importlib.util,io,json,pathlib,re,subprocess,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
spec=importlib.util.spec_from_file_location('presentation_metadata',ROOT/'scripts/imports/presentation/import-saffron-gym-presentation-source.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
from image_io import png as encode_rgba_png
PROFILE='firered-saffron-gym-interface-v1'
def build(root,reference=None):
 lb=meta.safe(root,'scripts/recipes/presentation/saffron-gym-interface.lock.json').read_bytes();lock=json.loads(lb)
 meta.require((lock['commit'], lock['profile'], len(lock['files'])) == (meta.COMMIT, PROFILE, 5),'Interface scope differs');raw={}
 for row in lock['files']:
  p=meta.safe(root,'content/raw/pokefirered/037335f4/'+row['path']);b=subprocess.check_output(['git','-C',str(reference),'show',meta.COMMIT+':'+row['path']]) if reference else p.read_bytes()
  raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 table=raw['src/data/item_icon_table.h'].decode();meta.require(re.search(r'\[ITEM_MAX_REPEL\]\s*= \{gItemIcon_Repel, gItemIconPalette_MaxRepel\}',table),'Max Repel binding differs')
 graphics=raw['src/data/graphics/items.h'].decode();meta.require('gItemIcon_Repel[] = INCBIN_U32("graphics/items/icons/repel.4bpp.lz")' in graphics and 'gItemIconPalette_MaxRepel[] = INCBIN_U32("graphics/items/icon_palettes/max_repel.gbapal.lz")' in graphics,'Max Repel graphics binding differs')
 im=Image.open(io.BytesIO(raw['graphics/items/icons/repel.png']));meta.require(im.mode=='P' and im.size==(24,24),'Max Repel source shape differs');ix=im.tobytes()
 lines=raw['graphics/items/icon_palettes/max_repel.pal'].decode().splitlines();meta.require(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19 and max(ix)<16,'Max Repel palette differs');pal=[tuple((int(c)//8<<3)|(int(c)//8>>2) for c in line.split()) for line in lines[3:]]
 rgba=bytes(c for i in ix for c in (*pal[i],255 if i else 0));out = {'max-repel.png': encode_rgba_png(24, 24, rgba)}
 text=raw['src/data/text/trainer_class_names.h'].decode();names={}
 for symbol in ['TRAINER_CLASS_PSYCHIC','TRAINER_CLASS_CHANNELER','TRAINER_CLASS_LEADER']:
  m=re.search(r'\['+symbol+r'\]\s*= _\("([^"]+)"\)',text);meta.require(m is not None,'Trainer class source name missing');names[symbol]=m[1]
 out['manifest.json']=runtime_json_bytes(dict(schemaVersion=1,trainerClasses=names,item='pret:ITEM_MAX_REPEL',width=24,height=24,paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',outputs=[dict(path=p) for p,b in sorted(out.items())]), sort_keys=True);return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();outputs=build(a.project_root,a.reference_root)
 for rel,b in outputs.items():
  target=meta.safe(a.project_root,'content/generated/ui/saffron-gym-interface/'+rel)
  if a.check:meta.require(target.read_bytes()==b,'Interface output differs: '+rel)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(items=1,classes=3,bytes=len(outputs['manifest.json']))))
