#!/usr/bin/env python3
"""Import named source table animations and their exact material dependencies."""
import argparse,importlib.util,io,json,re,struct
from PIL import Image
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('general_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');shared=importlib.util.module_from_spec(spec);spec.loader.exec_module(shared)
def branched_program(source,label):
 # Compile one general root and its physical local labels. Keep executable PCs;
 # source return comparison aliases use the shared signed argument register7.
 start=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
 if not start:raise ValueError('Missing source label '+label)
 tail=source[start.end():];end=re.search(r'^General_[A-Za-z0-9_]+:',tail,re.M)
 if end:tail=tail[:end.start()]
 result=[];labels={label:0}
 allowed={'createvisualtask','goto','monbg','clearmonbg','playsewithpan','waitplaysewithpan','waitforvisualfinish','end','jumpargeq'}
 for i,line in enumerate(tail.splitlines()):
  line=line.split('@',1)[0].strip()
  if not line:continue
  if line.endswith(':'):
   name=line[:-1]
   if name in labels:raise ValueError('Duplicate general label '+name)
   labels[name]=len(result);continue
  op,*rest=line.split(None,1);args=[v.strip() for v in rest[0].split(',')] if rest else []
  if op=='jumpreteq':op='jumpargeq';args=['7',*args]
  if op not in allowed:raise ValueError('Uncovered branched general command '+op)
  result.append(dict(op=op,args=args,line=source[:start.end()].count('\n')+i+1))
 active=set();done=set()
 def visit(pc):
  if pc in active:raise ValueError('Cyclic general source program')
  if pc in done:return
  if pc<0 or pc>=len(result):raise ValueError('General source program falls off')
  active.add(pc);row=result[pc];op=row['op'];args=row['args'];nexts=[]
  if op in {'goto','jumpargeq'}:
   if len(args)!=(1 if op=='goto' else 3) or args[-1] not in labels:raise ValueError('Missing general branch target')
   nexts.append(labels[args[-1]])
  if op not in {'goto','end'}:nexts.append(pc+1)
  for target in nexts:visit(target)
  active.remove(pc);done.add(pc)
 visit(0)
 if len(done)!=len(result):raise ValueError('Unreachable general instructions')
 return result,labels

def thin_ring_indexed(read):
 # Native index zero retains transparency even when another palette entry
 # has the same RGB555 value. The indexed representation owns this material.
 png=read('graphics/battle_anims/sprites/thin_ring.png')
 registration=read('src/data/battle_anim.h').decode();graphics=read('src/graphics.c').decode()
 if not re.search(r'\{gBattleAnimSpriteGfx_ThinRing,\s*0x0800,\s*ANIM_TAG_THIN_RING\}',registration) or not re.search(r'\{gBattleAnimSpritePal_ThinRing,\s*ANIM_TAG_THIN_RING\}',registration):raise ValueError('Thin-ring source registration differs')
 for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+r'_ThinRing\[\] = INCBIN_U32\("graphics/battle_anims/sprites/thin_ring\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Thin-ring source material linkage differs')
 with Image.open(io.BytesIO(png)) as image:
  if image.mode!='P' or image.size!=(64,64):raise ValueError('Thin-ring indexed source shape differs')
  indices=[b for y in range(0,64,8) for x in range(0,64,8) for b in image.crop((x,y,x+8,y+8)).tobytes()];palette=image.getpalette()[:48]
 if max(indices)>15 or len(palette)!=48:raise ValueError('Thin-ring indexed source palette differs')
 colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
 blobs={};blobs['general-thin-ring.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2));blobs['general-thin-ring.pal16']=struct.pack('<16H',*colors)
 indexed=[dict(id='thin-ring',width=64,height=64,indicesPath='general-thin-ring.4bpp',palettePath='general-thin-ring.pal16',paletteTag='ANIM_TAG_THIN_RING')]
 return indexed,blobs

def build(reference):
 read=lambda p:shared.read(reference,p)
 source=read('data/battle_anim_scripts.s').decode()
 def commands(label,terminal):
  match=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not match:raise ValueError('Missing source label '+label)
  result=[]
  for i,line in enumerate(source[match.end():].splitlines()):
   line=line.split('@',1)[0].strip()
   if not line:continue
   if line.endswith(':'):raise ValueError('Unterminated general source label')
   op,*tail=line.split(None,1);args=[s.strip() for s in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in {'loadspritegfx','delay','playsewithpan','createvisualtask','waitforvisualfinish','call','createsprite','return','end','goto','monbg','clearmonbg','fadetobg','restorebg','waitbgfadein','waitbgfadeout','setarg','setalpha','blendoff','waitplaysewithpan'}:raise ValueError('Uncovered source general command '+op)
   result.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+i+1))
   if op==terminal:return result
  raise ValueError('Missing general terminator')
 roots={name:commands(name,'end') for name in ['General_HeldItemEffect','General_WishHeal']}
 castform,castform_labels=branched_program(source,'General_CastformChange')
 roots['General_CastformChange']=castform
 # Shared move-controller wrappers remain named programs, not move identities.
 for name in ['Special_SubstituteToMon','Special_MonToSubstitute']:
  roots[name]=commands(name,'end')
 # These are named controller programs, never additional move identities.
 for name in ['General_Rain','General_FutureSightHit','General_DoomDesireHit','General_FocusPunchSetUp']:
  roots[name]=commands(name,'end')
 for name in ['General_Sun','General_Sandstorm','General_Hail']:
  roots[name]=commands(name,'goto')
  if len(roots[name])!=1 or len(roots[name][0]['args'])!=1 or not roots[name][0]['args'][0].startswith('Move_'):
   raise ValueError('General tail must be one canonical move transfer')
 subs={name:commands(name,'return') for name in ['GrantingStarsEffect','HealingEffect','EndureEffect','SetPsychicBackground','UnsetPsychicBackground']}
 utility=read('src/battle_anim_utility_funcs.c').decode()
 target=re.search(r'void AnimTask_SetAnimTargetToBattlerTarget\(u8 taskId\)\s*\{(.*?)^\}',utility,re.S|re.M)
 if target is None or re.sub(r'\s+','',target[1])!='gBattleAnimTarget=gBattlerTarget;DestroyAnimVisualTask(taskId);':raise ValueError('Delayed target callback contract differs')
 beam=read('src/battle_anim_effects_3.c').decode()
 beam_body=re.search(r'void AnimTask_DoomDesireLightBeam\(u8 taskId\)\s*\{(.*?)^\}',beam,re.S|re.M)
 if beam_body is None or any(token not in beam_body[1] for token in ['AnimLoadCompressedBgTilemap(animBg.bgId, gBattleAnim_MorningSunTilemap);','AnimLoadCompressedBgGfx(animBg.bgId, gBattleAnim_MorningSunGfx, animBg.tilesOffset);','LoadCompressedPalette(gBattleAnim_MorningSunPal, BG_PLTT_ID(animBg.paletteId), PLTT_SIZE_4BPP);']):raise ValueError('Doom beam shared material contract differs')
 def dependency_commands(rows):
  flat=list(rows)
  for row in rows:
   if row['op']=='call':
    name=row['args'][0]
    if name not in subs:raise ValueError('Uncovered general subroutine '+name)
    flat+=subs[name]
  return flat
 recipes=[]
 for file,name,callback in [('battle_anim_effects_1.c','gGrantingStarsSpriteTemplate','AnimGrantingStars'),('battle_anim_effects_2.c','gThinRingExpandingSpriteTemplate','AnimSpriteOnMonPos')]:
  code=read('src/'+file).decode()
  recipes.append(shared.recipe(code,name,callback))
 rows=read('graphics/battle_anims/sprites/sparkle_2.pal').decode().splitlines()
 if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Unexpected sparkle palette')
 palette=[int(v) for row in rows[3:] for v in row.split()]
 blobs={'general-sparkle-2.rgb555':shared.hit.rgb555(read('graphics/battle_anims/sprites/sparkle_1.png'),(32,256),palette)}
 assets=[dict(id='sparkle-2',path='general-sparkle-2.rgb555')]
 indexed,extra=thin_ring_indexed(read);blobs.update(extra)
 data=dict(schemaVersion=1,indexedAssets=indexed,generalScripts=[dict(label=name,commands=rows,soundEffects=shared.sound_effects(reference,dependency_commands(rows))) for name,rows in roots.items()],subroutines=subs,spriteRecipes=recipes,assets=assets)
 for row in data['generalScripts']:
  if row['label']=='General_CastformChange':row['labels']=castform_labels
  if row['label'] in {'General_Sun','General_Sandstorm','General_Hail'}:row['programReferences']=[row['commands'][0]['args'][0]]
  if row['label']=='General_Rain':row['spriteDependencies']=['gRainDropSpriteTemplate']
  if row['label']=='General_DoomDesireHit':row['overlayDependencies']=['morning-sun']
  if row['label'] not in {'General_HeldItemEffect','General_WishHeal','General_CastformChange'}:
   tags={name:10000+int(value) for name,value in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',read('include/constants/battle_anim.h').decode(),re.M)}
   row['spriteTags']={name:tags[name] for name in sorted({c['args'][0] for c in dependency_commands(row['commands']) if c['op']=='loadspritegfx'})}
 names={c['args'][0] for row in roots.values() for c in dependency_commands(row) if c['op']=='fadetobg'}
 definitions={name:int(value) for name,value in re.findall(r'^#define (BG_\w+)\s+(\d+)\b',read('include/constants/battle_anim.h').decode(),re.M)}
 data['backgroundSymbols']={name:definitions[name] for name in sorted(names)}
 return (json.dumps(data,indent=2)+'\n').encode(),blobs
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data,blobs=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/name,blob) for name,blob in blobs.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('General runtime output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,generalAnimations=len(json.loads(data)['generalScripts']))))
