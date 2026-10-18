#!/usr/bin/env python3
from pathlib import Path as _ReferencePath
import sys as _reference_sys
_reference_sys.path.insert(0, str(next(p for p in _ReferencePath(__file__).resolve().parents if (p / 'Reference/reference_c.py').is_file()) / 'Reference'))
from reference_c import compile_c, verify_reference
"""Compile two unchanged original-C commands with bounded trainer-double storage stubs."""
from pathlib import Path
import hashlib, subprocess, sys
import atexit
import tempfile
fixtures_only = "--fixtures-only" in sys.argv
arguments = [argument for argument in sys.argv[1:] if argument != "--fixtures-only"]
if len(arguments) != 2:
    raise SystemExit("Usage: build-source-oracle.py PINNED_REFERENCE OUTPUT_DIRECTORY [--fixtures-only]")
root, fixture_output = (Path(argument).resolve() for argument in arguments)
fixture_output.mkdir(parents=True, exist_ok=True)
if fixtures_only:
    temporary_build = tempfile.TemporaryDirectory(prefix="firered-switching-reference-")
    atexit.register(temporary_build.cleanup)
    output = Path(temporary_build.name)
else:
    output = fixture_output

expected='037335f4c725d7c9aecdac87066f2002b4bd7e14'
verify_reference(root)
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
compile_c(['cc','-std=c11','-O2',str(output/'source-switch-oracle.c'),'-o',str(output/'source-switch-oracle')])

# Cover the six current controlled Roar setups and their possible bounded RNG cursors.
import json
rows={}
for seed,level,live in [(0,90,15),(7,90,15),(31,90,7),(0,20,15),(31,20,15),(7,90,5)]:
 for _ in range(21):
  key=','.join(map(str,[seed,live,0,level,80]))
  rows[key]=list(map(int,subprocess.check_output([str(output/'source-switch-oracle'),*key.split(',')],text=True).strip().split(',')))
  seed=(seed*1103515245+24691)&0xffffffff
(output/'source-switch-vectors.json').write_text(json.dumps({'sourceCommit':expected,'rows':rows},indent=2)+'\n',encoding='utf-8')
print('Generated',len(rows),'independent C switch vectors')

# Only independent numeric output is published in fixture preparation mode.
if fixtures_only:
    for name in ['source-switch-vectors.json']:
        target = fixture_output / name
        candidate = (output / name).read_bytes()
        if target.exists() and target.read_bytes() != candidate:
            raise SystemExit("Existing reference differs; review a new candidate directory: " + str(target))
        target.write_bytes(candidate)
