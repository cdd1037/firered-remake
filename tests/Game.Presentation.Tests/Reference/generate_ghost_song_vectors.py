#!/usr/bin/env python3
"""Pinned whole-life ghost/song C callbacks and shared sprite/palette engines.
Every representative/boundary case keeps all rows. --full uses the same schema.
"""
import argparse,hashlib,importlib.util,json,re
from pathlib import Path
TEMPLATES=['gPerishSongMusicNoteSpriteTemplate','gPerishSongMusicNote2SpriteTemplate','gCurseNailSpriteTemplate','gCurseGhostSpriteTemplate','AnimTask_DestinyBondWhiteShadow','AnimTask_CurseStretchingBlackBg','AnimTask_DrawFallingWhiteLinesOnAttacker']
CONFIGS=[[[i,frame,i*16] for i,frame in enumerate([0,1,1,2,2,0,0,1,2,0,2,0,1,3,3,0])]+[[32767,7,-32768],[-32768,6,32767]],[[15,0,0],[0,0,0],[120,0,0]],[[],[17,-18],[-32768,32767]],[[]],[[0,48],[9,1],[120,255]],[[]],[[],[1,128,64,-64,128],[3,128,64,-64,128]]]
PINS={'include/constants/songs.h': 'ae4166b40bdde0bc014657636f63b49ebe0d3e00f7fb61531c99bf714eba36b4', 'include/gba/io_reg.h': '4b1d9592c1f2fdbefaa4f7f0d2751e5cef11069432303aa46bec142af57086d0', 'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae', 'src/battle_anim_ghost.c': '622823b0ad91a22d98b29964caf03a35409d8f667379d45477ad7f660c31955e', 'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb', 'src/battle_anim_effects_1.c': '80de93553f73982ca5b1abab6af952fe96e0b4763fefddf8aa1dc5071ad20682', 'src/battle_anim_effects_2.c': '50561e4882a0f8813f305af6f4147ed1c394657278e32aae6c733bef31994a04', 'src/battle_anim_utility_funcs.c': '8dbf0d05319980658ac1f698b1fed5a9dba02d1bda5aba487fec42d9ae16d297', 'src/palette.c': 'd406f56ba0a815d184753396e7e05120921c651a7bb2f5efff642816e64e7dc6', 'include/palette.h': '3f5ee66bab01538193629ffef3ff3cd6ad7555f7e294f52ae3ccfa36f94ac418', 'include/gba/defines.h': '4356e895d7108dedd2c788bbbe98e0948778712a4ea6374193bdc1a851efe69b', 'include/gba/types.h': 'd83a5cb845180b881f3de081ef9c2694b2da017a406fd3d6b0830329cc64ed93', 'include/constants/battle_anim.h': '87437751121d8f9dbcd62e2ba67122b9c7c68ce2255b55096982c7ca8c3cf48e', 'src/battle_anim_effects_3.c': 'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f', 'src/battle_controller_player.c': '1fc12b9ed3e5f598eb1fc3761db3d57d44b5b68d6e432042d13d1679d76efa05', 'src/battle_anim.c': '5883d9ee0483120ef67461952f5880499f322fb1c706cc213ca13c0c2b0e88c5', 'src/battle_anim_fight.c': '343a674d3230233af2ecd681c104f550b13e51485b011534c1d60d68ba65f5f5', 'src/battle_anim_flying.c': 'd51ff92894cbed0dd2c2434e73ed03bb2f2f20d930561f55fb0e31df4568ab0a', 'src/trig.c': '4144065a90e5376ce0259796a01ebaae33c6cc6c7ef716de95b9d9a13c85a810', 'src/blend_palette.c': '219a02b2ed9ab3010e203e0131fa8133571dcc271e025d7f6f14bf7993a51090', 'src/random.c': 'e7e90cc070d1fbf09579f5f91762cd8875d5bbe1d7a90f468bcd870df73b9de2', 'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773', 'include/battle_anim.h': '15d0a815fec54ed669e1cf7130d323070d799ecfba0f3a935da4b9c28d6f09a7', 'include/random.h': '8a6049183598b2d865b5ff522abdb3d0e94012a127d724af46b9b7b69803f5b9', 'data/battle_anim_scripts.s': 'f1566a2d43fad88619f4e3799f995bfd1c53b65fe8de4c4a91d66a440284ea9e', 'src/battle_main.c': 'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928', 'include/battle.h': 'bf64a16fb4e660f8a326eb352a511bb9e8fd54a74ebb03e328c08d5a23e5d8fa'}
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def source(root):
 raw=root/'content/raw/pokefirered/037335f4';base=module('ghost_focus_base',root/'tests/Game.Presentation.Tests/Reference/generate_focus_sound_vectors.py');h=base.source(root).split('static const int config0')[0]
 for path,digest in PINS.items():
  if hashlib.sha256((raw/path).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned ghost/song input differs: '+path)
 x=module('ghost_extract',root/'tests/Reference/Recovery/source_extract.py');src={p:(raw/p).read_text() for p in PINS}
 h=h.replace('static bool8 IsBattlerSpriteVisible(u8 b){return b<slots;}','static int hidden;static bool8 IsBattlerSpriteVisible(u8 b){return b<slots&&!(hidden&(1<<b));}')
 h=h.replace('static s16 GetBattlerSpriteCoordAttr(u8 b,u8 attr){assert(attr==0);return (s16[]){64,48,32,56}[b];}','static s16 GetBattlerSpriteCoordAttr(u8 b,u8 attr){s16 height=(s16[]){64,48,32,56}[b];assert(attr==0||attr==5);return attr==0?height:GetBattlerSpriteCoord(b,3)+height/2;}')
 # These adapters model static source battle geometry and VRAM entrypoints only.
 h+='\n'+src['include/gba/types.h'].split('struct BgCnt\n',1)[1].split('typedef volatile struct BgCnt vBgCnt;',1)[0].join(['struct BgCnt\n',''])+'\n'
 h+=r'''
#define MAX_BATTLERS_COUNT 4
#define BATTLER_COORD_ATTR_BOTTOM 5
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_RIGHT 3
#define MON_DATA_SPECIES 11
#define BG_PLTT_ID(p) ((p)*16)
#define PLTT_SIZE_4BPP 32
#define ST_OAM_OBJ_WINDOW 2
static u16 gBattle_WIN0H,gBattle_WIN0V,gBattle_BG1_X,gBattle_BG1_Y;static int clears;static u8 gBattlerSpriteIds[]={0,1,2,3},gBattlerPartyIndexes[4];static int gEnemyParty[6],gPlayerParty[6];
static int GetMonData(int*p,int field){return 1;}static int IsDoubleBattle(void){return mode;}static int GetBattlerPosition(int b){return b;}
static u16 GetGpuReg(int offset){return gpu[offset/2];}static void SetGpuRegBits(int offset,u16 value){gpu[offset/2]|=value;}
struct BattleAnimBgData {int bgId,paletteId,tilesOffset,bgTilemap;};
static void GetBattleAnimBg1Data(struct BattleAnimBgData*p){*p=(struct BattleAnimBgData){1,8,512,0};}
static void AnimLoadCompressedBgTilemap(int id,const void*p){assert(id==1);}static void RelocateBattleBgPal(int p,int m,int x,int y){abort();}static void AnimLoadCompressedBgGfx(int id,const void*p,int offset){assert(id==1&&offset==512);}static void ResetBattleAnimBg(int id){assert(id==0);clears++;}static void InitBattleAnimBg(int id){assert(id==1);}
static const u16 sRgbWhite[]={32767};static const u8 gFile_graphics_battle_anims_masks_curse_sheet[1],gFile_graphics_battle_anims_masks_curse_tilemap[1];
static u8 CreateInvisibleSpriteWithCallback(SpriteCallback callback){for(int i=slots;i<64;i++)if(!gSprites[i].inUse){gSprites[i].inUse=TRUE;gSprites[i].callback=callback;return i;}abort();}
static const struct OamData gOamData_AffineNormal_ObjNormal_16x16={.size=1,.affineMode=1,.priority=2},gOamData_AffineOff_ObjBlend_64x32={.shape=1,.size=3,.objMode=1,.priority=2},gOamData_AffineOff_ObjBlend_32x16={.shape=1,.size=2,.objMode=1,.priority=2},gOamData_AffineOff_ObjBlend_32x32={.size=2,.objMode=1,.priority=2};
'''
 macros=x.macros([src[p] for p in ['src/palette.c','include/palette.h','include/gba/defines.h']],['gPaletteFade_selectedPalettes','gPaletteFade_delay','PALETTE_FADE_STATUS_LOADING','PALETTE_FADE_STATUS_DONE','PALETTE_FADE_STATUS_ACTIVE','PLTT_SIZE','OBJ_PLTT_OFFSET','PLTT_ID']);h+=macros+x.enum_containing(src['src/palette.c'],'NORMAL_FADE')+re.search(r'struct PaletteFadeControl\s*\{[\s\S]*?\n\};',src['include/palette.h'])[0]+'\n'
 h+=r'''
static struct PaletteFadeControl gPaletteFade;static bool8 sPlttBufferTransferPending;static u8 paletteHardware[1024];
#define PLTT paletteHardware
static void CpuCopy32(const void*src,void*dest,int size){memcpy(dest,src,size);}static u8 UpdateFastPaletteFade(void){abort();}static u8 UpdateHardwarePaletteFade(void){abort();}static void UpdateBlendRegisters(void){abort();}
'''
 names={'src/sprite.c':['StartSpriteAnim'],'src/battle_anim_mons.c':['SetGreyscaleOrOriginalPalette','SetSpritePrimaryCoordsFromSecondaryCoords','CreateInvisibleSpriteCopy'],'src/battle_anim_effects_2.c':['AnimPerishSongMusicNote','AnimPerishSongMusicNote2','AnimPerishSongMusicNote_Step1','AnimPerishSongMusicNote_Step2'],'src/battle_anim_ghost.c':['AnimDestinyBondWhiteShadow','AnimDestinyBondWhiteShadow_Step','AnimTask_DestinyBondWhiteShadow','AnimTask_DestinyBondWhiteShadow_Step','AnimTask_CurseStretchingBlackBg','AnimTask_CurseStretchingBlackBg_Step1','AnimTask_CurseStretchingBlackBg_Step2','AnimCurseNail','AnimCurseNail_Step1','AnimCurseNail_Step2','AnimCurseNail_End','AnimGhostStatusSprite','AnimGhostStatusSprite_End'],'src/battle_anim_utility_funcs.c':['AnimTask_DrawFallingWhiteLinesOnAttacker','AnimTask_DrawFallingWhiteLinesOnAttacker_Step'],'src/palette.c':['UpdatePaletteFade','BeginNormalPaletteFade','UpdateNormalPaletteFade','IsSoftwarePaletteFadeFinishing']}
 funcs=[x.function(src[p],n) for p,ns in names.items() for n in ns];h+='\n'.join(f.split('{',1)[0].strip()+';' for f in funcs)+'\n'
 for n in ['ANIM_TAG_MUSIC_NOTES_2','ANIM_TAG_WHITE_SHADOW','ANIM_TAG_NAIL','ANIM_TAG_GHOSTLY_SPIRIT']:h+=re.search(r'^#define '+n+r'\s+[^\n]+',src['include/constants/battle_anim.h'],re.M)[0]+'\n'
 for n in ['sWavyMusicNotesAnimCmds'+str(i) for i in range(1,9)]+['gMusicNotesAnimTable']:h+=x.array(src['src/battle_anim_effects_1.c'],n)
 for n in ['sPerishSongMusicNoteAffineAnimCmds'+str(i) for i in range(1,4)]+['sPerishSongMusicNoteAffineAnimTable']:h+=x.array(src['src/battle_anim_effects_2.c'],n)
 for i,name in enumerate(TEMPLATES[:4]+['gDestinyBondWhiteShadowSpriteTemplate']):
  p='src/battle_anim_effects_2.c' if i<2 else 'src/battle_anim_ghost.c';h+=re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src[p],re.S)[0]+'\n'
 h+='\n'.join(funcs)+'\n'
 for k,cs in enumerate(CONFIGS):h+='static const int config'+str(k)+'[][8]={'+','.join('{'+(','.join(map(str,c)) or '0')+'}' for c in cs)+'};\n'
 h+='static const int(*configs[])[8]={'+','.join('config'+str(i) for i in range(7))+'};\nstatic const int counts[]={'+','.join(map(str,map(len,CONFIGS)))+'};\n'
 h+='static const struct SpriteTemplate*templates[]={'+','.join('&'+n for n in TEMPLATES[:4])+'};\n'
 h+=r'''
static int owned(struct Sprite*s){return s->inUse&&(s->template!=NULL||s->oam.objMode==2);}
static void emit(int kind,int cfg,int tick){
 printf("%d:%d:%d:%d:%d:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,cfg,hidden,tick,gAnimVisualTaskCount,gTasks[0].isActive,gpu[REG_OFFSET_BLDCNT/2],gpu[REG_OFFSET_BLDALPHA/2],gBattle_WIN0H,gBattle_WIN0V,gpu[REG_OFFSET_WININ/2],gpu[REG_OFFSET_WINOUT/2],gpu[REG_OFFSET_BLDY/2],gBattle_BG1_X,gBattle_BG1_Y,gpu[REG_OFFSET_BG1CNT/2]&3,gpu[REG_OFFSET_DISPCNT/2],gPaletteFade.active);
 for(int i=0;i<4;i++)printf(",%d",gSprites[i].oam.priority);putchar('|');
 int first=1;for(int i=0;i<64;i++){struct Sprite*s=&gSprites[i];if(!owned(s))continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};if(!first)putchar(';');first=0;printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",i,s->x,s->y,s->x2,s->y2,!s->invisible,s->oam.tileNum,(s->oam.affineMode&1)?0:(s->oam.matrixNum>>3)&1,(s->oam.affineMode&1)?0:(s->oam.matrixNum>>4)&1,s->oam.priority,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode&1,s->oam.affineMode==3,s->oam.objMode==2);}
 putchar('|');for(int b=0;b<2;b++)for(int i=0;i<16;i++){int index=(b==0?20:8)*16+i;printf("%s%d,%d",b||i?",":"",gPlttBufferUnfaded[index],gPlttBufferFaded[index]);}putchar('\n');
}
static void run(int kind,int a,int t,int cfg,int hide){
 hidden=hide;slots=mode?4:2;gBattleAnimAttacker=a;gBattleAnimTarget=t;memset(gSprites,0,sizeof gSprites);memset(gTasks,0,sizeof gTasks);memset(sAffineAnimStates,0,sizeof sAffineAnimStates);memset(gBattleAnimArgs,0,sizeof gBattleAnimArgs);memset(gpu,0,sizeof gpu);memset(&gPaletteFade,0,sizeof gPaletteFade);gBattle_WIN0H=gBattle_WIN0V=gBattle_BG1_X=gBattle_BG1_Y=0;gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;soundCount=clears=0;blendOffset0=blendCoefficient=0;
 for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(i*997)&32767;
 for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->animPaused=s->affineAnimPaused=TRUE;s->oam.affineMode=ST_OAM_AFFINE_NORMAL;s->oam.matrixNum=i;s->oam.priority=2;s->data[0]=i;s->invisible=(hidden>>i)&1;s->x=X[mode][i];s->y=(Y[mode][i]+(int[]){5,11,7,13}[i]-((i&1)?(int[]){2,3,4,5}[i]:0))&255;s->subpriority=GetBattlerSpriteSubpriority(i);gBattleSpritesDataPtr->healthBoxesData[i].matrixNum=i;}
 for(int i=0;i<8;i++)gBattleAnimArgs[i]=configs[kind][cfg][i];
 if(kind==6&&cfg){gSprites[a].oam.affineMode=configs[kind][cfg][0];SetOamMatrix(a,configs[kind][cfg][1],configs[kind][cfg][2],configs[kind][cfg][3],configs[kind][cfg][4]);gSprites[a].x2=5;gSprites[a].y2=-7;}
 if(kind<4){CreateSpriteAndAnimate(templates[kind],GetBattlerSpriteCoord(t,2),GetBattlerSpriteCoord(t,3),28);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gAnimVisualTaskCount++;(kind==4?AnimTask_DestinyBondWhiteShadow:kind==5?AnimTask_CurseStretchingBlackBg:AnimTask_DrawFallingWhiteLinesOnAttacker)(0);}
 emit(kind,cfg,-1);
 for(tickNow=0;tickNow<700;tickNow++){AnimateSprites();sPlttBufferTransferPending=FALSE;UpdatePaletteFade();if(gTasks[0].isActive)gTasks[0].func(0);emit(kind,cfg,tickNow);if(!gAnimVisualTaskCount)return;}abort();
}
int main(void){for(int k=0;k<7;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++){run(k,a,t,c,0);if(mode&&(k==4||k==6))run(k,a,t,c,1<<(k==4?(a^3):(a^2)));}return 0;}
'''
 return h

def build(root,out,full=False):
 out.mkdir(parents=True,exist_ok=True);compiler=module('ghost_compile',root/'tests/Game.Presentation.Tests/Reference/generate_cure_vectors.py');rows=compiler.compile_rows(root,source(root),'ghost-song-native',out);cases={}
 for line in rows.splitlines():
  header,sprites,palette=line.split('|');key,*state=header.split(',');cases.setdefault(key,[]).append(dict(state=list(map(int,state)),sprites=[list(map(int,s.split(','))) for s in sprites.split(';') if s],palette=list(map(int,palette.split(',')))))
 all_count=len(cases);selected=[]
 for k,configs in enumerate(CONFIGS):
  selected += [f'{k}:0:0:1:{c}:0' for c in range(len(configs))]
  selected += [f'{k}:0:1:0:0:0']+[f'{k}:1:{a}:{t}:0:0' for a in range(4) for t in range(4)]
  if k in [4,6]:selected += [f'{k}:1:{a}:{a^1}:0:{1<<(a^3 if k==4 else a^2)}' for a in range(4)]
 assert len(selected)==len(set(selected))
 if not full:cases={k:cases[k] for k in selected}
 assert cases and all(v[0]['state'][0]==-1 and v[-1]['state'][1]==0 and not v[-1]['sprites'] for v in cases.values())
 return dict(templates=TEMPLATES,configs=CONFIGS,cases=cases,profile='full' if full else 'representative',coverage=dict(caseCount=len(cases),fullCaseCount=all_count,frames=sum(map(len,cases.values())),spriteRows=sum(len(f['sprites']) for v in cases.values() for f in v)),sourceHashes=PINS)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();dest=a.output_dir/'ghost-song-source-vectors.json'
 if dest.exists():p.error('Refusing to overwrite native vectors')
 result=build(a.root,a.output_dir,a.full);dest.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
