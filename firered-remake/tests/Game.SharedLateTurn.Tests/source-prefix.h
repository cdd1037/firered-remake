/* Host storage and observable boundary stubs; game mechanics are extracted verbatim. */
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "constants/battle.h"
#include "constants/global.h"
#include "constants/pokemon.h"
#include "constants/moves.h"
#include "constants/abilities.h"
typedef uint8_t u8;
typedef uint16_t u16;
typedef uint32_t u32;
typedef int32_t s32;
typedef u8 bool8;
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 9
#define MOVE_TARGET_SELECTED 0
#define MOVE_IS_PERMANENT(battler, position) ((void)(battler), (void)(position), 1)
#define T1_READ_PTR(address) ((void)(address), failureScript)
struct MonHost { u32 status2; u16 hp, moves[4]; u8 ability, pp[4]; };
struct DisableHost { u8 perishSongTimer, perishSongTimerStartValue, furyCutterCounter; };
static struct MonHost gBattleMons[4];
static struct DisableHost gDisableStructs[4];
static struct { u8 atkCancellerTracker; } battleStruct;
/* A macro avoids defining an upstream ABI whose layout this proof does not test. */
#define gBattleStruct (&battleStruct)
static struct { s32 bideDmg; } gBattleScripting;
static u8 gBattlerAttacker, gBattlerTarget, gActiveBattler, gBattlersCount;
static u8 gBattlerPositions[4], gTakenDmgByBattler[4], gAbsentBattlerFlags;
static u16 gCurrentMove, gLockedMoves[4];
static u32 gStatuses3[4], gHitMarker;
static s32 gTakenDmg[4];
static const u32 gBitTable[4] = {1, 2, 4, 8};
static u8 script[32], failureScript[1];
static const u8 BattleScript_BideStoringEnergy[1] = {1};
static const u8 BattleScript_BideAttack[1] = {2};
static const u8 BattleScript_BideNoEnergyToAttack[1] = {3};
static const u8 *gBattlescriptCurrInstr;
static unsigned ppWrites, retargetCalls;
static u8 fallbackTarget;
enum { CANCELLER_FLAGS, CANCELLER_BIDE };
static void BtlController_EmitSetMonData(u8 a, u8 b, u8 c, u8 size, const void *p)
{ (void)a; (void)b; (void)c; (void)size; (void)p; ppWrites++; }
static void MarkBattlerForControllerExec(u8 battler) { (void)battler; }
static u8 GetMoveTarget(u16 move, u8 target)
{ (void)move; (void)target; retargetCalls++; return fallbackTarget; }
static void Reset(void)
{
    memset(gBattleMons, 0, sizeof(gBattleMons));
    memset(gDisableStructs, 0, sizeof(gDisableStructs));
    memset(gStatuses3, 0, sizeof(gStatuses3));
    memset(gTakenDmg, 0, sizeof(gTakenDmg));
    memset(gTakenDmgByBattler, 0, sizeof(gTakenDmgByBattler));
    memset(gLockedMoves, 0, sizeof(gLockedMoves));
    for (u8 i = 0; i < 4; i++) { gBattlerPositions[i] = i; gBattleMons[i].hp = 100; }
    gBattlerAttacker = 0; gBattlerTarget = 1; gActiveBattler = 0; gBattlersCount = 4;
    gCurrentMove = MOVE_GROWL; gHitMarker = 0; gAbsentBattlerFlags = 0;
    gBattlescriptCurrInstr = script; gBattleStruct->atkCancellerTracker = 0;
    gBattleScripting.bideDmg = -7; ppWrites = 0; retargetCalls = 0; fallbackTarget = 1;
}
