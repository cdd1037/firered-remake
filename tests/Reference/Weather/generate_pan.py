#!/usr/bin/env python3
import argparse,json,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function,macros,identity,COMMIT,verify
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);a=p.parse_args();r=a.reference;out=Path(__file__).resolve().parent;paths=['src/battle_anim.c','include/constants/battle_anim.h'];verify(r,paths, Path(__file__).resolve().parent / 'pinned-inputs.json');s=(r/paths[0]).read_text();names=['Cmd_panse_adjustnone','Task_PanFromInitialToTarget'];bodies=[function(s,n, signed_byte=False) for n in names]
c='''#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <assert.h>
typedef uint8_t u8;typedef int8_t s8;typedef uint16_t u16;typedef int16_t s16;typedef int32_t bool32;
#define TRUE 1
#define FALSE 0
#define T1_READ_16(p) ((p)[0]|((p)[1]<<8))
struct Task {void(*func)(u8);s16 data[16];}gTasks[16];u8 *sBattleAnimScriptPtr,gAnimSoundTaskCount;static int alive=1,lastPan,plays,controls,destroyed;
static u8 CreateTask(void(*func)(u8),u8 priority){assert(priority==1);gTasks[3].func=func;return 3;}
static void DestroyTask(u8 id){assert(id==3);alive=0;destroyed++;}
static void PlaySE12WithPanning(u16 song,s8 pan){assert(song==123);lastPan=pan;plays++;}
static void SE12PanpotControl(s16 pan){lastPan=pan;controls++;}
'''+macros([s[s.index('#define tInitialPan'):s.index('static void Cmd_panse(void)',s.index('#define tInitialPan'))]],['tInitialPan','tTargetPan','tIncrementPan','tFramesToWait','tCurrentPan','tFrameCounter'])+'\n'+'\n'.join(f[:f.index('{')].strip()+';' for f in bodies)+'\n'+'\n'.join(bodies)+'''
static void row(int frame){printf("[%d,%d,%d,%d,%d,%d,%d",frame,alive,gAnimSoundTaskCount,lastPan,plays,controls,destroyed);for(int i=0;i<16;i++)printf(",%d",gTasks[3].data[i]);puts("]");}
int main(int argc,char**argv){assert(argc==5);u8 bytes[]={0,123,0,atoi(argv[1]),atoi(argv[2]),atoi(argv[3]),atoi(argv[4])};sBattleAnimScriptPtr=bytes;Cmd_panse_adjustnone();assert(sBattleAnimScriptPtr==bytes+7);row(0);for(int f=1;alive&&f<2048;f++){gTasks[3].func(3);row(f);}assert(!alive);return 0;}
''';(out/'pan.c').write_text(c)
for opt in ['O0','O2']:subprocess.run(['cc','-std=c11','-'+opt,'-fwrapv',str(out/'pan.c'),'-o',str(out/('pan-'+opt))],check=True)
cases=[]
for args in [(-64,63,1,0),(63,-64,-1,0),(-64,63,2,0),(3,3,0,0),(-1,1,1,2),(0,1,0,0),(127,-128,-128,0)]:
 data=[subprocess.check_output([str(out/('pan-'+opt)),*map(str,args)]) for opt in ['O0','O2']];assert data[0]==data[1];rows=[json.loads(x) for x in data[0].splitlines()];cases.append(dict(args=list(args),rows=rows))
result=dict(schema='firered-panse-adjustnone-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,optimization_parity=['O0','O2'],rowFields=['frame','alive','soundTaskCount','lastPan','playCalls','panCalls','destroyCalls',*[f'data{i}' for i in range(16)]],cases=cases);(out/'pan-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('pan',len(cases),sum(len(c['rows']) for c in cases))
