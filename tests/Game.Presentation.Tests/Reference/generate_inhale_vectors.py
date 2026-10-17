#!/usr/bin/env python3
"""Original DeepInhale task, task-affine interpreter and source foot compensation."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');ref.verify_reference(root/'content/raw/pokefirered/037335f4')
 helper=module('task_extract',root/'tests/Game.Presentation.Tests/Reference/generate_task_affine_vectors.py')
 raw=root/'content/raw/pokefirered/037335f4';source=(raw/'src/battle_anim_effects_3.c').read_text();mons=(raw/'src/battle_anim_mons.c').read_text()
 # Reuse the maintained task-ABI shim, not a second task-affine algorithm.
 shim=(root/'tests/Game.Presentation.Tests/Reference/generate_task_affine_vectors.py').read_text();h=re.search(" harness=r'''(.*?)'''",shim,re.S)[1]
 h+='\nstatic void AnimTask_DeepInhale_Step(u8);\n'+re.search(r'static const union AffineAnimCmd sDeepInhaleAffineAnimCmds\[\] =\s*\{.*?\};',source,re.S)[0]+'\n'
 for text,names in [(mons,['PrepareAffineAnimInTaskData','RunAffineAnimFromTaskData','SetBattlerSpriteYOffsetFromYScale']),(source,['AnimTask_DeepInhale','AnimTask_DeepInhale_Step'])]:
  for name in names:h+=helper.function(text,name)+'\n'
 h+=r'''
int main(void){for(int height=0;height<=31;height++){memset(gTasks,0,sizeof gTasks);memset(gSprites,0,sizeof gSprites);yDelta=height;gBattleAnimArgs[0]=0;AnimTask_DeepInhale(0);
for(int tick=0;tick<100&&active;tick++){gTasks[0].func(0);s16 *d=gTasks[0].data;printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",height,tick,d[0],d[1],d[2],d[10],d[11],d[12],gSprites[0].x2,gSprites[0].y2,active);}}return 0;}
'''
 with tempfile.TemporaryDirectory() as d:
  c=Path(d)/'inhale.c';exe=Path(d)/'inhale';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 return [list(map(int,line.split(','))) for line in out.splitlines()]
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')
