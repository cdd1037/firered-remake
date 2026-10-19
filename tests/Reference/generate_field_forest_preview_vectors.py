"""Run pinned forest-preview, plain-arrival, weather and palette callbacks.

The authored portable C host extracts the source task scheduler, forest creator
and callback, readiness predicate, plain arrival, normal palette, FadeScreen and
SHADE fade callbacks. Hardware copies/drawing, unrelated weather and music are
stubs; readiness inputs are explicit and per-frame. No upstream scripts run.
RGB555 composites use a documented hardware arithmetic oracle, not screenshots.
Outputs are local ignored fixtures consumed by ordinary managed tests.
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

HASHES = {
    'src/map_preview_screen.c': '6172d9268cb0f7bb77678b0d814e4fc800f11b5e8d49d009a4f7b33892109264',
    'src/field_fadetransition.c': '6fa3cb3b827470de6c4942b0296bf823529faec41c6473ffe88675a44114851e',
    'src/field_weather.c': '935ed0d0d83ff91bd8fdb52883c8223a816a66a40ec44b2b61c3e68fedc52672',
    'src/palette.c': 'd406f56ba0a815d184753396e7e05120921c651a7bb2f5efff642816e64e7dc6',
    'src/task.c': '8bdd5205ec396be7b66d6e384a82309b3cab59783d46fb8c4c991b2172d4868b',
    'src/overworld.c': 'df352e738b56856765aec65adc6fdea0a4b25fe567bc760d4ce2d34afc6ff7e2',
}


def extract(source, name):
    match = re.search(r'^(?:static )?(?:bool32|bool8|u8|s8|u16|int|void) ' + re.escape(name)
                      + r'\([^;]*?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing extracted function: ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end] + '\n'


HEADER = r'''
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int8_t s8; typedef int16_t s16; typedef int32_t s32;
typedef int bool8; typedef int bool32;
#define TRUE 1
#define FALSE 0
#define NUM_TASKS 16
#define HEAD_SENTINEL 0xFE
#define TAIL_SENTINEL 0xFF
#define REVISION 0
#define PALETTE_FADE_STATUS_DONE 0
#define PALETTE_FADE_STATUS_ACTIVE 1
#define PALETTE_FADE_STATUS_LOADING 255
#define OBJ_PLTT_OFFSET 256
#define NORMAL_FADE 0
#define FAST_FADE 1
#define HARDWARE_FADE 2
#define PLTT_SIZE 1024
#define PLTT NULL
#define CpuCopy32(a,b,c) ((void)0)
#define CpuFastCopy(a,b,c) ((void)0)
#define PALETTES_ALL 0xffffffffu
#define RGB_BLACK 0
#define RGB_WHITEALPHA 0xffff
#define COPYWIN_FULL 3
#define BG_ATTR_PRIORITY 0
#define BLDCNT_TGT1_BG0 1
#define BLDCNT_EFFECT_BLEND 0x40
#define BLDCNT_TGT2_BG1 0x200
#define BLDCNT_TGT2_BG2 0x400
#define BLDCNT_TGT2_BG3 0x800
#define BLDCNT_TGT2_OBJ 0x1000
#define BLDCNT_TGT2_BD 0x2000
#define WININ_WIN0_CLR 0x20
#define WININ_WIN1_CLR 0x2000
#define WINOUT_WIN01_CLR 0x20
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
enum { REG_OFFSET_DISPCNT,REG_OFFSET_BLDCNT,REG_OFFSET_BLDALPHA,REG_OFFSET_WININ,REG_OFFSET_WINOUT };
enum { WEATHER_NONE,WEATHER_SUNNY,WEATHER_SHADE,WEATHER_RAIN,WEATHER_RAIN_THUNDERSTORM,
    WEATHER_DOWNPOUR,WEATHER_SNOW,WEATHER_FOG_HORIZONTAL,WEATHER_DROUGHT,
    WEATHER_VOLCANIC_ASH,WEATHER_SANDSTORM,WEATHER_FOG_DIAGONAL,WEATHER_UNDERWATER };
enum { WEATHER_PAL_STATE_IDLE,WEATHER_PAL_STATE_SCREEN_FADING_IN,WEATHER_PAL_STATE_SCREEN_FADING_OUT };
enum { FADE_FROM_BLACK,FADE_FROM_WHITE,FADE_TO_BLACK,FADE_TO_WHITE };
typedef void (*TaskFunc)(u8);
struct Task { TaskFunc func; bool8 isActive; u8 prev,next,priority; s16 data[16]; } gTasks[NUM_TASKS];
struct Palette { int active,deltaY,delayCounter,delay,y,targetY,blendColor,mode,yDec,
    bufferTransferDisabled,objPaletteToggle,softwareFadeFinishing,softwareFadeFinishingCounter;
    u32 multipurpose1; } gPaletteFade;
#define gPaletteFade_delay gPaletteFade.delay
#define gPaletteFade_selectedPalettes gPaletteFade.multipurpose1
struct Weather { int currWeather,fadeDestColor,fadeScreenCounter,palProcessingState,
    fadeInActive,fadeInCounter,currBlendEVA,currBlendEVB,readyForInit,gammaIndex,gammaTargetIndex;
    u8 gammaShifts[19][32],altGammaShifts[19][32]; } weatherState;
struct Weather *gWeatherPtr=&weatherState;
enum { GAMMA_NORMAL,GAMMA_NONE,GAMMA_ALT }; const u8 *sPaletteGammaTypes;
u16 gPlttBufferFaded[512],gPlttBufferUnfaded[512],gpu[5];
int sPlttBufferTransferPending,bgBlend,objBlend,bgPriority,controlsLocked,weatherInitialized;
int duration,graphicsReady,windowReady,cleanupReady,externalWeatherReady,useExternalWeather,heldB;
int windowCopyRequested,fadeRequested,musicRequested,cleanupRequested,nameWindow,forestFrames;
u8 forestId,arrivalId;
static void InsertTask(u8); static u8 FindFirstActiveTask(void); void TaskDummy(u8);
static void Task_RunMapPreviewScreenForest(u8);
static u8 UpdateNormalPaletteFade(void); static bool8 IsSoftwarePaletteFadeFinishing(void);
u8 UpdatePaletteFade(void); bool8 IsWeatherNotFadingIn(void);
void FadeInFromBlack(void); void Weather_SetBlendCoeffs(u8,u8);
void FreezeObjectEvents(void){} void UnfreezeObjectEvents(void){}
void LockPlayerFieldControls(void){controlsLocked=1;}
void UnlockPlayerFieldControls(void){controlsLocked=0;}
int MapPreview_GetDuration(int mapsec){(void)mapsec;return duration;}
u16 MapPreview_CreateMapNameWindow(u8 mapsec){(void)mapsec;nameWindow=1;return 7;}
int MapPreview_IsGfxLoadFinished(void){return !graphicsReady;}
int IsDma3ManagerBusyWithBgCopy(void)
{return gTasks[forestId].data[0]==5?!cleanupReady:!windowReady;}
void CopyWindowToVram(int window,int mode)
{if(window!=7||mode!=COPYWIN_FULL)exit(11);windowCopyRequested=1;}
void FillBgTilemapBufferRect_Palette0(int bg,int tile,int x,int y,int w,int h)
{if(bg||tile||x||y||w!=32||h!=32)exit(12);}
void CopyBgTilemapBufferToVram(int bg){if(bg)exit(13);cleanupRequested=1;}
void MapPreview_Unload(s32 window){if(window!=7)exit(14);nameWindow=0;}
void Overworld_PlaySpecialMapMusic(void){musicRequested=1;}
int GetBgAttribute(int bg,int attr){if(bg||attr!=BG_ATTR_PRIORITY)exit(15);return bgPriority;}
void SetBgAttribute(int bg,int attr,int value){if(bg||attr!=BG_ATTR_PRIORITY)exit(16);bgPriority=value;}
u16 GetGpuReg(int reg){return gpu[reg];}
void SetGpuReg(int reg,u16 value){gpu[reg]=value;}
void SetGpuRegBits(int reg,u16 value){gpu[reg]|=value;}
void BlendPalette(int offset,int count,int y,int color)
{(void)count;if(color!=0)exit(17);if(offset<256)bgBlend=y;else objBlend=y;}
void CpuFastFill16(int color,void *destination,int bytes)
{if(color||destination!=gPlttBufferFaded||bytes!=1024)exit(18);bgBlend=objBlend=16;}
void UpdateBlendRegisters(void){}
static u8 UpdateFastPaletteFade(void){exit(19);}
static u8 UpdateHardwarePaletteFade(void){exit(20);}
static bool8 FadeInScreen_Drought(void){exit(21);}
static bool8 FadeInScreen_FogHorizontal(void){exit(22);}
static void ApplyGammaShift(int start,int count,int gamma)
{if(start||count!=32||gamma!=3)exit(23);bgBlend=objBlend=0;}
static void ApplyGammaShiftWithBlend(int start,int count,int gamma,int coefficient,int color)
{if(start||count!=32||gamma!=3||color)exit(24);bgBlend=objBlend=coefficient;}
'''

SHIMS = r'''
static void WeatherHost(u8 taskId)
{
    (void)taskId;
    if(!weatherInitialized)weatherInitialized=1;
    else if(weatherState.palProcessingState==WEATHER_PAL_STATE_SCREEN_FADING_IN)FadeInScreenWithWeather();
}
void FadeInFromBlack(void){fadeRequested=1;SourceFadeInFromBlack();}
'''

MAIN = r'''
struct Case {const char *name;int duration,shade,graphicsFrom,windowFrom,windowPulse,cleanupFrom,external,weatherFrom,bHeld,plain;};
const struct Case cases[]={
    {"normalFirst",120,0,1,1,0,1,0,1,0,0},
    {"normalVisited",40,0,1,1,0,1,0,1,0,0},
    {"shadeFirst",120,1,1,1,0,1,0,1,0,0},
    {"shadeVisited",40,1,1,1,0,1,0,1,0,0},
    {"normalDelayedCopy",40,0,6,10,6,1,0,1,0,0},
    {"normalFinishingRefill",40,0,1,20,1,1,0,1,0,0},
    {"normalRestart",40,0,30,1,0,1,0,1,0,0},
    {"shadeRestart",40,1,30,1,0,1,0,1,0,0},
    {"shadeCleanupWait",40,1,1,1,0,130,0,1,0,0},
    {"readyFirst",120,0,1,1,0,1,1,1,0,0},
    {"readyVisited",40,0,1,1,0,1,1,1,0,0},
    {"delayedReadiness",40,0,6,10,6,125,1,26,0,0},
    {"heldButton",40,0,1,1,0,1,1,1,1,0},
    {"plainNormal",0,0,1,1,0,1,0,1,0,1},
    {"plainShade",0,1,1,1,0,1,0,1,0,1}
};
void state(int frame,int plain)
{
    struct Task *task=&gTasks[forestId];
    printf("{\"frame\":%d,\"input\":[%d,%d,%d,%d,%d],\"forest\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],",
        frame,graphicsReady,windowReady,IsWeatherNotFadingIn(),cleanupReady,heldB,
        forestFrames,plain?0:task->data[0],plain?0:task->data[1],duration,
        plain?16:task->data[8],plain?0:task->data[9],windowCopyRequested,fadeRequested,
        musicRequested,cleanupRequested,plain?0:!task->isActive);
    printf("\"arrival\":[%d,%d,%d,%d,%d,%d],\"weatherReady\":%d,\"controlsLocked\":%d,\"restored\":%d}",
        frame,gTasks[arrivalId].data[0],bgBlend,objBlend,gPaletteFade.active,!gTasks[arrivalId].isActive,
        IsWeatherNotFadingIn(),controlsLocked,plain?0:(bgPriority==2&&gpu[0]==0x1100&&gpu[1]==0x2200&&gpu[2]==0x0010&&gpu[3]==0x3300&&gpu[4]==0x4400));
}
int main(void)
{
    printf("{\"traces\":[");
    for(unsigned trace=0;trace<sizeof(cases)/sizeof(*cases);trace++)
    {
        const struct Case *item=&cases[trace];ResetTasks();memset(&gPaletteFade,0,sizeof(gPaletteFade));
        memset(&weatherState,0,sizeof(weatherState));memset(gpu,0,sizeof(gpu));
        duration=item->duration;useExternalWeather=item->external;externalWeatherReady=0;heldB=item->bHeld;
        windowCopyRequested=fadeRequested=musicRequested=cleanupRequested=0;
        bgBlend=objBlend=16;weatherInitialized=controlsLocked=nameWindow=forestFrames=0;
        sPlttBufferTransferPending=0;bgPriority=2;
        gpu[0]=0x1100;gpu[1]=0x2200;gpu[2]=0x0010;gpu[3]=0x3300;gpu[4]=0x4400;
        weatherState.currWeather=item->shade?WEATHER_SHADE:WEATHER_NONE;weatherState.currBlendEVA=16;
        weatherState.gammaTargetIndex=item->shade?3:0;
        CreateTask(WeatherHost,80);
        forestId=1;
        if(!item->plain){MapPreview_StartForestTransition(0);if(gTasks[forestId].priority!=0)return 30;}
        arrivalId=CreateTask(Task_ExitNonDoor,10);if(gTasks[arrivalId].priority!=10)return 31;
        SourceFadeInFromBlack();LockPlayerFieldControls();
        if(trace)printf(",");
        printf("{\"name\":\"%s\",\"shade\":%d,\"duration\":%d,\"externalWeather\":%d,\"plain\":%d,\"frames\":[",
            item->name,item->shade,duration,item->external,item->plain);
        graphicsReady=windowReady=cleanupReady=0;state(0,item->plain);
        int frame;
        for(frame=1;frame<=512;frame++)
        {
            graphicsReady=frame>=item->graphicsFrom;
            windowReady=frame>=item->windowFrom||frame==item->windowPulse;
            cleanupReady=frame>=item->cleanupFrom;externalWeatherReady=frame>=item->weatherFrom;
            windowCopyRequested=fadeRequested=musicRequested=cleanupRequested=0;
            sPlttBufferTransferPending=0;
            if(!item->plain&&gTasks[forestId].isActive)forestFrames++;
            RunTasks();UpdatePaletteFade();printf(",");state(frame,item->plain);
            if(!gTasks[arrivalId].isActive)break;
        }
        if(frame>512)return 32;
        printf("]}");
    }
    BuildGammaShiftTables();printf("],\"shadeGamma\":{\"normal\":[");
    for(int i=0;i<32;i++){if(i)printf(",");printf("%d",weatherState.gammaShifts[2][i]);}
    printf("],\"alternate\":[");
    for(int i=0;i<32;i++){if(i)printf(",");printf("%d",weatherState.altGammaShifts[2][i]);}
    printf("],\"paletteTypes\":[");
    for(int i=0;i<32;i++){if(i)printf(",");printf("%d",sPaletteGammaTypes[i]);}
    printf("]}}\n");return 0;
}
'''


def generate(reference):
    sources = {}
    for relative, digest in HASHES.items():
        data = (reference / relative).read_bytes()
        if hashlib.sha256(data).hexdigest() != digest:
            raise ValueError('Forest preview reference mismatch: ' + relative)
        sources[relative] = data.decode()
    definitions = (reference / 'include/gba/defines.h').read_text()
    for definition in ('#define BG_PLTT_SIZE  0x200', '#define OBJ_PLTT_SIZE 0x200',
                       '#define PLTT_SIZE     (BG_PLTT_SIZE + OBJ_PLTT_SIZE)'):
        if definition not in definitions:
            raise ValueError('Source full-palette fill size changed')
    order = sources['src/overworld.c']
    begin = order.index('static void OverworldBasic')
    if order.index('RunTasks();', begin) >= order.index('UpdatePaletteFade();', begin):
        raise ValueError('Source task/palette callback order changed')
    gamma_types = re.search(r'static const u8 sBasePaletteGammaTypes\[32\] = \{.*?\n\};',
                            sources['src/field_weather.c'], re.S)
    if gamma_types is None:
        raise ValueError('Missing source palette gamma bank types')
    code = HEADER + gamma_types.group() + '\n'
    code += extract(sources['src/field_weather.c'], 'BuildGammaShiftTables')
    for name in ('ResetTasks', 'CreateTask', 'InsertTask', 'DestroyTask', 'RunTasks',
                 'FindFirstActiveTask', 'TaskDummy', 'FuncIsActiveTask'):
        code += extract(sources['src/task.c'], name)
    for name in ('UpdatePaletteFade', 'IsSoftwarePaletteFadeFinishing',
                 'UpdateNormalPaletteFade', 'BeginNormalPaletteFade'):
        code += extract(sources['src/palette.c'], name)
    code += extract(sources['src/field_weather.c'], 'FadeInScreen_RainShowShade')
    code += extract(sources['src/field_weather.c'], 'FadeInScreenWithWeather')
    # Keep the source predicate intact and apply explicit external weather only
    # in the isolated task-contract traces, never in the full arrival traces.
    code += '#define IsWeatherNotFadingIn SourceIsWeatherNotFadingIn\n'
    code += extract(sources['src/field_weather.c'], 'IsWeatherNotFadingIn')
    code += '#undef IsWeatherNotFadingIn\n'
    code += 'bool8 IsWeatherNotFadingIn(void){return useExternalWeather?externalWeatherReady:SourceIsWeatherNotFadingIn();}\n'
    code += extract(sources['src/field_weather.c'], 'FadeScreen')
    code += 'void Weather_SetBlendCoeffs(u8 eva,u8 evb){weatherState.currBlendEVA=eva;weatherState.currBlendEVB=evb;SetGpuReg(REG_OFFSET_BLDALPHA,BLDALPHA_BLEND(eva,evb));}\n'
    code += extract(sources['src/field_fadetransition.c'], 'palette_bg_faded_fill_black')
    code += '#define FadeInFromBlack SourceFadeInFromBlack\n'
    code += extract(sources['src/field_fadetransition.c'], 'FadeInFromBlack')
    code += '#undef FadeInFromBlack\n'
    for name in ('MapPreview_StartForestTransition', 'ForestMapPreviewScreenIsRunning', 'Task_RunMapPreviewScreenForest'):
        code += extract(sources['src/map_preview_screen.c'], name)
    for name in ('FieldFadeTransitionBackgroundEffectIsFinished', 'Task_ExitNonDoor'):
        code += extract(sources['src/field_fadetransition.c'], name)
    code += SHIMS + MAIN
    with tempfile.TemporaryDirectory() as directory:
        source = Path(directory) / 'forest_preview.c'
        executable = Path(directory) / ('forest_preview.exe' if os.name == 'nt' else 'forest_preview')
        source.write_text(code)
        compile_c(['cc', '-std=c11', '-O0', str(source), '-o', str(executable)])
        result = json.loads(subprocess.check_output([str(executable)], text=True))
    return {'sourceCommit': COMMIT, **result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = generate(args.reference)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(json.dumps({'traces': len(result['traces']),
                      'rows': sum(len(t['frames']) for t in result['traces']),
                      'endFrames': {t['name']: t['frames'][-1]['frame'] for t in result['traces']}}))


if __name__ == '__main__':
    main()
