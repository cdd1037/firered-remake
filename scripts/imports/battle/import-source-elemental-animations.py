#!/usr/bin/env python3
"""Convert reviewed elemental script roots and sprite recipes from pinned pret inputs."""
import argparse, importlib.util, io, json, re, subprocess, struct
from PIL import Image
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('elemental_hit',ROOT/'scripts/imports/battle/import-source-hit-animations.py');hit=importlib.util.module_from_spec(spec);spec.loader.exec_module(hit)
spec=importlib.util.spec_from_file_location('source_animation_affine',ROOT/'scripts/lib/source_animation_affine.py');affine=importlib.util.module_from_spec(spec);spec.loader.exec_module(affine)
task_affine_program=affine.task_affine_program
TEMPLATES={
 'gSleepLetterZSpriteTemplate':('battle_anim_effects_1.c','AnimSleepLetterZ'),
 'gGreenStarSpriteTemplate':('battle_anim_effects_3.c','AnimGreenStar'),
 'gMoonSpriteTemplate':('battle_anim_effects_1.c','AnimMoon'),
 'gMoonlightSparkleSpriteTemplate':('battle_anim_effects_1.c','AnimMoonlightSparkle'),
 'gAuroraBeamRingSpriteTemplate':('battle_anim_water.c','AnimAuroraBeamRings'),
 'gFlyingSandCrescentSpriteTemplate':('battle_anim_rock.c','AnimFlyingSandCrescent'),
 'gRainDropSpriteTemplate':('battle_anim_water.c','AnimRainDrop'),
 'gSunlightRaySpriteTemplate':('battle_anim_fire.c','AnimSunlight'),
 'gGlareEyeDotSpriteTemplate':('battle_anim_effects_3.c','AnimGlareEyeDot'),
 'gMistBallSpriteTemplate':('battle_anim_ice.c','AnimThrowMistBall'),
 'gFirePlumeSpriteTemplate':('battle_anim_fire.c','AnimFirePlume'),
 'gLargeFlameSpriteTemplate':('battle_anim_fire.c','AnimLargeFlame'),
 'gLargeFlameScatterSpriteTemplate':('battle_anim_fire.c','AnimLargeFlame'),
 'gLeechSeedSpriteTemplate':('battle_anim_effects_1.c','AnimLeechSeed'),
 'gDizzyPunchDuckSpriteTemplate':('battle_anim_fight.c','AnimDizzyPunchDuck'),
 'gConstrictBindingSpriteTemplate':('battle_anim_effects_1.c','AnimConstrictBinding'),
 'gKnockOffStrikeSpriteTemplate':('battle_anim_effects_3.c','AnimKnockOffStrike'),
 'gNeedleArmSpikeSpriteTemplate':('battle_anim_effects_1.c','AnimNeedleArmSpike'),
 'gFrenzyPlantRootSpriteTemplate':('battle_anim_effects_1.c','AnimFrenzyPlantRoot'),
 'gLeerSpriteTemplate':('battle_anim_effects_3.c','AnimLeer'),
 'gTailGlowOrbSpriteTemplate':('battle_anim_bug.c','AnimTailGlowOrb'),
 'gTearDropSpriteTemplate':('battle_anim_dark.c','AnimTearDrop'),
 'gRoarNoiseLineSpriteTemplate':('battle_anim_effects_3.c','AnimRoarNoiseLine'),
 'gHyperVoiceRingSpriteTemplate':('battle_anim_effects_2.c','AnimHyperVoiceRing'),
 'gQuestionMarkSpriteTemplate':('battle_anim_psychic.c','AnimQuestionMark'),
 'gHyperBeamOrbSpriteTemplate':('battle_anim_effects_1.c','AnimHyperBeamOrb'),
 'gSonicBoomSpriteTemplate':('battle_anim_effects_2.c','AnimSonicBoomProjectile'),
 'gSmallDriftingBubblesSpriteTemplate':('battle_anim_water.c','AnimSmallDriftingBubbles'),
 'gBonemerangSpriteTemplate':('battle_anim_ground.c','AnimBonemerangProjectile'),
 'gMeanLookEyeSpriteTemplate':('battle_anim_effects_3.c','AnimMeanLookEye'),
 'gBlockXSpriteTemplate':('battle_anim_effects_3.c','AnimBlockX'),
 'gWebThreadSpriteTemplate':('battle_anim_bug.c','AnimTranslateWebThread'),
 'gStringWrapSpriteTemplate':('battle_anim_bug.c','AnimStringWrap'),
 'gSpiderWebSpriteTemplate':('battle_anim_bug.c','AnimSpiderWeb'),
 'gSwordsDanceBladeSpriteTemplate':('battle_anim_effects_2.c','AnimSwordsDanceBlade'),
 'gSharpenSphereSpriteTemplate':('battle_anim_effects_1.c','AnimSharpenSphere'),
 'gConversionSpriteTemplate':('battle_anim_effects_1.c','AnimConversion'),
 'gConversion2SpriteTemplate':('battle_anim_effects_1.c','AnimConversion2'),
 'sHailParticleSpriteTemplate':('battle_anim_ice.c','AnimHailBegin'),
 'gWaterPulseBubbleSpriteTemplate':('battle_anim_water.c','AnimWaterPulseBubble'),
 'gWaterPulseRingBubbleSpriteTemplate':('battle_anim_water.c','AnimWaterPulseRingBubble'),
 'gWaterPulseRingSpriteTemplate':('battle_anim_effects_2.c','AnimWaterPulseRing'),
 'gWavyMusicNotesSpriteTemplate':('battle_anim_effects_1.c','AnimWavyMusicNotes'),
 'gSlowFlyingMusicNotesSpriteTemplate':('battle_anim_effects_1.c','AnimSlowFlyingMusicNotes'),
 'gBellyDrumHandSpriteTemplate':('battle_anim_effects_1.c','AnimBellyDrumHand'),
 'gSoftBoiledEggSpriteTemplate':('battle_anim_effects_2.c','AnimSoftBoiledEgg'),
 'gMilkBottleSpriteTemplate':('battle_anim_effects_1.c','AnimMilkBottle'),
 'gRockFragmentSpriteTemplate':('battle_anim_rock.c','AnimRockFragment'),
 'gRockBlastRockSpriteTemplate':('battle_anim_rock.c','AnimRockBlastRock'),
 'gSwirlingSnowballSpriteTemplate':('battle_anim_ice.c','AnimSwirlingSnowball'),
 'gIceGroundSpikeSpriteTemplate':('battle_anim_ice.c','AnimWaveFromCenterOfTarget'),
 'gFallingRockSpriteTemplate':('battle_anim_rock.c','AnimFallingRock'),
 'gSporeParticleSpriteTemplate':('battle_anim_effects_1.c','AnimSporeParticle'),
 'gGoldRingSpriteTemplate':('battle_anim_psychic.c','TranslateAnimSpriteToTargetMonLocation'),
 'gPsywaveRingSpriteTemplate':('battle_anim_water.c','AnimToTargetInSinWave'),
 'gOutrageFlameSpriteTemplate':('battle_anim_dragon.c','AnimOutrageFlame'),
 'gRazorLeafParticleSpriteTemplate':('battle_anim_effects_1.c','AnimRazorLeafParticle'),
 'gRazorLeafCutterSpriteTemplate':('battle_anim_effects_1.c','AnimTranslateLinearSingleSineWave'),
 'gSwiftStarSpriteTemplate':('battle_anim_effects_1.c','AnimTranslateLinearSingleSineWave'),

 'gMagentaHeartSpriteTemplate':('battle_anim_effects_2.c','AnimMagentaHeart'),
 'gDragonBreathFireSpriteTemplate':('battle_anim_dragon.c', 'AnimDragonFireToTarget'),
 'gDragonRageFireSpitSpriteTemplate':('battle_anim_dragon.c', 'AnimDragonFireToTarget'),
 'gDragonRageFirePlumeSpriteTemplate':('battle_anim_dragon.c', 'AnimDragonRageFirePlume'),
 'gFireBlastRingSpriteTemplate':('battle_anim_fire.c', 'AnimFireRing'),
 'gFireBlastCrossSpriteTemplate':('battle_anim_fire.c', 'AnimFireCross'),
 'gFireSpiralOutwardSpriteTemplate':('battle_anim_fire.c', 'AnimFireSpiralOutward'),

 'gPowderSnowSnowballSpriteTemplate':('battle_anim_ice.c','AnimMoveParticleBeyondTarget'),
 'gBlizzardIceCrystalSpriteTemplate':('battle_anim_ice.c','AnimMoveParticleBeyondTarget'),
 'gIceCrystalSpiralInwardLarge':('battle_anim_ice.c','AnimIcePunchSwirlingParticle'),
 'gIceCrystalSpiralInwardSmall':('battle_anim_ice.c','AnimIcePunchSwirlingParticle'),
 'gIceBeamInnerCrystalSpriteTemplate':('battle_anim_ice.c','AnimIceBeamParticle'),
 'gIceBeamOuterCrystalSpriteTemplate':('battle_anim_ice.c','AnimIceBeamParticle'),
 'gIceCrystalHitLargeSpriteTemplate':('battle_anim_ice.c','AnimIceEffectParticle'),
 'gIceCrystalHitSmallSpriteTemplate':('battle_anim_ice.c','AnimIceEffectParticle'),

 'gEndureEnergySpriteTemplate':('battle_anim_effects_1.c','AnimEndureEnergy'),
 'gSharpTeethSpriteTemplate':('battle_anim_dark.c','AnimBite'),
 'gClampJawSpriteTemplate':('battle_anim_dark.c','AnimBite'),
 'gSwirlingDirtSpriteTemplate':('battle_anim_rock.c','AnimParticleInVortex'),
 'gWhirlpoolSpriteTemplate':('battle_anim_rock.c','AnimParticleInVortex'),
 'gFireSpinSpriteTemplate':('battle_anim_rock.c','AnimParticleInVortex'),
 'gToxicBubbleSpriteTemplate':('battle_anim_poison.c', 'AnimSpriteOnMonPos'),
 'gPoisonBubbleSpriteTemplate':('battle_anim_poison.c', 'AnimBubbleEffect'),
 'gWaterBubbleSpriteTemplate':('battle_anim_poison.c', 'AnimBubbleEffect'),
 'gAcidPoisonBubbleSpriteTemplate':('battle_anim_poison.c', 'AnimAcidPoisonBubble'),
 'gAcidPoisonDropletSpriteTemplate':('battle_anim_poison.c', 'AnimAcidPoisonDroplet'),
 'gLeechLifeNeedleSpriteTemplate':('battle_anim_bug.c', 'AnimLeechLifeNeedle'),
 'gWaterBubbleProjectileSpriteTemplate':('battle_anim_water.c', 'AnimWaterBubbleProjectile'),

 'gSupersonicRingSpriteTemplate':('battle_anim_effects_2.c','TranslateAnimSpriteToTargetMonLocation'),
 'gScreechRingSpriteTemplate':('battle_anim_effects_2.c','TranslateAnimSpriteToTargetMonLocation'),
 'gMetalSoundSpriteTemplate':('battle_anim_effects_2.c','TranslateAnimSpriteToTargetMonLocation'),
 'gHealingBlueStarSpriteTemplate':('battle_anim_effects_1.c','AnimSpriteOnMonPos'),
 'gAbsorptionOrbSpriteTemplate':('battle_anim_effects_1.c','AnimAbsorptionOrb'),
 'gPowerAbsorptionOrbSpriteTemplate':('battle_anim_effects_1.c','AnimPowerAbsorptionOrb'),
 'gEmberSpriteTemplate':('battle_anim_fire.c','TranslateAnimSpriteToTargetMonLocation'),
 'gEmberFlareSpriteTemplate':('battle_anim_fire.c','AnimEmberFlare'),
 'gFireSpiralInwardSpriteTemplate':('battle_anim_fire.c','AnimFireSpiralInward'),
 'gFireSpreadSpriteTemplate':('battle_anim_fire.c','AnimFireSpread'),
 'gFlamethrowerFlameSpriteTemplate':('battle_anim_water.c','AnimToTargetInSinWave'),
 'gHydroPumpOrbSpriteTemplate':('battle_anim_water.c','AnimToTargetInSinWave'),
 'gMudShotOrbSpriteTemplate':('battle_anim_water.c','AnimToTargetInSinWave'),
 'gSignalBeamRedOrbSpriteTemplate':('battle_anim_water.c','AnimToTargetInSinWave'),
 'gSignalBeamGreenOrbSpriteTemplate':('battle_anim_water.c','AnimToTargetInSinWave'),
 'gOctazookaBallSpriteTemplate':('battle_anim_effects_1.c','TranslateAnimSpriteToTargetMonLocation'),
 'gOctazookaSmokeSpriteTemplate':('battle_anim_effects_1.c','AnimSpriteOnMonPos'),
 'gWaterHitSplatSpriteTemplate':('battle_anim_normal.c','AnimHitSplatBasic'),
 'gElectricitySpriteTemplate':('battle_anim_electric.c','AnimElectricity'),
 'gThunderWaveSpriteTemplate':('battle_anim_electric.c','AnimThunderWave'),
 'sElectricBoltSegmentSpriteTemplate':('battle_anim_electric.c','AnimElectricBoltSegment'),
 'gZapCannonBallSpriteTemplate':('battle_anim_electric.c','TranslateAnimSpriteToTargetMonLocation'),
 'gZapCannonSparkSpriteTemplate':('battle_anim_electric.c','AnimZapCannonSpark'),
 'gSparkElectricityFlashingSpriteTemplate':('battle_anim_electric.c','AnimSparkElectricityFlashing'),
 'gThunderboltOrbSpriteTemplate':('battle_anim_electric.c','AnimThunderboltOrb'),
 'gLightningSpriteTemplate':('battle_anim_electric.c','AnimLightning'),
 'gWaterGunProjectileSpriteTemplate':('battle_anim_water.c','AnimThrowProjectile'),
 'gWaterGunDropletSpriteTemplate':('battle_anim_water.c','AnimWaterGunDroplet'),
 'gSmallBubblePairSpriteTemplate':('battle_anim_water.c','AnimSmallBubblePair'),
 'gEggThrowSpriteTemplate':('battle_anim_effects_2.c','AnimThrowProjectile'),
 'gExplosionSpriteTemplate':('battle_anim_effects_2.c','AnimSpriteOnMonPos'),
}
def read(reference,path):
 return subprocess.check_output(['git','-C',str(reference),'show',hit.COMMIT+':'+path]) if (reference/'.git').exists() else (reference/path).read_bytes()
def recipe(source,name,callback):
 match=re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{(.*?)\};',source,re.S)
 if not match:raise ValueError('Missing template '+name)
 fields=dict(re.findall(r'\.(\w+) = ([^,]+),',match[1]))
 shape=re.fullmatch(r'&gOamData_Affine(Off|Normal|Double)_Obj(Normal|Blend)_(8|16|32|64)x(8|16|32|64)',fields['oam'])
 if fields['callback']!=callback or not shape or fields['tileTag']!=fields['paletteTag']:raise ValueError('Elemental recipe differs '+name)
 asset=fields['tileTag'].removeprefix('ANIM_TAG_').lower().replace('_','-')
 animations=[];loops=[]
 if fields['anims']=='gDummySpriteAnimTable':animations=[[dict(tile=0,duration=1,hFlip=False,vFlip=False)]];loops=[0]
 else:
  table=re.search(r'const union AnimCmd \*const '+fields['anims']+r'\[\] =\s*\{(.*?)\};',source,re.S)
  if not table:raise ValueError('Missing frame table')
  for array in re.findall(r'\b([sg]\w+)\s*,',table[1]):
   body=re.search(r'const union AnimCmd '+array+r'\[\] =\s*\{(.*?)\};',source,re.S)[1];frames=[];loop=-1
   for row in body.splitlines():
    row=row.strip().rstrip(',')
    if not row:continue
    f=re.fullmatch(r'ANIMCMD_FRAME\((\d+),\s*(\d+)((?:, \.[hv]Flip = TRUE)*)\)',row)
    if f:frames.append(dict(tile=int(f[1]),duration=int(f[2]),hFlip='.hFlip' in f[3],vFlip='.vFlip' in f[3]))
    elif re.fullmatch(r'ANIMCMD_JUMP\((\d+)\)',row):loop=int(re.fullmatch(r'ANIMCMD_JUMP\((\d+)\)',row)[1])
    elif row!='ANIMCMD_END':raise ValueError('Unsupported frame '+row)
   if not frames or loop>=len(frames):raise ValueError('Invalid frame loop')
   animations.append(frames);loops.append(loop)
 affine=[]
 affine_table=fields['affineAnims']
 if callback=='AnimQuestionMark':
  # This table is activated by the callback after the frame animation ends.
  step=re.search(r'static void AnimQuestionMark_Step1\(struct Sprite \*sprite\)\s*\{(.*?)^\}',source,re.S|re.M)
  expected='sprite->oam.affineMode=ST_OAM_AFFINE_NORMAL;sprite->affineAnims=sAffineAnims_QuestionMark;sprite->data[0]=0;InitSpriteAffineAnim(sprite);sprite->callback=AnimQuestionMark_Step2;'
  if shape[1]!='Off' or affine_table!='gDummySpriteAffineAnimTable' or step is None or re.sub(r'\s+','',step[1])!=expected:raise ValueError('Question mark dynamic affine activation differs')
  affine_table='sAffineAnims_QuestionMark'
 if affine_table!='gDummySpriteAffineAnimTable' and (shape[1]!='Off' or callback=='AnimQuestionMark'):
  table=re.search(r'const union AffineAnimCmd \*const '+affine_table+r'\[\] =\s*\{(.*?)\};',source,re.S)
  if not table:raise ValueError('Missing affine table '+affine_table)
  for array in re.findall(r'\b([sg]\w+)\s*,',table[1]):
   body=re.search(r'const union AffineAnimCmd '+array+r'\[\] =\s*\{(.*?)\};',source,re.S)[1];commands=[]
   for row in body.splitlines():
    row=row.strip().rstrip(',')
    if not row:continue
    f=re.fullmatch(r'AFFINEANIMCMD_FRAME\(([^)]+)\)',row)
    j=re.fullmatch(r'AFFINEANIMCMD_JUMP\((\d+)\)',row)
    if f:
     x,y,rotation,duration=[int(v.strip(),0) for v in f[1].split(',')];commands.append(dict(op='frame',x=((x+32768)&65535)-32768,y=((y+32768)&65535)-32768,rotation=rotation&255,duration=duration&255,target=0))
    elif j:commands.append(dict(op='jump',x=0,y=0,rotation=0,duration=0,target=int(j[1])))
    elif re.fullmatch(r'AFFINEANIMCMD_LOOP\((\d+)\)',row):commands.append(dict(op='loop',x=0,y=0,rotation=0,duration=0,target=int(re.fullmatch(r'AFFINEANIMCMD_LOOP\((\d+)\)',row)[1])))
    elif row=='AFFINEANIMCMD_END' or re.fullmatch(r'AFFINEANIMCMD_END_ALT\(\d+\)',row):commands.append(dict(op='end',x=0,y=0,rotation=0,duration=0,target=0))
    else:raise ValueError('Unsupported affine operation '+row)
   if not commands or commands[-1]['op'] not in ('jump','end'):raise ValueError('Unterminated affine animation')
   affine.append(commands)
 result=dict(template=name,asset=asset,callback=callback,blend=shape[2]=='Blend',width=int(shape[3]),height=int(shape[4]),animations=animations,loopStarts=loops,affineAnimations=affine,affineDoubleSize=shape[1]=='Double')
 if callback=='AnimFlyingSandCrescent':
  body=re.search(r'static void '+callback+r'\(struct Sprite \*sprite\)\s*\{(.*?)^\}',source,re.S|re.M)
  tables=re.findall(r'SetSubspriteTables\(sprite, (\w+)\);',body[1]) if body else []
  if len(tables)!=1:raise ValueError('Sand crescent subsprite activation differs')
  table=re.search(r'const struct SubspriteTable '+tables[0]+r'\[\] =\s*\{\s*\{ NELEMS\((\w+)\), \1 \},\s*\};',source,re.S)
  if table is None:raise ValueError('Sand crescent subsprite table differs')
  array=re.search(r'const struct Subsprite '+table[1]+r'\[\] =\s*\{(.*?)\n\};',source,re.S)
  if array is None:raise ValueError('Missing sand crescent subsprites')
  pieces=[]
  for entry in re.findall(r'\{(.*?)\}',array[1],re.S):
   fields=dict(re.findall(r'\.(\w+)\s*=\s*([^,]+),',entry))
   dimensions=re.fullmatch(r'SPRITE_SHAPE\((8|16|32|64)x(8|16|32|64)\)',fields.get('shape',''))
   if not dimensions or fields.get('size')!='SPRITE_SIZE('+dimensions[1]+'x'+dimensions[2]+')' or set(fields)!={'x','y','shape','size','tileOffset','priority'}:raise ValueError('Unsupported source subsprite fields')
   x,y,tile,priority=(int(fields[k],0) for k in ('x','y','tileOffset','priority'))
   if not -128<=x<=127 or not -128<=y<=127 or not 0<=tile<=1023 or not 0<=priority<=3:raise ValueError('Source subsprite field width differs')
   pieces.append(dict(x=x,y=y,width=int(dimensions[1]),height=int(dimensions[2]),tileOffset=tile,priority=priority))
  if pieces!=[dict(x=-16,y=0,width=32,height=16,tileOffset=0,priority=1),dict(x=16,y=0,width=32,height=16,tileOffset=8,priority=1)]:raise ValueError('Sand crescent source subsprite geometry differs')
  result['subsprites']=pieces
 return result
SHARED_SPRITES={'gThinRingExpandingSpriteTemplate', 'gShakeMonOrTerrainSpriteTemplate', 'gGrantingStarsSpriteTemplate', 'gSimplePaletteBlendSpriteTemplate', 'gSlideMonToOriginalPosSpriteTemplate', 'gComplexPaletteBlendSpriteTemplate', 'gFistFootSpriteTemplate', 'gSlideMonToOffsetSpriteTemplate', 'gBasicHitSplatSpriteTemplate'}
TASKS={'AnimTask_ShakeMon', 'AnimTask_InvertScreenColor', 'AnimTask_BlendBattleAnimPal', 'AnimTask_StartSinAnimTimer', 'AnimTask_SwayMon', 'AnimTask_BlendColorCycle', 'AnimTask_ElectricBolt', 'AnimTask_ShakeMon2'}
SOUNDS={'SE_M_MEGA_KICK2', 'SE_M_CRABHAMMER', 'SE_M_VITAL_THROW2', 'SE_M_HYDRO_PUMP', 'SE_M_FLAME_WHEEL', 'SE_M_HYPER_BEAM', 'SE_M_SCREECH', 'SE_M_ABSORB_2', 'SE_M_COMET_PUNCH', 'SE_M_SUPERSONIC', 'SE_M_TAIL_WHIP', 'SE_M_THUNDERBOLT2', 'SE_M_ABSORB', 'SE_M_GIGA_DRAIN', 'SE_M_EMBER', 'SE_M_TRI_ATTACK2', 'SE_M_FLAMETHROWER', 'SE_M_BUBBLE_BEAM', 'SE_M_THUNDERBOLT', 'SE_M_THUNDER_WAVE', 'SE_M_FIRE_PUNCH', 'SE_M_SELF_DESTRUCT', 'SE_M_BUBBLE3', 'SE_M_BUBBLE', 'SE_M_WHIRLPOOL', 'SE_M_MEGA_KICK'}
TASKS|={'AnimTask_DeepInhale','SoundTask_PlayCryHighPitch','SoundTask_PlayDoubleCry','SoundTask_PlayCryWithEcho','SoundTask_WaitForCry','AnimTask_ShakeBattleTerrain','AnimTask_ScaleMonAndRestore'}
GFX={'ANIM_TAG_SPARK', 'ANIM_TAG_METAL_SOUND_WAVES', 'ANIM_TAG_BROWN_ORB', 'ANIM_TAG_LIGHTNING', 'ANIM_TAG_SPARK_2', 'ANIM_TAG_LARGE_FRESH_EGG', 'ANIM_TAG_ICE_CRYSTALS', 'ANIM_TAG_DUCK', 'ANIM_TAG_PURPLE_RING', 'ANIM_TAG_GLOWY_RED_ORB', 'ANIM_TAG_IMPACT', 'ANIM_TAG_SPARK_H', 'ANIM_TAG_BLACK_BALL_2', 'ANIM_TAG_SMALL_EMBER', 'ANIM_TAG_GLOWY_GREEN_ORB', 'ANIM_TAG_WATER_IMPACT', 'ANIM_TAG_BLUE_STAR', 'ANIM_TAG_WATER_ORB', 'ANIM_TAG_HANDS_AND_FEET', 'ANIM_TAG_GRAY_SMOKE', 'ANIM_TAG_SMALL_BUBBLES', 'ANIM_TAG_ORBS', 'ANIM_TAG_BLACK_BALL', 'ANIM_TAG_GOLD_RING', 'ANIM_TAG_EXPLOSION', 'ANIM_TAG_SHOCK_3'}
GFX|={'ANIM_TAG_AMNESIA'}
SOUNDS|={'SE_M_METRONOME','SE_M_HEADBUTT','SE_M_TAKE_DOWN'}
GFX|={'ANIM_TAG_NOISE_LINE'}
GFX|={'ANIM_TAG_WEB_THREAD','ANIM_TAG_STRING','ANIM_TAG_SPIDER_WEB'}
SOUNDS|={'SE_M_STRING_SHOT2'}
GFX|={'ANIM_TAG_EYE','ANIM_TAG_X_SIGN'}
SOUNDS|={'SE_M_PSYBEAM','SE_M_CONFUSE_RAY','SE_M_LEER','SE_M_SWAGGER'}
GFX|={'ANIM_TAG_BLUE_RING_2'}
GFX|={'ANIM_TAG_TOXIC_BUBBLE','ANIM_TAG_POISON_BUBBLE','ANIM_TAG_NEEDLE','ANIM_TAG_BUBBLE'}
SOUNDS|={'SE_M_TOXIC','SE_M_BUBBLE2'}
GFX|={'ANIM_TAG_MUD_SAND'}
SOUNDS|={'SE_M_SACRED_FIRE2','SE_M_SAND_TOMB','SE_M_WHIRLPOOL'}
GFX|={'ANIM_TAG_SHARP_TEETH','ANIM_TAG_CLAMP'}
SOUNDS|={'SE_M_BITE','SE_M_VICEGRIP'}
GFX|={'ANIM_TAG_FOCUS_ENERGY'}
SOUNDS|={'SE_M_DRAGON_RAGE'}
SOUNDS|={'SE_M_GUST','SE_M_GUST2','SE_M_STRING_SHOT','SE_M_ICY_WIND','SE_M_BUBBLE_BEAM2'}
GFX|={'ANIM_TAG_FIRE_PLUME','ANIM_TAG_FIRE','ANIM_TAG_SEED'}
SOUNDS|={'SE_M_SACRED_FIRE'}
SOUNDS|={'SE_M_FLAME_WHEEL2'}
TASKS|={'AnimTask_BlendMonInAndOut'}
GFX|={'ANIM_TAG_MAGENTA_HEART','ANIM_TAG_ITEM_BAG'}
SOUNDS|={'SE_M_CHARM','SE_M_DIZZY_PUNCH'}
TASKS|={'AnimTask_RockMonBackAndForth'}
SHARED_SPRITES|={'gHorizontalLungeSpriteTemplate'}
TASKS|={'AnimTask_MetallicShine','AnimTask_SetGrayscaleOrOriginalPal'}
SOUNDS|={'SE_M_HARDEN'}
GFX|={'ANIM_TAG_EXPLOSION_6','ANIM_TAG_LEAF','ANIM_TAG_RAZOR_LEAF','ANIM_TAG_YELLOW_STAR'}
SOUNDS|={'SE_M_SWIFT','SE_M_POISON_POWDER','SE_M_RAZOR_WIND','SE_M_RAZOR_WIND2'}
TASKS|={'AnimTask_TranslateMonEllipticalRespectSide'}
GFX|={'ANIM_TAG_ICE_SPIKES','ANIM_TAG_BLUE_RING','ANIM_TAG_SPARKLE_2','ANIM_TAG_SPORE','ANIM_TAG_ROCKS'}
SOUNDS|={'SE_M_ROCK_THROW'}
TASKS|={'AnimTask_SporeDoubleBattle'}
SOUNDS|={'SE_M_SWAGGER','SE_M_MINIMIZE'}
SOUNDS|={'SE_M_PSYBEAM','SE_M_PSYBEAM2','SE_M_TELEPORT'}
TASKS|={'AnimTask_MusicNotesRainbowBlend','AnimTask_MusicNotesClearRainbowBlend','AnimTask_Hail'}
GFX|={'ANIM_TAG_HAIL'}
SOUNDS|={'SE_M_HAIL'}
SOUNDS|={'SE_M_SING','SE_M_GRASSWHISTLE','SE_M_BELLY_DRUM'}
GFX|={'ANIM_TAG_MUSIC_NOTES','ANIM_TAG_PURPLE_HAND_OUTLINE'}
TASKS|={'AnimTask_ConversionAlphaBlend','AnimTask_Conversion2AlphaBlend','AnimTask_FlashAnimTagWithColor'}
GFX|={'ANIM_TAG_CONVERSION','ANIM_TAG_SWORD','ANIM_TAG_SPHERE_TO_CUBE'}
SOUNDS|={'SE_M_SWORDS_DANCE'}
SOUNDS|={'SE_M_BARRIER'}
GFX|={'ANIM_TAG_TENDRILS','ANIM_TAG_SLAM_HIT_2','ANIM_TAG_GREEN_SPIKE','ANIM_TAG_ROOTS'}
SOUNDS|={'SE_M_SCRATCH','SE_M_BIND','SE_M_DOUBLE_SLAP'}
TASKS|={'AnimTask_ShakeMonInPlace'}
GFX|={'ANIM_TAG_AIR_WAVE','ANIM_TAG_BONE'}
SOUNDS|={'SE_M_WATERFALL','SE_M_BONEMERANG','SE_M_VITAL_THROW'}
GFX|={'ANIM_TAG_LEER','ANIM_TAG_CIRCLE_OF_LIGHT','ANIM_TAG_THOUGHT_BUBBLE'}
TASKS|={'AnimTask_BlendParticle'}
SOUNDS|={'SE_M_MORNING_SUN'}
OVERLAY_MATERIALS={'AnimTask_ScaryFace':('scary-face-player','scary-face-opponent'),'AnimTask_HazeScrollingFog':('fog',),'AnimTask_MistBallFog':('fog',),'AnimTask_LoadSandstormBackground':('sandstorm',),'AnimTask_MorningSunLightBeam':('morning-sun',)}
OVERLAY_TASKS=set(OVERLAY_MATERIALS)
TASKS|={'AnimTask_AlphaFadeIn','AnimTask_MoonlightEndFade','AnimTask_RotateAuroraRingColors'}
GFX|={'ANIM_TAG_LETTER_Z','ANIM_TAG_GREEN_STAR','ANIM_TAG_MOON','ANIM_TAG_GREEN_SPARKLE','ANIM_TAG_RAINBOW_RINGS'}
SOUNDS|={'SE_M_SNORE','SE_M_MOONLIGHT','SE_M_BUBBLE_BEAM'}
TASKS|={'AnimTask_BlendBackground','AnimTask_MoveHeatWaveTargets','AnimTask_CreateRaindrops'}
GFX|={'ANIM_TAG_FLYING_DIRT','ANIM_TAG_RAIN_DROPS','ANIM_TAG_SUNLIGHT'}
SOUNDS|={'SE_M_SANDSTORM','SE_M_HEAT_WAVE','SE_M_RAIN_DANCE','SE_M_PETAL_DANCE'}
TASKS|=OVERLAY_TASKS|{'AnimTask_GlareEyeDots','AnimTask_ShakeTargetInPattern'}
SHARED_SPRITES|={'gEyeSparkleSpriteTemplate'}
GFX|={'ANIM_TAG_EYE_SPARKLE','ANIM_TAG_SMALL_RED_EYE','ANIM_TAG_WHITE_FEATHER'}
SOUNDS|={'SE_M_HAZE','SE_M_SAND_ATTACK'}
def fold_firered_contest_branches(reference,source,commands,labels):
 # Both native contest tests are constant false in pinned FireRed. Canonical
 # argument comparisons are folded only immediately after the actual predicate
 # task; its observable arg7 write and synchronous destruction remain intact.
 direct=[i for i,c in enumerate(commands) if c['op']=='jumpifcontest']
 compared=[i for i,c in enumerate(commands) if i and c['op']=='jumpargeq' and len(c['args'])==3 and c['args'][0]=='7' and commands[i-1]['op']=='createvisualtask' and commands[i-1]['args']==['AnimTask_IsContest','2']]
 if not direct and not compared:return commands,labels
 if any(pc in compared for pc in labels.values()):raise ValueError('Contest comparison has an alternate entry')
 engine=read(reference,'src/battle_anim.c').decode()
 if direct:
  body=re.search(r'static void Cmd_jumpifcontest\(void\)\s*\{([^}]+)\}',engine)
  if body is None or re.sub(r'\s+','',body[1])!='sBattleAnimScriptPtr+=5;':raise ValueError('Source engine contest-branch invariant differs')
 if compared:
  body=re.search(r'bool8 IsContest\(void\)\s*\{([^}]+)\}',engine)
  utility=read(reference,'src/battle_anim_utility_funcs.c').decode()
  task=re.search(r'void AnimTask_IsContest\(u8 taskId\)\s*\{([^}]+)\}',utility)
  expected='if(IsContest())gBattleAnimArgs[ARG_RET_ID]=TRUE;elsegBattleAnimArgs[ARG_RET_ID]=FALSE;DestroyAnimVisualTask(taskId);'
  constants=read(reference,'include/constants/battle_anim.h').decode()
  if body is None or re.sub(r'\s+','',body[1])!='returnFALSE;' or task is None or re.sub(r'\s+','',task[1])!=expected or not re.search(r'^#define ARG_RET_ID\s+7\b',constants,re.M):raise ValueError('FireRed contest predicate differs')
 removed=set();replacements={}
 for i in direct+compared:
  row=commands[i];args=row['args'];target=args[-1]
  if len(args)!=(1 if i in direct else 3) or not re.search(r'^'+re.escape(target)+r':\n',source,re.M):raise ValueError('Missing dead source target')
  if i in compared:
   if not re.fullmatch(r'-?\d+',args[1]) or not -32768<=int(args[1])<=32767:raise ValueError('Source argument branch operands differ')
   if int(args[1])==0:
    replacements[i]=dict(row,op='goto',args=[target]);continue
  if target in labels:raise ValueError('Contest-only target overlaps executable labels')
  removed.add(i)
 return [replacements.get(i,c) for i,c in enumerate(commands) if i not in removed],{name:sum(i not in removed for i in range(pc)) for name,pc in labels.items()}
def sound_effects(reference,commands):
 definitions={name:int(value) for name,value in re.findall(r'^#define (SE_\w+)\s+(\d+)\b',read(reference,'include/constants/songs.h').decode(),re.M)}
 result=set()
 for command in commands:
  op,args=command['op'],command['args']
  if op in {'playse','playsewithpan','loopsewithpan','panse','panse_adjustall','panse_adjustnone','waitplaysewithpan'}:result.add(args[0])
  elif op=='createvisualtask' and args[0] in {'SoundTask_PlaySE1WithPanning','SoundTask_PlaySE2WithPanning'}:result.add(args[2])
  elif op=='createsoundtask':
   if args[0]=='SoundTask_LoopSEAdjustPanning':result.add(args[1])
   elif args[0]=='SoundTask_FireBlast':result.update(args[1:3])
   else:raise ValueError('Unreviewed source sound-task dependencies')
 if not result<=definitions.keys():raise ValueError('Undefined source sound dependency: '+','.join(sorted(result-definitions.keys())))
 return {symbol:definitions[symbol] for symbol in sorted(result)}
GFX|={'ANIM_TAG_BREAKING_EGG','ANIM_TAG_MILK_BOTTLE','ANIM_TAG_THIN_RING'}
SOUNDS|={'SE_M_MILK_DRINK','SE_M_HORN_ATTACK'}
def supported_sound_task(args,songs):
 # The existing task ABI accepts any source-resolved song, not one copied song ID.
 if args and args[0]=='SoundTask_FireBlast':return args==['SoundTask_FireBlast','SE_M_FLAME_WHEEL','SE_M_FLAME_WHEEL2']
 if len(args)!=8 or args[0]!='SoundTask_LoopSEAdjustPanning' or args[1] not in songs:return False
 try:
  values=[{'SOUND_PAN_ATTACKER':-64,'SOUND_PAN_TARGET':63}.get(v,int(v,0) if re.fullmatch(r'-?(?:0x[0-9a-fA-F]+|\d+)',v) else None) for v in args[2:]]
 except ValueError:return False
 return all(v is not None for v in values) and -64<=values[0]<=63 and -64<=values[1]<=63 and -128<=values[2]<=127 and 1<=values[3]<=255 and 0<=values[4]<=255 and 0<=values[5]<=255
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
   if op not in hit.OPS|{'call','return','splitbgprio','panse','waitplaysewithpan','splitbgprio_foes','createsoundtask','waitsound','setarg','panse_adjustnone','fadetobg','waitbgfadein','restorebg'}:raise ValueError('Unsupported command '+op)
   result.append(dict(op=op,args=args,line=source[:m.end()].count('\n')+offset+1))
   if op==end:return result
  raise ValueError('Unterminated '+label)
 # Discover complete source roots only when every dependency has a reviewed adapter.
 # Existing explosion roots retain their dedicated catalog and multi-battler semantics.
 ids={name:int(value) for name,value in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',read(reference,'include/constants/moves.h').decode(),re.M)}
 tags={name:10000+int(value) for name,value in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',read(reference,'include/constants/battle_anim.h').decode(),re.M)}
 sprites=set(TEMPLATES)|SHARED_SPRITES
 tasks=TASKS
 sounds=SOUNDS
 songs={name for name in re.findall(r'^#define (SE_\w+)\s+\d+\b',read(reference,'include/constants/songs.h').decode(),re.M)}
 gfx=GFX
 scripts=[];subs={}
 for name in re.findall(r'^Move_(\w+):\n',source,re.M):
  if name not in ids or ids[name] in {120,153}:continue
  try:
   root=commands('Move_'+name,'end')
   calls=dict.fromkeys(c['args'][0] for c in root if c['op']=='call')
   local={label:commands(label,'return') for label in calls}
   if any(c['op']=='call' for sub in local.values() for c in sub):continue
  except ValueError:continue
  flat=root+[c for sub in local.values() for c in sub]
  if not any(c['op']=='createsprite' and c['args'][0] in TEMPLATES or c['op']=='createvisualtask' and c['args'][0] in {'AnimTask_ElectricBolt','AnimTask_SetGrayscaleOrOriginalPal','AnimTask_Hail','AnimTask_CreateRaindrops'}|OVERLAY_TASKS for c in flat):continue
  if any(c['op']=='createsoundtask' and not supported_sound_task(c['args'],songs) or c['op']=='createsprite' and c['args'][0] not in sprites or c['op']=='createvisualtask' and c['args'][0] not in tasks or c['op']=='loadspritegfx' and c['args'][0] not in gfx or c['op'] in {'playsewithpan','loopsewithpan','panse','panse_adjustnone','waitplaysewithpan'} and c['args'][0] not in sounds for c in flat):continue
  if any(c['op']=='fadetobg' and c['args']!=['BG_AURORA'] for c in flat):continue
  # Spark's zero modulus requires original ARM helper semantics before admission.
  if any(c['op']=='createsprite' and c['args'][0]=='gSparkElectricityFlashingSpriteTemplate' and int(c['args'][-1],0)&32767==0 for c in flat):continue
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=root,soundEffects=sound_effects(reference,flat),spriteTags={name:tags[name] for name in sorted({c['args'][0] for c in flat if c['op']=='loadspritegfx'})}));subs.update(local)
 for row in scripts:
  flat=row['commands']+[c for command in row['commands'] if command['op']=='call' for c in subs[command['args'][0]]]
  overlay_tasks={c['args'][0] for c in flat if c['op']=='createvisualtask'} & OVERLAY_TASKS
  if any(c['op']=='loadspritegfx' and c['args']==['ANIM_TAG_WHITE_FEATHER'] for c in flat):
   row['materialDependencies']=['white-feather']
  if overlay_tasks:
   row['overlayDependencies']=sorted({name for task in overlay_tasks for name in OVERLAY_MATERIALS[task]})
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_GlareEyeDots' for c in flat):
   effects=read(reference,'src/battle_anim_effects_3.c').decode()
   child=re.search(r'static void AnimTask_GlareEyeDots_Step\(u8 taskId\)\s*\{(.*?)^\}',effects,re.S|re.M)
   if child is None or re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',child[1])!=['gGlareEyeDotSpriteTemplate']:raise ValueError('Glare raw child dependency differs')
   row['spriteDependencies']=['gGlareEyeDotSpriteTemplate']
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_CreateRaindrops' for c in flat):
   water=read(reference,'src/battle_anim_water.c').decode()
   child=re.search(r'void AnimTask_CreateRaindrops\(u8 taskId\)\s*\{(.*?)^\}',water,re.S|re.M)
   if child is None or re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',child[1])!=['gRainDropSpriteTemplate']:raise ValueError('Rain raw child dependency differs')
   row['spriteDependencies']=['gRainDropSpriteTemplate']
  if any(c['op']=='createsprite' and c['args'][0]=='gGreenStarSpriteTemplate' for c in flat):
   effects=read(reference,'src/battle_anim_effects_3.c').decode()
   child=re.search(r'static void AnimGreenStar\(struct Sprite \*sprite\)\s*\{(.*?)^\}',effects,re.S|re.M)
   if child is None or re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',child[1])!=['gGreenStarSpriteTemplate','gGreenStarSpriteTemplate']:raise ValueError('Morning Sun raw child dependency differs')
   row['spriteDependencies']=['gGreenStarSpriteTemplate']
  if 'AnimTask_MorningSunLightBeam' in overlay_tasks:
   effects=read(reference,'src/battle_anim_effects_3.c').decode()
   beam=re.search(r'void AnimTask_MorningSunLightBeam\(u8 taskId\)\s*\{(.*?)^\}',effects,re.S|re.M)
   cues=re.findall(r'PlaySE12WithPanning\((SE_\w+), BattleAnimAdjustPanning\(SOUND_PAN_ATTACKER\)\);',beam[1]) if beam else []
   if cues!=['SE_M_MORNING_SUN','SE_M_MORNING_SUN']:raise ValueError('Morning Sun callback sounds differ')
   row['soundEffects'].update(sound_effects(reference,[dict(op='playsewithpan',args=[cue,'SOUND_PAN_ATTACKER']) for cue in cues]))
  if any(c['op']=='createsprite' and c['args'][0]=='gBlockXSpriteTemplate' for c in flat):
   effects=read(reference,'src/battle_anim_effects_3.c').decode()
   init=re.search(r'static void AnimBlockX\(struct Sprite \*sprite\)\s*\{(.*?)^\}',effects,re.S|re.M)
   step=re.search(r'static void AnimBlockX_Step\(struct Sprite \*sprite\)\s*\{(.*?)^\}',effects,re.S|re.M)
   if init is None or step is None or 'sprite->callback = AnimBlockX_Step;' not in init[1]:raise ValueError('Block source callback handoff differs')
   cues=re.findall(r'PlaySE12WithPanning\((SE_\w+), BattleAnimAdjustPanning\(SOUND_PAN_TARGET\)\);',step[1])
   if cues!=['SE_M_SKETCH','SE_M_SKETCH','SE_M_LEER']:raise ValueError('Block source callback sounds differ')
   row['soundEffects'].update(sound_effects(reference,[dict(op='playsewithpan',args=[cue,'SOUND_PAN_TARGET']) for cue in cues]))
  if any(c['op']=='createsprite' and c['args'][0]=='gSharpenSphereSpriteTemplate' for c in flat):
   effects=read(reference,'src/battle_anim_effects_1.c').decode()
   init=re.search(r'static void AnimSharpenSphere\(struct Sprite\* sprite\)\s*\{(.*?)^\}',effects,re.S|re.M)
   step=re.search(r'static void AnimSharpenSphere_Step\(struct Sprite\* sprite\)\s*\{(.*?)^\}',effects,re.S|re.M)
   if init is None or step is None or 'sprite->callback = AnimSharpenSphere_Step;' not in init[1]:raise ValueError('Sharpen source callback handoff differs')
   sounds=re.findall(r'PlaySE12WithPanning\((SE_\w+), sprite->data\[5\]\);',step[1])
   if len(sounds)!=1:raise ValueError('Sharpen source callback sound differs')
   row['soundEffects'].update(sound_effects(reference,[dict(op='playsewithpan',args=[sound,'SOUND_PAN_ATTACKER']) for sound in sounds]))
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_Hail' for c in flat):
   ice=read(reference,'src/battle_anim_ice.c').decode()
   def body(name):
    match=re.search(r'(?:static )?(?:void|bool8) '+name+r'\([^;]*?\)\s*\{(.*?)^\}',ice,re.S|re.M)
    if not match:raise ValueError('Missing Hail callback '+name)
    return match[1]
   if 'GenerateHailParticle(' not in body('AnimTask_Hail2') or 'AnimHailContinue' not in body('AnimHailBegin'):raise ValueError('Hail task callback handoff differs')
   children=[]
   for name in ['GenerateHailParticle','AnimHailBegin']:
    children+=re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',body(name))
   if children!=['sHailParticleSpriteTemplate','gIceCrystalHitLargeSpriteTemplate']:raise ValueError('Hail raw child dependencies differ')
   row['spriteDependencies']=children
  if any(c['op']=='createvisualtask' and c['args'][0] in {'AnimTask_MusicNotesRainbowBlend','AnimTask_MusicNotesClearRainbowBlend'} for c in flat):
   for tag in ['ANIM_TAG_MUSIC_NOTES','ANIM_TAG_BENT_SPOON','ANIM_TAG_SPHERE_TO_CUBE','ANIM_TAG_LARGE_FRESH_EGG']:row['spriteTags'][tag]=tags[tag]
 scripts.sort(key=lambda row:row['moveId'])
 rules=read(reference,'graphics_file_rules.mk').decode()
 if '$(BTLANMSPRGFXDIR)/spark.4bpp: $(BTLANMSPRGFXDIR)/spark_0.4bpp' not in rules or '$(BTLANMSPRGFXDIR)/spark_1.4bpp' not in rules:raise ValueError('Electric bolt tile concatenation differs')
 all_source='\n'.join(read(reference,'src/'+file).decode() for file in sorted({v[0] for v in TEMPLATES.values()}|{'battle_anim_poison.c','battle_anim_ice.c'}))
 recipes=[recipe(all_source,name,callback) for name,(file,callback) in TEMPLATES.items()]
 pixels={}
 def palette(name):
  rows=read(reference,'graphics/battle_anims/sprites/'+name+'.pal').decode().splitlines()
  if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Invalid source palette')
  colors=[int(c) for row in rows[3:] for c in row.split()]
  if len(colors)!=48:raise ValueError('Invalid palette length')
  return colors
 for asset,png,shape,pal in [('fire','fire',(32,256),None),('duck','duck',(16,48),None),('tendrils','tendrils',(64,128),None),('slam-hit-2','slam_hit_2',(64,128),None),('green-spike','green_spike',(16,16),None),('roots','roots',(32,128),None),('leer','leer',(32,160),None),('circle-of-light','circle_of_light',(64,64),'electric_orbs'),('thought-bubble','thought_bubble',(32,128),None),('air-wave','air_wave',(32,16),None),('hail','hail',(16,16),None),('breaking-egg','breaking_egg',(32,96),None),('milk-bottle','milk_bottle',(32,32),None),('ice-spikes','ice_spikes',(8,64),'ice_crystals'),('blue-ring','gold_ring',(16,32),'blue_ring'),('blue-ring-2','gold_ring',(16,32),'blue_ring_2'),('explosion-6','explosion_6',(32,128),None),('leaf','leaf',(16,144),None),('razor-leaf','razor_leaf',(32,16),None),('yellow-star','yellow_star',(32,32),None),('magenta-heart','magenta_heart',(16,16),None),('fire-plume','fire_plume',(32,160),'fire'),('focus-energy','focus_energy',(16,128),None),('sharp-teeth','sharp_teeth',(64,64),None),('clamp','clamp',(64,64),'sharp_teeth'),('toxic-bubble','toxic_bubble',(16,128),'poison_bubble'),('poison-bubble','poison_bubble',(16,48),None),('needle','needle',(16,16),None),('bubble','bubble',(16,48),'rain_drops'),('gold-ring','gold_ring',(16,32),None),('purple-ring','gold_ring',(16,32),'purple_ring'),('metal-sound-waves','metal_sound_waves',(32,64),None),('blue-star','blue_star',(32,224),None),('orbs','orbs',(16,48),None),('small-ember','small_ember',(32,160),None),('water-orb','water_orb',(16,64),'water_impact'),('brown-orb','water_orb',(16,64),'brown_orb'),('glowy-red-orb','glowy_red_orb',(8,8),None),('glowy-green-orb','glowy_red_orb',(8,8),'glowy_green_orb'),('black-ball','black_ball',(8,8),None),('gray-smoke','gray_smoke',(32,160),None),('water-impact','water_impact',(32,32),None),('spark-thin','spark_0',(8,64),'spark'),('spark-wide','spark_1',(16,64),'spark'),('spark-2','spark_2',(16,48),None),('spark-h','spark_h',(32,32),'spark'),('black-ball-2','black_ball_2',(16,16),None),('shock-3','shock_3',(32,96),None),('small-bubbles','small_bubbles',(16,40),None),('large-fresh-egg','large_fresh_egg',(16,16),None),('explosion','explosion',(32,128),None),('lightning','lightning',(32,160),'lightning_2')]:
  if pal in ('electric_orbs','water_impact','lightning_2','poison_bubble','rain_drops','sharp_teeth','fire'):
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/'+pal+'.png'))) as im:colors=im.getpalette()[:48]
  else:colors=palette(pal) if pal else None
  pixels[asset]=hit.rgb555(read(reference,'graphics/battle_anims/sprites/'+png+'.png'),shape,colors)
 # Preserve source 1D tile concatenation before interpreting sprite frame addresses.
 for asset,names in [('ice-crystals',['ice_crystals_'+str(i) for i in range(5)]),('mud-sand',['mud_sand_0','mud_sand_1'])]:
  tiles=[]
  for name in names:
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/'+name+'.png'))) as image:
    for y in range(0,image.height,8):
     for x in range(0,image.width,8):tiles.append(image.crop((x,y,x+8,y+8)).copy())
  image=Image.new('P',(8,8*len(tiles)));image.putpalette(palette(asset.replace('-','_')))
  for i,tile in enumerate(tiles):image.paste(tile,(0,8*i))
  buffer=io.BytesIO();image.save(buffer,format='PNG');pixels[asset]=hit.rgb555(buffer.getvalue(),image.size)
 pixels['rocks']=hit.rgb555(read(reference,'graphics/battle_anims/sprites/rocks.png'),(32,192))
 pixels['spore']=hit.rgb555(read(reference,'graphics/battle_anims/sprites/spore.png'),(16,32))
 assets=[dict(id=id,path='elemental-'+id+'.rgb555') for id in pixels]
 indexed=[]
 registrations=read(reference,'src/data/battle_anim.h').decode();graphics=read(reference,'src/graphics.c').decode()
 if not re.search(r'\{gBattleAnimSpriteGfx_NoiseLine,\s*0x0800,\s*ANIM_TAG_NOISE_LINE\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_HitDuplicate,\s*ANIM_TAG_NOISE_LINE\}',registrations):raise ValueError('Source noise line registration differs')
 for symbol,path,suffix in [('Gfx_NoiseLine','noise_line','4bpp'),('Pal_HitDuplicate','hit_duplicate','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+path+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Source noise line material differs')
 for stem,symbol,size in [('duck','Duck',0x0180),('tendrils','Tendrils',0x1000),('slam_hit_2','SlamHit2',0x1000),('green_spike','GreenSpike',0x0080),('roots','Roots',0x0800)]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registrations):raise ValueError('Impact source registration differs: '+stem)
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Impact source material differs: '+stem)
 # Fire/seed callbacks share existing plume/seed materials; verify the actual
 # sheet registrations and shared fire palette before admitting the new roots.
 for stem,symbol,pal,size in [('fire','Fire','Fire',0x1000),('fire_plume','FirePlume','Fire',0x0a00),('seed','Seed','Seed',0x0180)]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal+r',\s*'+tag+r'\}',registrations):raise ValueError('Fire/seed source registration differs: '+stem)
  for kind,symbol,path,suffix in [('Gfx',symbol,stem,'4bpp'),('Pal',pal,'fire' if pal=='Fire' else 'seed','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+path+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Fire/seed source material differs: '+stem)
 # Sonic Boom's material is AIR_WAVE, not the separate AIR_WAVE_2 crescent.
 if not re.search(r'\{gBattleAnimSpriteGfx_AirWave,\s*0x0100,\s*ANIM_TAG_AIR_WAVE\}',registrations) or not re.search(r'\{gBattleAnimSpritePal_AirWave,\s*ANIM_TAG_AIR_WAVE\}',registrations):raise ValueError('Sonic Boom source registration differs')
 for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+r'_AirWave\[\] = INCBIN_U32\("graphics/battle_anims/sprites/air_wave\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Sonic Boom source material path differs')
 for stem,symbol,size in [('letter_z','LetterZ',0x0200),('green_star','GreenStar',0x0200),('moon','Moon',0x0800),('green_sparkle','GreenSparkle',0x0200),('rainbow_rings','RainbowRings',0x00c0),('flying_dirt','FlyingDirt',0x0200),('rain_drops','RainDrops',0x0700),('sunlight','Sunlight',0x0200),('small_red_eye','SmallRedEye',0x0020),('white_feather','WhiteFeather',0x0400),('leer','Leer',0xa00),('thought_bubble','ThoughtBubble',0x800),('small_bubbles','SmallBubbles',0x140),('orbs','Orbs',0x180),('amnesia','Amnesia',0x1000),('sword','Sword',0x400),('sphere_to_cube','SphereToCube',0xa00)]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registrations):raise ValueError('Source indexed registration differs: '+stem)
  for suffix,kind in [('4bpp','Gfx'),('gbapal','Pal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Source indexed graphics differs: '+stem)
 if not re.search(r'\{gBattleAnimSpriteGfx_CircleOfLight,\s*0x0800,\s*ANIM_TAG_CIRCLE_OF_LIGHT\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_ElectricOrbs,\s*ANIM_TAG_CIRCLE_OF_LIGHT\}',registrations):raise ValueError('Tail Glow shared source palette differs')
 for kind,symbol,stem,suffix in [('Gfx','CircleOfLight','circle_of_light','4bpp'),('Pal','ElectricOrbs','electric_orbs','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Tail Glow source material path differs')
 for stem,symbol,size in [('string','String',0x0400),('web_thread','WebThread',0x0020),('spider_web','SpiderWeb',0x0800)]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_String,\s*'+tag+r'\}',registrations):raise ValueError('Source web registration differs: '+stem)
  if not re.search(r'gBattleAnimSpriteGfx_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.4bpp\.lz"\)',graphics):raise ValueError('Source web graphics differs: '+stem)
 for stem,symbol,pal in [('eye','Eye','Eye'),('x_sign','XSign','HollowOrb')]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x0800,\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal+r',\s*'+tag+r'\}',registrations):raise ValueError('Source trapping registration differs: '+stem)
  for kind,name,path,suffix in [('Gfx',symbol,stem,'4bpp'),('Pal',pal,'hollow_orb' if stem=='x_sign' else stem,'gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+name+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+path+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Source trapping graphics differs: '+stem)
 for stem,symbol,size in [('leaf','Leaf',0x0480),('razor_leaf','RazorLeaf',0x0100)]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registrations):raise ValueError('Source leaf registration differs: '+stem)
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Source leaf material differs: '+stem)
 for stem,size in [('letter_z',(32,32)),('green_star',(16,64)),('moon',(64,64)),('green_sparkle',(16,64)),('rainbow_rings',(8,48)),('leaf',(16,144)),('razor_leaf',(32,16)),('flying_dirt',(32,32)),('rain_drops',(16,224)),('sunlight',(32,32)),('small_red_eye',(8,8)),('white_feather',(32,64)),('leer',(32,160)),('circle_of_light',(64,64)),('thought_bubble',(32,128)),('small_bubbles',(16,40)),('orbs',(16,48)),('noise_line',(32,128)),('amnesia',(32,256)),('music_notes',(16,96)),('purple_hand_outline',(32,32)),('conversion',(8,32)),('sword',(32,64)),('sphere_to_cube',(32,160)),('web_thread',(8,8)),('string',(64,32)),('spider_web',(64,64)),('eye',(64,64)),('x_sign',(64,64))]:
  with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/'+stem+'.png'))) as im:
   if im.mode!='P' or im.size!=size:raise ValueError('Invalid source indexed image')
   indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()];palette=im.getpalette()[:48]
  if stem=='circle_of_light':
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/electric_orbs.png'))) as shared:palette=shared.getpalette()[:48]
  if stem in ('web_thread','spider_web'):
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/string.png'))) as shared:palette=shared.getpalette()[:48]
  if stem=='noise_line':
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/hit_duplicate.png'))) as shared:palette=shared.getpalette()[:48]
  if stem=='x_sign':
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/hollow_orb.png'))) as shared:palette=shared.getpalette()[:48]
  if max(indices)>15 or len(palette)!=48:raise ValueError('Invalid source indices/palette')
  if stem=='music_notes' and any(i>5 for i in indices):raise ValueError('Music notes sample outside source recolored range')
  name=stem.replace('_','-');base='elemental-'+name
  pixels[name+'.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2))
  pixels[name+'.pal16']=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
  indexed.append(dict(id=name,width=size[0],height=size[1],indicesPath=base+'.4bpp',palettePath=base+'.pal16',paletteTag='ANIM_TAG_'+stem.upper()))
 background_names={c['args'][0] for row in scripts for c in row['commands']+[c for command in row['commands'] if command['op']=='call' for c in subs[command['args'][0]]] if c['op'] in {'fadetobg','changebg'}}
 definitions={name:int(value) for name,value in re.findall(r'^#define (BG_\w+)\s+(\d+)\b',read(reference,'include/constants/battle_anim.h').decode(),re.M)}
 if not background_names<=definitions.keys():raise ValueError('Missing source elemental background symbol')
 return (json.dumps(dict(schemaVersion=1,scripts=scripts,subroutines=subs,backgroundSymbols={name:definitions[name] for name in sorted(background_names)},taskAffinePrograms={'sDeepInhaleAffineAnimCmds':task_affine_program(all_source,'sDeepInhaleAffineAnimCmds')},spriteRecipes=recipes,assets=assets,indexedAssets=indexed),indent=2)+'\n').encode(),pixels
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/elemental.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,pixels=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/('elemental-'+id if id.endswith(('.4bpp','.pal16')) else 'elemental-'+id+'.rgb555'),blob) for id,blob in pixels.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Elemental output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[s['moveId'] for s in json.loads(data)['scripts']])))
