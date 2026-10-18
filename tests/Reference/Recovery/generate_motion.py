#!/usr/bin/env python3
import argparse,json,re,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
from source_extract import function,array,macros,identity,COMMIT,verify,enum_containing
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);a=p.parse_args();r=a.reference;out=Path(__file__).resolve().parent
paths=['src/battle_anim_effects_1.c','src/battle_anim_effects_3.c','src/battle_anim_water.c','src/battle_anim_mons.c','src/battle_anim.c','src/sprite.c','src/random.c','include/sprite.h','src/data/battle_anim.h','include/gba/types.h','include/random.h','include/battle_anim.h','include/constants/battle_anim.h','include/constants/battle.h','data/battle_anim_scripts.s']
verify(r,paths);s={p:(r/p).read_text() for p in paths};header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')];oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSprite','CreateSpriteAndAnimate','StartSpriteAnim','StartSpriteAffineAnim','SpriteCallbackDummy']
names={'src/battle_anim_effects_1.c':['AnimSleepLetterZ','AnimSleepLetterZ_Step','AnimMoon','AnimMoon_Step','AnimMoonlightSparkle','AnimMoonlightSparkle_Step'],'src/battle_anim_effects_3.c':['AnimGreenStar','AnimGreenStar_Step1','AnimGreenStar_Step2','AnimGreenStar_Callback'],'src/battle_anim_water.c':['AnimAuroraBeamRings','AnimAuroraBeamRings_Step'],'src/battle_anim_mons.c':['InitAnimLinearTranslation','AnimTranslateLinear','SetSpriteCoordsToAnimAttackerCoords','SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','DestroySpriteAndMatrix'],'src/battle_anim.c':['DestroyAnimSprite'],'src/sprite.c':engine,'src/random.c':['Random']}
funcs=[]
for path,ns in names.items():
 for n in ns:
  f=function(s[path],n)
  if n=='Random':f=f.replace('u16 Random(void)','u16 SourceRandom(void)')
  if n=='CreateSprite':f=f.replace('u8 CreateSprite(','u8 SourceCreateSprite(')
  funcs.append(f)
protos='\n'.join(f[:f.index('{')].strip()+';' for f in funcs)
arrays='\n'.join(array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'])
for path,ns in {'src/battle_anim_effects_1.c':['sSleepLetterZAnimCmds','sSleepLetterZAnimTable','sSleepLetterZAffineAnimCmds1','sSleepLetterZAffineAnimCmds2','sSleepLetterZAffineAnimTable','sMoonlightSparkleAnimCmds','sMoonlightSparkleAnimTable'],'src/battle_anim_effects_3.c':['sGreenStarAnimCmds1','sGreenStarAnimCmds2','sGreenStarAnimCmds3','sGreenStarAnimTable'],'src/battle_anim_water.c':['sAnim_AuroraBeamRing_0','sAnim_AuroraBeamRing_1','sAnims_AuroraBeamRing','sAffineAnim_AuroraBeamRing','sAffineAnims_AuroraBeamRing']}.items():arrays+='\n'+'\n'.join(array(s[path],n) for n in ns)
arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
for n in ['gOamData_AffineNormal_ObjNormal_32x32','gOamData_AffineOff_ObjNormal_16x16','gOamData_AffineOff_ObjBlend_64x64','gOamData_AffineDouble_ObjNormal_8x16']:
 arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
for path,n in [('src/battle_anim_effects_1.c','gSleepLetterZSpriteTemplate'),('src/battle_anim_effects_1.c','gMoonSpriteTemplate'),('src/battle_anim_effects_1.c','gMoonlightSparkleSpriteTemplate'),('src/battle_anim_effects_3.c','gGreenStarSpriteTemplate'),('src/battle_anim_water.c','gAuroraBeamRingSpriteTemplate')]:arrays+=re.search(r'const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s[path])[0]+'\n'
defs=macros(list(s.values()),['ISO_RANDOMIZE1','sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY','sTransl_Speed','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_LETTER_Z','ANIM_TAG_GREEN_STAR','ANIM_TAG_MOON','ANIM_TAG_GREEN_SPARKLE','ANIM_TAG_RAINBOW_RINGS','B_SIDE_PLAYER'])
defs+=enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')
defs+='\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
c=(out/'motion-prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',protos).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(funcs));(out/'motion.c').write_text(c)
for opt in ['O0','O2']:subprocess.run(['cc','-std=c11','-'+opt,'-fwrapv',str(out/'motion.c'),'-o',str(out/('motion-'+opt))],check=True)
cases=[]
for kind in range(6):
 for doubles in [0,1]:
  for actor in range(4 if doubles else 2):
   for signal in ([0,1,8] if kind==4 else [0]):
    args=[kind,doubles,actor,actor^1,signal,0x12345678];data=[subprocess.check_output([str(out/('motion-'+opt)),*map(str,args)]) for opt in ['O0','O2']];assert data[0]==data[1];rows=[json.loads(x) for x in data[0].splitlines()];cases.append(dict(kind=kind,doubles=bool(doubles),attacker=actor,target=actor^1,signalFrame=signal,seed=0x12345678,rows=rows))
result=dict(schema='firered-recovery-motion-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,optimization_parity=['O0','O2'],semantics={'kinds':['sleep-letter','green-star-family','moon','moonlight-sparkle','aurora-ring','fifteen-green-families-spacing7'],'frame0':'after CreateSpriteAndAnimate; later frames after AnimateSprites','spriteFields':['slot','instance','template','x','y','x2','y2','tile','visible','affine','doubleSize','xScale','yScale','rotation','subpriority','frameIndex','frameDelay','frameEnded','data0','data1','data2','data3','data4','data5','data6','data7'],'stubs':['Battler coordinate inputs use canonical single/double positions, pic offset+8 on players; no species assumption beyond this fixture','CreateSpriteAt initializes consumed fields and allocates a distinct source affine matrix; source creation scan/player/callback bodies are verbatim','BIOS matrix helper records input only; matrix coefficients remain covered by established shared affine vectors','fifteen-family spacing is7 here; extracted Cmd_delay scheduler independently authenticates source5+2']},cases=cases)
(out/'motion-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('motion',len(cases),sum(len(c['rows']) for c in cases))
