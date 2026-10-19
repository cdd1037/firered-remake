#!/usr/bin/env python3
"""Extract the pinned pattern-shake body and run bounded signed-pattern probes."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function,array
p=argparse.ArgumentParser();p.add_argument('--repository',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();root=a.repository.resolve();out=a.output.resolve()
if out.is_relative_to(root):raise SystemExit('Use an output directory outside the repository.')
out.mkdir(parents=True,exist_ok=True)
source=root/'content/raw/pokefirered/037335f4/src/battle_anim_fire.c';raw=source.read_bytes();sha=hashlib.sha256(raw).hexdigest()
if sha!='369439a2956832942756db1afc5b9ca8955762b7af121ff4b4aeda156e07ede6':raise SystemExit('Pinned pattern-shake input differs.')
s=raw.decode();body=function(s,'AnimTask_ShakeTargetInPattern', signed_byte=False);tables='\n'.join(array(s,n) for n in ['sShakeDirsPattern0','sShakeDirsPattern1'])
prefix='''#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
typedef int8_t s8; typedef uint8_t u8; typedef int16_t s16;
#define TRUE 1
#define tShakeNum data[0]
#define tMaxShakes data[1]
#define tShakeOffset data[2]
#define tVertical data[3]
#define tPatternId data[4]
struct Task {s16 data[16];} gTasks[1];
struct Sprite {s16 x2,y2;} gSprites[2];
s16 gBattleAnimArgs[8];u8 gBattleAnimTarget=1,gBattlerSpriteIds[2]={0,1};int alive=1;
void DestroyAnimVisualTask(u8 id){alive=0;}
'''
suffix='''int main(int argc,char **argv){gBattleAnimArgs[0]=20;gBattleAnimArgs[1]=1;gBattleAnimArgs[2]=atoi(argv[1]);gBattleAnimArgs[3]=atoi(argv[2]);printf("[");for(int i=1;alive&&i<=20;i++){AnimTask_ShakeTargetInPattern(0);printf("%s[%d,%d,%d,%d]",i==1?"":",",i,gSprites[1].x2,gSprites[1].y2,alive);}puts("]");return 0;}'''
c=out/'pattern.c';c.write_text(prefix+tables+'\n'+body+'\n'+suffix)
for opt in ['O0','O2']:subprocess.run(['cc','-std=c11','-'+opt,'-fwrapv',str(c),'-o',str(out/opt)],check=True)
cases=[]
for vertical in [0,1]:
 for pattern in [0,16,-32768,32767]:
  rows=[subprocess.check_output([str(out/opt),str(vertical),str(pattern)]) for opt in ['O0','O2']]
  assert rows[0]==rows[1]
  cases.append(dict(vertical=vertical,pattern=pattern,rows=json.loads(rows[0])))
result=dict(schema='firered-pattern-shake-v1',commit='037335f4c725d7c9aecdac87066f2002b4bd7e14',source='src/battle_anim_fire.c',sourceSha256=sha,functionSha256=hashlib.sha256(body.encode()).hexdigest(),optimizationParity=['O0','O2'],cases=cases)
(out/'vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('8 cases / 160 source callback rows, O0/O2 identical')
