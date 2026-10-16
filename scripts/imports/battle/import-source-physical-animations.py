#!/usr/bin/env python3
"""Discover complete source roots with bounded shared label targets using shared physical callback adapters.
Unsupported roots are recorded as dependency gaps, never replaced by a generic hit.
"""
import argparse, importlib.util, io, json, re, struct, subprocess
from PIL import Image
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result
hit=module('physical_hit_import',ROOT/'scripts/imports/battle/import-source-hit-animations.py')
elemental=module('physical_elemental_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py')
def read_sources(reference):
 # Only actual conversion inputs. The source revision comes from the shared importer.
 paths=('data/battle_anim_scripts.s','src/data/battle_moves.h','include/constants/moves.h','include/constants/battle_anim.h','src/data/battle_anim.h','src/battle_anim_fight.c','src/battle_anim_bug.c','src/battle_anim_normal.c','src/battle_anim_poison.c','src/battle_anim_dark.c','src/battle_anim_rock.c','src/battle_anim_fire.c',
        'src/battle_anim_effects_1.c','src/battle_anim_effects_2.c','src/battle_anim_ground.c','src/battle_anim_effects_3.c',
        'graphics/battle_anims/sprites/claw_slash.png','graphics/battle_anims/sprites/torn_metal.png','graphics/battle_anims/sprites/blue_light_wall.pal','graphics/battle_anims/sprites/bone.png','graphics/battle_anims/sprites/eye_sparkle.png','graphics/battle_anims/sprites/anger.png','graphics/battle_anims/sprites/breath.png','graphics/battle_anims/sprites/eclipsing_orb.png',
        'graphics/battle_anims/sprites/mud_sand_0.png','graphics/battle_anims/sprites/mud_sand_1.png','graphics/battle_anims/sprites/mud_sand.pal',
        'graphics/battle_anims/sprites/slam_hit.png','graphics/battle_anims/sprites/whip_hit.pal','graphics/battle_anims/sprites/poison_powder.png',
        'graphics/battle_anims/sprites/sleep_powder.pal','graphics/battle_anims/sprites/stun_spore.pal')
 return {path:(subprocess.check_output(['git','-C',str(reference),'show',hit.COMMIT+':'+path])
               if (reference/'.git').exists() else (reference/path).read_bytes()) for path in paths}
SPRITES={'gLinearStingerSpriteTemplate','gHandleInvertHitSplatSpriteTemplate','gPoisonBubbleSpriteTemplate','gClawSlashSpriteTemplate','gFireSpinSpriteTemplate','gShakeMonOrTerrainSpriteTemplate','gSpinningBoneSpriteTemplate','gFistFootSpriteTemplate','gEyeSparkleSpriteTemplate','gAngerMarkSpriteTemplate','gEclipsingOrbSpriteTemplate','gBreathPuffSpriteTemplate','gHealingBlueStarSpriteTemplate','gMudsportMudSpriteTemplate','gKarateChopSpriteTemplate','gPoisonPowderParticleSpriteTemplate','gStunSporeParticleSpriteTemplate','gSleepPowderParticleSpriteTemplate','gBowMonSpriteTemplate','gFlashingHitSplatSpriteTemplate','gSimplePaletteBlendSpriteTemplate','gSlamHitSpriteTemplate','gVineWhipSpriteTemplate','gViceGripSpriteTemplate','gBasicHitSplatSpriteTemplate','gHorizontalLungeSpriteTemplate','gSlideMonToOffsetSpriteTemplate','gSlideMonToOriginalPosSpriteTemplate','gComplexPaletteBlendSpriteTemplate','gVerticalDipSpriteTemplate','gStompFootSpriteTemplate','gJumpKickSpriteTemplate','gSlidingKickSpriteTemplate'}
TASKS={'AnimTask_MetallicShine','AnimTask_BlendBattleAnimPal','SoundTask_PlaySE1WithPanning','AnimTask_RockMonBackAndForth','AnimTask_DefenseCurlDeformMon','AnimTask_SetGrayscaleOrOriginalPal','AnimTask_GrowAndShrink','AnimTask_SlackOffSquish','AnimTask_Splash','AnimTask_ScaleMonAndRestore','AnimTask_SwayMon','AnimTask_TranslateMonElliptical','AnimTask_RotateMonToSideAndRestore','AnimTask_ShakeMon','AnimTask_ShakeMon2','AnimTask_TranslateMonEllipticalRespectSide','AnimTask_WindUpLunge','AnimTask_ShakeAndSinkMon','AnimTask_ShakeMonInPlace','AnimTask_RotateMonSpriteToSide'}
NEW= {'gLinearStingerSpriteTemplate','gHandleInvertHitSplatSpriteTemplate','gClawSlashSpriteTemplate','gSpinningBoneSpriteTemplate','jumpifmoveturn','choosetwoturnanim','gEyeSparkleSpriteTemplate','gAngerMarkSpriteTemplate','AnimTask_DefenseCurlDeformMon','AnimTask_GrowAndShrink','AnimTask_SlackOffSquish','AnimTask_Splash','gMudsportMudSpriteTemplate','goto','gKarateChopSpriteTemplate','AnimTask_TranslateMonElliptical','gPoisonPowderParticleSpriteTemplate','gStunSporeParticleSpriteTemplate','gSleepPowderParticleSpriteTemplate','gBowMonSpriteTemplate','gFlashingHitSplatSpriteTemplate','gSlamHitSpriteTemplate','gVineWhipSpriteTemplate','gViceGripSpriteTemplate','gVerticalDipSpriteTemplate','gStompFootSpriteTemplate','gJumpKickSpriteTemplate','gSlidingKickSpriteTemplate','AnimTask_WindUpLunge','AnimTask_ShakeAndSinkMon','AnimTask_ShakeMonInPlace','AnimTask_RotateMonSpriteToSide'}
def sprite_recipe(source,template,asset,expected_callback):
 match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',source,re.S)
 if match is None:raise ValueError('Missing sprite recipe '+template)
 fields=dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
 shape=re.fullmatch(r'&gOamData_AffineOff_Obj(Normal|Blend)_(8|16|32)x(8|16|32)',fields['oam'])
 if fields['callback']!=expected_callback or not shape:raise ValueError('Unsupported sprite shape or callback')
 if fields['anims']=='gDummySpriteAnimTable':
  if fields['tileTag']!='ANIM_TAG_MUD_SAND' or fields['paletteTag']!=fields['tileTag'] or fields['affineAnims']!='gDummySpriteAffineAnimTable':raise ValueError('Static mud sprite recipe differs')
  return dict(template=template,asset=asset,callback=expected_callback,blend=False,animations=[[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],loopStarts=[-1],width=int(shape[2]),height=int(shape[3]))
 table=re.search(r'const union AnimCmd \*const '+fields['anims']+r'\[\] =\s*\{(.*?)\};',source,re.S)
 animations=[];loops=[];frame_loops=[]
 for array in re.findall(r'\b(s\w+)\s*,',table[1]):
  body=re.search(r'const union AnimCmd '+array+r'\[\] =\s*\{(.*?)\};',source,re.S)[1]
  frames=[];loop=-1;controls=[];previous_loop=0;raw_frames={};jump=None;terminated=False
  rows=[r.strip().rstrip(',') for r in body.splitlines() if r.strip()]
  if len(rows)>256:raise ValueError('Sprite command count exceeds source byte index')
  for index,row in enumerate(rows):
   if terminated:raise ValueError('Sprite frame command follows termination')
   frame=re.fullmatch(r'ANIMCMD_FRAME\((\d+), (\d+)((?:, \.[hv]Flip = TRUE)*)\)',row)
   command=re.fullmatch(r'ANIMCMD_(LOOP|JUMP)\((\d+)\)',row)
   if frame:
    if not 0<=int(frame[2])<=63:raise ValueError('Sprite duration exceeds source bitfield')
    raw_frames[index]=len(frames);frames.append(dict(tile=int(frame[1]),duration=int(frame[2]),hFlip='.hFlip' in frame[3],vFlip='.vFlip' in frame[3]))
   elif command:
    value=int(command[2])
    if value>63:raise ValueError('Sprite control exceeds source bitfield')
    if command[1]=='LOOP':
     # Adjacent controls have source scan/counter interactions, not a frame loop.
     # Admit bounded frame regions only; zero-count markers bound the next region.
     if len(frames)<=previous_loop:raise ValueError('Sprite loops must enclose frame commands')
     if value:controls.append(dict(at=len(frames),start=previous_loop,count=value))
     previous_loop=len(frames)
    else:jump=value;terminated=True
   elif row=='ANIMCMD_END':terminated=True
   else:raise ValueError('Unimplemented frame operation '+row)
  if not frames or 0 not in raw_frames or not terminated:raise ValueError('Frame animation has no accepted termination/loop')
  if jump is not None:
   if jump not in raw_frames:raise ValueError('Sprite jump must target a frame command')
   loop=raw_frames[jump]
  animations.append(frames);loops.append(loop);frame_loops.append(controls)
 result=dict(template=template,asset=asset,callback=fields['callback'],blend='ObjBlend' in fields['oam'],animations=animations,loopStarts=loops,width=int(shape[2]),height=int(shape[3]))
 if any(frame_loops):result['frameLoops']=frame_loops
 return result

def task_affine_program(source,name):
 match=re.search(r'const union AffineAnimCmd '+re.escape(name)+r'\[\] =\s*\{(.*?)\};',source,re.S)
 if match is None:raise ValueError('Missing task affine program '+name)
 commands=[]
 for row in match[1].splitlines():
  row=row.split('//',1)[0].strip().rstrip(',')
  if not row:continue
  command=dict(op='',x=0,y=0,rotation=0,duration=0,target=0,count=0)
  frame=re.fullmatch(r'AFFINEANIMCMD_FRAME\(([^)]+)\)',row)
  control=re.fullmatch(r'AFFINEANIMCMD_(JUMP|LOOP)\(([^)]+)\)',row)
  if frame:
   x,y,rotation,duration=[int(v.strip(),0) for v in frame[1].split(',')]
   if not -32768<=x<=32764 or not -32768<=y<=32767 or not -128<=rotation<=255 or not 0<=duration<=255:raise ValueError('Task affine frame outside source representation')
   command.update(op='frame',x=x,y=y,rotation=rotation&255,duration=duration)
  elif control:
   value=int(control[2],0)
   if not 0<=value<=32767:raise ValueError('Task affine control outside source representation')
   command.update(op=control[1].lower());command['target' if control[1]=='JUMP' else 'count']=value
  elif row=='AFFINEANIMCMD_END':command['op']='end'
  else:raise ValueError('Unsupported task affine command '+row)
  commands.append(command)
 if not commands or len(commands)>256 or commands[-1]['op'] not in {'end','jump'}:raise ValueError('Unterminated task affine program')
 if any(c['op']=='jump' and c['target']>=len(commands) for c in commands):raise ValueError('Invalid task affine jump')
 return commands

def registered_sprite_pixels(raw,symbol,png,palette=None):
 # The compressed PNG can contain more tiles than LoadSpriteSheet copies.
 # Preserve only the source sheet's registered tile count, never unused art.
 source=raw['src/data/battle_anim.h'].decode()
 row=re.search(r'\{'+re.escape(symbol)+r', (0x[0-9A-Fa-f]+|\d+), ANIM_TAG_\w+\}',source)
 if not row:raise ValueError('Missing source sprite sheet registration '+symbol)
 size=int(row[1],0);data=raw['graphics/battle_anims/sprites/'+png+'.png']
 with Image.open(io.BytesIO(data)) as image:width,height=image.size
 pixels=size*2
 if size<=0 or size%32 or width%8 or height%8 or pixels%width or (pixels//width)%8 or pixels>width*height:raise ValueError('Sprite sheet registration must span complete source tile rows')
 converted=hit.rgb555(data,(width,height),palette)
 return struct.pack('<HH',width,pixels//width)+converted[4:4+pixels*2]

def build_assets(reference):
 raw=read_sources(reference);png=raw['graphics/battle_anims/sprites/slam_hit.png']
 lines=raw['graphics/battle_anims/sprites/whip_hit.pal'].decode().splitlines()
 if lines[:3]!=['JASC-PAL','0100','16']:raise ValueError('Whip source palette differs')
 palette=[int(c) for line in lines[3:] for c in line.split()]
 if len(palette)!=48:raise ValueError('Whip palette size differs')
 if not re.search(r'\{gBattleAnimSpritePal_BlueLightWall,\s*ANIM_TAG_TORN_METAL\}',raw['src/data/battle_anim.h'].decode()):raise ValueError('Torn metal source palette alias differs')
 blue_rows=raw['graphics/battle_anims/sprites/blue_light_wall.pal'].decode().splitlines()
 if blue_rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Blue wall source palette differs')
 blue_palette=[int(c) for row in blue_rows[3:] for c in row.split()]
 if len(blue_palette)!=48:raise ValueError('Blue wall palette size differs')
 assets={'claw-slash':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_ClawSlash','claw_slash'),'torn-metal':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_TornMetal','torn_metal',blue_palette),'bone':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Bone','bone'),'eye-sparkle':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_EyeSparkle','eye_sparkle'),'anger':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Anger','anger'),'eclipsing-orb':hit.rgb555(raw['graphics/battle_anims/sprites/eclipsing_orb.png'],(32,128)),'breath':hit.rgb555(raw['graphics/battle_anims/sprites/breath.png'],(16,64)),'slam-hit':hit.rgb555(png,(32,256)),'whip-hit':hit.rgb555(png,(32,256),palette)}
 powder=raw['graphics/battle_anims/sprites/poison_powder.png']
 assets['poison-powder']=hit.rgb555(powder,(8,128))
 for name in ['sleep-powder','stun-spore']:
  rows=raw['graphics/battle_anims/sprites/'+name.replace('-','_')+'.pal'].decode().splitlines()
  if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Powder palette differs')
  colors=[int(c) for row in rows[3:] for c in row.split()]
  if len(colors)!=48:raise ValueError('Powder palette size differs')
  assets[name]=hit.rgb555(powder,(8,128),colors)
 # Source 1D tile order: 8x8 dust first, then the 16x16 mud image.
 colors=[int(c) for row in raw['graphics/battle_anims/sprites/mud_sand.pal'].decode().splitlines()[3:] for c in row.split()]
 if len(colors)!=48:raise ValueError('Mud palette size differs')
 tiles=[]
 for name in ['mud_sand_0','mud_sand_1']:
  with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/'+name+'.png'])) as image:
   for y in range(0,image.height,8):
    for x in range(0,image.width,8):tiles.append(image.crop((x,y,x+8,y+8)))
 image=Image.new('P',(8,len(tiles)*8));image.putpalette(colors)
 for i,tile in enumerate(tiles):image.paste(tile,(0,i*8))
 buffer=io.BytesIO();image.save(buffer,format='PNG');assets['mud-sand']=hit.rgb555(buffer.getvalue(),image.size)
 return assets

def build(reference):
 raw=read_sources(reference);source=raw['data/battle_anim_scripts.s'].decode()
 fight=raw['src/battle_anim_fight.c'].decode()
 for template,callback,table in [('gKarateChopSpriteTemplate','AnimSlideHandOrFootToTarget','sAnims_HandsAndFeet'),('gStompFootSpriteTemplate','AnimStompFoot','&sAnims_HandsAndFeet[1]'),('gJumpKickSpriteTemplate','AnimJumpKick','sAnims_HandsAndFeet'),('gSlidingKickSpriteTemplate','AnimSlidingKick','&sAnims_HandsAndFeet[1]')]:
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',fight,re.S)
  if match is None:raise ValueError('Missing physical sprite template: '+template)
  fields=dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
  expected={'tileTag':'ANIM_TAG_HANDS_AND_FEET','paletteTag':'ANIM_TAG_HANDS_AND_FEET','oam':'&gOamData_AffineOff_ObjNormal_32x32','anims':table,'callback':callback}
  if any(fields.get(k)!=v for k,v in expected.items()):raise ValueError('Physical source recipe differs: '+template)

 ids={m:int(n) for m,n in re.findall(r'^#define (MOVE_\w+)\s+(\d+)\s*$',raw['include/constants/moves.h'].decode(),re.M)}
 tag_source=raw['include/constants/battle_anim.h'].decode()
 tag_start=int(re.search(r'^#define ANIM_SPRITES_START\s+(\d+)\b',tag_source,re.M)[1])
 tags={name:tag_start+int(offset) for name,offset in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',tag_source,re.M)}
 effects=dict(re.findall(r'\[(MOVE_\w+)\] =\s*\{\s*\.effect = (EFFECT_\w+),',raw['src/data/battle_moves.h'].decode()))
 scripts=[];blocked=[];subroutines={}
 def subcommands(label):
  match=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not match:raise ValueError('Missing source subroutine '+label)
  result=[]
  for offset,line in enumerate(source[match.end():].splitlines()):
   text=line.split('@',1)[0].strip()
   if not text:continue
   if text.endswith(':'):raise ValueError('Unterminated source subroutine')
   op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op in {'call','end'}:raise ValueError('Unsupported nested subroutine')
   result.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
   if op=='return':return result
  raise ValueError('Unterminated source subroutine')
 def source_block(label):
  match=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not match:raise ValueError('Missing source label '+label)
  result=[]
  for offset,line in enumerate(source[match.end():].splitlines()):
   text=line.split('@',1)[0].strip()
   if not text:continue
   if text.endswith(':'):raise ValueError('Unterminated source block '+label)
   op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   result.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
   if op in {'choosetwoturnanim','goto','end','return'}:return result
  raise ValueError('Unterminated source block '+label)
 def branch_targets(commands):
  return [target for c in commands for target in (c['args'] if c['op']=='choosetwoturnanim' else c['args'][:1] if c['op']=='goto' else c['args'][1:2] if c['op']=='jumpifmoveturn' else [])]
 for match in re.finditer(r'^(Move_(\w+)):\n',source,re.M):
  commands=[];gaps=set();symbols=set();labels={}
  try:
   commands=source_block(match[1]);graph={match[1]:branch_targets(commands)};pending=list(graph[match[1]])
   while pending:
    label=pending.pop(0)
    if label in graph:continue
    labels[label]=len(commands);block=source_block(label);commands.extend(block)
    graph[label]=branch_targets(block);pending.extend(graph[label])
    if len(commands)>128:raise ValueError('Source command bound')
   active=set();done=set()
   def visit(label):
    if label in active:raise ValueError('Unbounded source branch cycle')
    if label in done:return
    active.add(label)
    for target in graph[label]:visit(target)
    active.remove(label);done.add(label)
   visit(match[1])
  except ValueError:gaps.add('control-flow:unterminated')
  for command in commands:
   op=command['op'];args=command['args']
   if op not in hit.OPS|{'waitplaysewithpan','call','splitbgprio','goto','choosetwoturnanim','jumpifmoveturn'}:gaps.add('command:'+op)
   if op=='jumpifmoveturn':
    symbols.add(op)
    if len(args)!=2 or not args[0].isdigit() or not 0<=int(args[0])<=255:gaps.add('control-flow:branch-operands')
   if op in {'goto','choosetwoturnanim'}:
    symbols.add(op)
    if len(args)!=(2 if op=='choosetwoturnanim' else 1):gaps.add('control-flow:branch-operands')
   if op in ('createsprite','createvisualtask'):
    symbols.add(args[0]);allowed=SPRITES if op=='createsprite' else TASKS
    if args[0] not in allowed:gaps.add('callback:'+args[0])
   if op=='loadspritegfx' and args[0] not in {'ANIM_TAG_NEEDLE','ANIM_TAG_CLAW_SLASH','ANIM_TAG_TORN_METAL','ANIM_TAG_BLUE_LIGHT_WALL','ANIM_TAG_SMALL_EMBER','ANIM_TAG_BONE','ANIM_TAG_EYE_SPARKLE','ANIM_TAG_ANGER','ANIM_TAG_ECLIPSING_ORB','ANIM_TAG_BREATH','ANIM_TAG_BLUE_STAR','ANIM_TAG_MUD_SAND','ANIM_TAG_IMPACT','ANIM_TAG_HANDS_AND_FEET','ANIM_TAG_CUT','ANIM_TAG_SLAM_HIT','ANIM_TAG_WHIP_HIT','ANIM_TAG_POISON_POWDER','ANIM_TAG_SLEEP_POWDER','ANIM_TAG_STUN_SPORE','ANIM_TAG_POISON_BUBBLE'}:gaps.add('graphics:'+args[0])
  if not symbols & NEW:continue
  # Move-turn commands require an admitted invocation contract as well as pixels.
  # Beat Up uses contributor traversal; charge moves use a turn lock, neither is
  # an ordinary EFFECT_MULTI_HIT receipt ordinal. Keep those contexts separate.
  effect=effects.get('MOVE_'+match[2])
  if 'choosetwoturnanim' in symbols and effect!='EFFECT_MULTI_HIT':gaps.add('invocation:move-turn')
  if 'jumpifmoveturn' in symbols and effect!='EFFECT_TRIPLE_KICK':gaps.add('invocation:move-turn')
  if not commands or commands[-1]['op']!='end':gaps.add('control-flow:unterminated')
  if len(commands)>128:gaps.add('command-bound')
  local_subs={}
  for command in commands:
   if command['op']!='call':continue
   try:sub=subcommands(command['args'][0])
   except ValueError:gaps.add('control-flow:subroutine');continue
   for row in sub:
    if row['op'] not in hit.OPS|{'return','waitplaysewithpan'}:gaps.add('command:'+row['op'])
    if row['op'] in {'createsprite','createvisualtask'} and row['args'][0] not in (SPRITES if row['op']=='createsprite' else TASKS):gaps.add('callback:'+row['args'][0])
   local_subs[command['args'][0]]=sub
  move=ids.get('MOVE_'+match[2])
  if not move or move>354:continue
  if gaps:blocked.append(dict(moveId=move,missing=sorted(gaps)));continue
  dependencies=commands+[c for sub in local_subs.values() for c in sub]
  loaded=sorted({c['args'][0] for c in dependencies if c['op']=='loadspritegfx'})
  if not set(loaded)<=tags.keys():raise ValueError('Undefined source sprite tag dependency')
  scripts.append(dict(moveId=move,label=match[1],commands=commands,labels=labels,soundEffects=elemental.sound_effects(reference,dependencies),spriteTags={name:tags[name] for name in loaded}));subroutines.update(local_subs)
 if not scripts:raise ValueError('No supported physical roots')
 return (json.dumps(dict(schemaVersion=1,scripts=scripts,subroutines=subroutines,taskAffinePrograms={name:task_affine_program(raw['src/'+file].decode(),name) for file,name in [('battle_anim_effects_2.c','sSplashEffectAffineAnimCmds'),('battle_anim_effects_2.c','sGrowAndShrinkAffineAnimCmds'),('battle_anim_effects_3.c','sSlackOffSquishAffineAnimCmds'),('battle_anim_effects_3.c','DefenseCurlDeformMonAffineAnimCmds')]},assets=[dict(id=id,path='physical-'+id+'.rgb555') for id in build_assets(reference)]+[dict(id='needle',path='elemental-needle.rgb555'),dict(id='poison-bubble',path='elemental-poison-bubble.rgb555'),dict(id='blue-star',path='elemental-blue-star.rgb555'),dict(id='small-ember',path='elemental-small-ember.rgb555')],spriteRecipes=[elemental.recipe(raw['src/battle_anim_bug.c'].decode(),'gLinearStingerSpriteTemplate','AnimTranslateStinger'),elemental.recipe(raw['src/battle_anim_normal.c'].decode(),'gHandleInvertHitSplatSpriteTemplate','AnimHitSplatHandleInvert'),elemental.recipe(raw['src/battle_anim_poison.c'].decode(),'gPoisonBubbleSpriteTemplate','AnimBubbleEffect'),elemental.recipe(raw['src/battle_anim_dark.c'].decode(),'gClawSlashSpriteTemplate','AnimClawSlash'),elemental.recipe(raw['src/battle_anim_rock.c'].decode()+raw['src/battle_anim_fire.c'].decode(),'gFireSpinSpriteTemplate','AnimParticleInVortex'),elemental.recipe(raw['src/battle_anim_ground.c'].decode(),'gSpinningBoneSpriteTemplate','AnimBoneHitProjectile'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gEyeSparkleSpriteTemplate','eye-sparkle','AnimEyeSparkle'),elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gAngerMarkSpriteTemplate','AnimAngerMark'),sprite_recipe(raw['src/battle_anim_effects_3.c'].decode(),'gEclipsingOrbSpriteTemplate','eclipsing-orb','AnimSpriteOnMonPos'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gBreathPuffSpriteTemplate','breath','AnimBreathPuff'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gHealingBlueStarSpriteTemplate','blue-star','AnimSpriteOnMonPos'),sprite_recipe(raw['src/battle_anim_ground.c'].decode(),'gMudsportMudSpriteTemplate','mud-sand','AnimMudSportDirt'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gViceGripSpriteTemplate','cut','AnimViceGripPincer'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gSlamHitSpriteTemplate','slam-hit','AnimWhipHit'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gVineWhipSpriteTemplate','whip-hit','AnimWhipHit')]+[sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),template,asset,'AnimMovePowderParticle') for template,asset in [('gPoisonPowderParticleSpriteTemplate','poison-powder'),('gSleepPowderParticleSpriteTemplate','sleep-powder'),('gStunSporeParticleSpriteTemplate','stun-spore')]]),indent=2)+'\n').encode(),blocked
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/physical.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,blocked=build(a.reference);assets=build_assets(a.reference)
 if a.check:
  if a.output.read_bytes()!=data:raise SystemExit('Physical animation pack differs')
  for id,blob in assets.items():
   if (a.output.parent/('physical-'+id+'.rgb555')).read_bytes()!=blob:raise SystemExit('Physical sprite pixels differ')
 else:
  a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_bytes(data)
  for id,blob in assets.items():(a.output.parent/('physical-'+id+'.rgb555')).write_bytes(blob)
 print(json.dumps(dict(passed=True,roots=[s['moveId'] for s in json.loads(data)['scripts']],blocked=blocked)))
