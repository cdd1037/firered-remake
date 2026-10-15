#!/usr/bin/env python3
"""Compile only the pinned original input callback against harmless graphics/audio stubs.
No upstream script is executed. Usage: build-hall-pc-oracle.py PINNED_GIT OUTPUT_ROOT.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from pathlib import Path
import hashlib
import json
import subprocess
import sys
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
SCHEMA_FUNCTION_SHA = 'c06b0d73f5b66d398b046b518dc44217f48035f7a7867000363a42890796feb3'
SCAFFOLD = '#include <stdio.h>\n#include <stdint.h>\ntypedef uint8_t u8; typedef uint16_t u16;\n#define A_BUTTON 1\n#define B_BUTTON 2\n#define DPAD_UP 64\n#define DPAD_DOWN 128\n#define TRACKS_ALL 0\n#define JOY_NEW(button) (keys & (button))\nstatic int keys, gMPlayInfo_BGM;\nstruct Task { int16_t data[16]; void (*func)(u8); }; static struct Task gTasks[1];\nstatic void Task_HofPC_DrawSpritesPrintText(u8 id) {(void)id;}\nstatic void Task_HofPC_PrintMonInfo(u8 id) {(void)id;}\nstatic void Task_HofPC_HandlePaletteOnExit(u8 id) {(void)id;}\nstatic void FreeAndDestroyMonPicSprite(u8 id) {(void)id;}\nstatic int IsCryPlayingOrClearCrySongs(void) {return 0;}\nstatic void StopCryAndClearCrySongs(void) {}\nstatic void m4aMPlayVolumeControl(int *a,int b,int c) {(void)a;(void)b;(void)c;}\n'
MAIN = '\nint main(void) {\n const int numbers[]={0,1,999};\n for(int team=0;team<50;team++) for(int count=1;count<=6;count++) for(int member=0;member<count;member++)\n for(int n=0;n<3;n++) for(int mask=0;mask<16;mask++) {\n  keys=((mask&1)?1:0)|((mask&2)?2:0)|((mask&4)?64:0)|((mask&8)?128:0);\n  gTasks[0]=(struct Task){0}; gTasks[0].data[0]=team;gTasks[0].data[1]=numbers[n];gTasks[0].data[2]=member;gTasks[0].data[4]=count;\n  for(int i=5;i<11;i++)gTasks[0].data[i]=255;\n  Task_HofPC_HandleInput(0);\n  int exit=gTasks[0].func==Task_HofPC_HandlePaletteOnExit;\n  int redraw=gTasks[0].func==Task_HofPC_DrawSpritesPrintText;\n  int changed=redraw||gTasks[0].func==Task_HofPC_PrintMonInfo;\n  printf("%d %d %d %d %d %d %d %d %d %d\\n",team,count,member,numbers[n],keys,gTasks[0].data[0],gTasks[0].data[1],redraw?0:gTasks[0].data[2],exit,changed);\n }\n return 0;\n}\n'
source = subprocess.check_output(["git", "-C", sys.argv[1], "show", COMMIT + ":src/hall_of_fame.c"]).decode()
start = source.index("static void Task_HofPC_HandleInput(u8 taskId)\n{")
end = source.index("static void Task_HofPC_HandlePaletteOnExit(u8 taskId)\n{", start)
function = source[start:end]
redraw = source[source.index("static void Task_HofPC_DrawSpritesPrintText(u8 taskId)\n{"):start]
assert "gTasks[taskId].data[2] = 0;" in redraw
out = Path(sys.argv[2]) / "build"
out.mkdir(parents=True, exist_ok=True)
source_out = out / "source-input-oracle.c"
source_out.write_text(SCAFFOLD + function + MAIN)
subprocess.run(["cc", "-std=c11", "-O2", "-Wall", "-Wextra", "-Werror", str(source_out), "-o", str(out / "source-input-oracle")], check=True)
print(json.dumps(dict(sourceCommit=COMMIT, sourcePath="src/hall_of_fame.c", function="Task_HofPC_HandleInput", functionSha256=SCHEMA_FUNCTION_SHA, output=str(out / "source-input-oracle"))))
