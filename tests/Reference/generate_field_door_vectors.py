"""Run pinned ordinary animated-door callbacks in an authored portable C host.

The host extracts the original task scheduler, door-frame timer, warp/exit tasks,
normal walking pixel callbacks, avatar standing-state update, palette callbacks,
and barn-door window callbacks. Only hardware drawing/audio and unrelated world
state are stubbed. Door pointers use a host-width side table instead of GBA's two
16-bit task slots. No upstream script runs; generated evidence stays ignored.

Row zero is before the first door task callback. Each row runs CB1's avatar-state
observation, source RunTasks (including newly inserted tasks in that same pass),
then held-movement sprites, then palette, matching overworld.c. Ordinary NONE /
SUNNY black-fade routes only; weather-specific and preview paths are excluded.
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
from generate_field_warp_vectors import HASHES as WARP_HASHES

HASHES = {**WARP_HASHES,
    'src/field_door.c': '04dddc75cebd2c4f864a25dd97ee7baea045b5d4ba471cf6ba8dffee97f553db',
    'src/field_screen_effect.c': '2b4e9e17b4528008ce36a38bab6108cb396f0aff2ef35cf6028bad49820c88e6',
    'src/event_object_movement.c': '920f96b45569e702dd00997e7984effb3a7677ba2b68512b939eda69c2294254',
    'src/event_object_lock.c': 'e18c4cdf700e7d20a71b2faaedae6dd2f7f33ebbff40562a333668587d95c516',
    'src/field_player_avatar.c': 'd36588c4d02385393a5e8dc74d78f907944ca556700b5c67b39f8304f4dde08e',
    'src/task.c': '8bdd5205ec396be7b66d6e384a82309b3cab59783d46fb8c4c991b2172d4868b',
}


def extract_function(source, name):
    match = re.search(r'^(?:static )?(?:bool32|bool8|u8|s8|u16|void) ' + re.escape(name)
                      + r'\([^;]*?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing extracted function: ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end] + '\n'


def extract_array(source, name):
    match = re.search(r'^static const [^\n]+\b' + re.escape(name)
                      + r'\[\][^=]*=\s*\{.*?^\};', source, re.M | re.S)
    if match is None:
        raise ValueError('Missing original array: ' + name)
    return match.group() + '\n'


HEADER = r'''
#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int8_t s8; typedef int16_t s16; typedef int32_t s32;
typedef int bool8; typedef int bool32;
#define TRUE 1
#define FALSE 0
#define NELEMS(a) (sizeof(a)/sizeof((a)[0]))
#define NUM_TASKS 16
#define HEAD_SENTINEL 0xFE
#define TAIL_SENTINEL 0xFF
#define REVISION 0
#define PALETTE_FADE_STATUS_DONE 0
#define PALETTE_FADE_STATUS_ACTIVE 1
#define OBJ_PLTT_OFFSET 256
#define NORMAL_FADE 0
#define HARDWARE_FADE 1
#define PLTT_SIZE 0
#define PLTT NULL
#define CpuCopy32(a,b,c) ((void)0)
#define TILE_SIZE_4BPP 32
#define CLOSED_DOOR_TILES_OFFSET 0xffff
#define LOCALID_PLAYER 0
#define DISPLAY_WIDTH 240
#define WIN_RANGE(a,b) (((a)<<8)|(b))
#define DISPCNT_WIN0_ON 0x2000
#define DISPCNT_WIN1_ON 0x4000
#define WINOUT_WIN01_BG_ALL 0x0f
#define WINOUT_WIN01_OBJ 0x10
#define WINOUT_WIN01_CLR 0x20
enum { REG_OFFSET_DISPCNT,REG_OFFSET_WININ,REG_OFFSET_WINOUT,REG_OFFSET_BLDCNT,
    REG_OFFSET_BLDALPHA,REG_OFFSET_WIN0H,REG_OFFSET_WIN0V,REG_OFFSET_WIN1H,REG_OFFSET_WIN1V };
enum { WEATHER_NONE, WEATHER_RAIN, WEATHER_RAIN_THUNDERSTORM, WEATHER_DOWNPOUR,
    WEATHER_SNOW, WEATHER_SHADE, WEATHER_DROUGHT, WEATHER_FOG_HORIZONTAL,
    WEATHER_VOLCANIC_ASH, WEATHER_SANDSTORM, WEATHER_FOG_DIAGONAL, WEATHER_UNDERWATER };
enum { WEATHER_PAL_STATE_IDLE, WEATHER_PAL_STATE_SCREEN_FADING_IN };
enum { DIR_NONE,DIR_SOUTH,DIR_NORTH,DIR_WEST,DIR_EAST,DIR_SOUTHWEST,DIR_SOUTHEAST,DIR_NORTHWEST,DIR_NORTHEAST };
enum { MOVE_SPEED_NORMAL,MOVE_SPEED_FAST_1,MOVE_SPEED_FAST_2,MOVE_SPEED_FASTER,MOVE_SPEED_FASTEST };
enum { T_NOT_MOVING,T_TILE_TRANSITION,T_TILE_CENTER };
enum { MOVEMENT_ACTION_WALK_NORMAL_DOWN,MOVEMENT_ACTION_WALK_NORMAL_UP };
typedef void (*TaskFunc)(u8);
struct Task { TaskFunc func; bool8 isActive; u8 prev,next,priority; s16 data[16]; } gTasks[NUM_TASKS];
struct Palette { int active,deltaY,delayCounter,delay,y,targetY,blendColor,mode,yDec,
    bufferTransferDisabled,objPaletteToggle,softwareFadeFinishing,softwareFadeFinishingCounter;
    u32 selectedPalettes; } gPaletteFade;
#define gPaletteFade_delay gPaletteFade.delay
#define gPaletteFade_selectedPalettes gPaletteFade.selectedPalettes
int sPlttBufferTransferPending,bgBlend,objBlend; u16 gPlttBufferFaded[512];
struct Weather { int fadeInCounter,fadeInActive,currWeather,gammaIndex,gammaTargetIndex,palProcessingState; } weatherState;
struct Weather *gWeatherPtr=&weatherState;
struct Coords16 { s16 x,y; };
struct Sprite { s16 x,y,data[8]; int animPaused; } gSprites[1];
struct ObjectEvent { int singleMovementActive,heldMovementActive,heldMovementFinished,movementActionId;
    int triggerGroundEffectsOnStop; } gObjectEvents[1];
struct { int tileTransitionState,objectEventId; } gPlayerAvatar;
struct DoorAnimFrame { u8 duration; u16 tileOffset; };
struct DoorGraphics { int unused; } doorGfx;
const struct DoorAnimFrame *doorFrames[NUM_TASKS];
u16 gpu[9]; int musicReady,playerVisible,doorFrame,weatherInitialized,musicStopRequested,openDoorSound;
void BlendPalette(int offset,int count,int y,int color)
{ (void)count; (void)color; if(offset<256)bgBlend=y;else objBlend=y; }
void UpdateBlendRegisters(void){}
void FreezeObjectEvents(void){} void UnfreezeObjectEvents(void){}
void LockPlayerFieldControls(void){} void UnlockPlayerFieldControls(void){}
void WarpIntoMap(void){} void CB2_LoadMap(void){}
void SetMainCallback2(void(*fn)(void)){(void)fn;}
int BGMusicStopped(void){return musicReady;}
bool8 ForestMapPreviewScreenIsRunning(void){return TRUE;}
bool8 FadeInScreen_RainShowShade(void){return FALSE;}
bool8 FadeInScreen_Drought(void){return FALSE;}
bool8 FadeInScreen_FogHorizontal(void){return FALSE;}
void PlayerGetDestCoords(s16*x,s16*y){*x=0;*y=0;}
void PlaySE(int sound){(void)sound;openDoorSound=1;} int GetDoorSoundEffect(int x,int y){return 0;}
void SetPlayerVisibility(int visible){playerVisible=visible;}
void FieldSetDoorOpened(int x,int y){doorFrame=2;}
void TryFadeOutOldMapMusic(void){musicStopRequested=1;}
void PlayRainStoppingSoundEffect(void){}
u16 GetGpuReg(int reg){return gpu[reg];}
void SetGpuReg(int reg,u16 value){gpu[reg]=value;}
void SetGpuRegBits(int reg,u16 value){gpu[reg]|=value;}
void DrawDoor(const struct DoorGraphics *gfx,const struct DoorAnimFrame *frame,int x,int y)
{doorFrame=frame->tileOffset==CLOSED_DOOR_TILES_OFFSET?-1:frame->tileOffset/128;}
int GetObjectEventIdByLocalIdAndMap(int id,int map,int group){return 0;}
void ShiftStillObjectEventCoords(struct ObjectEvent *obj){}
void ObjectEventClearHeldMovementIfActive(struct ObjectEvent *obj)
{obj->heldMovementActive=0;obj->heldMovementFinished=0;gSprites[0].data[2]=0;}
void ObjectEventClearHeldMovementIfFinished(struct ObjectEvent *obj)
{if(obj->heldMovementFinished)ObjectEventClearHeldMovementIfActive(obj);}
void ObjectEventSetHeldMovement(struct ObjectEvent *obj,int action)
{obj->movementActionId=action;obj->heldMovementActive=1;obj->heldMovementFinished=0;gSprites[0].data[2]=0;}
/* These predicates are false for the two extracted normal-walk actions. */
bool8 PlayerAnimIsMultiFrameStationary(void){return FALSE;}
bool8 PlayerAnimIsMultiFrameStationaryAndStateNotTurning(void){return FALSE;}
'''

SHIMS = r'''
u8 UpdatePaletteFade(void){return UpdateNormalPaletteFade();}
void WarpFadeOutScreen(void){BeginNormalPaletteFade(0xffffffff,0,0,16,0);}
void WarpFadeInScreenWithDelay(void)
{weatherState.palProcessingState=WEATHER_PAL_STATE_SCREEN_FADING_IN;BeginNormalPaletteFade(0xffffffff,3,16,0,0);}
static void Task_AnimateDoor(u8 taskId)
{if(!AnimateDoorFrame(&doorGfx,doorFrames[taskId],gTasks[taskId].data))DestroyTask(taskId);}
s8 FieldAnimateDoorOpen(int x,int y)
{u8 id=CreateTask(Task_AnimateDoor,80);doorFrames[id]=sDoorAnimFrames_OpenSmall;return id;}
s8 FieldAnimateDoorClose(int x,int y)
{u8 id=CreateTask(Task_AnimateDoor,80);doorFrames[id]=sDoorAnimFrames_CloseSmall;return id;}
bool8 FieldIsDoorAnimationRunning(void){return FuncIsActiveTask(Task_AnimateDoor);}
void InitMovementNormal(struct ObjectEvent *obj,struct Sprite *sprite,u8 direction,u8 speed)
{SetSpriteDataForNormalStep(sprite,direction,speed);sprite->animPaused=0;sprite->data[2]=1;}
static void WeatherHost(u8 taskId)
{if(weatherInitialized)FadeInScreenWithWeather();else weatherInitialized=1;}
static void AnimateSpritesHost(void)
{
    struct ObjectEvent *obj=&gObjectEvents[0];struct Sprite *sprite=&gSprites[0];
    if(!obj->heldMovementActive||obj->heldMovementFinished)return;
    bool8 finished;
    if(obj->movementActionId==MOVEMENT_ACTION_WALK_NORMAL_UP)
        finished=sprite->data[2]==0?MovementAction_WalkNormalUp_Step0(obj,sprite):MovementAction_WalkNormalUp_Step1(obj,sprite);
    else finished=sprite->data[2]==0?MovementAction_WalkNormalDown_Step0(obj,sprite):MovementAction_WalkNormalDown_Step1(obj,sprite);
    if(finished)obj->heldMovementFinished=1;
}
'''

MAIN = r'''
void row(int frame,int mainTask)
{
    int wipe=FuncIsActiveTask(Task_BarnDoorWipe);
    printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",
        frame,gTasks[mainTask].data[0],bgBlend,objBlend,gPaletteFade.active,!gTasks[mainTask].isActive,
        doorFrame,FieldIsDoorAnimationRunning(),playerVisible,
        gSprites[0].y<0?-gSprites[0].y:gSprites[0].y,wipe,
        wipe?gpu[REG_OFFSET_WIN0H]&255:0,wipe?gpu[REG_OFFSET_WIN1H]>>8:240,
        gpu[REG_OFFSET_WIN0H],gpu[REG_OFFSET_WIN1H],musicStopRequested,
        gTasks[mainTask].func==Task_Teleport2Warp,openDoorSound);
}
int main(void)
{
    const char *names[]={"doorDepartureReady","doorDepartureDelayed","animatedArrival"};
    printf("{\"traces\":[");
    for(int trace=0;trace<3;trace++)
    {
        ResetTasks();memset(&gPaletteFade,0,sizeof(gPaletteFade));memset(gpu,0,sizeof(gpu));
        memset(&weatherState,0,sizeof(weatherState));memset(gSprites,0,sizeof(gSprites));
        memset(gObjectEvents,0,sizeof(gObjectEvents));memset(&gPlayerAvatar,0,sizeof(gPlayerAvatar));
        weatherInitialized=0;musicReady=trace!=1;playerVisible=1;doorFrame=-1;musicStopRequested=0;openDoorSound=0;
        bgBlend=objBlend=trace==2?16:0;
        if(trace==2)CreateTask(WeatherHost,80);
        int taskId=CreateTask(trace==2?Task_ExitDoor:Task_DoorWarp,10);
        if(trace)printf(",");printf("{\"name\":\"%s\",\"frames\":[",names[trace]);row(0,taskId);
        for(int frame=1;frame<=240;frame++)
        {
            if(trace==1)musicReady=frame>=100;
            openDoorSound=0;UpdatePlayerAvatarTransitionState();
            RunTasks();AnimateSpritesHost();UpdatePaletteFade();
            printf(",");row(frame,taskId);
            if(!gTasks[taskId].isActive)break;
        }
        if(gTasks[taskId].isActive)return 2;
        printf("]}");
    }
    printf("]}\n");return 0;
}
'''


def generate(reference):
    sources = {}
    for path, expected in HASHES.items():
        raw = (reference / path).read_bytes()
        if hashlib.sha256(raw).hexdigest() != expected:
            raise ValueError('Door reference revision mismatch: ' + path)
        sources[path] = raw.decode()
    order = sources['src/overworld.c']
    basic = extract_function(order, 'OverworldBasic')
    if not basic.index('RunTasks();') < basic.index('AnimateSprites();') < basic.index('UpdatePaletteFade();'):
        raise ValueError('Source task/sprite/palette callback order changed')
    cb1 = extract_function(order, 'DoCB1_Overworld')
    if 'UpdatePlayerAvatarTransitionState();' not in cb1:
        raise ValueError('Source pre-task avatar observation changed')
    # These host-width wrappers preserve the original public-entry and task
    # scheduling seams; the graphics lookup is admitted only for valid doors.
    door_start = extract_function(sources['src/field_door.c'], 'StartDoorAnimationTask')
    if 'CreateTask(Task_AnimateDoor, 80)' not in door_start:
        raise ValueError('Source door-animation task priority changed')
    door_entry = extract_function(sources['src/field_fadetransition.c'], 'DoDoorWarp')
    if 'CreateTask(Task_DoorWarp, 10);' not in door_entry:
        raise ValueError('Source door-warp entry changed')
    selections = {
        'src/task.c': ('ResetTasks','CreateTask','InsertTask','DestroyTask','RunTasks','FindFirstActiveTask','TaskDummy','FuncIsActiveTask','FindTaskIdByFunc'),
        'src/palette.c': ('IsSoftwarePaletteFadeFinishing','UpdateNormalPaletteFade','BeginNormalPaletteFade'),
        'src/field_weather.c': ('FadeInScreenWithWeather','IsWeatherNotFadingIn'),
        'src/field_door.c': ('AnimateDoorFrame',),
        'src/field_screen_effect.c': ('DoOutwardBarnDoorWipe','BarnDoorWipeSaveGpuRegs','BarnDoorWipeLoadGpuRegs','Task_BarnDoorWipe','Task_BarnDoorWipeChild'),
        'src/event_object_movement.c': ('ObjectEventIsMovementOverridden','ObjectEventCheckHeldMovementStatus','Step1','Step2','Step3','Step4','Step8','SetSpriteDataForNormalStep','NpcTakeStep','UpdateMovementNormal','MovementAction_WalkNormalDown_Step0','MovementAction_WalkNormalDown_Step1','MovementAction_WalkNormalUp_Step0','MovementAction_WalkNormalUp_Step1'),
        'src/field_player_avatar.c': ('PlayerIsAnimActive','PlayerCheckIfAnimFinishedOrInactive','UpdatePlayerAvatarTransitionState'),
        'src/event_object_lock.c': ('walkrun_is_standing_still',),
        'src/field_fadetransition.c': ('WaitWarpFadeOutScreen','FieldFadeTransitionBackgroundEffectIsFinished','Task_DoorWarp','Task_ExitDoor','Task_Teleport2Warp'),
    }
    bodies = {path: [extract_function(sources[path],name) for name in names] for path,names in selections.items()}
    declarations = '\n'.join(body.partition('\n{')[0]+';' for group in bodies.values() for body in group)
    code = HEADER + declarations + '''
static void Task_AnimateDoor(u8 taskId);
s8 FieldAnimateDoorOpen(int,int); s8 FieldAnimateDoorClose(int,int); bool8 FieldIsDoorAnimationRunning(void);
void WarpFadeOutScreen(void);void WarpFadeInScreenWithDelay(void);u8 UpdatePaletteFade(void);
void InitMovementNormal(struct ObjectEvent*,struct Sprite*,u8,u8);
'''
    for path in ('src/task.c','src/palette.c','src/field_weather.c'):
        code += ''.join(bodies[path])
    for name in ('sDoorAnimFrames_OpenSmall','sDoorAnimFrames_CloseSmall'):
        code += extract_array(sources['src/field_door.c'],name)
    code += '#define tFrameId data[4]\n#define tCounter data[5]\n#define tX data[6]\n#define tY data[7]\n'
    code += ''.join(bodies['src/field_door.c'])
    code += '#undef tFrameId\n#undef tCounter\n#undef tX\n#undef tY\n'
    code += '#define tState data[9]\n#define tDirection data[10]\n#define tChildOffset data[0]\n#define DIR_WIPE_IN 0\n#define DIR_WIPE_OUT 1\n'
    code += ''.join(bodies['src/field_screen_effect.c'])
    code += '#undef tState\n#undef tDirection\n#undef tChildOffset\n'
    code += '#define tDirection data[3]\n#define tSpeed data[4]\n#define tStepNo data[5]\ntypedef void (*SpriteStepFunc)(struct Sprite*,u8);\n'
    for name in ('sDirectionToVectors','sSpeedNormalStepFuncs','sSpeedFast1StepFuncs','sSpeedFast2StepFuncs','sSpeedFasterStepFuncs','sSpeedFastestStepFuncs','sNpcStepFuncTables','sStepTimes'):
        code += extract_array(sources['src/event_object_movement.c'],name)
    code += ''.join(bodies['src/event_object_movement.c'])
    code += '#undef tDirection\n#undef tSpeed\n#undef tStepNo\n'
    for path in ('src/field_player_avatar.c','src/event_object_lock.c','src/field_fadetransition.c'):
        code += ''.join(bodies[path])
    code += SHIMS + MAIN
    with tempfile.TemporaryDirectory() as directory:
        source = Path(directory) / 'field_door.c'
        executable = Path(directory) / ('field_door.exe' if os.name == 'nt' else 'field_door')
        source.write_text(code)
        compile_c(['cc','-std=c11','-O0',str(source),'-o',str(executable)])
        result = json.loads(subprocess.check_output([str(executable)],text=True))
    if [trace['frames'][-1][0] for trace in result['traces']] != [81,101,81]:
        raise ValueError('Unexpected source door boundaries: '+str([trace['frames'][-1][0] for trace in result['traces']]))
    return {'sourceCommit':COMMIT,'columns':['frame','taskPhase','backgroundBlend','objectBlend','paletteActive','complete','doorFrame','doorAnimationActive','playerVisible','walkPixels','wipeActive','wipeLeft','wipeRight','win0h','win1h','musicStopRequested','teleportTask','openDoorSoundRequested'],**result}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    output=generate(args.reference)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(json.dumps({'traces':len(output['traces']),'rows':sum(len(trace['frames']) for trace in output['traces'])}))


if __name__=='__main__':
    main()
