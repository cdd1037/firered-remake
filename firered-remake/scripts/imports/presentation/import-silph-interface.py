#!/usr/bin/env python3
"""Pinned source elevator scroll-arrow sprites and exact integer sine bounce offsets."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,importlib.util,io,json,pathlib,re,subprocess,sys
from fractions import Fraction
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
spec=importlib.util.spec_from_file_location('silph_art',ROOT/'scripts/imports/presentation/import-silph-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art);meta=art.meta
def authenticated(root,reference=None):
 b=meta.safe(root,'scripts/recipes/presentation/silph-interface.lock.json').read_bytes();lock=json.loads(b)
 meta.require((lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], len(lock['files'])) == (1, 'pret/pokefirered', meta.COMMIT, 'firered-silph-interface-v1', 5),'Silph interface inventory differs');sources={}
 if reference:
  meta.require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==meta.COMMIT,'Interface reference HEAD differs')
 for row in lock['files']:
  p=meta.safe(root,'content/raw/pokefirered/037335f4/'+row['path'])
  b=subprocess.check_output(['git','-C',str(reference),'cat-file','blob',meta.COMMIT + ':' + row['path']]) if reference else p.read_bytes();sources[row['path']]=b
 if reference:
  for path,b in sources.items():p=meta.safe(root,'content/raw/pokefirered/037335f4/'+path);p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,sources
def build(root,reference=None):
 lock,s=authenticated(root,reference);text=s['src/menu_indicators.c'].decode();field=s['src/field_specials.c'].decode()
 for needle in ('sRedArrowOtherGfx[] = INCBIN_U32("graphics/interface/red_arrow_other.4bpp.lz")','sRedArrowPal[] = INCBIN_U16("graphics/interface/red_arrow.gbapal")','spriteSheet.data = sRedArrowOtherGfx','spriteSheet.size = 0x100','ANIMCMD_FRAME(4, 30)','ANIMCMD_FRAME(4, 30, 0, 1)','sprite->y2 = (gSineTable[(u8)(sprite->tSinePos)] * multiplier) / 256','sprite->tSinePos += sprite->tFrequency'):
  meta.require(needle in text,'Source arrow binding differs: '+needle)
 templates=re.search(r'sScrollIndicatorTemplates\[\]\s*=\s*\{(.*?)\n\};',text,re.S)[1];values=[tuple(map(int,m)) for m in re.findall(r'\.animNum = (\d+),\s*\.bounceDir = (\d+),\s*\.multiplier = (\d+),\s*\.frequency = (-?\d+)',templates)];meta.require(values[2:]==[(2,1,2,8),(3,1,2,-8)],'Source vertical bounce differs')
 for needle in ('.firstArrowType = 2','.secondArrowType = 3','template.firstX = 4 * task->data[4] + 8 * task->data[2]','template.firstY = 8','template.secondY = 8 * task->data[5] + 10','template.fullyDownThreshold = task->data[1] - task->data[0]'):
  meta.require(needle in field,'Source scroll arrow positioning differs')
 st=re.search(r'const s16 gSineTable\[\]\s*=\s*\{(.*?)\};',s['src/trig.c'].decode(),re.S)[1];sine=[Fraction(v) for v in re.findall(r'Q_8_8\(([-\d.]+)\)',st)];meta.require(len(sine)==320,'Source sine table scope differs')
 im=Image.open(io.BytesIO(s['graphics/interface/red_arrow_other.png']));meta.require(im.mode=='P' and im.size==(16,32),'Source arrow sheet differs');up=im.crop((0,16,16,32));lines=s['graphics/interface/red_arrow.pal'].decode().splitlines();meta.require(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Arrow palette differs');pal=[tuple((int(c)//8<<3)|(int(c)//8>>2) for c in line.split()) for line in lines[3:]];out={};arrows=[]
 for direction,kind,frequency,image in [('up',2,8,up),('down',3,-8,up.transpose(Image.Transpose.FLIP_TOP_BOTTOM))]:
  ix=image.tobytes();meta.require(max(ix)<16,'Arrow palette index differs');rgba=bytes(c for i in ix for c in (*pal[i],0 if i==0 else 255));stem='scroll-'+direction;out[stem+'.png']=art.png(16,16,rgba);arrows.append(dict(id=stem, arrowType=kind, sourceTileOffset=4, flipV=direction == 'down', width=16, height=16, texturePath=stem + '.png', bounceOffsets=[int(sine[tick * frequency % 256] * 2) for tick in range(32)]))
 out['manifest.json']=meta.canonical(dict(schemaVersion=1,arrows=arrows,outputs=[dict(path=p) for p,b in sorted(out.items())]));return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();out=build(a.project_root,a.reference_root)
 for relative,b in out.items():
  target=meta.safe(a.project_root,'content/generated/ui/silph-interface/'+relative)
  if a.check:meta.require(target.read_bytes()==b,'Interface output differs: '+relative)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(passed=True,arrows=2,manifestBytes=len(out['manifest.json']))))
