#!/usr/bin/env python3
"""Generate source-only psychic utility vectors. Runs bounded authored C harnesses.

Every selected callback frame is retained. Ordinary tests never regenerate expected data.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def run_c(code,name,build,helper,args):
 path=build/(name+'.c');path.write_text(code);bins=[]
 for opt in ['O0','O2']:
  exe=build/(name+'-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(exe)]);bins.append(exe)
 result=[]
 for case in args:
  outputs=[subprocess.check_output([str(exe),*map(str,case)],text=True) for exe in bins]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization parity failure '+str(case))
  result.append([list(map(int,row.split(','))) for row in outputs[0].splitlines()])
 return result

def build(root,reference,build_dir,full=False):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('source_extract',root/'tests/Reference/source_extract.py')
 f=module('fang_harness',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 paths=['src/sprite.c','include/sprite.h','src/battle_anim_psychic.c','src/battle_anim_effects_2.c','src/battle_anim_mons.c','src/battle_anim_normal.c','src/palette.c','src/blend_palette.c','include/palette.h','include/gba/types.h','include/gba/defines.h','include/constants/rgb.h','data/battle_anim_scripts.s','src/battle_script_commands.c']
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text())
 for path in paths:
  if hashlib.sha256((reference/path).read_bytes()).hexdigest()!=pins[path]:raise ValueError('Pinned source changed: '+path)
 s={path:(reference/path).read_text() for path in paths}
 funcs=[f.function(s['src/battle_anim_psychic.c'],'AnimBentSpoon'),f.function(s['src/battle_anim_effects_2.c'],'AnimKinesisZapEnergy'),f.function(s['src/battle_anim_mons.c'],'RunStoredCallbackWhenAnimEnds'),f.function(s['src/battle_anim_mons.c'],'SetSpriteCoordsToAnimAttackerCoords'),f.function(s['src/battle_anim_mons.c'],'AnimSpriteOnMonPos'),f.function(s['src/sprite.c'],'StartSpriteAnim')]
 extra=r'''
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
static int doubles;static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];
static int GetBattlerSide(u8 id){return id&1;}
static int GetBattlerSpriteCoord(u8 id,u8 coord){static const int one[4][2]={{72,88},{176,48},{48,48},{112,88}},two[4][2]={{32,88},{200,48},{90,96},{152,40}};assert(coord==2||coord==3);return doubles?two[id][coord-2]:one[id][coord-2];}
static void InitSpritePosToAnimAttacker(struct Sprite*s,bool8 respect){assert(respect);s->x=GetBattlerSpriteCoord(gBattleAnimAttacker,2)+gBattleAnimArgs[0];s->y=GetBattlerSpriteCoord(gBattleAnimAttacker,3)+gBattleAnimArgs[1];}
static void InitSpritePosToAnimTarget(struct Sprite*s,bool8 respect){assert(0);}
static void DestroySpriteAndMatrix(struct Sprite*s){DestroyAnimSprite(s);}
'''
 h=f.sprite_harness(reference,funcs,extra)
 h=h.replace('struct {u8 affineMode;int tileNum;} oam;','struct {u8 affineMode;int tileNum,matrixNum;} oam;')
 h=h.replace('static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}',f.function(s['src/sprite.c'],'SetSpriteOamFlipBits'))
 for path,names in [('src/battle_anim_psychic.c',['sAnim_BentSpoon_0','sAnim_BentSpoon_1','sAffineAnim_PsychUpSpiral','sAffineAnim_LusterPurgeCircle']),('src/battle_anim_effects_2.c',['sKinesisZapEnergyAnimCmds'])]:
  h+='\n'.join(f.declaration(s[path],n) for n in names)+'\n'
 h+=r'''
static const union AnimCmd dummy[]={ANIMCMD_FRAME(0,1),ANIMCMD_JUMP(0)};static const union AnimCmd *const frames[]={dummy},*const spoon[]={sAnim_BentSpoon_0,sAnim_BentSpoon_1},*const zap[]={sKinesisZapEnergyAnimCmds};
static const union AffineAnimCmd *const spiral[]={sAffineAnim_PsychUpSpiral},*const circle[]={sAffineAnim_LusterPurgeCircle};
int main(int argc,char**argv){assert(argc==5);int kind=atoi(argv[1]);doubles=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=gBattleAnimAttacker^1;int config=atoi(argv[4]);struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->affineAnimBeginning=TRUE;
 if(kind==0){s->anims=spoon;s->callback=AnimBentSpoon;}else if(kind==1){s->anims=zap;s->callback=AnimKinesisZapEnergy;gBattleAnimArgs[0]=32;gBattleAnimArgs[1]=config?16:-8;gBattleAnimArgs[2]=config;}else{s->anims=frames;s->callback=AnimSpriteOnMonPos;s->oam.affineMode=kind==2?1:3;s->affineAnims=kind==2?spiral:circle;}
 for(int tick=-1;tick<512;tick++){AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",tick,s->inUse,s->x,s->y,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->animEnded,a->xScale,a->yScale,a->rotation,s->affineAnimEnded,s->callback==DestroyAnimSprite,s->animDelayCounter,s->animLoopCounter,s->data[0]);if(!s->inUse)return 0;}abort();}
'''
 sprite_cases=[(kind,d,actor,config) for d in [0,1] for actor in range(4 if d else 2) for kind in range(4) for config in (range(2) if kind==1 else [0])]
 sprite_rows=run_c(h,'sprites',build_dir,helper,sprite_cases)
 names={'src/battle_anim_normal.c':['AnimTask_BlendColorCycleExclude','BlendColorCycleExclude','AnimTask_BlendColorCycleExcludeLoop'],'src/palette.c':['UpdatePaletteFade','BeginNormalPaletteFade','UpdateNormalPaletteFade','IsSoftwarePaletteFadeFinishing'],'src/blend_palette.c':['BlendPalette']}
 bodies=[x.function(s[path],n) for path,ns in names.items() for n in ns];protos='\n'.join(body[:body.index('{')].strip()+';' for body in bodies)
 macros=x.macros([s['src/battle_anim_normal.c']],['tDelay','tNumBlends','tInitialBlendY','tTargetBlendY','tBlendColor','tRestoreBlend','tPalSelectorHi','tPalSelectorLo'])+x.macros([s[p] for p in ['src/palette.c','include/palette.h','include/gba/defines.h']],['gPaletteFade_selectedPalettes','gPaletteFade_delay','PALETTE_FADE_STATUS_LOADING','PALETTE_FADE_STATUS_DONE','PALETTE_FADE_STATUS_ACTIVE','PLTT_SIZE','OBJ_PLTT_OFFSET'])
 if not re.search(r'^#define tDelay\s+data\[1\]\s*$',macros,re.M):raise ValueError('Palette task macro scope differs')
 types=x.enum_containing(s['src/palette.c'],'NORMAL_FADE')+re.search(r'struct PaletteFadeControl\s*\{[\s\S]*?\n\};',s['include/palette.h'])[0]+'\n';types+=s['include/gba/types.h'][s['include/gba/types.h'].index('struct PlttData'):s['include/gba/types.h'].index('struct OamData')]
 h=(Path(__file__).parent/'palette-prefix.c.in').read_text().replace('@DEFINES@',macros).replace('@TYPES@',types).replace('@PROTOTYPES@',protos).replace('@FUNCTIONS@','\n'.join(bodies))
 task_cases=[(d,actor,actor^1,1,2,6,1,11,0,0) for d in [0,1] for actor in range(4 if d else 2)]
 task_cases += [(1,0,2,0,0,2,0,16,32767,0),(1,3,1,1,-1,3,2,13,31744,0),(1,2,0,1,0,1,0,10,31,1)]
 if full:task_cases += [(1,actor,target,bg,delay,cycles,0,16,31,prior) for actor in range(4) for target in range(4) if actor!=target for bg in [0,1] for delay in [-1,0,2] for cycles in [1,2,3,6] for prior in [0,1]]
 task_rows=run_c(h,'palette',build_dir,helper,task_cases)
 result=dict(schema='firered-psychic-utility-source-v1',profile='full' if full else 'explicit-branches',sourceSha256={p:pins[p] for p in paths},optimizationParity=['O0','O2'],semantics={'sprites':'frame -1 is original callback plus native BeginAnim; following rows are original AnimateSprites, including finite-loop boundaries, XOR flips, deferred callback destruction and affine END. BIOS matrix is a stub; native affine state is observed. Battler coordinate queries are explicit static picture centers: player YOffset0 plus native8; opponent YOffset8, elevation0. Positioned affine cases use authored all-zero arguments.','palette':'frame -1 is the original initializer; every subsequent frame clears transfer-pending, runs original UpdatePaletteFade, then original callback. Colors retain all 16 entries in banks1,2,3,16..19. Hidden-slot visibility is irrelevant in this original task.','copy':'Cmd_copyfoestats has an unused failure pointer; it always copies all seven stages. The product emits only changed assignments before its committed completion marker.'},sprites=[dict(kind=c[0],doubles=bool(c[1]),actor=c[2],config=c[3],rows=rows) for c,rows in zip(sprite_cases,sprite_rows)],palettes=[dict(doubles=bool(c[0]),actor=c[1],target=c[2],arguments=list(c[3:9]),prior=bool(c[9]),rows=rows) for c,rows in zip(task_cases,task_rows)])
 return result

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full',action='store_true');a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=(a.build_dir or Path(tempfile.mkdtemp(prefix='psychic-utility-reference-'))).resolve();out.mkdir(exist_ok=True,parents=True)
 result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.full);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps({k:dict(cases=len(result[k]),rows=sum(len(c['rows']) for c in result[k])) for k in ['sprites','palettes']}))
