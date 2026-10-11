#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32;
#include "constants/battle.h"
#include "constants/battle_script_commands.h"
#include "constants/battle_move_effects.h"
#include "constants/pokemon.h"
#include "constants/abilities.h"
/* ABILITY_MACROS */
#define GET_BATTLER_SIDE(b) ((b)&1)
#define FALSE 0
#define TRUE 1
#define T2_READ_16(p) ((u16)((p)[0]|((p)[1]<<8)))
#define T2_READ_32(p) ((u32)((p)[0]|((p)[1]<<8)|((p)[2]<<16)|((p)[3]<<24)))
#define T2_READ_PTR(p) (jumpTarget)
static const u8 BattleScript_HitFromCritCalc[1]={1}, BattleScript_AlreadyAtFullHp[1]={2}, BattleScript_PresentHealTarget[1]={3};
static const u8 *gBattlescriptCurrInstr,*jumpTarget;
static u8 scriptBuffer[16],gBattlerAttacker,gBattlerTarget,gBattleTerrain,gMoveResultFlags,gBattleCommunication[8],gLastUsedAbility;
static u16 gCurrentMove,gDynamicBasePower,gSideStatuses[2];
static s32 gBattleMoveDamage;
static struct {u16 maxHP,hp;u8 ability;u32 status2;} gBattleMons[4];
static struct {u8 secondaryEffectChance;} gBattleMoves[355];
static struct {u8 multihitMoveEffect,battlerWithAbility;} gBattleScripting;
static u16 nextRandom; static int randomCalls,effectApplied;
static u16 Random(void){randomCalls++;return nextRandom;}
static void SetMoveEffect(int primary,int certain){(void)primary;(void)certain;effectApplied++;}
static u8 GetBattlerForBattleScript(u8 who){return who==BS_TARGET?gBattlerTarget:gBattlerAttacker;}
static u8 AbilityBattleEffects(int a,int b,int c,int d,int e){(void)a;(void)b;(void)c;(void)d;(void)e;return 0;}
static void RecordAbilityBattle(int a,int b){(void)a;(void)b;}
