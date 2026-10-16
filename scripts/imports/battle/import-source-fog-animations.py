#!/usr/bin/env python3
"""Discover complete swirling-fog roots and import their shared source tiles/palettes."""
import argparse,importlib.util,io,json,re,struct
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('fog_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');shared=importlib.util.module_from_spec(spec);spec.loader.exec_module(shared)
TEMPLATES={'gMistCloudSpriteTemplate','gSmogCloudSpriteTemplate'}
def build(reference):
 read=lambda name:shared.read(reference,name)
 source=read('data/battle_anim_scripts.s').decode()
 ids={n:int(i) for n,i in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',read('include/constants/moves.h').decode(),re.M)}
 tags={n:10000+int(i) for n,i in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',read('include/constants/battle_anim.h').decode(),re.M)}
 ops={'loadspritegfx','monbg','splitbgprio_all','setalpha','loopsewithpan','createsprite','createvisualtask','delay','waitforvisualfinish','clearmonbg','blendoff','end','call','return'}
 def commands(label,end):
  m=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not m:raise ValueError('Missing source label '+label)
  rows=[]
  for offset,line in enumerate(source[m.end():].splitlines()):
   line=line.split('@',1)[0].strip()
   if not line:continue
   if line.endswith(':'):raise ValueError('Unexpected source label')
   op,*tail=line.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in ops:raise ValueError('Uncovered source command '+op)
   rows.append(dict(op=op,args=args,line=source[:m.end()].count('\n')+offset+1))
   if op==end:return rows
  raise ValueError('Unterminated source label')
 scripts=[];subs={}
 for name in re.findall(r'^Move_(\w+):\n',source,re.M):
  if name not in ids:continue
  try:root=commands('Move_'+name,'end');local={label:commands(label,'return') for label in dict.fromkeys(c['args'][0] for c in root if c['op']=='call')}
  except ValueError:continue
  flat=root+[c for sub in local.values() for c in sub]
  if not any(c['op']=='createsprite' and c['args'][0] in TEMPLATES for c in flat):continue
  if any(c['op']=='call' for sub in local.values() for c in sub):continue
  if any(c['op']=='createsprite' and c['args'][0] not in TEMPLATES or c['op']=='createvisualtask' and c['args'][0] not in {'AnimTask_BlendColorCycle','AnimTask_ShakeMon2'} or c['op']=='loadspritegfx' and c['args'][0] not in {'ANIM_TAG_MIST_CLOUD','ANIM_TAG_PURPLE_GAS_CLOUD'} or c['op']=='loopsewithpan' and c['args'][0] not in {'SE_M_MIST','SE_M_TOXIC'} for c in flat):continue
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=root,soundEffects=shared.sound_effects(reference,flat),spriteTags={n:tags[n] for n in sorted({c['args'][0] for c in flat if c['op']=='loadspritegfx'})}));subs.update(local)
 csource=read('src/battle_anim_ice.c').decode();recipes=[shared.recipe(csource,name,'InitSwirlingFogAnim') for name in sorted(TEMPLATES)]
 png=read('graphics/battle_anims/sprites/mist_cloud.png')
 with Image.open(io.BytesIO(png)) as image:
  if image.mode!='P' or image.size!=(32,32):raise ValueError('Unexpected source fog sheet')
  white=image.getpalette()[:48]
  indices=[v for y in range(0,32,8) for x in range(0,32,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
 rows=read('graphics/battle_anims/sprites/purple_gas_cloud.pal').decode().splitlines()
 if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Invalid purple cloud palette')
 purple=[int(c) for row in rows[3:] for c in row.split()]
 if len(purple)!=48:raise ValueError('Invalid purple cloud palette length')
 blobs={'fog-cloud.4bpp':bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2))};assets=[]
 for name,palette in [('mist-cloud',white),('purple-gas-cloud',purple)]:
  file='fog-'+name+'.pal16';blobs[file]=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]);assets.append(dict(id=name,width=32,height=32,indicesPath='fog-cloud.4bpp',palettePath=file,paletteTag='ANIM_TAG_'+name.upper().replace('-','_')))
 if not scripts:raise ValueError('No complete swirling fog roots')
 data=dict(schemaVersion=1,scripts=sorted(scripts,key=lambda r:r['moveId']),subroutines=subs,spriteRecipes=recipes,assets=[],indexedAssets=assets)
 return (json.dumps(data,indent=2)+'\n').encode(),blobs
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data,blobs=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/name,blob) for name,blob in blobs.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Fog runtime output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[r['moveId'] for r in json.loads(data)['scripts']])))
