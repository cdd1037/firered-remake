#!/usr/bin/env python3
"""Import complete gust-family scripts and their indexed source material."""
import argparse,importlib.util,io,json,re,struct
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
def module(name,file):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
shared=module('flying_elemental','import-source-elemental-animations.py')
TEMPLATES={'gEllipticalGustSpriteTemplate':'AnimEllipticalGust','gGustToTargetSpriteTemplate':'AnimGustToTarget','gAirWaveCrescentSpriteTemplate':'AnimAirWaveCrescent'}
def build(reference):
 read=lambda name:shared.read(reference,name)
 source=read('data/battle_anim_scripts.s').decode()
 ids={n:int(i) for n,i in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',read('include/constants/moves.h').decode(),re.M)}
 tags={n:10000+int(i) for n,i in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',read('include/constants/battle_anim.h').decode(),re.M)}
 sprites=set(TEMPLATES)|{'gBasicHitSplatSpriteTemplate','gSlideMonToOffsetSpriteTemplate','gSlideMonToOriginalPosSpriteTemplate'}
 tasks={'AnimTask_AnimateGustTornadoPalette','AnimTask_TranslateMonElliptical','AnimTask_ShakeMon2','AnimTask_MetallicShine'}
 sounds={'SE_M_GUST','SE_M_GUST2','SE_M_WING_ATTACK','SE_M_DOUBLE_SLAP','SE_M_HARDEN','SE_M_RAZOR_WIND'}
 ops={'loadspritegfx','monbg','splitbgprio','setalpha','playsewithpan','loopsewithpan','createsprite','createvisualtask','delay','waitforvisualfinish','clearmonbg','blendoff','end'}
 scripts=[]
 for name in re.findall(r'^Move_(\w+):\n',source,re.M):
  if name not in ids:continue
  m=re.search(r'^Move_'+name+r':\n',source,re.M);rows=[]
  for offset,line in enumerate(source[m.end():].splitlines()):
   line=line.split('@',1)[0].strip()
   if not line:continue
   if line.endswith(':'):break
   op,*tail=line.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in ops:break
   rows.append(dict(op=op,args=args,line=source[:m.end()].count('\n')+offset+1))
   if op=='end':break
  if not rows or rows[-1]['op']!='end' or not any(c['op']=='createsprite' and c['args'][0] in TEMPLATES for c in rows):continue
  if any(c['op']=='createsprite' and c['args'][0] not in sprites or c['op']=='createvisualtask' and c['args'][0] not in tasks or c['op']=='loadspritegfx' and c['args'][0] not in {'ANIM_TAG_GUST','ANIM_TAG_IMPACT'} or c['op'] in {'playsewithpan','loopsewithpan'} and c['args'][0] not in sounds for c in rows):continue
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=rows,soundEffects=shared.sound_effects(reference,rows),spriteTags={n:tags[n] for n in sorted({c['args'][0] for c in rows if c['op']=='loadspritegfx'})}))
 csource=read('src/battle_anim_flying.c').decode();recipes=[shared.recipe(csource,name,callback) for name,callback in TEMPLATES.items()]
 blobs={};assets=[]
 for stem,width,height in [('gust',32,64),('air_wave_2',32,16)]:
  with Image.open(io.BytesIO(read('graphics/battle_anims/sprites/'+stem+'.png'))) as image:
   if image.mode!='P' or image.size!=(width,height):raise ValueError('Unexpected source flying image')
   indices=[v for y in range(0,height,8) for x in range(0,width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()];palette=image.getpalette()[:48]
  if max(indices)>15 or len(palette)!=48:raise ValueError('Invalid source flying indexed material')
  name=stem.replace('_','-');file='flying-'+name;blobs[file+'.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2));blobs[file+'.pal16']=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
  assets.append(dict(id=name,width=width,height=height,indicesPath=file+'.4bpp',palettePath=file+'.pal16',paletteTag='ANIM_TAG_'+stem.upper()))
 body=re.search(r'static const union AnimCmd sAffineAnim_AirWaveCrescent\[\] =\s*\{(.*?)\};',csource,re.S)
 if body is None:raise ValueError('Missing air-wave source command array')
 raw=[line.split('//',1)[0].strip().rstrip(',') for line in body[1].splitlines() if line.split('//',1)[0].strip()]
 if len(raw)!=5 or any(not re.fullmatch(r'ANIMCMD_FRAME\([^)]*\)',line) for line in raw[:4]) or raw[4]!='ANIMCMD_JUMP(0)':raise ValueError('Air-wave seek requires four direct source FRAME commands')
 # SeekSpriteAnim operands 0..3 select direct FRAME commands before the JUMP.
 air=next(r for r in recipes if r['callback']=='AnimAirWaveCrescent')
 if len(air['animations'])!=1 or len(air['animations'][0])!=4 or air['loopStarts']!=[0] or any(f['duration']!=3 for f in air['animations'][0]):raise ValueError('Air-wave source direct-frame seek mapping differs')
 data=dict(schemaVersion=1,scripts=sorted(scripts,key=lambda r:r['moveId']),subroutines={},spriteRecipes=recipes,assets=[],indexedAssets=assets)
 if not scripts:raise ValueError('No complete gust source roots')
 return (json.dumps(data,indent=2)+'\n').encode(),blobs
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data,blobs=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/name,blob) for name,blob in blobs.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Flying runtime output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[r['moveId'] for r in json.loads(data)['scripts']])))
