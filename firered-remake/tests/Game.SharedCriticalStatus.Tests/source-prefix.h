/* Authored host declarations and observable stubs. The Tri Attack and thaw command branches
 * and chance command are extracted unchanged from the authenticated source. */
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "constants/battle.h"
#include "constants/battle_move_effects.h"
#include "constants/battle_script_commands.h"
#include "constants/pokemon.h"
#include "constants/moves.h"
#include "constants/abilities.h"
#include "constants/battle_string_ids.h"
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef u8 bool8; typedef u32 bool32;
#define TRUE 1
#define FALSE 0
#define BUFFER_A 0
#define REQUEST_STATUS_BATTLE 40
#define WEATHER_HAS_EFFECT weatherHasEffect
#define GET_BATTLER_SIDE(b) ((b)&1)
#define GetBattlerSide(b) GET_BATTLER_SIDE(b)
#define IS_BATTLER_OF_TYPE(b,t) (gBattleMons[b].type1==(t) || gBattleMons[b].type2==(t))
#define INCREMENT_RETURN { gBattlescriptCurrInstr++; return; }
struct MonHost { u32 status1,status2; u16 hp; u8 ability,type1,type2; };
static struct MonHost gBattleMons[4];
static struct { u8 secondaryEffectChance; } gBattleMoves[1];
static struct { u8 battler,multihitMoveEffect,moveendState; } gBattleScripting;
static struct { u8 synchronizeMoveEffect; } battleStruct;
#define gBattleStruct (&battleStruct)
static u8 gBattleCommunication[16],gBattlerAttacker,gBattlerTarget,gEffectBattler,gActiveBattler,gCurrentMove,gMoveResultFlags;
static u32 gBattleTypeFlags,gHitMarker,gSideStatuses[2];
static u16 gBattleWeather;
static bool8 weatherHasEffect;
static u8 script[128],statusScript[128];
static const u8 *gBattlescriptCurrInstr;
static const u8 *sMoveEffectBS_Ptrs[8]={[MOVE_EFFECT_POISON]=statusScript,[MOVE_EFFECT_TOXIC]=statusScript,[MOVE_EFFECT_FREEZE]=statusScript,[MOVE_EFFECT_BURN]=statusScript,[MOVE_EFFECT_PARALYSIS]=statusScript};
static const u8 *BattleScript_PSNPrevention=statusScript,*BattleScript_BRNPrevention=statusScript,*BattleScript_PRLZPrevention=statusScript,*BattleScript_DefrostedViaFireMove=statusScript;
static u8 gLastUsedAbility;
static struct { unsigned specialDmg; } gSpecialStatuses[4];
static unsigned thawed;
static u8 moveType;
static unsigned draws,writes,cancels;
static u16 nextRandom,statusRandom;
static u16 Random(void) { return draws++==0?nextRandom:statusRandom; }
static void CancelMultiTurnMoves(u8 battler) { (void)battler;cancels++; }
static void BattleScriptPush(const u8 *p) { (void)p; }
static void BtlController_EmitSetMonData(u8 a,u8 b,u8 c,u8 n,const void *p) { (void)a;(void)b;(void)c;(void)n;(void)p;writes++; }
static void MarkBattlerForControllerExec(u8 battler) { (void)battler; }

static void RecordAbilityBattle(u8 b,u8 a){(void)b;(void)a;}
static void BattleScriptPushCursor(void){thawed++;}
