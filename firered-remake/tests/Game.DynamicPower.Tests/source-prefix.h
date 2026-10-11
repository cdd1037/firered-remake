/* Bounded host declarations, not a ROM ABI. Only the unchanged extracted
 * commands, tables and HP helper determine dynamic power. Weight lookup returns
 * an explicit synthetic hectogram register; species conversion is an identity
 * stub. CalculateBaseDamage executes only its unchanged power-selection branch
 * and returns that selected power so command latch lifetime can be observed.
 * This harness does not claim complete damage, targeting, species or battle
 * execution. Critical/random probes have no held effects, abilities or tutorial.
 */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "constants/battle.h"
#include "constants/battle_move_effects.h"
#include "constants/battle_script_commands.h"
#include "constants/pokemon.h"
#include "constants/moves.h"
#include "constants/abilities.h"
#include "constants/species.h"
#include "constants/items.h"
#include "constants/hold_effects.h"
typedef uint8_t u8; typedef int8_t s8; typedef uint16_t u16;
typedef int16_t s16; typedef uint32_t u32; typedef int32_t s32;
#define ARRAY_COUNT(a) (sizeof(a) / sizeof((a)[0]))
/* SOURCE_BATTLE_MACROS */
struct BattlePokemon { u16 hp, maxHP, species, item; u8 friendship, ability; u32 status2; };
struct MoveHost { u8 effect, power, type, accuracy, pp, secondaryEffectChance, target; s8 priority; u32 flags; };
static const struct MoveHost gBattleMoves[355];
static struct BattlePokemon gBattleMons[4];
static struct { u8 holdEffect; } gEnigmaBerries[4];
static struct { u8 helpingHand; } gProtectStructs[4];
static struct { u8 dmgMultiplier; } gBattleScripting;
static struct { u8 dynamicMoveType; } battleStruct, *gBattleStruct = &battleStruct;
static u16 gCurrentMove, gDynamicBasePower, gBattleMovePower, gSideStatuses[2];
static u8 gBattlerAttacker, gBattlerTarget, gCritMultiplier, gPotentialItemEffectBattler;
static u8 gMoveResultFlags, gBattleCommunication[16];
static u32 gStatuses3[4], gBattleTypeFlags, gHitMarker;
static s32 gBattleMoveDamage;
static u8 script[32];
static const u8 *gBattlescriptCurrInstr;
static u16 weightRegister, randomRegister;
static unsigned randomCalls, weightCalls;
static u8 GetBattlerPosition(u8 battler) { return battler; }
static u16 SpeciesToNationalPokedexNum(u16 species) { return species; }
static u16 GetPokedexHeightWeight(u16 species, u8 isWeight)
{
    if (species != gBattleMons[gBattlerTarget].species || isWeight != 1) abort();
    weightCalls++;
    return weightRegister;
}
static u16 Random(void) { randomCalls++; return randomRegister; }
static u8 ItemId_GetHoldEffect(u16 item) { if (item != ITEM_NONE) abort(); return 0; }
static u8 BtlCtrl_OakOldMan_TestState2Flag(u8 flag) { (void)flag; return 0; }
static s32 CalculateBaseDamage(struct BattlePokemon *attacker, struct BattlePokemon *defender,
    u32 move, u16 sideStatus, u16 powerOverride, u8 typeOverride, u8 battlerIdAtk, u8 battlerIdDef)
{
    (void)attacker; (void)defender; (void)sideStatus; (void)typeOverride;
    (void)battlerIdAtk; (void)battlerIdDef;
    /* SOURCE_POWER_SELECTION */
    return gBattleMovePower;
}
