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
spec=importlib.util.spec_from_file_location('source_animation_frames',ROOT/'scripts/lib/source_animation_frames.py');frames_module=importlib.util.module_from_spec(spec);spec.loader.exec_module(frames_module)
frame_program=frames_module.frame_program
hit=module('physical_hit_import',ROOT/'scripts/imports/battle/import-source-hit-animations.py')
elemental=module('physical_elemental_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py')
def read_sources(reference):
 # Only actual conversion inputs. The source revision comes from the shared importer.
 paths=('graphics/battle_anims/sprites/weather_ball.png','src/battle_anim_ice.c','include/constants/battle.h','src/battle_anim.c','graphics/battle_anims/sprites/meteor.png','graphics/battle_anims/sprites/flat_rock.png','graphics/battle_anims/sprites/pain_split.png','graphics/battle_anims/sprites/purple_scratch.png','graphics/battle_anims/sprites/purple_swipe.png','graphics/battle_anims/sprites/rapid_spin.png','graphics/battle_anims/sprites/ice_cube.pal','graphics/battle_anims/sprites/ice_cube_0.png','graphics/battle_anims/sprites/ice_cube_1.png','graphics/battle_anims/sprites/ice_cube_2.png','graphics/battle_anims/sprites/ice_cube_3.png','src/battle_anim_water.c','src/battle_anim_dragon.c','graphics/battle_anims/sprites/pencil.png','graphics/battle_anims/sprites/dirt_mound.png','graphics/battle_anims/sprites/white_shadow.png','graphics/battle_anims/sprites/nail.png','graphics/battle_anims/sprites/ghostly_spirit.png','graphics/battle_anims/sprites/item_bag.png','graphics/battle_anims/sprites/speed_dust.png','graphics/battle_anims/sprites/recycle.png','graphics/battle_anims/sprites/sparkle_6.png','graphics/battle_anims/sprites/lock_on.png','graphics/battle_anims/sprites/teal_alert.png','graphics/battle_anims/sprites/opening_eye.png','graphics/battle_anims/sprites/round_white_halo.png','graphics/battle_anims/sprites/snore_z.png','graphics/battle_anims/sprites/jagged_music_note.png','graphics/battle_anims/sprites/pink_cloud.png','graphics/battle_anims/sprites/bluegreen_orb.png','graphics/battle_anims/sprites/spikes.png','graphics/battle_anims/sprites/purple_flame.png','graphics/battle_anims/sprites/bird.png','graphics/battle_anims/sprites/red_orb_2.pal','src/battle_anim_utility_funcs.c','graphics_file_rules.mk','graphics/battle_anims/sprites/bell.png','graphics/battle_anims/sprites/music_notes_2.png','graphics/battle_anims/sprites/music_notes_2.pal','graphics/battle_anims/sprites/flower_0.png','graphics/battle_anims/sprites/flower_1.png','graphics/battle_anims/sprites/flower.pal','src/battle_anim_mon_movement.c','src/battle_anim_status_effects.c','src/battle_anim_psychic.c','graphics/battle_anims/sprites/sparkle_4.png','graphics/battle_anims/sprites/sparkle_3.png','graphics/battle_anims/sprites/x_sign.png','graphics/battle_anims/sprites/hollow_orb.png','asm/macros/battle_anim_script.inc','src/battle_anim_ghost.c','src/battle_anim_mons.c','graphics/battle_anims/sprites/lick.png','graphics/battle_anims/sprites/movement_waves.png','data/battle_scripts_1.s','data/battle_anim_scripts.s','src/data/battle_moves.h','include/constants/moves.h','include/constants/battle_anim.h','src/data/battle_anim.h','src/battle_anim_fight.c','src/battle_anim_bug.c','src/battle_anim_normal.c','src/battle_anim_poison.c','src/battle_anim_dark.c','src/battle_anim_rock.c','src/battle_anim_fire.c',
        'src/graphics.c','src/battle_anim_flying.c','src/battle_anim_effects_1.c','src/battle_anim_effects_2.c','src/battle_anim_ground.c','src/battle_anim_effects_3.c',
        'graphics/battle_anims/sprites/round_shadow.png','graphics/battle_anims/sprites/tri_attack_triangle.png','graphics/battle_anims/sprites/sweat_drop.png','graphics/battle_anims/sprites/tag_hand.png','graphics/battle_anims/sprites/finger.png','graphics/battle_anims/sprites/smellingsalt_effect.png','graphics/battle_anims/sprites/red_orb.png','graphics/battle_anims/sprites/sweat_bead.png','graphics/battle_anims/sprites/splash.png','graphics/battle_anims/sprites/coin.png','graphics/battle_anims/sprites/cross_impact.png','graphics/battle_anims/sprites/red_ball.png','graphics/battle_anims/sprites/whirlwind_lines.png','graphics/battle_anims/sprites/gold_stars.png','graphics/battle_anims/sprites/swipe.png','graphics/battle_anims/sprites/fang_attack.png','graphics/battle_anims/sprites/seed.png','graphics/battle_anims/sprites/icicle_spear.png','graphics/battle_anims/sprites/claw_slash.png','graphics/battle_anims/sprites/torn_metal.png','graphics/battle_anims/sprites/blue_light_wall.pal','graphics/battle_anims/sprites/bone.png','graphics/battle_anims/sprites/eye_sparkle.png','graphics/battle_anims/sprites/anger.png','graphics/battle_anims/sprites/breath.png','graphics/battle_anims/sprites/eclipsing_orb.png',
        'graphics/battle_anims/sprites/mud_sand_0.png','graphics/battle_anims/sprites/mud_sand_1.png','graphics/battle_anims/sprites/mud_sand.pal',
        'graphics/battle_anims/sprites/slam_hit.png','graphics/battle_anims/sprites/whip_hit.pal','graphics/battle_anims/sprites/poison_powder.png',
        'graphics/battle_anims/sprites/sleep_powder.pal','graphics/battle_anims/sprites/stun_spore.pal','graphics/battle_anims/sprites/protect.png','graphics/battle_anims/sprites/shadow_ball.png','graphics/battle_anims/sprites/gray_orb.png','graphics/battle_anims/sprites/blue_orb.pal')
 return {path:(subprocess.check_output(['git','-C',str(reference),'show',hit.COMMIT+':'+path])
               if (reference/'.git').exists() else (reference/path).read_bytes()) for path in paths}
SPRITES={'gFistFootRandomPosSpriteTemplate','gWishStarSpriteTemplate','gGrantingStarsSpriteTemplate','gMeteorMashStarSpriteTemplate','gArmThrustHandSpriteTemplate','gFurySwipesSpriteTemplate','gFangSpriteTemplate','gSuperFangSpriteTemplate','gBulletSeedSpriteTemplate','gSandAttackDirtSpriteTemplate','gMudSlapMudSpriteTemplate','gMegaPunchKickSpriteTemplate','gSpinningHandOrFootSpriteTemplate','gFireSpreadSpriteTemplate','gPinMissileSpriteTemplate','gIcicleSpearSpriteTemplate','gLinearStingerSpriteTemplate','gHandleInvertHitSplatSpriteTemplate','gPoisonBubbleSpriteTemplate','gClawSlashSpriteTemplate','gFireSpinSpriteTemplate','gShakeMonOrTerrainSpriteTemplate','gSpinningBoneSpriteTemplate','gFistFootSpriteTemplate','gEyeSparkleSpriteTemplate','gAngerMarkSpriteTemplate','gEclipsingOrbSpriteTemplate','gBreathPuffSpriteTemplate','gHealingBlueStarSpriteTemplate','gMudsportMudSpriteTemplate','gKarateChopSpriteTemplate','gPoisonPowderParticleSpriteTemplate','gStunSporeParticleSpriteTemplate','gSleepPowderParticleSpriteTemplate','gBowMonSpriteTemplate','gFlashingHitSplatSpriteTemplate','gSimplePaletteBlendSpriteTemplate','gSlamHitSpriteTemplate','gVineWhipSpriteTemplate','gViceGripSpriteTemplate','gBasicHitSplatSpriteTemplate','gHorizontalLungeSpriteTemplate','gSlideMonToOffsetSpriteTemplate','gSlideMonToOriginalPosSpriteTemplate','gComplexPaletteBlendSpriteTemplate','gVerticalDipSpriteTemplate','gStompFootSpriteTemplate','gJumpKickSpriteTemplate','gSlidingKickSpriteTemplate'}
TASKS={'AnimTask_FrozenIceCube','AnimTask_CreateSurfWave','AnimTask_MeditateStretchAttacker','AnimTask_BlendMonInAndOut','AnimTask_BlendColorCycle','AnimTask_IsContest','AnimTask_IsTargetPlayerSide','AnimTask_MetallicShine','AnimTask_BlendBattleAnimPal','SoundTask_PlaySE1WithPanning','AnimTask_RockMonBackAndForth','AnimTask_DefenseCurlDeformMon','AnimTask_SetGrayscaleOrOriginalPal','AnimTask_GrowAndShrink','AnimTask_SlackOffSquish','AnimTask_Splash','AnimTask_ScaleMonAndRestore','AnimTask_SwayMon','AnimTask_TranslateMonElliptical','AnimTask_RotateMonToSideAndRestore','AnimTask_ShakeMon','AnimTask_ShakeMon2','AnimTask_TranslateMonEllipticalRespectSide','AnimTask_WindUpLunge','AnimTask_ShakeAndSinkMon','AnimTask_ShakeMonInPlace','AnimTask_RotateMonSpriteToSide'}
SPRITES|={'gFocusPunchFistSpriteTemplate'}
# Ice Ball keeps its bounded source graph here; recipes/material remain elemental.
SPRITES|={'gIceBallChunkSpriteTemplate','gIceBallImpactShardSpriteTemplate'}
TASKS|={'AnimTask_GetRolloutCounter','AnimTask_GetBattleTerrain'}
TASKS|={'GetIsDoomDesireHitTurn','AnimTask_FakeOut'}
TASKS|={'AnimTask_HorizontalShake','AnimTask_ShakeTargetBasedOnMovePowerOrDmg','AnimTask_IsPowerOver99'}
NEW= {'gFistFootRandomPosSpriteTemplate','gWishStarSpriteTemplate','gMeteorMashStarSpriteTemplate','gArmThrustHandSpriteTemplate','gFurySwipesSpriteTemplate','gFangSpriteTemplate','gSuperFangSpriteTemplate','gBulletSeedSpriteTemplate','gSandAttackDirtSpriteTemplate','gMudSlapMudSpriteTemplate','gMegaPunchKickSpriteTemplate','gSpinningHandOrFootSpriteTemplate','gPinMissileSpriteTemplate','gIcicleSpearSpriteTemplate','gLinearStingerSpriteTemplate','gHandleInvertHitSplatSpriteTemplate','gClawSlashSpriteTemplate','gSpinningBoneSpriteTemplate','jumpifmoveturn','choosetwoturnanim','gEyeSparkleSpriteTemplate','gAngerMarkSpriteTemplate','AnimTask_DefenseCurlDeformMon','AnimTask_GrowAndShrink','AnimTask_SlackOffSquish','AnimTask_Splash','gMudsportMudSpriteTemplate','goto','gKarateChopSpriteTemplate','AnimTask_TranslateMonElliptical','gPoisonPowderParticleSpriteTemplate','gStunSporeParticleSpriteTemplate','gSleepPowderParticleSpriteTemplate','gBowMonSpriteTemplate','gFlashingHitSplatSpriteTemplate','gSlamHitSpriteTemplate','gVineWhipSpriteTemplate','gViceGripSpriteTemplate','gVerticalDipSpriteTemplate','gStompFootSpriteTemplate','gJumpKickSpriteTemplate','gSlidingKickSpriteTemplate','AnimTask_WindUpLunge','AnimTask_ShakeAndSinkMon','AnimTask_ShakeMonInPlace','AnimTask_RotateMonSpriteToSide'}
SPRITES|={'gCoinThrowSpriteTemplate','gFallingCoinSpriteTemplate','gCrossChopHandSpriteTemplate','gCrossImpactSpriteTemplate'}
TASKS|={'AnimTask_DrillPeckHitSplats','AnimTask_BarrageBall'}
NEW|={'gCoinThrowSpriteTemplate','gFallingCoinSpriteTemplate','gCrossChopHandSpriteTemplate','gCrossImpactSpriteTemplate','AnimTask_DrillPeckHitSplats','AnimTask_BarrageBall'}
NEW|={'gIceBallChunkSpriteTemplate','gIceBallImpactShardSpriteTemplate','AnimTask_GetRolloutCounter'}
NEW|={'AnimTask_GetBattleTerrain','GetIsDoomDesireHitTurn','gFocusPunchFistSpriteTemplate','AnimTask_FakeOut'}
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
ROUND_SHADOW_RECIPES=[('gFlyBallUpSpriteTemplate','battle_anim_flying.c','AnimFlyBallUp'),('gFlyBallAttackSpriteTemplate','battle_anim_flying.c','AnimFlyBallAttack'),('gBounceBallShrinkSpriteTemplate','battle_anim_flying.c','AnimBounceBallShrink'),('gBounceBallLandSpriteTemplate','battle_anim_flying.c','AnimBounceBallLand'),('gDiveBallSpriteTemplate','battle_anim_flying.c','AnimDiveBall'),('gDiveWaterSplashSpriteTemplate','battle_anim_flying.c','AnimDiveWaterSplash')]
ROUND_SHADOW_WIDTHS={'gFlyBallUpSpriteTemplate':7,'gFlyBallAttackSpriteTemplate':4,'gBounceBallShrinkSpriteTemplate':5,'gBounceBallLandSpriteTemplate':3,'gDiveBallSpriteTemplate':7,'gDiveWaterSplashSpriteTemplate':4}
# Water impact/bubbles keep their existing elemental recipe/material authority.
SPRITES|={r[0] for r in ROUND_SHADOW_RECIPES}|{'gWaterHitSplatSpriteTemplate','gSmallDriftingBubblesSpriteTemplate'}
NEW|={r[0] for r in ROUND_SHADOW_RECIPES}
CHARGED_ROOT_RECIPES=[('gSkyAttackBirdSpriteTemplate','battle_anim_flying.c','AnimSkyAttackBird'),('gSpitUpOrbSpriteTemplate','battle_anim_effects_2.c','AnimSpitUpOrb')]
SPRITES|={r[0] for r in CHARGED_ROOT_RECIPES}
TASKS|={'AnimTask_SpitUpDeformMon','AnimTask_GetTargetIsAttackerPartner','AnimTask_AttackerFadeToInvisible','AnimTask_AttackerFadeFromInvisible','AnimTask_BlendBattleAnimPalExclude','AnimTask_StartSlidingBg'}
NEW|={r[0] for r in CHARGED_ROOT_RECIPES}|{'AnimTask_SpitUpDeformMon'}
LOCKED_PAIR_TASKS={'AnimTask_ThrashMoveMonHorizontal','AnimTask_ThrashMoveMonVertical'}
TASKS|=LOCKED_PAIR_TASKS
NEW|={'AnimTask_CreateSurfWave','AnimTask_FrozenIceCube'}
NEW|=LOCKED_PAIR_TASKS|{'choosetwoturnanim'}
CUT_WALL_RECIPES=[('gTwisterLeafSpriteTemplate','battle_anim_effects_1.c','AnimMoveTwisterParticle'),('gTwisterRockSpriteTemplate','battle_anim_rock.c','AnimMoveTwisterParticle'),('gBrickBreakWallSpriteTemplate','battle_anim_fight.c','AnimBrickBreakWall'),('gBrickBreakWallShardSpriteTemplate','battle_anim_fight.c','AnimBrickBreakWallShard')]
CUT_WALL_TASKS={'AnimTask_IsFuryCutterHitRight','AnimTask_GetFuryCutterHitCount'}
SPRITES|={r[0] for r in CUT_WALL_RECIPES}|{'gCuttingSliceSpriteTemplate'}
TASKS|=CUT_WALL_TASKS
NEW|={r[0] for r in CUT_WALL_RECIPES}|CUT_WALL_TASKS
ORB_DEFENSE_RECIPES=[('gReversalOrbSpriteTemplate','battle_anim_effects_3.c','AnimReversalOrb'),('gProtectSpriteTemplate','battle_anim_effects_1.c','AnimProtect'),('gShadowBallSpriteTemplate','battle_anim_ghost.c','AnimShadowBall'),('gStockpileAbsorptionOrbSpriteTemplate','battle_anim_effects_1.c','AnimPowerAbsorptionOrb'),('gSwallowBlueOrbSpriteTemplate','battle_anim_effects_3.c','AnimSwallowBlueOrb')]
ORB_DEFENSE_TASKS={'AnimTask_StockpileDeformMon','AnimTask_SwallowDeformMon'}
ORB_DEFENSE_WIDTHS={'gReversalOrbSpriteTemplate':5,'gProtectSpriteTemplate':6,'gShadowBallSpriteTemplate':6,'gStockpileAbsorptionOrbSpriteTemplate':6,'gSwallowBlueOrbSpriteTemplate':5}
SPRITES|={r[0] for r in ORB_DEFENSE_RECIPES}
TASKS|=ORB_DEFENSE_TASKS
NEW|={r[0] for r in ORB_DEFENSE_RECIPES}|ORB_DEFENSE_TASKS
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
CURE_RECIPES=[('gAromatherapySmallFlowerSpriteTemplate','battle_anim_effects_1.c','AnimFlyingParticle'),('gAromatherapyBigFlowerSpriteTemplate','battle_anim_effects_1.c','AnimFlyingParticle'),('gSparklingStarsSpriteTemplate','battle_anim_effects_1.c','AnimSparklingStars'),('gBlendThinRingExpandingSpriteTemplate','battle_anim_effects_2.c','AnimBlendThinRing'),('gBellSpriteTemplate','battle_anim_effects_2.c','AnimSpriteOnMonPos'),('gHealBellMusicNoteSpriteTemplate','battle_anim_effects_2.c','AnimHealBellMusicNote')]
CURE_TASKS={'AnimTask_StatusClearedEffect','AnimTask_LoadMusicNotesPals','AnimTask_FreeMusicNotesPals'}
CURE_WIDTHS={'gAromatherapySmallFlowerSpriteTemplate':10,'gAromatherapyBigFlowerSpriteTemplate':10,'gSparklingStarsSpriteTemplate':10,'gBlendThinRingExpandingSpriteTemplate':7,'gBellSpriteTemplate':7,'gHealBellMusicNoteSpriteTemplate':10}
SPRITES|={r[0] for r in CURE_RECIPES}|{'gThinRingExpandingSpriteTemplate'}
TASKS|=CURE_TASKS|{'AnimTask_BlendBattleAnimPalExclude'}
NEW|={r[0] for r in CURE_RECIPES}|CURE_TASKS

SOLAR_PINCER_RECIPES=[('gSolarBeamBigOrbSpriteTemplate','battle_anim_effects_1.c','AnimSolarBeamBigOrb'),('gSolarBeamSmallOrbSpriteTemplate','battle_anim_effects_1.c','AnimSolarBeamSmallOrb'),('gGuillotineSpriteTemplate','battle_anim_effects_2.c','AnimGuillotinePincer')]
SPRITES|={r[0] for r in SOLAR_PINCER_RECIPES}|{'gPowerAbsorptionOrbSpriteTemplate'}
TASKS|={'AnimTask_CreateSmallSolarBeamOrbs'}
NEW|={r[0] for r in SOLAR_PINCER_RECIPES}|{'AnimTask_CreateSmallSolarBeamOrbs'}

GHOST_SONG_RECIPES=[('gPerishSongMusicNoteSpriteTemplate','battle_anim_effects_2.c','AnimPerishSongMusicNote'),('gPerishSongMusicNote2SpriteTemplate','battle_anim_effects_2.c','AnimPerishSongMusicNote2'),('gDestinyBondWhiteShadowSpriteTemplate','battle_anim_ghost.c','AnimDestinyBondWhiteShadow'),('gCurseNailSpriteTemplate','battle_anim_ghost.c','AnimCurseNail'),('gCurseGhostSpriteTemplate','battle_anim_ghost.c','AnimGhostStatusSprite')]
GHOST_SONG_TASKS={'AnimTask_DestinyBondWhiteShadow','AnimTask_CurseStretchingBlackBg','AnimTask_DrawFallingWhiteLinesOnAttacker'}
GHOST_SONG_WIDTHS={'gPerishSongMusicNoteSpriteTemplate':6,'gPerishSongMusicNote2SpriteTemplate':6,'gCurseNailSpriteTemplate':3,'gCurseGhostSpriteTemplate':3,'AnimTask_DestinyBondWhiteShadow':4,'AnimTask_CurseStretchingBlackBg':2,'AnimTask_DrawFallingWhiteLinesOnAttacker':2}
GHOST_SONG_MATERIALS=[('white_shadow','WhiteShadow',0x400,(64,32)),('nail','Nail',0x400,(32,64)),('ghostly_spirit','GhostlySpirit',0x200,(32,32))]
SPRITES|={r[0] for r in GHOST_SONG_RECIPES if r[0]!='gDestinyBondWhiteShadowSpriteTemplate'}
TASKS|=GHOST_SONG_TASKS
NEW|=set(GHOST_SONG_WIDTHS)


DAMAGE_FAMILY_RECIPES=[('gRevengeSmallScratchSpriteTemplate','battle_anim_fight.c','AnimRevengeScratch'),('gRevengeBigScratchSpriteTemplate','battle_anim_fight.c','AnimRevengeScratch'),('gPersistHitSplatSpriteTemplate','battle_anim_normal.c','AnimHitSplatPersistent'),('gMonEdgeHitSplatSpriteTemplate','battle_anim_normal.c','AnimHitSplatOnMonEdge'),('gRapidSpinSpriteTemplate','battle_anim_effects_3.c','AnimRapidSpin'),('gSpeedDustSpriteTemplate','battle_anim_effects_2.c','AnimSpeedDust'),('gAirWaveProjectileSpriteTemplate','battle_anim_effects_2.c','AnimAirWaveProjectile'),('gAirCutterSliceSpriteTemplate','battle_anim_effects_1.c','AnimAirCutterSlice')]
DAMAGE_FAMILY_TASKS={'AnimTask_RapinSpinMonElevation','AnimTask_AttackerStretchAndDisappear','AnimTask_ExtremeSpeedImpact','AnimTask_ExtremeSpeedMonReappear','AnimTask_SpeedDust','AnimTask_AirCutterProjectile'}
DAMAGE_FAMILY_WIDTHS={'gRevengeSmallScratchSpriteTemplate':5,'gRevengeBigScratchSpriteTemplate':5,'gPersistHitSplatSpriteTemplate':8,'gMonEdgeHitSplatSpriteTemplate':7,'gRapidSpinSpriteTemplate':9,'gAirCutterSliceSpriteTemplate':7,'AnimTask_RapinSpinMonElevation':5,'AnimTask_AttackerStretchAndDisappear':2,'AnimTask_ExtremeSpeedImpact':2,'AnimTask_ExtremeSpeedMonReappear':2,'AnimTask_SpeedDust':2,'AnimTask_AirCutterProjectile':7}
DAMAGE_FAMILY_MATERIALS=[('purple_scratch','PurpleScratch',0x600,(32,96),'PurpleScratch','purple_scratch'),('purple_swipe','PurpleSwipe',0x1000,(64,128),'PurpleScratch','purple_scratch'),('rapid_spin','RapidSpin',0x300,(32,48),'RapidSpin','rapid_spin')]
SPRITES|={r[0] for r in DAMAGE_FAMILY_RECIPES if r[0] not in {'gSpeedDustSpriteTemplate','gAirWaveProjectileSpriteTemplate'}}
TASKS|=DAMAGE_FAMILY_TASKS|{'AnimTask_GetAttackerSide','AnimTask_SetAttackerInvisibleWaitForSignal'}
NEW|=set(DAMAGE_FAMILY_WIDTHS)

SUPERPOWER_RECIPES=[('gSuperpowerOrbSpriteTemplate','battle_anim_fight.c','AnimSuperpowerOrb'),('gSuperpowerRockSpriteTemplate','battle_anim_fight.c','AnimSuperpowerRock'),('gSuperpowerFireballSpriteTemplate','battle_anim_fight.c','AnimSuperpowerFireball')]
SUPERPOWER_WIDTHS={'gSuperpowerOrbSpriteTemplate':4,'gSuperpowerRockSpriteTemplate':7,'gSuperpowerFireballSpriteTemplate':4}
SUPERPOWER_MATERIALS=[('meteor','Meteor',0x800,(64,64)),('flat_rock','FlatRock',0x280,(16,80))]
SPRITES|={r[0] for r in SUPERPOWER_RECIPES}
NEW|=set(SUPERPOWER_WIDTHS)

PAIN_SPLIT_RECIPES=[('gPainSplitProjectileSpriteTemplate','battle_anim_effects_3.c','AnimPainSplitProjectile')]
PAIN_SPLIT_WIDTHS={'gPainSplitProjectileSpriteTemplate':6,'AnimTask_PainSplitMovement':4}
PAIN_SPLIT_MATERIALS=[('pain_split','PainSplit',0x0180,(16,48))]
SPRITES|={r[0] for r in PAIN_SPLIT_RECIPES}
TASKS|={'AnimTask_PainSplitMovement'}
NEW|=set(PAIN_SPLIT_WIDTHS)

WEATHER_BALL_RECIPES=[('gWeatherBall'+kind+'SpriteTemplate','battle_anim_'+file+'.c','AnimWeatherBallUp' if kind=='Up' else 'AnimWeatherBallDown') for kind,file in [('Up','status_effects'),('NormalDown','status_effects'),('FireDown','fire'),('WaterDown','water'),('RockDown','rock'),('IceDown','ice')]]
WEATHER_BALL_WIDTHS={name:3 if callback=='AnimWeatherBallUp' else 9 for name,file,callback in WEATHER_BALL_RECIPES}
WEATHER_BALL_MATERIALS=[('weather_ball','WeatherBall',0x200,(32,32))]
SPRITES|=set(WEATHER_BALL_WIDTHS)
TASKS|={'AnimTask_GetWeather'}
NEW|=set(WEATHER_BALL_WIDTHS)|{'AnimTask_GetWeather'}

def weather_ball_recipes(raw):
 result=[]
 for name,file,callback in WEATHER_BALL_RECIPES:
  source=raw['src/'+file].decode()
  if name=='gWeatherBallRockDownSpriteTemplate':
   template=re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{(.*?)\};',source,re.S)
   if template is None or '.anims = &sAnims_BasicRock[2],' not in template[1]:raise ValueError('Weather rock source frame pointer differs')
   source=source.replace(template[0],template[0].replace('.anims = &sAnims_BasicRock[2],','.anims = sAnims_BasicRock,'),1)
  recipe=elemental.recipe(source,name,callback)
  if name=='gWeatherBallRockDownSpriteTemplate':
   if len(recipe['animations'])!=6:raise ValueError('Weather rock backing frame table differs')
   recipe['animations']=recipe['animations'][2:];recipe['loopStarts']=recipe['loopStarts'][2:]
   if 'frameLoops' in recipe:recipe['frameLoops']=recipe['frameLoops'][2:]
  result.append(recipe)
 return result

def validate_weather_ball_sources(raw):
 def body(file,name):
  match=re.search(r'^(?:static )?void '+name+r'\([^;]*?\)\s*\{(.*?)^\}',raw['src/'+file].decode(),re.M|re.S)
  if match is None:raise ValueError('Missing weather source callback '+name)
  return re.sub(r'\s+','',match[1])
 expected={
  'AnimWeatherBallUp':'sprite->x=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_X_2);sprite->y=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_Y_PIC_OFFSET);if(GetBattlerSide(gBattleAnimAttacker)==B_SIDE_PLAYER)sprite->data[0]=5;elsesprite->data[0]=-10;sprite->data[1]=-40;sprite->callback=AnimWeatherBallUp_Step;',
  'AnimWeatherBallUp_Step':'sprite->data[2]+=sprite->data[0];sprite->data[3]+=sprite->data[1];sprite->x2=sprite->data[2]/10;sprite->y2=sprite->data[3]/10;if(sprite->data[1]<-20)++sprite->data[1];if(sprite->y+sprite->y2<-32)DestroyAnimSprite(sprite);',
  'AnimWeatherBallDown':'s32x;sprite->data[0]=gBattleAnimArgs[2];sprite->data[2]=sprite->x+gBattleAnimArgs[4];sprite->data[4]=sprite->y+gBattleAnimArgs[5];if(GetBattlerSide(gBattleAnimTarget)==B_SIDE_PLAYER){x=(u16)gBattleAnimArgs[4]+30;sprite->x+=x;sprite->y=gBattleAnimArgs[5]-20;}else{x=(u16)gBattleAnimArgs[4]-30;sprite->x+=x;sprite->y=gBattleAnimArgs[5]-80;}sprite->callback=StartAnimLinearTranslation;StoreSpriteCallbackInData6(sprite,DestroyAnimSprite);'}
 for name,text in expected.items():
  if body('battle_anim_mons.c',name)!=text:raise ValueError('Weather source callback differs '+name)
 expected='gBattleAnimArgs[ARG_RET_ID]=ANIM_WEATHER_NONE;if(gWeatherMoveAnim&B_WEATHER_SUN)gBattleAnimArgs[ARG_RET_ID]=ANIM_WEATHER_SUN;elseif(gWeatherMoveAnim&B_WEATHER_RAIN)gBattleAnimArgs[ARG_RET_ID]=ANIM_WEATHER_RAIN;elseif(gWeatherMoveAnim&B_WEATHER_SANDSTORM)gBattleAnimArgs[ARG_RET_ID]=ANIM_WEATHER_SANDSTORM;elseif(gWeatherMoveAnim&B_WEATHER_HAIL)gBattleAnimArgs[ARG_RET_ID]=ANIM_WEATHER_HAIL;DestroyAnimVisualTask(taskId);'
 if body('battle_anim_effects_3.c','AnimTask_GetWeather')!=expected:raise ValueError('Weather predicate precedence differs')
 constants={k:int(v) for k,v in re.findall(r'^#define (ANIM_WEATHER_\w+)\s+(\d+)\s*$',raw['include/constants/battle_anim.h'].decode(),re.M)}
 if [constants.get('ANIM_WEATHER_'+n) for n in ['NONE','SUN','RAIN','SANDSTORM','HAIL']]!=list(range(5)):raise ValueError('Weather return constants differ')
 weather=re.sub(r'\s+','',raw['include/constants/battle.h'].decode())
 for text in ['B_WEATHER_RAIN_TEMPORARY(1<<0)','B_WEATHER_RAIN_DOWNPOUR(1<<1)','B_WEATHER_RAIN_PERMANENT(1<<2)','B_WEATHER_SANDSTORM_TEMPORARY(1<<3)','B_WEATHER_SANDSTORM_PERMANENT(1<<4)','B_WEATHER_SUN_TEMPORARY(1<<5)','B_WEATHER_SUN_PERMANENT(1<<6)','B_WEATHER_HAIL_TEMPORARY(1<<7)','B_WEATHER_RAIN(B_WEATHER_RAIN_TEMPORARY|B_WEATHER_RAIN_DOWNPOUR|B_WEATHER_RAIN_PERMANENT)','B_WEATHER_SANDSTORM(B_WEATHER_SANDSTORM_TEMPORARY|B_WEATHER_SANDSTORM_PERMANENT)','B_WEATHER_SUN(B_WEATHER_SUN_TEMPORARY|B_WEATHER_SUN_PERMANENT)','B_WEATHER_HAIL(B_WEATHER_HAIL_TEMPORARY)']:
  if '#define'+text not in weather:raise ValueError('Weather controller bit masks differ')
 return constants

def damage_family_materials(raw):
 indexed=[];files={};registrations=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
 for stem,gfx,size,shape,pal,palstem in DAMAGE_FAMILY_MATERIALS:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+gfx+r',\s*0x0*'+format(size,'x')+r',\s*'+tag+r'\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal+r',\s*'+tag+r'\}',registrations):raise ValueError('Damage source material registration differs: '+stem)
  for kind,symbol,name,suffix in [('Gfx',gfx,stem,'4bpp'),('Pal',pal,palstem,'gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+name+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Damage source material linkage differs: '+stem)
  with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/'+stem+'.png'])) as im:
   if im.mode!='P' or im.size!=shape:raise ValueError('Damage source indexed geometry differs: '+stem)
   indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()]
  palette=reaction_palette(raw,palstem)
  if len(indices)!=size*2 or max(indices)>15 or len(palette)!=48:raise ValueError('Damage source loaded sheet size differs: '+stem)
  name=stem.replace('_','-')
  files[name+'.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2));files[name+'.pal16']=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
  indexed.append(dict(id=name,width=shape[0],height=shape[1],indicesPath='physical-'+name+'.4bpp',palettePath='physical-'+name+'.pal16',paletteTag=tag))
 return indexed,files

def frozen_cube_recipe(raw):
 source=raw['src/battle_anim_status_effects.c'].decode();recipe=elemental.recipe(source,'sFrozenIceCubeSpriteTemplate','SpriteCallbackDummy')
 table=re.search(r'const struct SubspriteTable sFrozenIceCubeSubspriteTable\[\]\s*=\s*\{\s*\{NELEMS\(sFrozenIceCubeSubsprites\), sFrozenIceCubeSubsprites\},\s*\};',source,re.S)
 if table is None or 'SetSubspriteTables(&gSprites[spriteId], sFrozenIceCubeSubspriteTable);' not in source:raise ValueError('Frozen cube subsprite owner differs')
 body=re.search(r'const struct Subsprite sFrozenIceCubeSubsprites\[\]\s*=\s*\{(.*?)\n\};',source,re.S)
 if body is None:raise ValueError('Frozen cube subsprite table is missing')
 pieces=[]
 for row in re.findall(r'\{(.*?)\}',body[1]):
  fields=dict(re.findall(r'\.(\w+)\s*=\s*([^,]+)(?:,|$)',row));shape=re.fullmatch(r'SPRITE_SHAPE\((\d+)x(\d+)\)',fields['shape']);size=re.fullmatch(r'SPRITE_SIZE\((\d+)x(\d+)\)',fields['size'])
  if shape is None or size is None or shape.groups()!=size.groups():raise ValueError('Frozen cube subsprite shape differs')
  pieces.append(dict(x=int(fields['x']),y=int(fields['y']),width=int(shape[1]),height=int(shape[2]),tileOffset=int(fields['tileOffset']),priority=int(fields['priority'])))
 expected=[dict(x=x,y=y,width=w,height=h,tileOffset=t,priority=2) for x,y,w,h,t in [(-16,-16,64,64,0),(-16,48,64,32,64),(48,-16,32,64,96),(48,48,32,32,128)]]
 if pieces!=expected:raise ValueError('Frozen cube literal subsprite geometry differs')
 recipe['subsprites']=pieces;return recipe

def frozen_cube_materials(raw):
 registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode();rules=raw['graphics_file_rules.mk'].decode()
 if not re.search(r'\{gBattleAnimSpriteGfx_IceCube,\s*0x1200,\s*ANIM_TAG_ICE_CUBE\}',registration) or not re.search(r'\{gBattleAnimSpritePal_IceCube,\s*ANIM_TAG_ICE_CUBE\}',registration):raise ValueError('Frozen cube source sheet registration differs')
 for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+r'_IceCube\[\] = INCBIN_U32\("graphics/battle_anims/sprites/ice_cube\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Frozen cube source material linkage differs')
 rule=re.search(r'^\$\(BTLANMSPRGFXDIR\)/ice_cube\.4bpp: (.*?)\n\t@cat \$\^ >\$@',rules,re.M|re.S)
 if rule is None or re.findall(r'ice_cube_(\d)\.4bpp',rule[1])!=['0','1','2','3']:raise ValueError('Frozen cube source sheet concatenation differs')
 rows=raw['graphics/battle_anims/sprites/ice_cube.pal'].decode().splitlines()
 if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Frozen cube source palette shape differs')
 colors=[tuple(map(int,line.split())) for line in rows[3:]]
 if len(colors)!=16 or any(len(c)!=3 or any(v<0 or v>255 for v in c) for c in colors):raise ValueError('Frozen cube source palette range differs')
 palette=[sum((c>>3)<<(shift*5) for shift,c in enumerate(row)) for row in colors];indices=[]
 for i,shape in enumerate([(64,64),(64,32),(32,64),(32,32)]):
  with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/ice_cube_'+str(i)+'.png'])) as image:
   if image.mode!='P' or image.size!=shape:raise ValueError('Frozen cube indexed part shape differs')
   indices.extend(v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes())
 if len(indices)!=0x2400 or max(indices)>15:raise ValueError('Frozen cube indexed concatenation size differs')
 files={'ice-cube.4bpp':bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),'ice-cube.pal16':struct.pack('<16H',*palette)}
 return [dict(id='ice-cube',width=64,height=144,indicesPath='physical-ice-cube.4bpp',palettePath='physical-ice-cube.pal16',paletteTag='ANIM_TAG_ICE_CUBE')],files

def ghost_song_recipes(raw):
 # The shared music-note table is defined in effects_1; do not synthesize it.
 return [elemental.recipe(raw['src/'+file].decode()+raw['src/battle_anim_effects_1.c'].decode(),name,callback) for name,file,callback in GHOST_SONG_RECIPES]

FOCUS_SOUND_RECIPES=[('gOpeningEyeSpriteTemplate', 'battle_anim_effects_3.c', 'AnimSpriteOnMonPos'), ('gWhiteHaloSpriteTemplate', 'battle_anim_effects_3.c', 'AnimWhiteHalo'), ('gTealAlertSpriteTemplate', 'battle_anim_effects_3.c', 'AnimTealAlert'), ('gLockOnTargetSpriteTemplate', 'battle_anim_effects_1.c', 'AnimLockOnTarget'), ('gLockOnMoveTargetSpriteTemplate', 'battle_anim_effects_1.c', 'AnimLockOnMoveTarget'), ('gSnoreZSpriteTemplate', 'battle_anim_effects_2.c', 'AnimTravelDiagonally'), ('gJaggedMusicNoteSpriteTemplate', 'battle_anim_effects_2.c', 'AnimJaggedMusicNote'), ('gUproarRingSpriteTemplate', 'battle_anim_effects_2.c', 'AnimUproarRing')]
FOCUS_SOUND_WIDTHS={'gOpeningEyeSpriteTemplate': 7, 'gWhiteHaloSpriteTemplate': 3, 'gTealAlertSpriteTemplate': 6, 'gLockOnTargetSpriteTemplate': 3, 'gLockOnMoveTargetSpriteTemplate': 4, 'gSnoreZSpriteTemplate': 10, 'gJaggedMusicNoteSpriteTemplate': 7, 'gUproarRingSpriteTemplate': 9, 'AnimTask_UproarDistortion': 3}
SPRITES|={r[0] for r in FOCUS_SOUND_RECIPES}
TASKS|={'AnimTask_UproarDistortion'}
NEW|={r[0] for r in FOCUS_SOUND_RECIPES}|{'AnimTask_UproarDistortion'}
FOCUS_SOUND_MATERIALS=[('lock_on', 'LockOn', 640, (32, 40)), ('teal_alert', 'TealAlert', 512, (32, 32)), ('opening_eye', 'OpeningEye', 2048, (32, 128)), ('round_white_halo', 'RoundWhiteHalo', 2048, (64, 64)), ('snore_z', 'SnoreZ', 512, (32, 32)), ('jagged_music_note', 'JaggedMusicNote', 1024, (32, 64))]

ITEM_WIND_RECIPES=[('gTrickBagSpriteTemplate','battle_anim_effects_1.c','AnimTrickBag'),('gRecycleSpriteTemplate','battle_anim_effects_3.c','AnimRecycle'),('gSilverWindBigSparkSpriteTemplate','battle_anim_effects_1.c','AnimFlyingParticle'),('gSilverWindMediumSparkSpriteTemplate','battle_anim_effects_1.c','AnimFlyingParticle'),('gSilverWindSmallSparkSpriteTemplate','battle_anim_effects_1.c','AnimFlyingParticle')]
ITEM_WIND_WIDTHS={'gTrickBagSpriteTemplate':5,'gRecycleSpriteTemplate':3,'gSilverWindBigSparkSpriteTemplate':10,'gSilverWindMediumSparkSpriteTemplate':10,'gSilverWindSmallSparkSpriteTemplate':10,'AnimTask_StretchAttackerUp':2,'AnimTask_GetTargetSide':2}
SPRITES|={r[0] for r in ITEM_WIND_RECIPES}
TASKS|={'AnimTask_StretchAttackerUp','AnimTask_GetTargetSide'}
NEW|={r[0] for r in ITEM_WIND_RECIPES}
ITEM_WIND_MATERIALS=[('item_bag','ItemBag',0x200,(32,32)),('speed_dust','SpeedDust',0x180,(16,48)),('recycle','Recycle',0x800,(64,64)),('sparkle_6','Sparkle6',0x80,(16,32))]

PRESENT_RECIPES=[('gPresentSpriteTemplate','battle_anim_effects_1.c','AnimPresent'),('gPresentHealParticleSpriteTemplate','battle_anim_effects_1.c','AnimPresentHealParticle')]
PRESENT_WIDTHS={'gPresentSpriteTemplate':8,'gPresentHealParticleSpriteTemplate':7,'AnimTask_IsHealingMove':2}
SPRITES|={r[0] for r in PRESENT_RECIPES}|{'gExplosionSpriteTemplate'}
TASKS|={'AnimTask_IsHealingMove'}
NEW|={r[0] for r in PRESENT_RECIPES}|{'AnimTask_IsHealingMove'}

def validate_present_predicate(raw):
 source=raw['src/battle_anim_effects_3.c'].decode()
 match=re.search(r'void AnimTask_IsHealingMove\(u8 taskId\)\s*\{(.*?)^\}',source,re.S|re.M)
 expected='if(gAnimMoveDmg>0)gBattleAnimArgs[ARG_RET_ID]=FALSE;elsegBattleAnimArgs[ARG_RET_ID]=TRUE;DestroyAnimVisualTask(taskId);'
 if match is None or re.sub(r'\s+','',match[1])!=expected:raise ValueError('Present source damage-sign predicate differs')

EXTRASENSORY_TASKS={'AnimTask_ExtrasensoryDistortion','AnimTask_TransparentCloneGrowAndShrink'}
TASKS|=EXTRASENSORY_TASKS
NEW|=EXTRASENSORY_TASKS

EARTH_BACKGROUND_RECIPES=[('gRockScatterSpriteTemplate','battle_anim_rock.c','AnimRockScatter')]
EARTH_BACKGROUND_TASKS={'AnimTask_PositionFissureBgOnBattler','AnimTask_GetSeismicTossDamageLevel','AnimTask_MoveSeismicTossBg','AnimTask_SeismicTossBgAccelerateDownAtEnd'}
EARTH_BACKGROUND_WIDTHS={'gRockScatterSpriteTemplate':7,'AnimTask_PositionFissureBgOnBattler':5,'AnimTask_GetSeismicTossDamageLevel':2,'AnimTask_MoveSeismicTossBg':2,'AnimTask_SeismicTossBgAccelerateDownAtEnd':2}
SPRITES|={r[0] for r in EARTH_BACKGROUND_RECIPES}
TASKS|=EARTH_BACKGROUND_TASKS
NEW|={r[0] for r in EARTH_BACKGROUND_RECIPES}|EARTH_BACKGROUND_TASKS

SCANLINE_BATTLER_RECIPES=[('gDirtPlumeSpriteTemplate','battle_anim_ground.c','AnimDirtPlumeParticle'),('gDirtMoundSpriteTemplate','battle_anim_ground.c','AnimDigDirtMound'),('gPencilSpriteTemplate','battle_anim_effects_2.c','AnimPencil'),('gDragonDanceOrbSpriteTemplate','battle_anim_dragon.c','AnimDragonDanceOrb')]
SCANLINE_BATTLER_TASKS={'AnimTask_DigDownMovement','AnimTask_DigUpMovement','AnimTask_SketchDrawMon','AnimTask_DragonDanceWaver','AnimTask_AcidArmor','AnimTask_BlendPalInAndOutByTag'}
SCANLINE_BATTLER_WIDTHS={'gDirtPlumeSpriteTemplate':9,'gDirtMoundSpriteTemplate':6,'gPencilSpriteTemplate':3,'gDragonDanceOrbSpriteTemplate':4,'AnimTask_DigDownMovement':3,'AnimTask_DigUpMovement':3,'AnimTask_SketchDrawMon':2,'AnimTask_DragonDanceWaver':2,'AnimTask_AcidArmor':3,'AnimTask_BlendPalInAndOutByTag':7}
SCANLINE_BATTLER_MATERIALS=[('pencil','Pencil',0x200,(32,32)),('dirt_mound','DirtMound',0x200,(32,32))]
SPRITES|={r[0] for r in SCANLINE_BATTLER_RECIPES}
TASKS|=SCANLINE_BATTLER_TASKS
NEW|={r[0] for r in SCANLINE_BATTLER_RECIPES}|SCANLINE_BATTLER_TASKS

UTILITY_RECIPES=[('gYawnCloudSpriteTemplate','battle_anim_effects_3.c','AnimYawnCloud'),('gSpikesSpriteTemplate','battle_anim_effects_3.c','AnimSpikes'),('sSkillSwapOrbSpriteTemplate','battle_anim_psychic.c','AnimSkillSwapOrb'),('gGrudgeFlameSpriteTemplate','battle_anim_ghost.c','AnimGrudgeFlame')]
UTILITY_TASKS={'AnimTask_SkillSwap','AnimTask_GrudgeFlames'}
UTILITY_WIDTHS={'gYawnCloudSpriteTemplate':4,'gSpikesSpriteTemplate':8,'AnimTask_SkillSwap':3,'AnimTask_GrudgeFlames':2}
SPRITES|={'gYawnCloudSpriteTemplate','gSpikesSpriteTemplate'}
TASKS|=UTILITY_TASKS|{'AnimTask_DeepInhale','AnimTask_SetPsychicBackground'}
NEW|={'gYawnCloudSpriteTemplate','gSpikesSpriteTemplate'}|UTILITY_TASKS
UTILITY_MATERIALS=[('pink_cloud','PinkCloud',0x0200,(32,32)),('bluegreen_orb','BluegreenOrb',0x0080,(16,16)),('spikes','Spikes',0x0080,(16,16)),('purple_flame','PurpleFlame',0x0400,(16,128))]

def utility_materials(raw,materials=UTILITY_MATERIALS):
 registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode();indexed=[];files={}
 for stem,symbol,size,shape in materials:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x0*'+format(size,'x')+r',\s*'+tag+r'\}',registration,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registration):raise ValueError('Utility source registration differs: '+stem)
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Utility source material linkage differs: '+stem)
  png=raw['graphics/battle_anims/sprites/'+stem+'.png']
  with Image.open(io.BytesIO(png)) as image:
   if image.mode!='P' or image.size!=shape:raise ValueError('Utility indexed source shape differs: '+stem)
   indices=[b for y in range(0,image.height,8) for x in range(0,image.width,8) for b in image.crop((x,y,x+8,y+8)).tobytes()];palette=image.getpalette()[:48]
  if max(indices)>15 or len(indices)<size*2 or size*2%shape[0] or (size*2//shape[0])%8 or len(palette)!=48:raise ValueError('Utility indexed sheet size/palette differs: '+stem)
  indices=indices[:size*2];name=stem.replace('_','-');base='physical-'+name
  files[name+'.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2));files[name+'.pal16']=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
  indexed.append(dict(id=name,width=shape[0],height=size*2//shape[0],indicesPath=base+'.4bpp',palettePath=base+'.pal16',paletteTag=tag))
 return indexed,files

def validate_solar_pincer_callbacks(raw):
 def body(file,name):
  match=re.search(r'^(?:static )?void '+name+r'\([^;]*?\)\s*\{(.*?)^\}',raw['src/'+file].decode(),re.M|re.S)
  if match is None:raise ValueError('Missing Solar/pincer callback: '+name)
  return re.sub(r'\s+','',match[1])
 required=[('battle_anim_effects_1.c','AnimSolarBeamBigOrb',['InitSpritePosToAnimAttacker(sprite,TRUE);','StartSpriteAnim(sprite,gBattleAnimArgs[3]);','sprite->callback=StartAnimLinearTranslation;','StoreSpriteCallbackInData6(sprite,DestroyAnimSprite);']),
 ('battle_anim_effects_1.c','AnimSolarBeamSmallOrb_Step',['if(AnimTranslateLinear(sprite))','DestroySprite(sprite);','Sin(sprite->data[5],5)','Cos(sprite->data[5],14)','sprite->data[5]=(sprite->data[5]+15)&0xFF;']),
 ('battle_anim_effects_1.c','AnimTask_CreateSmallSolarBeamOrbs',['--gTasks[taskId].data[0]==-1','gTasks[taskId].data[0]=6;','gBattleAnimArgs[2]=80;','CreateSpriteAndAnimate(&gSolarBeamSmallOrbSpriteTemplate','gTasks[taskId].data[1]==15']),
 ('battle_anim_effects_2.c','AnimGuillotinePincer_Step1',['SeekSpriteAnim(sprite,0);','sprite->animPaused=TRUE;','sprite->data[1]^=1;','sprite->data[2]^=1;']),
 ('battle_anim_effects_2.c','AnimGuillotinePincer_Step2',['++sprite->data[4]==51','sprite->animPaused=FALSE;']),
 ('battle_anim_effects_2.c','AnimGuillotinePincer_Step3',['if(AnimTranslateLinear(sprite))','DestroyAnimSprite(sprite);'])]
 for file,name,expressions in required:
  text=body(file,name)
  if any(expression not in text for expression in expressions):raise ValueError('Solar/pincer callback contract differs: '+name)

def cure_materials(raw):
 registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode();rules=raw['graphics_file_rules.mk'].decode()
 if not all(name in rules for name in ['$(BTLANMSPRGFXDIR)/flower.4bpp: $(BTLANMSPRGFXDIR)/flower_0.4bpp','$(BTLANMSPRGFXDIR)/flower_1.4bpp']):raise ValueError('Flower concatenation rule differs')
 sources=[('bell','Bell',0x600,[(32,96)],None),('music_notes_2','MusicNotes2',0x800,[(16,256)],80),('flower','Flower',0xa0,[(16,16),(8,8)],16)]
 indexed=[];files={}
 for stem,symbol,size,shapes,colors in sources:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x0*'+format(size,'x')+r',\s*'+tag+r'\}',registration,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registration):raise ValueError('Cure sheet/palette registration differs: '+stem)
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_U32\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Cure graphics declaration differs: '+stem)
  tiles=[];embedded=[]
  for i,shape in enumerate(shapes):
   path=stem+('_'+str(i) if len(shapes)>1 else '')
   with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/'+path+'.png'])) as im:
    if im.mode!='P' or im.size!=shape:raise ValueError('Cure indexed shape differs: '+path)
    tiles.extend(im.crop((x,y,x+8,y+8)).tobytes() for y in range(0,im.height,8) for x in range(0,im.width,8));embedded=im.getpalette()[:48]
  indices=b''.join(tiles)
  if len(indices)!=size*2 or max(indices)>15:raise ValueError('Cure sheet allocation differs: '+stem)
  if colors is not None:
   rows=raw['graphics/battle_anims/sprites/'+stem+'.pal'].decode().splitlines()
   if rows[:3]!=['JASC-PAL','0100',str(colors)]:raise ValueError('Cure palette group differs: '+stem)
   palette=[int(c) for row in rows[3:] for c in row.split()]
   if len(palette)!=colors*3 or any(c<0 or c>255 for c in palette):raise ValueError('Cure palette count differs')
  else:palette=embedded
  pal=struct.pack('<'+'H'*(len(palette)//3),*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(len(palette)//3)])
  identity=stem.replace('_','-');base='physical-'+identity
  packed=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2));files[identity+'.4bpp']=packed;files[identity+'.pal16']=pal[:32]
  # A vertical tile strip preserves original linear tile numbers for concatenated flowers.
  shape=shapes[0] if len(shapes)==1 else (8,len(indices)//8)
  indexed.append(dict(id=identity,width=shape[0],height=shape[1],indicesPath=base+'.4bpp',palettePath=base+'.pal16',paletteTag=tag))
  if colors==80:files[identity+'.pal48']=pal[:96]
 return indexed,files

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

def normalize_committed_damage_shake(effect,commands,raw):
 # Approved visual policy: Endeavor never prepares gBattleMovePower. Use the
 # committed, pre-HP-clamp damage with the existing damage-mode shake formula.
 if effect!='EFFECT_ENDEAVOR':return
 body=raw['data/battle_scripts_1.s'].decode().split('BattleScript_EffectEndeavor::',1)[1].split('\nBattleScript_',1)[0]
 if 'setdamagetohealthdifference' not in body or 'adjustsetdamage' not in body or 'goto BattleScript_HitFromAtkAnimation' not in body or re.search(r'^\s*damagecalc\b',body,re.M):raise ValueError('Endeavor source power-provenance boundary differs')
 shakes=[c for c in commands if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_ShakeTargetBasedOnMovePowerOrDmg']
 expected=['AnimTask_ShakeTargetBasedOnMovePowerOrDmg','5','FALSE','1','8','1','0']
 if len(shakes)!=2 or any(c['args']!=expected for c in shakes):raise ValueError('Endeavor source shake policy operands differ')
 for command in shakes:command['args']=[*command['args'][:2],'TRUE',*command['args'][3:]]

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
  frames,loop,controls=frame_program(body)
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

def validate_orb_defense_callbacks(raw):
 def compact(file,name):
  match=re.search(r'^(?:static )?void '+name+r'\([^;]*?\)\s*\{(.*?)^\}',raw['src/'+file].decode(),re.M|re.S)
  if match is None:raise ValueError('Missing orb/defense source callback: '+name)
  return re.sub(r'\s+','',match[1])
 expected='InitSpritePosToAnimAttacker(sprite,TRUE);sprite->data[0]=gBattleAnimArgs[2];sprite->data[2]=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_X_2);sprite->data[4]=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_Y_PIC_OFFSET);sprite->callback=StartAnimLinearTranslation;StoreSpriteCallbackInData6(sprite,DestroySpriteAndMatrix);'
 if compact('battle_anim_effects_1.c','AnimPowerAbsorptionOrb')!=expected:raise ValueError('Stockpile source translation/matrix callback differs')
 expected='switch(sprite->data[0]){case0:InitSpritePosToAnimAttacker(sprite,FALSE);sprite->data[1]=0x900;sprite->data[2]=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_Y_PIC_OFFSET);sprite->data[0]++;break;case1:sprite->y2-=sprite->data[1]>>8;sprite->data[1]-=96;if(sprite->y+sprite->y2>sprite->data[2])DestroyAnimSprite(sprite);break;}'
 if compact('battle_anim_effects_3.c','AnimSwallowBlueOrb')!=expected:raise ValueError('Swallow source ballistic callback differs')
 for callback,program in [('AnimTask_SpitUpDeformMon','sSpitUpDeformMonAffineAnimCmds'),('AnimTask_StockpileDeformMon','sStockpileDeformMonAffineAnimCmds'),('AnimTask_SwallowDeformMon','sSwallowDeformMonAffineAnimCmds')]:
  expected='if(!gTasks[taskId].data[0]){PrepareAffineAnimInTaskData(&gTasks[taskId],GetAnimBattlerSpriteId(ANIM_ATTACKER),'+program+');gTasks[taskId].data[0]++;}else{if(!RunAffineAnimFromTaskData(&gTasks[taskId]))DestroyAnimVisualTask(taskId);}'
  if compact('battle_anim_effects_3.c',callback)!=expected:raise ValueError('Orb/defense source task-affine wrapper differs: '+callback)

def validate_charged_root_callbacks(raw):
 def body(file,name):
  match=re.search(r'^(?:static )?void '+name+r'\([^;]*?\)\s*\{(.*?)^\}',raw['src/'+file].decode(),re.M|re.S)
  if match is None:raise ValueError('Missing charged root callback: '+name)
  return re.sub(r'\s+','',match[1])
 if body('battle_anim_utility_funcs.c','AnimTask_GetTargetIsAttackerPartner')!='gBattleAnimArgs[7]=BATTLE_PARTNER(gBattleAnimAttacker)==gBattleAnimTarget;DestroyAnimVisualTask(taskId);':raise ValueError('Partner predicate differs')
 if body('battle_anim_effects_2.c','AnimSpitUpOrb')!='sprite->x=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_X_2);sprite->y=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_Y_PIC_OFFSET);sprite->data[0]=Sin(gBattleAnimArgs[0],10);sprite->data[1]=Cos(gBattleAnimArgs[0],7);sprite->data[2]=gBattleAnimArgs[1];sprite->callback=AnimSpitUpOrb_Step;':raise ValueError('Spit Up callback differs')
 if body('battle_anim_effects_2.c','AnimSpitUpOrb_Step')!='sprite->x2+=sprite->data[0];sprite->y2+=sprite->data[1];if(sprite->data[3]++>=sprite->data[2])DestroyAnimSprite(sprite);':raise ValueError('Spit Up postincrement lifetime differs')
 if body('battle_anim_flying.c','AnimSkyAttackBird')!='u16rotation;s16posx=sprite->x;s16posy=sprite->y;sprite->x=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_X_2);sprite->y=GetBattlerSpriteCoord(gBattleAnimAttacker,BATTLER_COORD_Y_PIC_OFFSET);sprite->data[4]=sprite->x<<4;sprite->data[5]=sprite->y<<4;sprite->data[6]=((posx-sprite->x)<<4)/12;sprite->data[7]=((posy-sprite->y)<<4)/12;rotation=ArcTan2Neg(posx-sprite->x,posy-sprite->y);rotation+=49152;TrySetSpriteRotScale(sprite,1,0x100,0x100,rotation);sprite->callback=AnimSkyAttackBird_Step;':raise ValueError('Sky Attack fixed-point initialization differs')
 if body('battle_anim_flying.c','AnimSkyAttackBird_Step')!='sprite->data[4]+=sprite->data[6];sprite->data[5]+=sprite->data[7];sprite->x=sprite->data[4]>>4;sprite->y=sprite->data[5]>>4;if(sprite->x>DISPLAY_WIDTH+45||sprite->x<-45||sprite->y>157||sprite->y<-45)DestroySpriteAndMatrix(sprite);':raise ValueError('Sky Attack source exit boundary differs')

def orb_defense_materials(raw):
 # BlueOrb shares GrayOrb's three source tiles but has its own palette bank.
 registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
 indexed=[];pixels={}
 for stem,gfx,pal,size,shape,png in [('protect','Protect','Protect',0x0800,(64,64),'protect'),('shadow_ball','ShadowBall','ShadowBall',0x0200,(32,32),'shadow_ball'),('gray_orb','GrayOrb','GrayOrb',0x0060,(8,24),'gray_orb'),('blue_orb','GrayOrb','BlueOrb',0x0060,(8,24),'gray_orb'),('red_orb_2','GrayOrb','RedOrb2',0x0060,(8,24),'gray_orb')]:
  tag='ANIM_TAG_'+stem.upper()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+gfx+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registration,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+pal+r',\s*'+tag+r'\}',registration):raise ValueError('Orb/defense source material registration differs: '+stem)
  for kind,symbol,name,suffix in [('Gfx',gfx,png,'4bpp'),('Pal',pal,stem,'gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+name+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Orb/defense graphic linkage differs: '+stem)
  source=raw['graphics/battle_anims/sprites/'+png+'.png']
  with Image.open(io.BytesIO(source)) as image:
   if image.mode!='P' or image.size!=shape:raise ValueError('Orb/defense indexed source shape differs: '+stem)
   indices=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()];palette=image.getpalette()[:48]
  if stem in {'blue_orb','red_orb_2'}:
   rows=raw['graphics/battle_anims/sprites/'+stem+'.pal'].decode().splitlines()
   if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Blue orb source palette differs')
   palette=[int(c) for row in rows[3:] for c in row.split()]
  if max(indices)>15 or len(palette)!=48:raise ValueError('Orb/defense indexed palette differs: '+stem)
  name=stem.replace('_','-');base='physical-'+name
  pixels[name+'.4bpp']=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2))
  pixels[name+'.pal16']=struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
  indexed.append(dict(id=name,width=shape[0],height=shape[1],indicesPath=base+'.4bpp',palettePath=base+'.pal16',paletteTag=tag))
 return indexed,pixels

def build_assets(reference):
 raw=read_sources(reference);validate_present_predicate(raw);png=raw['graphics/battle_anims/sprites/slam_hit.png']
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
 assets={'gold-stars':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_GoldStars','gold_stars'),'swipe':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Swipe','swipe'),'fang-attack':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_FangAttack','fang_attack'),'seed':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Seed','seed'),'icicle-spear':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_IcicleSpear','icicle_spear'),'claw-slash':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_ClawSlash','claw_slash'),'torn-metal':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_TornMetal','torn_metal',blue_palette),'bone':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_Bone','bone'),'eye-sparkle':registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_EyeSparkle','eye_sparkle'),'eclipsing-orb':hit.rgb555(raw['graphics/battle_anims/sprites/eclipsing_orb.png'],(32,128)),'breath':hit.rgb555(raw['graphics/battle_anims/sprites/breath.png'],(16,64)),'slam-hit':hit.rgb555(png,(32,256)),'whip-hit':hit.rgb555(png,(32,256),palette)}
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
 raw=read_sources(reference);validate_reaction_sources(raw);indexed=[];pixels={}
 # Match source tile/palette registration and graphic linkage, including the
 # loaded-but-unused Whirlwind Lines material in Drill Peck's original script.
 for stem,symbol,size in [('bird','Bird',0x0800),('round_shadow','RoundShadow',0x0800),('splash','Splash',0x0800),('red_orb','RedOrb',0x0080),('coin','Coin',0x0200),('cross_impact','CrossImpact',0x0200),('red_ball','RedBall',0x0200),('whirlwind_lines','WhirlwindLines',0x0300)]:
  tag='ANIM_TAG_'+stem.upper();registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
  if not re.search(r'\{gBattleAnimSpriteGfx_'+symbol+r',\s*0x'+format(size,'04x')+r',\s*'+tag+r'\}',registration,re.I) or not re.search(r'\{gBattleAnimSpritePal_'+symbol+r',\s*'+tag+r'\}',registration):raise ValueError('Impact emission source registration differs: '+stem)
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
   if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Impact emission source material differs: '+stem)
 registration=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
 if not re.search(r'\{gBattleAnimSpriteGfx_SweatBead,\s*0x0020,\s*ANIM_TAG_SWEAT_BEAD\}',registration) or not re.search(r'\{gBattleAnimSpritePal_Splash,\s*ANIM_TAG_SWEAT_BEAD\}',registration):raise ValueError('Water droplet source palette alias differs')
 for kind,symbol,stem,suffix in [('Gfx','SweatBead','sweat_bead','4bpp'),('Pal','Splash','splash','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+'_'+symbol+r'\[\] = INCBIN_[US]\d+\("graphics/battle_anims/sprites/'+stem+r'\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Water droplet source material differs')
 for stem,size in [('bird',(64,64)),('round_shadow',(64,64)),('splash',(64,64)),('red_orb',(16,16)),('sweat_bead',(8,8)),('coin',(16,64)),('cross_impact',(32,32)),('red_ball',(32,32)),('whirlwind_lines',(32,48))]+[(r[0],r[3]) for r in REACTION_MATERIALS+FRIENDSHIP_CONTACT_MATERIALS+ROCK_STATUS_MATERIALS]:
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
 orb_indexed,orb_pixels=orb_defense_materials(raw);indexed.extend(orb_indexed);pixels.update(orb_pixels)
 cure_indexed,cure_pixels=cure_materials(raw);indexed.extend(cure_indexed);pixels.update(cure_pixels)
 utility_indexed,utility_pixels=utility_materials(raw);indexed.extend(utility_indexed);pixels.update(utility_pixels)
 ghost_indexed,ghost_pixels=utility_materials(raw,GHOST_SONG_MATERIALS);indexed.extend(ghost_indexed);pixels.update(ghost_pixels)
 focus_indexed,focus_pixels=utility_materials(raw,FOCUS_SOUND_MATERIALS);indexed.extend(focus_indexed);pixels.update(focus_pixels)
 item_indexed,item_pixels=utility_materials(raw,ITEM_WIND_MATERIALS);indexed.extend(item_indexed);pixels.update(item_pixels)
 scanline_indexed,scanline_pixels=utility_materials(raw,SCANLINE_BATTLER_MATERIALS);indexed.extend(scanline_indexed);pixels.update(scanline_pixels)
 super_indexed,super_pixels=utility_materials(raw,SUPERPOWER_MATERIALS);indexed.extend(super_indexed);pixels.update(super_pixels)
 pain_indexed,pain_pixels=utility_materials(raw,PAIN_SPLIT_MATERIALS);indexed.extend(pain_indexed);pixels.update(pain_pixels)
 damage_indexed,damage_pixels=damage_family_materials(raw);indexed.extend(damage_indexed);pixels.update(damage_pixels)
 frozen_indexed,frozen_pixels=frozen_cube_materials(raw);indexed.extend(frozen_indexed);pixels.update(frozen_pixels)
 weather_indexed,weather_pixels=utility_materials(raw,WEATHER_BALL_MATERIALS);indexed.extend(weather_indexed);pixels.update(weather_pixels)
 return indexed,pixels

def physical_sound_effects(reference,commands,source,focus_source=None):
 # Callback sounds are real dependencies even when the authored script has no
 # sound opcode. Feed the source call operands to the shared song-ID extractor.
 dependencies=list(commands)
 if any(c['op']=='createsprite' and c['args'][0]=='gBulletSeedSpriteTemplate' for c in commands):
  body=re.search(r'static void AnimBulletSeed_Step1\(struct Sprite \*sprite\)\s*\{(.*?)\n\}',source,re.S)
  sounds=[] if body is None else re.findall(r'PlaySE12WithPanning\((SE_\w+), BattleAnimAdjustPanning\((SOUND_PAN_\w+)\)\);',body[1])
  if len(sounds)!=1 or sounds[0][1]!='SOUND_PAN_TARGET':raise ValueError('Bullet seed callback sound differs')
  dependencies.append(dict(op='playsewithpan',args=list(sounds[0])))
 if any(c['op']=='createsprite' and c['args'][0] in {'gLockOnTargetSpriteTemplate','gLockOnMoveTargetSpriteTemplate'} for c in commands):
  if focus_source is None:focus_source=elemental.read(reference,'src/battle_anim_effects_1.c').decode()
  for name,expected in [('AnimLockOnTarget_Step1','SE_M_LOCK_ON'),('AnimLockOnTarget_Step4','SE_M_LEER')]:
   body=re.search(r'static void '+name+r'\(struct Sprite\* sprite\)\s*\{(.*?)\n\}',focus_source,re.S)
   sounds=[] if body is None else re.findall(r'PlaySE12WithPanning\((SE_\w+), BattleAnimAdjustPanning\((SOUND_PAN_\w+)\)\);',body[1])
   if sounds!=[(expected,'SOUND_PAN_TARGET')]:raise ValueError('Lock-On callback sound differs: '+name)
   dependencies.append(dict(op='playsewithpan',args=list(sounds[0])))
 if any(c['op']=='createsprite' and c['args'][0]=='gPencilSpriteTemplate' for c in commands):
  body=re.search(r'static void AnimPencil_Step\(struct Sprite \*sprite\)\s*\{(.*?)\n\}',source,re.S)
  cues=[] if body is None else re.findall(r'PlaySE12WithPanning\((SE_\w+),',body[1])
  if cues!=['SE_M_SKETCH']:raise ValueError('Pencil callback sound differs')
  dependencies.extend(dict(op='playse',args=[cue]) for cue in cues)
 if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_AirCutterProjectile' for c in commands):
  body=re.search(r'static void AirCutterProjectile_Step1\(u8 taskId\)\s*\{(.*?)\n\}',source,re.S)
  if body is None or 'PlaySE12WithPanning(SE_M_BLIZZARD2, BattleAnimAdjustPanning(-SOUND_PAN_TARGET));' not in body[1]:raise ValueError('Air Cutter callback sound differs')
  dependencies.append(dict(op='playse',args=['SE_M_BLIZZARD2']))
 return elemental.sound_effects(reference,dependencies)

def terrain_tail_program(commands,raw):
 # Preserve source program boundaries. The source initializer writes argument 0
 # and destroys synchronously; goto changes only the script pointer.
 def body(path,name):
  match=re.search(r'(?:static )?void '+name+r'\([^)]*\)\s*\{(.*?)\n\}',raw[path].decode(),re.S)
  if match is None:raise ValueError('Missing source selector ABI '+name)
  return re.sub(r'\s+','',match[1])
 if body('src/battle_anim_utility_funcs.c','AnimTask_GetBattleTerrain')!='gBattleAnimArgs[0]=gBattleTerrain;DestroyAnimVisualTask(taskId);':raise ValueError('Source terrain selector ABI differs')
 if body('src/battle_anim.c','Cmd_goto')!='sBattleAnimScriptPtr++;sBattleAnimScriptPtr=T2_READ_PTR(sBattleAnimScriptPtr);':raise ValueError('Source tail pointer transfer differs')
 expected='u8argId;s16valueToCheck;sBattleAnimScriptPtr++;argId=sBattleAnimScriptPtr[0];valueToCheck=T1_READ_16(sBattleAnimScriptPtr+1);if(valueToCheck==gBattleAnimArgs[argId])sBattleAnimScriptPtr=T2_READ_PTR(sBattleAnimScriptPtr+3);elsesBattleAnimScriptPtr+=7;'
 if body('src/battle_anim.c','Cmd_jumpargeq')!=expected:raise ValueError('Source conditional tail pointer transfer differs')
 expected='TaskFunctaskFunc;u8taskPriority;u8taskId;u8numArgs;s32i;sBattleAnimScriptPtr++;taskFunc=(TaskFunc)T2_READ_32(sBattleAnimScriptPtr);sBattleAnimScriptPtr+=4;taskPriority=sBattleAnimScriptPtr[0];sBattleAnimScriptPtr++;numArgs=sBattleAnimScriptPtr[0];sBattleAnimScriptPtr++;for(i=0;i<numArgs;i++){gBattleAnimArgs[i]=T1_READ_16(sBattleAnimScriptPtr);sBattleAnimScriptPtr+=2;}taskId=CreateTask(taskFunc,taskPriority);taskFunc(taskId);gAnimVisualTaskCount++;'
 if body('src/battle_anim.c','Cmd_createvisualtask')!=expected:raise ValueError('Source synchronous task initializer differs')
 terrain={k:int(v) for k,v in re.findall(r'^#define (BATTLE_TERRAIN_\w+)\s+(\d+)\s*$',raw['include/constants/battle.h'].decode(),re.M)}
 native=['GRASS','LONG_GRASS','SAND','UNDERWATER','WATER','POND','MOUNTAIN','CAVE','BUILDING','PLAIN']
 if [terrain.get('BATTLE_TERRAIN_'+name) for name in native]!=list(range(10)):raise ValueError('Source native terrain constants differ')
 if len(commands)!=11 or commands[0]['op']!='createvisualtask' or commands[0]['args']!=['AnimTask_GetBattleTerrain','5'] or commands[-1]['op']!='goto' or len(commands[-1]['args'])!=1:raise ValueError('Source terrain tail selector shape differs')
 targets=[]
 for i,command in enumerate(commands[1:-1]):
  args=command['args']
  if command['op']!='jumpargeq' or len(args)!=3 or args[:2]!=['0','BATTLE_TERRAIN_'+native[i]]:raise ValueError('Source terrain comparison ABI differs')
  command['args']=['0',str(terrain[args[1]]),args[2]];targets.append(args[2])
 targets.append(commands[-1]['args'][0]);source=raw['data/battle_anim_scripts.s'].decode()
 if len(set(targets))!=len(targets) or any(not name.startswith('Move_') or not re.search(r'^'+re.escape(name)+r':\n',source,re.M) for name in targets):raise ValueError('Source tail reference is missing or duplicated')
 return targets

def build(reference):
 # Complete programs already owned by the background graph producer keep that
 # authority when a shared branch opcode becomes available here. Derive this
 # set from source closure, not a second move-ID inventory or generated output.
 background=module('physical_background_owner',ROOT/'scripts/imports/battle/import-source-background-scripts.py')
 background_roots={row['moveId'] for row in json.loads(background.build(reference))['scripts']}
 raw=read_sources(reference);validate_reaction_sources(raw);validate_friendship_predicates(raw);validate_orb_defense_callbacks(raw);validate_charged_root_callbacks(raw);validate_solar_pincer_callbacks(raw);weather_constants=validate_weather_ball_sources(raw);source=raw['data/battle_anim_scripts.s'].decode()
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
     args=['7',str(weather_constants.get(args[0],args[0])),args[1]];op='jumpargeq'
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
    args=['7',str(weather_constants.get(args[0],args[0])),args[1]];op='jumpargeq'
   if op in {'jumprettrue','jumpretfalse'}:
    if len(args)!=1:raise ValueError('Source predicate boolean operands differ')
    args=['7','1' if op=='jumprettrue' else '0',args[0]];op='jumpargeq'
   if op=='createvisualtask' and args and args[0]=='AnimTask_AirCutterProjectile':
    if len(args)!=7 or args[4]!='6 * 256':raise ValueError('Air Cutter source speed expression differs')
    args[4]=str(6*256)
   result.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
   if op in {'choosetwoturnanim','goto','end','return'}:return result,local
  raise ValueError('Unterminated source block '+label)
 def branch_targets(commands):
  return [target for c in commands for target in (c['args'] if c['op']=='choosetwoturnanim' else c['args'][:1] if c['op']=='goto' else c['args'][1:2] if c['op']=='jumpifmoveturn' else c['args'][2:3] if c['op']=='jumpargeq' else [])]
 for match in re.finditer(r'^(Move_(\w+)):\n',source,re.M):
  commands=[];gaps=set();symbols=set();labels={};program_references=[]
  try:
   commands,labels=source_block(match[1]);parsed={match[1]}
   if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_GetBattleTerrain' for c in commands):program_references=terrain_tail_program(commands,raw)
   pending=[target for target in branch_targets(commands) if target not in program_references]
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
    for target in branch_targets([command]):
     if target not in program_references:visit(0 if target==match[1] else labels[target])
    if op not in {'goto','choosetwoturnanim','end','return'}:visit(pc+1)
    active.remove(pc);done.add(pc)
   visit(0)
  except ValueError:gaps.add('control-flow:unterminated')
  for command in commands:
   op=command['op'];args=command['args']
   if op=='createsoundtask' and not elemental.supported_sound_task(args,elemental.sound_effects(reference,[command])):gaps.add('sound-task:unreviewed')
   if op not in hit.OPS|{'monbg_static','clearmonbg_static','unloadspritegfx','splitbgprio_all','playse','waitsound','panse_adjustall','panse','fadetobg','fadetobgfromset','waitplaysewithpan','call','splitbgprio','goto','choosetwoturnanim','jumpifmoveturn','restorebg','waitbgfadein','waitbgfadeout','setarg','jumpargeq','createsoundtask','visible'}:gaps.add('command:'+op)
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
    widths={'AnimTask_GetWeather':2,**PRESENT_WIDTHS,**PAIN_SPLIT_WIDTHS,**DAMAGE_FAMILY_WIDTHS,**dict.fromkeys(EXTRASENSORY_TASKS,3),'AnimTask_FrozenIceCube':2,'AnimTask_GetBattleTerrain':2,'AnimTask_CreateSurfWave':3,**EARTH_BACKGROUND_WIDTHS,**SCANLINE_BATTLER_WIDTHS,**GHOST_SONG_WIDTHS,**ITEM_WIND_WIDTHS,**FOCUS_SOUND_WIDTHS,'AnimTask_GetRolloutCounter':3,'AnimTask_CreateSmallSolarBeamOrbs':2,'AnimTask_StatusClearedEffect':3,'AnimTask_LoadMusicNotesPals':2,'AnimTask_FreeMusicNotesPals':2,'AnimTask_SpitUpDeformMon':2,'AnimTask_GetTargetIsAttackerPartner':3,'AnimTask_AttackerFadeToInvisible':3,'AnimTask_AttackerFadeFromInvisible':3,'AnimTask_BlendBattleAnimPalExclude':7,**dict.fromkeys(ORB_DEFENSE_TASKS,2),**CHARGE_PAIR_WIDTHS,**dict.fromkeys(LOCKED_PAIR_TASKS,2),**dict.fromkeys(CUT_WALL_TASKS,2),'AnimTask_ShakeMonInPlace':7,'AnimTask_FlailMovement':3,'AnimTask_GrowAndGrayscale':2,**dict.fromkeys(FRIENDSHIP_CONTACT_TASKS,2),'AnimTask_TraceMonBlended':6,'AnimTask_SquishAndSweatDroplets':4,'AnimTask_FacadeColorBlend':4,'AnimTask_SmellingSaltsSquish':4,'AnimTask_LeafBlade':2,'AnimTask_HorizontalShake':5,'AnimTask_ShakeTargetBasedOnMovePowerOrDmg':7,'AnimTask_IsPowerOver99':2,'GetIsDoomDesireHitTurn':2,'AnimTask_FakeOut':2,'AnimTask_CycleMagicalLeafPal':2,'AnimTask_MoveSkyUppercutBg':3,'AnimTask_StretchTargetUp':2}
    if op=='createvisualtask' and args[0] in widths and len(args)!=widths[args[0]]:gaps.add('callback-arguments:'+args[0])
    sprite_widths={**WEATHER_BALL_WIDTHS,**SUPERPOWER_WIDTHS,**PRESENT_WIDTHS,**PAIN_SPLIT_WIDTHS,**DAMAGE_FAMILY_WIDTHS,**EARTH_BACKGROUND_WIDTHS,**SCANLINE_BATTLER_WIDTHS,**GHOST_SONG_WIDTHS,**ITEM_WIND_WIDTHS,**FOCUS_SOUND_WIDTHS,'gIceBallChunkSpriteTemplate':9,'gIceBallImpactShardSpriteTemplate':5,'gSolarBeamBigOrbSpriteTemplate':7,'gGuillotineSpriteTemplate':4,**CURE_WIDTHS,'gSkyAttackBirdSpriteTemplate':3,**ORB_DEFENSE_WIDTHS,**ROUND_SHADOW_WIDTHS,**CHARGE_PAIR_WIDTHS,'gFocusPunchFistSpriteTemplate':3,'gTwisterLeafSpriteTemplate':8,'gTwisterRockSpriteTemplate':8,'gBrickBreakWallSpriteTemplate':8,'gBrickBreakWallShardSpriteTemplate':7,'gRandomPosHitSplatSpriteTemplate':5,'gAncientPowerRockSpriteTemplate':8,'gRockTombRockSpriteTemplate':8,'gRedXSpriteTemplate':5,'gSpinningSparkleSpriteTemplate':5,'gLickSpriteTemplate':5,'gMovementWavesSpriteTemplate':6,'gWeakFrustrationAngerMarkSpriteTemplate':5,'gTriAttackTriangleSpriteTemplate':5,'gSmellingSaltsHandSpriteTemplate':6,'gSmellingSaltExclamationSpriteTemplate':6}
    if op=='createsprite' and args[0] in sprite_widths and len(args)!=sprite_widths[args[0]]:gaps.add('callback-arguments:'+args[0])
   if op=='createsprite' and args[0]=='gSpitUpOrbSpriteTemplate' and len(args) not in {4,5}:gaps.add('callback-arguments:'+args[0])
   if op in {'loadspritegfx','unloadspritegfx'} and args[0] not in {'ANIM_TAG_WEATHER_BALL','ANIM_TAG_HAIL','ANIM_TAG_CIRCLE_OF_LIGHT','ANIM_TAG_METEOR','ANIM_TAG_FLAT_ROCK','ANIM_TAG_EXPLOSION','ANIM_TAG_GREEN_SPARKLE','ANIM_TAG_PAIN_SPLIT','ANIM_TAG_PURPLE_SCRATCH','ANIM_TAG_PURPLE_SWIPE','ANIM_TAG_RAPID_SPIN','ANIM_TAG_AIR_WAVE','ANIM_TAG_ICE_CUBE','ANIM_TAG_PENCIL','ANIM_TAG_DIRT_MOUND','ANIM_TAG_HOLLOW_ORB','ANIM_TAG_WHITE_SHADOW','ANIM_TAG_NAIL','ANIM_TAG_GHOSTLY_SPIRIT','ANIM_TAG_ITEM_BAG','ANIM_TAG_SPEED_DUST','ANIM_TAG_RECYCLE','ANIM_TAG_SPARKLE_6','ANIM_TAG_LOCK_ON','ANIM_TAG_TEAL_ALERT','ANIM_TAG_OPENING_EYE','ANIM_TAG_ROUND_WHITE_HALO','ANIM_TAG_SNORE_Z','ANIM_TAG_JAGGED_MUSIC_NOTE','ANIM_TAG_PINK_CLOUD','ANIM_TAG_BLUEGREEN_ORB','ANIM_TAG_SPIKES','ANIM_TAG_PURPLE_FLAME','ANIM_TAG_ICE_CHUNK','ANIM_TAG_ORBS','ANIM_TAG_FLOWER','ANIM_TAG_BELL','ANIM_TAG_MUSIC_NOTES_2','ANIM_TAG_THIN_RING','ANIM_TAG_BIRD','ANIM_TAG_RED_ORB_2','ANIM_TAG_ROUND_SHADOW','ANIM_TAG_SPLASH','ANIM_TAG_WATER_IMPACT','ANIM_TAG_SMALL_BUBBLES','ANIM_TAG_PROTECT','ANIM_TAG_SHADOW_BALL','ANIM_TAG_GRAY_ORB','ANIM_TAG_BLUE_ORB','ANIM_TAG_GUST','ANIM_TAG_AIR_WAVE_2','ANIM_TAG_ROCKS','ANIM_TAG_X_SIGN','ANIM_TAG_SPARKLE_4','ANIM_TAG_LICK','ANIM_TAG_MOVEMENT_WAVES','ANIM_TAG_TRI_ATTACK_TRIANGLE','ANIM_TAG_SWEAT_DROP','ANIM_TAG_TAG_HAND','ANIM_TAG_SMELLINGSALT_EFFECT','ANIM_TAG_FIRE','ANIM_TAG_LIGHTNING','ANIM_TAG_ICE_CRYSTALS','ANIM_TAG_RED_ORB','ANIM_TAG_SWEAT_BEAD','ANIM_TAG_LEAF','ANIM_TAG_RAZOR_LEAF','ANIM_TAG_COIN','ANIM_TAG_CROSS_IMPACT','ANIM_TAG_RED_BALL','ANIM_TAG_WHIRLWIND_LINES','ANIM_TAG_SPARKLE_2','ANIM_TAG_GOLD_STARS','ANIM_TAG_SWIPE','ANIM_TAG_FANG_ATTACK','ANIM_TAG_SEED','ANIM_TAG_ICICLE_SPEAR','ANIM_TAG_NEEDLE','ANIM_TAG_CLAW_SLASH','ANIM_TAG_TORN_METAL','ANIM_TAG_BLUE_LIGHT_WALL','ANIM_TAG_SMALL_EMBER','ANIM_TAG_BONE','ANIM_TAG_EYE_SPARKLE','ANIM_TAG_ANGER','ANIM_TAG_ECLIPSING_ORB','ANIM_TAG_BREATH','ANIM_TAG_BLUE_STAR','ANIM_TAG_MUD_SAND','ANIM_TAG_IMPACT','ANIM_TAG_HANDS_AND_FEET','ANIM_TAG_CUT','ANIM_TAG_SLAM_HIT','ANIM_TAG_WHIP_HIT','ANIM_TAG_POISON_POWDER','ANIM_TAG_SLEEP_POWDER','ANIM_TAG_STUN_SPORE','ANIM_TAG_POISON_BUBBLE'}:gaps.add('graphics:'+args[0])
  # Move-turn commands require an admitted invocation contract as well as pixels.
  # Beat Up uses contributor/hit pairs; charge moves use a turn lock. Their
  # application matchers authenticate these contexts separately from multihit.
  effect=effects.get('MOVE_'+match[2])
  normalize_committed_damage_shake(effect,commands,raw)
  if 'GetIsDoomDesireHitTurn' in symbols and effect!='EFFECT_FUTURE_SIGHT':gaps.add('invocation:move-turn')
  if 'choosetwoturnanim' in symbols and effect not in {'EFFECT_MULTI_HIT','EFFECT_BEAT_UP','EFFECT_PURSUIT','EFFECT_BRICK_BREAK','EFFECT_BIDE','EFFECT_RAZOR_WIND','EFFECT_SKULL_BASH','EFFECT_SEMI_INVULNERABLE','EFFECT_SKY_ATTACK','EFFECT_SOLAR_BEAM','EFFECT_CURSE'}:gaps.add('invocation:move-turn')
  if 'jumpifmoveturn' in symbols and effect not in {'EFFECT_TRIPLE_KICK','EFFECT_SWALLOW','EFFECT_SPIT_UP'}:gaps.add('invocation:move-turn')
  if not program_references and not any(c['op']=='end' for c in commands):gaps.add('control-flow:unterminated')
  if len(commands)>128:gaps.add('command-bound')
  local_subs={};local_labels={}
  for command in commands:
   if command['op']!='call':continue
   try:sub,sub_labels=subcommands(command['args'][0])
   except ValueError:gaps.add('control-flow:subroutine');continue
   for row in sub:
    if row['op'] in {'fadetobg','restorebg','waitbgfadein','waitbgfadeout','setarg'} and 'choosetwoturnanim' not in symbols and 'AnimTask_SkillSwap' not in symbols:gaps.add('background:shared-owner')
    if row['op'] not in hit.OPS|{'return','waitplaysewithpan','goto','jumpargeq','changebg','fadetobg','restorebg','waitbgfadein','waitbgfadeout','setarg'}:gaps.add('command:'+row['op'])
    if row['op'] in {'createsprite','createvisualtask'}:
     symbols.add(row['args'][0])
     if row['args'][0] in (ORB_DEFENSE_WIDTHS|CHARGE_PAIR_WIDTHS|ROUND_SHADOW_WIDTHS|FOCUS_SOUND_WIDTHS|GHOST_SONG_WIDTHS|SCANLINE_BATTLER_WIDTHS|EARTH_BACKGROUND_WIDTHS) and len(row['args'])!=(ORB_DEFENSE_WIDTHS|CHARGE_PAIR_WIDTHS|ROUND_SHADOW_WIDTHS|FOCUS_SOUND_WIDTHS|GHOST_SONG_WIDTHS|SCANLINE_BATTLER_WIDTHS|EARTH_BACKGROUND_WIDTHS)[row['args'][0]]:gaps.add('callback-arguments:'+row['args'][0])
     if row['args'][0] not in (SPRITES if row['op']=='createsprite' else TASKS):gaps.add('callback:'+row['args'][0])
   if any(row['op']=='changebg' and (len(row['args'])!=1 or row['args'][0] not in {'BG_IMPACT_OPPONENT','BG_IMPACT_PLAYER'}) for row in sub):gaps.add('background:unreviewed')
   local_subs[command['args'][0]]=sub
   if sub_labels:local_labels[command['args'][0]]=sub_labels
  # A family's source callback may live entirely in a called helper.
  if not symbols & NEW:continue
  move=ids.get('MOVE_'+match[2])
  if not move or move>354 or move in background_roots:continue
  if gaps:blocked.append(dict(moveId=move,missing=sorted(gaps)));continue
  dependencies=commands+[c for sub in local_subs.values() for c in sub]
  elemental.visibility_contracts(reference,dependencies)
  if any(c['op']=='fadetobg' and c['args'][0] not in ({'BG_SKY','BG_SOLAR_BEAM_OPPONENT','BG_SOLAR_BEAM_PLAYER'}|({'BG_PSYCHIC'} if 'AnimTask_SkillSwap' in symbols else set())) for sub in local_subs.values() for c in sub):
   blocked.append(dict(moveId=move,missing=['background:unreviewed']));continue
  loaded=sorted({c['args'][0] for c in dependencies if c['op'] in {'loadspritegfx','unloadspritegfx'}})
  if not set(loaded)<=tags.keys():raise ValueError('Undefined source sprite tag dependency')
  children=sorted(set((['gSpeedDustSpriteTemplate'] if 'AnimTask_SpeedDust' in symbols else [])+(['gAirWaveProjectileSpriteTemplate'] if 'AnimTask_AirCutterProjectile' in symbols else [])+(['sFrozenIceCubeSpriteTemplate'] if 'AnimTask_FrozenIceCube' in symbols else [])+(['gDestinyBondWhiteShadowSpriteTemplate'] if 'AnimTask_DestinyBondWhiteShadow' in symbols else [])+(['gSolarBeamSmallOrbSpriteTemplate'] if 'AnimTask_CreateSmallSolarBeamOrbs' in symbols else [])+(['gBasicHitSplatSpriteTemplate'] if 'gFistFootRandomPosSpriteTemplate' in symbols else [])+(['gFlashingHitSplatSpriteTemplate'] if 'AnimTask_DrillPeckHitSplats' in symbols else [])+(['gBarrageBallSpriteTemplate'] if 'AnimTask_BarrageBall' in symbols else [])+(['gFacadeSweatDropSpriteTemplate'] if 'AnimTask_SquishAndSweatDroplets' in symbols else [])+(['gLeafBladeSpriteTemplate'] if 'AnimTask_LeafBlade' in symbols else [])+(['sSkillSwapOrbSpriteTemplate'] if 'AnimTask_SkillSwap' in symbols else [])+(['gGrudgeFlameSpriteTemplate'] if 'AnimTask_GrudgeFlames' in symbols else [])))
  scripts.append(dict(moveId=move,label=match[1],commands=commands,labels=labels,**({'programReferences':program_references} if program_references else {}),**({'spriteDependencies':children} if children else {}),**({'materialDependencies':sorted(({'whirlwind-lines'} if 'AnimTask_DrillPeckHitSplats' in symbols else set())|({'speed-dust'} if 'ANIM_TAG_SPEED_DUST' in loaded else set()))} if 'AnimTask_DrillPeckHitSplats' in symbols or 'ANIM_TAG_SPEED_DUST' in loaded else {}),**({'overlayDependencies':(['cure-bubbles'] if 'AnimTask_StatusClearedEffect' in symbols else [])+(['curse'] if 'AnimTask_DrawFallingWhiteLinesOnAttacker' in symbols else [])+(['surf-player','surf-opponent'] if 'AnimTask_CreateSurfWave' in symbols and any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_CreateSurfWave' and c['args'][2] in ('0','FALSE') for c in dependencies) else ['muddy-water-player','muddy-water-opponent'] if 'AnimTask_CreateSurfWave' in symbols else [])} if symbols & {'AnimTask_StatusClearedEffect','AnimTask_DrawFallingWhiteLinesOnAttacker','AnimTask_CreateSurfWave'} else {}),soundEffects=physical_sound_effects(reference,dependencies,raw['src/battle_anim_effects_2.c'].decode(),raw['src/battle_anim_effects_1.c'].decode()),spriteTags={name:tags[name] for name in loaded}));subroutines.update(local_subs);subroutine_labels.update(local_labels);background_symbols.update({c['args'][0]:backgrounds[c['args'][0]] for c in dependencies if c['op'] in {'changebg','fadetobg'}});background_symbols.update({name:backgrounds[name] for c in dependencies if c['op']=='fadetobgfromset' for name in c['args']})
 if not scripts:raise ValueError('No supported physical roots')
 # SmallEmber, like leaf and razor-leaf, is owned by the elemental indexed pack.
 # Physical callbacks retain the shared recipe asset ID without a second RGB declaration.
 return (json.dumps(dict(schemaVersion=1,scripts=scripts,subroutines=subroutines,subroutineLabels=subroutine_labels,backgroundSymbols=background_symbols,taskAffinePrograms={name:task_affine_program(raw['src/'+file].decode(),name) for file,name in [('battle_anim_effects_2.c','sStretchAttackerAffineAnimCmds'),('battle_anim_effects_2.c','sUproarAffineAnimCmds'),('battle_anim_effects_3.c','sDeepInhaleAffineAnimCmds'),('battle_anim_effects_3.c','sSpitUpDeformMonAffineAnimCmds'),('battle_anim_effects_3.c','sStockpileDeformMonAffineAnimCmds'),('battle_anim_effects_3.c','sSwallowDeformMonAffineAnimCmds'),('battle_anim_effects_2.c','sThrashMoveMonAffineAnimCmds'),('battle_anim_effects_3.c','sStrongFrustrationAffineAnimCmds'),('battle_anim_effects_3.c','sFacadeSquishAffineAnimCmds'),('battle_anim_effects_3.c','sSmellingSaltsSquishAffineAnimCmds'),('battle_anim_effects_2.c','sAffineAnims_StretchBattlerUp'),('battle_anim_effects_2.c','sSplashEffectAffineAnimCmds'),('battle_anim_effects_2.c','sGrowAndShrinkAffineAnimCmds'),('battle_anim_effects_3.c','sSlackOffSquishAffineAnimCmds'),('battle_anim_effects_3.c','DefenseCurlDeformMonAffineAnimCmds')]},paletteGroups=[dict(tag=tags['ANIM_TAG_MUSIC_NOTES_2'],path='physical-music-notes-2.pal48',banks=3)],indexedAssets=build_indexed_assets(reference)[0],assets=[dict(id=id,path='physical-'+id+'.rgb555') for id in build_assets(reference)]+[dict(id='sparkle-4',path='walls-sparkle-4.rgb555'),dict(id='rocks',path='elemental-rocks.rgb555'),dict(id='needle',path='elemental-needle.rgb555'),dict(id='poison-bubble',path='elemental-poison-bubble.rgb555'),dict(id='blue-star',path='elemental-blue-star.rgb555')],spriteRecipes=weather_ball_recipes(raw)+[frozen_cube_recipe(raw)]+ghost_song_recipes(raw)+[elemental.recipe(raw['src/'+file].decode(),name,callback) for name,file,callback in SUPERPOWER_RECIPES+PRESENT_RECIPES+PAIN_SPLIT_RECIPES+DAMAGE_FAMILY_RECIPES+EARTH_BACKGROUND_RECIPES+SCANLINE_BATTLER_RECIPES+ITEM_WIND_RECIPES+FOCUS_SOUND_RECIPES+UTILITY_RECIPES+SOLAR_PINCER_RECIPES+CURE_RECIPES+ROUND_SHADOW_RECIPES+CHARGED_ROOT_RECIPES]+[focus_punch_recipe(fight)]+[elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gRazorWindTornadoSpriteTemplate','AnimRazorWindTornado')]+[cut_wall_recipe(raw['src/'+file].decode(),name,callback) for name,file,callback in CUT_WALL_RECIPES]+[elemental.recipe(raw['src/'+file].decode(),name,callback) for name,file,callback in REACTION_RECIPES+FRIENDSHIP_CONTACT_RECIPES+ROCK_STATUS_RECIPES+FLAIL_RECIPES+ORB_DEFENSE_RECIPES]+[elemental.recipe(raw['src/'+file].decode(),name,callback) for name,file,callback in ORBITAL_PARTICLE_RECIPES]+[impact_emission_recipe(raw,*row) for row in IMPACT_EMISSION_RECIPES]+[elemental.recipe(fight,'gFistFootRandomPosSpriteTemplate','AnimFistOrFootRandomPos'),elemental.recipe(raw['src/battle_anim_normal.c'].decode(),'gBasicHitSplatSpriteTemplate','AnimHitSplatBasic'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gWishStarSpriteTemplate','AnimWishStar'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gMeteorMashStarSpriteTemplate','AnimMeteorMashStar'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gMiniTwinklingStarSpriteTemplate','AnimMiniTwinklingStar'),elemental.recipe(raw['src/battle_anim_fight.c'].decode(),'gArmThrustHandSpriteTemplate','AnimArmThrustHit'),elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gFurySwipesSpriteTemplate','AnimFurySwipes'),elemental.recipe(raw['src/battle_anim_effects_3.c'].decode(),'gFangSpriteTemplate','AnimFang'),elemental.recipe(raw['src/battle_anim_effects_1.c'].decode(),'gSuperFangSpriteTemplate','AnimSuperFang'),elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gBulletSeedSpriteTemplate','AnimBulletSeed'),elemental.recipe(raw['src/battle_anim_ground.c'].decode(),'gSandAttackDirtSpriteTemplate','AnimDirtScatter'),elemental.recipe(raw['src/battle_anim_ground.c'].decode(),'gMudSlapMudSpriteTemplate','AnimDirtScatter'),elemental.recipe(fight,'gMegaPunchKickSpriteTemplate','AnimSpinningKickOrPunch'),elemental.recipe(fight,'gSpinningHandOrFootSpriteTemplate','AnimSpinningKickOrPunch'),elemental.recipe(raw['src/battle_anim_fire.c'].decode(),'gFireSpreadSpriteTemplate','AnimFireSpread'),elemental.recipe(raw['src/battle_anim_bug.c'].decode(),'gPinMissileSpriteTemplate','AnimMissileArc'),elemental.recipe(raw['src/battle_anim_bug.c'].decode(),'gIcicleSpearSpriteTemplate','AnimMissileArc'),elemental.recipe(raw['src/battle_anim_bug.c'].decode(),'gLinearStingerSpriteTemplate','AnimTranslateStinger'),elemental.recipe(raw['src/battle_anim_normal.c'].decode(),'gHandleInvertHitSplatSpriteTemplate','AnimHitSplatHandleInvert'),elemental.recipe(raw['src/battle_anim_poison.c'].decode(),'gPoisonBubbleSpriteTemplate','AnimBubbleEffect'),elemental.recipe(raw['src/battle_anim_dark.c'].decode(),'gClawSlashSpriteTemplate','AnimClawSlash'),elemental.recipe(raw['src/battle_anim_rock.c'].decode()+raw['src/battle_anim_fire.c'].decode(),'gFireSpinSpriteTemplate','AnimParticleInVortex'),elemental.recipe(raw['src/battle_anim_ground.c'].decode(),'gSpinningBoneSpriteTemplate','AnimBoneHitProjectile'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gEyeSparkleSpriteTemplate','eye-sparkle','AnimEyeSparkle'),elemental.recipe(raw['src/battle_anim_effects_2.c'].decode(),'gAngerMarkSpriteTemplate','AnimAngerMark'),sprite_recipe(raw['src/battle_anim_effects_3.c'].decode(),'gEclipsingOrbSpriteTemplate','eclipsing-orb','AnimSpriteOnMonPos'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gBreathPuffSpriteTemplate','breath','AnimBreathPuff'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gHealingBlueStarSpriteTemplate','blue-star','AnimSpriteOnMonPos'),sprite_recipe(raw['src/battle_anim_ground.c'].decode(),'gMudsportMudSpriteTemplate','mud-sand','AnimMudSportDirt'),sprite_recipe(raw['src/battle_anim_effects_2.c'].decode(),'gViceGripSpriteTemplate','cut','AnimViceGripPincer'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gSlamHitSpriteTemplate','slam-hit','AnimWhipHit'),sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),'gVineWhipSpriteTemplate','whip-hit','AnimWhipHit')]+[sprite_recipe(raw['src/battle_anim_effects_1.c'].decode(),template,asset,'AnimMovePowderParticle') for template,asset in [('gPoisonPowderParticleSpriteTemplate','poison-powder'),('gSleepPowderParticleSpriteTemplate','sleep-powder'),('gStunSporeParticleSpriteTemplate','stun-spore')]]),indent=2)+'\n').encode(),blocked
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/physical.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,blocked=build(a.reference);assets={**build_assets(a.reference),**build_indexed_assets(a.reference)[1]}
 if a.check:
  if a.output.read_bytes()!=data:raise SystemExit('Physical animation pack differs')
  for id,blob in assets.items():
   if (a.output.parent/('physical-'+id if id.endswith(('.4bpp','.pal16','.pal48')) else 'physical-'+id+'.rgb555')).read_bytes()!=blob:raise SystemExit('Physical sprite pixels differ')
 else:
  a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_bytes(data)
  for id,blob in assets.items():(a.output.parent/('physical-'+id if id.endswith(('.4bpp','.pal16','.pal48')) else 'physical-'+id+'.rgb555')).write_bytes(blob)
 print(json.dumps(dict(passed=True,roots=[s['moveId'] for s in json.loads(data)['scripts']],blocked=blocked)))
