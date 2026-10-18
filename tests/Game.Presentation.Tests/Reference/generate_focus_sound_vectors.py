#!/usr/bin/env python3
"""Complete pinned focus/sound callback cases, shared native players, O0/O2 parity.
Only host callbacks/pointers, loaded palettes, battle geometry and BIOS are adapted.
All selected cases retain every frame; --full uses the identical comparator schema.
"""
import argparse,hashlib,importlib.util,json,re,tempfile
from pathlib import Path
SOURCE_SHA256={'src/battle_anim_effects_3.c':'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f','src/battle_controller_player.c':'1fc12b9ed3e5f598eb1fc3761db3d57d44b5b68d6e432042d13d1679d76efa05'}
TEMPLATES=['gOpeningEyeSpriteTemplate','gWhiteHaloSpriteTemplate','gTealAlertSpriteTemplate','gLockOnTargetSpriteTemplate','gSnoreZSpriteTemplate','gJaggedMusicNoteSpriteTemplate','gUproarRingSpriteTemplate','AnimTask_UproarDistortion']
CONFIGS=[[[0,0,1,0],[17,-18,0,1],[32767,-32768,0,0],[-32768,32767,1,1]], [[]], [[70,0,6],[40,40,6],[10,-60,6],[-50,-40,6],[-40,40,6],[50,-50,6],[50,-30,6],[60,10,6],[0,60,6],[0,-40,6],[-60,20,6],[-60,-30,6],[-50,50,6],[-60,20,6],[-40,-40,6],[20,-60,6],[50,-50,6],[35,40,6],[0,0,1],[0,-1,255],[32767,-32768,6]], [[0,120],[1,120],[2,120],[3,120],[4,120],[5,120],[5,0],[5,180]], [[0,0,-42,-38,24,0,0],[0,0,0,-42,24,0,0],[0,0,42,-38,24,0,0],[17,-18,23,24,1,1,0],[17,-18,23,24,255,0,1]], [[0,29,-12,0],[0,-12,-29,1],[1,12,-29,1],[0,-29,-12,0],[0,24,-24,1],[0,-24,-24,0],[1,-32768,32767,1]], [[0,0,0,0,31,8],[17,-18,1,1,32767,0],[0,0,0,0,0,16],[32767,-32768,0,0,31,8],[-32768,32767,1,1,31,8]], [[0],[1],[2],[3]]]
COLUMNS=['slot','x','y','x2','y2','visible','tile','hflip','vflip','priority','subpriority','a','b','c','d','affine','double']
def module(n,p):
 s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def source(root):
 raw=root/'content/raw/pokefirered/037335f4';base=module('focus_native_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 for p,digest in SOURCE_SHA256.items():
  if hashlib.sha256((raw/p).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned focus/sound source mismatch: '+p)
 with tempfile.TemporaryDirectory() as d:
  p=Path(d)/'base.c';base.build(root,'tasks',p);h=p.read_text().split('static const long long config0')[0]
 h=h.replace('struct {u8 matrixNum;}healthBoxesData[4]','struct {u8 matrixNum,statusAnimActive;}healthBoxesData[4]')
 h=h.replace('u8 affineMode,matrixNum,shape,size,paletteNum,objMode;','u8 affineMode,matrixNum,shape,size,paletteNum,objMode;u8 priority:2;u16 affineParam;')
 h=h.replace('static bool8 IsBattlerSpriteVisible(u8 b){assert(b<slots);return TRUE;}','static bool8 IsBattlerSpriteVisible(u8 b){return b<slots;}')
 h=h.replace('static u8 GetAnimBattlerSpriteId(u8 which){return which?gBattleAnimTarget:gBattleAnimAttacker;}','static u8 GetAnimBattlerSpriteId(u8 which){return which==0?gBattleAnimAttacker:which==1?gBattleAnimTarget:which==2?(gBattleAnimAttacker^2):(gBattleAnimTarget^2);}')
 h=h.replace('static u8 IndexOfSpritePaletteTag(u16 tag){return tag==ANIM_TAG_LEAF?paletteSlots[0]:tag==ANIM_TAG_RAZOR_LEAF?paletteSlots[1]:0;}','static u8 IndexOfSpritePaletteTag(u16 tag){return 4;}')
 h+='\n#undef BlendPalette\n'+(root/'tests/Game.Presentation.Tests/Reference/bios_angles_adapter.c').read_text()+'\n'
 tags=(raw/'include/constants/battle_anim.h').read_text();songs=(raw/'include/constants/songs.h').read_text()
 for name in ['ANIM_SPRITES_START','ANIM_TAG_OPENING_EYE','ANIM_TAG_ROUND_WHITE_HALO','ANIM_TAG_TEAL_ALERT','ANIM_TAG_LOCK_ON','ANIM_TAG_SNORE_Z','ANIM_TAG_JAGGED_MUSIC_NOTE','ANIM_TAG_THIN_RING']:h+=re.search(r'^#define '+name+r'\s+[^\n]+',tags,re.M)[0]+'\n'
 for name in ['SE_M_LOCK_ON','SE_M_LEER']:h+=re.search(r'^#define '+name+r'\s+[^\n]+',songs,re.M)[0]+'\n'
 h+=r'''
#include "gba/io_reg.h"
#define SOUND_PAN_TARGET 63
#define SOUND_PAN_ATTACKER -64
#define RGB_WHITE 32767
#define ST_OAM_VFLIP 16
#define BATTLE_PARTNER(n) ((n)^2)
#define BG_ANIM_PAL_1 8
#define BG_ANIM_PAL_2 9
#define PLTT_SIZEOF(n) ((n)*2)
#define gDummySpriteAnimTable dummyFrames
#define gDummySpriteAffineAnimTable dummyAffines
static u16 gpu[128];static int tickNow,soundCount,soundIds[64],soundPans[64];
static void SetGpuReg(int offset,u16 value){gpu[offset/2]=value;}
s8 BattleAnimAdjustPanning(s8 pan);
static void PlaySE12WithPanning(u16 song,s8 pan){assert(soundCount<64);soundIds[soundCount]=song;soundPans[soundCount++]=pan;}
static void LoadPalette(const void*src,u16 offset,u16 size){memmove(gPlttBufferUnfaded+offset,src,size);memcpy(gPlttBufferFaded+offset,gPlttBufferUnfaded+offset,size);}
static void BlendPalettes(u32 mask,u8 coefficient,u16 color){blendWrites++;blendCoefficient=coefficient;blendColor=color;blendOffset0=(int)mask;for(int i=0;i<32;i++)if(mask&(1u<<i))BlendPalette(i*16,16,coefficient,color);}
static const struct OamData gOamData_AffineOff_ObjNormal_32x32={.size=2,.priority=2},gOamData_AffineOff_ObjBlend_64x64={.size=3,.objMode=1,.priority=2},gOamData_AffineNormal_ObjNormal_32x32={.size=2,.affineMode=1,.priority=2},gOamData_AffineOff_ObjNormal_16x16={.size=1,.priority=2},gOamData_AffineDouble_ObjBlend_64x64={.size=3,.affineMode=3,.objMode=1,.priority=2};
'''
 src={n:(raw/('src/'+n+'.c')).read_text() for n in ['battle_anim','battle_anim_mons','battle_anim_effects_1','battle_anim_effects_2','battle_anim_effects_3']}
 names={'battle_anim':['BattleAnimAdjustPanning'],'battle_anim_mons':['InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','SetAnimSpriteInitialXOffset','SetSpriteCoordsToAnimAttackerCoords','DestroySpriteAndMatrix','AnimSpriteOnMonPos','WaitAnimForDuration','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','TrySetSpriteRotScale','GetBattlePalettesMask','AnimTravelDiagonally'], 'battle_anim_effects_1':['AnimLockOnTarget','AnimLockOnTarget_Step1','AnimLockOnTarget_Step2','AnimLockOnTarget_Step3','AnimLockOnTarget_Step4','AnimLockOnTarget_Step5','AnimLockOnTarget_Step6','AnimLockOnMoveTarget'], 'battle_anim_effects_2':['AnimTask_UproarDistortion','AnimTask_UproarDistortion_Step','AnimJaggedMusicNote','AnimJaggedMusicNote_Step','AnimUproarRing'], 'battle_anim_effects_3':['AnimWhiteHalo','AnimWhiteHalo_Step1','AnimWhiteHalo_Step2','AnimTealAlert']}
 def function(code,name):
  m=re.search(r'^(?:static )?(?:void|s8|s16|s32|u8|u16|u32|bool8) '+name+r'\([^;]+?\)\n\{',code,re.M)
  if not m:raise ValueError('Missing function '+name)
  i=m.end();depth=1
  while depth:depth+=(code[i]=='{')-(code[i]=='}');i+=1
  return code[m.start():i]
 fs=[function(src[f],n) for f,ns in names.items() for n in ns];h+='\n'.join(f.split('\n{',1)[0]+';' for f in fs)+'\n'
 for file,prefix in [('battle_anim_effects_3','sOpeningEye'),('battle_anim_effects_2','sThinRingExpanding'),('battle_anim_effects_2','sUproarAffine')]:
  snippets=re.findall(r'static const union (?:AffineAnimCmd|AnimCmd) (?:\*const )?'+prefix+r'\w*\[\]\s*=\s*\{.*?\};',src[file],re.S);assert snippets;h+='\n'.join(snippets)+'\n'
 h+=re.search(r'static const s8 sInclineMonCoordTable\[\]\[2\]\s*=\s*\{.*?\};',src['battle_anim_effects_1'],re.S)[0]+'\n'
 for i,name in enumerate(TEMPLATES[:7]+['gLockOnMoveTargetSpriteTemplate']):
  file='battle_anim_effects_3' if i<3 else 'battle_anim_effects_1' if i in [3,7] else 'battle_anim_effects_2';h+=re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src[file],re.S)[0]+'\n'
 h+='\n'.join(fs)+'\n'
 for k,cs in enumerate(CONFIGS):h+='static const int config'+str(k)+'[][8]={'+','.join('{'+(','.join(map(str,c)) or '0')+'}' for c in cs)+'};\n'
 h+='static const int(*configs[])[8]={'+','.join('config'+str(i) for i in range(8))+'};\nstatic const int counts[]={'+','.join(map(str,map(len,CONFIGS)))+'};\n'
 h+='static const struct SpriteTemplate*templates[]={'+','.join('&'+n for n in TEMPLATES[:7])+'};\n'
 h+=r'''
static int own(struct Sprite*s){return s->inUse&&s->template!=NULL;}
static void emit(int kind,int cfg,int tick){
 printf("%d:%d:%d:%d:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,cfg,tick,gAnimVisualTaskCount,gTasks[0].isActive,gpu[REG_OFFSET_BLDCNT/2],gpu[REG_OFFSET_BLDALPHA/2],gBattleAnimArgs[1],gBattleAnimArgs[2],blendOffset0,blendCoefficient);
 for(int i=0;i<4;i++){struct OamMatrix m=gOamMatrices[i];printf(",%d,%d,%d,%d,%d,%d",gSprites[i].oam.affineMode,gSprites[i].y2,m.a,m.b,m.c,m.d);}putchar('|');
 int first=1;for(int i=0;i<64;i++){struct Sprite*s=&gSprites[i];if(!own(s))continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};if(!first)putchar(';');first=0;printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",i,s->x,s->y,s->x2,s->y2,!s->invisible,s->oam.tileNum,(s->oam.affineMode&1)?0:(s->oam.matrixNum>>3)&1,(s->oam.affineMode&1)?0:(s->oam.matrixNum>>4)&1,s->oam.priority,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode&1,s->oam.affineMode==3);}
 putchar('|');for(int i=0;i<16;i++)printf("%s%d,%d",i?",":"",gPlttBufferUnfaded[320+i],gPlttBufferFaded[320+i]);putchar('|');for(int i=0;i<soundCount;i++)printf("%s%d,%d",i?";":"",soundIds[i],soundPans[i]);putchar('\n');soundCount=0;
}
static void run(int kind,int a,int t,int cfg){
 slots=mode?4:2;gBattleAnimAttacker=a;gBattleAnimTarget=t;memset(gSprites,0,sizeof gSprites);memset(gTasks,0,sizeof gTasks);memset(sAffineAnimStates,0,sizeof sAffineAnimStates);memset(gBattleAnimArgs,0,sizeof gBattleAnimArgs);memset(gpu,0,sizeof gpu);gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;soundCount=0;blendOffset0=blendCoefficient=0;
 for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(i*997)&32767;
 for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->animPaused=s->affineAnimPaused=TRUE;s->oam.affineMode=ST_OAM_AFFINE_NORMAL;s->oam.matrixNum=i;s->data[0]=i;gBattleSpritesDataPtr->healthBoxesData[i].matrixNum=i;}
 for(int i=0;i<8;i++)gBattleAnimArgs[i]=configs[kind][cfg][i];
 if(kind==3){int corner=configs[kind][cfg][0];for(int c=(corner==5?0:corner);c<=(corner==5?4:corner);c++){gBattleAnimArgs[0]=c;CreateSpriteAndAnimate(c==0?templates[3]:&gLockOnMoveTargetSpriteTemplate,GetBattlerSpriteCoord(t,2),GetBattlerSpriteCoord(t,3),28);gAnimVisualTaskCount++;}}
 else if(kind<7){CreateSpriteAndAnimate(templates[kind],GetBattlerSpriteCoord(t,2),GetBattlerSpriteCoord(t,3),28);gAnimVisualTaskCount++;}
 else{int who=gBattleAnimArgs[0]==0?a:gBattleAnimArgs[0]==1?t:gBattleAnimArgs[0]==2?(a^2):(t^2);if(who>=slots)return;gTasks[0].isActive=TRUE;gAnimVisualTaskCount++;AnimTask_UproarDistortion(0);}
 emit(kind,cfg,-1);
 for(tickNow=0;tickNow<700;tickNow++){AnimateSprites();if(kind==3&&tickNow==configs[kind][cfg][1])gBattleAnimArgs[7]=-1;if(gTasks[0].isActive)gTasks[0].func(0);emit(kind,cfg,tickNow);if(!gAnimVisualTaskCount)return;}abort();
}
int main(void){for(int k=0;k<8;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)run(k,a,t,c);return 0;}
'''
 return h

def build(root,out,full=False):
 out.mkdir(parents=True,exist_ok=True);compiler=module('focus_compile',root/'tests/Game.Presentation.Tests/Reference/generate_cure_vectors.py');rows=compiler.compile_rows(root,source(root),'focus-sound-native',out);cases={}
 for line in rows.splitlines():
  header,sprites,palette,sounds=line.split('|');key,*state=header.split(',');cases.setdefault(key,[]).append(dict(state=list(map(int,state)),sprites=[list(map(int,s.split(','))) for s in sprites.split(';') if s],palette=list(map(int,palette.split(','))),sounds=[list(map(int,s.split(','))) for s in sounds.split(';') if s]))
 all_count=len(cases);selected=[]
 for k,configs in enumerate(CONFIGS):
  selected += [f'{k}:0:0:1:{c}' for c in range(len(configs)) if f'{k}:0:0:1:{c}' in cases]
  selected += [f'{k}:0:1:0:0']+[f'{k}:1:{a}:{t}:0' for a in range(4) for t in range(4)]
 selected+=['3:1:3:2:5','3:1:2:0:5','2:1:3:0:20','7:1:3:2:3','4:1:1:3:4'];assert len(selected)==len(set(selected))
 if not full:cases={k:cases[k] for k in selected}
 assert cases and all(v[0]['state'][0]==-1 and v[-1]['state'][1]==0 and not v[-1]['sprites'] for v in cases.values())
 return dict(templates=TEMPLATES,configs=CONFIGS,columns=COLUMNS,cases=cases,profile='full' if full else 'representative',coverage=dict(caseCount=len(cases),fullCaseCount=all_count,frames=sum(map(len,cases.values())),spriteRows=sum(len(f['sprites']) for v in cases.values() for f in v)),sourceHashes={**module("focus_hashes",root/"tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py").SOURCE_SHA256,**SOURCE_SHA256})
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();dest=a.output_dir/'focus-sound-source-vectors.json'
 if dest.exists():p.error('Refusing to overwrite native vectors')
 result=build(a.root,a.output_dir,a.full);dest.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
