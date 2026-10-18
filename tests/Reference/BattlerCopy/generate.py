"""Extract pinned native callbacks and shared sprite players into a bounded C oracle."""
import argparse, hashlib, json, re, subprocess, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--repo',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a=p.parse_args();here=Path(__file__).resolve().parent
if a.output.exists():raise FileExistsError(a.output)
sys.path.insert(0,str(a.repo/'tests/Reference'));sys.path.insert(0,str(a.repo/'tests/Reference/CallerPrelude'))
from reference_c import compile_c,verify_reference
from source_extract import function,array,macros,identity,COMMIT
verify_reference(a.reference)
names={
'src/battle_anim_ghost.c':['AnimTask_NightShadeClone','AnimTask_NightShadeClone_Step1','AnimTask_NightShadeClone_Step2'],
'src/battle_anim_effects_1.c':['AnimTask_ShrinkTargetCopy','AnimTask_DuplicateAndShrinkToPos_Step1','AnimTask_DuplicateAndShrinkToPos_Step2','AnimTask_DoubleTeam','AnimTask_DoubleTeam_Step','AnimDoubleTeam','AnimMimicOrb'],
'src/battle_anim_mons.c':['CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet','SetBattlerSpriteYOffsetFromYScale','InitAnimFastLinearTranslation','InitAndRunAnimFastLinearTranslation','AnimFastTranslateLinear','AnimFastTranslateLinearWaitEnd'],
'src/trig.c':['Sin'],
'src/sprite.c':['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','ChangeSpriteAffineAnim']}
paths=list(names)+['include/sprite.h','include/gba/types.h','include/gba/io_reg.h','include/gba/defines.h','include/global.h','include/palette.h','include/constants/battle_anim.h']
s={p:(a.reference/p).read_text() for p in paths};pins=json.loads((here/'pinned-inputs.json').read_text())
for path in paths:
 if hashlib.sha256((a.reference/path).read_bytes()).hexdigest()!=pins[path]:raise ValueError('Pinned source mismatch '+path)
functions=[];extracted=[]
for path,ns in names.items():
 for name in ns:
  f=function(s[path],name);functions.append(f);at=s[path].index(f.rstrip());extracted.append(dict(path=path,name=name,startLine=s[path][:at].count('\n')+1,endLine=s[path][:at+len(f.rstrip())].count('\n')+1,sha256=hashlib.sha256(f.encode()).hexdigest()))
prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in functions)
h=s['include/sprite.h'];types=h[h.index('struct AnimFrameCmd'):h.index('\nenum\n{\n    SUBSPRITES_OFF')].replace('union AffineAnimCmd\n{','union AffineAnimCmd\n{\n    u32 portableArmAbiAlignment;')
oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
defs=macros(list(s.values()),['Q_8_8','ST_OAM_AFFINE_ON_MASK','ST_OAM_OBJ_BLEND','ANIM_TAG_BENT_SPOON','ANIM_ATTACKER','ANIM_TARGET','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_DISPCNT','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDALPHA_BLEND','DISPCNT_BG1_ON','DISPCNT_BG2_ON','OBJ_PLTT_ID','OBJ_PLTT_ID2'])
arrays='\n'.join(array(s[path],name) for path,ns in {'src/sprite.c':['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'],'src/trig.c':['gSineTable'],'src/battle_anim_effects_1.c':['sPowerAbsorptionOrbAnimCmds','sPowerAbsorptionOrbAnimTable','sMimicOrbAffineAnimCmds1','sMimicOrbAffineAnimCmds2','sMimicOrbAffineAnimTable']}.items() for name in ns)
c=(here/'prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(functions))
build=(a.build_dir or Path(tempfile.mkdtemp(prefix='battler-copy-oracle-'))).resolve();build.mkdir(parents=True,exist_ok=True);(build/'reference.c').write_text(c)
for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(build/'reference.c'),'-o',str(build/opt)])
cases=[]
def add(kind,d,actor,target,hidden=0,free=2,speed=128,duration=24,signal=26,hold=85):
 args=[kind,d,actor,target,hidden,free,speed,duration,signal,hold];runs=[subprocess.check_output([str(build/opt),*map(str,args)]) for opt in ['O0','O2']];assert runs[0]==runs[1],args;rows=[json.loads(r) for r in runs[0].splitlines()];assert not rows[-1]['alive'];cases.append(dict(id=f'{kind}-{len(cases):03}',kind=kind,doubles=bool(d),actor=actor,target=target,hidden=bool(hidden),free=free,speed=speed,duration=duration,signal=signal,hold=hold,rows=rows))
for d in [0,1]:
 for b in range(4 if d else 2):
  for kind in range(4):add(kind,d,b,b^1)
for free in [0,1]:
 for b in range(4):add(2,1,b,b^1,free=free)
for b in [0,1]:
 add(0,0,b,b^1,hold=0);add(1,0,b,b^1,hidden=1);add(1,0,b,b^1,speed=32767,duration=3,signal=5);add(3,0,b,b)
def representative(c):
    # Each callback on both sides/layouts; allocation 0/1/2, zero hold,
    # hidden target, signed-speed overflow, and the self-target orb branch.
    baseline = (c['doubles'], c['actor']) in [(False, 0), (True, 3)]
    ordinary = not c['hidden'] and c['free'] == 2 and c['speed'] == 128 and c['hold'] == 85 and c['target'] == (c['actor'] ^ 1)
    boundary = (c['kind'] == 2 and c['free'] < 2 and c['actor'] == 0) or (c['hold'] == 0 and c['actor'] == 0) or (c['hidden'] and c['actor'] == 0) or (c['speed'] == 32767 and c['actor'] == 1) or (c['kind'] == 3 and c['target'] == c['actor'] == 1)
    return baseline and ordinary or boundary

if not a.full:
    cases = [c for c in cases if representative(c)]
result=dict(profile='full' if a.full else 'representative',schema='firered-battler-copy-source-v1',commit=COMMIT,sources=identity(a.reference,paths),extractedFunctions=extracted,optimizationParity=['O0','O2'],semantics={'frame0':'Native initializer only for tasks; native orb initializer then AnimateSprite. Following frames run native clone/orb callbacks and sprite players before native task callback.','hardware':'Explicit GPU register storage, palette allocation/free, source body inputs, and zero-rotation ObjAffineSet boundary. Native callback logic, palette-index copy, native sine, shared native sprite frame and affine players, and native fast-linear translation are unmodified.','battlers':'Fixed source coordinates, actor/target slot sides and YDelta=8. Initial body x2=3,y2=-2. Rank uses native slots0/3->2,1/2->1. Hidden is the target OBJ flag.','palette':'Slot4 is allocated with entry0 initially1234 and other entries zero. Battler unfaded palette entries are deterministic actor-specific values. Native DoubleTeam copies1..15 and blends all16. No source texture decompression is simulated.','destruction':'Observer retains sprite fields while clearing inUse; dead frames are not rendered. Affine matrices are inspected before pool reclamation.'},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');report=dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),terminalFrames={k:sorted({c['rows'][-1]['frame'] for c in cases if c['kind']==k}) for k in range(4)},fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest());(build/'verification.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
