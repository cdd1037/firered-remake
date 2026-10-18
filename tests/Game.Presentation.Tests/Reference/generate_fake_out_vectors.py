#!/usr/bin/env python3
"""Extract Fake Out's actual C callbacks, battle init and VBlank register latch.

Only the authored adapter below is executable; no upstream build script is run.
The callback and frame fixtures are independent of the managed task translation.
"""
import argparse, importlib.util, json, re, subprocess, tempfile
from pathlib import Path

def function(source,name):
    m=re.search(r'^(?:static )?\w+ '+name+r'\([^;]+?\)\n\{',source,re.M)
    if not m: raise ValueError('Missing source function '+name)
    end=m.end(); depth=1
    while depth:
        depth+=(source[end]=='{')-(source[end]=='}'); end+=1
    return source[m.start():end]

def build(root):
    refpath=root/'content/raw/pokefirered/037335f4'
    spec=importlib.util.spec_from_file_location('reference_c',root/'tests/Reference/reference_c.py')
    helper=importlib.util.module_from_spec(spec); spec.loader.exec_module(helper);helper.verify_reference(refpath)
    effects=(refpath/'src/battle_anim_effects_2.c').read_text()
    mons=(refpath/'src/battle_anim_mons.c').read_text()
    bg=(refpath/'src/battle_bg.c').read_text(); main=(refpath/'src/battle_main.c').read_text()
    funcs=[function(effects,n) for n in ['AnimTask_FakeOut','AnimTask_FakeOut_Step1','AnimTask_FakeOut_Step2']]
    funcs+=[function(mons,'GetBattlePalettesMask'),function(bg,'InitBattleBgsVideo'),function(main,'VBlankCB_Battle')]
    h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "gba/io_reg.h"
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int16_t s16; typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define DISPLAY_WIDTH 240
#define DISPLAY_HEIGHT 160
#define RGB_WHITE 0x7fff
#define BATTLE_PARTNER(x) ((x)^2)
#define BG_ANIM_PAL_1 8
#define BG_ANIM_PAL_2 9
struct Task {s16 data[16];void(*func)(u8);} gTasks[1];
u16 gBattle_WIN0H,gBattle_WIN0V,gBattle_WIN1H,gBattle_WIN1V;
u16 gBattle_BG0_X,gBattle_BG0_Y,gBattle_BG1_X,gBattle_BG1_Y,gBattle_BG2_X,gBattle_BG2_Y,gBattle_BG3_X,gBattle_BG3_Y;
u8 gBattleAnimAttacker,gBattleAnimTarget;
static u16 registers[128];static int alive,contest,blends,blendAmount,blendColor;static u32 blendMask;
static int IsContest(void){return contest;}
static int IsBattlerSpriteVisible(int slot){return 1;}
static void SetGpuReg(int offset,u16 value){assert(offset%2==0&&offset/2<128);registers[offset/2]=value;}
static void DestroyAnimVisualTask(u8 id){assert(id==0);alive=0;}
static void BlendPalettes(u32 mask,int amount,int color){blends++;blendMask=mask;blendAmount=amount;blendColor=color;}
static void EnableInterrupts(int flags){}
static void BattleInitBgsAndWindows(void){}
static void Random(void){}
static void LoadOam(void){}
static void ProcessSpriteCopyRequests(void){}
static void TransferPlttBuffer(void){}
static void ScanlineEffect_InitHBlankDmaTransfer(void){}
'''
    h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)+r'''
static void reset(int mode){
 memset(gTasks,0,sizeof(gTasks));memset(registers,0,sizeof(registers));
 gBattle_WIN0H=gBattle_WIN0V=0;alive=1;contest=mode;blends=blendAmount=blendColor=0;blendMask=0;
 InitBattleBgsVideo();assert(registers[REG_OFFSET_DISPCNT/2]&DISPCNT_WIN0_ON);gTasks[0].func=AnimTask_FakeOut;
}
static void print(char kind,int ordinal){
 s16*d=gTasks[0].data;
 printf("%c %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d\n",kind,contest,ordinal,
 gTasks[0].func==AnimTask_FakeOut_Step2,d[0],d[1],d[10],d[11],alive,gBattle_WIN0H,gBattle_WIN0V,
 registers[REG_OFFSET_WIN0H/2],registers[REG_OFFSET_WIN0V/2],registers[REG_OFFSET_WININ/2],registers[REG_OFFSET_WINOUT/2],
 registers[REG_OFFSET_BLDCNT/2],registers[REG_OFFSET_BLDY/2],blends,blendMask,blendAmount,blendColor);
}
int main(void){
 for(int mode=0;mode<2;mode++){
  reset(mode);
  for(int ordinal=0;alive&&ordinal<32;ordinal++){gTasks[0].func(0);print('T',ordinal);}
  assert(!alive&&blends==1&&blendMask==0xe&&blendAmount==16&&blendColor==0x7fff);
  reset(mode);
  for(int frame=0;alive&&frame<32;frame++){
   if(frame==0)gTasks[0].func(0); // Cmd_createvisualtask calls initializer immediately.
   gTasks[0].func(0); // Same frame RunTasks, after BuildOamBuffer.
   VBlankCB_Battle();print('F',frame);
  }
  assert(!alive&&gBattle_WIN0H==0&&gBattle_WIN0V==0);
 }
}
'''
    with tempfile.TemporaryDirectory(prefix='fake-out-source-') as tmp:
        p=Path(tmp);(p/'fake.c').write_text(h)
        helper.compile_c(['cc','-std=c11','-O0','-I'+str(refpath/'include'),str(p/'fake.c'),'-o',str(p/'fake')])
        output=subprocess.check_output([str(p/'fake')],text=True)
    columns=['ordinal','closed','left','right','timer','unused88','alive','globalH','globalV','registerH','registerV','winin','winout','bldcnt','bldy','blends','blendMask','blendAmount','blendColor']
    result={'columns':columns,'tasks':{},'frames':{}}
    for row in output.splitlines():
        kind,mode,*values=row.split();values=list(map(int,values));assert len(values)==len(columns)
        result['tasks' if kind=='T' else 'frames'].setdefault(mode,[]).append(values)
    assert [len(result['tasks'][x]) for x in ['0','1']]==[17,13]
    assert [len(result['frames'][x]) for x in ['0','1']]==[16,12]
    normal=result['frames']['0'];assert normal[0][7]==0x0de3 and normal[9][7]==0
    assert normal[14][13]==0x88 and normal[15][13]==0
    return result

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    if a.output.exists():raise SystemExit('Refusing to overwrite an existing reference fixture.')
    a.output.parent.mkdir(parents=True,exist_ok=True);data=build(a.root);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n')
    print('30 source callback rows; 28 source frame/latch rows; battle WIN0 init and BG palette mask verified')
