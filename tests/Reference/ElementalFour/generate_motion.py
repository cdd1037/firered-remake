#!/usr/bin/env python3
"""Generate deterministic C-oracle vectors; never import or execute upstream scripts."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path
sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
from reference_c import compile_c, verify_reference
from source_extract import function, array, macros, identity, COMMIT, verify, enum_containing

p = argparse.ArgumentParser()
p.add_argument('reference', type=Path)
p.add_argument('--build-dir', type=Path)
p.add_argument('--output', type=Path, default=HERE.parent.parent/'Game.Presentation.Tests/Fixtures/elemental-four-motion-source-vectors.json')
p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a = p.parse_args(); r = a.reference.resolve(); out = (a.build_dir or Path(tempfile.mkdtemp(prefix="elemental-four-reference-"))).resolve(); out.mkdir(parents=True, exist_ok=True)
paths = ['src/battle_anim_effects_1.c','src/battle_anim_effects_2.c','src/battle_anim_water.c','src/battle_anim_ice.c','src/battle_anim_mons.c','src/battle_anim.c','src/sprite.c','src/trig.c','include/sprite.h','src/data/battle_anim.h','include/gba/types.h','include/global.h','include/battle.h','include/battle_anim.h','include/constants/battle_anim.h','include/constants/battle.h','data/battle_anim_scripts.s']
verify_reference(r); verify(r, paths)
s = {path:(r/path).read_text() for path in paths}
header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSprite','CreateSpriteAndAnimate','StartSpriteAnim','StartSpriteAffineAnim','SpriteCallbackDummy']
names={
 'src/battle_anim_ice.c':['InitPoisonGasCloudAnim','MovePoisonGasCloud'],
 'src/battle_anim_effects_2.c':['AnimGuardRing'],
 'src/battle_anim_effects_1.c':['AnimIngrainRoot','AnimRootFlickerOut','AnimIngrainOrb'],
 'src/battle_anim_water.c':['AnimHydroCannonCharge','AnimHydroCannonCharge_Step','AnimHydroCannonBeam'],
 'src/battle_anim_mons.c':['InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','InitAnimLinearTranslationWithSpeed','SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','SetAverageBattlerPositions','GetAnimBattlerSpriteId','GetBattlerSide','GetBattlerPosition'],
 'src/battle_anim.c':['DestroyAnimSprite'], 'src/trig.c':['Sin'], 'src/sprite.c':engine,
}
bodies=[]; extracted=[]
for path, ns in names.items():
 for n in ns:
  original=function(s[path],n); f=original
  if n=='CreateSprite': f=f.replace('u8 CreateSprite(','u8 SourceCreateSprite(')
  bodies.append(f)
  start=s[path].index(original.rstrip())
  extracted.append(dict(path=path,name=n,startLine=s[path][:start].count('\n')+1,endLine=s[path][:start+len(original.rstrip())].count('\n')+1,sha256=hashlib.sha256(original.encode()).hexdigest(),adaptation='symbol renamed to SourceCreateSprite for wrapper' if n=='CreateSprite' else None))
protos='\n'.join(f[:f.index('{')].strip()+';' for f in bodies)
arrays='\n'.join(array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'])
arrayNames={
 'src/trig.c':['gSineTable'],
 'src/battle_anim_ice.c':['sAnim_Cloud','sAnims_Cloud'],
 'src/battle_anim_effects_1.c':['sIngrainRootAnimCmds1','sIngrainRootAnimCmds2','sIngrainRootAnimCmds3','sIngrainRootAnimCmds4','sIngrainRootAnimTable','sIngrainOrbAnimCmds','sIngrainOrbAnimTable'],
 'src/battle_anim_effects_2.c':['sGuardRingAffineAnimCmds1','sGuardRingAffineAnimCmds2','sGuardRingAffineAnimTable'],
 'src/battle_anim_water.c':['sAnim_WaterMudOrb','gAnims_WaterMudOrb','sAffineAnim_HydroCannonCharge','sAffineAnim_HydroCannonBeam','sAffineAnims_HydroCannonCharge','sAffineAnims_HydroCannonBeam'],
}
for path,ns in arrayNames.items(): arrays+='\n'+'\n'.join(array(s[path],n) for n in ns)
arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
for n in ['gOamData_AffineDouble_ObjBlend_16x16','gOamData_AffineOff_ObjNormal_32x32','gOamData_AffineOff_ObjNormal_8x8','gOamData_AffineDouble_ObjBlend_64x32','gOamData_AffineOff_ObjBlend_32x16']:
 arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
for path,n in [('src/battle_anim_ice.c','gPoisonGasCloudSpriteTemplate'),('src/battle_anim_effects_2.c','gGuardRingSpriteTemplate'),('src/battle_anim_effects_1.c','gIngrainRootSpriteTemplate'),('src/battle_anim_effects_1.c','gIngrainOrbSpriteTemplate'),('src/battle_anim_water.c','gHydroCannonChargeSpriteTemplate'),('src/battle_anim_water.c','gHydroCannonBeamSpriteTemplate')]:
 arrays+=re.search(r'const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s[path])[0]+'\n'
defs=macros(list(s.values()),['sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY','sTransl_Speed','sTransl_Duration','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_WATER_ORB','ANIM_TAG_ROOTS','ANIM_TAG_ORBS','ANIM_TAG_GUARD_RING','ANIM_TAG_PURPLE_GAS_CLOUD','B_SIDE_PLAYER','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','SPRITE_NONE','Q_8_8'])
defs+=enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')
defs+='\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
c=(HERE/'motion-prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',protos).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(bodies))
(out/'motion.c').write_text(c)
for opt in ['O0','O2']: compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(out/'motion.c'),'-o',str(out/('motion-'+opt))])
def run(args):
 results=[subprocess.check_output([str(out/('motion-'+opt)),*map(str,args)]) for opt in ['O0','O2']]
 assert results[0]==results[1], ('O0/O2 mismatch', args)
 return [json.loads(row) for row in results[0].splitlines()]
kindNames=['poison-cloud','guard-ring','ingrain-root','ingrain-orb','hydro-charge','hydro-beam']
roots=[139,219,275,275,308,308]
slots=[(d,b) for d in [0,1] for b in range(4 if d else 2)]
cases=[]
def add(kind,doubles,attacker,target,args,mask=None,yOverride=0,label='source'):
 args=list(args)+[0]*(8-len(args)); mask=(15 if doubles else 3) if mask is None else mask
 values=[kind,doubles,attacker,target,mask,yOverride,*args]
 rows=run(values)
 assert not rows[-1]['alive'] and rows[-1]['visualCount']==0
 cases.append(dict(id=f'{kindNames[kind]}-{len(cases):03}',kind=kindNames[kind],kindId=kind,root=roots[kind],doubles=bool(doubles),attacker=attacker,target=target,visibleMask=mask,attackerYOffset=yOverride,variant=label,initialArgs=args,rows=rows))
for d,b in slots:
 opponents=[b^1]+([b^3] if d else [])
 for t in opponents: add(0,d,b,t,[64,0,0,-32,-6,4192,1072,0])
 add(0,d,b,b^1,[64,7,-4,-32,-6,4192,1072,1],label='pic-offset-target-and-mutated-x')
 if d: add(0,d,b,b^2,[64,7,0,-32,-6,4192,1072,0],label='selected-ally')
 add(1,d,b,b^1,[])
 if d: add(1,d,b,b^1,[],mask=15^(1<<(b^2)),label='hidden-partner')
 for recipe in [[16,26,-1,2,150],[-32,20,1,1,140],[32,22,1,0,130],[-16,25,-1,3,120]]: add(2,d,b,b^1,recipe)
 if not (b&1): add(2,d,b,b^1,[16,26,-1,2,150],yOverride=32,label='y-greater-than120')
 for recipe in [[32,26,-1,3,30],[-48,20,1,2,30],[48,26,-2,3,18]]: add(3,d,b,b^1,recipe)
 add(4,d,b,b^1,[])
 for t in opponents: add(5,d,b,t,[10,-10,0,0,15,257])
 if d: add(5,d,b,b^2,[10,-10,0,0,15,257],label='selected-ally')
 for flags in [0,1,256]: add(5,d,b,b^1,[10,-10,7,-3,15,flags],label='coordinate-flag-and-mutated-x')
def representative(c):
    # Both sides and coordinate layouts retain every source recipe, target
    # selector, hidden-partner, selected-ally, and Y-clamp branch.
    return (c['doubles'], c['attacker']) in [(False, 0), (True, 3)]

if not a.full:
    cases = [c for c in cases if representative(c)]
result=dict(profile='full' if a.full else 'representative',schema='firered-elemental-four-motion-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,extractedFunctions=extracted,optimization_parity=['O0','O2'],compiler_flags=['-std=c11','-fwrapv','-fno-strict-aliasing'],semantics={
 'kindNames':kindNames,
 'frame0':'after source CreateSpriteAndAnimate: initializer callback, then source AnimateSprite; later frames after source AnimateSprites (callback before frame/affine engine)',
 'positionFields':['x','y','x2','y2'],
 'flipFields':'hFlip/vFlip retain the raw Sprite base flags; when affineMode has bit0 clear, oamMatrixNum bits3/4 are the effective rendered flips written by original SetSpriteOamFlipBits. For affine sprites matrixNum selects a matrix and is not a flip field.',
 'coordinates':{'single':[[72,80,72,88],[176,40,176,40],[48,40,48,48],[112,80,112,80]],'double':[[32,80,32,88],[200,40,200,40],[90,88,90,96],[152,32,152,32]],'fields':['X','Y','X_2','Y_PIC_OFFSET'],'attackerYOffset':'added to attacker Y and Y_PIC_OFFSET before callback; zero except explicit floor-boundary vectors'},
 'battlerSubpriority':[30,20,28,18], 'battlerBgPriority':[2,1,1,2], 'initialSpriteSubpriority':28,
 'events':{'4':['DestroySprite','slot','instance','data0','animEnded',0,0,0],'6':['FreeOamMatrix','matrix',0,0,0,0,0,0]},
 'stubs':['Source CreateSpriteAt allocation boundary is a bounded authored field initializer: first four battler slots reserved, effect uses slot4 and source affine matrix4. Source CreateSprite/CreateSpriteAndAnimate and AnimateSprites loops are extracted.', 'Battler coordinate lookups, visibility mask, BG priority and subpriority are explicit inputs; identity position-to-id mapping, all battlers present, non-contest only.', 'StoreSpriteCallbackInData6 and SetCallbackToStoredInData6 use stable token data6=1,data7=0 instead of serializing a host function pointer; actual callback change still occurs only when source invokes SetCallbackToStoredInData6.', 'BIOS ObjAffineSet records its input and writes zero matrix coefficients; xScale/yScale/rotation and affine-ended timing come from the verbatim source animation engine. Matrix pixel coefficients and renderer are outside this oracle.', 'FreeOamMatrix records event; DestroySprite marks inUse false and retains fields for final inspection. Source DestroyAnimSprite decrements actual visual count.', 'ROM-independent dummy animation tables mirror end sentinels; no upstream build scripts or source scripts execute.']},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n')
verification=dict(schema=result['schema'],fixture=a.output.name,fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest(),generatedCSha256=hashlib.sha256(c.encode()).hexdigest(),caseCount=len(cases),rowCount=sum(len(c['rows']) for c in cases),optimizationParity=['O0','O2'],terminalFrames={k:sorted(set(c['rows'][-1]['frame'] for c in cases if c['kind']==k)) for k in kindNames})
(out/'verification.json').write_text(json.dumps(verification,indent=2)+'\n')
print(json.dumps(verification,indent=2))
