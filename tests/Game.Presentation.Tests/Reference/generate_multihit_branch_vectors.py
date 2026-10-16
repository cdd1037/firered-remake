#!/usr/bin/env python3
"""Run original move-turn branches with all source u8 turn values.

Only the GBA address reader is modeled: serialized little-endian addresses point
into a bounded local byte array instead of GBA memory. The command body is exact
pinned C; ordinary tests consume the generated numerical branch targets.
"""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]

def build(reference,helper):
 spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 source=(reference/'src/battle_anim.c').read_text()
 bodies=[]
 for command in ['choosetwoturnanim','jumpifmoveturn']:
  match=re.search(r'^static void Cmd_'+command+r'\(void\)\n\{.*?^\}',source,re.M|re.S)
  if match is None:raise ValueError('Missing original move-turn command '+command)
  bodies.append(match[0])
 harness=r'''
#include <stdint.h>
#include <stdio.h>
#include <assert.h>
typedef uint8_t u8;
static u8 code[64];static const u8 *sBattleAnimScriptPtr;static u8 gAnimMoveTurn;
#define T2_READ_PTR(p) (code + ((uint32_t)(p)[0] | ((uint32_t)(p)[1]<<8) | ((uint32_t)(p)[2]<<16) | ((uint32_t)(p)[3]<<24)))
'''+ '\n'.join(bodies)+r'''
int main(void){
 code[0]=0x11;code[1]=32;code[5]=48;
 for(int turn=0;turn<256;turn++){
  sBattleAnimScriptPtr=code;gAnimMoveTurn=(u8)turn;Cmd_choosetwoturnanim();
  assert(sBattleAnimScriptPtr==code+32||sBattleAnimScriptPtr==code+48);
  printf("%d\n",(int)(sBattleAnimScriptPtr-code));
 }
 const u8 checks[]={0,1,2,255};
 code[0]=0x12;code[2]=32;code[3]=code[4]=code[5]=0;
 for(int check=0;check<4;check++){
  code[1]=checks[check];
  for(int turn=0;turn<256;turn++){
   sBattleAnimScriptPtr=code;gAnimMoveTurn=(u8)turn;Cmd_jumpifmoveturn();
   assert(sBattleAnimScriptPtr==code+32||sBattleAnimScriptPtr==code+6);
   printf("%d\n",(int)(sBattleAnimScriptPtr-code));
  }
 }
}
'''
 with tempfile.TemporaryDirectory(prefix='multihit-branch-c-') as tmp:
  c=Path(tmp)/'branch.c';exe=Path(tmp)/'branch';c.write_text(harness)
  ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)])
  values=[int(line) for line in subprocess.check_output([str(exe)],text=True).splitlines()]
 if len(values)!=1280:raise ValueError('Incomplete source turn vectors')
 return dict(targets=[32,48],branches=values[:256],jump=dict(checks=[0,1,2,255],target=32,fallthrough=6,branches=[values[i:i+256] for i in range(256,1280,256)]))

if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
 if args.output.exists():parser.error('Refusing to overwrite an existing fixture.')
 args.output.write_text(json.dumps(build(args.reference,args.helper),separators=(',',':'))+'\n')
