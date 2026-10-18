"""Extract ordinary warp callbacks into three small, local numerical traces.

The authored host represents NONE/SUNNY weather without a map preview. It calls
the warp task (priority 10), the weather task (priority 80), then palette update.
Departure row zero includes the initial freeze callback; arrival row zero is
after FieldCB_DefaultWarpExit setup. No upstream script is executed.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

from reference_c import COMMIT, compile_c, verify_reference


HASHES = {
    'src/palette.c': 'd406f56ba0a815d184753396e7e05120921c651a7bb2f5efff642816e64e7dc6',
    'src/field_fadetransition.c': '6fa3cb3b827470de6c4942b0296bf823529faec41c6473ffe88675a44114851e',
    'src/field_weather.c': '935ed0d0d83ff91bd8fdb52883c8223a816a66a40ec44b2b61c3e68fedc52672',
    'src/overworld.c': 'df352e738b56856765aec65adc6fdea0a4b25fe567bc760d4ce2d34afc6ff7e2',
}


def extract_function(source, name):
    match = re.search(r'^(?:static )?(?:bool8|u8|void) ' + re.escape(name)
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
#include <stdint.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int8_t s8; typedef int16_t s16; typedef int bool8;
#define TRUE 1
#define FALSE 0
#define PALETTE_FADE_STATUS_DONE 0
#define PALETTE_FADE_STATUS_ACTIVE 1
#define OBJ_PLTT_OFFSET 256
#define NORMAL_FADE 0
#define HARDWARE_FADE 1
#define PLTT_SIZE 0
#define PLTT NULL
#define CpuCopy32(a,b,c) ((void)0)
enum { WEATHER_NONE, WEATHER_RAIN, WEATHER_RAIN_THUNDERSTORM, WEATHER_DOWNPOUR,
    WEATHER_SNOW, WEATHER_SHADE, WEATHER_DROUGHT, WEATHER_FOG_HORIZONTAL,
    WEATHER_VOLCANIC_ASH, WEATHER_SANDSTORM, WEATHER_FOG_DIAGONAL, WEATHER_UNDERWATER };
enum { WEATHER_PAL_STATE_IDLE, WEATHER_PAL_STATE_SCREEN_FADING_IN };
struct Palette { int active,deltaY,delayCounter,delay,y,targetY,blendColor,mode,yDec,
    bufferTransferDisabled,objPaletteToggle,softwareFadeFinishing,softwareFadeFinishingCounter;
    u32 selectedPalettes; } gPaletteFade;
#define gPaletteFade_delay gPaletteFade.delay
#define gPaletteFade_selectedPalettes gPaletteFade.selectedPalettes
int sPlttBufferTransferPending,bgBlend,objBlend; u16 gPlttBufferFaded[512];
void BlendPalette(int offset,int count,int y,int color)
{ (void)count; (void)color; if(offset<256)bgBlend=y;else objBlend=y; }
void UpdateBlendRegisters(void){}
static u8 UpdateNormalPaletteFade(void); static bool8 IsSoftwarePaletteFadeFinishing(void);
u8 UpdatePaletteFade(void){return UpdateNormalPaletteFade();}
struct Task { s16 data[16]; } gTasks[1];
struct Weather { int fadeInCounter,fadeInActive,currWeather,gammaIndex,gammaTargetIndex,palProcessingState; } weatherState;
struct Weather *gWeatherPtr=&weatherState;
int musicReady,complete;
void FreezeObjectEvents(void){} void UnfreezeObjectEvents(void){}
void LockPlayerFieldControls(void){} void UnlockPlayerFieldControls(void){}
void WarpIntoMap(void){} void CB2_LoadMap(void){}
void SetMainCallback2(void(*fn)(void)){(void)fn;}
void DestroyTask(int taskId){(void)taskId;complete=1;}
int WaitWarpFadeOutScreen(void){return gPaletteFade.active;}
int BGMusicStopped(void){return musicReady;}
int FieldFadeTransitionBackgroundEffectIsFinished(void)
{return weatherState.palProcessingState!=WEATHER_PAL_STATE_SCREEN_FADING_IN;}
bool8 FadeInScreen_RainShowShade(void){return FALSE;}
bool8 FadeInScreen_Drought(void){return FALSE;}
bool8 FadeInScreen_FogHorizontal(void){return FALSE;}
'''

MAIN = r'''
void row(int frame)
{printf("[%d,%d,%d,%d,%d,%d]",frame,gTasks[0].data[0],bgBlend,objBlend,gPaletteFade.active,complete);}
int main(void)
{
    const char *names[]={"departureReady","departureDelayed","plainArrival"};
    printf("{\"traces\":[");
    for(int trace=0;trace<3;trace++)
    {
        memset(&gPaletteFade,0,sizeof(gPaletteFade));memset(gTasks,0,sizeof(gTasks));
        memset(&weatherState,0,sizeof(weatherState));complete=0;musicReady=trace==0;
        bgBlend=objBlend=trace==2?16:0;
        if(trace==2)weatherState.palProcessingState=WEATHER_PAL_STATE_SCREEN_FADING_IN;
        BeginNormalPaletteFade(0xffffffff,0,bgBlend,16-bgBlend,0);
        if(trace!=2)Task_Teleport2Warp(0);
        if(trace)printf(",");printf("{\"name\":\"%s\",\"frames\":[",names[trace]);row(0);
        for(int frame=1;frame<=100;frame++)
        {
            if(trace==2)
            {
                Task_ExitNonDoor(0);
                // First weather task initializes; later calls use the source palette callback.
                if(frame>1)FadeInScreenWithWeather();
            }
            else Task_Teleport2Warp(0);
            UpdatePaletteFade();
            if(trace==1)musicReady=frame>=64;
            printf(",");row(frame);
            if(complete)break;
        }
        if(!complete)return 2;
        printf("]}");
    }
    printf("]}\n");return 0;
}
'''


def generate(reference):
    verify_reference(reference)
    sources = {}
    for path, expected in HASHES.items():
        raw = (reference / path).read_bytes()
        if hashlib.sha256(raw).hexdigest() != expected:
            raise ValueError('Field warp reference revision mismatch: ' + path)
        sources[path] = raw.decode()
    order = sources['src/overworld.c']
    start = order.index('static void OverworldBasic')
    if order.index('RunTasks();', start) >= order.index('UpdatePaletteFade();', start):
        raise ValueError('Source task/palette callback order changed')
    code = HEADER
    for name in ('IsSoftwarePaletteFadeFinishing', 'UpdateNormalPaletteFade', 'BeginNormalPaletteFade'):
        code += extract_function(sources['src/palette.c'], name)
    code += extract_function(sources['src/field_weather.c'], 'FadeInScreenWithWeather')
    for name in ('Task_Teleport2Warp', 'Task_ExitNonDoor'):
        code += extract_function(sources['src/field_fadetransition.c'], name)
    code += MAIN
    with tempfile.TemporaryDirectory() as directory:
        source = Path(directory) / 'field_warp.c'
        executable = Path(directory) / ('field_warp.exe' if os.name == 'nt' else 'field_warp')
        source.write_text(code)
        compile_c(['cc', '-std=c11', '-O0', str(source), '-o', str(executable)])
        result = json.loads(subprocess.check_output([str(executable)], text=True))
    if [trace['frames'][-1][0] for trace in result['traces']] != [24, 66, 24]:
        raise ValueError('Unexpected source warp callback boundaries')
    return {'sourceCommit': COMMIT,
            'columns': ['frame', 'taskPhase', 'backgroundBlend', 'objectBlend', 'paletteActive', 'complete'],
            **result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    output = generate(args.reference)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, separators=(',', ':')) + '\n')
    print(json.dumps({'traces': len(output['traces']),
                      'rows': sum(len(trace['frames']) for trace in output['traces'])}))


if __name__ == '__main__':
    main()
