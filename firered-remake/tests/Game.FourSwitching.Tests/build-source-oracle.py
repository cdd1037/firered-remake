#!/usr/bin/env python3
"""Compile two unchanged original-C commands with bounded trainer-double storage stubs."""
from pathlib import Path
import hashlib, subprocess, sys
root, output = map(Path, sys.argv[1:])
expected='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()==expected
source=(root/'src/battle_script_commands.c').read_text()
def function(signature):
 start=source.index(signature+'\n{'); opening=source.index('{',start); depth=1; end=opening+1
 while depth:
  depth += (source[end]=='{')-(source[end]=='}'); end+=1
 return source[start:end]
functions='\n\n'.join(function(x) for x in ['static bool8 TryDoForceSwitchOut(void)','static void Cmd_forcerandomswitch(void)'])
header=r'''
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int bool8;
#define TRUE 1
#define FALSE 0
#define PARTY_SIZE 6
#define B_SIDE_PLAYER 0
#define BATTLE_TYPE_TRAINER 1
#define BATTLE_TYPE_DOUBLE 2
#define BATTLE_TYPE_MULTI 4
#define BIT_FLANK 2
#define SPECIES_NONE 0
#define MON_DATA_SPECIES 0
#define MON_DATA_IS_EGG 1
#define MON_DATA_HP 2
#define MON_CAN_BATTLE(mon) (((GetMonData(mon, MON_DATA_SPECIES) && GetMonData(mon, MON_DATA_IS_EGG) != TRUE && GetMonData(mon, MON_DATA_HP))))
struct Pokemon { int values[3]; };
struct Pokemon gPlayerParty[6],gEnemyParty[6];
struct BattlePokemon { int level; } gBattleMons[4];
struct BattleStruct { u8 battlerPartyIndexes[4],monToSwitchIntoId[4]; } storage,*gBattleStruct=&storage;
u8 gBattlerAttacker=0,gBattlerTarget=1,gBattlerPartyIndexes[4]={0,0,1,1};
u32 gBattleTypeFlags=BATTLE_TYPE_TRAINER|BATTLE_TYPE_DOUBLE;
const u8 Failed[]={1}, BattleScript_SuccessForceOut[]={2}, Initial[]={3};
const u8 *gBattlescriptCurrInstr=Initial;
#define T1_READ_PTR(ptr) Failed
static u32 seed,draws;
static u16 Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
static int GetBattlerSide(int x){return x&1;}
static int GetMonData(struct Pokemon *mon,int kind){return mon->values[kind];}
static int GetLinkTrainerFlankId(int x){return 0;}
static int GetBattlerMultiplayerId(int x){return 0;}
static int IsMultiBattle(void){return 0;}
static void UpdatePartyOwnerOnSwitch_NonMulti(int x){}
static void SwitchPartyOrderLinkMulti(int a,int b,int c){}
'''
main=r'''
int main(int argc,char **argv){
 if(argc!=6)return 2;
 seed=strtoul(argv[1],0,10);int live=atoi(argv[2]),egg=atoi(argv[3]);
 gBattleMons[0].level=atoi(argv[4]);gBattleMons[1].level=atoi(argv[5]);
 for(int i=0;i<6;i++){gEnemyParty[i].values[0]=i<4?1:0;gEnemyParty[i].values[1]=(egg>>i)&1;gEnemyParty[i].values[2]=(live>>i)&1;}
 Cmd_forcerandomswitch();
 printf("%d,%u,%u\n",gBattlescriptCurrInstr==BattleScript_SuccessForceOut?storage.monToSwitchIntoId[1]:-1,seed,draws);
 return 0;
}
'''
output.mkdir(parents=True,exist_ok=True)
(output/'source-switch-oracle.c').write_text(header+'\n'+functions+'\n'+main)
(output/'source-switch-oracle.sha256').write_text(hashlib.sha256(functions.encode()).hexdigest()+' extracted source functions\n')
subprocess.run(['cc','-std=c11','-O2',str(output/'source-switch-oracle.c'),'-o',str(output/'source-switch-oracle')],check=True)
