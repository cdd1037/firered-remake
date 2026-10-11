#include <assert.h>
#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8;
typedef uint16_t u16;
#define MAX_MON_MOVES 4
#define ABILITY_PRESSURE 46
#define TRUE 1
#define FALSE 0
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 0
#define MOVE_IS_PERMANENT(a,b) 1
#define T1_READ_PTR(p) failure
struct { u16 moves[4]; u8 pp[4], ability; } gBattleMons[4];
struct { u8 bounceMove, stealMove; } gProtectStructs[4];
struct { u8 ppNotAffectedByPressure; } gSpecialStatuses[4];
struct { u8 battler; } gBattleScripting;
u8 gBattlerTarget, gBattlerAttacker, gCurrentTurnActionNumber, gBattlersCount, gEffectBattler, gActiveBattler;
u8 script[8], failed[8], *failure=failed, *gBattlescriptCurrInstr=script;
static void BtlController_EmitSetMonData(int a,int b,int c,int d,u8* e){(void)a;(void)b;(void)c;(void)d;(void)e;}
static void MarkBattlerForControllerExec(u8 a){(void)a;}
#include "source-commands.h"
int main(void){
 int checks=0;
 gBattlersCount=4;
 for(int i=0;i<4;i++){
  gBattlerAttacker=i;gCurrentTurnActionNumber=i;gBattlescriptCurrInstr=script;Cmd_trysetmagiccoat();
  assert(gBattlerTarget==i&&gSpecialStatuses[i].ppNotAffectedByPressure==1);checks++;
  assert(gProtectStructs[i].bounceMove==(i!=3));assert((gBattlescriptCurrInstr==failure)==(i==3));checks+=2;
  gBattlescriptCurrInstr=script;Cmd_trysetsnatch();assert(gProtectStructs[i].stealMove==(i!=3));checks++;
 }
 gBattleMons[1].ability=ABILITY_PRESSURE;gBattleMons[0].moves[0]=289;gBattleMons[0].pp[0]=0;gBattleMons[0].moves[1]=289;gBattleMons[0].pp[1]=10;
 PressurePPLose(1,0,289);assert(gBattleMons[0].pp[0]==0&&gBattleMons[0].pp[1]==10);checks++;
 gBattleMons[0].pp[0]=5;PressurePPLose(1,0,289);assert(gBattleMons[0].pp[0]==4);checks++;
 gBattlerAttacker=1;gBattlerTarget=1;gBattleScripting.battler=2;gBattlescriptCurrInstr=script;Cmd_snatchsetbattlers();assert(gBattlerAttacker==2&&gBattlerTarget==2&&gBattleScripting.battler==1);checks++;
 gBattlerAttacker=1;gBattlerTarget=0;gBattleScripting.battler=3;gBattlescriptCurrInstr=script;Cmd_snatchsetbattlers();assert(gBattlerAttacker==1&&gBattlerTarget==3&&gBattleScripting.battler==1);checks++;
 printf("{\"sourceCommit\":\"037335f4c725d7c9aecdac87066f2002b4bd7e14\",\"checks\":%d,\"passed\":true}\n",checks);
 return 0;
}
