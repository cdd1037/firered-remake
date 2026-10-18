#!/usr/bin/env python3
"""Separate legal original-C trajectories from explicitly approved visual policies."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out):
 helper=module('sludge_reference',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('sludge_extract',root/'tests/Reference/ConfusionKiss/source_extract.py')
 pins=json.loads((HERE/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for p,digest in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+p)
 header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
 oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
 engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSpriteAndAnimate','SetSpriteSheetFrameTileNum','StartSpriteAnim','SpriteCallbackDummy']
 names={'src/battle_anim_poison.c':['AnimSludgeProjectile','AnimSludgeProjectile_Step','AnimSludgeBombHitParticle','AnimSludgeBombHitParticle_Step'],'src/battle_anim_electric.c':['AnimSparkElectricity','AnimSparkElectricityFlashing','AnimSparkElectricityFlashing_Step'],'src/battle_anim_mons.c':['InitAnimLinearTranslation','AnimTranslateLinear','InitAnimArcTranslation','TranslateAnimHorizontalArc','InitSpriteDataForLinearTranslation','TranslateSpriteLinearFixedPoint','SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','GetBattlerSide','GetBattlerPosition'],'src/battle_anim_flying.c':['DestroyAnimSpriteAfterTimer'],'src/battle_anim.c':['DestroyAnimSprite'],'src/trig.c':['Sin','Cos'],'src/sprite.c':engine}
 funcs=[x.function(s[p],n) for p,ns in names.items() for n in ns];prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in funcs)
 arrays='\n'.join(x.array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'])+'\n'+x.array(s['src/trig.c'],'gSineTable')
 arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
 for n in ['gOamData_AffineDouble_ObjNormal_16x16','gOamData_AffineNormal_ObjNormal_16x16']:
  arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
 for p,ns in {'src/battle_anim_poison.c':['sAnim_PoisonProjectile','sAnims_PoisonProjectile','sAnim_SludgeBombHit','sAnims_SludgeBombHit','sAffineAnim_PoisonProjectile','sAffineAnims_PoisonProjectile','sAffineAnim_SludgeBombHit','sAffineAnims_SludgeBombHit'],'src/battle_anim_electric.c':['sAffineAnim_FlashingSpark','sAffineAnims_FlashingSpark']}.items():
  arrays+='\n'.join(x.array(s[p],n) for n in ns)
 for p,ns in {'src/battle_anim_poison.c':['gSludgeProjectileSpriteTemplate','gSludgeBombHitParticleSpriteTemplate'],'src/battle_anim_electric.c':['gSparkElectricityFlashingSpriteTemplate','gSparkElectricitySpriteTemplate']}.items():
  for n in ns:arrays+=re.search(r'const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s[p])[0]+'\n'
 defs=x.macros(s.values(),['Q_8_8','sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY','sTransl_Speed','sTransl_Duration','sTransl_ArcAmpl','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_POISON_BUBBLE','ANIM_TAG_SPARK_2','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','ANIM_DEF_PARTNER','SPRITE_NONE'])
 defs+=x.enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')+'\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
 harness=(HERE/'harness.c.in').read_text();executables={};identities={}
 for policy in [False,True]:
  bodies='\n'.join(funcs)
  if policy:
   assert bodies.count('StartSpriteAnim(sprite, 2);')==1
   assert bodies.count('if (sprite->data[7] % sprite->data[4] == 0)')==1
   bodies=bodies.replace('StartSpriteAnim(sprite, 2);','StartSpriteAnim(sprite, 0);').replace('if (sprite->data[7] % sprite->data[4] == 0)','if (sprite->data[4] != 0 && sprite->data[7] % sprite->data[4] == 0)')
  c=harness.replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@',bodies)
  prefix='approved-policy' if policy else 'legal-source';source=out/(prefix+'.c');source.write_text(c);identities[prefix]=hashlib.sha256(c.encode()).hexdigest();executables[policy]=[]
  for opt in ['O0','O2']:
   exe=out/(prefix+'-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);executables[policy].append(exe)
 cases=[]
 def add(kind,d,actor,target,args,policy=False,label='legal-source',mask=None):
  args=list(args)+[0]*(8-len(args));mask=(15 if d else 3) if mask is None else mask;argv=[kind,d,actor,target,mask,*args]
  outputs=[subprocess.check_output([str(exe),*map(str,argv)],text=True) for exe in executables[policy]]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization mismatch '+str(argv))
  rows=[json.loads(row) for row in outputs[0].splitlines()]
  if not rows or rows[-1]['alive'] or rows[-1]['visualCount']:raise ValueError('Incomplete native trajectory')
  cases.append(dict(kind=kind,doubles=bool(d),actor=actor,target=target,visibleMask=mask,arguments=args,evidence='approved-policy' if policy else 'legal-source',branch=label,rows=rows))
 layouts=[(d,a,t) for d in [0,1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a!=t]
 hitargs=[[42,27,20],[-27,44,20],[39,-28,20],[-42,-42,20],[0,40,20],[-8,-44,20],[-46,-28,20],[46,9,20],[42,0,20],[-43,-12,20],[16,-46,20],[-16,44,20],[32767,-32768,7],[-32768,32767,1]]
 for d,a,t in layouts:
  for args in [[32,24,190,12,0,1,0],[80,24,22,12,0,1,0],[156,24,121,13,0,1,1],[100,24,60,10,0,1,0],[170,24,42,11,0,1,1],[238,24,165,10,0,1,1]]:add(3,d,a,t,args)
  for selector in [0,1,2,3]:
   for coordinate in [0,1]:
    add(3,d,a,t,[64,-32768,192,0,selector,coordinate,1],label='direct-matrix-native-width')
    if d:add(3,d,a,t,[128,32,64,2,selector,coordinate,0],label='hidden-attacker-partner',mask=15^(1<<(a^2)))
  add(0,d,a,t,[20,0,40,1]);add(0,d,a,t,[-17,11,7,1]);add(0,d,a,t,[20,0,40,0],True,'sole-legal-projectile-frame')
  for args in hitargs:add(1,d,a,t,args)
  for selector in [0,-32768]:
   for divisor in [1,3,7,32767]:add(2,d,a,t,[9,-7,24,12,224,20,2,selector|divisor])
   add(2,d,a,t,[32767,-32768,-32768,1,192,64,0,selector|3],label='native-width-offset-and-amplitude')
   for angle,amplitude,tile in [(0,32,0),(64,32,1),(128,32,0),(192,32,2),(32,16,0),(96,16,1),(160,16,0),(224,16,2)]:add(2,d,a,t,[0,0,amplitude,12,angle,20,tile,selector],True,'zero-divisor-no-flash')
 return dict(schema='firered-sludge-spark-v1',sourceSha256=pins,sourceFunctions=names,optimizationParity=['O0','O2'],generatedSha256=identities,semantics={'source':'Verbatim original callbacks, horizontal arc, signed fixed-point helper, frame/affine engine. Only legal Sludge mode1 and nonzero Spark divisors execute here. Hit particles use their full original callbacks.','policy':'Separate executables apply exactly two approved changes: Sludge StartSpriteAnim2 becomes0; Spark modulo test is guarded by divisor!=0. They are not claimed as original retail trajectories.','frame0':'Native CreateSpriteAndAnimate callback followed by source animation engine; later callback before engine; dead sprites skip engine.','stubs':'Explicit standard single/double geometry, no contest; native player pictureY +8; affine BIOS writes zero matrices, native scale and rotation retained. Raw allocator reserves actual battler cells and initializes fields; no gameplay or visual random helper is present.'},cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='sludge-spark-reference-'));out.mkdir(exist_ok=True,parents=True);result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(result['cases']),rows=sum(len(c['rows']) for c in result['cases']),source=sum(c['evidence']=='legal-source' for c in result['cases']),policy=sum(c['evidence']=='approved-policy' for c in result['cases']))))
