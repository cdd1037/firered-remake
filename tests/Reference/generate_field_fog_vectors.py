"""Extract pinned original C fog/weather/arrival/palette callbacks into local vectors.

Only this authored host is executed; no upstream repository script is run.
Hardware draw, audio, fog sprite allocation and map preview are bounded stubs.
Palette coefficients are recorded at the original BlendPalette/ApplyFogBlend seam;
this file does not claim RGB555 pixel, sprite allocation or hardware timing evidence.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

EXTRA_HASHES = {
    'src/task.c': '8bdd5205ec396be7b66d6e384a82309b3cab59783d46fb8c4c991b2172d4868b',
    'src/field_camera.c': '256f0b9606918403f3230bd814d3234d1731609d683bdac0a7f45f5604e8383d',
    'src/field_weather_effects.c': '28ac11dd8d3cf66bb4e6d7e663cfc2411e8bfc82fec13bb2f2ec55d9c51ec67d',
    'include/field_weather.h': 'aa42a5396c46db9d86182a9615c12132092b03a457e532c1089a8d2cc0c0bef1',
}

HEADER = r'''
#include <stdlib.h>
typedef int32_t s32;
#undef PLTT_SIZE
#define PLTT_SIZE 1024
#define PALETTES_ALL 0xffffffffu
#define RGB_BLACK 0
#define RGB_WHITE 0x7fff
#define RGB_WHITEALPHA 0xffff
#define CpuFastCopy(a,b,c) (memcpy((b),(a),(c)),paletteCopies++)
#define FADE_FROM_BLACK 0
#define FADE_TO_BLACK 1
#define FADE_FROM_WHITE 2
#define FADE_TO_WHITE 3
#define REG_OFFSET_BLDALPHA 0
#define BLDALPHA_BLEND(eva,evb) ((eva)|((evb)<<8))
#define DISPLAY_WIDTH 240
#define tSpriteColumn data[0]
#define MB_UP_RIGHT_STAIR_WARP 0x6c
#define MB_UP_LEFT_STAIR_WARP 0x6d
#define MB_DOWN_RIGHT_STAIR_WARP 0x6e
#define MB_DOWN_LEFT_STAIR_WARP 0x6f
#define DIR_WEST 3
#define DIR_EAST 4
#define SE_EXIT 1
u16 gPlttBufferUnfaded[512];
int mainCalls,fadeCalls,paletteCopies,sourceBehavior,musicStopRequested;
s16 gSpriteCoordOffsetX,gSpriteCoordOffsetY;
u16 gTotalCameraPixelOffsetX,gTotalCameraPixelOffsetY;
s16 sHorizontalCameraPan,sVerticalCameraPan;
static void(*sFieldCameraPanningCallback)(void);
struct Sprite { s16 data[8],x,y,x2,y2; struct { int priority; } oam; } gSprites[1];
struct ObjectEvent { int heldMovementFinished,heldMovementActive,movementActionId; } gObjectEvents[1];
struct { int objectEventId,spriteId; } gPlayerAvatar;
void SetGpuReg(int reg,int value){(void)reg;(void)value;}
void CreateFogHorizontalSprites(void){weatherState.fogHSpritesCreated=TRUE;}
void DestroyFogHorizontalSprites(void){weatherState.fogHSpritesCreated=FALSE;}
void ApplyFogBlend(u8 coefficient,u16 color){(void)color;bgBlend=objBlend=coefficient;}
void CameraObjectReset1(void){} void CameraObjectReset2(void){}
void Overworld_PlaySpecialMapMusic(void){}
void FieldCB_DefaultWarpExit(void){} void (*gFieldCallback)(void);
int CreateTask(void(*fn)(u8),int priority){(void)fn;(void)priority;return 0;}
void TryFadeOutOldMapMusic(void){musicStopRequested=1;}
void PlayRainStoppingSoundEffect(void){} void PlaySE(int id){(void)id;}
void PlayerGetDestCoords(s16*x,s16*y){*x=0;*y=0;}
int MapGridGetMetatileBehaviorAt(int x,int y){(void)x;(void)y;return sourceBehavior;}
int GetPlayerFacingDirection(void){return (sourceBehavior&1)?DIR_WEST:DIR_EAST;}
int GetWalkInPlaceNormalMovementAction(int direction){return 10+direction;}
int GetWalkInPlaceFastMovementAction(int direction){return 20+direction;}
bool8 ObjectEventIsMovementOverridden(struct ObjectEvent*o){return o->heldMovementActive;}
int ObjectEventClearHeldMovementIfFinished(struct ObjectEvent*o)
{if(!o->heldMovementFinished)return FALSE;o->heldMovementActive=0;o->heldMovementFinished=0;return TRUE;}
void ObjectEventForceSetHeldMovement(struct ObjectEvent*o,int action)
{o->heldMovementActive=1;o->heldMovementFinished=0;o->movementActionId=action;}
int GetLastUsedWarpMapType(void){return 0;} int GetCurrentMapType(void){return 0;}
int MapTransitionIsExit(int previous,int current){(void)previous;(void)current;return FALSE;}
void CpuFastFill16(int color,void *destination,int bytes)
{if(color||destination!=gPlttBufferFaded||bytes!=1024)exit(10);bgBlend=objBlend=16;}
void FogHorizontal_Main(void);
void WarpFadeInScreen(void);
void WarpFadeOutScreen(void);
void DoNothing(void){}
'''

WEATHER_STRUCT = '''struct Weather {
    u8 fadeInCounter,fadeInActive,currWeather,nextWeather,palProcessingState;
    s8 gammaIndex,gammaTargetIndex;
    u8 gammaStepDelay,gammaStepFrameCounter,weatherChangeComplete;
    u16 fadeDestColor;
    u8 fadeScreenCounter,readyForInit;
    u16 initStep,finishStep;
    u8 weatherGfxLoaded,fogHSpritesCreated;
    u16 fogHScrollPosX,fogHScrollCounter,fogHScrollOffset;
    u16 currBlendEVA,currBlendEVB,targetBlendEVA,targetBlendEVB;
    u8 blendUpdateCounter,blendFrameCounter,blendDelay;
} weatherState;'''

MAIN = r'''
void FogHorizontal_Main(void){mainCalls++;SourceFogHorizontal_Main();}
void WarpFadeInScreen(void){fadeCalls++;SourceWarpFadeInScreen();}
void WarpFadeOutScreen(void){FadeScreen(FADE_TO_BLACK,0);}
void reset(void)
{
    memset(&weatherState,0,sizeof(weatherState));memset(gTasks,0,sizeof(gTasks));
    memset(&gPaletteFade,0,sizeof(gPaletteFade));memset(gSprites,0,sizeof(gSprites));
    memset(gObjectEvents,0,sizeof(gObjectEvents));
    mainCalls=fadeCalls=paletteCopies=musicStopRequested=complete=bgBlend=objBlend=0;
    gSpriteCoordOffsetX=gSpriteCoordOffsetY=0;musicReady=1;
    gTotalCameraPixelOffsetX=gTotalCameraPixelOffsetY=0;sHorizontalCameraPan=0;sVerticalCameraPan=32;sFieldCameraPanningCallback=NULL;
    weatherState.currWeather=weatherState.nextWeather=WEATHER_FOG_HORIZONTAL;
    weatherState.palProcessingState=WEATHER_PAL_STATE_IDLE;
    Weather_SetBlendCoeffs(16,0);
}
void fresh(void){reset();FogHorizontal_InitAll();}
void row(int frame)
{printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",
    frame,mainCalls,weatherState.initStep,weatherState.weatherGfxLoaded,
    weatherState.fogHScrollOffset,weatherState.fogHScrollCounter,weatherState.fogHScrollPosX,
    weatherState.currBlendEVA,weatherState.currBlendEVB,weatherState.blendFrameCounter,
    weatherState.blendUpdateCounter,weatherState.fadeScreenCounter,weatherState.palProcessingState,
    weatherState.fadeInActive,bgBlend,objBlend,gPaletteFade.active,gTasks[0].data[0],complete,
    gSprites[0].x2,gSprites[0].y2,fadeCalls,paletteCopies,weatherState.targetBlendEVA,weatherState.targetBlendEVB,gSpriteCoordOffsetX,gSpriteCoordOffsetY);}
int first=1;
void begin(const char*name,int behavior)
{if(!first)printf(",");first=0;printf("{\"name\":\"%s\",\"behavior\":%d,\"frames\":[",name,behavior);row(0);}
void next(int frame){printf(",");row(frame);}
void end(void){printf("]}");}
void run_transition(const char*name,int kind,int behavior,int stop)
{
    sourceBehavior=behavior;
    if(kind==0||kind==1){reset();bgBlend=objBlend=16;WarpFadeInScreen();gTasks[1].func=Task_WeatherInit;}
    else {fresh();gTasks[1].func=Task_WeatherMain;}
    if(kind==2||kind==3){FadeScreen(FADE_TO_BLACK,0);Task_Teleport2Warp(0);}
    if(kind==4)DoStairWarp(behavior,0);
    begin(name,behavior);
    for(int f=1;f<=100;f++)
    {
        musicReady=f>=stop;
        if(kind==0)Task_ExitNonDoor(0);
        else if(kind==1)Task_ExitStairs(0);
        else if(kind==4)Task_StairWarp(0);
        else Task_Teleport2Warp(0);
        gTasks[1].func(1);
        UpdatePaletteFade();next(f);
        if(complete)break;
    }
    if(!complete)exit(12);
    end();
}
int main(void)
{
    printf("{\"traces\":[");
    reset();FogHorizontal_InitVars();begin("freshInitMain",0);
    for(int f=1;!weatherState.weatherGfxLoaded&&f<=100;f++){FogHorizontal_Main();next(f);}end();
    fresh();begin("freshInitAll",0);end();
    fresh();begin("repeatInitAll",0);FogHorizontal_InitAll();next(1);end();
    fresh();begin("stableScroll",0);
    for(int f=1;f<=1040;f++){FogHorizontal_Main();if(f<=12||f>=924)next(f);}end();
    fresh();weatherState.fogHScrollOffset=65535;weatherState.fogHScrollCounter=3;
    begin("u16ScrollWrap",0);for(int f=1;f<=5;f++){FogHorizontal_Main();next(f);}end();
    fresh();gSpriteCoordOffsetX=-17;gSpriteCoordOffsetY=-9;begin("signedCameraScroll",0);
    for(int f=1;f<=8;f++){FogHorizontal_Main();next(f);}end();
    fresh();FadeScreen(FADE_FROM_BLACK,0);bgBlend=objBlend=16;begin("directFogFade",0);
    for(int f=1;f<=18;f++){Task_WeatherMain(1);next(f);}end();
    fresh();WarpFadeInScreen();begin("repeatFogFade",0);
    for(int f=1;f<=25;f++){if(f==8)WarpFadeInScreen();Task_WeatherMain(1);next(f);}end();
    run_transition("plainArrival",0,0,0);
    for(int b=0x6c;b<=0x6f;b++)run_transition("stairArrival",1,b,0);
    run_transition("plainDeparture",2,0,0);run_transition("plainDepartureDelayed",3,0,65);
    for(int b=0x6c;b<=0x6f;b++)run_transition("stairDeparture",4,b,0);
    printf("],\"spritePositions\":[");
    int poses[]={0,1,31,32,63,64,127,128,223,224,233,255};
    for(int i=0;i<12;i++)
    {if(i)printf(",");printf("[%d",poses[i]);weatherState.fogHScrollPosX=poses[i];
     gSpriteCoordOffsetY=-9;
     for(int col=0;col<5;col++){gSprites[0].data[0]=col;FogHorizontalSpriteCallback(&gSprites[0]);printf(",%d",gSprites[0].x);}
     printf(",%d]",gSprites[0].y2);}
    printf("],\"cameraLag\":[");
    reset();weatherState.readyForInit=TRUE;gTasks[1].func=Task_WeatherInit;
    int dx[]={0,1,2,-4,0},dy[]={0,2,-1,3,0};
    for(int f=1;f<=5;f++)
    {
        int oldX=gSpriteCoordOffsetX,oldY=gSpriteCoordOffsetY;
        gTasks[1].func(1);gSprites[0].data[0]=0;FogHorizontalSpriteCallback(&gSprites[0]);
        // Bounded movement-input stub for CameraUpdate's total-offset changes.
        // The real UpdateCameraPanning body executes after weather/sprite callbacks.
        gTotalCameraPixelOffsetX-=dx[f-1];gTotalCameraPixelOffsetY-=dy[f-1];UpdateCameraPanning();
        if(f>1)printf(",");
        printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",f,oldX,oldY,gSpriteCoordOffsetX,gSpriteCoordOffsetY,
            weatherState.fogHScrollOffset,weatherState.fogHScrollCounter,weatherState.fogHScrollPosX,
            gSprites[0].x,gSprites[0].y2,dx[f-1],dy[f-1]);
    }
    printf("]}\n");return 0;
}
'''


def generate(reference, helpers, retain_c=None):
    sys.path.insert(0,str(helpers))
    from reference_c import COMMIT, compile_c
    from generate_field_warp_vectors import HEADER as WARP_HEADER, HASHES as WARP_HASHES
    from generate_field_stair_vectors import HASHES as STAIR_HASHES, extract_function
    hashes={**WARP_HASHES,**{k:v for k,v in STAIR_HASHES.items() if k=='src/metatile_behavior.c'},**EXTRA_HASHES}
    sources={}
    for path,digest in hashes.items():
        raw=(reference/path).read_bytes()
        if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError('Fog reference revision mismatch: '+path)
        sources[path]=raw.decode()
    weather=sources['src/field_weather.c'];effects=sources['src/field_weather_effects.c']
    if 'CreateTask(Task_WeatherInit, 80)' not in weather:raise ValueError('Weather priority changed')
    if 'CreateTask(func, 10)' not in sources['src/field_fadetransition.c']:raise ValueError('Arrival priority changed')
    if 'gTasks[newTaskId].priority < gTasks[taskId].priority' not in extract_function(sources['src/task.c'],'InsertTask'):raise ValueError('Ascending task priority changed')
    order=extract_function(sources['src/overworld.c'],'OverworldBasic')
    if not order.index('RunTasks();')<order.index('AnimateSprites();')<order.index('CameraUpdate();')<order.index('UpdateCameraPanning();')<order.index('BuildOamBuffer();')<order.index('UpdatePaletteFade();'):raise ValueError('Frame order changed')
    header=WARP_HEADER.replace('enum { WEATHER_PAL_STATE_IDLE, WEATHER_PAL_STATE_SCREEN_FADING_IN };',
        'enum { WEATHER_PAL_STATE_CHANGING_WEATHER, WEATHER_PAL_STATE_SCREEN_FADING_IN, WEATHER_PAL_STATE_SCREEN_FADING_OUT, WEATHER_PAL_STATE_IDLE };')
    header=header.replace('struct Task { s16 data[16]; } gTasks[1];','struct Task { s16 data[16]; void(*func)(u8); } gTasks[2];')
    header=header.replace('struct Weather { int fadeInCounter,fadeInActive,currWeather,gammaIndex,gammaTargetIndex,palProcessingState; } weatherState;',WEATHER_STRUCT)
    header=header.replace('bool8 FadeInScreen_FogHorizontal(void){return FALSE;}','')
    code=header+HEADER
    groups={
        'src/field_camera.c':('UpdateCameraPanning',),
        'src/palette.c':('IsSoftwarePaletteFadeFinishing','UpdateNormalPaletteFade','BeginNormalPaletteFade'),
        'src/field_weather.c':('Weather_SetBlendCoeffs','Weather_SetTargetBlendCoeffs','Weather_UpdateBlend','FadeInScreen_FogHorizontal','FadeInScreenWithWeather','FadeScreen','Task_WeatherInit','Task_WeatherMain'),
        'src/field_weather_effects.c':('FogHorizontal_InitVars','FogHorizontal_InitAll','FogHorizontal_Main','FogHorizontal_Finish','FogHorizontalSpriteCallback'),
        'src/metatile_behavior.c':('MetatileBehavior_IsDirectionalUpRightStairWarp','MetatileBehavior_IsDirectionalUpLeftStairWarp','MetatileBehavior_IsDirectionalDownRightStairWarp','MetatileBehavior_IsDirectionalDownLeftStairWarp'),
        'src/field_fadetransition.c':('palette_bg_faded_fill_black','palette_bg_faded_fill_white','WarpFadeInScreen','Task_ExitNonDoor','Task_Teleport2Warp','DoStairWarp','Task_StairWarp','UpdateStairsMovement','ForceStairsMovement','GetStairsMovementDirection','Task_ExitStairs','ExitStairsMovement','WaitStairExitMovementFinished'),
    }
    bodies=[extract_function(sources[path],name).replace('void FogHorizontal_Main(void)','void SourceFogHorizontal_Main(void)').replace('void WarpFadeInScreen(void)','void SourceWarpFadeInScreen(void)') for path,names in groups.items() for name in names]
    code+=''.join(body.split('\n{',1)[0]+';\n' for body in bodies)
    code+='''struct WeatherFuncs { void(*initVars)(void);void(*main)(void);void(*initAll)(void);bool8(*finish)(void); };
static const struct WeatherFuncs sWeatherFuncs[WEATHER_UNDERWATER+1]={[WEATHER_FOG_HORIZONTAL]={FogHorizontal_InitVars,FogHorizontal_Main,FogHorizontal_InitAll,FogHorizontal_Finish}};
static void(*const sWeatherPalStateFuncs[])(void)={DoNothing,FadeInScreenWithWeather,DoNothing,DoNothing};
'''
    code+=''.join(bodies)+MAIN
    if retain_c:retain_c.write_text(code)
    with tempfile.TemporaryDirectory() as directory:
        cpath=Path(directory)/'field_fog.c';binary=Path(directory)/('field_fog.exe' if os.name=='nt' else 'field_fog')
        cpath.write_text(code);compile_c(['cc','-std=c11','-O0',str(cpath),'-o',str(binary)])
        result=json.loads(subprocess.check_output([str(binary)],text=True))
    columns=['frame','mainCalls','initStep','gfxLoaded','scrollOffset','scrollCounter','scrollPosX','eva','evb','blendFrameCounter','blendUpdateCounter','fadeCounter','palState','fadeInActive','backgroundBlend','objectBlend','paletteActive','taskPhase','complete','offsetX','offsetY','fadeCalls','paletteCopies','targetEva','targetEvb','cameraX','cameraY']
    return {'sourceCommit':COMMIT,'columns':columns,'spriteColumns':['scrollPosX','column0','column1','column2','column3','column4','y2'],'cameraLagColumns':['frame','inputCameraX','inputCameraY','outputCameraX','outputCameraY','scrollOffset','scrollCounter','scrollPosX','fogSpriteX','fogSpriteY2','movementX','movementY'],**result}


def validate(result, case_path):
    definitions=json.loads(case_path.read_text())
    if definitions['sourceCommit'] != result['sourceCommit']:raise ValueError('Case revision mismatch')
    by_key={(t['name'],t['behavior']):t for t in result['traces']}
    if len(by_key)!=len(result['traces']) or len(by_key)!=len(definitions['cases']):raise ValueError('Empty, missing or duplicate fog trace')
    for case in definitions['cases']:
        trace=by_key[(case['name'],case['behavior'])]
        if trace['frames'][-1][0]!=case['endFrame']:raise ValueError('Unexpected end: '+case['name'])
        rows={row[0]:dict(zip(result['columns'],row)) for row in trace['frames']}
        for check in case.get('checks',[]):
            actual=rows[check['frame']]
            for key,value in check.items():
                if actual[key]!=value:raise ValueError(f"{case['name']} frame {check['frame']} {key}: {actual[key]} != {value}")
    if not result['spritePositions'] or len(result['spritePositions'])!=12:raise ValueError('Missing sprite position cases')
    if result['cameraLag'][:2] != [[1,0,0,0,-40,23,1,233,265,0,0,0],[2,0,-40,-1,-42,23,2,233,265,216,1,2]]:raise ValueError('Camera-lag startup changed')
    # Strong contracts spanning every emitted frame. These are semantic checks,
    # independent of the extracted body rather than golden rewrites.
    fresh=by_key[('freshInitAll',0)]['frames'][0]
    for key,value in {'mainCalls':93,'scrollOffset':23,'scrollCounter':1,'scrollPosX':233,'eva':12,'evb':8,'gfxLoaded':1}.items():
        if fresh[result['columns'].index(key)]!=value:raise ValueError('Fresh InitAll signature: '+key)
    for trace in result['traces']:
        for raw in trace['frames']:
            row=dict(zip(result['columns'],raw))
            if trace['name']=='stableScroll':
                f=row['frame']
                expected={'mainCalls':93+f,'scrollOffset':(23+(f+1)//4)&65535,'scrollCounter':(f+1)%4,'scrollPosX':(-23-f//4)&255,'eva':12,'evb':8}
                if any(row[k]!=v for k,v in expected.items()):raise ValueError('Stable scroll phase mismatch')
            if trace['name'] in ('plainArrival','stairArrival'):
                f=row['frame'];coefficient=16 if f<=1 else max(0,17-f)
                if row['backgroundBlend']!=coefficient or row['objectBlend']!=coefficient:raise ValueError('Arrival coefficient mismatch')
                if bool(row['complete'])!=(f==19):raise ValueError('Arrival gate mismatch')
    return len(definitions['cases'])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--helpers',type=Path,default=Path(__file__).parent)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--retain-c',type=Path)
    parser.add_argument('--cases',type=Path,default=Path(__file__).with_name('field-fog-cases.json'))
    args=parser.parse_args();result=generate(args.reference,args.helpers,args.retain_c)
    validate(result,args.cases)
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(json.dumps({'traces':len(result['traces']),'rows':sum(len(t['frames']) for t in result['traces']),'ends':[(t['name'],t['behavior'],t['frames'][-1][0]) for t in result['traces']]}))

if __name__=='__main__':main()
