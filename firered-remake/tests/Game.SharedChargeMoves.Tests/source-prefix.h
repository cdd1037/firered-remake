/* Host declarations and observable stubs only. Mechanics are extracted verbatim. */
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "constants/battle.h"
#include "constants/battle_move_effects.h"
#include "constants/battle_script_commands.h"
#include "constants/pokemon.h"
#include "constants/moves.h"
#include "constants/abilities.h"
typedef uint8_t u8; typedef int8_t s8; typedef uint16_t u16;
typedef uint32_t u32; typedef int32_t s32; typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define MOVE_TARGET_SELECTED 0
#define MOVE_TARGET_DEPENDS (1 << 0)
#define MOVE_TARGET_USER_OR_SELECTED (1 << 1)
#define MOVE_TARGET_RANDOM (1 << 2)
#define MOVE_TARGET_BOTH (1 << 3)
#define MOVE_TARGET_USER (1 << 4)
#define MOVE_TARGET_FOES_AND_ALLY (1 << 5)
#define MOVE_TARGET_OPPONENTS_FIELD (1 << 6)
#define WEATHER_HAS_EFFECT weatherHasEffect
#define MOVE_IS_PERMANENT(battler, position) ((void)(battler), (void)(position), 1)
#define ABILITYEFFECT_COUNT_OTHER_SIDE 16
#define ABILITYEFFECT_COUNT_ON_FIELD 18
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 9
struct MonHost { u32 status1, status2; u8 ability, pp[4]; };
struct DisableHost { u8 rolloutTimer, furyCutterCounter, battlerWithSureHit; };
struct ProtectHost { u8 chargingTurn, notFirstStrike; };
struct SpecialHost { u8 ppNotAffectedByPressure; };
struct MoveHost { u8 effect, power, type, accuracy, pp, secondaryEffectChance, target; s8 priority; u32 flags; };
static struct MonHost gBattleMons[4];
static struct DisableHost gDisableStructs[4];
static struct ProtectHost gProtectStructs[4];
static struct SpecialHost gSpecialStatuses[4];
static struct { u8 multihitMoveEffect; } gBattleScripting;
static u8 gBattlerAttacker, gBattlerTarget, gEffectBattler, gActiveBattler, gCurrMovePos, gMoveResultFlags;
static u8 gBattleCommunication[16];
static u16 gCurrentMove, gLockedMoves[4], gBattleWeather;
static u32 gStatuses3[4], gHitMarker, gBattleControllerExecFlags;
static u8 script[128], failureScript[128];
static const u8 *gBattlescriptCurrInstr;
static bool8 weatherHasEffect = TRUE;
static u8 pressureCount;
static unsigned ppWrites, failedChecks;
static u8 GetBattlerForBattleScript(u8 battler) { (void)battler; return gBattlerAttacker; }
static void BtlController_EmitSetMonData(u8 a,u8 b,u8 c,u8 size,const void *p) { (void)a;(void)b;(void)c;(void)size;(void)p;ppWrites++; }
static void MarkBattlerForControllerExec(u8 battler) { (void)battler; }
static u8 AbilityBattleEffects(u8 which,u8 battler,u8 ability,u8 unused,u16 move) { (void)which;(void)battler;(void)ability;(void)unused;(void)move;return pressureCount; }
static void JumpIfMoveFailed(u8 adder,u16 move) { (void)move;failedChecks++;gBattlescriptCurrInstr = gMoveResultFlags & MOVE_RESULT_NO_EFFECT ? failureScript : gBattlescriptCurrInstr + adder; }
