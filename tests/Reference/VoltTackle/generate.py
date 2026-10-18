#!/usr/bin/env python3
"""Explicit pinned native Volt Tackle orb, raw sweep and reappearance oracle."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference,out):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('source_extract',root/'tests/Reference/ConfusionKiss/source_extract.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for p,digest in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+p)
 header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
 oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
 engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSprite','CreateSpriteAndAnimate','SetSpriteSheetFrameTileNum','StartSpriteAnim','StartSpriteAffineAnim','SpriteCallbackDummy']
 names={'src/battle_anim_electric.c':['AnimVoltTackleOrbSlide','AnimVoltTackleOrbSlide_Step','AnimTask_VoltTackleAttackerReappear','AnimTask_VoltTackleBolt','CreateVoltTackleBolt','AnimVoltTackleBolt'],'src/battle_anim_mons.c':['DestroySpriteAndMatrix','GetBattlerSide','GetBattlerPosition'],'src/battle_anim.c':['DestroyAnimSprite'],'src/sprite.c':engine}
 funcs=[x.function(s[p],n) for p,ns in names.items() for n in ns];prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in funcs)
 arrays='\n'.join(x.array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'])
 arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
 for n in ['gOamData_AffineNormal_ObjBlend_64x64','gOamData_AffineDouble_ObjNormal_8x16']:
  arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
 for n in ['sAffineAnim_GrowingElectricOrb_0','sAffineAnim_GrowingElectricOrb_1','sAffineAnim_GrowingElectricOrb_2','sAffineAnims_GrowingElectricOrb','sAnim_VoltTackleBolt_0','sAnim_VoltTackleBolt_1','sAnim_VoltTackleBolt_2','sAnim_VoltTackleBolt_3','sAnims_VoltTackleBolt','sAffineAnim_VoltTackleBolt','sAffineAnims_VoltTackleBolt']:arrays+=x.array(s['src/battle_anim_electric.c'],n)+'\n'
 for n in ['gVoltTackleOrbSlideSpriteTemplate','gVoltTackleBoltSpriteTemplate']:arrays+=re.search(r'(?:static )?const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/battle_anim_electric.c'])[0]+'\n'
 defs=x.macros(s.values(),['ST_OAM_AFFINE_ON_MASK','ANIM_TAG_CIRCLE_OF_LIGHT','ANIM_TAG_SPARK','B_SIDE_PLAYER','B_SIDE_OPPONENT','BIT_SIDE','BATTLE_TYPE_DOUBLE','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','SPRITE_NONE'])
 defs+=x.enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')+'\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
 c=(Path(__file__).parent/'harness.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(funcs));source=out/'volt-tackle.c';source.write_text(c);exes=[]
 for opt in ['O0','O2']:
  exe=out/('volt-tackle-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);exes.append(exe)
 cases=[]
 def add(kind,d,a,t,args,occupied=0,release=0,hidden=0,offset=0,label='authored'):
  args=list(args)+[2000+i for i in range(len(args),8)];argv=[kind,d,a,t,occupied,release,hidden,offset,*args];outputs=[subprocess.check_output([str(e),*map(str,argv)],text=True) for e in exes]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization mismatch '+str(argv))
  rows=[json.loads(row) for row in outputs[0].splitlines()]
  if rows[-1]['alive']:raise ValueError('Incomplete source case')
  cases.append(dict(kind=kind,doubles=bool(d),actor=a,target=t,occupied=occupied,release=release,hidden=bool(hidden),offset=offset,arguments=args,label=label,rows=rows))
 for d,a,t in [(0,0,1),(0,1,0),(1,0,3),(1,1,2),(1,2,1),(1,3,0)]:
  add(0,d,a,t,[]);add(1,d,a,t,[])
  for mode in range(5):add(2,d,a,t,[mode])
  for hidden in [0,1]:add(1,d,a,t,[],hidden=hidden,offset=-301,label='visibility-and-offset');add(0,d,a,t,[],hidden=hidden,offset=17,label='retained-offset')
 for a,t in [(0,1),(1,0),(2,3),(3,2)]:
  for mode in [0,1,4]:
   add(2,1,a,t,[mode],occupied=60,label='all-allocations-fail');add(2,1,a,t,[mode],occupied=58,label='two-free-cells');add(2,1,a,t,[mode],occupied=60,release=4,label='capacity-released-mid-sweep')
 return dict(schema='firered-volt-tackle-source-v1',sourceSha256=pins,optimizationParity=['O0','O2'],sourceFunctions=names,semantics='Frame0 invokes original orb callback and AnimateSprite or original task state0 initializer. Later frames run original AnimateSprites then the original task callback. Raw bolts use original CreateSprite and are not counted visual sprites; successful children release only their parent counter after callback13. Numeric battler sprite IDs are fixture handles and never operands in this family. Platform coordinates use explicit pinned standard positions, Y offset0 and player+8. Source sprite allocator has64 real cells; occupied cells are inert external owners. Affine matrix allocation/BIOS output are platform stubs; original scale/rotation/END state and raw frame timing are observed. A cycling task field is deliberately not used as animation index because the original never selects it. Every row and terminal lifetime is retained. No managed code derives expectations.',cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='volt-tackle-reference-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']))))
