#!/usr/bin/env python3
"""Rebuild the exact Max Repel pixels and source Gym trainer class strings."""
import argparse,hashlib,importlib.util,io,json,pathlib,re,subprocess,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('art',ROOT/'scripts/import-saffron-gym-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
meta=art.meta
LOCK_SHA='60d721dcc902aced2516b80e8c388c8ea5cd00b49d1f2220206f1fa258aeff21'
PROFILE='firered-saffron-gym-interface-v1'
def build(root,reference=None):
 lb=meta.safe(root,'scripts/saffron-gym-interface.lock.json').read_bytes();meta.require(meta.sha(lb)==LOCK_SHA,'Interface lock differs');lock=json.loads(lb)
 meta.require((lock['commit'],lock['tree'],lock['profile'],len(lock['files']))==(meta.COMMIT,meta.TREE,PROFILE,5),'Interface scope differs');raw={}
 for row in lock['files']:
  p=meta.safe(root,'content/raw/saffron-gym-interface/'+row['path']);b=subprocess.check_output(['git','-C',str(reference),'show',meta.COMMIT+':'+row['path']]) if reference else p.read_bytes()
  meta.require((len(b),meta.sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())==(row['byteLength'],row['sha256'],row['gitBlobSha1']),'Interface source differs');raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 table=raw['src/data/item_icon_table.h'].decode();meta.require(re.search(r'\[ITEM_MAX_REPEL\]\s*= \{gItemIcon_Repel, gItemIconPalette_MaxRepel\}',table),'Max Repel binding differs')
 graphics=raw['src/data/graphics/items.h'].decode();meta.require('gItemIcon_Repel[] = INCBIN_U32("graphics/items/icons/repel.4bpp.lz")' in graphics and 'gItemIconPalette_MaxRepel[] = INCBIN_U32("graphics/items/icon_palettes/max_repel.gbapal.lz")' in graphics,'Max Repel graphics binding differs')
 im=Image.open(io.BytesIO(raw['graphics/items/icons/repel.png']));meta.require(im.mode=='P' and im.size==(24,24),'Max Repel source shape differs');ix=im.tobytes()
 lines=raw['graphics/items/icon_palettes/max_repel.pal'].decode().splitlines();meta.require(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19 and max(ix)<16,'Max Repel palette differs');pal=[tuple((int(c)//8<<3)|(int(c)//8>>2) for c in line.split()) for line in lines[3:]]
 rgba=bytes(c for i in ix for c in (*pal[i],255 if i else 0));out={'max-repel.rgba':rgba,'max-repel.png':art.png(24,24,rgba)}
 text=raw['src/data/text/trainer_class_names.h'].decode();names={}
 for symbol in ['TRAINER_CLASS_PSYCHIC','TRAINER_CLASS_CHANNELER','TRAINER_CLASS_LEADER']:
  m=re.search(r'\['+symbol+r'\]\s*= _\("([^"]+)"\)',text);meta.require(m is not None,'Trainer class source name missing');names[symbol]=m[1]
 out['manifest.json']=meta.canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=meta.COMMIT,lockSha256=LOCK_SHA,inputs=lock['files'],trainerClasses=names,item='pret:ITEM_MAX_REPEL',width=24,height=24,rgbaSha256=meta.sha(rgba),indexSha256=meta.sha(ix),paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',outputs=[dict(path=p,sha256=meta.sha(b),byteLength=len(b)) for p,b in sorted(out.items())]));return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();outputs=build(a.project_root,a.reference_root)
 for rel,b in outputs.items():
  target=meta.safe(a.project_root,'content/generated/saffron-gym-interface/'+rel)
  if a.check:meta.require(target.read_bytes()==b,'Interface output differs: '+rel)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(items=1,classes=3,sha256=meta.sha(outputs['manifest.json']),bytes=len(outputs['manifest.json']))))
