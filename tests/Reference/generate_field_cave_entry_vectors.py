"""Extract pinned ENTER/preview callbacks into local, ignored numerical traces.

No upstream scripts run. Authored readiness observations are explicit: the host
never pretends that decompression or DMA takes a fixed number of hardware cycles.
The source UpdatePaletteFade transfer-pending gate is preserved, including both
its task-local call and CB2's trailing call. Generic ENTER records all palette
writes and pixel composites; previews record exact scheduling and handoff state.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

from reference_c import COMMIT, compile_c
from generate_field_cave_exit_vectors import HEADER as CAVE_HEADER, read_palettes

HASHES = {
    'src/fldeff_flash.c': 'e9d52450071d1a7b54c1774c98b1317910fbdbc6916e4839c42d9ae5dc8307a9',
    'src/map_preview_screen.c': '6172d9268cb0f7bb77678b0d814e4fc800f11b5e8d49d009a4f7b33892109264',
    'src/palette.c': 'd406f56ba0a815d184753396e7e05120921c651a7bb2f5efff642816e64e7dc6',
}
BLACK_HASH = '226f0ddc9a6d3b26b78e138333dfde67d39aa7ac95a257e9e53c766fb6035675'


def extract(source, name):
    match = re.search(r'^(?:static )?(?:bool8|u8|u16|void) ' + re.escape(name)
                      + r'\([^;]*?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing extracted callback: ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end] + '\n'


HEADER = r'''
typedef int8_t s8; typedef int32_t s32; typedef int bool8;
typedef void(*IntrCallback)(void);
#define TRUE 1
#define FALSE 0
#define PALETTE_FADE_STATUS_DONE 0
#define PALETTE_FADE_STATUS_ACTIVE 1
#define PALETTE_FADE_STATUS_LOADING 255
#define OBJ_PLTT_OFFSET 256
#define NORMAL_FADE 0
#define FAST_FADE 1
#define HARDWARE_FADE 2
#define PLTT_SIZE 0
#define PLTT NULL
#define CpuCopy32(a,b,c) ((void)0)
#define PALETTES_ALL 0xffffffffu
#define RGB_BLACK 0
#define RGB_WHITE 32767
#define COPYWIN_FULL 3
#define B_BUTTON 2
#define JOY_HELD(button) (heldB ? (button) : 0)
#define MPS_TYPE_CAVE 1
#define MPS_COUNT 1
struct Palette { int active,deltaY,delayCounter,delay,y,targetY,blendColor,mode,yDec,
    bufferTransferDisabled,objPaletteToggle,softwareFadeFinishing,softwareFadeFinishingCounter;
    u32 multipurpose1; } gPaletteFade;
#define gPaletteFade_delay gPaletteFade.delay
#define gPaletteFade_selectedPalettes gPaletteFade.multipurpose1
u32 sPlttBufferTransferPending; int bgBlend,objBlend;
u16 gPlttBufferFaded[512],sCaveTransitionPalette_Black[16];
int visited,graphicsReady,windowCopyReady,heldB,nameWindow,observedHold,enterFrame;
int graphicsReadyFrom,windowCopyReadyFrom,bHeldFrom,bHeldThrough;
uintptr_t savedWord;
struct MapPreview { u8 type; u16 flagId; } sMapPreviewScreenData[1]={{MPS_TYPE_CAVE,1}};
int sHasVisitedMapBefore;
u8 GetMapPreviewScreenIdx(u8 mapsec){(void)mapsec;return 0;}
int FlagGet(u16 flag){(void)flag;return visited;}
void BlendPalette(int offset,int count,int y,int color)
{
    (void)count;
    if(offset<256)bgBlend=y;else objBlend=y;
    if(offset==0&&y==16)palette[0]=(u16)color;
}
void BlendPalettes(u32 mask,u8 coeff,u16 color)
{if(mask!=PALETTES_ALL||coeff!=16||color!=RGB_BLACK)exit(7);bgBlend=objBlend=coeff;palette[0]=color;}
void UpdateBlendRegisters(void){}
static u8 UpdateNormalPaletteFade(void);
static bool8 IsSoftwarePaletteFadeFinishing(void);
static u8 UpdateFastPaletteFade(void){exit(8);}
static u8 UpdateHardwarePaletteFade(void){exit(9);}
u8 UpdatePaletteFade(void);
void VBlank(void){sPlttBufferTransferPending=0;}
void SetVBlankCallback(IntrCallback callback){gMain.vblankCallback=callback;}
void SetWordTaskArg(u8 task,u8 index,uintptr_t value){(void)task;(void)index;savedWord=value;}
uintptr_t GetWordTaskArg(u8 task,u8 index){(void)task;(void)index;return savedWord;}
void MapPreview_InitBgs(void){}
void MapPreview_LoadGfx(u8 mapsec){(void)mapsec;}
int MapPreview_IsGfxLoadFinished(void){return !graphicsReady;}
u16 MapPreview_CreateMapNameWindow(u8 mapsec){(void)mapsec;nameWindow=1;return 0;}
void CopyWindowToVram(u16 window,u8 mode){if(window||mode!=COPYWIN_FULL)exit(10);}
int IsDma3ManagerBusyWithBgCopy(void){return !windowCopyReady;}
void MapPreview_Unload(s32 window){if(window!=0)exit(11);nameWindow=0;}
void RunTasks(void){gTasks[0].func(0);}
void AnimateSprites(void){}
void BuildOamBuffer(void){}
'''

MAIN = r'''
void colors(const u16* values,int count)
{printf("[");for(int i=0;i<count;i++){if(i)printf(",");printf("%u",values[i]);}printf("]");}
int enterPhase(void)
{
    if(gTasks[0].func==Task_FlashTransition_Enter_0)return 0;
    if(gTasks[0].func==Task_FlashTransition_Enter_1)return 1;
    if(gTasks[0].func==Task_FlashTransition_Enter_2)return 2;
    if(gTasks[0].func==Task_FlashTransition_Enter_3)return 3;
    exit(12);
}
void caveState(int count)
{
    int bg=(registers[REG_OFFSET_DISPCNT]&DISPCNT_BG0_ON)!=0;
    int blend=bg&&(registers[REG_OFFSET_BLDCNT]&0xc0)==BLDCNT_EFFECT_BLEND;
    int a=registers[REG_OFFSET_BLDALPHA]&31,b=(registers[REG_OFFSET_BLDALPHA]>>8)&31;
    printf("{\"state\":[%d,%d,%d,%d,%d,%d,%u,%d],\"palette\":",count,enterPhase(),bg,blend,a,b,palette[0],complete);
    colors(palette+224,16);printf(",\"palette0\":");colors(palette,16);
    printf(",\"writes\":[");
    for(int i=0;i<writeCount;i++)
    {if(i)printf(",");printf("{\"offset\":%d,\"colors\":",writes[i].offset);colors(writes[i].colors,writes[i].count);printf("}");}
    printf("],\"rgba\":[");
    for(int index=0;index<16;index++)
    {
        if(index)printf(",");
        for(int channel=0;channel<3;channel++)
        {
            int background=(palette[0]>>(channel*5))&31,value=background;
            if(bg&&index)
            {
                value=(palette[224+index]>>(channel*5))&31;
                if(blend){value=(value*(a>16?16:a)+background*(b>16?16:b))/16;if(value>31)value=31;}
            }
            printf("%d,",(value<<3)|(value>>2));
        }
        printf("255");
    }
    printf("]}");
}
void row(int preview)
{
    if(!preview){caveState(frame);return;}
    int phase=complete?7:enterFrame?6:gTasks[0].data[0];
    printf("{\"state\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",frame,phase,observedHold,
        MapPreview_GetDuration(0),nameWindow,bgBlend,objBlend,gPaletteFade.blendColor,gPaletteFade.active,enterFrame,complete);
    if(enterFrame>1){printf(",\"cave\":");caveState(enterFrame);}
    printf("}");
}
int main(void)
{
    const char* names[]={"enterSource","enterDistinctPalette","previewFirst","previewVisited",
        "previewHeld","previewEarlyHeld","previewDelayed","previewLaterHeld"};
    printf("{\"traces\":[");
    for(int kind=0;kind<8;kind++)
    {
        memset(gTasks,0,sizeof(gTasks));memset(registers,0,sizeof(registers));memset(palette,0,sizeof(palette));
        memset(&gPaletteFade,0,sizeof(gPaletteFade));sPlttBufferTransferPending=0;
        writeCount=complete=frame=nameWindow=observedHold=enterFrame=0;
        gMain.savedCallback=ReturnedToMap;gMain.vblankCallback=VBlank;
        memcpy(sCaveTransitionPalette,originalPalette,sizeof(originalPalette));
        memcpy(sCaveTransitionPalette_White,originalWhite,sizeof(originalWhite));
        memcpy(sCaveTransitionPalette_Black,originalBlack,sizeof(originalBlack));
        if(kind==1)
        {
            for(int i=0;i<16;i++)
            {
                sCaveTransitionPalette[i]=(u16)((i*2)|((31-i)<<5)|((i+7)<<10));
                sCaveTransitionPalette_White[i]=(u16)((31-i)|(i<<5)|((31-i/2)<<10));
                sCaveTransitionPalette_Black[i]=(u16)(i|((15-i)<<5)|((i+3)<<10));
            }
        }
        palette[0]=kind==1?3|(9<<5)|(17<<10):RGB_WHITE;
        visited=kind==3;bgBlend=objBlend=16;graphicsReadyFrom=kind==6?7:2;windowCopyReadyFrom=kind==6?11:3;
        bHeldFrom=kind==4||kind==5?0:kind==7?60:10000;bHeldThrough=kind==5?10:10000;
        if(kind)printf(",");printf("{\"name\":\"%s\",\"initialBackdrop\":%u,\"transitionPalette\":",names[kind],palette[0]);
        colors(sCaveTransitionPalette,16);printf(",\"whitePalette\":");colors(sCaveTransitionPalette_White,16);
        printf(",\"blackPalette\":");colors(sCaveTransitionPalette_Black,16);
        printf(",\"visited\":%s,\"graphicsReadyFrom\":%d,\"windowCopyReadyFrom\":%d,\"bHeldFrom\":%d,\"bHeldThrough\":%d",
            visited?"true":"false",graphicsReadyFrom,windowCopyReadyFrom,bHeldFrom,bHeldThrough);
        if(kind<2)FlashTransition_Enter();else RunMapPreviewScreen(0);
        printf(",\"frames\":[");row(kind>=2);
        for(frame=1;frame<=300;frame++)
        {
            graphicsReady=frame>=graphicsReadyFrom;windowCopyReady=frame>=windowCopyReadyFrom;
            heldB=frame>=bHeldFrom&&frame<=bHeldThrough;writeCount=0;
            if(gMain.vblankCallback)gMain.vblankCallback();
            if(kind>=2&&gTasks[0].func==Task_MapPreviewScreen_0&&gTasks[0].data[0]==4)observedHold++;
            if(enterFrame)enterFrame++;
            CB2_ChangeMapMain();
            if(kind>=2&&!enterFrame&&gTasks[0].func==Task_FlashTransition_Enter_1)enterFrame=1;
            printf(",");row(kind>=2);if(complete)break;
        }
        if(!complete)return 2;printf("]}");
    }
    printf("]}\n");return 0;
}
'''


def make_code(reference):
    sources = {}
    for path, digest in HASHES.items():
        raw = (reference/path).read_bytes()
        if hashlib.sha256(raw).hexdigest() != digest:
            raise ValueError('Cave entry reference revision mismatch: '+path)
        sources[path] = raw.decode()
    graphics = reference/'graphics/cave_transition'
    palette, white = read_palettes(graphics)
    raw = (graphics/'black.pal').read_bytes()
    if hashlib.sha256(raw).hexdigest() != BLACK_HASH:
        raise ValueError('Cave black palette revision mismatch')
    black = []
    for line in raw.decode().splitlines()[3:]:
        r,g,b = map(int,line.split());black.append((r>>3)|((g>>3)<<5)|((b>>3)<<10))
    if len(black)!=16:
        raise ValueError('Cave black palette must have sixteen colors')
    code = CAVE_HEADER.replace('void(*savedCallback)(void);','void(*savedCallback)(void); void(*vblankCallback)(void);')+HEADER
    for name, values in [('originalPalette',palette),('originalWhite',white),('originalBlack',black)]:
        code+='static const u16 '+name+'[16]={'+','.join(map(str,values))+'};\n'
    functions=[]
    for name in ['IsSoftwarePaletteFadeFinishing','UpdateNormalPaletteFade','UpdatePaletteFade','BeginNormalPaletteFade']:
        functions.append(extract(sources['src/palette.c'],name))
    functions.append(extract(sources['src/map_preview_screen.c'],'MapPreview_GetDuration'))
    for name in ['CB2_ChangeMapMain','FlashTransition_Enter']+['Task_FlashTransition_Enter_'+str(i) for i in range(4)]+['RunMapPreviewScreen','Task_MapPreviewScreen_0']:
        functions.append(extract(sources['src/fldeff_flash.c'],name))
    code+=''.join(body.split('\n{',1)[0]+';\n' for body in functions)+''.join(functions)+MAIN
    return code


def generate(reference):
    code=make_code(reference)
    with tempfile.TemporaryDirectory() as directory:
        path=Path(directory)/'field_cave_entry.c'
        executable=Path(directory)/('field_cave_entry.exe' if os.name=='nt' else 'field_cave_entry')
        path.write_text(code)
        compile_c(['cc','-std=c11','-O0',str(path),'-o',str(executable)])
        output=json.loads(subprocess.check_output([str(executable)],text=True))
    traces=output['traces']
    if len(traces)!=8 or any(t['frames'][-1]['state'][0]!=28 for t in traces[:2]):
        raise ValueError('Unexpected ENTER trace coverage or completion boundary')
    if traces[2]['frames'][-1]['state'][0]-traces[3]['frames'][-1]['state'][0]!=80:
        raise ValueError('Preview first/visited duration differs from the source 120/40 holds')
    return {'sourceCommit':COMMIT,**output}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--output',type=Path)
    parser.add_argument('--emit-c',type=Path,help='Stage extracted C without compiling or executing it')
    args=parser.parse_args()
    if args.emit_c:
        args.emit_c.parent.mkdir(parents=True,exist_ok=True);args.emit_c.write_text(make_code(args.reference))
    if args.output:
        output=generate(args.reference);args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(json.dumps(output,separators=(',',':'))+'\n')
        print(json.dumps({'traces':len(output['traces']),'rows':sum(len(t['frames']) for t in output['traces']),
            'completeFrames':[t['frames'][-1]['state'][0] for t in output['traces']]}))
    if not args.output and not args.emit_c:
        parser.error('Supply --output or --emit-c')


if __name__=='__main__':
    main()
