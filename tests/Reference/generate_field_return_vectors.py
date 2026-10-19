"""Extract pinned menu/script field-return weather task ordering into tiny vectors.

This authored host extends the maintained fog oracle. Original task.c functions
run priority-80 weather before the later priority-80 menu wait, or after the
priority-10 script wait. Original start-menu task initialization is retained.
Hardware rendering, music, union-room presence and actual key input are bounded
stubs. Frame zero is after FadeInFromBlack and before first OverworldBasic;
black map/menu setup callbacks are outside this trace and do not tick weather.
No upstream repository script is run and no generated artwork is needed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

START_MENU_HASH = '68dd2fc725c7e2685f539fe40b00d5350eecfbfd33bbbda7d6d6ff9b6e400f56'
COLUMNS = ['frame', 'fadeCounter', 'palState', 'backgroundBlend', 'mainCalls',
           'menuTask', 'menuState', 'firstInput', 'scriptEnabled', 'scrollOffset',
           'scrollCounter', 'scrollX']

STUBS = r'''
int frame,firstInput,scriptEnabled;
bool8 (*sStartMenuCallback)(void);
int InUnionRoom(void){return FALSE;}
void SetUsingUnionRoomStartMenu(void){}
// Observe the first invocation of the real task's input handler, without
// manufacturing key events or asking the source menu to change its owner.
bool8 StartCB_HandleInput(void){if(!firstInput)firstInput=frame;return FALSE;}
void ScriptContext_Enable(void){scriptEnabled=frame;}
'''

MAIN = r'''
int main(void)
{
    printf("{\"traces\":[");
    for(int kind=0;kind<2;kind++)
    {
        reset();ResetTasks();firstInput=scriptEnabled=0;
        CreateTask(Task_WeatherInit,80);
        if(kind==0)FadeTransition_FadeInOnReturnToStartMenu();else FieldCB_ContinueScript();
        if(kind)printf(",");
        printf("{\"name\":\"%s\",\"frames\":[",kind?"scriptReturn":"menuReturn");
        for(frame=0;frame<=22;frame++)
        {
            if(frame){RunTasks();UpdatePaletteFade();printf(",");}
            int menuId=-1;
            for(int i=0;i<NUM_TASKS;i++)
                if(gTasks[i].isActive&&gTasks[i].func==Task_StartMenuHandleInput)menuId=i;
            printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",frame,
                weatherState.fadeScreenCounter,weatherState.palProcessingState,
                bgBlend,mainCalls,menuId,menuId<0?-1:gTasks[menuId].data[0],
                firstInput,scriptEnabled,weatherState.fogHScrollOffset,
                weatherState.fogHScrollCounter,weatherState.fogHScrollPosX);
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
    from reference_c import COMMIT

    task = (reference/'src/task.c').read_text()
    raw_menu = (reference/'src/start_menu.c').read_bytes()
    if hashlib.sha256(raw_menu).hexdigest() != START_MENU_HASH:
        raise ValueError('Field-return reference revision mismatch: src/start_menu.c')
    menu = raw_menu.decode()
    fade = (reference/'src/field_fadetransition.c').read_text()
    task_names = ('ResetTasks', 'CreateTask', 'InsertTask', 'DestroyTask',
                  'RunTasks', 'FindFirstActiveTask', 'TaskDummy')
    task_bodies = [extract_function(task, name) for name in task_names]
    return_bodies = [extract_function(menu, 'Task_StartMenuHandleInput')] + [
        extract_function(fade, name) for name in (
            'FadeInFromBlack', 'Task_WaitFadeAndCreateStartMenuTask',
            'FadeTransition_FadeInOnReturnToStartMenu', 'Task_ContinueScript',
            'FieldCB_ContinueScript')]
    declarations = lambda bodies: ''.join(body.split('\n{', 1)[0]+';\n' for body in bodies)
    extra = declarations(task_bodies)+''.join(task_bodies)+STUBS
    extra += declarations(return_bodies)+''.join(return_bodies)

    # The base generator checks the pinned hashes of task, weather, overworld,
    # palette and fade-transition source before compiling any extracted body.
    saved = warp.HEADER, fog.HEADER, fog.MAIN
    try:
        warp.HEADER = warp.HEADER.replace(
            'struct Task { s16 data[16]; } gTasks[1];', r'''
#define NUM_TASKS 16
#define NUM_TASK_DATA 16
#define HEAD_SENTINEL 0xFE
#define TAIL_SENTINEL 0xFF
#define REVISION 0
struct Task { s16 data[16]; void(*func)(u8); u8 isActive,prev,next,priority; } gTasks[NUM_TASKS];
typedef void(*TaskFunc)(u8);
''').replace('void DestroyTask(int taskId){(void)taskId;complete=1;}',
             'void DestroyTask(u8 taskId);')
        fog.HEADER = fog.HEADER.replace(
            'int CreateTask(void(*fn)(u8),int priority){(void)fn;(void)priority;return 0;}',
            'u8 CreateTask(TaskFunc fn,u8 priority);')
        fog.MAIN = fog.MAIN[:fog.MAIN.index('void row(')]+extra+MAIN
        result = fog.generate(reference, helpers, retain_c)
    finally:
        warp.HEADER, fog.HEADER, fog.MAIN = saved
    return {'sourceCommit': COMMIT, 'columns': COLUMNS, 'traces': result['traces']}


def validate(result):
    traces = result['traces']
    if [trace['name'] for trace in traces] != ['menuReturn', 'scriptReturn']:
        raise ValueError('Missing or duplicate field-return traces')
    for trace in traces:
        rows = trace['frames']
        if len(rows) != 23 or [row[0] for row in rows] != list(range(23)):
            raise ValueError('Missing field-return callback rows')
        if any(len(row) != len(COLUMNS) for row in rows):
            raise ValueError('Invalid field-return vector width')
        for row in rows:
            frame = row[0]
            if row[3] != (16 if frame <= 1 else max(0, 17-frame)):
                raise ValueError('Field-return fog coefficient mismatch')
            if row[2] != (1 if frame <= 17 else 3):
                raise ValueError('Field-return weather readiness mismatch')
            if row[4] != (0 if frame == 0 else 92+frame):
                raise ValueError('Field-return weather clock mismatch')
            if trace['name'] == 'menuReturn':
                if row[5] != (-1 if frame < 18 else 1):
                    raise ValueError('Menu-return task creation mismatch')
                if row[6] != (-1 if frame < 18 else 0 if frame == 18 else 1):
                    raise ValueError('Menu-return task initialization mismatch')
                if row[7] != (0 if frame < 20 else 20) or row[8] != 0:
                    raise ValueError('Menu-return first input mismatch')
            elif row[5:8] != [-1, -1, 0] or row[8] != (0 if frame < 19 else 19):
                raise ValueError('Script-return continuation mismatch')
        if rows[18][9:] != [27, 2, 229] or rows[20][9:] != [28, 0, 229]:
            raise ValueError('Field-return fog phase mismatch')
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
