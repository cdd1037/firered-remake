#!/usr/bin/env python3
"""Compile four unchanged pinned FireRed command bodies against small boundary inputs."""
import hashlib,json,pathlib,subprocess,sys,tempfile
root=pathlib.Path(sys.argv[1]); out=pathlib.Path(sys.argv[2]); here=pathlib.Path(__file__).parent
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def body(file,name):
 text=(root/file).read_text()
 # Select definition, skipping prototypes and calls.
 import re
 m=re.search(r'^(?:static )?(?:void|u8) '+name+r'\([^;]*?\)\n\{',text,re.M); assert m,name
 end=m.end(); depth=1
 while depth:
  depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[m.start():end]
names=[('src/battle_util.c','PressurePPLoseOnUsingImprison'),('src/battle_util.c','GetImprisonedMovesCount'),('src/battle_script_commands.c','Cmd_trysetencore'),('src/battle_script_commands.c','Cmd_tryimprison')]
parts=[body(*n) for n in names]
prefix='''#include <stdio.h>
#include <string.h>
#include "constants/moves.h"
#include "constants/abilities.h"
typedef unsigned char u8; typedef unsigned short u16; typedef int s32;
#define MAX_MON_MOVES 4
#define STATUS3_IMPRISONED_OTHERS 1
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 0
#define MOVE_IS_PERMANENT(a,b) 0
#define T1_READ_PTR(p) failure
struct Mon {u16 moves[4];u8 pp[4];u8 ability;} gBattleMons[4];
struct Disable {u16 encoredMove;u8 encoredMovePos,encoreTimer,encoreTimerStartValue;} gDisableStructs[4];
u16 gLastMoves[4];unsigned gStatuses3[4];u8 gBattlerTarget=1,gBattlerAttacker=0,gBattlersCount=4,gActiveBattler;u16 roll;int draws;u8 instructions[16],bad[1];u8 *gBattlescriptCurrInstr,*failure=bad;
u16 Random(void){draws++;return roll;} u8 GetBattlerSide(u8 n){return n&1;}
void BtlController_EmitSetMonData(int a,int b,int c,int d,void*e){} void MarkBattlerForControllerExec(int a){}
void reset(void){memset(gBattleMons,0,sizeof(gBattleMons));memset(gDisableStructs,0,sizeof(gDisableStructs));memset(gStatuses3,0,sizeof(gStatuses3));memset(gLastMoves,0,sizeof(gLastMoves));gBattlescriptCurrInstr=instructions;draws=0;roll=0;}
'''
main='''int main(void){puts("{\\"encore\\":[");for(int k=0;k<9;k++){reset();gBattleMons[1].moves[0]=MOVE_TACKLE;gBattleMons[1].pp[0]=5;gLastMoves[1]=MOVE_TACKLE;roll=k<4?k:3;
if(k==4)gLastMoves[1]=MOVE_ENCORE;if(k==5)gLastMoves[1]=MOVE_UNAVAILABLE;if(k==6)gBattleMons[1].pp[0]=0;if(k==7)gDisableStructs[1].encoredMove=MOVE_TACKLE;if(k==8){gBattleMons[1].moves[1]=MOVE_TACKLE;gBattleMons[1].pp[0]=0;gBattleMons[1].pp[1]=9;}
Cmd_trysetencore();printf("%s{\\"case\\":%d,\\"success\\":%s,\\"slot\\":%d,\\"turns\\":%d,\\"draws\\":%d}",k?",":"",k,gBattlescriptCurrInstr==failure?"false":"true",gDisableStructs[1].encoredMovePos,gDisableStructs[1].encoreTimer,draws);}
puts("],\\"imprison\\":[");for(int k=0;k<6;k++){reset();gBattleMons[0].moves[0]=MOVE_IMPRISON;gBattleMons[0].moves[1]=MOVE_TACKLE;gBattleMons[0].pp[0]=9;gBattleMons[3].moves[0]=MOVE_TACKLE;if(k==1)gBattleMons[3].moves[0]=MOVE_GROWL;if(k==2){gBattleMons[2].moves[0]=MOVE_TACKLE;gBattleMons[3].moves[0]=MOVE_GROWL;}if(k>=3){gBattleMons[1].ability=ABILITY_PRESSURE;gBattleMons[3].ability=ABILITY_PRESSURE;}if(k==4)gStatuses3[0]=1;if(k==5)gBattleMons[3].moves[0]=MOVE_GROWL;
Cmd_tryimprison();printf("%s{\\"case\\":%d,\\"success\\":%s,\\"pp\\":%d,\\"blocked3\\":%d}",k?",":"",k,gBattlescriptCurrInstr==failure?"false":"true",gBattleMons[0].pp[0],GetImprisonedMovesCount(3,MOVE_TACKLE));}puts("]}");}
'''
with tempfile.TemporaryDirectory(prefix='firered-encore-source-') as td:
 p=pathlib.Path(td);(p/'probe.c').write_text(prefix+'\n'.join(parts)+main)
 subprocess.run(['cc','-std=c99','-I'+str(root/'include'),str(p/'probe.c'),'-o',str(p/'probe')],check=True)
 result=json.loads(subprocess.check_output([str(p/'probe')],text=True))
result['source_sha']='037335f4c725d7c9aecdac87066f2002b4bd7e14';result['functions']={name:hashlib.sha256(part.encode()).hexdigest() for (_,name),part in zip(names,parts)}
assert [x['turns'] for x in result['encore'][:4]]==[3,4,5,6]
assert all(not x['success'] and x['draws']==0 for x in result['encore'][4:])
assert [x['pp'] for x in result['imprison']]==[9,9,9,7,9,7]
assert [x['success'] for x in result['imprison']]==[True,False,False,True,False,False]
out.write_text(json.dumps(result,indent=2)+'\n');print('PASS 15 exact source command boundaries')
