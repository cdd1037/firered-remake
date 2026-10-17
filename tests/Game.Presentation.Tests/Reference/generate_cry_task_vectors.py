#!/usr/bin/env python3
import argparse,importlib.util,json,subprocess,tempfile
from pathlib import Path
def module(n,p):
 s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');ex=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');raw=root/'content/raw/pokefirered/037335f4';ref.verify_reference(raw);src=(raw/'src/battle_anim_sound_tasks.c').read_text()
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int8_t s8;typedef int16_t s16;
#define DOUBLE_CRY_GROWL 255
#define CRY_MODE_GROWL_2 10
#define CRY_MODE_ROAR_2 8
#define CRY_MODE_ECHO_END 6
#define tSpecies data[1]
#define tPan data[2]
#define tState data[9]
struct Task{s16 data[16];};struct Task gTasks[1];int playing,dead,mode,species,pan;
int IsCryPlaying(void){return playing;}void DestroyAnimVisualTask(u8 id){dead=1;}void PlayCry_ByMode(u16 s,s8 p,u8 m){species=s;pan=p;mode=m;}
'''
 for n in ['SoundTask_PlayDoubleCry_Step','SoundTask_PlayCryWithEcho_Step','SoundTask_WaitForCry']:h+=ex.function(src,n)+'\n'
 h+=r'''
int main(void){for(int kind=0;kind<3;kind++)for(int release=0;release<9;release++){memset(gTasks,0,sizeof gTasks);dead=0;gTasks[0].data[0]=255;gTasks[0].data[1]=132;gTasks[0].data[2]=-64;
for(int tick=0;tick<12&&!dead;tick++){mode=species=pan=0;playing=tick<release;if(kind==0)SoundTask_PlayDoubleCry_Step(0);else if(kind==1)SoundTask_PlayCryWithEcho_Step(0);else SoundTask_WaitForCry(0);printf("%d,%d,%d,%d,%d,%d,%d,%d\n",kind,release,tick,gTasks[0].data[9],dead,mode,species,pan);}}return 0;}
'''
 with tempfile.TemporaryDirectory() as d:
  c=Path(d)/'cry-task.c';exe=Path(d)/'cry-task';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 return [list(map(int,l.split(','))) for l in out.splitlines()]
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')
