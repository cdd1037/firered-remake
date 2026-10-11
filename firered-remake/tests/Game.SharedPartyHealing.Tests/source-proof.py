#!/usr/bin/env python3
"""Run the pinned original healpartystatus command, with only controller/data stubs."""
import hashlib, json, pathlib, subprocess, sys
repo=pathlib.Path(sys.argv[1]); out=pathlib.Path(sys.argv[2]); out.mkdir(parents=True,exist_ok=False)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()==pin
inputs=[]
for name in ['src/battle_script_commands.c','data/battle_scripts_1.s','src/data/battle_moves.h']:
    data=(repo/name).read_bytes(); original=subprocess.check_output(['git','-C',str(repo),'show',f'{pin}:{name}'])
    assert data==original, name
    inputs.append(dict(path=name,sha256=hashlib.sha256(data).hexdigest()))
source=(repo/'src/battle_script_commands.c').read_text()
a=source.index('static void Cmd_healpartystatus(void)\n{'); b=source.index('\nstatic void Cmd_cursetarget',a)
command=source[a:b]
header=r'''
#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32;
#define MOVE_HEAL_BELL 215
#define ABILITY_SOUNDPROOF 43
#define B_MSG_BELL 0
#define B_MSG_SOOTHING_AROMA 1
#define B_MSG_BELL_SOUNDPROOF_ATTACKER 2
#define B_MSG_BELL_SOUNDPROOF_PARTNER 4
#define B_SIDE_PLAYER 0
#define BIT_FLANK 2
#define BATTLE_TYPE_DOUBLE 1
#define STATUS2_NIGHTMARE (1u<<27)
#define PARTY_SIZE 6
#define SPECIES_NONE 0
#define SPECIES_EGG 412
#define MON_DATA_SPECIES_OR_EGG 1
#define MON_DATA_ABILITY_NUM 2
#define MULTISTRING_CHOOSER 0
#define BUFFER_A 0
#define REQUEST_STATUS_BATTLE 0
struct Pokemon { int species,ability; u32 status; } gPlayerParty[6],gEnemyParty[6];
struct { int ability; u32 status1,status2; } gBattleMons[4];
struct { int battler; } gBattleScripting;
u32 gBitTable[4]={1,2,4,8}; u8 gBattleCommunication[1],gBattlerPartyIndexes[4]={0,0,1,1};
int gCurrentMove,gBattlerAttacker,gActiveBattler,gBattleTypeFlags,gAbsentBattlerFlags,mask,records;
u8 *gBattlescriptCurrInstr;
int GetBattlerSide(int b){return b&1;} int GetBattlerPosition(int b){return b;}
int GetBattlerAtPosition(int b){return b;}
int GetMonData(struct Pokemon *p,int request){return request==MON_DATA_SPECIES_OR_EGG?p->species:p->ability;}
int GetAbilityBySpecies(int species,int number){(void)species;return number;}
void RecordAbilityBattle(int b,int ability){(void)ability;records|=1<<b;}
void BtlController_EmitSetMonData(int a,int b,int m,int size,const void *value){
 (void)a;(void)b;(void)size;mask=m;struct Pokemon *party=GetBattlerSide(gActiveBattler)?gEnemyParty:gPlayerParty;
 for(int i=0;i<6;i++)if(m&(1<<i))party[i].status=*(const u32*)value;
}
void MarkBattlerForControllerExec(int b){(void)b;}
'''
# actor, Heal Bell, absent partner, live Soundproof bits (actor/partner), partner permanent Soundproof, doubles.
# Expected healed party mask, cured live mask (actual numeric slots), ability records.
cases=[(0,1,0,0,1,1,39,5,0),(0,1,0,3,1,1,36,0,5),(1,1,0,1,1,1,38,8,2),
       (2,1,0,2,1,1,37,4,1),(0,1,1,2,0,1,39,1,0),(0,1,1,0,1,1,37,1,0),
       (3,0,0,3,1,1,63,10,0),(0,0,1,3,1,1,63,1,0),(0,1,0,0,1,0,37,1,0)]
rows=','.join('{'+','.join(map(str,c))+ '}' for c in cases)
driver=r'''
int main(void){
 int cases[][9]={ROWS};
 for(unsigned n=0;n<sizeof(cases)/sizeof(cases[0]);n++){
  int *c=cases[n];gBattlerAttacker=c[0];int partner=c[0]^2;gCurrentMove=c[1]?MOVE_HEAL_BELL:312;
  gAbsentBattlerFlags=c[2]?1<<partner:0;gBattleTypeFlags=c[5];mask=records=0;
  u8 script[2]={0};gBattlescriptCurrInstr=script;
  for(int i=0;i<4;i++){gBattleMons[i].ability=0;gBattleMons[i].status1=2;gBattleMons[i].status2=STATUS2_NIGHTMARE;}
  gBattleMons[c[0]].ability=c[3]&1?ABILITY_SOUNDPROOF:0;gBattleMons[partner].ability=c[3]&2?ABILITY_SOUNDPROOF:0;
  for(int i=0;i<6;i++){gPlayerParty[i]=(struct Pokemon){i==4?SPECIES_EGG:1,i==3||i==1&&c[4]?ABILITY_SOUNDPROOF:0,2};gEnemyParty[i]=gPlayerParty[i];}
  gBattlerPartyIndexes[c[0]]=0;gBattlerPartyIndexes[partner]=1;
  Cmd_healpartystatus();int cured=0;
  for(int i=0;i<4;i++){if(gBattleMons[i].status1==0&&gBattleMons[i].status2==0)cured|=1<<i;}
  struct Pokemon *party=c[0]&1?gEnemyParty:gPlayerParty,*opponent=c[0]&1?gPlayerParty:gEnemyParty;
  for(int i=0;i<6;i++)if(party[i].status!=((c[6]&(1<<i))?0:2)||opponent[i].status!=2)return 2;
  if(mask!=c[6]||cured!=c[7]||records!=c[8]||gBattlescriptCurrInstr!=script+1)return 3;
  printf("%u,%d,%d,%d\n",n,mask,cured,records);
 }
 return 0;
}
'''.replace('ROWS',rows)
unit=out/'command.c';unit.write_text(header+command+driver)
subprocess.run(['cc','-std=c11','-O2','-fsanitize=undefined','-fno-sanitize-recover=all',str(unit),'-o',str(out/'command')],check=True)
result=subprocess.check_output([str(out/'command')],text=True)
(out/'result.txt').write_text(result)
(out/'provenance.json').write_text(json.dumps(dict(pin=pin,inputs=inputs,vectors=len(cases),adapterSha256=hashlib.sha256(unit.read_bytes()).hexdigest()),indent=2))
print(f'PASS {len(cases)} pinned original healpartystatus command vectors')
