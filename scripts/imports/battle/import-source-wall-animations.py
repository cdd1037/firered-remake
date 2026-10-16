#!/usr/bin/env python3
"""Import indexed defensive-wall materials and complete source callback dependencies."""
import argparse,importlib.util,json,re,struct,io
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('wall_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');shared=importlib.util.module_from_spec(spec);spec.loader.exec_module(shared)
WALLS={'gLightScreenWallSpriteTemplate':'green','gReflectWallSpriteTemplate':'blue','gMirrorCoatWallSpriteTemplate':'red','gBarrierWallSpriteTemplate':'gray','gMagicCoatWallSpriteTemplate':'orange'}
SPARKLES={'gReflectSparkleSpriteTemplate':'sparkle-4','gSpecialScreenSparkleSpriteTemplate':'sparkle-3'}
def build(reference):
 read=lambda path:shared.read(reference,path)
 source=read('data/battle_anim_scripts.s').decode();constants=read('include/constants/battle_anim.h').decode();base=int(re.search(r'^#define ANIM_SPRITES_START\s+(\d+)',constants,re.M)[1])
 tags={n:base+int(i) for n,i in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START \+ (\d+)\)',constants,re.M)}
 ids={n:int(i) for n,i in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',read('include/constants/moves.h').decode(),re.M)}
 def commands(label,end):
  m=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not m:raise ValueError('Missing source label '+label)
  rows=[]
  for offset,line in enumerate(source[m.end():].splitlines()):
   line=line.split('@',1)[0].strip()
   if not line:continue
   if line.endswith(':'):raise ValueError('Unterminated source label')
   op,*tail=line.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in {'loadspritegfx','setalpha','waitplaysewithpan','playsewithpan','createsprite','delay','call','return','waitforvisualfinish','blendoff','end'}:raise ValueError('Uncovered source command '+op)
   rows.append(dict(op=op,args=args,line=source[:m.end()].count('\n')+offset+1))
   if op==end:return rows
  raise ValueError('Unterminated source label')
 scripts=[];subs={};templates=set(WALLS)|set(SPARKLES);gfx={'ANIM_TAG_'+c.upper()+'_LIGHT_WALL' for c in WALLS.values()}|{'ANIM_TAG_SPARKLE_3','ANIM_TAG_SPARKLE_4'}
 for name in re.findall(r'^Move_(\w+):\n',source,re.M):
  if name not in ids:continue
  try:
   root=commands('Move_'+name,'end');local={label:commands(label,'return') for label in dict.fromkeys(c['args'][0] for c in root if c['op']=='call')}
  except ValueError:continue
  flat=root+[c for sub in local.values() for c in sub]
  if not any(c['op']=='createsprite' and c['args'][0] in WALLS for c in flat):continue
  if any(c['op']=='call' for sub in local.values() for c in sub):continue
  if any(c['op']=='createsprite' and c['args'][0] not in templates or c['op']=='loadspritegfx' and c['args'][0] not in gfx or c['op'] in {'playsewithpan','waitplaysewithpan'} and c['args'][0] not in {'SE_M_REFLECT','SE_M_BARRIER'} for c in flat):continue
  used={a for c in flat for a in c['args'] if a.startswith('ANIM_TAG_')}
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=root,soundEffects=shared.sound_effects(reference,flat),spriteTags={name:tags[name] for name in sorted(used)}));subs.update(local)
 csource=read('src/battle_anim_psychic.c').decode();recipes=[shared.recipe(csource,name,'AnimDefensiveWall' if name in WALLS else 'AnimWallSparkle') for name in sorted(templates)]
 blobs={};indexed=[]
 with Image.open(io.BytesIO(read('graphics/battle_anims/sprites/green_light_wall.png'))) as image:
  if image.mode!='P' or image.size!=(64,64):raise ValueError('Unexpected source wall image')
  indices=[index for y in range(0,64,8) for x in range(0,64,8) for index in image.crop((x,y,x+8,y+8)).tobytes()];green=image.getpalette()[:48]
  if max(indices)>15:raise ValueError('Wall source is not4bpp')
  blobs['walls-light-wall.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2))
 for color in WALLS.values():
  if color=='green':palette=green
  else:
   rows=read('graphics/battle_anims/sprites/'+color+'_light_wall.pal').decode().splitlines()
   if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Invalid wall palette')
   palette=[int(c) for row in rows[3:] for c in row.split()]
  if len(palette)!=48:raise ValueError('Invalid wall palette length')
  file='walls-'+color+'-light-wall.pal16';blobs[file]=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
  indexed.append(dict(id=color+'-light-wall',width=64,height=64,indicesPath='walls-light-wall.4bpp',palettePath=file,paletteTag='ANIM_TAG_'+color.upper()+'_LIGHT_WALL'))
 assets=[]
 with Image.open(io.BytesIO(read('graphics/battle_anims/sprites/sparkle_3.png'))) as image:palette=image.getpalette()[:48]
 for name,shape in [('sparkle-3',(16,64)),('sparkle-4',(32,160))]:
  file='walls-'+name+'.rgb555';blobs[file]=shared.hit.rgb555(read('graphics/battle_anims/sprites/'+name.replace('-','_')+'.png'),shape,palette);assets.append(dict(id=name,path=file))
 data=dict(schemaVersion=1,scripts=sorted(scripts,key=lambda s:s['moveId']),subroutines=subs,spriteRecipes=recipes,assets=assets,indexedAssets=indexed)
 return (json.dumps(data,indent=2)+'\n').encode(),blobs
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/walls.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,blobs=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/name,blob) for name,blob in blobs.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Wall runtime output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[s['moveId'] for s in json.loads(data)['scripts']])))
