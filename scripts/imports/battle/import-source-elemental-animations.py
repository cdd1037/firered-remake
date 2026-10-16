#!/usr/bin/env python3
"""Convert reviewed elemental script roots and sprite recipes from pinned pret inputs."""
import argparse, importlib.util, io, json, re, subprocess
from PIL import Image
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('elemental_hit',ROOT/'scripts/imports/battle/import-source-hit-animations.py');hit=importlib.util.module_from_spec(spec);spec.loader.exec_module(hit)
TEMPLATES={
 'gEmberSpriteTemplate':('battle_anim_fire.c','TranslateAnimSpriteToTargetMonLocation'),
 'gEmberFlareSpriteTemplate':('battle_anim_fire.c','AnimEmberFlare'),
 'gFireSpiralInwardSpriteTemplate':('battle_anim_fire.c','AnimFireSpiralInward'),
 'gFireSpreadSpriteTemplate':('battle_anim_fire.c','AnimFireSpread'),
 'gFlamethrowerFlameSpriteTemplate':('battle_anim_water.c','AnimToTargetInSinWave'),
}
def read(reference,path):
 return subprocess.check_output(['git','-C',str(reference),'show',hit.COMMIT+':'+path]) if (reference/'.git').exists() else (reference/path).read_bytes()
def recipe(source,name,callback):
 match=re.search(r'const struct SpriteTemplate '+name+r' =\s*\{(.*?)\};',source,re.S)
 if not match:raise ValueError('Missing template '+name)
 fields=dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
 shape=re.fullmatch(r'&gOamData_Affine(Off|Normal)_Obj(Normal|Blend)_(8|16|32)x(8|16|32)',fields['oam'])
 if fields['callback']!=callback or not shape or fields['tileTag']!=fields['paletteTag']:raise ValueError('Elemental recipe differs '+name)
 if shape[1]!='Off' and not(callback=='AnimHitSplatBasic' and fields['affineAnims']=='sAffineAnims_HitSplat'):raise ValueError('Unsupported affine recipe')
 asset=fields['tileTag'].removeprefix('ANIM_TAG_').lower().replace('_','-')
 animations=[];loops=[]
 if fields['anims']=='gDummySpriteAnimTable':animations=[[dict(tile=0,duration=1,hFlip=False,vFlip=False)]];loops=[-1]
 else:
  table=re.search(r'const union AnimCmd \*const '+fields['anims']+r'\[\] =\s*\{(.*?)\};',source,re.S)
  if not table:raise ValueError('Missing frame table')
  for array in re.findall(r'\b([sg]\w+)\s*,',table[1]):
   body=re.search(r'const union AnimCmd '+array+r'\[\] =\s*\{(.*?)\};',source,re.S)[1];frames=[];loop=-1
   for row in body.splitlines():
    row=row.strip().rstrip(',')
    if not row:continue
    f=re.fullmatch(r'ANIMCMD_FRAME\((\d+), (\d+)((?:, \.[hv]Flip = TRUE)*)\)',row)
    if f:frames.append(dict(tile=int(f[1]),duration=int(f[2]),hFlip='.hFlip' in f[3],vFlip='.vFlip' in f[3]))
    elif re.fullmatch(r'ANIMCMD_JUMP\((\d+)\)',row):loop=int(re.fullmatch(r'ANIMCMD_JUMP\((\d+)\)',row)[1])
    elif row!='ANIMCMD_END':raise ValueError('Unsupported frame '+row)
   if not frames or loop>=len(frames):raise ValueError('Invalid frame loop')
   animations.append(frames);loops.append(loop)
 return dict(template=name,asset=asset,callback=callback,blend=shape[2]=='Blend',width=int(shape[3]),height=int(shape[4]),animations=animations,loopStarts=loops)
def build(reference):
 source=read(reference,'data/battle_anim_scripts.s').decode()
 def commands(label,end):
  m=re.search(r'^'+label+r'\s*:\n',source,re.M)
  if not m:raise ValueError('Missing '+label)
  result=[]
  for offset,line in enumerate(source[m.end():].splitlines()):
   row=line.split('@',1)[0].strip()
   if not row:continue
   if row.endswith(':'):raise ValueError('Unexpected label')
   op,*tail=row.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in hit.OPS|{'call','return','splitbgprio','panse'}:raise ValueError('Unsupported command '+op)
   result.append(dict(op=op,args=args,line=source[:m.end()].count('\n')+offset+1))
   if op==end:return result
  raise ValueError('Unterminated '+label)
 scripts=[dict(moveId=n,label='Move_'+name,commands=commands('Move_'+name,'end')) for n,name in [(7,'FIRE_PUNCH'),(52,'EMBER'),(53,'FLAMETHROWER')]]
 subs={name:commands(name,'return') for name in ['EmberFireHit','FireSpreadEffect','FlamethrowerCreateFlames']}
 recipes=[recipe(read(reference,'src/'+file).decode(),name,callback) for name,(file,callback) in TEMPLATES.items()]
 pixels={'small-ember':hit.rgb555(read(reference,'graphics/battle_anims/sprites/small_ember.png'),(32,160))}
 return (json.dumps(dict(schemaVersion=1,scripts=scripts,subroutines=subs,spriteRecipes=recipes,assets=[dict(id=id,path='elemental-'+id+'.rgb555') for id in pixels]),indent=2)+'\n').encode(),pixels
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/elemental.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,pixels=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/('elemental-'+id+'.rgb555'),blob) for id,blob in pixels.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Elemental output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[s['moveId'] for s in json.loads(data)['scripts']])))
