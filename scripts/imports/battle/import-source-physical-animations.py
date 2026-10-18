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
 paths=('src/battle_anim_mon_movement.c','src/battle_anim_status_effects.c','src/battle_anim_psychic.c','graphics/battle_anims/sprites/sparkle_4.png','graphics/battle_anims/sprites/sparkle_3.png','graphics/battle_anims/sprites/x_sign.png','graphics/battle_anims/sprites/hollow_orb.png','asm/macros/battle_anim_script.inc','src/battle_anim_ghost.c','src/battle_anim_mons.c','graphics/battle_anims/sprites/lick.png','graphics/battle_anims/sprites/movement_waves.png','data/battle_scripts_1.s','data/battle_anim_scripts.s','src/data/battle_moves.h','include/constants/moves.h','include/constants/battle_anim.h','src/data/battle_anim.h','src/battle_anim_fight.c','src/battle_anim_bug.c','src/battle_anim_normal.c','src/battle_anim_poison.c','src/battle_anim_dark.c','src/battle_anim_rock.c','src/battle_anim_fire.c',
        'src/graphics.c','src/battle_anim_flying.c','src/battle_anim_effects_1.c','src/battle_anim_effects_2.c','src/battle_anim_ground.c','src/battle_anim_effects_3.c',
        'graphics/battle_anims/sprites/tri_attack_triangle.png','graphics/battle_anims/sprites/sweat_drop.png','graphics/battle_anims/sprites/tag_hand.png','graphics/battle_anims/sprites/finger.png','graphics/battle_anims/sprites/smellingsalt_effect.png','graphics/battle_anims/sprites/red_orb.png','graphics/battle_anims/sprites/sweat_bead.png','graphics/battle_anims/sprites/splash.png','graphics/battle_anims/sprites/coin.png','graphics/battle_anims/sprites/cross_impact.png','graphics/battle_anims/sprites/red_ball.png','graphics/battle_anims/sprites/whirlwind_lines.png','graphics/battle_anims/sprites/gold_stars.png','graphics/battle_anims/sprites/swipe.png','graphics/battle_anims/sprites/fang_attack.png','graphics/battle_anims/sprites/seed.png','graphics/battle_anims/sprites/icicle_spear.png','graphics/battle_anims/sprites/claw_slash.png','graphics/battle_anims/sprites/torn_metal.png','graphics/battle_anims/sprites/blue_light_wall.pal','graphics/battle_anims/sprites/bone.png','graphics/battle_anims/sprites/eye_sparkle.png','graphics/battle_anims/sprites/anger.png','graphics/battle_anims/sprites/breath.png','graphics/battle_anims/sprites/eclipsing_orb.png',
        'graphics/battle_anims/sprites/mud_sand_0.png','graphics/battle_anims/sprites/mud_sand_1.png','graphics/battle_anims/sprites/mud_sand.pal',
        'graphics/battle_anims/sprites/slam_hit.png','graphics/battle_anims/sprites/whip_hit.pal','graphics/battle_anims/sprites/poison_powder.png',
        'graphics/battle_anims/sprites/sleep_powder.pal','graphics/battle_anims/sprites/stun_spore.pal')
 return {path:(subprocess.check_output(['git','-C',str(reference),'show',hit.COMMIT+':'+path])
               if (reference/'.git').exists() else (reference/path).read_bytes()) for path in paths}
SPRITES={'gFistFootRandomPosSpriteTemplate','gWishStarSpriteTemplate','gGrantingStarsSpriteTemplate','gMeteorMashStarSpriteTemplate','gArmThrustHandSpriteTemplate','gFurySwipesSpriteTemplate','gFangSpriteTemplate','gSuperFangSpriteTemplate','gBulletSeedSpriteTemplate','gSandAttackDirtSpriteTemplate','gMudSlapMudSpriteTemplate','gMegaPunchKickSpriteTemplate','gSpinningHandOrFootSpriteTemplate','gFireSpreadSpriteTemplate','gPinMissileSpriteTemplate','gIcicleSpearSpriteTemplate','gLinearStingerSpriteTemplate','gHandleInvertHitSplatSpriteTemplate','gPoisonBubbleSpriteTemplate','gClawSlashSpriteTemplate','gFireSpinSpriteTemplate','gShakeMonOrTerrainSpriteTemplate','gSpinningBoneSpriteTemplate','gFistFootSpriteTemplate','gEyeSparkleSpriteTemplate','gAngerMarkSpriteTemplate','gEclipsingOrbSpriteTemplate','gBreathPuffSpriteTemplate','gHealingBlueStarSpriteTemplate','gMudsportMudSpriteTemplate','gKarateChopSpriteTemplate','gPoisonPowderParticleSpriteTemplate','gStunSporeParticleSpriteTemplate','gSleepPowderParticleSpriteTemplate','gBowMonSpriteTemplate','gFlashingHitSplatSpriteTemplate','gSimplePaletteBlendSpriteTemplate','gSlamHitSpriteTemplate','gVineWhipSpriteTemplate','gViceGripSpriteTemplate','gBasicHitSplatSpriteTemplate','gHorizontalLungeSpriteTemplate','gSlideMonToOffsetSpriteTemplate','gSlideMonToOriginalPosSpriteTemplate','gComplexPaletteBlendSpriteTemplate','gVerticalDipSpriteTemplate','gStompFootSpriteTemplate','gJumpKickSpriteTemplate','gSlidingKickSpriteTemplate'}
TASKS={'AnimTask_MeditateStretchAttacker','AnimTask_BlendMonInAndOut','AnimTask_BlendColorCycle','AnimTask_IsContest','AnimTask_IsTargetPlayerSide','AnimTask_MetallicShine','AnimTask_BlendBattleAnimPal','SoundTask_PlaySE1WithPanning','AnimTask_RockMonBackAndForth','AnimTask_DefenseCurlDeformMon','AnimTask_SetGrayscaleOrOriginalPal','AnimTask_GrowAndShrink','AnimTask_SlackOffSquish','AnimTask_Splash','AnimTask_ScaleMonAndRestore','AnimTask_SwayMon','AnimTask_TranslateMonElliptical','AnimTask_RotateMonToSideAndRestore','AnimTask_ShakeMon','AnimTask_ShakeMon2','AnimTask_TranslateMonEllipticalRespectSide','AnimTask_WindUpLunge','AnimTask_ShakeAndSinkMon','AnimTask_ShakeMonInPlace','AnimTask_RotateMonSpriteToSide'}
SPRITES|={'gFocusPunchFistSpriteTemplate'}
TASKS|={'GetIsDoomDesireHitTurn','AnimTask_FakeOut'}
TASKS|={'AnimTask_HorizontalShake','AnimTask_ShakeTargetBasedOnMovePowerOrDmg','AnimTask_IsPowerOver99'}
NEW= {'gFistFootRandomPosSpriteTemplate','gWishStarSpriteTemplate','gMeteorMashStarSpriteTemplate','gArmThrustHandSpriteTemplate','gFurySwipesSpriteTemplate','gFangSpriteTemplate','gSuperFangSpriteTemplate','gBulletSeedSpriteTemplate','gSandAttackDirtSpriteTemplate','gMudSlapMudSpriteTemplate','gMegaPunchKickSpriteTemplate','gSpinningHandOrFootSpriteTemplate','gPinMissileSpriteTemplate','gIcicleSpearSpriteTemplate','gLinearStingerSpriteTemplate','gHandleInvertHitSplatSpriteTemplate','gClawSlashSpriteTemplate','gSpinningBoneSpriteTemplate','jumpifmoveturn','choosetwoturnanim','gEyeSparkleSpriteTemplate','gAngerMarkSpriteTemplate','AnimTask_DefenseCurlDeformMon','AnimTask_GrowAndShrink','AnimTask_SlackOffSquish','AnimTask_Splash','gMudsportMudSpriteTemplate','goto','gKarateChopSpriteTemplate','AnimTask_TranslateMonElliptical','gPoisonPowderParticleSpriteTemplate','gStunSporeParticleSpriteTemplate','gSleepPowderParticleSpriteTemplate','gBowMonSpriteTemplate','gFlashingHitSplatSpriteTemplate','gSlamHitSpriteTemplate','gVineWhipSpriteTemplate','gViceGripSpriteTemplate','gVerticalDipSpriteTemplate','gStompFootSpriteTemplate','gJumpKickSpriteTemplate','gSlidingKickSpriteTemplate','AnimTask_WindUpLunge','AnimTask_ShakeAndSinkMon','AnimTask_ShakeMonInPlace','AnimTask_RotateMonSpriteToSide'}
SPRITES|={'gCoinThrowSpriteTemplate','gFallingCoinSpriteTemplate','gCrossChopHandSpriteTemplate','gCrossImpactSpriteTemplate'}
TASKS|={'AnimTask_DrillPeckHitSplats','AnimTask_BarrageBall'}
NEW|={'gCoinThrowSpriteTemplate','gFallingCoinSpriteTemplate','gCrossChopHandSpriteTemplate','gCrossImpactSpriteTemplate','AnimTask_DrillPeckHitSplats','AnimTask_BarrageBall'}
NEW|={'GetIsDoomDesireHitTurn','gFocusPunchFistSpriteTemplate','AnimTask_FakeOut'}
NEW|={'AnimTask_HorizontalShake','AnimTask_ShakeTargetBasedOnMovePowerOrDmg','AnimTask_IsPowerOver99'}
SPRITES|={'gHiddenPowerOrbSpriteTemplate','gHiddenPowerOrbScatterSpriteTemplate','gSprayWaterDropletSpriteTemplate','gRazorLeafParticleSpriteTemplate','gRazorLeafCutterSpriteTemplate'}
TASKS|={'AnimTask_CycleMagicalLeafPal','AnimTask_MoveSkyUppercutBg','AnimTask_StretchTargetUp'}
NEW|={'gHiddenPowerOrbSpriteTemplate','gHiddenPowerOrbScatterSpriteTemplate','gSprayWaterDropletSpriteTemplate','AnimTask_CycleMagicalLeafPal','AnimTask_MoveSkyUppercutBg','AnimTask_StretchTargetUp'}
# These shared elemental callbacks close Tri Attack's original fire/ice/lightning phases.
SPRITES|={'gTriAttackTriangleSpriteTemplate','gSmellingSaltsHandSpriteTemplate','gSmellingSaltExclamationSpriteTemplate','gLargeFlameScatterSpriteTemplate','gLightningSpriteTemplate','gIceCrystalHitLargeSpriteTemplate','gIceCrystalHitSmallSpriteTemplate'}
TASKS|={'AnimTask_SquishAndSweatDroplets','AnimTask_FacadeColorBlend','AnimTask_SmellingSaltsSquish','AnimTask_LeafBlade','AnimTask_ShakeTargetInPattern','AnimTask_InvertScreenColor'}
NEW|={'gTriAttackTriangleSpriteTemplate','gSmellingSaltsHandSpriteTemplate','gSmellingSaltExclamationSpriteTemplate','AnimTask_SquishAndSweatDroplets','AnimTask_FacadeColorBlend','AnimTask_SmellingSaltsSquish','AnimTask_LeafBlade'}
CHARGE_PAIR_SPRITES={'gRazorWindTornadoSpriteTemplate','gAirWaveCrescentSpriteTemplate','gSlideMonToOffsetAndBackSpriteTemplate'}
SPRITES|=CHARGE_PAIR_SPRITES
TASKS|={'AnimTask_SkullBashPosition'}
NEW|=CHARGE_PAIR_SPRITES|{'AnimTask_SkullBashPosition'}
CHARGE_PAIR_WIDTHS={'gRazorWindTornadoSpriteTemplate':10,'gSlideMonToOffsetAndBackSpriteTemplate':9,'AnimTask_SkullBashPosition':3}
LOCKED_PAIR_TASKS={'AnimTask_ThrashMoveMonHorizontal','AnimTask_ThrashMoveMonVertical'}
TASKS|=LOCKED_PAIR_TASKS
NEW|=LOCKED_PAIR_TASKS|{'choosetwoturnanim'}
CUT_WALL_RECIPES=[('gTwisterLeafSpriteTemplate','battle_anim_effects_1.c','AnimMoveTwisterParticle'),('gTwisterRockSpriteTemplate','battle_anim_rock.c','AnimMoveTwisterParticle'),('gBrickBreakWallSpriteTemplate','battle_anim_fight.c','AnimBrickBreakWall'),('gBrickBreakWallShardSpriteTemplate','battle_anim_fight.c','AnimBrickBreakWallShard')]
CUT_WALL_TASKS={'AnimTask_IsFuryCutterHitRight','AnimTask_GetFuryCutterHitCount'}
SPRITES|={r[0] for r in CUT_WALL_RECIPES}|{'gCuttingSliceSpriteTemplate'}
TASKS|=CUT_WALL_TASKS
NEW|={r[0] for r in CUT_WALL_RECIPES}|CUT_WALL_TASKS
FLAIL_RECIPES=[('gRandomPosHitSplatSpriteTemplate','battle_anim_normal.c','AnimHitSplatRandom')]
SPRITES|={r[0] for r in FLAIL_RECIPES}
TASKS|={'AnimTask_FlailMovement'}
NEW|={r[0] for r in FLAIL_RECIPES}|{'AnimTask_FlailMovement'}
ROCK_STATUS_RECIPES=[('gAncientPowerRockSpriteTemplate','battle_anim_rock.c','AnimRaiseSprite'),('gRockTombRockSpriteTemplate','battle_anim_rock.c','AnimRockTomb'),('gRedXSpriteTemplate','battle_anim_psychic.c','AnimRedX'),('gSpinningSparkleSpriteTemplate','battle_anim_status_effects.c','AnimSpinningSparkle')]
ROCK_STATUS_MATERIALS=[('x_sign','XSign',0x0800,(64,64),'HollowOrb','hollow_orb')]
# Sparkle4 is already owned by the wall pack with this exact registered alias.
ROCK_STATUS_SHARED_MATERIALS=[('sparkle_4','Sparkle4',0x0A00,(32,160),'Sparkle3','sparkle_3')]
SPRITES|={r[0] for r in ROCK_STATUS_RECIPES}
TASKS|={'AnimTask_GrowAndGrayscale','AnimTask_ShakeBattleTerrain'}
NEW|={r[0] for r in ROCK_STATUS_RECIPES}|{'AnimTask_GrowAndGrayscale'}
FRIENDSHIP_CONTACT_RECIPES=[('gLickSpriteTemplate','battle_anim_ghost.c','AnimLick'),('gMovementWavesSpriteTemplate','battle_anim_effects_2.c','AnimMovementWaves'),('gWeakFrustrationAngerMarkSpriteTemplate','battle_anim_effects_3.c','AnimWeakFrustrationAngerMark')]
FRIENDSHIP_CONTACT_TASKS={'AnimTask_GetReturnPowerLevel','AnimTask_GetFrustrationPowerLevel','AnimTask_StrongFrustrationGrowAndShrink'}
SPRITES|={r[0] for r in FRIENDSHIP_CONTACT_RECIPES}
TASKS|=FRIENDSHIP_CONTACT_TASKS|{'AnimTask_TraceMonBlended','SoundTask_PlaySE2WithPanning'}
NEW|={r[0] for r in FRIENDSHIP_CONTACT_RECIPES}|FRIENDSHIP_CONTACT_TASKS
FRIENDSHIP_CONTACT_MATERIALS=[('lick','Lick',0x0500,(16,160),'Lick','lick'),('movement_waves','MovementWaves',0x0600,(32,96),'MovementWaves','movement_waves'),('anger','Anger',0x0080,(16,16),'Anger','anger')]
REACTION_RECIPES=[('gTriAttackTriangleSpriteTemplate','battle_anim_effects_3.c','AnimTriAttackTriangle'),('gFacadeSweatDropSpriteTemplate','battle_anim_effects_3.c','AnimFacadeSweatDrop'),('gSmellingSaltsHandSpriteTemplate','battle_anim_effects_3.c','AnimSmellingSaltsHand'),('gSmellingSaltExclamationSpriteTemplate','battle_anim_effects_3.c','AnimSmellingSaltExclamation'),('gLeafBladeSpriteTemplate','battle_anim_effects_1.c','SpriteCallbackDummy')]
REACTION_MATERIALS=[('tri_attack_triangle','TriAttackTriangle',0x0800,(64,64),'TriAttackTriangle','tri_attack_triangle'),('sweat_drop','SweatDrop',0x0020,(8,8),'SweatDrop','sweat_drop'),('tag_hand','TagHand',0x0400,(32,64),'Finger','finger'),('smellingsalt_effect','SmellingsaltEffect',0x0200,(32,32),'SmellingsaltEffect','smellingsalt_effect')]
def focus_punch_recipe(source):
 body=re.search(r'static void AnimFocusPunchFist\(struct Sprite \*sprite\)\s*\{(.*?)^\}',source,re.M|re.S)
 expected='if(sprite->affineAnimEnded){sprite->data[1]=(sprite->data[1]+40)&0xFF;sprite->x2=Sin(sprite->data[1],2);if(++sprite->data[0]>40)DestroyAnimSprite(sprite);}'
 if body is None or re.sub(r'\s+','',body[1])!=expected:raise ValueError('Focus Punch callback contract differs')
 result=elemental.recipe(source,'gFocusPunchFistSpriteTemplate','AnimFocusPunchFist')
 expected_affine=[dict(op='frame',x=512,y=512,rotation=0,duration=0,target=0),dict(op='frame',x=-32,y=-32,rotation=0,duration=8,target=0),dict(op='end',x=0,y=0,rotation=0,duration=0,target=0)]
 if result['asset']!='hands-and-feet' or result['width']!=32 or result['height']!=32 or result['blend'] or not result.get('affineDoubleSize') or result.get('affineAnimations')!=[expected_affine]:raise ValueError('Focus Punch source recipe differs')
 return result

def cut_wall_recipe(source,name,callback):
 # Preserve an actual pointer into the original frame-pointer table. No frame
 # aliasing or out-of-range clamping: this template exposes entries four onward.
 if name!='gTwisterRockSpriteTemplate':return elemental.recipe(source,name,callback)
 template=re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{(.*?)\};',source,re.S)
 if template is None or '.anims = &sAnims_BasicRock[4],' not in template[1]:raise ValueError('Twister rock frame pointer differs')
 block=template[0];translated=source.replace(block,block.replace('.anims = &sAnims_BasicRock[4],','.anims = sAnims_BasicRock,'),1)
 result=elemental.recipe(translated,name,callback)
 if len(result['animations'])!=6:raise ValueError('Twister rock frame table capacity differs')
 result['animations']=result['animations'][4:];result['loopStarts']=result['loopStarts'][4:]
 return result

def reaction_palette(raw,stem):
 with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/'+stem+'.png'])) as image:return image.getpalette()[:48]
def validate_reaction_sources(raw):
 registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
 for stem,symbol,size,shape,pal_symbol,pal_stem in REACTION_MATERIALS+FRIENDSHIP_CONTACT_MATERIALS+ROCK_STATUS_MATERIALS+ROCK_STATUS_SHARED_MATERIALS:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registration,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal_symbol+r',\s*'+tag+r'\}',registration):raise ValueError('Reaction source sheet/palette registration differs: '+stem)
  for kind,symbol,name,suffix in [('Gfx',symbol,stem,'4bpp'),('Pal',pal_symbol,pal_stem,'gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+name+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Reaction source material differs: '+stem)
 colors=re.search(r'const u16 sFacadeBlendColors\[\] =\s*\{(.*?)\};',raw['src/battle_anim_effects_3.c'].decode(),re.S)
 expected=[(28,25,1),(28,21,5),(27,18,8),(27,14,11),(26,10,15),(26,7,18),(25,3,21),(25,0,25),(25,0,23),(25,0,20),(25,0,16),(25,0,13),(26,0,10),(26,0,6),(26,0,3),(27,0,0),(27,1,0),(27,5,0),(27,9,0),(27,12,0),(28,16,0),(28,19,0),(28,23,0),(29,27,0)]
 if colors is None or [tuple(map(int,row)) for row in re.findall(r'RGB\(\s*(\d+),\s*(\d+),\s*(\d+)\)',colors[1])]!=expected:raise ValueError('Facade source colors differ')

def validate_friendship_predicates(raw):
 macro=raw['asm/macros/battle_anim_script.inc'].decode()
 if not re.search(r'\.macro jumpreteq value:req, ptr:req\s+jumpargeq ARG_RET_ID, \\value, \\ptr\s+\.endm',macro) or not re.search(r'^#define ARG_RET_ID 7$',raw['include/constants/battle_anim.h'].decode(),re.M):raise ValueError('Source return-register branch macro differs')
 for file,name,conditions in [('battle_anim_effects_3.c','AnimTask_GetReturnPowerLevel',['gAnimFriendship<60','gAnimFriendship>60&&gAnimFriendship<92','gAnimFriendship>91&&gAnimFriendship<201','gAnimFriendship>200']),('battle_anim_mons.c','AnimTask_GetFrustrationPowerLevel',['gAnimFriendship<=30','gAnimFriendship<=100','gAnimFriendship<=200'])]:
  body=re.search(r'void '+name+r'\(u8 taskId\)\s*\{(.*?)\n\}',raw['src/'+file].decode(),re.S)
  if body is None or re.findall(r'if\((.*?)\)',re.sub(r'\s+','',body[1]))!=conditions or 'gBattleMovePower' in body[1]:raise ValueError('Source friendship predicates differ')
  compact=re.sub(r'\s+','',body[1]);expected=['0','0','1','2','3'] if name=='AnimTask_GetReturnPowerLevel' else ['0','1','2','3']
  output=r'gBattleAnimArgs\[ARG_RET_ID\]=(\d);' if name=='AnimTask_GetReturnPowerLevel' else r'powerLevel=(\d);'
  if re.findall(output,compact)!=expected or compact.count('DestroyAnimVisualTask(taskId);')!=1 or (name=='AnimTask_GetFrustrationPowerLevel' and 'gBattleAnimArgs[ARG_RET_ID]=powerLevel;' not in compact):raise ValueError('Source friendship predicate result differs')

def requires_uncommitted_power(effect,commands,raw):
 # Endeavor computes fixed HP-difference damage then enters HitFromAtkAnimation.
 # Its source animation reads retained gBattleMovePower, not that hit's damage.
 # Keep this effect unadmitted until a real retained-power receipt exists.
 reads=any(c['op']=='createvisualtask' and len(c['args'])==7 and c['args'][0]=='AnimTask_ShakeTargetBasedOnMovePowerOrDmg' and c['args'][2] in {'0','FALSE'} for c in commands)
 if not reads or effect!='EFFECT_ENDEAVOR':return False
 body=raw['data/battle_scripts_1.s'].decode().split('BattleScript_EffectEndeavor::',1)[1].split('\nBattleScript_',1)[0]
 if 'setdamagetohealthdifference' not in body or 'adjustsetdamage' not in body or 'goto BattleScript_HitFromAtkAnimation' not in body or re.search(r'^\s*damagecalc\b',body,re.M):raise ValueError('Endeavor source power-provenance boundary differs')
 return True

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

task_affine_program=elemental.task_affine_program

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

def impact_emission_recipe(raw,template,file,callback):
 source=raw['src/'+file].decode()
 if template=='gCrossChopHandSpriteTemplate':
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',source,re.S)
  if match is None or '.anims = &sAnims_HandsAndFeet[3],' not in match[1]:raise ValueError('Cross Chop source frame-table offset differs')
  source=source[:match.start(1)]+match[1].replace('.anims = &sAnims_HandsAndFeet[3],','.anims = sAnims_HandsAndFeet,')+source[match.end(1):]
  result=elemental.recipe(source,template,callback)
  if len(result['animations'])!=5:raise ValueError('Cross Chop backing frame-table length differs')
  result['animations']=result['animations'][3:];result['loopStarts']=result['loopStarts'][3:]
  return result
 return elemental.recipe(source,template,callback)

ORBITAL_PARTICLE_RECIPES=[('gHiddenPowerOrbSpriteTemplate','battle_anim_effects_2.c','AnimOrbitFast'),('gHiddenPowerOrbScatterSpriteTemplate','battle_anim_effects_2.c','AnimOrbitScatter'),('gSprayWaterDropletSpriteTemplate','battle_anim_flying.c','AnimSprayWaterDroplet')]
IMPACT_EMISSION_RECIPES=[
 ('gCoinThrowSpriteTemplate','battle_anim_effects_2.c','AnimCoinThrow'),
 ('gFallingCoinSpriteTemplate','battle_anim_effects_2.c','AnimFallingCoin'),
 ('gCrossChopHandSpriteTemplate','battle_anim_fight.c','AnimCrossChopHand'),
 ('gCrossImpactSpriteTemplate','battle_anim_normal.c','AnimCrossImpact'),
 ('gFlashingHitSplatSpriteTemplate','battle_anim_normal.c','AnimFlashingHitSplat'),
 ('gBarrageBallSpriteTemplate','battle_anim_effects_3.c','SpriteCallbackDummy')]

def build_assets(reference):
 raw=read_sources(reference);validate_reaction_sources(raw);png=raw['graphics/battle_anims/sprites/slam_hit.png']
 lines=raw['graphics/battle_anims/sprites/whip_hit.pal'].decode().splitlines()
 if lines[:3]!=['JASC-PAL','0100','16']:raise ValueError('Whip source palette differs')
 palette=[int(c) for line in lines[3:] for c in line.split()]
 if len(palette)!=48:raise ValueError('Whip palette size differs')
 if not re.search(r'\{gBattleAnimSpritePal_BlueLightWall,\s*ANIM_TAG_TORN_METAL\}',raw['src/data/battle_anim.h'].decode()):raise ValueError('Torn metal source palette alias differs')
 blue_rows=raw['graphics/battle_anims/sprites/blue_light_wall.pal'].decode().splitlines()
 if blue_rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Blue wall source palette differs')
 blue_palette=[int(c) for row in blue_rows[3:] for c in row.split()]
 if len(blue_palette)!=48:raise ValueError('Blue wall palette size differs')
 if not re.search(r'\{gBattleAnimSpriteGfx_IcicleSpear,\s*0x0200,\s*ANIM_TAG_ICICLE_SPEAR\}',raw['src/data/battle_anim.h'].decode()) or not re.search(r'\{gBattleAnimSpritePal_IcicleSpear,\s*ANIM_TAG_ICICLE_SPEAR\}',raw['src/data/battle_anim.h'].decode()):raise ValueError('Icicle source sheet or palette registration differs')
 if not re.search(r'\{gBattleAnimSpriteGfx_FangAttack,\s*0x0800,\s*ANIM_TAG_FANG_ATTACK\}',raw['src/data/battle_anim.h'].decode()) or not re.search(r'\{gBattleAnimSpritePal_FangAttack,\s*ANIM_TAG_FANG_ATTACK\}',raw['src/data/battle_anim.h'].decode()):raise ValueError('Fang source registration differs')
 if not re.search(r'\{gBattleAnimSpriteGfx_Swipe,\s*0x0a00,\s*ANIM_TAG_SWIPE\}',raw['src/data/battle_anim.h'].decode()) or not re.search(r'\{gBattleAnimSpritePal_Swipe,\s*ANIM_TAG_SWIPE\}',raw['src/data/battle_anim.h'].decode()):raise ValueError('Swipe source registration differs')
 assets={'gold-stars':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_GoldStars','gold_stars'),'swipe':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Swipe','swipe'),'fang-attack':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_FangAttack','fang_attack'),'seed':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Seed','seed'),'icicle-spear':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_IcicleSpear','icicle_spear'),'claw-slash':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_ClawSlash','claw_slash'),'torn-metal':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_TornMetal','torn_metal',blue_palette),'bone':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Bone','bone'),'eye-sparkle':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_EyeSparkle','eye_sparkle'),'anger':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Anger','anger'),'eclipsing-orb':hit.rgb555(raw['graphics/battle_anims/sprites/eclipsing_orb.png'],(32,128)),'breath':hit.rgb555(raw['graphics/battle_anims/sprites/breath.png'],(16,64)),'slam-hit':hit.rgb555(png,(32,256)),'whip-hit':hit.rgb555(png,(32,256),palette)}
 # Match source tile/palette registration and graphic linkage, including the
 # loaded-but-unused Whirlwind Lines material in Drill Peck's original script.
 for stem,symbol,size in [('red_orb','RedOrb',0x0080),('coin','Coin',0x0200),('cross_impact','CrossImpact',0x0200),('red_ball','RedBall',0x0200),('whirlwind_lines','WhirlwindLines',0x0300)]:
  tag='ANIM_TAG_'+stem.upper();registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registration,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registration):raise ValueError('Impact emission source registration differs: '+stem)
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Impact emission source material differs: '+stem)
  assets[stem.replace('_','-')]=registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_'+symbol,stem)
 registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
 if not re.search(r'\{gBattleAnimSpriteGfx_SweatBead,\s*0x0020,\s*ANIM_TAG_SWEAT_BEAD\}',registration) or not re.search(r'\{gBattleAnimSpritePal_Splash,\s*ANIM_TAG_SWEAT_BEAD\}',registration):raise ValueError('Water droplet source palette alias differs')
 for kind,symbol,stem,suffix in [('Gfx','SweatBead','sweat_bead','4bpp'),('Pal','Splash','splash','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Water droplet source material differs')
 with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/splash.png'])) as image:shared_palette=image.getpalette()[:48]
 for stem,symbol,size,shape,pal_symbol,pal_stem in REACTION_MATERIALS+FRIENDSHIP_CONTACT_MATERIALS+ROCK_STATUS_MATERIALS:
  assets[stem.replace('_','-')]=registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_'+symbol,stem,reaction_palette(raw,pal_stem))
 assets['sweat-bead']=registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_SweatBead','sweat_bead',shared_palette)
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

def build_indexed_assets(reference):
 raw=read_sources(reference);indexed=[];pixels={}
 for stem,size in [('red_orb',(16,16)),('sweat_bead',(8,8)),('coin',(16,64)),('cross_impact',(32,32)),('red_ball',(32,32)),('whirlwind_lines',(32,48))]+[(r[0],r[3]) for r in REACTION_MATERIALS+FRIENDSHIP_CONTACT_MATERIALS+ROCK_STATUS_MATERIALS]:
  with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/'+stem+'.png'])) as image:
   if image.mode!='P' or image.size!=size:raise ValueError('Impact emission indexed source shape differs: '+stem)
   indices=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()];palette=image.getpalette()[:48]
  if stem=='sweat_bead':
   with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/splash.png'])) as shared:palette=shared.getpalette()[:48]
  if max(indices)>15 or len(palette)!=48:raise ValueError('Physical indexed palette differs: '+stem)
  if stem=='tag_hand':palette=reaction_palette(raw,'finger')
  if stem in {'sparkle_4','x_sign'}:palette=reaction_palette(raw,'sparkle_3' if stem=='sparkle_4' else 'hollow_orb')
  name=stem.replace('_','-');base='physical-'+name
  pixels[name+'.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2))
  pixels[name+'.pal16']=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
  indexed.append(dict(id=name,width=size[0],height=size[1],indicesPath=base+'.4bpp',palettePath=base+'.pal16',paletteTag='ANIM_TAG_'+stem.upper()))
 return indexed,pixels

def physical_sound_effects(reference,commands,source):
 # Callback sounds are real dependencies even when the authored script has no
 # sound opcode. Feed the source call operands to the shared song-ID extractor.
 dependencies=list(commands)
 if any(c['op']=='createsprite' and c['args'][0]=='gBulletSeedSpriteTemplate' for c in commands):
  body=re.search(r'static void AnimBulletSeed_Step1\(struct Sprite \*sprite\)\s*\{(.*?)\n\}',source,re.S)
  sounds=[] if body is None else re.findall(r'PlaySE12WithPanning\((SE_\w+), BattleAnimAdjustPanning\((SOUND_PAN_\w+)\)\);',body[1])
  if len(sounds)!=1 or sounds[0][1]!='SOUND_PAN_TARGET':raise ValueError('Bullet seed callback sound differs')
  dependencies.append(dict(op='playsewithpan',args=list(sounds[0])))
 return elemental.sound_effects(reference,dependencies)

def build(reference):
 raw=read_sources(reference);validate_reaction_sources(raw);validate_friendship_predicates(raw);source=raw['data/battle_anim_scripts.s'].decode()
 dummy=re.search(r'const struct SpriteTemplate gSlideMonToOffsetAndBackSpriteTemplate =\s*\{(.*?)\};',raw['src/battle_anim_mon_movement.c'].decode(),re.S)
 fields={} if dummy is None else dict(re.findall(r'\.(\w+) = ([^,]+),',dummy[1]))
 if fields!=dict(tileTag='0',paletteTag='0',oam='&gDummyOamData',anims='gDummySpriteAnimTable',images='NULL',affineAnims='gDummySpriteAffineAnimTable',callback='SlideMonToOffsetAndBack'):raise ValueError('Slide-and-back source template differs')
 colors=re.search(r'const u16 sMagicalLeafBlendColors\[\] =\s*\{(.*?)\};',raw['src/battle_anim_effects_1.c'].decode(),re.S)
 if colors is None or re.findall(r'RGB\((\d+), (\d+), (\d+)\)',colors[1])!=[('31','0','0'),('31','19','0'),('31','31','0'),('0','31','0'),('5','14','31'),('22','10','31'),('22','21','31')]:raise ValueError('Magical Leaf source blend colors differ')
 for template,size,callback in [('gWishStarSpriteTemplate',16,'AnimWishStar'),('gMeteorMashStarSpriteTemplate',16,'AnimMeteorMashStar'),('gMiniTwinklingStarSpriteTemplate',8,'AnimMiniTwinklingStar')]:
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',raw['src/battle_anim_effects_3.c'].decode(),re.S)
  fields={} if match is None else dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
  expected=dict(tileTag='ANIM_TAG_GOLD_STARS',paletteTag='ANIM_TAG_GOLD_STARS',oam=f'&gOamData_AffineOff_ObjNormal_{size}x{size}',anims='gDummySpriteAnimTable',images='NULL',affineAnims='gDummySpriteAffineAnimTable',callback=callback)
  if fields!=expected:raise ValueError('Shooting/twinkling star source template differs '+template)
 if not re.search(r'\{gBattleAnimSpriteGfx_GoldStars,\s*0x00c0,\s*ANIM_TAG_GOLD_STARS\}',raw['src/data/battle_anim.h'].decode()) or not re.search(r'\{gBattleAnimSpritePal_GoldStars,\s*ANIM_TAG_GOLD_STARS\}',raw['src/data/battle_anim.h'].decode()):raise ValueError('Gold stars source registration differs')
 for file,template,tag,anims,callback in [('battle_anim_fight.c','gFistFootRandomPosSpriteTemplate','ANIM_TAG_HANDS_AND_FEET','sAnims_HandsAndFeet','AnimFistOrFootRandomPos'),('battle_anim_fight.c','gArmThrustHandSpriteTemplate','ANIM_TAG_HANDS_AND_FEET','sAnims_HandsAndFeet','AnimArmThrustHit'),('battle_anim_effects_2.c','gFurySwipesSpriteTemplate','ANIM_TAG_SWIPE','sAnims_FurySwipes','AnimFurySwipes')]:
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',raw['src/'+file].decode(),re.S)
  fields={} if match is None else dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
  expected=dict(tileTag=tag,paletteTag=tag,oam='&gOamData_AffineOff_ObjNormal_32x32',anims=anims,images='NULL',affineAnims='gDummySpriteAffineAnimTable',callback=callback)
  if fields!=expected:raise ValueError('Multi-strike source template differs '+template)
 for file,template,mode,anims,affine,callback in [('battle_anim_effects_3.c','gFangSpriteTemplate','Double','sFangAnimTable','sFangAffineAnimTable','AnimFang'),('battle_anim_effects_1.c','gSuperFangSpriteTemplate','Off','sSuperFangAnimTable','gDummySpriteAffineAnimTable','AnimSuperFang')]:
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',raw['src/'+file].decode(),re.S)
  fields={} if match is None else dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
  expected=dict(tileTag='ANIM_TAG_FANG_ATTACK',paletteTag='ANIM_TAG_FANG_ATTACK',oam=f'&gOamData_Affine{mode}_ObjNormal_32x32',anims=anims,images='NULL',affineAnims=affine,callback=callback)
  if fields!=expected:raise ValueError('Fang source template differs '+template)
 effects2=raw['src/battle_anim_effects_2.c'].decode()
 template=re.search(r'const struct SpriteTemplate gBulletSeedSpriteTemplate =\s*\{(.*?)\};',effects2,re.S)
 fields={} if template is None else dict(re.findall(r'\.(\w+) = ([^,]+),',template[1]))
 expected=dict(tileTag='ANIM_TAG_SEED',paletteTag='ANIM_TAG_SEED',oam='&gOamData_AffineNormal_ObjNormal_16x16',anims='gDummySpriteAnimTable',images='NULL',affineAnims='sBulletSeedAffineAnimTable',callback='AnimBulletSeed')
 if fields!=expected:raise ValueError('Bullet seed source template differs')
 if not re.search(r'\{gBattleAnimSpriteGfx_Seed,\s*0x0180,\s*ANIM_TAG_SEED\}',raw['src/data/battle_anim.h'].decode()) or not re.search(r'\{gBattleAnimSpritePal_Seed,\s*ANIM_TAG_SEED\}',raw['src/data/battle_anim.h'].decode()):raise ValueError('Seed source registration differs')
 bug=raw['src/battle_anim_bug.c'].decode()
 for template,tag,size in [('gPinMissileSpriteTemplate','ANIM_TAG_NEEDLE',16),('gIcicleSpearSpriteTemplate','ANIM_TAG_ICICLE_SPEAR',32)]:
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',bug,re.S)
  if match is None:raise ValueError('Missing arc-projectile template '+template)
  fields=dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
  expected=dict(tileTag=tag,paletteTag=tag,oam=f'&gOamData_AffineNormal_ObjNormal_{size}x{size}',anims='gDummySpriteAnimTable',images='NULL',affineAnims='gDummySpriteAffineAnimTable',callback='AnimMissileArc')
  if fields!=expected:raise ValueError('Arc-projectile source template differs '+template)
 ground=raw['src/battle_anim_ground.c'].decode()
 for template,size,anims in [('gSandAttackDirtSpriteTemplate',8,'gDummySpriteAnimTable'),('gMudSlapMudSpriteTemplate',16,'sAnims_MudSlapMud')]:
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',ground,re.S)
  if match is None:raise ValueError('Missing dirt-scatter template '+template)
  fields=dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
  expected=dict(tileTag='ANIM_TAG_MUD_SAND',paletteTag='ANIM_TAG_MUD_SAND',oam=f'&gOamData_AffineOff_ObjNormal_{size}x{size}',anims=anims,images='NULL',affineAnims='gDummySpriteAffineAnimTable',callback='AnimDirtScatter')
  if fields!=expected:raise ValueError('Dirt-scatter source template differs '+template)
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
 scripts=[];blocked=[];subroutines={};subroutine_labels={};background_symbols={}
 backgrounds={name:int(value) for name,value in re.findall(r'^#define (BG_\w+)\s+(\d+)\s*$',tag_source,re.M)}
 def subcommands(label):
  # One source call frame can contain labels and branches. Keep those addresses
  # scoped to the helper; a backwards goto to its return is not recursion.
  result=[];labels={};pending=[label];parsed=set()
  while pending:
   entry=pending.pop(0)
   if entry in labels or entry in parsed:continue
   match=re.search(r'^'+re.escape(entry)+r':\n',source,re.M)
   if not match:raise ValueError('Missing source subroutine label '+entry)
   if entry!=label:labels[entry]=len(result)
   parsed.add(entry);terminated=False
   for offset,line in enumerate(source[match.end():].splitlines()):
    text=line.split('@',1)[0].strip()
    if not text:continue
    if text.endswith(':'):
     name=text[:-1]
     if name.startswith('Move_') or name in labels:raise ValueError('Overlapping source subroutine')
     labels[name]=len(result);continue
    op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op in {'call','end'}:raise ValueError('Unsupported nested subroutine')
    if op=='jumpreteq':
     if len(args)!=2:raise ValueError('Source predicate comparison operands differ')
     args=['7',args[0],args[1]];op='jumpargeq'
    if op in {'jumprettrue','jumpretfalse'}:
     if len(args)!=1 or not re.search(r'^'+re.escape(args[0])+r':\n',source,re.M):raise ValueError('Missing source predicate target')
     args=['7','1' if op=='jumprettrue' else '0',args[0]];op='jumpargeq'
    previous=len(result);result.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
    result,labels=elemental.fold_firered_contest_branches(reference,source,result,labels)
    if len(result)==previous:continue
    op,args=result[-1]['op'],result[-1]['args']
    if op=='jumpargeq':
     if len(args)!=3 or not args[0].isdigit() or not 0<=int(args[0])<8 or not re.fullmatch(r'-?\d+',args[1]) or not -32768<=int(args[1])<=32767:raise ValueError('Source argument branch operands differ')
     pending.append(args[2])
    if op=='goto':
     if len(args)!=1:raise ValueError('Source goto operands differ')
     pending.append(args[0])
    if op in {'goto','return'}:terminated=True;break
   if not terminated or len(result)>128:raise ValueError('Unterminated or unbounded source subroutine')
  active=set();done=set()
  def visit(pc):
   if pc in active:raise ValueError('Unbounded source subroutine cycle')
   if pc in done:return
   if not 0<=pc<len(result):raise ValueError('Source subroutine falls through')
   active.add(pc);c=result[pc];op=c['op']
   if op in {'goto','jumpargeq'}:
    target=c['args'][-1]
    if target==label:target_pc=0
    elif target in labels:target_pc=labels[target]
    else:raise ValueError('Source subroutine target is missing')
    visit(target_pc)
   if op not in {'goto','return'}:visit(pc+1)
   active.remove(pc);done.add(pc)
  visit(0)
  return result,labels
 def source_block(label):
  match=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not match:raise ValueError('Missing source label '+label)
  result=[];local={}
  for offset,line in enumerate(source[match.end():].splitlines()):
   text=line.split('@',1)[0].strip()
   if not text:continue
   if text.endswith(':'):
    name=text[:-1]
    if name.startswith('Move_') or name==label or name in local:raise ValueError('Unterminated or overlapping source block '+label)
    local[name]=len(result);continue
   op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op=='jumpreteq':
    if len(args)!=2:raise ValueError('Source predicate comparison operands differ')
    args=['7',args[0],args[1]];op='jumpargeq'
   if op=='jumpretfalse':
    if len(args)!=1:raise ValueError('Source predicate boolean operands differ')
    args=['7','1' if op=='jumprettrue' else '0',args[0]];op='jumpargeq'
   result.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
   if op in {'choosetwoturnanim','goto','end','return'}:return result,local
  raise ValueError('Unterminated source block '+label)
 def branch_targets(commands):
  return [target for c in commands for target in (c['args'] if c['op']=='choosetwoturnanim' else c['args'][:1] if c['op']=='goto' else c['args'][1:2] if c['op']=='jumpifmoveturn' else c['args'][2:3] if c['op']=='jumpargeq' else [])]
 for match in re.finditer(r'^(Move_(\w+)):\n',source,re.M):
  commands=[];gaps=set();symbols=set();labels={}
  try:
   commands,labels=source_block(match[1]);parsed={match[1]};pending=branch_targets(commands)
   while pending:
    label=pending.pop(0)
    if label in parsed or label in labels:continue
    labels[label]=len(commands);block,local=source_block(label)
    if set(local)&labels.keys():raise ValueError('Overlapping source branch region')
    labels.update({name:len(commands)+pc for name,pc in local.items()});commands.extend(block);parsed.add(label);pending.extend(branch_targets(block))
    if len(commands)>128:raise ValueError('Source command bound')
   active=set();done=set()
   def visit(pc):
    if pc in active:raise ValueError('Unbounded source branch cycle')
    if pc in done:return
    if not 0<=pc<len(commands):raise ValueError('Source branch falls through')
    active.add(pc);command=commands[pc];op=command['op']
    for target in branch_targets([command]):visit(0 if target==match[1] else labels[target])
    if op not in {'goto','choosetwoturnanim','end','return'}:visit(pc+1)
    active.remove(pc);done.add(pc)
   visit(0)
  except ValueError:gaps.add('control-flow:unterminated')
  for command in commands:
   op=command['op'];args=command['args']
   if op=='createsoundtask' and not elemental.supported_sound_task(args,elemental.sound_effects(reference,[command])):gaps.add('sound-task:unreviewed')
   if op not in hit.OPS|{'playse','panse_adjustall','panse','fadetobg','waitplaysewithpan','call','splitbgprio','goto','choosetwoturnanim','jumpifmoveturn','restorebg','waitbgfadein','waitbgfadeout','setarg','jumpargeq','createsoundtask'}:gaps.add('command:'+op)
   if op=='jumpargeq':
    if len(args)!=3 or not args[0].isdigit() or not 0<=int(args[0])<16 or not (args[1] in {'TRUE','FALSE'} or re.fullmatch(r'-?\d+',args[1]) and -32768<=int(args[1])<=32767):gaps.add('control-flow:branch-operands')
   if op=='jumpifmoveturn':
    symbols.add(op)
    if len(args)!=2 or not args[0].isdigit() or not 0<=int(args[0])<=255:gaps.add('control-flow:branch-operands')
   if op in {'goto','choosetwoturnanim'}:
    symbols.add(op)
    if len(args)!=(2 if op=='choosetwoturnanim' else 1):gaps.add('control-flow:branch-operands')
   if op in ('createsprite','createvisualtask'):
    symbols.add(args[0]);allowed=SPRITES if op=='createsprite' else TASKS
    if args[0] not in allowed:gaps.add('callback:'+args[0])
    # Source callbacks may read retained argument registers. Admit this family
    # only when the script explicitly supplies every value consumed by its ABI.
    widths={**CHARGE_PAIR_WIDTHS,**dict.fromkeys(LOCKED_PAIR_TASKS,2),**dict.fromkeys(CUT_WALL_TASKS,2),'AnimTask_ShakeMonInPlace':7,'AnimTask_FlailMovement':3,'AnimTask_GrowAndGrayscale':2,**dict.fromkeys(FRIENDSHIP_CONTACT_TASKS,2),'AnimTask_TraceMonBlended':6,'AnimTask_SquishAndSweatDroplets':4,'AnimTask_FacadeColorBlend':4,'AnimTask_SmellingSaltsSquish':4,'AnimTask_LeafBlade':2,'AnimTask_HorizontalShake':5,'AnimTask_ShakeTargetBasedOnMovePowerOrDmg':7,'AnimTask_IsPowerOver99':2,'GetIsDoomDesireHitTurn':2,'AnimTask_FakeOut':2,'AnimTask_CycleMagicalLeafPal':2,'AnimTask_MoveSkyUppercutBg':3,'AnimTask_StretchTargetUp':2}
    if op=='createvisualtask' and args[0] in widths and len(args)!=widths[args[0]]:gaps.add('callback-arguments:'+args[0])
    sprite_widths={**CHARGE_PAIR_WIDTHS,'gFocusPunchFistSpriteTemplate':3,'gTwisterLeafSpriteTemplate':8,'gTwisterRockSpriteTemplate':8,'gBrickBreakWallSpriteTemplate':8,'gBrickBreakWallShardSpriteTemplate':7,'gRandomPosHitSplatSpriteTemplate':5,'gAncientPowerRockSpriteTemplate':8,'gRockTombRockSpriteTemplate':8,'gRedXSpriteTemplate':5,'gSpinningSparkleSpriteTemplate':5,'gLickSpriteTemplate':5,'gMovementWavesSpriteTemplate':6,'gWeakFrustrationAngerMarkSpriteTemplate':5,'gTriAttackTriangleSpriteTemplate':5,'gSmellingSaltsHandSpriteTemplate':6,'gSmellingSaltExclamationSpriteTemplate':6}
    if op=='createsprite' and args[0] in sprite_widths and len(args)!=sprite_widths[args[0]]:gaps.add('callback-arguments:'+args[0])
   if op=='loadspritegfx' and args[0] not in {'ANIM_TAG_GUST','ANIM_TAG_AIR_WAVE_2','ANIM_TAG_ROCKS','ANIM_TAG_X_SIGN','ANIM_TAG_SPARKLE_4','ANIM_TAG_LICK','ANIM_TAG_MOVEMENT_WAVES','ANIM_TAG_TRI_ATTACK_TRIANGLE','ANIM_TAG_SWEAT_DROP','ANIM_TAG_TAG_HAND','ANIM_TAG_SMELLINGSALT_EFFECT','ANIM_TAG_FIRE','ANIM_TAG_LIGHTNING','ANIM_TAG_ICE_CRYSTALS','ANIM_TAG_RED_ORB','ANIM_TAG_SWEAT_BEAD','ANIM_TAG_LEAF','ANIM_TAG_RAZOR_LEAF','ANIM_TAG_COIN','ANIM_TAG_CROSS_IMPACT','ANIM_TAG_RED_BALL','ANIM_TAG_WHIRLWIND_LINES','ANIM_TAG_SPARKLE_2','ANIM_TAG_GOLD_STARS','ANIM_TAG_SWIPE','ANIM_TAG_FANG_ATTACK','ANIM_TAG_SEED','ANIM_TAG_ICICLE_SPEAR','ANIM_TAG_NEEDLE','ANIM_TAG_CLAW_SLASH','ANIM_TAG_TORN_METAL','ANIM_TAG_BLUE_LIGHT_WALL','ANIM_TAG_SMALL_EMBER','ANIM_TAG_BONE','ANIM_TAG_EYE_SPARKLE','ANIM_TAG_ANGER','ANIM_TAG_ECLIPSING_ORB','ANIM_TAG_BREATH','ANIM_TAG_BLUE_STAR','ANIM_TAG_MUD_SAND','ANIM_TAG_IMPACT','ANIM_TAG_HANDS_AND_FEET','ANIM_TAG_CUT','ANIM_TAG_SLAM_HIT','ANIM_TAG_WHIP_HIT','ANIM_TAG_POISON_POWDER','ANIM_TAG_SLEEP_POWDER','ANIM_TAG_STUN_SPORE','ANIM_TAG_POISON_BUBBLE'}:gaps.add('graphics:'+args[0])
  # Move-turn commands require an admitted invocation contract as well as pixels.
  # Beat Up uses contributor/hit pairs; charge moves use a turn lock. Their
  # application matchers authenticate these contexts separately from multihit.
  effect=effects.get('MOVE_'+match[2])
  if requires_uncommitted_power(effect,commands,raw):gaps.add('invocation:retained-move-power')
  if 'GetIsDoomDesireHitTurn' in symbols and effect!='EFFECT_FUTURE_SIGHT':gaps.add('invocation:move-turn')
  if 'choosetwoturnanim' in symbols and effect not in {'EFFECT_MULTI_HIT','EFFECT_BEAT_UP','EFFECT_PURSUIT','EFFECT_BRICK_BREAK','EFFECT_BIDE','EFFECT_RAZOR_WIND','EFFECT_SKULL_BASH'}:gaps.add('invocation:move-turn')
  if 'jumpifmoveturn' in symbols and effect!='EFFECT_TRIPLE_KICK':gaps.add('invocation:move-turn')
  if not any(c['op']=='end' for c in commands):gaps.add('control-flow:unterminated')
  if len(commands)>128:gaps.add('command-bound')
  local_subs={};local_labels={}
  for command in commands:
   if command['op']!='call':continue
   try:sub,sub_labels=subcommands(command['args'][0])
   except ValueError:gaps.add('control-flow:subroutine');continue
   for row in sub:
    if row['op'] not in hit.OPS|{'return','waitplaysewithpan','goto','jumpargeq','changebg'}:gaps.add('command:'+row['op'])
    if row['op'] in {'createsprite','createvisualtask'}:
     symbols.add(row['args'][0])
     if row['args'][0] in CHARGE_PAIR_WIDTHS and len(row['args'])!=CHARGE_PAIR_WIDTHS[row['args'][0]]:gaps.add('callback-arguments:'+row['args'][0])
     if row['args'][0] not in (SPRITES if row['op']=='createsprite' else TASKS):gaps.add('callback:'+row['args'][0])
   if any(row['op']=='changebg' and (len(row['args'])!=1 or row['args'][0] not in {'BG_IMPACT_OPPONENT','BG_IMPACT_PLAYER'}) for row in sub):gaps.add('background:unreviewed')
   local_subs[command['args'][0]]=sub
   if sub_labels:local_labels[command['args'][0]]=sub_labels
  # A family's source callback may live entirely in a called helper.
  if not symbols & NEW:continue
  move=ids.get('MOVE_'+match[2])
  if not move or move>354:continue
  if gaps:blocked.append(dict(moveId=move,missing=sorted(gaps)));continue
  dependencies=commands+[c for sub in local_subs.values() for c in sub]
  loaded=sorted({c['args'][0] for c in dependencies if c['op']=='loadspritegfx'})
  if not set(loaded)<=tags.keys():raise ValueError('Undefined source sprite tag dependency')
  children=sorted(set((['gBasicHitSplatSpriteTemplate'] if 'gFistFootRandomPosSpriteTemplate' in symbols else [])+(['gFlashingHitSplatSpriteTemplate'] if 'AnimTask_DrillPeckHitSplats' in symbols else [])+(['gBarrageBallSpriteTemplate'] if 'AnimTask_BarrageBall' in symbols else [])+(['gFacadeSweatDropSpriteTemplate'] if 'AnimTask_SquishAndSweatDroplets' in symbols else [])+(['gLeafBladeSpriteTemplate'] if 'AnimTask_LeafBlade' in symbols else [])))
  scripts.append(dict(moveId=move,label=match[1],commands=commands,labels=labels,**({'spriteDependencies':children} if children else {}),**({'materialDependencies':['whirlwind-lines']} if 'AnimTask_DrillPeckHitSplats' in symbols else {}),soundEffects=physical_sound_effects(reference,dependencies,raw['src/battle_anim_effects_2.c'].decode()),spriteTags={name:tags[name] for name in loaded}));subroutines.update(local_subs);subroutine_labels.update(local_labels);background_symbols.update({c['args'][0]:backgrounds[c['args'][0]] for c in dependencies if c['op'] in {'changebg','fadetobg'}})
 if not scripts:raise ValueError('No supported physical roots')
 # SmallEmber, like leaf and razor-leaf, is owned by the elemental indexed pack.
 # Physical callbacks retain the shared recipe asset ID without a second RGB declaration.
 return (json.dumps(dict(schemaVersion=1,scripts=scripts,subroutines=subroutines,subroutineLabels=subroutine_labels,backgroundSymbols=background_symbols,taskAffinePrograms={name:task_affine_program(raw['src/'+file].decode(),name) for file,name in [('battle_anim_effects_2.c','sThrashMoveMonAffineAnimCmds'),('battle_anim_effects_3.c','sStrongFrustrationAffineAnimCmds'),('battle_anim_effects_3.c','sFacadeSquishAffineAnimCmds'),('battle_anim_effects_3.c','sSmellingSaltsSquishAffineAnimCmds'),('battle_anim_effects_2.c','sAffineAnims_StretchBattlerUp'),('battle_anim_effects_2.c','sSplashEffectAffineAnimCmds'),('battle_anim_effects_2.c','sGrowAndShrinkAffineAnimCmds'),('battle_anim_effects_3.c','sSlackOffSquishAffineAnimCmds'),('battle_anim_effects_3.c','DefenseCurlDeformMonAffineAnimCmds')]},indexedAssets=build_indexed_assets(reference)[0],assets=[dict(id=id,path='physical-'+id+'.rgb555') for id in build_assets(reference)]+[dict(id='sparkle-4',path='walls-sparkle-4.rgb555'),dict(id='rocks',path='elemental-rocks.rgb555'),dict(id='needle',path='elemental-needle.rgb555'),dict(id='poison-bubble',path='elemental-poison-bubble.rgb555'),dict(id='blue-star',path='elemental-blue-star.rgb555')],spriteRecipes=[focus_punch_recipe(fight)]+[elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gRazorWindTornadoSpriteTemplate','AnimRazorWindTornado')]+[cut_wall_recipe(raw['src/'+file].decode(),name,callback) for name,file,callback in CUT_WALL_RECIPES]+[elemental.recipe(raw['src/'+file].decode(),name,callback) for name,file,callback in REACTION_RECIPES+FRIENDSHIP_CONTACT_RECIPES+ROCK_STATUS_RECIPES+FLAIL_RECIPES]+[elemental.recipe(raw['src/'+file].decode(),name,callback) for name,file,callback in ORBITAL_PARTICLE_RECIPES]+[impact_emission_recipe(raw,*row) for row in IMPACT_EMISSION_RECIPES]+[elemental.recipe(fight,'gFistFootRandomPosSpriteTemplate','AnimFistOrFootRandomPos'),elemental.recipe(raw['src/battle_anim_normal.c'].decode(),'gBasicHitSplatSpriteTemplate','AnimHitSplatBasic'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gWishStarSpriteTemplate','AnimWishStar'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gMeteorMashStarSpriteTemplate','AnimMeteorMashStar'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gMiniTwinklingStarSpriteTemplate','AnimMiniTwinklingStar'),elemental.recipe(raw['src/battle_anim_fight.c'].decode(),'gArmThrustHandSpriteTemplate','AnimArmThrustHit'),elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gFurySwipesSpriteTemplate','AnimFurySwipes'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gFangSpriteTemplate','AnimFang'),elemental.recipe(raw['src/battle_anim_effects_1.c'].decode(),'gSuperFangSpriteTemplate','AnimSuperFang'),elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gBulletSeedSpriteTemplate','AnimBulletSeed'),elemental.recipe(raw['src/battle_anim_ground.c'].decode(),'gSandAttackDirtSpriteTemplate','AnimDirtScatter'),elemental.recipe(raw['src/battle_anim_ground.c'].decode(),'gMudSlapMudSpriteTemplate','AnimDirtScatter'),elemental.recipe(fight,'gMegaPunchKickSpriteTemplate','AnimSpinningKickOrPunch'),elemental.recipe(fight,'gSpinningHandOrFootSpriteTemplate','AnimSpinningKickOrPunch'),elemental.recipe(raw['src/battle_anim_fire.c'].decode(),'gFireSpreadSpriteTemplate','AnimFireSpread'),elemental.recipe(raw['src/battle_anim_bug.c'].decode(),'gPinMissileSpriteTemplate','AnimMissileArc'),elemental.recipe(raw['src/battle_anim_bug.c'].decode(),'gIcicleSpearSpriteTemplate','AnimMissileArc'),elemental.recipe(raw['src/battle_anim_bug.c'].decode(),'gLinearStingerSpriteTemplate','AnimTranslateStinger'),elemental.recipe(raw['src/battle_anim_normal.c'].decode(),'gHandleInvertHitSplatSpriteTemplate','AnimHitSplatHandleInvert'),elemental.recipe(raw['src/battle_anim_poison.c'].decode(),'gPoisonBubbleSpriteTemplate','AnimBubbleEffect'),elemental.recipe(raw['src/battle_anim_dark.c'].decode(),'gClawSlashSpriteTemplate','AnimClawSlash'),elemental.recipe(raw['src/battle_anim_rock.c'].decode()+raw['src/battle_anim_fire.c'].decode(),'gFireSpinSpriteTemplate','AnimParticleInVortex'),elemental.recipe(raw['src/battle_anim_ground.c'].decode(),'gSpinningBoneSpriteTemplate','AnimBoneHitProjectile'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gEyeSparkleSpriteTemplate','eye-sparkle','AnimEyeSparkle'),elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gAngerMarkSpriteTemplate','AnimAngerMark'),sprite_recipe(raw['src/battle_anim_effects_3.c'].decode(),'gEclipsingOrbSpriteTemplate','eclipsing-orb','AnimSpriteOnMonPos'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gBreathPuffSpriteTemplate','breath','AnimBreathPuff'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gHealingBlueStarSpriteTemplate','blue-star','AnimSpriteOnMonPos'),sprite_recipe(raw['src/battle_anim_ground.c'].decode(),'gMudsportMudSpriteTemplate','mud-sand','AnimMudSportDirt'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gViceGripSpriteTemplate','cut','AnimViceGripPincer'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gSlamHitSpriteTemplate','slam-hit','AnimWhipHit'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gVineWhipSpriteTemplate','whip-hit','AnimWhipHit')]+[sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),template,asset,'AnimMovePowderParticle') for template,asset in [('gPoisonPowderParticleSpriteTemplate','poison-powder'),('gSleepPowderParticleSpriteTemplate','sleep-powder'),('gStunSporeParticleSpriteTemplate','stun-spore')]]),indent=2)+'\n').encode(),blocked
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/physical.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,blocked=build(a.reference);assets={**build_assets(a.reference),**build_indexed_assets(a.reference)[1]}
 if a.check:
  if a.output.read_bytes()!=data:raise SystemExit('Physical animation pack differs')
  for id,blob in assets.items():
   if (a.output.parent/('physical-'+id if id.endswith(('.4bpp','.pal16')) else 'physical-'+id+'.rgb555')).read_bytes()!=blob:raise SystemExit('Physical sprite pixels differ')
 else:
  a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_bytes(data)
  for id,blob in assets.items():(a.output.parent/('physical-'+id if id.endswith(('.4bpp','.pal16')) else 'physical-'+id+'.rgb555')).write_bytes(blob)
 print(json.dumps(dict(passed=True,roots=[s['moveId'] for s in json.loads(data)['scripts']],blocked=blocked)))
