#!/usr/bin/env python3
"""Extract the pinned predicate and native visual-task destruction into a bounded C oracle."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root):
 raw=root/'content/raw/pokefirered/037335f4';ref=module('doom_ref',root/'tests/Reference/reference_c.py');ref.verify_reference(raw)
 ex=module('doom_extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 parts=[('src/battle_anim.c','DestroyAnimVisualTask'),('src/battle_anim_dark.c','GetIsDoomDesireHitTurn')];functions=[];sources=[]
 for path,name in parts:
  source=(raw/path).read_text();body=ex.function(source,name);functions.append(body);start=source[:source.index(body)].count('\n')+1;sources.append(dict(path=path,function=name,line=start,endLine=start+body.count('\n'),sha256=hashlib.sha256(body.encode()).hexdigest()))
 constants=(raw/'include/constants/battle_anim.h').read_text();assert re.search(r'^#define ARG_RET_ID 7$',constants,re.M)
 harness=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8;typedef int16_t s16;
#define FALSE 0
#define TRUE 1
#define ARG_RET_ID 7
static u8 gAnimMoveTurn,gAnimVisualTaskCount;
static s16 gBattleAnimArgs[8];
static int active,destroyed;
static void DestroyTask(u8 taskId){assert(taskId==3&&active);active=0;destroyed++;}
'''+ '\n'.join(functions)+r'''
int main(void){const s16 prior[]={-32768,-1,0,32767};
 for(int turn=0;turn<256;turn++)for(int seed=0;seed<4;seed++){
  for(int i=0;i<8;i++)gBattleAnimArgs[i]=(s16)(i*37-100);
  gBattleAnimArgs[7]=prior[seed];gAnimMoveTurn=(u8)turn;gAnimVisualTaskCount=1;active=1;destroyed=0;
  GetIsDoomDesireHitTurn(3);printf("%d,%d",turn,prior[seed]);for(int i=0;i<8;i++)printf(",%d",gBattleAnimArgs[i]);printf(",%d,%d,%d\n",gAnimVisualTaskCount,active,destroyed);
 }
 return 0;
}
'''
 with tempfile.TemporaryDirectory() as d:
  c=Path(d)/'doom_desire.c';exe=Path(d)/'doom_desire';c.write_text(harness);ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)]);rows=[list(map(int,line.split(','))) for line in subprocess.check_output([str(exe)],text=True).splitlines()]
 assert len(rows)==1024 and all(len(row)==13 and row[-3:]==[0,0,1] for row in rows)
 return dict(schemaVersion=1,sources=sources,columns=['turn','initialReturn',*[f'arg{i}' for i in range(8)],'visualCount','taskActive','destroyCount'],rows=rows)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():p.error('Refusing to overwrite an existing source vector candidate')
 result=build(a.root);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print('Doom Desire source predicate rows:',len(result['rows']))
