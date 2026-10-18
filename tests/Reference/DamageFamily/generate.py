#!/usr/bin/env python3
"""Bounded pinned-C damage callback oracle; upstream scripts are never executed."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

KINDS=['revenge-small','revenge-big','persistent-impact','edge-impact','rapid-ring','air-slice','rapid-elevation','speed-stretch','speed-impact','speed-reappear','speed-dust','air-projectile']
ROOTS=[279,279,279,245,229,314,229,245,245,245,245,314]
TEMPLATES=['gRevengeSmallScratchSpriteTemplate','gRevengeBigScratchSpriteTemplate','gPersistHitSplatSpriteTemplate','gMonEdgeHitSplatSpriteTemplate','gRapidSpinSpriteTemplate','gAirCutterSliceSpriteTemplate','gSpeedDustSpriteTemplate','gAirWaveProjectileSpriteTemplate']

def build(root,reference,out,full=False):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('source_extract',root/'tests/Reference/ConfusionKiss/source_extract.py');here=Path(__file__).parent
 pins=json.loads((here/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for p,digest in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+p)
 header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
 types=types.replace('union AffineAnimCmd\n{','union AffineAnimCmd\n{\n    u32 portableArmAbiAlignment;')
 oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
 engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSprite','CreateSpriteAndAnimate','SetSpriteSheetFrameTileNum','StartSpriteAnim','StartSpriteAffineAnim','SpriteCallbackDummy','CalcCenterToCornerVec']
 names={'src/battle_anim_effects_3.c':['AnimRapidSpin','AnimRapidSpin_Step','AnimTask_RapinSpinMonElevation','RapinSpinMonElevation_Step'],'src/battle_anim_effects_2.c':['AnimTask_AttackerStretchAndDisappear','AnimTask_AttackerStretchAndDisappear_Step','AnimTask_ExtremeSpeedImpact','AnimTask_ExtremeSpeedImpact_Step','AnimTask_ExtremeSpeedMonReappear','AnimTask_ExtremeSpeedMonReappear_Step','AnimTask_SpeedDust','AnimTask_SpeedDust_Step','AnimSpeedDust','AnimTask_AirCutterProjectile','AirCutterProjectile_Step1','AirCutterProjectile_Step2','AnimAirWaveProjectile','AnimAirWaveProjectile_Step1','AnimAirWaveProjectile_Step2'],'src/battle_anim_fight.c':['AnimRevengeScratch'],'src/battle_anim_normal.c':['AnimHitSplatPersistent','AnimHitSplatOnMonEdge'],'src/battle_anim_effects_1.c':['AnimAirCutterSlice','AnimSlice_Step'],'src/battle_anim_flying.c':['DestroyAnimSpriteAfterTimer'],'src/battle_anim_mons.c':['SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','RunStoredCallbackWhenAnimEnds','RunStoredCallbackWhenAffineAnimEnds','DestroySpriteAndMatrix','WaitAnimForDuration','GetBattlerSide','GetBattlerPosition','SetAverageBattlerPositions','PrepareAffineAnimInTaskData','RunAffineAnimFromTaskData','SetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped','PrepareBattlerSpriteForRotScale','ResetSpriteRotScale','SetBattlerSpriteYOffsetFromYScale'],'src/battle_anim.c':['DestroyAnimSprite','BattleAnimAdjustPanning','KeepPanInRange'],'src/math_util.c':['Q_8_8_mul','Q_8_8_inv'],'src/sprite.c':engine}
 funcs=[];extracted=[]
 for p,ns in names.items():
  for n in ns:
   original=x.function(s[p],n);f=original;adaptation=None
   if n=='RunAffineAnimFromTaskData':
    f=f.replace('sAnimTaskAffineAnim = LoadPointerFromVars(task->data[13], task->data[14]) + (task->data[7] << 3);','sAnimTaskAffineAnim = (const union AffineAnimCmd *)(LoadPointerFromVars(task->data[13], task->data[14]) + (task->data[7] << 3));');adaptation='Explicit cast for portable byte-pointer arithmetic; source 8-byte command stride retained.'
   funcs.append(f);extracted.append(dict(path=p,name=n,sha256=hashlib.sha256(original.encode()).hexdigest(),adaptation=adaptation))
 prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in funcs)
 arrays='\n'.join(x.array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs','sCenterToCornerVecTable'])+'\n'+x.array(s['src/trig.c'],'gSineTable')
 arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
 for p,ns in {'src/battle_anim_effects_3.c':['sRapidSpinAnimCmds','sRapidSpinAnimTable'],'src/battle_anim_effects_2.c':['sStretchAttackerAffineAnimCmds','sSpeedDustAnimCmds','sSpeedDustAnimTable','sSpeedDustPosTable'],'src/battle_anim_fight.c':['sAnim_RevengeSmallScratch_0','sAnim_RevengeSmallScratch_1','sAnim_RevengeSmallScratch_2','sAnims_RevengeSmallScratch','sAnim_RevengeBigScratch_0','sAnim_RevengeBigScratch_1','sAnim_RevengeBigScratch_2','sAnims_RevengeBigScratch'],'src/battle_anim_normal.c':['sAffineAnim_HitSplat_0','sAffineAnim_HitSplat_1','sAffineAnim_HitSplat_2','sAffineAnim_HitSplat_3','sAffineAnims_HitSplat'],'src/battle_anim_effects_1.c':['sCuttingSliceAnimCmds','sCuttingSliceAnimTable']}.items():arrays+='\n'.join(x.array(s[p],n) for n in ns)
 for n in ['gOamData_AffineOff_ObjNormal_16x16','gOamData_AffineOff_ObjNormal_32x16','gOamData_AffineOff_ObjNormal_32x32','gOamData_AffineOff_ObjNormal_64x64','gOamData_AffineOff_ObjBlend_32x32','gOamData_AffineOff_ObjBlend_32x16','gOamData_AffineNormal_ObjBlend_32x32']:
  arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
 for n in TEMPLATES:
  for p in names:
   found=re.search(r'(?:static )?const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s[p])
   if found:arrays+=found[0]+'\n';break
  else:raise ValueError(n)
 defs=x.macros(s.values(),['Q_8_8','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_PURPLE_SCRATCH','ANIM_TAG_PURPLE_SWIPE','ANIM_TAG_IMPACT','ANIM_TAG_RAPID_SPIN','ANIM_TAG_SPEED_DUST','ANIM_TAG_AIR_WAVE','ANIM_TAG_CUT','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','SPRITE_NONE','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','SE_M_BLIZZARD2','MON_PIC_HEIGHT'])
 defs+=x.enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')+'\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
 c=(here/'harness.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(funcs));source=out/'damage-family.c';source.write_text(c)
 exes=[]
 for opt in ['O0','O2']:
  exe=out/('damage-family-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);exes.append(exe)
 cases=[]
 def add(kind,d,a,t,args=(),y=0,e=0,shift=0,hidden=0,seed=0,bank=0,label='authored'):
  args=list(args)+[2000+i for i in range(len(args),8)];argv=[kind,d,a,t,y,e,shift,hidden,seed,bank,*args]
  outputs=[subprocess.check_output([str(exe),*map(str,argv)],text=True) for exe in exes]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization mismatch '+str(argv))
  rows=[json.loads(row) for row in outputs[0].splitlines()]
  if rows[-1]['alive']:raise ValueError('Incomplete source case '+str(argv))
  previous=[(b*4093+i*17)&65535 if seed else 0 for b in range(2) for i in range(960)]
  for row in rows:
   if 'buffers' in row:
    values=row.pop('buffers');row['writes']=[[i,v] for i,v in enumerate(values) if v!=previous[i]];previous=values
  cases.append(dict(kind=kind,name=KINDS[kind],root=ROOTS[kind],doubles=bool(d),actor=a,target=t,yOffset=y,elevation=e,shift=bool(shift),hiddenMask=hidden,seeded=bool(seed),writeBuffer=bank,arguments=args,branch=label,rows=rows))
 layouts=[(0,0,1),(0,1,0),*( (1,a,a^1) for a in range(4))]
 if full:layouts=[(d,a,t) for d in [0,1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a!=t]
 for d,a,t in layouts:
  for selector in [0,1]:
   add(0,d,a,t,[10,-12,selector]);add(1,d,a,t,[-7,8,selector]);add(4,d,a,t,[selector,8,32,-32,7,-4]);add(4,d,a,t,[selector,-8,-32,32,253,4]);
   for phase in [0,1]:add(6,d,a,t,[selector,3,phase])
  for variant in range(4):add(2,d,a,t,[12,-8,1,variant,3]);add(3,d,a,t,[0,-12,8,variant])
  for selector in ([0,1,2] if d else [0]):
   for flip in [0,1]:add(5,d,a,t,[12,-20,flip,selector])
  for kind in [7,8,9,10]:add(kind,d,a,t)
  add(11,d,a,t,[8,8,512,10,1])
 # Exact operands used by the authored roots, on every single/double actor position.
 for d,a,t in [(0,0,1),(0,1,0),*((1,a,a^1) for a in range(4))]:
  for kind,args in [(4,[0,0,32,-32,40,-2]),(6,[0,2,0]),(6,[0,2,1]),(11,[32,-24,1536,2,128]),(5,[40,-32,0,2]),(2,[-10,-8,1,1,8]),(2,[10,8,1,1,8]),(0,[10,-10,0]),(1,[10,-10,4])]:
   add(kind,d,a,t,args,label='authored-root-exact')
 # Branch-focused explicit inputs: retained register2, geometry/elevation, existing base/x2/y2,
 # logical and sprite hidden partners, timer edges, signed priority clamp, and bank retention.
 for retained in [0,1,2002]:add(0,1,3,1,[10,20,retained],label='retained-register2')
 for d,a,t in [(0,0,1),(1,3,2)]:
  add(2,d,a,t,[20,-20,0,3,1],y=20,e=48,label='picture-offset-clamp-and-minimum-timer')
  add(3,d,a,t,[1,-12,4,2],y=8,e=16,shift=1,label='live-base-plus-secondary-position')
  add(6,d,a,t,[1,64,1],e=48,seed=1,bank=1,label='clamped-top-fast-crossing-retained-banks')
  add(7,d,a,t,y=20,e=16,shift=1,hidden=1<<a,label='preexisting-hidden-and-secondary-offset')
  add(8,d,a,t,shift=1,label='preexisting-target-offset')
  add(9,d,a,t,hidden=1<<a,label='initially-invisible-attacker')
  add(10,d,a,t,shift=1,label='raw-child-base-ignores-live-battler-shift')
 for mask in [4,8,12]:
  add(5,1,0,1,[16,-16,1,2],hidden=mask,label='average-visible-partner')
  add(11,1,0,1,[8,8,512,0,193],hidden=mask,label='hidden-partner-zero-cadence-retained-priority')
 for priority in [63,64,127,128,255]:add(11,1,3,2,[8,8,512,2,priority],label='signed-subpriority-and-high-bit')
 add(11,1,2,0,[-28,0,2,19,1],label='signed-reciprocal-max-safe-child-cadence')
 for speed in [2,3,4]:add(11,1,2,0,[-28,0,speed,0,1],label='signed-reciprocal-and-smallest-positive-duration')
 add(11,1,0,1,[64,8,32766,1,1],label='largest-admitted-even-speed-nonzero-duration')
 for speed in [256,513,1024]:add(11,1,2,0,[-16,24,speed,3,5],label='same-side-direction-and-fixed-point-speed')
 return dict(schema='firered-damage-family-source-v1',profile='full' if full else 'representative',sourceSha256=pins,extractedFunctions=extracted,optimizationParity=['O0','O2'],excludedUndefinedInputs=[dict(doubles=True,actor=2,target=0,arguments=[-29,0,512,0,1],reason='Zero x difference makes data5 zero; original Q_8_8_inv(data5) divides by zero.'),dict(doubles=True,actor=2,target=0,arguments=[-28,0,32766,0,1],reason='Nonzero x difference1 truncates Q_8_8_mul(1,2) to zero; original Q_8_8_inv(data5) divides by zero.')],semantics='Frame0 is source CreateSpriteAndAnimate or task initializer. Later frames run source AnimateSprites then source task callback. All frames retained through native lifetime. Native sprite player and task-affine helper bodies are independently extracted. Explicit battler base/centers, geometry, visibility, secondary offsets and scanline hardware boundary are harness inputs. BIOS angle-zero matrix is exact; no nonzero rotation is tested. Handle words are opaque and not managed contracts. Raw allocations reserve actual battler cells and reuse lowest free slot; source capacity failure is outside this successful-closure corpus. Scanline rows retain all 1920 words via deltas.',cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full',action='store_true');a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='damage-family-reference-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.full);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']),terminalFrames={n:sorted(set(c['rows'][-1]['frame'] for c in r['cases'] if c['name']==n)) for n in KINDS})))
