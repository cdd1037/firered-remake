#!/usr/bin/env python3
"""Convert reviewed elemental script roots and sprite recipes from pinned pret inputs."""
import argparse, importlib.util, io, json, re, subprocess, struct
from PIL import Image
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('elemental_hit',ROOT/'scripts/imports/battle/import-source-hit-animations.py');hit=importlib.util.module_from_spec(spec);spec.loader.exec_module(hit)
spec=importlib.util.spec_from_file_location('source_animation_affine',ROOT/'scripts/lib/source_animation_affine.py');affine=importlib.util.module_from_spec(spec);spec.loader.exec_module(affine)
task_affine_program=affine.task_affine_program
spec=importlib.util.spec_from_file_location('source_animation_frames',ROOT/'scripts/lib/source_animation_frames.py');frames_module=importlib.util.module_from_spec(spec);spec.loader.exec_module(frames_module)
frame_program=frames_module.frame_program
BATTLER_COPY_TASKS={'AnimTask_SpiteTargetShadow','AnimTask_AttackerPunchWithTrace','AnimTask_NightmareClone','AnimTask_NightShadeClone','AnimTask_DoubleTeam','AnimTask_ShrinkTargetCopy'}
EXPULSION_TASKS={'AnimTask_SlideOffScreen','AnimTask_Teleport'}
ADDITIONAL_MON_TASKS={'AnimTask_RolePlaySilhouette'}
MATERIAL_CHANGE_TASKS={'AnimTask_TransformMon','AnimTask_MonToSubstitute'}
PALETTE_BACKUP_TASKS={'AnimTask_AllocBackupPalBuffer','AnimTask_FreeBackupPalBuffer','AnimTask_CopyPalUnfadedToBackup','AnimTask_CopyPalUnfadedFromBackup','AnimTask_CopyPalFadedToUnfaded'}
MEMENTO_TASKS={'AnimTask_InitMementoShadow','AnimTask_MoveAttackerMementoShadow','AnimTask_MementoHandleBg','AnimTask_MoveTargetMementoShadow'}
SQUASH_TASKS={'AnimTask_EruptionLaunchRocks','AnimTask_WaterSpoutLaunch','AnimTask_WaterSpoutRain'}
TEMPLATES={
 'gBatonPassPokeballSpriteTemplate':('battle_anim_effects_3.c','AnimBatonPassPokeball'),
 'gSparkElectricitySpriteTemplate':('battle_anim_electric.c','AnimSparkElectricity'),
 'gSludgeProjectileSpriteTemplate':('battle_anim_poison.c','AnimSludgeProjectile'),
 'gSludgeBombHitParticleSpriteTemplate':('battle_anim_poison.c','AnimSludgeBombHitParticle'),
 'gBlackSmokeSpriteTemplate':('battle_anim_effects_3.c','AnimBlackSmoke'),
 'gBlackBallSpriteTemplate':('battle_anim_effects_3.c','AnimThrowProjectile'),
 'sSmokescreenImpactSpriteTemplate':('battle_anim_smokescreen.c','SpriteCB_SmokescreenImpact'),
 'gEruptionLaunchRockSpriteTemplate':('battle_anim_fire.c','AnimEruptionLaunchRock'),
 'gEruptionFallingRockSpriteTemplate':('battle_anim_fire.c','AnimEruptionFallingRock'),
 'gSmallWaterOrbSpriteTemplate':('battle_anim_water.c','AnimSmallWaterOrb'),
 'gSpotlightSpriteTemplate':('battle_anim_effects_3.c','AnimSpotlight'),
 'gFlatterSpotlightSpriteTemplate':('battle_anim_effects_3.c','AnimFlatterSpotlight'),
 'gClappingHandSpriteTemplate':('battle_anim_effects_3.c','AnimClappingHand'),
 'gClappingHand2SpriteTemplate':('battle_anim_effects_3.c','AnimClappingHand2'),
 'gFlatterConfettiSpriteTemplate':('battle_anim_effects_3.c','AnimFlatterConfetti'),
 'gAssistPawprintSpriteTemplate':('battle_anim_effects_3.c','AnimAssistPawprint'),
 'gSweetScentPetalSpriteTemplate':('battle_anim_effects_3.c','AnimSweetScentPetal'),
 'gElectricChargingParticlesSpriteTemplate':('battle_anim_electric.c','SpriteCallbackDummy'),
 'gGrowingChargeOrbSpriteTemplate':('battle_anim_electric.c','AnimGrowingChargeOrb'),
 'gVoltTackleOrbSlideSpriteTemplate':('battle_anim_electric.c','AnimVoltTackleOrbSlide'),
 'gVoltTackleBoltSpriteTemplate':('battle_anim_electric.c','AnimVoltTackleBolt'),
 'gElectricPuffSpriteTemplate':('battle_anim_electric.c','AnimElectricPuff'),
 'gGrowingShockWaveOrbSpriteTemplate':('battle_anim_electric.c','AnimGrowingShockWaveOrb'),
 'sShockWaveProgressingBoltSpriteTemplate':('battle_anim_electric.c','AnimShockWaveProgressingBolt'),
 'gWillOWispOrbSpriteTemplate':('battle_anim_fire.c','AnimWillOWispOrb'),
 'gWillOWispFireSpriteTemplate':('battle_anim_fire.c','AnimWillOWispFire'),
 'gPetalDanceBigFlowerSpriteTemplate':('battle_anim_effects_1.c','AnimPetalDanceBigFlower'),
 'gPetalDanceSmallFlowerSpriteTemplate':('battle_anim_effects_1.c','AnimPetalDanceSmallFlower'),
 'gIceBallChunkSpriteTemplate':('battle_anim_ice.c','InitIceBallAnim'),
 'gIceBallImpactShardSpriteTemplate':('battle_anim_ice.c','InitIceBallParticle'),
 'gBentSpoonSpriteTemplate':('battle_anim_psychic.c','AnimBentSpoon'),
 'gKinesisZapEnergySpriteTemplate':('battle_anim_effects_2.c','AnimKinesisZapEnergy'),
 'gPsychUpSpiralSpriteTemplate':('battle_anim_psychic.c','AnimSpriteOnMonPos'),
 'gLusterPurgeCircleSpriteTemplate':('battle_anim_psychic.c','AnimSpriteOnMonPos'),
 'gFallingFeatherSpriteTemplate':('battle_anim_flying.c','AnimFallingFeather'),
 'gOverheatFlameSpriteTemplate':('battle_anim_dragon.c','AnimOverheatFlame'),

 'gRolloutMudSpriteTemplate':('battle_anim_rock.c','AnimRolloutParticle'),
 'gRolloutRockSpriteTemplate':('battle_anim_rock.c','AnimRolloutParticle'),
 'gWhirlwindLineSpriteTemplate':('battle_anim_flying.c','AnimWhirlwindLine'),
 'gMimicOrbSpriteTemplate':('battle_anim_effects_1.c','AnimMimicOrb'),
 'gPsychoBoostOrbSpriteTemplate':('battle_anim_psychic.c','AnimPsychoBoost'),
    'gMetronomeFingerSpriteTemplate':('battle_anim_effects_1.c','AnimMetronomeFinger'),
    'gLetterZSpriteTemplate':('battle_anim_effects_3.c','AnimLetterZ'),
 'gThinRingShrinkingSpriteTemplate':('battle_anim_effects_2.c','AnimSpriteOnMonPos'),
 'gHelpingHandClapSpriteTemplate':('battle_anim_effects_3.c','AnimHelpingHandClap'),
 'gFastFlyingMusicNotesSpriteTemplate':('battle_anim_effects_1.c','AnimFlyingMusicNotes'),
 'sImprisonOrbSpriteTemplate':('battle_anim_psychic.c','SpriteCallbackDummy'),
 'gThoughtBubbleSpriteTemplate':('battle_anim_effects_1.c','AnimThoughtBubble'),
 'gTauntFingerSpriteTemplate':('battle_anim_effects_1.c','AnimTauntFinger'),
 'gFollowMeFingerSpriteTemplate':('battle_anim_effects_1.c','AnimFollowMeFinger'),
 'gRedHeartProjectileSpriteTemplate':('battle_anim_effects_2.c','AnimRedHeartProjectile'),
 'gRedHeartRisingSpriteTemplate':('battle_anim_effects_2.c','AnimRedHeartRising'),
 'gConfuseRayBallBounceSpriteTemplate':('battle_anim_ghost.c', 'AnimConfuseRayBallBounce'),
 'gConfuseRayBallSpiralSpriteTemplate':('battle_anim_ghost.c', 'AnimConfuseRayBallSpiral'),
 'gDevilSpriteTemplate':('battle_anim_effects_2.c', 'AnimDevil'),
 'gPinkHeartSpriteTemplate':('battle_anim_effects_2.c', 'AnimPinkHeart'),
 'gAngelSpriteTemplate':('battle_anim_effects_2.c', 'AnimAngel'),
 'gRedHeartBurstSpriteTemplate':('battle_anim_effects_2.c', 'AnimParticleBurst'),
 'gForesightMagnifyingGlassSpriteTemplate':('battle_anim_effects_3.c', 'AnimForesightMagnifyingGlass'),
 'gHydroCannonChargeSpriteTemplate':('battle_anim_water.c','AnimHydroCannonCharge'),
 'gHydroCannonBeamSpriteTemplate':('battle_anim_water.c','AnimHydroCannonBeam'),
 'gGuardRingSpriteTemplate':('battle_anim_effects_2.c','AnimGuardRing'),
 'gIngrainRootSpriteTemplate':('battle_anim_effects_1.c','AnimIngrainRoot'),
 'gIngrainOrbSpriteTemplate':('battle_anim_effects_1.c','AnimIngrainOrb'),
 'gPoisonGasCloudSpriteTemplate':('battle_anim_ice.c','InitPoisonGasCloudAnim'),
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
VISIBILITY_TASKS={'AnimTask_AttackerFadeToInvisible','AnimTask_AttackerFadeFromInvisible','AnimTask_InitAttackerFadeFromInvisible','AnimTask_SetAttackerInvisibleWaitForSignal','AnimTask_SetCamouflageBlend','AnimTask_SetAllNonAttackersInvisiblity','AnimTask_BlendBattleAnimPalExclude','AnimTask_OdorSleuthMovement'}
def read(reference,path):
 return subprocess.check_output(['git','-C',str(reference),'show',hit.COMMIT+':'+path]) if (reference/'.git').exists() else (reference/path).read_bytes()
def source_oam_shape(source,operand):
 shape=re.fullmatch(r'&gOamData_Affine(Off|Normal|Double)_Obj(Normal|Blend)_(8|16|32|64)x(8|16|32|64)',operand)
 if shape:return shape
 # Local source templates use the same OAM ABI as the shared named constants.
 local=re.search(r'const struct OamData '+re.escape(operand.removeprefix('&'))+r'\s*=\s*\{(.*?)\};',source,re.S)
 if not local:return None
 fields=dict(re.findall(r'\.(\w+)\s*=\s*([^,]+),',local[1]))
 size=re.fullmatch(r'SPRITE_SHAPE\((8|16|32|64)x(8|16|32|64)\)',fields.get('shape',''))
 modes={'ST_OAM_AFFINE_OFF':'Off','ST_OAM_AFFINE_NORMAL':'Normal','ST_OAM_AFFINE_DOUBLE':'Double'}
 objects={'ST_OAM_OBJ_NORMAL':'Normal','ST_OAM_OBJ_BLEND':'Blend'}
 if not size or fields.get('size')!='SPRITE_SIZE('+size[1]+'x'+size[2]+')' or fields.get('affineMode') not in modes or fields.get('objMode') not in objects or fields.get('bpp')!='ST_OAM_4BPP' or fields.get('mosaic')!='FALSE':return None
 return (None,modes[fields['affineMode']],objects[fields['objMode']],size[1],size[2])

def recipe(source,name,callback):
 match=re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{(.*?)\};',source,re.S)
 if not match:raise ValueError('Missing template '+name)
 fields={k:v.strip() for k,v in re.findall(r'\.(\w+)\s*=\s*([^,]+)(?:,|$)',match[1])}
 shape=source_oam_shape(source,fields['oam'])
 if fields['callback']!=callback or not shape or fields['tileTag']!=fields['paletteTag']:raise ValueError('Elemental recipe differs '+name)
 asset={'TAG_SMOKESCREEN':'smokescreen-impact'}.get(fields['tileTag'],fields['tileTag'].removeprefix('ANIM_TAG_').lower().replace('_','-'))
 if name in {'sShockWaveProgressingBoltSpriteTemplate','gVoltTackleBoltSpriteTemplate'}:asset='spark-thin' # Source tiles0..7 are the existing first SPARK sheet.
 animations=[];loops=[];frame_loops=[]
 if fields['anims']=='gDummySpriteAnimTable':animations=[[dict(tile=0,duration=1,hFlip=False,vFlip=False)]];loops=[0]
 else:
  table=re.search(r'const union AnimCmd \*const '+fields['anims']+r'\[\] =\s*\{(.*?)\};',source,re.S)
  if not table:raise ValueError('Missing frame table')
  for array in re.findall(r'\b([sg]\w+)\s*(?:,|$)',table[1]):
   body=re.search(r'const union AnimCmd '+array+r'\[\] =\s*\{(.*?)\};',source,re.S)[1]
   frames,loop,controls=frame_program(body)
   animations.append(frames);loops.append(loop);frame_loops.append(controls)
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
  for array in re.findall(r'\b([sg]\w+)\s*(?:,|$)',table[1]):
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
 if any(frame_loops):result['frameLoops']=frame_loops
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
SHARED_SPRITES={'gVerticalDipSpriteTemplate', 'gThinRingExpandingSpriteTemplate', 'gShakeMonOrTerrainSpriteTemplate', 'gGrantingStarsSpriteTemplate', 'gSimplePaletteBlendSpriteTemplate', 'gSlideMonToOriginalPosSpriteTemplate', 'gComplexPaletteBlendSpriteTemplate', 'gFistFootSpriteTemplate', 'gSlideMonToOffsetSpriteTemplate', 'gBasicHitSplatSpriteTemplate'}
TASKS={'SoundTask_PlaySE1WithPanning','SoundTask_PlaySE2WithPanning','AnimTask_CreateSpotlight','AnimTask_RemoveSpotlight','AnimTask_HardwarePaletteFade','AnimTask_BlendBattleAnimPal','AnimTask_BlendColorCycle','AnimTask_ElectricBolt','AnimTask_InvertScreenColor','AnimTask_NightmareClone','AnimTask_Rollout','AnimTask_ShakeMon','AnimTask_ShakeMon2','AnimTask_ShakeTargetBasedOnMovePowerOrDmg','AnimTask_StartSinAnimTimer','AnimTask_SwayMon'}
SOUNDS={'SE_M_ABSORB','SE_M_ABSORB_2','SE_M_BUBBLE','SE_M_BUBBLE3','SE_M_BUBBLE_BEAM','SE_M_COMET_PUNCH','SE_M_CRABHAMMER','SE_M_EMBER','SE_M_FIRE_PUNCH','SE_M_FLAMETHROWER','SE_M_FLAME_WHEEL','SE_M_GIGA_DRAIN','SE_M_HYDRO_PUMP','SE_M_HYPER_BEAM','SE_M_MEGA_KICK','SE_M_MEGA_KICK2','SE_M_NIGHTMARE','SE_M_SCREECH','SE_M_SELF_DESTRUCT','SE_M_SUPERSONIC','SE_M_TAIL_WHIP','SE_M_THUNDERBOLT','SE_M_THUNDERBOLT2','SE_M_THUNDER_WAVE','SE_M_TRI_ATTACK2','SE_M_VITAL_THROW2','SE_M_WHIRLPOOL'}
TASKS|={'AnimTask_DeepInhale','SoundTask_PlayCryHighPitch','SoundTask_PlayDoubleCry','SoundTask_PlayCryWithEcho','SoundTask_WaitForCry','AnimTask_ShakeBattleTerrain','AnimTask_ScaleMonAndRestore'}
GFX={'ANIM_TAG_BLACK_SMOKE','ANIM_TAG_SPARK', 'ANIM_TAG_METAL_SOUND_WAVES', 'ANIM_TAG_BROWN_ORB', 'ANIM_TAG_LIGHTNING', 'ANIM_TAG_SPARK_2', 'ANIM_TAG_LARGE_FRESH_EGG', 'ANIM_TAG_ICE_CRYSTALS', 'ANIM_TAG_DUCK', 'ANIM_TAG_PURPLE_RING', 'ANIM_TAG_GLOWY_RED_ORB', 'ANIM_TAG_IMPACT', 'ANIM_TAG_SPARK_H', 'ANIM_TAG_BLACK_BALL_2', 'ANIM_TAG_SMALL_EMBER', 'ANIM_TAG_GLOWY_GREEN_ORB', 'ANIM_TAG_WATER_IMPACT', 'ANIM_TAG_BLUE_STAR', 'ANIM_TAG_WATER_ORB', 'ANIM_TAG_HANDS_AND_FEET', 'ANIM_TAG_GRAY_SMOKE', 'ANIM_TAG_SMALL_BUBBLES', 'ANIM_TAG_ORBS', 'ANIM_TAG_BLACK_BALL', 'ANIM_TAG_GOLD_RING', 'ANIM_TAG_EXPLOSION', 'ANIM_TAG_SHOCK_3'}
GFX|={'ANIM_TAG_POKEBALL','ANIM_TAG_SPOTLIGHT','ANIM_TAG_CONFETTI'}
SOUNDS|={'SE_M_BATON_PASS'}
SOUNDS|={'SE_M_DIG'}
SOUNDS|={'SE_M_ENCORE2','SE_M_FLATTER','SE_M_JUMP_KICK'}
GFX|={'ANIM_TAG_PAW_PRINT','ANIM_TAG_PINK_PETAL'}
SOUNDS|={'SE_M_SCRATCH','SE_M_SWEET_SCENT'}
GFX|={'ANIM_TAG_AMNESIA','ANIM_TAG_FLOWER'}
GFX|={'ANIM_TAG_ELECTRIC_ORBS','ANIM_TAG_ELECTRICITY','ANIM_TAG_WISP_ORB','ANIM_TAG_WISP_FIRE'}
TASKS|={'AnimTask_ElectricChargingParticles','AnimTask_ShockWaveProgressingBolt','AnimTask_ShockWaveLightning','AnimTask_VoltTackleBolt','AnimTask_VoltTackleAttackerReappear'}
SOUNDS|={'SE_M_CHARGE'}
SOUNDS|={'SE_M_PETAL_DANCE'}
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
SHARED_SPRITES|={'gAngerMarkSpriteTemplate'}
GFX|={'ANIM_TAG_FINGER','ANIM_TAG_FINGER_2','ANIM_TAG_ANGER'}
SOUNDS|={'SE_M_SWAGGER2','SE_M_ATTRACT2'}
TASKS|={'AnimTask_TormentAttacker'}
OVERLAY_MATERIALS={'AnimTask_HeartsBackground':('attract',),'AnimTask_ScaryFace':('scary-face-player','scary-face-opponent'),'AnimTask_HazeScrollingFog':('fog',),'AnimTask_MistBallFog':('fog',),'AnimTask_LoadSandstormBackground':('sandstorm',),'AnimTask_MorningSunLightBeam':('morning-sun',)}
OVERLAY_TASKS=set(OVERLAY_MATERIALS)
TASKS|={'AnimTask_AlphaFadeIn','AnimTask_MoonlightEndFade','AnimTask_RotateAuroraRingColors'}
GFX|={'ANIM_TAG_LETTER_Z','ANIM_TAG_GREEN_STAR','ANIM_TAG_MOON','ANIM_TAG_GREEN_SPARKLE','ANIM_TAG_RAINBOW_RINGS'}
SOUNDS|={'SE_M_SNORE','SE_M_MOONLIGHT','SE_M_BUBBLE_BEAM'}
TASKS|={'AnimTask_BlendBackground','AnimTask_MoveHeatWaveTargets','AnimTask_CreateRaindrops'}
GFX|={'ANIM_TAG_FLYING_DIRT','ANIM_TAG_RAIN_DROPS','ANIM_TAG_SUNLIGHT'}
SOUNDS|={'SE_M_SANDSTORM','SE_M_HEAT_WAVE','SE_M_RAIN_DANCE','SE_M_PETAL_DANCE'}
GFX|={'ANIM_TAG_ALERT','ANIM_TAG_BENT_SPOON','ANIM_TAG_SPIRAL','ANIM_TAG_WHITE_CIRCLE_OF_LIGHT'}
TASKS|={'AnimTask_BlendColorCycleExclude','AnimTask_SetPsychicBackground','AnimTask_FadeScreenToWhite','SoundTask_PlaySE1WithPanning'}
SHARED_SPRITES|={'gRandomPosHitSplatSpriteTemplate'}
SOUNDS|={'SE_M_SOLAR_BEAM'}
TASKS|=PALETTE_BACKUP_TASKS
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
GFX|={'ANIM_TAG_GUARD_RING','ANIM_TAG_PURPLE_GAS_CLOUD'}
SOUNDS|={'SE_M_MIST','SE_SHINY','SE_M_SURF'}
TASKS|={'SoundTask_AdjustPanningVar','AnimTask_BlendColorCycleByTag'}
GFX|={'ANIM_TAG_YELLOW_BALL','ANIM_TAG_DEVIL','ANIM_TAG_PINK_HEART','ANIM_TAG_ANGEL','ANIM_TAG_RED_HEART','ANIM_TAG_MAGNIFYING_GLASS'}
SOUNDS|={'SE_M_ATTRACT','SE_M_HEAL_BELL','SE_M_SKETCH','SE_M_DETECT'}
TASKS |= MEMENTO_TASKS | SQUASH_TASKS | {'AnimTask_WaterSport'}
GFX |= {'ANIM_TAG_WARM_ROCK','ANIM_TAG_GLOWY_BLUE_ORB'}
SOUNDS |= {'SE_M_EXPLOSION','SE_M_DIVE','SE_M_SURF','SE_M_ROCK_THROW'}
def squash_dependencies(reference,commands):
 used={c['args'][0] for c in commands if c['op']=='createvisualtask'}&SQUASH_TASKS
 if not used:return []
 def body(file,name):
  text=read(reference,'src/'+file).decode();m=re.search(r'^(?:static )?(?:void|u8|u16) '+name+r'\([^;]*?\)\s*\{(.*?)^\}',text,re.S|re.M)
  if m is None:raise ValueError('Missing squash callback '+name)
  return m[1]
 children=[]
 for task,file,callback,template in [('AnimTask_EruptionLaunchRocks','battle_anim_fire.c','CreateEruptionLaunchRocks','gEruptionLaunchRockSpriteTemplate'),('AnimTask_WaterSpoutLaunch','battle_anim_water.c','CreateWaterSpoutLaunchDroplets','gSmallWaterOrbSpriteTemplate'),('AnimTask_WaterSpoutRain','battle_anim_water.c','CreateWaterSpoutRainDroplet','gSmallWaterOrbSpriteTemplate')]:
  if task not in used:continue
  b=body(file,callback)
  if re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',b)!=[template] or 'spriteId != MAX_SPRITES' not in b:raise ValueError('Squash raw child ownership differs: '+callback)
  children.append(template)
 if 'AnimTask_WaterSpoutRain' in used:
  b=body('battle_anim_water.c','AnimWaterSpoutRain')
  if re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',b)!=['gWaterHitSplatSpriteTemplate'] or 'StartSpriteAffineAnim(&gSprites[sprite->data[1]], 3);' not in b or 'gSprites[sprite->data[1]].data[6] = sprite->data[6];' not in b or 'gSprites[sprite->data[1]].data[7] = sprite->data[7];' not in b or 'DestroySprite(sprite);' not in b:raise ValueError('Water Spout affine splash handoff differs')
  children.append('gWaterHitSplatSpriteTemplate')
 return list(dict.fromkeys(children))
def water_sport_dependencies(reference,commands):
 if not any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_WaterSport' for c in commands):return []
 text=read(reference,'src/battle_anim_water.c').decode()
 def body(name):
  match=re.search(r'^(?:static )?void '+name+r'\([^;]*?\)\s*\{(.*?)^\}',text,re.S|re.M)
  if match is None:raise ValueError('Missing Water Sport callback '+name)
  return re.sub(r'\s+','',match[1])
 initial,step,emit,bounce,finish=[body(n) for n in ['AnimTask_WaterSport','AnimTask_WaterSport_Step','CreateWaterSportDroplet','AnimWaterSportDroplet','AnimWaterSportDroplet_Step']]
 contracts=[(initial,['task->data[3]=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_X_2);','task->data[4]=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_Y_PIC_OFFSET);','task->func=AnimTask_WaterSport_Step;']),
  (step,['CreateWaterSportDroplet(task);','if(task->data[8]==0)','DestroyAnimVisualTask(taskId);']),
  (emit,['spriteId=CreateSprite(&gSmallWaterOrbSpriteTemplate,task->data[3],task->data[4],10);','if(spriteId!=MAX_SPRITES)','gSprites[spriteId].data[0]=16;','InitAnimArcTranslation(&gSprites[spriteId]);','gSprites[spriteId].callback=AnimWaterSportDroplet;','task->data[8]++;']),
  (bounce,['if(TranslateAnimHorizontalArc(sprite))','sprite->data[0]=6;','sprite->data[2]=(Random()&0x1F)-16+sprite->x;','sprite->data[4]=(Random()&0x1F)-16+sprite->y;','sprite->data[5]=~(Random()&7);','InitAnimArcTranslation(sprite);','sprite->callback=AnimWaterSportDroplet_Step;']),
  (finish,['if(TranslateAnimHorizontalArc(sprite))','for(i=0;i<NUM_TASKS;i++)','if(gTasks[i].func==AnimTask_WaterSport_Step)','gTasks[i].data[10]=1;','gTasks[i].data[8]--;','DestroySprite(sprite);'])]
 for code,tokens in contracts:
  if any(token not in code for token in tokens):raise ValueError('Water Sport raw task, arc or ownership contract differs')
 if re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',emit)!=['gSmallWaterOrbSpriteTemplate'] or bounce.count('Random()')!=3:raise ValueError('Water Sport raw child or visual random closure differs')
 return ['gSmallWaterOrbSpriteTemplate']
def supported_sound_task(args,songs):
 # The existing task ABI accepts any source-resolved song, not one copied song ID.
 if args and args[0]=='SoundTask_FireBlast':return args==['SoundTask_FireBlast','SE_M_FLAME_WHEEL','SE_M_FLAME_WHEEL2']
 if len(args)!=8 or args[0]!='SoundTask_LoopSEAdjustPanning' or args[1] not in songs:return False
 try:
  values=[{'SOUND_PAN_ATTACKER':-64,'SOUND_PAN_TARGET':63}.get(v,int(v,0) if re.fullmatch(r'-?(?:0x[0-9a-fA-F]+|\d+)',v) else None) for v in args[2:]]
 except ValueError:return False
 return all(v is not None for v in values) and -64<=values[0]<=63 and -64<=values[1]<=63 and -128<=values[2]<=127 and 1<=values[3]<=255 and 0<=values[4]<=255 and 0<=values[5]<=255
TASKS|={'AnimTask_HelpingHandAttackerMovement','AnimTask_ImprisonOrbs','AnimTask_TeeterDanceMovement','AnimTask_HorizontalShake'}
SHARED_SPRITES|={'gRedXSpriteTemplate'}
GFX|={'ANIM_TAG_TAG_HAND','ANIM_TAG_HOLLOW_ORB'}
SOUNDS|={'SE_M_ENCORE','SE_M_TEETER_DANCE'}
def imprison_dependencies(reference,commands):
 if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_ImprisonOrbs' for c in commands):
   psychic=read(reference,'src/battle_anim_psychic.c').decode()
   child=re.search(r'static void AnimTask_ImprisonOrbs_Step\(u8 taskId\)\s*\{(.*?)^\}',psychic,re.S|re.M)
   if child is None or re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',child[1])!=['sImprisonOrbSpriteTemplate'] or 'task->data[task->data[2] + 8] = spriteId;' not in child[1] or 'if (spriteId != MAX_SPRITES)' not in child[1] or 'DestroySprite(&gSprites[task->data[i]]);' not in child[1]:raise ValueError('Imprison raw child ownership differs')
   for required in ['CreateSprite(&sImprisonOrbSpriteTemplate, task->data[13], task->data[14], 0)', 'if (++task->data[2] == 5)', 'if (++task->data[1] == 32)', 'for (i = 8; i < 13; ++i)', 'if (task->data[i] != MAX_SPRITES)']:
    if required not in child[1]:raise ValueError('Imprison raw phase/capacity contract differs')
   return ['sImprisonOrbSpriteTemplate']
 return []
TASKS |= {'AnimTask_SmokescreenImpact'} | VISIBILITY_TASKS | BATTLER_COPY_TASKS | EXPULSION_TASKS | ADDITIONAL_MON_TASKS | MATERIAL_CHANGE_TASKS | {'AnimTask_SetPsychicBackground'}
GFX |= {'ANIM_TAG_WHIRLWIND_LINES'}
SOUNDS |= {'SE_M_TELEPORT','SE_M_TRI_ATTACK','SE_M_DETECT'}
SOUNDS |= {'SE_M_DOUBLE_TEAM','SE_M_MINIMIZE','SE_M_PSYBEAM','SE_M_LEER','SE_M_TAKE_DOWN'}
SOUNDS |= {'SE_M_FAINT_ATTACK'}
def visibility_contracts(reference,commands):
 used={c['args'][0] for c in commands if c['op']=='createvisualtask'}&VISIBILITY_TASKS
 if not used and not any(c['op'] in {'visible','invisible'} for c in commands):return
 def expect(path,name,*fragments):
  text=read(reference,'src/'+path).decode();match=re.search(r'^(?:static )?(?:void|bool8|u8|s16) '+re.escape(name)+r'\([^;]*?\)\s*\{(.*?)^\}',text,re.S|re.M)
  if match is None:raise ValueError('Missing visibility source function '+name)
  body=re.sub(r'\s+','',match[1])
  for fragment in fragments:
   if re.sub(r'\s+','',fragment) not in body:raise ValueError('Visibility source contract differs: '+name)
  return body
 expect('battle_anim.c','IsBattlerSpriteVisible','if (!IsBattlerSpritePresent(battler)) return FALSE;','if (!gBattleSpritesDataPtr->battlerData[battler].invisible || !gSprites[gBattlerSpriteIds[battler]].invisible) return TRUE;','return FALSE;')
 for name,value in [('Cmd_visible','FALSE'),('Cmd_invisible','TRUE')]:expect('battle_anim.c',name,'spriteId = GetAnimBattlerSpriteId(sBattleAnimScriptPtr[1]);','if (spriteId != SPRITE_NONE) gSprites[spriteId].invisible = '+value+';')
 if 'AnimTask_SetAttackerInvisibleWaitForSignal' in used:
  expect('battle_anim_utility_funcs.c','AnimTask_SetAttackerInvisibleWaitForSignal','gTasks[taskId].data[0] = gBattleSpritesDataPtr->battlerData[gBattleAnimAttacker].invisible;','gBattleSpritesDataPtr->battlerData[gBattleAnimAttacker].invisible = 1;','gTasks[taskId].func = AnimTask_WaitAndRestoreVisibility;','--gAnimVisualTaskCount;')
  expect('battle_anim_utility_funcs.c','AnimTask_WaitAndRestoreVisibility','if (gBattleAnimArgs[7] == 0x1000)','gBattleSpritesDataPtr->battlerData[gBattleAnimAttacker].invisible = (u8)gTasks[taskId].data[0] & 1;','DestroyTask(taskId);')
 if 'AnimTask_SetCamouflageBlend' in used:
  body=expect('battle_anim_utility_funcs.c','AnimTask_SetCamouflageBlend','u32 selectedPalettes = UnpackSelectedBattlePalettes(gBattleAnimArgs[0]);','switch (gBattleTerrain)','StartBlendAnimSpriteColor(taskId, selectedPalettes);')
  if re.findall(r'case(BATTLE_TERRAIN_\w+):gBattleAnimArgs\[4\]=RGB\((\d+),(\d+),(\d+)\);break;',body)!=[(name,*map(str,color)) for name,color in [('BATTLE_TERRAIN_GRASS',(12,24,2)),('BATTLE_TERRAIN_LONG_GRASS',(0,15,2)),('BATTLE_TERRAIN_SAND',(30,24,11)),('BATTLE_TERRAIN_UNDERWATER',(0,0,18)),('BATTLE_TERRAIN_WATER',(11,22,31)),('BATTLE_TERRAIN_POND',(11,22,31)),('BATTLE_TERRAIN_MOUNTAIN',(22,16,10)),('BATTLE_TERRAIN_CAVE',(14,9,3)),('BATTLE_TERRAIN_BUILDING',(31,31,31)),('BATTLE_TERRAIN_PLAIN',(31,31,31))]]:raise ValueError('Camouflage native terrain colors differ')
 if 'AnimTask_BlendBattleAnimPalExclude' in used:expect('battle_anim_utility_funcs.c','AnimTask_BlendBattleAnimPalExclude','animBattlers[1] = 0xFF;','selectedPalettes = UnpackSelectedBattlePalettes(1);','battler != animBattlers[0] && battler != animBattlers[1] && IsBattlerSpriteVisible(battler)','selectedPalettes |= 0x10000 << GetSpritePalIdxByBattler(battler);')
 if 'AnimTask_SetAllNonAttackersInvisiblity' in used:expect('battle_anim_utility_funcs.c','AnimTask_SetAllNonAttackersInvisiblity','battler != gBattleAnimAttacker && IsBattlerSpriteVisible(battler)','gSprites[gBattlerSpriteIds[battler]].invisible = gBattleAnimArgs[0];')
 for name in ['AnimTask_AttackerFadeToInvisible','AnimTask_AttackerFadeFromInvisible']:
  if name in used:
   expect('battle_anim_dark.c',name,'gTasks[taskId].data[0] = gBattleAnimArgs[0];','gTasks[taskId].func = '+name+'_Step;')
   expect('battle_anim_dark.c',name+'_Step','gTasks[taskId].data[2] == (u8)gTasks[taskId].data[0]','gTasks[taskId].data[2] = 0;','DestroyAnimVisualTask(taskId);',*(['++blendA;','--blendB;','gSprites[gBattlerSpriteIds[gBattleAnimAttacker]].invisible = TRUE;'] if name.endswith('ToInvisible') else ['--blendA;','++blendB;','SetGpuReg(REG_OFFSET_BLDCNT, 0);','SetGpuReg(REG_OFFSET_BLDALPHA, 0);']))
 if 'AnimTask_OdorSleuthMovement' in used:
  body=expect('battle_anim_effects_3.c','AnimTask_OdorSleuthMovement','if (spriteId1 < 0)','if (spriteId2 < 0)','DestroySpriteWithActiveSheet(&gSprites[spriteId1]);','gTasks[taskId].data[0] = 2;','if (!gBattleSpritesDataPtr->battlerData[gBattleAnimTarget].invisible)')
  if body.count('CloneBattlerSpriteWithBlend(ANIM_TARGET)')!=2:raise ValueError('Odor Sleuth requires exactly two raw clones')
  expect('battle_anim_effects_3.c','MoveOdorSleuthClone','if (++sprite->data[1] > 1)','if (!gBattleSpritesDataPtr->battlerData[gBattleAnimTarget].invisible) sprite->invisible ^= 1;','sprite->x2 = Cos(sprite->data[4], sprite->data[5]);','if (++sprite->data[2] == 60)','sprite->data[5] -= 2;','gTasks[sprite->data[6]].data[sprite->data[7]]--;','DestroySpriteWithActiveSheet(sprite);')
  expect('battle_anim_mons.c','CloneBattlerSpriteWithBlend','for (i = 0; i < MAX_SPRITES; ++i)','if (!gSprites[i].inUse)','gSprites[i] = gSprites[spriteId];','gSprites[i].invisible = FALSE;')

def expulsion_contracts(reference,commands):
 used={c['args'][0] for c in commands if c['op'] in {'createvisualtask','createsprite'}}
 def expect(file,name,*parts):
  source=read(reference,'src/'+file).decode();m=re.search(r'^(?:static )?void '+re.escape(name)+r'\([^;]+?\)\s*\{(.*?)^\}',source,re.S|re.M)
  if m is None:raise ValueError('Missing expulsion callback '+name)
  body=' '.join(m[1].split())
  if any(' '.join(part.split()) not in body for part in parts):raise ValueError('Expulsion callback contract differs: '+name)
 if 'AnimTask_SlideOffScreen' in used:
  expect('battle_anim_mon_movement.c','AnimTask_SlideOffScreen','GetAnimBattlerSpriteId(gBattleAnimArgs[0])','if (!IsBattlerSpriteVisible(gBattleAnimAttacker ^ BIT_FLANK))','if (!IsBattlerSpriteVisible(gBattleAnimTarget ^ BIT_FLANK))','if (GetBattlerSide(gBattleAnimTarget) != B_SIDE_PLAYER)','gTasks[taskId].data[1] = -gBattleAnimArgs[1];','gTasks[taskId].func = AnimTask_SlideOffScreen_Step;')
  expect('battle_anim_mon_movement.c','AnimTask_SlideOffScreen_Step','gSprites[spriteId].x2 += gTasks[taskId].data[1];','gSprites[spriteId].x2 + gSprites[spriteId].x < -32 || gSprites[spriteId].x2 + gSprites[spriteId].x > DISPLAY_WIDTH + 32','DestroyAnimVisualTask(taskId);')
 if 'AnimTask_Teleport' in used:
  expect('battle_anim_psychic.c','AnimTask_Teleport','GetAnimBattlerSpriteId(ANIM_ATTACKER)','GetBattlerSide(gBattleAnimAttacker) != B_SIDE_PLAYER ? 4 : 8','PrepareAffineAnimInTaskData(task, task->data[0], sAffineAnim_Teleport);')
  expect('battle_anim_psychic.c','AnimTask_Teleport_Step','RunAffineAnimFromTaskData(task);','if (++task->data[2] > 19)','gSprites[task->data[0]].y2 -= 8;','gSprites[task->data[0]].invisible = TRUE;','gSprites[task->data[0]].x = DISPLAY_WIDTH + 32;','ResetSpriteRotScale(task->data[0]);','DestroyAnimVisualTask(taskId);')
 if 'gWhirlwindLineSpriteTemplate' in used:
  expect('battle_anim_flying.c','AnimWhirlwindLine','InitSpritePosToAnimAttacker(sprite, 0);','InitSpritePosToAnimTarget(sprite, FALSE);','sprite->x += 8;','SeekSpriteAnim(sprite, gBattleAnimArgs[4]);','sprite->x -= 32;','sprite->data[1] = 0x0ccc;','mult = 12;','sprite->data[7] = gBattleAnimArgs[3];')
  expect('battle_anim_flying.c','AnimWhirlwindLine_Step','sprite->x2 += sprite->data[1] >> 8;','if (++sprite->data[0] == 6)','sprite->x2 = 0;','StartSpriteAnim(sprite, 0);','if (--sprite->data[7] == -1)')
 if 'SoundTask_PlayDoubleCry' in used:
  constants=read(reference,'include/constants/sound.h').decode()
  if not re.search(r'^#define DOUBLE_CRY_ROAR\s+2\b',constants,re.M) or not re.search(r'^#define DOUBLE_CRY_GROWL\s+255\b',constants,re.M):raise ValueError('Double cry source selectors differ')

def palette_backup_contracts(reference,commands):
 used={c['args'][0] for c in commands if c['op'] in {'createvisualtask','createsprite'}}
 if not used & (PALETTE_BACKUP_TASKS|{'gOverheatFlameSpriteTemplate'}):return
 def exact(file,name,expected):
  source=read(reference,'src/'+file).decode();m=re.search(r'^(?:static )?void '+re.escape(name)+r'\([^;]+?\)\s*\{(.*?)^\}',source,re.S|re.M)
  if m is None or re.sub(r'\s+','',m[1])!=re.sub(r'\s+','',expected):raise ValueError('Palette backup callback contract differs: '+name)
 if used & PALETTE_BACKUP_TASKS:
  header=read(reference,'include/battle.h').decode()
  if not re.search(r'u16 \*multiUseBuffer;',header):raise ValueError('Palette backup source element width differs')
  mons=read(reference,'src/battle_anim_mons.c').decode()
  mask=re.search(r'u32 GetBattlePalettesMask\([^;]+?\)\s*\{(.*?)^\}',mons,re.S|re.M)
  if mask is None or 'selectedPalettes = 0xe;' not in mask[1]:raise ValueError('Palette backup background selection differs')
  exact('battle_anim_utility_funcs.c','AnimTask_AllocBackupPalBuffer','gMonSpritesGfxPtr->multiUseBuffer = AllocZeroed(0x2000); DestroyAnimVisualTask(taskId);')
  exact('battle_anim_utility_funcs.c','AnimTask_FreeBackupPalBuffer','FREE_AND_SET_NULL(gMonSpritesGfxPtr->multiUseBuffer); DestroyAnimVisualTask(taskId);')
  selection='u32 selectedPalettes; s32 paletteIndex = 0;\n    if (gBattleAnimArgs[0] == 0)\n        for (selectedPalettes = GetBattlePalettesMask(TRUE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE);\n             (selectedPalettes & 1) == 0;\n             ++paletteIndex)\n            selectedPalettes >>= 1;\n    else if (gBattleAnimArgs[0] == 1) paletteIndex = gBattleAnimAttacker + 16;\n    else if (gBattleAnimArgs[0] == 2) paletteIndex = gBattleAnimTarget + 16;'
  for name,dest,source in [('AnimTask_CopyPalUnfadedToBackup','&gMonSpritesGfxPtr->multiUseBuffer[gBattleAnimArgs[1] * 16]','&gPlttBufferUnfaded[PLTT_ID(paletteIndex)]'),('AnimTask_CopyPalUnfadedFromBackup','&gPlttBufferUnfaded[PLTT_ID(paletteIndex)]','&gMonSpritesGfxPtr->multiUseBuffer[gBattleAnimArgs[1] * 16]'),('AnimTask_CopyPalFadedToUnfaded','&gPlttBufferUnfaded[PLTT_ID(paletteIndex)]','&gPlttBufferFaded[PLTT_ID(paletteIndex)]')]:
   exact('battle_anim_utility_funcs.c',name,selection+'memcpy('+dest+','+source+', PLTT_SIZE_4BPP); DestroyAnimVisualTask(taskId);')
 if 'gOverheatFlameSpriteTemplate' in used:
  exact('battle_anim_dragon.c','AnimOverheatFlame','s32 i, yAmplitude = (gBattleAnimArgs[2] * 3) / 5;\n    sprite->x = GetBattlerSpriteCoord(gBattleAnimAttacker, BATTLER_COORD_X_2);\n    sprite->y = GetBattlerSpriteCoord(gBattleAnimAttacker, BATTLER_COORD_Y_PIC_OFFSET) + gBattleAnimArgs[4];\n    sprite->data[1] = Cos(gBattleAnimArgs[1], gBattleAnimArgs[2]);\n    sprite->data[2] = Sin(gBattleAnimArgs[1], yAmplitude);\n    sprite->x += sprite->data[1] * gBattleAnimArgs[0]; sprite->y += sprite->data[2] * gBattleAnimArgs[0];\n    sprite->data[3] = gBattleAnimArgs[3]; sprite->callback = AnimOverheatFlame_Step;\n    for (i = 0; i < 7; ++i) sUnusedOverheatData[i] = sprite->data[i];')
  exact('battle_anim_dragon.c','AnimOverheatFlame_Step','sprite->data[4] += sprite->data[1]; sprite->data[5] += sprite->data[2];\n    sprite->x2 = sprite->data[4] / 10; sprite->y2 = sprite->data[5] / 10;\n    if (++sprite->data[0] > sprite->data[3]) DestroyAnimSprite(sprite);')

def feather_contracts(reference,flat):
 if not any(c['op']=='createsprite' and c['args'][0]=='gFallingFeatherSpriteTemplate' for c in flat):return
 source=read(reference,'src/battle_anim_flying.c').decode()
 template=re.search(r'const struct SpriteTemplate gFallingFeatherSpriteTemplate\s*=\s*\{(.*?)\};',source,re.S)
 if template is None or '.oam = &gOamData_AffineNormal_ObjNormal_32x32,' not in template[1] or '.affineAnims = gDummySpriteAffineAnimTable,' not in template[1]:raise ValueError('Falling feather manual affine recipe differs')
 structure=re.search(r'struct FeatherDanceData\s*\{(.*?)\};',source,re.S)
 expected='u16unk0_0a:1;u16unk0_0b:1;u16unk0_0c:1;u16unk0_0d:1;u16unk0_1:4;u16unk1:8;u16unk2;s16unk4;u16unk6;u16unk8;u16unkA;u8unkC[2];u16unkE_0:1;u16unkE_1:15;'
 if structure is None or re.sub(r'\s+','',structure[1])!=expected:raise ValueError('Falling feather native field widths differ')
 normalized=re.sub(r'\s+','',source)
 for required in ['sprite->x=GetBattlerSpriteCoord(battler,BATTLER_COORD_ATTR_HEIGHT)+gBattleAnimArgs[0];','spriteCoord=GetBattlerSpriteCoord(battler,BATTLER_COORD_ATTR_WIDTH);','data->unkE_1=spriteCoord+gBattleAnimArgs[6];','*(u16*)(data->unkC)=gBattleAnimArgs[5];','if(data->unk1--%256==0)','sprite->x2=((s32)data->unkC[data->unk0_0b]*gSineTable[data->unk2])>>8;','sinIndex=(-sprite->x2>>1)+data->unkA;','gOamMatrices[matrixNum].a=gOamMatrices[matrixNum].d=gSineTable[sinIndex+64];','data->unk8+=data->unk6;','sprite->y=data->unk8>>8;','if(sprite->y+sprite->y2>=data->unkE_1)','sprite->callback=DestroyAnimSpriteAfterTimer;']:
  if required not in normalized:raise ValueError('Falling feather native callback contract differs')
 destructor=re.search(r'void DestroyAnimSpriteAfterTimer\(struct Sprite \*sprite\)\s*\{(.*?)^\}',source,re.S|re.M)
 if destructor is None or re.sub(r'\s+','',destructor[1])!='if(sprite->data[0]--<=0){if(sprite->oam.affineMode&ST_OAM_AFFINE_ON_MASK){FreeOamMatrix(sprite->oam.matrixNum);sprite->oam.affineMode=ST_OAM_AFFINE_OFF;}DestroySprite(sprite);--gAnimVisualTaskCount;}':raise ValueError('Falling feather matrix/count destructor differs')

def rollout_dependencies(reference,row,commands):
 if not any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_Rollout' for c in commands):return
 tasks=[c for c in commands if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_Rollout']
 if len(tasks)!=1 or len(tasks[0]['args'])!=2:raise ValueError('Rollout task operand contract differs')
 rock=read(reference,'src/battle_anim_rock.c').decode()
 def body(name):
  found=re.search(r'^(?:static )?(?:void|u8) '+name+r'\([^;]*?\)\s*\{(.*?)^\}',rock,re.S|re.M)
  if found is None:raise ValueError('Missing Rollout callback '+name)
  return found[1]
 init,step,child,particle,counter=[body(n) for n in ['AnimTask_Rollout','AnimTask_Rollout_Step','CreateRolloutDirtSprite','AnimRolloutParticle','GetRolloutCounter']]
 if re.findall(r'spriteTemplate = &([A-Za-z0-9_]+);',child)!=['gRolloutMudSpriteTemplate','gRolloutRockSpriteTemplate','gRolloutRockSpriteTemplate','gRolloutRockSpriteTemplate']:raise ValueError('Rollout raw templates differ')
 for fragment in ['CreateSprite(spriteTemplate, x, y, 35)','if (spriteId != MAX_SPRITES)','gSprites[spriteId].data[0] = 18;','InitAnimArcTranslation(&gSprites[spriteId]);','++task->data[11];','task->data[12] *= -1;']:
  if fragment not in child:raise ValueError('Rollout raw child ownership differs')
 if 'FindTaskIdByFunc(AnimTask_Rollout_Step)' not in particle or '--gTasks[taskId].data[11];' not in particle or 'DestroySprite(sprite);' not in particle or 'DestroyAnimSprite' in particle:raise ValueError('Rollout raw destructor differs')
 if 'task->func = AnimTask_Rollout_Step;' not in init or 'if (task->data[11] == 0)' not in step or 'DestroyAnimVisualTask(taskId);' not in step:raise ValueError('Rollout task ownership differs')
 if re.sub(r'\s+','',counter)!='u8retVal=gAnimDisableStructPtr->rolloutTimerStartValue-gAnimDisableStructPtr->rolloutTimer;u8var0=retVal-1;if(var0>4)retVal=1;returnretVal;':raise ValueError('Rollout source counter differs')
 cues=re.findall(r'PlaySE12WithPanning\((SE_\w+), task->data\[13\]\);',step)
 if cues!=['SE_M_HEADBUTT','SE_M_DIG']:raise ValueError('Rollout callback sounds differ')
 row['spriteDependencies']=['gRolloutMudSpriteTemplate','gRolloutRockSpriteTemplate']
 row['soundEffects'].update(sound_effects(reference,[dict(op='playse',args=[cue]) for cue in cues]))
 registrations=read(reference,'src/data/battle_anim.h').decode();graphics=read(reference,'src/graphics.c').decode()
 for tag,gfx,pal,gfx_stem,pal_stem,size in [('ALERT','Alert','Alert','alert','alert',0x700),('BENT_SPOON','BentSpoon','BentSpoon','bent_spoon','bent_spoon',0xc00),('SPIRAL','Spiral','Spiral','spiral','spiral',0x800),('WHITE_CIRCLE_OF_LIGHT','CircleOfLight','WhiteCircleOfLight','circle_of_light','white_circle_of_light',0x800)]:
  if not re.search(r'\{gBattleAnimSpriteGfx_'+gfx+r',\s*0x'+format(size,'04x')+r',\s*ANIM_TAG_'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal+r',\s*ANIM_TAG_'+tag+r'\}',registrations):raise ValueError('Psychic source sheet/palette registration differs: '+tag)
  for kind,symbol,stem,suffix in [('Gfx',gfx,gfx_stem,'4bpp'),('Pal',pal,pal_stem,'gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Psychic source material differs: '+tag)

 for stem,symbol,size in [('mud_sand','MudSand',0xa0),('rocks','Rocks',0xc00),('impact','Impact',0x200)]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registrations):raise ValueError('Rollout material registration differs: '+stem)
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Rollout material path differs: '+stem)
 rules=read(reference,'graphics_file_rules.mk').decode()
 if '$(BTLANMSPRGFXDIR)/mud_sand.4bpp: $(BTLANMSPRGFXDIR)/mud_sand_0.4bpp $(BTLANMSPRGFXDIR)/mud_sand_1.4bpp' not in re.sub(r'\s+',' ',rules.replace('\\\n',' ')):raise ValueError('Rollout mud tile concatenation differs')

def material_change_contracts(reference,row,commands):
 used=[c for c in commands if c['op']=='createvisualtask' and c['args'][0] in MATERIAL_CHANGE_TASKS]
 if not used:return
 if len(used)!=1:raise ValueError('Battler material transition requires one source task owner')
 task=used[0]['args'][0]
 if used[0]['args']!=([task,'2','0'] if task=='AnimTask_TransformMon' else [task,'2']):raise ValueError('Battler material task ABI differs')
 effects=read(reference,'src/battle_anim_effects_3.c').decode()
 def body(name):
  match=re.search(r'^(?:static )?void '+name+r'\(u8 taskId\)\s*\{(.*?)^\}',effects,re.M|re.S)
  if match is None:raise ValueError('Missing battler material callback '+name)
  return match[1]
 def require(text,*tokens):
  compact=re.sub(r'\s+','',text)
  if any(re.sub(r'\s+','',token) not in compact for token in tokens):raise ValueError('Battler material callback contract differs')
 first=body(task)
 if task=='AnimTask_TransformMon':
  require(first,'gTasks[taskId].data[10] = gBattleAnimArgs[0];','if (gTasks[taskId].data[2]++ > 1)',
   'if (stretch == 15)','if (stretch == 0)','HandleSpeciesGfxDataChange(gBattleAnimAttacker, gBattleAnimTarget, gTasks[taskId].data[10]);',
   'CpuCopy32(src, dest, MON_PIC_SIZE);','LoadBgTiles(1, animBg.bgTiles, 0x800, animBg.tilesOffset);','DestroyAnimVisualTask(taskId);')
  if first.count('gTasks[taskId].data[2]++ > 1')!=2:raise ValueError('Transform mosaic cadence differs')
 else:
  require(first,'PrepareBattlerSpriteForRotScale(spriteId, FALSE);','gTasks[taskId].data[1] = 0x100;',
   'gTasks[taskId].data[2] = 0x100;','gTasks[taskId].data[1] += 0x60;','gTasks[taskId].data[2] -= 0xD;',
   'if (++gTasks[taskId].data[3] == 9)','ResetSpriteRotScale(spriteId);','gSprites[spriteId].invisible = TRUE;',
   'LoadBattleMonGfxAndAnimate(gBattleAnimAttacker, 0, spriteId);','gTasks[taskId].data[i] = 0;',
   'gTasks[taskId].func = AnimTask_MonToSubstituteDoll;')
  child=body('AnimTask_MonToSubstituteDoll')
  require(child,'gSprites[spriteId].y2 = -200;','gSprites[spriteId].x2 = 200;','gSprites[spriteId].invisible = FALSE;',
   'gTasks[taskId].data[10] += 112;','gTasks[taskId].data[10] -= 112;','gTasks[taskId].data[10] -= 0x800;',
   'gSprites[spriteId].y + gSprites[spriteId].y2 >= -32','gSprites[spriteId].x2 = 0;',
   'gSprites[spriteId].y2 -= gTasks[taskId].data[10] >> 8;','DestroyAnimVisualTask(taskId);')
  sounds=re.findall(r'PlaySE12WithPanning\((SE_\w+), BattleAnimAdjustPanning\(SOUND_PAN_ATTACKER\)\);',child)
  if sounds!=['SE_M_BUBBLE2','SE_M_BUBBLE2']:raise ValueError('Substitute landing sound dependencies differ')
  if re.search(r'\bCreate(?:Sprite|Task)\(',first+child):raise ValueError('Substitute uses the existing battler and task')
  row['soundEffects'].update(sound_effects(reference,[dict(op='playsewithpan',args=[sound,'SOUND_PAN_ATTACKER']) for sound in sounds]))

def smokescreen_dependencies(reference,row,flat):
 if not any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_SmokescreenImpact' for c in flat):return
 source=read(reference,'src/battle_anim_smokescreen.c').decode();effects=read(reference,'src/battle_anim_effects_3.c').decode()
 def body(text,name):
  match=re.search(r'(?:static )?(?:void|u8) '+name+r'\([^;]*?\)\s*\{(.*?)^\}',text,re.S|re.M)
  if match is None:raise ValueError('Missing Smokescreen helper '+name)
  return match[1]
 wrapper=re.sub(r'\s+','',body(effects,'AnimTask_SmokescreenImpact'))
 if wrapper!='SmokescreenImpact(GetBattlerSpriteCoord(gBattleAnimTarget,BATTLER_COORD_X_2)+8,GetBattlerSpriteCoord(gBattleAnimTarget,BATTLER_COORD_Y_PIC_OFFSET)+8,FALSE);DestroyAnimVisualTask(taskId);':raise ValueError('Smokescreen wrapper lifetime differs')
 tag=re.search(r'^#define TAG_SMOKESCREEN (\d+)$',source,re.M)
 if not tag or int(tag[1])!=55019:raise ValueError('Smokescreen dynamic tag differs')
 for type,name,expected in [('CompressedSpriteSheet','sSmokescreenImpactSpriteSheet',{'data':'gSmokescreenImpactTiles','size':'0x180','tag':'TAG_SMOKESCREEN'}),('CompressedSpritePalette','sSmokescreenImpactSpritePalette',{'data':'gSmokescreenImpactPalette','tag':'TAG_SMOKESCREEN'})]:
  match=re.search(r'const struct '+type+' '+name+r'\s*=\s*\{(.*?)\};',source,re.S)
  fields=dict(re.findall(r'\.(\w+)\s*=\s*([^,}]+)',match[1])) if match else {}
  if {k:v.strip() for k,v in fields.items()}!=expected:raise ValueError('Smokescreen compressed material differs')
 create=body(source,'SmokescreenImpact');main=body(source,'SpriteCB_SmokescreenImpactMain');child=body(source,'SpriteCB_SmokescreenImpact')
 if re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',create)!=['sSmokescreenImpactSpriteTemplate']*4 or create.count('AnimateSprite(')!=4 or create.count('mainSprite->sActiveSprites++')!=4:raise ValueError('Smokescreen raw quadrant creation differs')
 if 'GetSpriteTileStartByTag(sSmokescreenImpactSpriteSheet.tag) == 0xFFFF' not in create or 'LoadCompressedSpriteSheetUsingHeap(&sSmokescreenImpactSpriteSheet);' not in create or 'LoadCompressedSpritePaletteUsingHeap(&sSmokescreenImpactSpritePalette);' not in create or 'CreateInvisibleSpriteWithCallback(SpriteCB_SmokescreenImpactMain)' not in create:raise ValueError('Smokescreen dynamic allocation differs')
 if 'sprite->sActiveSprites == 0' not in main or 'FreeSpriteTilesByTag(sSmokescreenImpactSpriteSheet.tag);' not in main or 'FreeSpritePaletteByTag(sSmokescreenImpactSpritePalette.tag);' not in main or 'if (!sprite->sPersist)' not in main or 'DestroySprite(sprite);' not in main:raise ValueError('Smokescreen parent cleanup differs')
 if re.sub(r'\s+','',child)!='if(sprite->animEnded){gSprites[sprite->sMainSpriteId].sActiveSprites--;DestroySprite(sprite);}':raise ValueError('Smokescreen raw child cleanup differs')
 graphics=read(reference,'src/graphics.c').decode();registrations=read(reference,'src/data/battle_anim.h').decode()
 for stem,symbol,size in [('black_smoke','BlackSmoke',0x100),('black_ball','BlackBall',0x20)]:
  material_tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+material_tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_BlackSmoke,\s*'+material_tag+r'\}',registrations):raise ValueError('Smokescreen counted sheet/shared palette registration differs')
  if not re.search(r'gBattleAnimSpriteGfx_'+symbol+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/'+stem+r'\.4bpp\.lz"\)',graphics):raise ValueError('Smokescreen counted sheet path differs')
 if not re.search(r'gBattleAnimSpritePal_BlackSmoke\[\] = INCBIN_U32\("graphics/battle_anims/sprites/black_smoke\.gbapal\.lz"\)',graphics):raise ValueError('Smokescreen shared palette path differs')
 # The existing RGB-only Black Ball material has the same pinned source palette.
 # Guard that equality while its common owner retains its current RGB contract.
 with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/black_ball.png'))) as ball,Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/black_smoke.png'))) as smoke:
  if ball.getpalette()[:48]!=smoke.getpalette()[:48]:raise ValueError('Black Ball RGB owner requires its registered Black Smoke palette')
 for symbol,suffix in [('gSmokescreenImpactTiles','4bpp'),('gSmokescreenImpactPalette','gbapal')]:
  if not re.search(symbol+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/smokescreen_impact\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Smokescreen material path differs')
 oam=re.search(r'const struct OamData sOamData_SmokescreenImpact\s*=\s*\{(.*?)\};',source,re.S)
 if not oam or re.search(r'\.priority\s*=\s*1\s*,',oam[1]) is None:raise ValueError('Smokescreen OAM priority differs')
 row['spriteTags']['TAG_SMOKESCREEN']=int(tag[1]);row['spriteDependencies']=list(dict.fromkeys(row.get('spriteDependencies',[])+['sSmokescreenImpactSpriteTemplate']))

def discover_scripts(reference):
 """Discover owned source programs without generating sprite materials."""
 source=read(reference,'data/battle_anim_scripts.s').decode()
 def commands(label,end):
  m=re.search(r'^'+label+r'\s*:\n',source,re.M)
  if not m:raise ValueError('Missing '+label)
  result=[]
  line_base=source[:m.end()].count('\n')
  for offset,line in enumerate(source[m.end():].splitlines()):
   row=line.split('@',1)[0].strip()
   if not row:continue
   if row.endswith(':'):raise ValueError('Unexpected label')
   op,*tail=row.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in hit.OPS|{'setpan','call','return','splitbgprio_all','splitbgprio','panse','waitplaysewithpan','splitbgprio_foes','createsoundtask','waitsound','setarg','panse_adjustnone','fadetobg','waitbgfadein','waitbgfadeout','restorebg','visible','invisible','monbg_static','clearmonbg_static','jumpifcontest'}:raise ValueError('Unsupported command '+op)
   result.append(dict(op=op,args=args,line=line_base+offset+1))
   if op==end:return fold_firered_contest_branches(reference,source,result,{})[0]
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
  if not any(c['op']=='createsprite' and c['args'][0] in TEMPLATES or c['op']=='createvisualtask' and c['args'][0] in {'AnimTask_Rollout','AnimTask_ImprisonOrbs','AnimTask_TormentAttacker','AnimTask_ElectricBolt','AnimTask_SetGrayscaleOrOriginalPal','AnimTask_Hail','AnimTask_CreateRaindrops'}|OVERLAY_TASKS|VISIBILITY_TASKS|BATTLER_COPY_TASKS|EXPULSION_TASKS|ADDITIONAL_MON_TASKS|MATERIAL_CHANGE_TASKS|SQUASH_TASKS|MEMENTO_TASKS|{'AnimTask_WaterSport'} for c in flat):continue
  if any(c['op']=='createsoundtask' and not supported_sound_task(c['args'],songs) or c['op']=='createsprite' and c['args'][0] not in sprites or c['op']=='createvisualtask' and c['args'][0] not in tasks or c['op']=='loadspritegfx' and c['args'][0] not in gfx or c['op'] in {'playsewithpan','loopsewithpan','panse','panse_adjustnone','waitplaysewithpan'} and c['args'][0] not in sounds for c in flat):continue
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_TormentAttacker' for c in flat) and 'gThoughtBubbleSpriteTemplate' not in sprites:continue
  if any(c['op']=='fadetobg' and c['args'] not in (['BG_AURORA'],['BG_GHOST'],['BG_PSYCHIC']) for c in flat):continue
  # Keep generic Psychic background roots under their existing producer. This
  # producer owns complete programs that require its Teleport or new material callbacks.
  if any(c['op']=='fadetobg' and c['args']==['BG_PSYCHIC'] for c in flat) and not any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_Teleport' or c['op']=='createsprite' and c['args'][0] in {'gBentSpoonSpriteTemplate','gLusterPurgeCircleSpriteTemplate'} for c in flat):continue
  # Approved safe punch initialization changes colors/position only; its ABI is unchanged.
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_AttackerPunchWithTrace' and (len(c['args'])!=4 or not 0<=int(c['args'][3],0)<=16) for c in flat):continue
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_SpiteTargetShadow' and (len(c['args'])!=2 or not 0<=int(c['args'][1],0)<=255) for c in flat):continue
  if any(c['op']=='createvisualtask' and c['args'][0] in MEMENTO_TASKS and (len(c['args'])!=2 or int(c['args'][1],0)!=(2 if c['args'][0] in {'AnimTask_InitMementoShadow','AnimTask_MementoHandleBg'} else 5)) for c in flat):continue
  # These callbacks consume exactly the authored argument widths.
  volt_widths={'gBatonPassPokeballSpriteTemplate':3,'gVoltTackleOrbSlideSpriteTemplate':3,'AnimTask_VoltTackleBolt':3,'AnimTask_VoltTackleAttackerReappear':2}
  if any(c['op'] in {'createsprite','createvisualtask'} and c['args'][0] in volt_widths and len(c['args'])!=volt_widths[c['args'][0]] for c in flat):continue
  # Approved safe policy: a zero Spark flash divisor disables blinking only.
  # Keep the authored argument and all source orbit/lifetime commands unchanged.
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=root,soundEffects=sound_effects(reference,flat),spriteTags={name:tags[name] for name in sorted({c['args'][0] for c in flat if c['op']=='loadspritegfx'})}));subs.update(local)
 for row in scripts:
  flat=row['commands']+[c for command in row['commands'] if command['op']=='call' for c in subs[command['args'][0]]]
  electric_tasks={c['args'][0] for c in flat if c['op']=='createvisualtask'}&{'AnimTask_ElectricChargingParticles','AnimTask_ShockWaveProgressingBolt','AnimTask_ShockWaveLightning','AnimTask_VoltTackleBolt'}
  if electric_tasks:
   electric=read(reference,'src/battle_anim_electric.c').decode();children=[]
   for task,callback,template in [('AnimTask_ElectricChargingParticles','AnimTask_ElectricChargingParticles_Step','gElectricChargingParticlesSpriteTemplate'),('AnimTask_ShockWaveProgressingBolt','CreateShockWaveBoltSprite','sShockWaveProgressingBoltSpriteTemplate'),('AnimTask_ShockWaveLightning','CreateShockWaveLightningSprite','gLightningSpriteTemplate'),('AnimTask_VoltTackleBolt','CreateVoltTackleBolt','gVoltTackleBoltSpriteTemplate')]:
    if task not in electric_tasks:continue
    body=re.search(r'(?:static )?(?:void|bool8) '+callback+r'\([^;]*?\)\s*\{(.*?)^\}',electric,re.S|re.M)
    if body is None or re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',body[1])!=[template]:raise ValueError('Electric raw sprite dependency differs: '+task)
    children.append(template)
   row['spriteDependencies']=children
   if 'AnimTask_ShockWaveProgressingBolt' in electric_tasks:
    body=re.search(r'static bool8 CreateShockWaveBoltSprite\([^;]*?\)\s*\{(.*?)^\}',electric,re.S|re.M)
    if re.findall(r'PlaySE12WithPanning\((SE_\w+),',body[1])!=['SE_M_THUNDERBOLT']:raise ValueError('Shock Wave sound dependency differs')
    row['soundEffects'].update(sound_effects(reference,[dict(op='playse',args=['SE_M_THUNDERBOLT'])]))
  if any(c['op']=='createsprite' and c['args'][0]=='gWillOWispOrbSpriteTemplate' for c in flat):
   fire=read(reference,'src/battle_anim_fire.c').decode();body=re.search(r'static void AnimWillOWispOrb_Step\([^;]*?\)\s*\{(.*?)^\}',fire,re.S|re.M)
   if body is None or re.findall(r'PlaySE12WithPanning\((SE_\w+), gAnimCustomPanning\);',body[1])!=['SE_M_FLAME_WHEEL']:raise ValueError('Wisp callback sound differs')
   row['soundEffects'].update(sound_effects(reference,[dict(op='playse',args=['SE_M_FLAME_WHEEL'])]))
  if any(c['op']=='createsprite' and c['args'][0]=='gClappingHandSpriteTemplate' for c in flat):
   effects=read(reference,'src/battle_anim_effects_3.c').decode();body=re.search(r'static void AnimClappingHand_Step\(struct Sprite \*sprite\)\s*\{(.*?)^\}',effects,re.S|re.M)
   if body is None or re.findall(r'PlaySE1WithPanning\((SE_\w+), BattleAnimAdjustPanning\(SOUND_PAN_ATTACKER\)\);',body[1])!=['SE_M_ENCORE']:raise ValueError('Encore callback sound dependency differs')
   row['soundEffects'].update(sound_effects(reference,[dict(op='playsewithpan',args=['SE_M_ENCORE','SOUND_PAN_ATTACKER'])]))
  smokescreen_dependencies(reference,row,flat)
  material_change_contracts(reference,row,flat)
  rollout_dependencies(reference,row,flat)
  water_sport=water_sport_dependencies(reference,flat)
  if water_sport:row['spriteDependencies']=list(dict.fromkeys(row.get('spriteDependencies',[])+water_sport))
  squash=squash_dependencies(reference,flat)
  if squash:row['spriteDependencies']=list(dict.fromkeys(row.get('spriteDependencies',[])+squash))
  visibility_contracts(reference,flat)
  expulsion_contracts(reference,flat)
  feather_contracts(reference,flat)
  palette_backup_contracts(reference,flat)
  if any(c['op']=='createvisualtask' and c['args'][0] in {'AnimTask_DoubleTeam','AnimTask_AttackerPunchWithTrace','AnimTask_SpiteTargetShadow'} for c in flat):
   row['spriteTags']['ANIM_TAG_BENT_SPOON']=tags['ANIM_TAG_BENT_SPOON']
  overlay_tasks={c['args'][0] for c in flat if c['op']=='createvisualtask'} & OVERLAY_TASKS
  if any(c['op']=='createsprite' and c['args'][0]=='gFastFlyingMusicNotesSpriteTemplate' for c in flat):
   # The authored Teeter leaf reads static coordinates, not the task-mutated X.
   effects=read(reference,'src/battle_anim_effects_1.c').decode()
   body=re.search(r'static void AnimFlyingMusicNotes\(struct Sprite\* sprite\)\s*\{(.*?)^\}',effects,re.S|re.M)
   if body is None or re.findall(r'GetBattlerSpriteCoord\(gBattleAnimAttacker, (\w+)\)',body[1])!=['BATTLER_COORD_X_2','BATTLER_COORD_Y_PIC_OFFSET'] or 'gSprites[' in body[1]:raise ValueError('Teeter static-coordinate leaf differs')
   row['materialDependencies']=['duck']

  if any(c['op']=='loadspritegfx' and c['args']==['ANIM_TAG_WHITE_FEATHER'] for c in flat):
   row['materialDependencies']=['white-feather']
  if overlay_tasks:
   row['overlayDependencies']=sorted({name for task in overlay_tasks for name in OVERLAY_MATERIALS[task]})
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_TormentAttacker' for c in flat):
   effects=read(reference,'src/battle_anim_effects_3.c').decode()
   child=re.search(r'static void TormentAttacker_Step\(u8 taskId\)\s*\{(.*?)^\}',effects,re.S|re.M)
   if child is None or re.findall(r'CreateSprite\(&([A-Za-z0-9_]+),',child[1])!=['gThoughtBubbleSpriteTemplate']:raise ValueError('Torment raw child dependency differs')
   if re.findall(r'PlaySE12WithPanning\((SE_\w+), BattleAnimAdjustPanning\(SOUND_PAN_ATTACKER\)\);',child[1])!=['SE_M_METRONOME']:raise ValueError('Torment callback sound differs')
   if re.findall(r'gSprites\[spriteId\]\.callback\s*=\s*(\w+)\s*;',child[1])!=['SpriteCallbackDummy'] or re.findall(r'gSprites\[i\]\.callback\s*=\s*(\w+)\s*;',child[1])!=['TormentAttacker_Callback']:raise ValueError('Torment raw child callback handoff differs')
   if 'StartSpriteAnim(&gSprites[i], 2);' not in child[1] or 'task->data[6] = j;' not in child[1]:raise ValueError('Torment child completion counter differs')
   row['spriteDependencies']=['gThoughtBubbleSpriteTemplate']
   row['soundEffects'].update(sound_effects(reference,[dict(op='playsewithpan',args=['SE_M_METRONOME','SOUND_PAN_ATTACKER'])]))
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
  if any(c['op']=='createsprite' and c['args'][0]=='gConfuseRayBallBounceSpriteTemplate' for c in flat):
   ghost=read(reference,'src/battle_anim_ghost.c').decode()
   bodies={}
   for name,next_callback in [('AnimConfuseRayBallBounce','AnimConfuseRayBallBounce_Step1'),('AnimConfuseRayBallBounce_Step1','AnimConfuseRayBallBounce_Step2'),('AnimConfuseRayBallBounce_Step2','DestroyAnimSpriteAndDisableBlend')]:
    step=re.search(r'static void '+name+r'\(struct Sprite \*sprite\)\s*\{(.*?)^\}',ghost,re.S|re.M)
    if step is None:raise ValueError('Confuse Ray callback missing')
    bodies[name]=step[1]
    if re.findall(r'sprite->callback\s*=\s*(\w+)\s*;',step[1])!=[next_callback]:raise ValueError('Confuse Ray callback handoff differs')
   for name,api,args in [('AnimConfuseRayBallBounce_Step1','PlaySE12WithPanning','SE_M_CONFUSE_RAY,gAnimCustomPanning'),('AnimConfuseRayBallBounce_Step2','PlaySE','SE_M_CONFUSE_RAY')]:
    calls=[(call,re.sub(r'\s+','',args)) for call,args in re.findall(r'\b(PlaySE(?:12WithPanning)?)\(([^;]*)\);',bodies[name])]
    if calls!=[(api,args)]:raise ValueError('Confuse Ray callback sounds differ')
   row['soundEffects'].update(sound_effects(reference,[dict(op='playse',args=['SE_M_CONFUSE_RAY'])]))
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
 return scripts,subs

def build(reference):
 scripts,subs=discover_scripts(reference)
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
 for asset,png,shape,pal in [('finger','finger',(32,32),None),('finger-2','finger_2',(32,96),'finger'),('devil','devil',(32,64),None),('angel','angel',(32,32),None),('magnifying-glass','magnifying_glass',(32,32),None),('pink-heart','magenta_heart',(16,16),'pink_heart'),('red-heart','magenta_heart',(16,16),'red_heart'),('guard-ring','guard_ring',(64,32),None),('fire','fire',(32,256),None),('duck','duck',(16,48),None),('tendrils','tendrils',(64,128),None),('slam-hit-2','slam_hit_2',(64,128),None),('green-spike','green_spike',(16,16),None),('roots','roots',(32,128),None),('air-wave','air_wave',(32,16),None),('hail','hail',(16,16),None),('breaking-egg','breaking_egg',(32,96),None),('milk-bottle','milk_bottle',(32,32),None),('ice-spikes','ice_spikes',(8,64),'ice_crystals'),('blue-ring','gold_ring',(16,32),'blue_ring'),('blue-ring-2','gold_ring',(16,32),'blue_ring_2'),('explosion-6','explosion_6',(32,128),None),('yellow-star','yellow_star',(32,32),None),('magenta-heart','magenta_heart',(16,16),None),('fire-plume','fire_plume',(32,160),'fire'),('focus-energy','focus_energy',(16,128),None),('sharp-teeth','sharp_teeth',(64,64),None),('clamp','clamp',(64,64),'sharp_teeth'),('toxic-bubble','toxic_bubble',(16,128),'poison_bubble'),('poison-bubble','poison_bubble',(16,48),None),('needle','needle',(16,16),None),('bubble','bubble',(16,48),'rain_drops'),('gold-ring','gold_ring',(16,32),None),('purple-ring','gold_ring',(16,32),'purple_ring'),('metal-sound-waves','metal_sound_waves',(32,64),None),('blue-star','blue_star',(32,224),None),('water-orb','water_orb',(16,64),'water_impact'),('brown-orb','water_orb',(16,64),'brown_orb'),('glowy-red-orb','glowy_red_orb',(8,8),None),('glowy-green-orb','glowy_red_orb',(8,8),'glowy_green_orb'),('black-ball','black_ball',(8,8),None),('gray-smoke','gray_smoke',(32,160),None),('water-impact','water_impact',(32,32),None),('spark-thin','spark_0',(8,64),'spark'),('spark-wide','spark_1',(16,64),'spark'),('spark-2','spark_2',(16,48),None),('spark-h','spark_h',(32,32),'spark'),('black-ball-2','black_ball_2',(16,16),None),('shock-3','shock_3',(32,96),None),('large-fresh-egg','large_fresh_egg',(16,16),None),('explosion','explosion',(32,128),None),('lightning','lightning',(32,160),'lightning_2')]:
  if pal in ('pokeball','wisp_orb','electric_orbs','water_impact','lightning_2','poison_bubble','rain_drops','sharp_teeth','fire','finger'):
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
 palette_colors_white=palette('white_circle_of_light')
 palette_colors_blue=palette('glowy_blue_orb')
 registrations=read(reference,'src/data/battle_anim.h').decode();graphics=read(reference,'src/graphics.c').decode()
 # Raw squash children use their registered native sheets and palettes.
 for tag,gfx,pal,size,png,palette_path in [('ANIM_TAG_WARM_ROCK','WarmRock','WarmRock',0x0a80,'warm_rock','warm_rock'),('ANIM_TAG_GLOWY_BLUE_ORB','GlowyRedOrb','GlowyBlueOrb',0x0020,'glowy_red_orb','glowy_blue_orb')]:
  if not re.search(r'\{gBattleAnimSpriteGfx_'+gfx+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal+r',\s*'+tag+r'\}',registrations):raise ValueError('Squash source registration differs: '+tag)
  for kind,symbol,stem,suffix in [('Gfx',gfx,png,'4bpp'),('Pal',pal,palette_path,'gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Squash source material path differs: '+tag)
 # Taunt's Finger2 graphics deliberately use the shared Finger palette.
 for tag,gfx,size in [('TAG_HAND','TagHand',0x400),('FINGER','Finger',0x200),('FINGER_2','Finger2',0x600)]:
  if not re.search(r'\{gBattleAnimSpriteGfx_'+gfx+r',\s*0x'+format(size,'04x')+r',\s*ANIM_TAG_'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_Finger,\s*ANIM_TAG_'+tag+r'\}',registrations):raise ValueError('Social finger material registration differs')
  if not re.search(r'gBattleAnimSpriteGfx_'+gfx+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/'+tag.lower()+r'\.4bpp\.lz"\)',graphics):raise ValueError('Social finger material path differs')
 if not re.search(r'gBattleAnimSpritePal_Finger\[\] = INCBIN_U32\("graphics/battle_anims/sprites/finger\.gbapal\.lz"\)',graphics):raise ValueError('Social finger palette path differs')
 # Hearts use the MagentaHeart pixels with their own registered palettes.
 for asset,gfx,pal,stem,size in [('YELLOW_BALL','YellowBall','YellowBall','yellow_ball',0x80),('DEVIL','Devil','Devil','devil',0x400),('ANGEL','Angel','Angel','angel',0x200),('MAGNIFYING_GLASS','MagnifyingGlass','MagnifyingGlass','magnifying_glass',0x200),('PINK_HEART','MagentaHeart','PinkHeart','magenta_heart',0x80),('RED_HEART','MagentaHeart','RedHeart','magenta_heart',0x80)]:
  tag='ANIM_TAG_'+asset
  if not re.search(r'\{gBattleAnimSpriteGfx_'+gfx+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal+r',\s*'+tag+r'\}',registrations):raise ValueError('Confusion/kiss material registration differs: '+asset)
  for kind,symbol,path,suffix in [('Gfx',gfx,stem,'4bpp'),('Pal',pal,asset.lower(),'gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+path+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Confusion/kiss source material path differs: '+asset)
 if not re.search(r'\{gBattleAnimSpriteGfx_NoiseLine,\s*0x0800,\s*ANIM_TAG_NOISE_LINE\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_HitDuplicate,\s*ANIM_TAG_NOISE_LINE\}',registrations):raise ValueError('Source noise line registration differs')
 for symbol,path,suffix in [('Gfx_NoiseLine','noise_line','4bpp'),('Pal_HitDuplicate','hit_duplicate','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+path+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Source noise line material differs')
 for stem,symbol,size in [('guard_ring','GuardRing',0x0400),('duck','Duck',0x0180),('tendrils','Tendrils',0x1000),('slam_hit_2','SlamHit2',0x1000),('green_spike','GreenSpike',0x0080),('roots','Roots',0x0800)]:
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
 # Baton Pass owns the same indexed palette source already used by Spotlight.
 if not re.search(r'\{gBattleAnimSpriteGfx_Pokeball,\s*0x0080,\s*ANIM_TAG_POKEBALL\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_Pokeball,\s*ANIM_TAG_POKEBALL\}',registrations):raise ValueError('Baton Pass source material registration differs')
 if not re.search(r'gBattleAnimSpriteGfx_Pokeball\[\] = INCBIN_U32\("graphics/battle_anims/sprites/pokeball\.4bpp\.lz"\)',graphics):raise ValueError('Baton Pass source material path differs')
 # Spotlight is an OBJ-window mask with the source Pokeball palette.
 if not re.search(r'\{gBattleAnimSpriteGfx_Spotlight,\s*0x0800,\s*ANIM_TAG_SPOTLIGHT\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_Pokeball,\s*ANIM_TAG_SPOTLIGHT\}',registrations):raise ValueError('Spotlight source palette registration differs')
 for kind,symbol,stem,suffix in [('Gfx','Spotlight','spotlight','4bpp'),('Pal','Pokeball','pokeball','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Spotlight source material path differs')
 for stem,symbol,size in [('confetti','Confetti',0x0180),('paw_print','PawPrint',0x0200),('pink_petal','PinkPetal',0x0080),('ice_chunk','IceChunk',0x0a00),('small_ember','SmallEmber',0x0a00),('hollow_orb','HollowOrb',0x0080),('music_notes','MusicNotes',0x0300),('letter_z','LetterZ',0x0200),('green_star','GreenStar',0x0200),('moon','Moon',0x0800),('green_sparkle','GreenSparkle',0x0200),('rainbow_rings','RainbowRings',0x00c0),('flying_dirt','FlyingDirt',0x0200),('rain_drops','RainDrops',0x0700),('sunlight','Sunlight',0x0200),('small_red_eye','SmallRedEye',0x0020),('white_feather','WhiteFeather',0x0400),('leer','Leer',0xa00),('thought_bubble','ThoughtBubble',0x800),('small_bubbles','SmallBubbles',0x140),('orbs','Orbs',0x180),('amnesia','Amnesia',0x1000),('sword','Sword',0x400),('sphere_to_cube','SphereToCube',0xa00)]:
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
 for stem,symbol,size,palette in [('electric_orbs','ElectricOrbs',0x0080,'ElectricOrbs'),('electricity','Electricity',0x0800,'ElectricOrbs'),('wisp_orb','WispOrb',0x0380,'WispOrb'),('wisp_fire','WispFire',0x0800,'WispOrb')]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+palette+r',\s*'+tag+r'\}',registrations):raise ValueError('Electric/Wisp source registration differs: '+stem)
  for kind,name,path,suffix in [('Gfx',symbol,stem,'4bpp'),('Pal',palette,'electric_orbs' if palette=='ElectricOrbs' else 'wisp_orb','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+name+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+path+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Electric/Wisp graphics differs: '+stem)
 for stem,size in [('pokeball',(16,16)),('smokescreen_impact',(16,48)),('black_smoke',(32,16)),('warm_rock',(32,168)),('glowy_blue_orb',(8,8)),('spotlight',(64,64)),('confetti',(8,96)),('paw_print',(32,32)),('pink_petal',(8,32)),('electric_orbs',(8,32)),('electricity',(32,128)),('wisp_orb',(16,112)),('wisp_fire',(32,128)),('ice_chunk',(32,160)),('alert',(32,112)),('bent_spoon',(16,192)),('spiral',(64,64)),('white_circle_of_light',(64,64)),('small_ember',(32,160)),('tag_hand',(32,64)),('hollow_orb',(16,16)),('yellow_ball',(16,16)),('letter_z',(32,32)),('green_star',(16,64)),('moon',(64,64)),('green_sparkle',(16,64)),('rainbow_rings',(8,48)),('leaf',(16,144)),('razor_leaf',(32,16)),('flying_dirt',(32,32)),('rain_drops',(16,224)),('sunlight',(32,32)),('small_red_eye',(8,8)),('white_feather',(32,64)),('leer',(32,160)),('circle_of_light',(64,64)),('thought_bubble',(32,128)),('small_bubbles',(16,40)),('orbs',(16,48)),('noise_line',(32,128)),('amnesia',(32,256)),('music_notes',(16,96)),('purple_hand_outline',(32,32)),('conversion',(8,32)),('sword',(32,64)),('sphere_to_cube',(32,160)),('web_thread',(8,8)),('string',(64,32)),('spider_web',(64,64)),('eye',(64,64)),('x_sign',(64,64))]:
  png_stem='circle_of_light' if stem=='white_circle_of_light' else 'glowy_red_orb' if stem=='glowy_blue_orb' else stem
  with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/'+png_stem+'.png'))) as im:
   if im.mode!='P' or im.size!=size:raise ValueError('Invalid source indexed image')
   indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()];palette=im.getpalette()[:48]
  if stem=='white_circle_of_light':palette=palette_colors_white
  if stem=='glowy_blue_orb':palette=palette_colors_blue
  if stem=='spotlight':
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/pokeball.png'))) as shared:palette=shared.getpalette()[:48]
  if stem=='tag_hand':
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/finger.png'))) as shared:palette=shared.getpalette()[:48]
  if stem=='wisp_fire':
   with Image.open(io.BytesIO(read(reference,'graphics/battle_anims/sprites/wisp_orb.png'))) as shared:palette=shared.getpalette()[:48]
  if stem in ('circle_of_light','electricity'):
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
  indexed.append(dict(id=name,width=size[0],height=size[1],indicesPath=base+'.4bpp',palettePath=base+'.pal16',paletteTag='TAG_SMOKESCREEN' if stem=='smokescreen_impact' else 'ANIM_TAG_'+stem.upper()))
 background_names={c['args'][0] for row in scripts for c in row['commands']+[c for command in row['commands'] if command['op']=='call' for c in subs[command['args'][0]]] if c['op'] in {'fadetobg','changebg'}}
 definitions={name:int(value) for name,value in re.findall(r'^#define (BG_\w+)\s+(\d+)\b',read(reference,'include/constants/battle_anim.h').decode(),re.M)}
 if not background_names<=definitions.keys():raise ValueError('Missing source elemental background symbol')
 return (json.dumps(dict(schemaVersion=1,scripts=scripts,subroutines=subs,backgroundSymbols={name:definitions[name] for name in sorted(background_names)},taskAffinePrograms={name:task_affine_program(all_source,name) for name in ['sDeepInhaleAffineAnimCmds','sAffineAnims_Torment']+(['sAffineAnim_Teleport'] if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_Teleport' for row in scripts for c in row['commands']) else [])},spriteRecipes=recipes,assets=assets,indexedAssets=indexed),indent=2)+'\n').encode(),pixels
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/elemental.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,pixels=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/('elemental-'+id if id.endswith(('.4bpp','.pal16')) else 'elemental-'+id+'.rgb555'),blob) for id,blob in pixels.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Elemental output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[s['moveId'] for s in json.loads(data)['scripts']])))
