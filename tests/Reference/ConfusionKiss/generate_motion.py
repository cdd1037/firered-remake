#!/usr/bin/env python3
"""Compile bounded, extracted original FireRed C; never execute upstream scripts."""
import argparse, hashlib, json, re, subprocess, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--build-dir',type=Path);p.add_argument('--output',type=Path,default=HERE.parent.parent/'Game.Presentation.Tests/Fixtures/confusion-kiss-motion-source-vectors.json');p.add_argument('--reference-helper',type=Path,default=HERE.parent/'reference_c.py');p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a=p.parse_args()
# External staging may point at the canonical authored portable compiler helper.
sys.path.insert(0,str(a.reference_helper.resolve().parent))
from reference_c import compile_c,verify_reference
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function,array,macros,enum_containing,identity,COMMIT,verify
r=a.reference.resolve();out=(a.build_dir or Path(tempfile.mkdtemp(prefix='confusion-kiss-reference-'))).resolve();out.mkdir(parents=True,exist_ok=True)
paths=['src/battle_main.c','src/battle_anim_ghost.c','src/battle_anim_effects_2.c','src/battle_anim_effects_3.c','src/battle_anim_sound_tasks.c','src/battle_anim_normal.c','src/battle_anim_mons.c','src/battle_anim.c','src/sprite.c','src/trig.c','src/random.c','src/palette.c','src/blend_palette.c','include/sprite.h','src/data/battle_anim.h','include/gba/types.h','include/gba/io_reg.h','include/gba/defines.h','include/global.h','include/battle.h','include/battle_anim.h','include/palette.h','include/random.h','include/constants/songs.h','include/constants/battle_anim.h','include/constants/battle.h','data/battle_anim_scripts.s']
verify_reference(r);verify(r,paths, Path(__file__).resolve().parent / 'pinned-inputs.json');s={p:(r/p).read_text() for p in paths}
header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
types+='\n'+enum_containing(s['src/palette.c'],'NORMAL_FADE')+re.search(r'struct PaletteFadeControl\s*\{[\s\S]*?\n\};',s['include/palette.h'])[0]+'\n'+oam[oam.index('struct PlttData'):oam.index('struct OamData')]
engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSprite','CreateSpriteAndAnimate','StartSpriteAnim','StartSpriteAffineAnim','SpriteCallbackDummy']
names={'src/battle_anim_ghost.c':['AnimConfuseRayBallBounce','AnimConfuseRayBallBounce_Step1','AnimConfuseRayBallBounce_Step2','UpdateConfuseRayBallBlend','AnimConfuseRayBallSpiral','AnimConfuseRayBallSpiral_Step'],'src/battle_anim_effects_2.c':['AnimDevil','AnimPinkHeart','AnimPinkHeart_Step','AnimAngel','AnimParticleBurst'],'src/battle_anim_effects_3.c':['AnimForesightMagnifyingGlass','AnimForesightMagnifyingGlass_Step'],'src/battle_anim_sound_tasks.c':['SoundTask_AdjustPanningVar','SoundTask_AdjustPanningVar_Step'],'src/battle_anim_normal.c':['AnimTask_BlendColorCycleByTag','BlendColorCycleByTag','AnimTask_BlendColorCycleByTagLoop'],'src/battle_anim_mons.c':['InitAnimLinearTranslation','AnimTranslateLinear','InitAnimLinearTranslationWithSpeed','SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','GetBattlerSide','GetBattlerPosition','DestroyAnimSpriteAndDisableBlend','GetBattlerSpriteCoordAttr'],'src/battle_anim.c':['DestroyAnimSprite','BattleAnimAdjustPanning','KeepPanInRange','CalculatePanIncrement'],'src/trig.c':['Sin','Cos'],'src/random.c':['Random'],'src/palette.c':['UpdatePaletteFade','BeginNormalPaletteFade','UpdateNormalPaletteFade','IsSoftwarePaletteFadeFinishing'],'src/blend_palette.c':['BlendPalette'],'src/sprite.c':engine}
bodies=[];extracted=[]
for path,ns in names.items():
 for n in ns:
  original=function(s[path],n);f=original;adaptation=None
  if n=='CreateSprite':f=f.replace('u8 CreateSprite(','u8 SourceCreateSprite(');adaptation='symbol renamed to SourceCreateSprite for wrapper'
  if n=='Random':f=f.replace('u16 Random(','u16 SourceRandom(');adaptation='symbol renamed to SourceRandom for instrumentation wrapper'
  if n=='BeginNormalPaletteFade':f=f.replace('bool8 BeginNormalPaletteFade(','bool8 SourceBeginNormalPaletteFade(');adaptation='symbol renamed for event wrapper'
  if n=='GetBattlerSpriteCoordAttr':
   switch=f.index('    switch (attr)');f=f[:f.index('{')+1]+'\n    s32 ret;\n    const struct MonCoords *coords = &explicitCoords[battlerId];\n'+f[switch:];adaptation='species/personality table-selection boundary replaced by explicit dimensions; original attribute switch retained verbatim'
  bodies.append(f);start=s[path].index(original.rstrip());extracted.append(dict(path=path,name=n,startLine=s[path][:start].count('\n')+1,endLine=s[path][:start+len(original.rstrip())].count('\n')+1,sha256=hashlib.sha256(original.encode()).hexdigest(),adaptation=adaptation))
protos='\n'.join(f[:f.index('{')].strip()+';' for f in bodies)
arrays='\n'.join(array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'])
for path,ns in {'src/trig.c':['gSineTable'],'src/battle_anim_ghost.c':['sAffineAnim_ConfuseRayBallBounce','sAffineAnims_ConfuseRayBallBounce'],'src/battle_anim_effects_2.c':['sAngelSpriteAnimCmds','sAngelSpriteAnimTable','sDevilAnimCmds1','sDevilAnimCmds2','sDevilAnimTable']}.items(): arrays+='\n'+'\n'.join(array(s[path],n) for n in ns)
arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
for n in ['gOamData_AffineDouble_ObjNormal_16x16','gOamData_AffineOff_ObjBlend_16x16','gOamData_AffineOff_ObjNormal_16x16','gOamData_AffineOff_ObjNormal_32x32','gOamData_AffineOff_ObjBlend_32x32']: arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
for path,ns in {'src/battle_anim_ghost.c':['gConfuseRayBallBounceSpriteTemplate','gConfuseRayBallSpiralSpriteTemplate'],'src/battle_anim_effects_2.c':['gDevilSpriteTemplate','gPinkHeartSpriteTemplate','gAngelSpriteTemplate','gRedHeartBurstSpriteTemplate'],'src/battle_anim_effects_3.c':['gForesightMagnifyingGlassSpriteTemplate']}.items():
 for n in ns:arrays+=re.search(r'const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s[path])[0]+'\n'
# Local task macro names are reused later in other translation units; preserve this lexical scope.
taskMacroScope=s['src/battle_anim_normal.c'];taskMacroScope=taskMacroScope[taskMacroScope.index('#define tPalSelector'):taskMacroScope.index('#undef tPalSelector')]
defs=macros([*s.values(),taskMacroScope],['sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY','sTransl_Speed','sTransl_Duration','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_YELLOW_BALL','ANIM_TAG_DEVIL','ANIM_TAG_PINK_HEART','ANIM_TAG_ANGEL','ANIM_TAG_RED_HEART','ANIM_TAG_MAGNIFYING_GLASS','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','SPRITE_NONE','Q_8_8','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','SE_M_CONFUSE_RAY','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','ISO_RANDOMIZE1','tPalTag','tDelay','tNumBlends','tInitialBlendY','tTargetBlendY','tBlendColor','tRestoreBlend','gPaletteFade_selectedPalettes','gPaletteFade_delay','PALETTE_FADE_STATUS_LOADING','PALETTE_FADE_STATUS_DONE','PALETTE_FADE_STATUS_ACTIVE','PLTT_SIZE','OBJ_PLTT_OFFSET'])
defs+=enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')+'\n'+enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_ATTR_HEIGHT')+'\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
c=(HERE/'motion-prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',protos).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(bodies));(out/'motion.c').write_text(c)
for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(out/'motion.c'),'-o',str(out/('motion-'+opt))])
def run(args):
 results=[subprocess.check_output([str(out/('motion-'+opt)),*map(str,args)]) for opt in ['O0','O2']];assert results[0]==results[1],('O0/O2 mismatch',args);return [json.loads(row) for row in results[0].splitlines()]
kindNames=['confuse-ray-bounce','confuse-ray-spiral','lovely-kiss-devil','lovely-kiss-heart','sweet-kiss-angel','sweet-kiss-heart-burst','foresight-glass'];roots=[109,109,142,142,186,186,193];cases=[];taskCases=[]
coordinates=[[[72,80,72,88],[176,40,176,40],[48,40,48,48],[112,80,112,80]],[[32,80,32,88],[200,40,200,40],[90,88,90,96],[152,32,152,32]]];subpriorities=[30,20,28,18]
def add(kind,d,attacker,target,args,seed=0x12345678,customPan=-27,geometry=0,label='source'):
 args=list(args)+[0]*(8-len(args));rows=run([kind,d,attacker,target,seed,customPan,geometry,*args]);assert not rows[-1]['alive'];entry=dict(id=f'{kindNames[kind] if kind<7 else ["adjust-custom-pan","blend-color-cycle-tag"][kind-7]}-{len(cases) if kind<7 else len(taskCases):03}',kind=kindNames[kind] if kind<7 else ['adjust-custom-pan','blend-color-cycle-tag'][kind-7],kindId=kind,root=roots[kind] if kind<7 else 109,doubles=bool(d),attacker=attacker,target=target,visibleMask=15 if d else 3,seed=seed,customPan=customPan,geometry=geometry,sourceWidths=([64,40,48,56] if not geometry else [32,64,24,48]),sourceHeights=([64,48,56,40] if not geometry else [48,32,64,24]),variant=label,initialArgs=args,initialPosition=coordinates[d][target][2:],initialSubpriority=subpriorities[target]-(3 if kind in [3,5] else 2),rows=rows);(cases if kind<7 else taskCases).append(entry)
for d in [0,1]:
 for b in range(4 if d else 2):
  targets=[b^1]+([b^3,b^2] if d else [])
  for t in targets:
   add(0,d,b,t,[28,0,288]);add(1,d,b,t,[0,-16]);add(2,d,b,t,[0,-24]);add(4,d,b,t,[16,-48]);add(6,d,b,t,[1]);add(7,d,b,t,[-64,63,2,0])
  for recipe in [[-256,-42],[128,-14],[416,-38],[-128,-22]]:add(3,d,b,b^1,recipe)
  for recipe in [[160,-30],[-256,-42],[128,-14],[416,-38],[-128,-22],[-384,-31]]:add(5,d,b,b^1,recipe)
  add(6,d,b,b^1,[0],geometry=1,label='attacker-and-alternate-dimensions');add(6,d,b,b^1,[1],geometry=1,label='target-and-alternate-dimensions')
  add(8,d,b,b^1,[10013,0,6,0,14,351]);add(7,d,b,b^1,[-64,63,2,3],label='delayed-pan');add(7,d,b,b^1,[31,-33,-4,1],label='signed-pan-clamp')
for seed in [0,1,0xFFFFFFFF,0x80000000]:add(3,0,0,1,[-256,-42],seed=seed,label='rng-state')
for pan in [-64,0,63]:add(0,0,0,1,[28,0,288],customPan=pan,label='custom-pan-consumption')
for delay in [-2,1,3]:add(8,0,0,1,[10013,delay,3,2,10,32767],label='fade-delay-and-final-zero')
groupCases=[];integratedCases=[]
for d in [0,1]:
 for b in range(4 if d else 2):
  for seed in [0,0x12345678,0xFFFFFFFF]:
   rows=run([9,d,b,b^1,seed,0,0,*([0]*8)]);groupCases.append(dict(id=f'pink-heart-group-{len(groupCases):03}',kind='pink-heart-group',root=142,doubles=bool(d),attacker=b,target=b^1,seed=seed,initialPosition=coordinates[d][b^1][2:],recipes=[[-256,-42],[128,-14],[416,-38],[-128,-22]],rows=rows))
  for t in [b^1]+([b^2] if d else []):
   rows=run([10,d,b,t,0x12345678,-27,0,28,0,288,0,0,0,0,0]);integratedCases.append(dict(id=f'pan-and-bounce-{len(integratedCases):03}',kind='pan-and-bounce',root=109,doubles=bool(d),attacker=b,target=t,seed=0x12345678,panArgs=[-64,63,2,0,0,0,0,0],initialArgs=[28,0,288,0,0,0,0,0],initialPosition=coordinates[d][t][2:],initialSubpriority=subpriorities[t]-2,rows=rows))
def representative(c):
    # Keep every script recipe once; opposite-side double ally targeting,
    # both geometry selectors, and all explicit RNG/custom-pan boundaries.
    if c['variant'] in ['rng-state', 'custom-pan-consumption']:
        return True
    if not c['doubles'] and c['attacker'] == 0:
        return True
    if not c['doubles'] or c['attacker'] != 3:
        return False
    if c['kindId'] in [0, 1, 2, 4, 6] and c['target'] == 1:
        return True
    return c['kindId'] in [3, 5] and c['initialArgs'][:2] == [-256, -42] or c['variant'] == 'target-and-alternate-dimensions'

def representative_task(c):
    # Pan direction, wait/clamp, each fade delay/final-zero boundary, and
    # the reversed double-side/ally pan calculation.
    return not c['doubles'] and c['attacker'] == 0 or c['doubles'] and c['attacker'] == 3 and c['kindId'] == 7 and c['target'] == 1

def representative_group(c):
    return not c['doubles'] and c['attacker'] == 0 or c['doubles'] and c['attacker'] == 3 and c['seed'] == 0xffffffff

def representative_integrated(c):
    # Preserve the short ally path and both audio dispatches: the double
    # right-side opponent path reaches plain PlaySE on its later bounce.
    return (c['doubles'], c['attacker'], c['target']) in [(False, 0, 1), (True, 3, 1), (True, 3, 2)]

if not a.full:
    cases = [c for c in cases if representative(c)]
    taskCases = [c for c in taskCases if representative_task(c)]
    groupCases = [c for c in groupCases if representative_group(c)]
    integratedCases = [c for c in integratedCases if representative_integrated(c)]
result=dict(profile='full' if a.full else 'representative',schema='firered-confusion-kiss-motion-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,extractedFunctions=extracted,optimization_parity=['O0','O2'],compiler_flags=['-std=c11','-fwrapv','-fno-strict-aliasing'],semantics={'kindNames':kindNames,'frame0':'after source CreateSpriteAndAnimate: callback then animation engine; tasks initializer calls its original immediate step. Later sprite rows are after AnimateSprites; later task rows run source global palette fade before task callback.','groupCases':'Four PinkHeart sprites created in source script order, source AnimateSprites visits slots4..7 ascending. Each consumes one RNG draw at frame24.','integratedCases':'Original panning task initializer runs before bounce creation. Each later frame runs AnimateSprites, UpdatePaletteFade, then task callback, matching BattleMainCB2 battle_main.c1447-1453. customPanBeforeSprites records the value consumed by callback audio; customPan is the post-task observer value. Visual count includes both task and sprite until individually destroyed.','creation':'Cmd_createsprite always passes target X_2,Y_PIC_OFFSET. The selected target subpriority minus script offset2 (offset3 hearts) is used.','flipFields':'hFlip/vFlip are raw base flags. For nonaffine sprites effective flips are oamMatrixNum bits3/4; source frame engine can overwrite directly set raw OAM bits.','coordinates':{'single':coordinates[0],'double':coordinates[1],'fields':['X','Y','X_2','Y_PIC_OFFSET']},'battlerSubpriority':subpriorities,'battlerBgPriority':[2,1,1,2],'paletteTagSlot':4,'initialPaletteColor':'for index i: ((i+3)&31)|(((i*7+5)&31)<<5)|(((i*11+9)&31)<<10)','events':{'1':['SetGpuReg','offset','value',0,0,0,0,0],'4':['DestroySprite','slot','instance','data0','animEnded',0,0,0],'6':['FreeOamMatrix','matrix',0,0,0,0,0,0],'7':['PlaySE12WithPanning','sound','signedPan',0,0,0,0,0],'8':['PlaySE','sound',0,0,0,0,0,0],'9':['Random','draw','priorState',0,'newState',0,0,0],'10':['DestroyAnimVisualTask','slot',0,0,0,0,0,0],'11':['BeginNormalPaletteFade','slot','delay','start','target','color','accepted',0]},'stubs':['Bounded sprite allocation reserves battler slots0..3 and uses effect slot4; source CreateSpriteAndAnimate/AnimateSprites/frame/affine logic are extracted.','Battler coordinates, dimensions, BG priority, subpriority and visibility are explicit. Geometry bypasses species/personality table selection only; original coordinate-attribute switch is retained.','BIOS ObjAffineSet records input and zeros coefficients; extracted frame engine owns scale/rotation/lifetime; matrix coefficient correctness is outside this oracle.','GPU/audio/palette events record original callback invocations exactly. PlaySE leaves panning unchanged and is distinct from PlaySE12WithPanning.','Original RNG uses modulo180; wrapper records each draw and 32bit state. No seed fallback.','Palette tag lookup maps requested yellow ball tag to slot4; original palette engine operates on deterministic source banks and completed-VBlank transfers.','Status-animation flag is false; battle-side mapping identity and non-contest.','Dummy animation sentinels match original engine tables. No upstream scripts are executed.']},cases=cases,taskCases=taskCases,groupCases=groupCases,integratedCases=integratedCases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');verification=dict(schema=result['schema'],fixture=a.output.name,fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest(),generatedCSha256=hashlib.sha256(c.encode()).hexdigest(),caseCount=len(cases),rowCount=sum(len(c['rows']) for c in cases),taskCaseCount=len(taskCases),taskRowCount=sum(len(c['rows']) for c in taskCases),groupCaseCount=len(groupCases),groupRowCount=sum(len(c['rows']) for c in groupCases),integratedCaseCount=len(integratedCases),integratedRowCount=sum(len(c['rows']) for c in integratedCases),optimizationParity=['O0','O2'],terminalFrames={k:sorted(set(c['rows'][-1]['frame'] for c in cases if c['kind']==k)) for k in kindNames});(out/'verification.json').write_text(json.dumps(verification,indent=2)+'\n');print(json.dumps(verification,indent=2))
