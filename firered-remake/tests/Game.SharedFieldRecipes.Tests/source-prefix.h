#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "constants/battle.h"
#include "constants/battle_script_commands.h"
#include "constants/abilities.h"
#include "constants/pokemon.h"
#include "constants/items.h"
#include "constants/hold_effects.h"
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32; typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define B_BUFF_PLACEHOLDER_BEGIN 0
#define B_BUFF_MOVE 0
#define B_BUFF_EOS 0
#define B_MSG_CANT_SLEEP_UPROAR 0
#define B_MSG_UPROAR_KEPT_AWAKE 1
#define ABILITYEFFECT_CHECK_FIELD_EXCEPT_BATTLER 14
#define IS_BATTLER_OF_TYPE(b,t) (gBattleMons[b].type == (t))
#define GET_BATTLER_SIDE(b) ((b) & 1)
#define B_SIDE_PLAYER 0
struct MonHost { u32 status1,status2; u16 item,level; u8 ability,type; } gBattleMons[4];
struct { u8 battler; } gBattleScripting;
struct { u8 wrappedBy[4],wrappedMove[8]; } structure, *gBattleStruct=&structure;
struct { u8 spikesAmount; } gSideTimers[2];
struct { u8 holdEffect; } gEnigmaBerries[4];
u32 gBattleTypeFlags,gStatuses3[4],gSideStatuses[2],seed;
u16 gPaydayMoney;
u8 gBattlerAttacker,gBattlerTarget,gActiveBattler,gBattlersCount=4,gPotentialItemEffectBattler,gLastUsedAbility,gBattleCommunication[8],gBattleTextBuff1[8];
u8 script[4],BattleScript_WrapFree[4],BattleScript_LeechSeedFree[4],BattleScript_SpikesFree[4],paydayScript[4];
u8 *gBattlescriptCurrInstr=script,*sMoveEffectBS_Ptrs[256];
int GetBattlerSide(int b){return b&1;}
int ItemId_GetHoldEffect(int item){return item==ITEM_SMOKE_BALL?HOLD_EFFECT_CAN_ALWAYS_RUN:0;}
int AbilityBattleEffects(int x,int b,int a,int y,int z){for(int i=0;i<gBattlersCount;i++)if(i!=b && gBattleMons[i].ability==a)return i+1;return 0;}
void BattleScriptPushCursor(void){}
void BattleScriptPush(void *p){}
u16 Random(void){seed=1103515245u*seed+24691;return seed>>16;}
