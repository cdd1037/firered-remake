/* Host plumbing only. Source constants, structures and C excerpts are inserted
 * verbatim by source_probe.py. No game/reference build script is executed. */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef int8_t s8; typedef uint16_t u16;
typedef uint32_t u32; typedef int32_t s32;
typedef u8 bool8; typedef u32 bool32;
#define TRUE 1
#define FALSE 0
/* SOURCE_CONSTANTS */
/* SOURCE_STRUCTURES */
struct Mon { u16 hp, maxHP, item; u32 status2; u8 ability; s8 statStages[8]; } gBattleMons[4];
struct DisableStruct gDisableStructs[4];
struct SpecialStatus gSpecialStatuses[4];
struct Protect { s32 physicalDmg, specialDmg; u8 physicalBattlerId, specialBattlerId, confusionSelfDmg; } gProtectStructs[4];
struct Side { u8 mistTimer; } gSideTimers[2];
struct Berry { u8 holdEffect, holdEffectParam; } gEnigmaBerries[4];
struct Battle { u8 dynamicMoveType, wrappedBy[4]; } battle, *gBattleStruct = &battle;
struct Scripting { u8 battler, moveendState, multihitMoveEffect; } gBattleScripting;
struct Move { u8 effect, type, flags, secondaryEffectChance; } gBattleMoves[400];
u8 gBattlerAttacker, gBattlerTarget, gActiveBattler, gEffectBattler, gBattlersCount;
u8 gMoveResultFlags, gLastUsedAbility, gPotentialItemEffectBattler, gBattleCommunication[8];
u8 gActionSelectionCursor[4], gMoveSelectionCursor[4], gTakenDmgByBattler[4];
u8 gBattleTextBuff1[32], gBattleTextBuff2[32];
u16 gCurrentMove, gLastUsedItem;
u32 gHitMarker, gBattleControllerExecFlags, gBattleTypeFlags, gSideStatuses[2], gStatuses3[4], gTakenDmg[4];
const u32 gBitTable[4] = {1,2,4,8};
s32 gBattleMoveDamage, gHpDealt;
static u8 command[32];
static const u8 BattleScript_SubstituteFade[1], BattleScript_ItemHealHP_Ret[1], BattleScript_RoughSkinActivates[1];
static const u8 BattleScript_MistProtected[1], BattleScript_ButItFailed[1], BattleScript_AbilityNoStatLoss[1], BattleScript_AbilityNoSpecificStatLoss[1];
const u8 *gBattlescriptCurrInstr, *savedCursor;
static unsigned gatePassed, gateCalls, rngCount, draw, pushCount, fadeCount, alreadyHas, statResult, itemEffect, roughEffect;
static u8 GetBattlerSide(u8 b) { return b & 1; }
static u8 GetBattlerPosition(u8 b) { return b; }
static u8 GetBattlerForBattleScript(u8 b) { return b == BS_TARGET ? gBattlerTarget : gBattlerAttacker; }
static void BattleScriptPushCursor(void) { savedCursor = gBattlescriptCurrInstr; pushCount++; }
static void BattleScriptPop(void) { gBattlescriptCurrInstr = savedCursor; }
static void BattleScriptPush(const u8 *p) { savedCursor = p; pushCount++; }
static u16 Random(void) { rngCount++; return draw; }
static void RecordAbilityBattle(u8 b, u8 a) { (void)b; (void)a; }
static bool8 JumpIfMoveAffectedByProtect(u8 b) { (void)b; return FALSE; }
static void BtlController_EmitSetMonData(u8 a, u8 b, u8 c, unsigned n, const void *p) { (void)a; (void)b; (void)c; (void)n; (void)p; }
static void MarkBattlerForControllerExec(u8 b) { (void)b; }
#define BUFFER_A 0
#define REQUEST_HP_BATTLE 0
#define PREPARE_STAT_BUFFER(buffer, stat) ((void)(buffer), (void)(stat));
#define INCREMENT_RETURN { gBattlescriptCurrInstr++; return; }
/* SOURCE_ITEM_TABLE */
static u8 ItemId_GetHoldEffect(u16 item) { return sourceItems[item].effect; }
static u8 ItemId_GetHoldEffectParam(u16 item) { return sourceItems[item].parameter; }
