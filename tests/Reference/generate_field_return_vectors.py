"""Extract pinned menu/script field-return weather task ordering into tiny vectors.

This authored host reuses the maintained fog host for NONE, SHADE and fog. Original task.c functions
run priority-80 weather before the later priority-80 menu wait, or after the
priority-10 script wait. Original start-menu task initialization is retained.
Hardware rendering, music, union-room presence, forest preview and actual key
input are bounded stubs. Frame zero is after FadeInFromBlack and before first OverworldBasic;
black map/menu setup callbacks are outside this trace and do not tick weather.
The original NONE/SHADE initialization and fade bodies select each return's
palette path. Palette writes are observed at the BlendPalette/ApplyGammaShift/
ApplyFogBlend seam; these are coefficient/timing vectors, not RGB555 pixels.
mainCalls counts FogHorizontal_Main calls only, including synchronous InitAll.
No upstream repository script is run and no generated artwork is needed.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import subprocess
import tempfile

START_MENU_HASH = '68dd2fc725c7e2685f539fe40b00d5350eecfbfd33bbbda7d6d6ff9b6e400f56'
COLUMNS = ['frame', 'fadeCounter', 'palState', 'backgroundBlend', 'mainCalls',
           'menuTask', 'menuState', 'firstInput', 'scriptEnabled', 'scrollOffset',
           'scrollCounter', 'scrollX', 'objectBlend', 'paletteActive', 'gammaIndex']

STUBS = r'''
int frame,firstInput,scriptEnabled;
// Observe whole-palette shade writes; source FadeInScreen_RainShowShade still
// owns the increment, terminal ApplyGammaShift and readiness transition.
static void ApplyGammaShift(u8 start,u8 count,s8 gamma)
{if(start||count!=32||gamma!=3)exit(11);bgBlend=objBlend=0;}
static void ApplyGammaShiftWithBlend(u8 start,u8 count,s8 gamma,u8 coefficient,u16 color)
{if(start||count!=32||gamma!=3||color)exit(12);bgBlend=objBlend=coefficient;}
// The source predicate returns TRUE when no forest-preview task is active.
bool8 ForestMapPreviewScreenIsRunning(void){return TRUE;}
bool8 (*sStartMenuCallback)(void);
int InUnionRoom(void){return FALSE;}
void SetUsingUnionRoomStartMenu(void){}
// Observe the first invocation of the real task's input handler, without
// manufacturing key events or asking the source menu to change its owner.
bool8 StartCB_HandleInput(void){if(!firstInput)firstInput=frame;return FALSE;}
void ScriptContext_Enable(void){scriptEnabled=frame;}
'''

MAIN = r'''
void FogHorizontal_Main(void){mainCalls++;SourceFogHorizontal_Main();}
void reset(int weather)
{
    memset(&weatherState,0,sizeof(weatherState));
    memset(&gPaletteFade,0,sizeof(gPaletteFade));
    ResetTasks();firstInput=scriptEnabled=mainCalls=bgBlend=objBlend=0;
    sPlttBufferTransferPending=0;sStartMenuCallback=NULL;
    gSpriteCoordOffsetX=gSpriteCoordOffsetY=0;
    weatherState.currWeather=weatherState.nextWeather=weather;
    weatherState.palProcessingState=WEATHER_PAL_STATE_IDLE;
    Weather_SetBlendCoeffs(16,0);
}
int main(void)
{
    const int weathers[]={WEATHER_FOG_HORIZONTAL,WEATHER_NONE,WEATHER_SHADE};
    const int lastFrames[]={22,27,21};
    const char* names[][2]={{"menuReturn","scriptReturn"},
        {"normalMenuReturn","normalScriptReturn"},{"shadeMenuReturn","shadeScriptReturn"}};
    printf("{\"traces\":[");
    for(int weather=0;weather<3;weather++)
    for(int kind=0;kind<2;kind++)
    {
        reset(weathers[weather]);
        CreateTask(Task_WeatherInit,80);
        if(kind==0)FadeTransition_FadeInOnReturnToStartMenu();else FieldCB_ContinueScript();
        if(weather||kind)printf(",");
        printf("{\"name\":\"%s\",\"frames\":[",names[weather][kind]);
        for(frame=0;frame<=lastFrames[weather];frame++)
        {
            if(frame){RunTasks();UpdatePaletteFade();printf(",");}
            int menuId=-1;
            for(int i=0;i<NUM_TASKS;i++)
                if(gTasks[i].isActive&&gTasks[i].func==Task_StartMenuHandleInput)menuId=i;
            printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",frame,
                weatherState.fadeScreenCounter,weatherState.palProcessingState,
                bgBlend,mainCalls,menuId,menuId<0?-1:gTasks[menuId].data[0],
                firstInput,scriptEnabled,weatherState.fogHScrollOffset,
                weatherState.fogHScrollCounter,weatherState.fogHScrollPosX,
                objBlend,gPaletteFade.active,weatherState.gammaIndex);
        }
        printf("]}");
    }
    printf("]}\n");return 0;
}
'''


def generate(reference, helpers, retain_c=None):
    sys.path.insert(0, str(helpers))
    import generate_field_fog_vectors as fog
    import generate_field_warp_vectors as warp
    from generate_field_stair_vectors import extract_function
    from reference_c import COMMIT, compile_c

    # Use the same pinned host/source definitions as the fog oracle, selecting
    # original callbacks for the two additional static weather table entries.
    hashes = {**warp.HASHES, **fog.EXTRA_HASHES,
              'src/start_menu.c': START_MENU_HASH}
    sources = {}
    for path, digest in hashes.items():
        raw = (reference/path).read_bytes()
        if hashlib.sha256(raw).hexdigest() != digest:
            raise ValueError('Field-return reference revision mismatch: '+path)
        sources[path] = raw.decode()
    task = sources['src/task.c']
    if 'CreateTask(Task_WeatherInit, 80)' not in sources['src/field_weather.c']:
        raise ValueError('Weather priority changed')
    if 'gTasks[newTaskId].priority < gTasks[taskId].priority' not in extract_function(task, 'InsertTask'):
        raise ValueError('Ascending task priority changed')
    order = extract_function(sources['src/overworld.c'], 'OverworldBasic')
    callbacks = ['RunTasks();', 'AnimateSprites();', 'CameraUpdate();',
                 'UpdateCameraPanning();', 'BuildOamBuffer();', 'UpdatePaletteFade();']
    if [order.index(call) for call in callbacks] != sorted(order.index(call) for call in callbacks):
        raise ValueError('Field-return frame order changed')

    header = warp.HEADER.replace('typedef int bool8;', 'typedef uint8_t bool8; typedef int bool32;')
    header = header.replace(
        'enum { WEATHER_PAL_STATE_IDLE, WEATHER_PAL_STATE_SCREEN_FADING_IN };',
        'enum { WEATHER_PAL_STATE_CHANGING_WEATHER, WEATHER_PAL_STATE_SCREEN_FADING_IN, WEATHER_PAL_STATE_SCREEN_FADING_OUT, WEATHER_PAL_STATE_IDLE };')
    header = header.replace('struct Task { s16 data[16]; } gTasks[1];', r'''
#define NUM_TASKS 16
#define NUM_TASK_DATA 16
#define HEAD_SENTINEL 0xFE
#define TAIL_SENTINEL 0xFF
#define REVISION 0
struct Task { s16 data[16]; void(*func)(u8); u8 isActive,prev,next,priority; } gTasks[NUM_TASKS];
typedef void(*TaskFunc)(u8);
''')
    header = header.replace(
        'struct Weather { int fadeInCounter,fadeInActive,currWeather,gammaIndex,gammaTargetIndex,palProcessingState; } weatherState;',
        fog.WEATHER_STRUCT)
    header = header.replace('void DestroyTask(int taskId){(void)taskId;complete=1;}',
                            'void DestroyTask(u8 taskId);')
    header = header.replace('bool8 FadeInScreen_RainShowShade(void){return FALSE;}', '')
    header = header.replace('bool8 FadeInScreen_FogHorizontal(void){return FALSE;}', '')
    header = header.replace('int FieldFadeTransitionBackgroundEffectIsFinished(void)\n'
                            '{return weatherState.palProcessingState!=WEATHER_PAL_STATE_SCREEN_FADING_IN;}', '')
    code = header + fog.HEADER.replace(
        'int CreateTask(void(*fn)(u8),int priority){(void)fn;(void)priority;return 0;}',
        'u8 CreateTask(TaskFunc fn,u8 priority);') + STUBS
    groups = {
        'src/task.c': ('ResetTasks', 'CreateTask', 'InsertTask', 'DestroyTask',
                       'RunTasks', 'FindFirstActiveTask', 'TaskDummy'),
        'src/palette.c': ('IsSoftwarePaletteFadeFinishing', 'UpdateNormalPaletteFade',
                          'BeginNormalPaletteFade'),
        'src/field_weather.c': ('Weather_SetBlendCoeffs', 'Weather_SetTargetBlendCoeffs',
            'Weather_UpdateBlend', 'FadeInScreen_FogHorizontal', 'FadeInScreen_RainShowShade',
            'FadeInScreenWithWeather', 'FadeScreen', 'Task_WeatherInit', 'Task_WeatherMain',
            'None_Init', 'None_Main', 'None_Finish', 'IsWeatherNotFadingIn'),
        'src/field_weather_effects.c': ('FogHorizontal_InitVars', 'FogHorizontal_InitAll',
            'FogHorizontal_Main', 'FogHorizontal_Finish', 'Shade_InitVars', 'Shade_InitAll',
            'Shade_Main', 'Shade_Finish'),
        'src/start_menu.c': ('Task_StartMenuHandleInput',),
        'src/field_fadetransition.c': ('palette_bg_faded_fill_black', 'FadeInFromBlack',
            'FieldFadeTransitionBackgroundEffectIsFinished', 'Task_WaitFadeAndCreateStartMenuTask',
            'FadeTransition_FadeInOnReturnToStartMenu', 'Task_ContinueScript', 'FieldCB_ContinueScript'),
    }
    bodies = [extract_function(sources[path], name).replace(
        'void FogHorizontal_Main(void)', 'void SourceFogHorizontal_Main(void)')
        for path, names in groups.items() for name in names]
    code += ''.join(body.split('\n{', 1)[0]+';\n' for body in bodies)
    code += r'''
struct WeatherFuncs { void(*initVars)(void);void(*main)(void);void(*initAll)(void);bool8(*finish)(void); };
static const struct WeatherFuncs sWeatherFuncs[WEATHER_UNDERWATER+1]={
    [WEATHER_NONE]={None_Init,None_Main,None_Init,None_Finish},
    [WEATHER_SHADE]={Shade_InitVars,Shade_Main,Shade_InitAll,Shade_Finish},
    [WEATHER_FOG_HORIZONTAL]={FogHorizontal_InitVars,FogHorizontal_Main,FogHorizontal_InitAll,FogHorizontal_Finish}};
static void(*const sWeatherPalStateFuncs[])(void)={DoNothing,FadeInScreenWithWeather,DoNothing,DoNothing};
'''
    code += ''.join(bodies) + MAIN
    if retain_c:
        retain_c.write_text(code)
    with tempfile.TemporaryDirectory() as directory:
        source = Path(directory)/'field_return.c'
        executable = Path(directory)/('field_return.exe' if os.name == 'nt' else 'field_return')
        source.write_text(code)
        compile_c(['cc', '-std=c11', '-O0', str(source), '-o', str(executable)])
        result = json.loads(subprocess.check_output([str(executable)], text=True))
    return {'sourceCommit': COMMIT, 'columns': COLUMNS, 'traces': result['traces']}


def validate(result):
    cases = [
        ('menuReturn', 'fog', True, 22, 18),
        ('scriptReturn', 'fog', False, 22, 18),
        ('normalMenuReturn', 'normal', True, 27, 23),
        ('normalScriptReturn', 'normal', False, 27, 23),
        ('shadeMenuReturn', 'shade', True, 21, 17),
        ('shadeScriptReturn', 'shade', False, 21, 17),
    ]
    traces = result['traces']
    if result['columns'] != COLUMNS:
        raise ValueError('Invalid field-return vector columns')
    if [trace['name'] for trace in traces] != [case[0] for case in cases]:
        raise ValueError('Missing or duplicate field-return traces')
    for trace, (name, weather, menu, last, ready) in zip(traces, cases):
        rows = trace['frames']
        if len(rows) != last+1 or [row[0] for row in rows] != list(range(last+1)):
            raise ValueError('Missing field-return callback rows: '+name)
        if any(len(row) != len(COLUMNS) for row in rows):
            raise ValueError('Invalid field-return vector width: '+name)
        for row in rows:
            frame = row[0]
            # Normal software fade alternates BG/OBJ writes and waits five
            # finishing callbacks; weather sees completion on the next pass.
            if weather == 'normal':
                background = max(0, 16-2*(frame//2))
                objects = max(0, 16-2*(max(0, frame-1)//2))
                fade_counter = 0
            else:
                background = objects = min(16, max(0, 17-frame))
                fade_counter = min(16, max(0, frame-1))
            calls = 92+frame if weather == 'fog' and frame else 0
            # The source computes fog position before its counter/offset tick.
            scroll = [calls//4, calls%4, (-((calls-1)//4))&255] if calls else [0, 0, 0]
            expected = [
                frame, fade_counter, 1 if frame < ready else 3, background, calls,
                1 if menu and frame >= ready else -1,
                (0 if frame == ready else 1) if menu and frame >= ready else -1,
                ready+2 if menu and frame >= ready+2 else 0,
                ready+1 if not menu and frame >= ready+1 else 0,
                *scroll, objects, int(weather == 'normal' and frame < 22),
                3 if weather == 'shade' and frame >= ready else 0,
            ]
            for column, actual, wanted in zip(COLUMNS, row, expected):
                if actual != wanted:
                    raise ValueError(f'{name} frame {frame} {column}: {actual} != {wanted}')
    return len(traces)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--helpers', type=Path, default=Path(__file__).parent)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--retain-c', type=Path)
    args = parser.parse_args()
    result = generate(args.reference, args.helpers, args.retain_c)
    validate(result)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, separators=(',', ':'))+'\n')
    print(json.dumps({'traces': len(result['traces']),
                      'rows': sum(len(t['frames']) for t in result['traces'])}))


if __name__ == '__main__':
    main()
