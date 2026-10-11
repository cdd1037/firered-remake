/* Controlled host-only environment for byte-identical extracted FireRed commands.
 * This is new test infrastructure, not original game code or a ROM emulator.
 * Real scalar widths/constants and SpecialStatus declaration are extracted.
 * Other structs expose only fields read/written by the selected functions.
 * The command pointer decoder deliberately maps the sole KO failure operand to
 * a host sentinel; it does not emulate ARM pointers or script execution.
 */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <limits.h>
typedef uint8_t u8;
typedef uint16_t u16;
typedef uint32_t u32;
typedef int32_t s32;
#define TRUE 1
#define FALSE 0

/* SOURCE_CONSTANTS_AND_SPECIAL_STATUS */

struct Mon { u16 hp, maxHP, item; u32 status2; u8 ability, level; } gBattleMons[4];
struct Berry { u8 holdEffect, holdEffectParam; } gEnigmaBerries[4];
struct Move { u8 effect, accuracy, type; } gBattleMoves[1];
struct Protect { u8 endured; s32 physicalDmg, specialDmg; u8 physicalBattlerId, specialBattlerId; } gProtectStructs[4];
struct Disable { u8 battlerWithSureHit; u16 substituteHP; } gDisableStructs[4];
struct Scripting { u8 dmgMultiplier; } gBattleScripting;
struct Battle { u8 dynamicMoveType; } battle, *gBattleStruct = &battle;
struct SpecialStatus gSpecialStatuses[4];
u8 gBattlerAttacker, gBattlerTarget, gPotentialItemEffectBattler;
u8 gActiveBattler, gBattlersCount = 4, gMoveResultFlags, gLastUsedAbility;
u8 gCritMultiplier, gBattleCommunication[8];
u16 gCurrentMove, gLastUsedItem;
u32 gStatuses3[4], gHitMarker;
s32 gBattleMoveDamage, gHpDealt;
u32 gBattleControllerExecFlags, gTakenDmg[4];
u8 gTakenDmgByBattler[4];
static const u8 command[8] = {0}, koFailure[1] = {0};
static const u8 BattleScript_SturdyPreventsOHKO[1] = {0};
static const u8 BattleScript_SubstituteFade[1] = {0};
const u8 *gBattlescriptCurrInstr;
static u16 suppliedDraws[2], consumedDraws[2];
static unsigned rngCount, itemRecords, abilityRecords, recordBattler, recordEffect;

static u8 ItemId_GetHoldEffect(u16 item) {
    return item == ITEM_FOCUS_BAND ? HOLD_EFFECT_FOCUS_BAND : 0;
}
static u8 ItemId_GetHoldEffectParam(u16 item) {
    return item == ITEM_FOCUS_BAND ? 10 : 0;
}
static u16 Random(void) {
    if (rngCount >= 2) { fputs("RNG stream exhausted\n", stderr); exit(3); }
    consumedDraws[rngCount] = suppliedDraws[rngCount];
    return suppliedDraws[rngCount++];
}
static void RecordItemEffectBattle(u8 battler, u8 effect) {
    itemRecords++; recordBattler = battler; recordEffect = effect;
}
static void RecordAbilityBattle(u8 battler, u8 ability) {
    if (battler != gBattlerTarget || ability != ABILITY_STURDY) exit(4);
    abilityRecords++;
}
static const u8 *ReadKoFailurePointer(const u8 *pointer) {
    if (pointer != command + 1) exit(5);
    return koFailure;
}
#define T1_READ_PTR(pointer) ReadKoFailurePointer(pointer)
/* Controller/script-stack output is irrelevant to HP/flag observation here. */
#define BUFFER_A 0
#define REQUEST_HP_BATTLE 0
static u8 GetBattlerForBattleScript(u8 battler) { if (battler != BS_TARGET) exit(7); return gBattlerTarget; }
static void BattleScriptPushCursor(void) {}
static void BtlController_EmitSetMonData(u8 a, u8 b, u8 c, unsigned d, const void *e) { (void)a; (void)b; (void)c; (void)d; (void)e; }
static void MarkBattlerForControllerExec(u8 battler) { (void)battler; }
