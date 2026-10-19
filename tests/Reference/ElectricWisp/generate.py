#!/usr/bin/env python3
"""Extract pinned electric/Wisp callbacks and complete native frame traces."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out,full=False):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('source_extract',root/'tests/Reference/source_extract.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for p,digest in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+p)
 header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
 oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
 engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSprite','CreateSpriteAndAnimate','SetSpriteSheetFrameTileNum','StartSpriteAnim','StartSpriteAffineAnim','SpriteCallbackDummy']
 names={'src/battle_anim_electric.c':['AnimTask_ElectricChargingParticles','AnimTask_ElectricChargingParticles_Step','AnimElectricChargingParticles','AnimElectricChargingParticles_Step','AnimGrowingChargeOrb','AnimElectricPuff','AnimGrowingShockWaveOrb','AnimTask_ShockWaveProgressingBolt','CreateShockWaveBoltSprite','AnimShockWaveProgressingBolt','AnimTask_ShockWaveLightning','CreateShockWaveLightningSprite','AnimShockWaveLightning','AnimLightning','AnimLightning_Step'],'src/battle_anim_fire.c':['AnimWillOWispOrb','AnimWillOWispOrb_Step','AnimWillOWispFire'],'src/battle_anim_mons.c':['InitAnimLinearTranslation','AnimTranslateLinear','InitAnimLinearTranslationWithSpeed','SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','RunStoredCallbackWhenAnimEnds','RunStoredCallbackWhenAffineAnimEnds','DestroySpriteAndMatrix','GetBattlerSide','GetBattlerPosition'],'src/battle_anim.c':['DestroyAnimSprite','BattleAnimAdjustPanning','KeepPanInRange'],'src/trig.c':['Sin','Cos'],'src/sprite.c':engine}
 funcs=[x.function(s[p],n) for p,ns in names.items() for n in ns]
 prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in funcs)
 arrays='\n'.join(x.array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'])+'\n'+x.array(s['src/trig.c'],'gSineTable')
 arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
 for n in ['gOamData_AffineNormal_ObjBlend_64x64','gOamData_AffineOff_ObjNormal_8x8','gOamData_AffineOff_ObjNormal_16x16','gOamData_AffineOff_ObjNormal_32x32']:
  arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
 for p,ns in {'src/battle_anim_electric.c':['sElectricChargingParticleCoordOffsets','sAnim_ElectricChargingParticles_0','sAnim_ElectricChargingParticles_1','sAnims_ElectricChargingParticles','sAffineAnim_GrowingElectricOrb_0','sAffineAnim_GrowingElectricOrb_1','sAffineAnim_GrowingElectricOrb_2','sAffineAnims_GrowingElectricOrb','sAnim_ElectricPuff','sAnims_ElectricPuff','sAnim_Lightning','sAnims_Lightning'],'src/battle_anim_fire.c':['sAnim_WillOWispOrb_0','sAnim_WillOWispOrb_1','sAnim_WillOWispOrb_2','sAnim_WillOWispOrb_3','sAnims_WillOWispOrb','sAnim_WillOWispFire','sAnims_WillOWispFire']}.items():
  arrays+='\n'.join(x.array(s[p],n) for n in ns)
 for p,ns in {'src/battle_anim_electric.c':['gElectricChargingParticlesSpriteTemplate','gGrowingChargeOrbSpriteTemplate','gElectricPuffSpriteTemplate','gGrowingShockWaveOrbSpriteTemplate','sShockWaveProgressingBoltSpriteTemplate','gLightningSpriteTemplate'],'src/battle_anim_fire.c':['gWillOWispOrbSpriteTemplate','gWillOWispFireSpriteTemplate']}.items():
  for n in ns:arrays+=re.search(r'(?:static )?const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s[p])[0]+'\n'
 defs=x.macros(s.values(),['Q_8_8','sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY','sTransl_Speed','sTransl_Duration','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_ELECTRIC_ORBS','ANIM_TAG_CIRCLE_OF_LIGHT','ANIM_TAG_ELECTRICITY','ANIM_TAG_SPARK','ANIM_TAG_LIGHTNING','ANIM_TAG_WISP_ORB','ANIM_TAG_WISP_FIRE','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','SPRITE_NONE','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','SE_M_THUNDERBOLT','SE_M_FLAME_WHEEL'])
 defs+=x.enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')+'\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
 c=(Path(__file__).parent/'harness.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(funcs));source=out/'electric-wisp.c';source.write_text(c)
 executables=[]
 for opt in ['O0','O2']:
  exe=out/('electric-wisp-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);executables.append(exe)
 cases=[]
 def add(kind,d,actor,target,args,occupied=0,release=0,pan=-27,label='authored'):
  args=list(args)+[2000+i for i in range(len(args),8)];argv=[kind,d,actor,target,occupied,release,pan,*args]
  outputs=[subprocess.check_output([str(exe),*map(str,argv)],text=True) for exe in executables]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization mismatch '+str(argv))
  rows=[json.loads(row) for row in outputs[0].splitlines()]
  if rows[-1]['alive']:raise ValueError('Incomplete source case')
  cases.append(dict(kind=kind,doubles=bool(d),actor=actor,target=target,occupied=occupied,release=release,customPan=pan,arguments=args,branch=label,rows=rows))
 layouts=[(0,0,1),(0,1,0),(1,3,2),(1,2,0)]
 if full:layouts=[(d,a,t) for d in [0,1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a!=t]
 for d,a,t in layouts:
  for selector in [0,1]:add(0,d,a,t,[selector]);add(1,d,a,t,[selector,16,16]);add(1,d,a,t,[selector,-16,-16])
  add(2,d,a,t,[])
  for variant in range(4):add(3,d,a,t,[0,0,variant])
  for angle in ([0,42,84,126,168,210] if full or a==0 else [210]):add(4,d,a,t,[angle])
  add(5,d,a,t,[0,60,2,12]);add(5,d,a,t,[0,20,0,2]);add(6,d,a,t,[]);add(7,d,a,t,[])
 # Allocation failure is scene capacity, never an injected random result.
 add(5,1,0,1,[0,20,0,2],occupied=60,release=12,label='full-capacity-retry')
 add(5,1,3,2,[1,20,0,2],occupied=57,label='three-available-cells')
 add(5,0,0,1,[0,1,0,1],label='single-birth-threshold')
 add(6,1,0,1,[],occupied=60,label='all-bolts-fail')
 add(6,1,3,1,[],occupied=58,label='two-available-bolts')
 add(7,1,0,1,[],occupied=60,label='all-lightning-fails')
 for pan in [-64,0,63]:add(3,0,0,1,[0,0,0],pan=pan,label='custom-pan-consumption')
 return dict(schema='firered-electric-wisp-source-v1',profile='full' if full else 'representative',sourceSha256=pins,optimizationParity=['O0','O2'],semantics='Frame0 is native creation callback then AnimateSprite, or immediate task initializer. Later task rows run original AnimateSprites then task callback. Source raw allocation uses 64 cells with only actual battler cells reserved; occupied cells are explicit inert external owners. BG priorities are explicit [2,1,1,2], subpriorities [30,40,20,50]; picture Y offsets are zero, with native player +8. Original SetSpriteSheetFrameTileNum initializes raw sheet frames before BeginAnim; dummy frame sentinels preserve raw tileNum offsets. BIOS affine output is stubbed; native scale/END/LOOP and frame timing are observed. Every selected frame is retained.',cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full',action='store_true');a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='electric-wisp-reference-'));out.mkdir(exist_ok=True,parents=True);result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.full);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(result['cases']),rows=sum(len(c['rows']) for c in result['cases']))))
