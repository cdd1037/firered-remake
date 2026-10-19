#!/usr/bin/env python3
"""Whole-life pinned Surf callbacks, task scheduler and scanline engine.
Retains every frame and every changed word from both 960-word banks. No pixels
or palette programs are guessed; only host register/DMA/material transport is adapted.
"""
import argparse,hashlib,importlib.util,json,re,struct
from pathlib import Path
PINS={'src/battle_anim_water.c': '8cd5ed3f7151528a55846f9d94c8aa40cec3635f', 'src/scanline_effect.c': '7b50649a25d2e9762a5cf47be9815148a751e44b', 'include/scanline_effect.h': '5bd8b554686ebd85eaf21ea551ff722a5d8c8984', 'src/task.c': '01503dc7ae78cf2348524bfb5bbb89183640f944', 'include/gba/io_reg.h': 'a8a36f7e295855dbc4281f5f9a9f223ddda34a16', 'graphics/battle_anims/backgrounds/water.png': '7740f99f8bf1709ec65e21e97b9a6386b40e7d22', 'graphics/battle_anims/backgrounds/water_muddy.pal': '0cb7218d9db47485608dc7b95a7dd240e9383aeb'}
CONFIGS=[[0,2,0],[1,2,1],[-32768,0,0],[32767,254,1],[0,255,0],[1,255,1]] # argument, source task priority, initial scanline write bank

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def source(root):
 raw=root/'content/raw/pokefirered/037335f4';src={}
 for p,sha in PINS.items():
  data=(raw/p).read_bytes();assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==sha,p
  if p.endswith(('.c','.h')):src[p]=data.decode()
 x=module('surf_extract',root/'tests/Reference/source_extract.py');bg=module('surf_bg',root/'scripts/imports/battle/import-source-animation-backgrounds.py')
 pals=[struct.unpack('<16H',bg.indexed_tiles((raw/'graphics/battle_anims/backgrounds/water.png').read_bytes())[1]),struct.unpack('<16H',bg.palette((raw/'graphics/battle_anims/backgrounds/water_muddy.pal').read_bytes()))]
 h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef uint8_t bool8;typedef volatile u16 vu16;typedef volatile u32 vu32;
#define TRUE 1
#define FALSE 0
#define EWRAM_DATA
#define COMMON_DATA
#define NUM_TASKS 16
#define REVISION 0
#define B_SIDE_OPPONENT 1
#define BG_ANIM_PRIORITY 0
#define BG_ANIM_SCREEN_SIZE 3
#define BG_ANIM_CHAR_BASE_BLOCK 1
#define BG_PLTT_ID(x) ((x)*16)
#define PLTT_SIZE_4BPP 32
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
typedef void(*TaskFunc)(u8);struct Task{TaskFunc func;bool8 isActive;u8 prev,next,priority;s16 data[16];};void TaskDummy(u8 id);
static u8 gBattleAnimAttacker;static s16 gBattleAnimArgs[8];static u16 gBattle_BG1_X,gBattle_BG1_Y,gPlttBufferUnfaded[512],gPlttBufferFaded[512];static u16 registers[128],logicalRegisters[128];static int clears,overlay,loadedPalette;static u8 attributes[4];
#define REG_ADDR_BLDALPHA (&registers[REG_OFFSET_BLDALPHA/2])
static void SetGpuReg(int offset,u16 value){registers[offset/2]=logicalRegisters[offset/2]=value;}
static const u16*dmaSource;static volatile u16*dmaDest;
static void DmaStop(int id){assert(id==0);dmaSource=NULL;dmaDest=NULL;}static void DmaSet(int id,const void*s,volatile void*d,u32 control){assert(id==0);dmaSource=s;dmaDest=d;}
static void CpuFill16(int value,void*d,size_t size){assert(value==0);memset(d,0,size);}
static int IsContest(void){return 0;}static int GetBattlerSide(int b){return b&1;}
struct BattleAnimBgData{int bgId,paletteId,tilesOffset,bgTilemap;};
static void GetBattleAnimBg1Data(struct BattleAnimBgData*b){*b=(struct BattleAnimBgData){1,8,512,0};}
static void SetAnimBgAttribute(int id,int attr,int value){assert(id==1);attributes[attr]=value;}
static void AnimLoadCompressedBgTilemap(int id,const void*p){assert(id==1);overlay=1;}static void AnimLoadCompressedBgGfx(int id,const void*p,int offset){assert(id==1&&offset==512);}
static void LZDecompressVram(const void*p,int dest){abort();}static void RelocateBattleBgPal(int p,int map,int x,int y){abort();}
static void InitBattleAnimBg(int id){assert(id==1||id==2);clears++;overlay=0;}
static const u8 gBattleAnimBgImage_Surf[1],gBattleAnimBgTilemap_SurfPlayer[1],gBattleAnimBgTilemap_SurfOpponent[1],gBattleAnimBgTilemap_SurfContest[1];
'''
 h+=x.macros([src['include/gba/io_reg.h']],['REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_BG1HOFS','REG_OFFSET_BG1VOFS','BLDCNT_TGT1_BG1','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','DMA_ENABLE','DMA_START_HBLANK','DMA_REPEAT','DMA_SRC_INC','DMA_DEST_INC','DMA_16BIT','DMA_32BIT','DMA_DEST_RELOAD'])
 for name,pal in zip(['Surf','MuddyWater'],pals):h+='static const u16 gBattleAnimBgPalette_'+name+'[]={'+','.join(map(str,pal))+'};\n'
 h+=r'''
static void LoadCompressedPalette(const void*p,int offset,int size){assert(offset==128&&size==32);memcpy(gPlttBufferUnfaded+offset,p,size);memcpy(gPlttBufferFaded+offset,p,size);loadedPalette=p==gBattleAnimBgPalette_MuddyWater;}
'''
 h+=re.sub(r'^#include[^\n]*\n','',src['src/task.c'].split('void SetTaskFuncWithFollowupFunc')[0],flags=re.M)
 h+='static void DestroyAnimVisualTask(u8 id){DestroyTask(id);}\n'+src['include/scanline_effect.h']+'\n'
 h+=re.sub(r'^#include[^\n]*\n','',src['src/scanline_effect.c'].split('#define tStartLine')[0],flags=re.M)
 funcs=[x.function(src['src/battle_anim_water.c'],n) for n in ['AnimTask_CreateSurfWave','AnimTask_CreateSurfWave_Step1','AnimTask_CreateSurfWave_Step2','AnimTask_SurfWaveScanlineEffect']]
 h+='\n'.join(f.split('{',1)[0].strip()+';' for f in funcs)+'\n'+'\n'.join(funcs)
 h+='\nstatic const int configs[][3]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'
 h+=r'''
static u16 previous[2][960];static int actor,cfg;
static void emit(int tick){
 printf("%d:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d|",actor,cfg,tick,gTasks[0].isActive,gTasks[1].isActive,overlay,clears,gBattle_BG1_X,gBattle_BG1_Y,logicalRegisters[REG_OFFSET_BLDCNT/2],logicalRegisters[REG_OFFSET_BLDALPHA/2],attributes[0],attributes[3],gScanlineEffect.state,gScanlineEffect.srcBuffer);
 for(int i=0;i<16;i++)printf("%s%d,%d",i?",":"",gPlttBufferUnfaded[128+i],gPlttBufferFaded[128+i]);putchar('|');int first=1;
 for(int b=0;b<2;b++)for(int i=0;i<960;i++)if(tick==-1||previous[b][i]!=gScanlineEffectRegBuffers[b][i]){printf("%s%d,%u",first?"":";",b*960+i,gScanlineEffectRegBuffers[b][i]);previous[b][i]=gScanlineEffectRegBuffers[b][i];first=0;}putchar('\n');
}
int main(void){for(actor=0;actor<4;actor++)for(cfg=0;cfg<sizeof(configs)/sizeof(configs[0]);cfg++){
 ResetTasks();ScanlineEffect_Clear();memset(registers,0,sizeof registers);memset(logicalRegisters,0,sizeof logicalRegisters);memset(attributes,0,sizeof attributes);clears=overlay=0;gBattle_BG1_X=gBattle_BG1_Y=0;gBattleAnimAttacker=actor;gBattleAnimArgs[0]=configs[cfg][0];gScanlineEffect.srcBuffer=configs[cfg][2];
 for(int b=0;b<2;b++)for(int i=0;i<960;i++)gScanlineEffectRegBuffers[b][i]=(u16)(0x1234+b*17011+i*977);
 for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(i*997)&32767;
 u8 id=CreateTask(AnimTask_CreateSurfWave,configs[cfg][1]);assert(id==0);AnimTask_CreateSurfWave(id);emit(-1);
 int tick;for(tick=0;tick<200;tick++){RunTasks();emit(tick);ScanlineEffect_InitHBlankDmaTransfer();if(dmaSource)for(int line=0;line<160;line++)*dmaDest=dmaSource[line];if(!gTasks[0].isActive&&!gTasks[1].isActive)break;}assert(tick<200&&gScanlineEffect.state==0&&clears==2);
 }return 0;}
'''
 return h

def build(root,out,full=False):
 out.mkdir(parents=True,exist_ok=True);c=module('surf_compile',root/'tests/Game.Presentation.Tests/Reference/generate_cure_vectors.py');rows=c.compile_rows(root,source(root),'surf-native',out);cases={}
 for line in rows.splitlines():
  state,palette,changes=line.split('|');key,*state=state.split(',');cases.setdefault(key,[]).append(dict(state=list(map(int,state)),palette=list(map(int,palette.split(','))),changes=[list(map(int,p.split(','))) for p in changes.split(';') if p]))
 assert len(cases)==24 and all(len(v)>100 and v[-1]['state'][1:3]==[0,0] for v in cases.values())
 return dict(configs=CONFIGS,cases=cases,coverage=dict(caseCount=len(cases),frames=sum(map(len,cases.values()))),sourceBlobs=PINS)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();dest=a.output_dir/'surf-source-vectors.json'
 if dest.exists():p.error('Refusing to overwrite source vectors')
 result=build(a.root,a.output_dir,a.full);dest.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
