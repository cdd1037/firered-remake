"""Extract the pinned Quest Log task bodies; stub only rendering/platform effects."""
import argparse, json, re, subprocess, tempfile
from pathlib import Path
from reference_c import compile_c, verify_reference

def function(source, name):
    match = re.search(r'^static (?:void|bool8) '+name+r'\([^;]+?\)\n\{', source, re.M)
    start=match.start(); end=match.end(); depth=1
    while depth:
        depth += (source[end]=='{')-(source[end]=='}'); end+=1
    return source[start:end]

p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
verify_reference(a.reference)
s=(a.reference/'src/quest_log.c').read_text()
header=r'''
#include <stdio.h>
typedef unsigned char u8; typedef short s16; typedef int bool8;
#define TRUE 1
#define FALSE 0
#define END_MODE_FINISH 2
#define WIN_TOP_BAR 0
#define WIN_BOTTOM_BAR 1
#define WIN_COUNT 3
#define COPYWIN_MAP 0
#define COPYWIN_GFX 0
#define PLTT_SIZE 1024
#define BG_PLTT_ID(x) 0
#define OBJ_PLTT_ID(x) 256
#define QL_TINT_NONE 0
#define A_BUTTON 1
#define B_BUTTON 2
#define JOY_NEW(x) pressed
struct Task {s16 data[16]; void (*func)(u8);};
struct Task gTasks[1];
struct PlaybackControl {int endMode;} sPlaybackControl;
struct {int width,height;} sWindowTemplates[3]={{30,2},{30,2},{30,4}};
struct {int autoScroll;} gTextFlags;
int sWindowIds[3]={0,1,2},gPlttBufferUnfaded[512],gPlttBufferFaded[512],sPalettesBackup[512];
int gDisableMapMusicChangeOnMapLoad,gGlobalFieldTintMode,gHelpSystemEnabled,gQuestLogState;
int headerVisible=1,barHeight=16,finished=0,caption=1,pressed=0;
#define Overworld_PlaySpecialMapMusic() ((void)0)
#define QL_SlightlyDarkenSomePals() ((void)0)
#define ClearWindowTilemap(x) ((void)0)
#define CopyWindowToVram(x,y) ((void)0)
#define RemoveWindow(x) ((void)0)
#define ShowMapNamePopup(x) ((void)0)
#define CpuCopy16(x,y,z) ((void)0)
#define Free(x) ((void)0)
#define ClearPlayerHeldMovementAndUnfreezeObjectEvents() ((void)0)
#define UnlockPlayerFieldControls() ((void)0)
#define DisableWildEncounters(x) ((void)0)
#define DestroyTask(x) (finished=1)
#define CopyPaletteInvertedTint(a,b,c,d) ((void)0)
#define QuestLog_CloseTextWindow() (caption=0)
void FillWindowPixelRect(int id,int color,int x,int y,int w,int h){if(id==0){if(color==15)headerVisible=0;else barHeight--;}}
static bool8 RestoreScreenAfterPlayback(u8);
static void Task_EndQuestLog(u8);
'''
c=header+'\n#define tTimer data[0]\n'+function(s,'Task_WaitAtEndOfQuestLog')+'\n#undef tTimer\n#define tState data[0]\n#define tTimer data[1]\n'+function(s,'Task_EndQuestLog')+'\n#undef tState\n'+function(s,'RestoreScreenAfterPlayback')+r'''
int main(void){gTasks[0].func=Task_WaitAtEndOfQuestLog;for(int i=1;i<=180;i++){gTasks[0].func(0);printf("%d,%d,%d,%d,%d\n",i,caption,headerVisible,barHeight,finished);}return 0;}
'''
with tempfile.TemporaryDirectory() as d:
    source=Path(d)/'quest.c';exe=Path(d)/'quest';source.write_text(c)
    compile_c(['cc','-std=c11','-O2',str(source),'-o',str(exe)])
    rows=[list(map(int,x.split(','))) for x in subprocess.check_output([str(exe)],text=True).splitlines()]
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(rows,separators=(',',':'))+'\n')
print(f'{len(rows)} source task rows')
