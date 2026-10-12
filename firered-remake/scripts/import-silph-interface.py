#!/usr/bin/env python3
"""Pinned source elevator scroll-arrow sprites and exact integer sine bounce offsets."""
import argparse,hashlib,importlib.util,io,json,pathlib,re,subprocess,sys
from fractions import Fraction
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('silph_art',ROOT/'scripts/import-silph-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art);meta=art.meta
LOCK_SHA='2411fd190ceff5a693f3bc30607846812aec92b9b77a7fd6cd35bf64dd94a904'
def authenticated(root,reference=None):
 b=meta.safe(root,'scripts/silph-interface.lock.json').read_bytes();meta.require(len(b)==1140 and meta.sha(b)==LOCK_SHA,'Silph interface lock differs');lock=json.loads(b)
 meta.require((lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'],lock['profile'],len(lock['files']))==(1,'pret/pokefirered',meta.COMMIT,meta.TREE,'firered-silph-interface-v1',5),'Silph interface inventory differs');sources={}
 if reference:
  meta.require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==meta.COMMIT,'Interface reference HEAD differs');meta.require(subprocess.check_output(['git','-C',str(reference),'rev-parse',meta.COMMIT+'^{tree}']).decode().strip()==meta.TREE,'Interface reference tree differs')
 for row in lock['files']:
  p=meta.safe(root,'content/raw/silph-interface/'+row['path'])
  if reference:meta.require(subprocess.check_output(['git','-C',str(reference),'ls-tree',meta.COMMIT,'--',row['path']]).decode().strip()=='100644 blob '+row['gitBlobSha1']+'\t'+row['path'],'Interface regular blob differs')
  b=subprocess.check_output(['git','-C',str(reference),'cat-file','blob',row['gitBlobSha1']]) if reference else p.read_bytes();meta.require((len(b),meta.sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())==(row['byteLength'],row['sha256'],row['gitBlobSha1']),'Interface source differs');sources[row['path']]=b
 if reference:
  for path,b in sources.items():p=meta.safe(root,'content/raw/silph-interface/'+path);p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
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
  ix=image.tobytes();meta.require(max(ix)<16,'Arrow palette index differs');rgba=bytes(c for i in ix for c in (*pal[i],0 if i==0 else 255));stem='scroll-'+direction;out[stem+'.png']=art.png(16,16,rgba);out[stem+'.rgba']=rgba;arrows.append(dict(id=stem,arrowType=kind,sourceTileOffset=4,flipV=direction=='down',width=16,height=16,texturePath=stem+'.png',rgbaPath=stem+'.rgba',sha256=meta.sha(out[stem+'.png']),rgbaSha256=meta.sha(rgba),bounceOffsets=[int(sine[(tick*frequency)%256]*2) for tick in range(32)]))
 out['manifest.json']=meta.canonical(dict(schemaVersion=1,profile='firered-silph-interface-v1',sourceCommit=meta.COMMIT,lockSha256=LOCK_SHA,arrows=arrows,outputs=[dict(path=p,byteLength=len(b),sha256=meta.sha(b)) for p,b in sorted(out.items())]));return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();out=build(a.project_root,a.reference_root)
 for relative,b in out.items():
  target=meta.safe(a.project_root,'content/generated/silph-interface/'+relative)
  if a.check:meta.require(target.read_bytes()==b,'Interface output differs: '+relative)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(passed=True,arrows=2,manifestBytes=len(out['manifest.json']),manifestSha256=meta.sha(out['manifest.json']))))
