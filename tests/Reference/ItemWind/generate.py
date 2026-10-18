#!/usr/bin/env python3
"""Pinned Trick/Recycle and shared flying-particle source callbacks, every selected frame."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out,full=False):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('source_extract',root/'tests/Reference/ConfusionKiss/source_extract.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for p,digest in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+p)
 header=s['include/sprite.h'];types=header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
 oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
 engine=['AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','CopyOamMatrix','GetSpriteMatrixNum','SetSpriteOamFlipBits','AffineAnimStateRestartAnim','AffineAnimStateReset','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAnimDelayCounter','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','AnimateSprites','CreateSprite','CreateSpriteAndAnimate','SetSpriteSheetFrameTileNum','StartSpriteAnim','StartSpriteAffineAnim','SpriteCallbackDummy']
 names={'src/battle_anim_effects_1.c':['AnimTrickBag','AnimTrickBag_Step1','AnimTrickBag_Step2','AnimTrickBag_Step3','AnimFlyingParticle','AnimFlyingParticle_Step'],'src/battle_anim_effects_3.c':['AnimRecycle','AnimRecycle_Step'],'src/battle_anim_utility_funcs.c':['AnimTask_GetTargetSide'],'src/battle_anim_mons.c':['DestroySpriteAndMatrix','GetBattlerSide','GetBattlerPosition'],'src/battle_anim.c':['DestroyAnimSprite'],'src/trig.c':['Sin','Cos'],'src/sprite.c':engine}
 funcs=[x.function(s[p],n) for p,ns in names.items() for n in ns]
 prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in funcs)
 arrays='\n'.join(x.array(s['src/sprite.c'],n) for n in ['sAnimFuncs','sAffineAnimFuncs','sAnimCmdFuncs','sAffineAnimCmdFuncs'])+'\n'+x.array(s['src/trig.c'],'gSineTable')
 # Original sentinel tables leave animBeginning set; BeginAnim returns without changing tile or END.
 arrays+='\nstatic const union AnimCmd sDummyAnim={.type=-1};static const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};static const union AffineAnimCmd sDummyAffineAnim={.type=32767};static const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\n'
 for n in ['gOamData_AffineNormal_ObjNormal_32x32','gOamData_AffineNormal_ObjNormal_16x16','gOamData_AffineNormal_ObjBlend_64x64']:
  arrays+=re.search(r'const struct OamData '+n+r'\s*=\s*\{[\s\S]*?\n\};',s['src/data/battle_anim.h'])[0]+'\n'
 for p,ns in {'src/battle_anim_effects_1.c':['sFallingBagAnimCmds','sFallingBagAnimTable','sFallingBagAffineAnimCmds1','sFallingBagAffineAnimCmds2','sTrickBagAffineAnimCmds1','sTrickBagAffineAnimCmds2','sTrickBagAffineAnimTable','gTrickBagCoordinates','sSilverWindBigSparkAffineAnimCmds','sSilverWindMediumSparkAffineAnimCmds','sSilverWindSmallSparkAffineAnimCmds','sSilverWindBigSparkAffineAnimTable','sSilverWindMediumSparkAffineAnimTable','sSilverWindSmallSparkAffineAnimTable'],'src/battle_anim_effects_3.c':['sRecycleSpriteAffineAnimCmds','sRecycleSpriteAffineAnimTable']}.items():
  arrays+='\n'.join(x.array(s[p],n) for n in ns)
 for p,ns in {'src/battle_anim_effects_1.c':['gTrickBagSpriteTemplate','gSilverWindBigSparkSpriteTemplate','gSilverWindMediumSparkSpriteTemplate','gSilverWindSmallSparkSpriteTemplate'],'src/battle_anim_effects_3.c':['gRecycleSpriteTemplate']}.items():
  for n in ns:arrays+=re.search(r'(?:static )?const struct SpriteTemplate '+n+r'\s*=\s*\{[\s\S]*?\n\};',s[p])[0]+'\n'
 defs=x.macros(s.values(),['Q_8_8','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_ITEM_BAG','ANIM_TAG_RECYCLE','ANIM_TAG_SPARKLE_6','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','SPRITE_NONE','BLDALPHA_BLEND','REG_OFFSET_BLDALPHA'])
 defs+=x.enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_X')+'\n'+x.enum_containing(s['include/battle_anim.h'],'BATTLER_COORD_ATTR_TOP')+'\n'+oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
 c=(Path(__file__).parent/'harness.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(funcs));source=out/'item-wind.c';source.write_text(c)
 executables=[]
 for opt in ['O0','O2']:
  exe=out/('item-wind-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);executables.append(exe)
 cases=[]
 def add(kind,d,actor,target,args,height=64,yoff=0,label='authored'):
  args=list(args)+[2000+i for i in range(len(args),8)];argv=[kind,d,actor,target,height,yoff,*args]
  outputs=[subprocess.check_output([str(exe),*map(str,argv)],text=True) for exe in executables]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization mismatch '+str(argv))
  rows=[json.loads(row) for row in outputs[0].splitlines()]
  if rows[-1]['alive']:raise ValueError('Incomplete source case')
  cases.append(dict(kind=kind,doubles=bool(d),actor=actor,target=target,height=height,yOffset=yoff,arguments=args,branch=label,rows=rows))
 layouts=[(0,0,1),(0,1,0),(1,3,2),(1,2,0)]
 if full:layouts=[(d,a,t) for d in [0,1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a!=t]
 authored=[[-32,16,0,6,2,3,1],[-8,18,64,3,2,2,1],[-24,18,90,5,1,2,1],[-40,14,128,4,1,2,1],[-4,16,0,6,1,2,1],[-16,12,192,5,2,3,1]]
 for d,a,t in layouts:
  for angle in [80,208]:add(0,d,a,t,[-40,angle])
  add(1,d,a,t,[])
  for kind in [2,3,4]:
   for args in authored:add(kind,d,a,t,args)
  add(5,d,a,t,[])
 for angle in [0,1,191,192,255]:add(0,0,0,1,[-40,angle],label='subpriority-boundary')
 for y in [-256,78,79,256]:add(0,0,0,1,[y,80],label='fall-state-boundary')
 for height in [8,32,64]:
  for off in [0,7,20]:add(1,0,1,0,[],height,off,'top-clamp')
 for mode in range(4):
  for selector in [0,1]:add(2,1,3,0,[-16,-12,255,1,255,mode,selector],label='shared-position-modes')
 add(4,0,0,1,[0,0,0,255,0,0,1],label='maximum-speed')
 return dict(schema='firered-item-wind-source-v1',profile='full' if full else 'representative',sourceSha256=pins,optimizationParity=['O0','O2'],semantics='Frame0 is source creation callback then AnimateSprite; later rows use original AnimateSprites. Native affine scale/rotation, frames, data, alpha and visibility are compared every selected frame. Picture heights and offsets are explicit input fixtures, player picture Y includes source +8. BIOS matrix output is stubbed; source affine state is observed. The target-side task destroys immediately. No upstream script is executed.',cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full',action='store_true');a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='item-wind-reference-'));out.mkdir(exist_ok=True,parents=True);result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.full);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(result['cases']),rows=sum(len(c['rows']) for c in result['cases']))))
