#!/usr/bin/env python3
"""Convert reviewed elemental script roots and sprite recipes from pinned pret inputs."""
import argparse, importlib.util, io, json, re, subprocess
from PIL import Image
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('elemental_hit',ROOT/'scripts/imports/battle/import-source-hit-animations.py');hit=importlib.util.module_from_spec(spec);spec.loader.exec_module(hit)
TEMPLATES={
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
 match=re.search(r'const struct SpriteTemplate '+name+r' =\s*\{(.*?)\};',source,re.S)
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
    f=re.fullmatch(r'ANIMCMD_FRAME\((\d+), (\d+)((?:, \.[hv]Flip = TRUE)*)\)',row)
    if f:frames.append(dict(tile=int(f[1]),duration=int(f[2]),hFlip='.hFlip' in f[3],vFlip='.vFlip' in f[3]))
    elif re.fullmatch(r'ANIMCMD_JUMP\((\d+)\)',row):loop=int(re.fullmatch(r'ANIMCMD_JUMP\((\d+)\)',row)[1])
    elif row!='ANIMCMD_END':raise ValueError('Unsupported frame '+row)
   if not frames or loop>=len(frames):raise ValueError('Invalid frame loop')
   animations.append(frames);loops.append(loop)
 affine=[]
 if shape[1]!='Off' and fields['affineAnims']!='gDummySpriteAffineAnimTable':
  table=re.search(r'const union AffineAnimCmd \*const '+fields['affineAnims']+r'\[\] =\s*\{(.*?)\};',source,re.S)
  if not table:raise ValueError('Missing affine table '+fields['affineAnims'])
  for array in re.findall(r'\b([sg]\w+)\s*,',table[1]):
   body=re.search(r'const union AffineAnimCmd '+array+r'\[\] =\s*\{(.*?)\};',source,re.S)[1];commands=[]
   for row in body.splitlines():
    row=row.strip().rstrip(',')
    if not row:continue
    f=re.fullmatch(r'AFFINEANIMCMD_FRAME\(([^)]+)\)',row)
    j=re.fullmatch(r'AFFINEANIMCMD_JUMP\((\d+)\)',row)
    if f:
     x,y,rotation,duration=[int(v.strip(),0) for v in f[1].split(',')];commands.append(dict(op='frame',x=x,y=y,rotation=rotation&255,duration=duration&255,target=0))
    elif j:commands.append(dict(op='jump',x=0,y=0,rotation=0,duration=0,target=int(j[1])))
    elif row=='AFFINEANIMCMD_END' or re.fullmatch(r'AFFINEANIMCMD_END_ALT\(\d+\)',row):commands.append(dict(op='end',x=0,y=0,rotation=0,duration=0,target=0))
    else:raise ValueError('Unsupported affine operation '+row)
   if not commands or commands[-1]['op'] not in ('jump','end'):raise ValueError('Unterminated affine animation')
   affine.append(commands)
 return dict(template=name,asset=asset,callback=callback,blend=shape[2]=='Blend',width=int(shape[3]),height=int(shape[4]),animations=animations,loopStarts=loops,affineAnimations=affine,affineDoubleSize=shape[1]=='Double')
SHARED_SPRITES={'gShakeMonOrTerrainSpriteTemplate', 'gGrantingStarsSpriteTemplate', 'gSimplePaletteBlendSpriteTemplate', 'gSlideMonToOriginalPosSpriteTemplate', 'gComplexPaletteBlendSpriteTemplate', 'gFistFootSpriteTemplate', 'gSlideMonToOffsetSpriteTemplate', 'gBasicHitSplatSpriteTemplate'}
TASKS={'AnimTask_ShakeMon', 'AnimTask_InvertScreenColor', 'AnimTask_BlendBattleAnimPal', 'AnimTask_StartSinAnimTimer', 'AnimTask_SwayMon', 'AnimTask_BlendColorCycle', 'AnimTask_ElectricBolt', 'AnimTask_ShakeMon2'}
SOUNDS={'SE_M_MEGA_KICK2', 'SE_M_CRABHAMMER', 'SE_M_VITAL_THROW2', 'SE_M_HYDRO_PUMP', 'SE_M_FLAME_WHEEL', 'SE_M_HYPER_BEAM', 'SE_M_SCREECH', 'SE_M_ABSORB_2', 'SE_M_COMET_PUNCH', 'SE_M_SUPERSONIC', 'SE_M_TAIL_WHIP', 'SE_M_THUNDERBOLT2', 'SE_M_ABSORB', 'SE_M_GIGA_DRAIN', 'SE_M_EMBER', 'SE_M_TRI_ATTACK2', 'SE_M_FLAMETHROWER', 'SE_M_BUBBLE_BEAM', 'SE_M_THUNDERBOLT', 'SE_M_THUNDER_WAVE', 'SE_M_FIRE_PUNCH', 'SE_M_SELF_DESTRUCT', 'SE_M_BUBBLE3', 'SE_M_BUBBLE', 'SE_M_WHIRLPOOL', 'SE_M_MEGA_KICK'}
GFX={'ANIM_TAG_SPARK', 'ANIM_TAG_METAL_SOUND_WAVES', 'ANIM_TAG_BROWN_ORB', 'ANIM_TAG_LIGHTNING', 'ANIM_TAG_SPARK_2', 'ANIM_TAG_LARGE_FRESH_EGG', 'ANIM_TAG_ICE_CRYSTALS', 'ANIM_TAG_DUCK', 'ANIM_TAG_PURPLE_RING', 'ANIM_TAG_GLOWY_RED_ORB', 'ANIM_TAG_IMPACT', 'ANIM_TAG_SPARK_H', 'ANIM_TAG_BLACK_BALL_2', 'ANIM_TAG_SMALL_EMBER', 'ANIM_TAG_GLOWY_GREEN_ORB', 'ANIM_TAG_WATER_IMPACT', 'ANIM_TAG_BLUE_STAR', 'ANIM_TAG_WATER_ORB', 'ANIM_TAG_HANDS_AND_FEET', 'ANIM_TAG_GRAY_SMOKE', 'ANIM_TAG_SMALL_BUBBLES', 'ANIM_TAG_ORBS', 'ANIM_TAG_BLACK_BALL', 'ANIM_TAG_GOLD_RING', 'ANIM_TAG_EXPLOSION', 'ANIM_TAG_SHOCK_3'}
GFX|={'ANIM_TAG_TOXIC_BUBBLE','ANIM_TAG_POISON_BUBBLE','ANIM_TAG_NEEDLE','ANIM_TAG_BUBBLE'}
SOUNDS|={'SE_M_TOXIC','SE_M_BUBBLE2'}
GFX|={'ANIM_TAG_MUD_SAND'}
SOUNDS|={'SE_M_SACRED_FIRE2','SE_M_SAND_TOMB','SE_M_WHIRLPOOL'}
GFX|={'ANIM_TAG_SHARP_TEETH','ANIM_TAG_CLAMP'}
SOUNDS|={'SE_M_BITE','SE_M_VICEGRIP'}
GFX|={'ANIM_TAG_FOCUS_ENERGY'}
SOUNDS|={'SE_M_DRAGON_RAGE'}
SOUNDS|={'SE_M_GUST','SE_M_GUST2','SE_M_STRING_SHOT','SE_M_ICY_WIND','SE_M_BUBBLE_BEAM2'}
GFX|={'ANIM_TAG_FIRE_PLUME'}
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
GFX|={'ANIM_TAG_BLUE_RING','ANIM_TAG_SPARKLE_2','ANIM_TAG_SPORE','ANIM_TAG_ROCKS'}
SOUNDS|={'SE_M_ROCK_THROW'}
TASKS|={'AnimTask_SporeDoubleBattle'}
SOUNDS|={'SE_M_SWAGGER','SE_M_MINIMIZE'}
SOUNDS|={'SE_M_PSYBEAM','SE_M_PSYBEAM2','SE_M_TELEPORT'}
def sound_effects(reference,commands):
 definitions={name:int(value) for name,value in re.findall(r'^#define (SE_\w+)\s+(\d+)\b',read(reference,'include/constants/songs.h').decode(),re.M)}
 result=set()
 for command in commands:
  op,args=command['op'],command['args']
  if op in {'playsewithpan','loopsewithpan','panse','waitplaysewithpan'}:result.add(args[0])
  elif op=='createvisualtask' and args[0] in {'SoundTask_PlaySE1WithPanning','SoundTask_PlaySE2WithPanning'}:result.add(args[2])
  elif op=='createsoundtask':
   if args[0]=='SoundTask_LoopSEAdjustPanning':result.add(args[1])
   elif args[0]=='SoundTask_FireBlast':result.update(args[1:3])
   else:raise ValueError('Unreviewed source sound-task dependencies')
 if not result<=definitions.keys():raise ValueError('Undefined source sound dependency: '+','.join(sorted(result-definitions.keys())))
 return {symbol:definitions[symbol] for symbol in sorted(result)}
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
   if op not in hit.OPS|{'call','return','splitbgprio','panse','waitplaysewithpan','splitbgprio_foes','createsoundtask','waitsound'}:raise ValueError('Unsupported command '+op)
   result.append(dict(op=op,args=args,line=source[:m.end()].count('\n')+offset+1))
   if op==end:return result
  raise ValueError('Unterminated '+label)
 # Discover complete source roots only when every dependency has a reviewed adapter.
 # Existing explosion roots retain their dedicated catalog and multi-battler semantics.
 ids={name:int(value) for name,value in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',read(reference,'include/constants/moves.h').decode(),re.M)}
 sprites=set(TEMPLATES)|SHARED_SPRITES
 tasks=TASKS
 sounds=SOUNDS
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
  if not any(c['op']=='createsprite' and c['args'][0] in TEMPLATES or c['op']=='createvisualtask' and c['args'][0] in {'AnimTask_ElectricBolt','AnimTask_SetGrayscaleOrOriginalPal'} for c in flat):continue
  if any(c['op']=='createsoundtask' and not (c['args'][0]=='SoundTask_LoopSEAdjustPanning' and c['args'][1]=='SE_M_BUBBLE_BEAM2' or c['args']==['SoundTask_FireBlast','SE_M_FLAME_WHEEL','SE_M_FLAME_WHEEL2']) or c['op']=='createsprite' and c['args'][0] not in sprites or c['op']=='createvisualtask' and c['args'][0] not in tasks or c['op']=='loadspritegfx' and c['args'][0] not in gfx or c['op'] in {'playsewithpan','loopsewithpan','panse','waitplaysewithpan'} and c['args'][0] not in sounds for c in flat):continue
  # Spark's zero modulus requires original ARM helper semantics before admission.
  if any(c['op']=='createsprite' and c['args'][0]=='gSparkElectricityFlashingSpriteTemplate' and int(c['args'][-1],0)&32767==0 for c in flat):continue
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=root,soundEffects=sound_effects(reference,flat)));subs.update(local)
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
 for asset,png,shape,pal in [('blue-ring','gold_ring',(16,32),'blue_ring'),('explosion-6','explosion_6',(32,128),None),('leaf','leaf',(16,144),None),('razor-leaf','razor_leaf',(32,16),None),('yellow-star','yellow_star',(32,32),None),('magenta-heart','magenta_heart',(16,16),None),('fire-plume','fire_plume',(32,160),'fire'),('focus-energy','focus_energy',(16,128),None),('sharp-teeth','sharp_teeth',(64,64),None),('clamp','clamp',(64,64),'sharp_teeth'),('toxic-bubble','toxic_bubble',(16,128),'poison_bubble'),('poison-bubble','poison_bubble',(16,48),None),('needle','needle',(16,16),None),('bubble','bubble',(16,48),'rain_drops'),('gold-ring','gold_ring',(16,32),None),('purple-ring','gold_ring',(16,32),'purple_ring'),('metal-sound-waves','metal_sound_waves',(32,64),None),('blue-star','blue_star',(32,224),None),('orbs','orbs',(16,48),None),('small-ember','small_ember',(32,160),None),('water-orb','water_orb',(16,64),'water_impact'),('brown-orb','water_orb',(16,64),'brown_orb'),('glowy-red-orb','glowy_red_orb',(8,8),None),('glowy-green-orb','glowy_red_orb',(8,8),'glowy_green_orb'),('black-ball','black_ball',(8,8),None),('gray-smoke','gray_smoke',(32,160),None),('water-impact','water_impact',(32,32),None),('spark-thin','spark_0',(8,64),'spark'),('spark-wide','spark_1',(16,64),'spark'),('spark-2','spark_2',(16,48),None),('spark-h','spark_h',(32,32),'spark'),('black-ball-2','black_ball_2',(16,16),None),('shock-3','shock_3',(32,96),None),('small-bubbles','small_bubbles',(16,40),None),('large-fresh-egg','large_fresh_egg',(16,16),None),('explosion','explosion',(32,128),None),('lightning','lightning',(32,160),'lightning_2')]:
  if pal in ('water_impact','lightning_2','poison_bubble','rain_drops','sharp_teeth','fire'):
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
 return (json.dumps(dict(schemaVersion=1,scripts=scripts,subroutines=subs,spriteRecipes=recipes,assets=[dict(id=id,path='elemental-'+id+'.rgb555') for id in pixels]),indent=2)+'\n').encode(),pixels
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/elemental.json');p.add_argument('--check',action='store_true');a=p.parse_args();data,pixels=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/('elemental-'+id+'.rgb555'),blob) for id,blob in pixels.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Elemental output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[s['moveId'] for s in json.loads(data)['scripts']])))
