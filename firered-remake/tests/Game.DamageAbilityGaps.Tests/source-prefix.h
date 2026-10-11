/* Reconstructed controlled dependency harness, 2026-10-11.
 * The original missing harness bytes/hashes are not recovered. The separately
 * extracted CalculateBaseDamage text is the exact pinned pret implementation.
 * These stubs model level-37 controlled stat registers, singles damage scaling,
 * no weather/sport/ally/Flash Fire effects, and the listed synthetic item IDs.
 * Badge inputs are independent arithmetic switches: GetBattlerSide deliberately
 * returns PLAYER for either numeric register. This is NOT a real battle layout
 * and does not test badge acquisition, battler identity or species admission.
 */
#include <stdint.h>
#include <stdio.h>
#include <string.h>

typedef uint8_t u8;
typedef uint16_t u16;
typedef uint32_t u32;
typedef int32_t s32;
#define ARRAY_COUNT(array) (sizeof(array) / sizeof((array)[0]))

#define TYPE_NORMAL 0
#define TYPE_ROCK 5
#define TYPE_BUG 6
#define TYPE_MYSTERY 9
#define TYPE_FIRE 10
#define TYPE_WATER 11
#define TYPE_GRASS 12
#define TYPE_ELECTRIC 13
#define TYPE_ICE 15
#define IS_TYPE_PHYSICAL(type) ((type) < TYPE_MYSTERY)
#define IS_TYPE_SPECIAL(type) ((type) > TYPE_MYSTERY)
#define DYNAMIC_TYPE_MASK 63
#define STAT_ATK 1
#define STAT_DEF 2
#define STAT_SPATK 4
#define STAT_SPDEF 5
#define DEFAULT_STAT_STAGE 6

#define B_SIDE_PLAYER 0
#define BATTLE_TYPE_DOUBLE (1u << 0)
#define BATTLE_TYPE_LINK (1u << 1)
#define BATTLE_TYPE_BATTLE_TOWER (1u << 8)
#define BATTLE_TYPE_EREADER_TRAINER (1u << 11)
#define BATTLE_ALIVE_DEF_SIDE 2
#define STATUS1_BURN (1u << 4)
#define SIDE_STATUS_REFLECT (1u << 0)
#define SIDE_STATUS_LIGHTSCREEN (1u << 1)
#define B_WEATHER_RAIN_TEMPORARY (1u << 0)
#define B_WEATHER_RAIN 7u
#define B_WEATHER_SANDSTORM ((1u << 3) | (1u << 4))
#define B_WEATHER_SUN ((1u << 5) | (1u << 6))
#define B_WEATHER_HAIL_TEMPORARY (1u << 7)
#define WEATHER_HAS_EFFECT2 1
#define ABILITY_ON_FIELD2(ability) 0
#define ABILITYEFFECT_FIELD_SPORT 14
#define ABILITYEFFECT_MUD_SPORT 253
#define ABILITYEFFECT_WATER_SPORT 254
#define RESOURCE_FLAG_FLASH_FIRE 1
#define MOVE_SOLAR_BEAM 76
#define EFFECT_EXPLOSION 7

/* Synthetic item transport IDs used by source_probe.py, NOT source item IDs.
 * Choice's extractor inserts ITEM_CHOICE_BAND before ITEM_ENIGMA_BERRY (ID 4).
 */
enum { ITEM_NONE, ITEM_HARD_STONE, ITEM_THICK_CLUB, ITEM_METAL_POWDER, ITEM_ENIGMA_BERRY };
#define HOLD_EFFECT_CHOICE_BAND 29
#define HOLD_EFFECT_SOUL_DEW 34
#define HOLD_EFFECT_DEEP_SEA_TOOTH 35
#define HOLD_EFFECT_DEEP_SEA_SCALE 36
#define HOLD_EFFECT_LIGHT_BALL 45
#define HOLD_EFFECT_ROCK_POWER 47
#define HOLD_EFFECT_METAL_POWDER 64
#define HOLD_EFFECT_THICK_CLUB 65
#define SPECIES_PIKACHU 25
#define SPECIES_CUBONE 104
#define SPECIES_MAROWAK 105
#define SPECIES_DITTO 132
#define SPECIES_CLAMPERL 373
#define SPECIES_LATIAS 407
#define SPECIES_LATIOS 408

/* Synthetic flag transport IDs. Badge 7 is always off in this fixture. */
#define FLAG_BADGE01_GET 1
#define FLAG_BADGE05_GET 5
#define FLAG_BADGE07_GET 7
static u8 badgeAttack, badgeDefense;
static u8 FlagGet(u16 flag)
{
    return flag == FLAG_BADGE01_GET ? badgeAttack : flag == FLAG_BADGE05_GET ? badgeDefense : 0;
}
static u8 GetBattlerSide(u8 battler)
{
    (void)battler;
    return B_SIDE_PLAYER;
}
static u8 ItemId_GetHoldEffect(u16 item)
{
    return item == ITEM_HARD_STONE ? HOLD_EFFECT_ROCK_POWER :
        item == ITEM_THICK_CLUB ? HOLD_EFFECT_THICK_CLUB :
        item == ITEM_METAL_POWDER ? HOLD_EFFECT_METAL_POWDER : 0;
}
static u8 ItemId_GetHoldEffectParam(u16 item)
{
    return item == ITEM_HARD_STONE ? 10 : 0;
}
static u8 AbilityBattleEffects(u8 caseId, u8 battler, u8 ability, u8 special, u16 move)
{
    (void)caseId; (void)battler; (void)ability; (void)special; (void)move;
    return 0;
}
static u8 CountAliveMonsInBattle(u8 caseId)
{
    (void)caseId;
    return 1;
}

/* Only fields read by CalculateBaseDamage are represented; this is not a ROM ABI. */
struct BattlePokemon {
    u16 species, attack, defense, spAttack, spDefense, hp, maxHP, item;
    u8 level, ability, statStages[8];
    u32 status1;
};
struct HarnessMove { u8 effect, power, type, target; };
static struct HarnessMove gBattleMoves[2];
struct HarnessBerry { u8 holdEffect, holdEffectParam; };
static struct HarnessBerry gEnigmaBerries[2];
struct HarnessResourceFlags { u8 flags[2]; };
struct HarnessBattleResources { struct HarnessResourceFlags *flags; };
static struct HarnessResourceFlags resourceFlags;
static struct HarnessBattleResources resources = { &resourceFlags };
static struct HarnessBattleResources *gBattleResources = &resources;
static u32 gBattleTypeFlags, gBattleWeather;
static u16 gBattleMovePower, gCurrentMove;
static u8 gCritMultiplier;
#define MOVE_TARGET_BOTH 8
static const u8 gStatStageRatios[13][2] = {
    {10,40}, {10,35}, {10,30}, {10,25}, {10,20}, {10,15}, {10,10},
    {15,10}, {20,10}, {25,10}, {30,10}, {35,10}, {40,10}
};
/* Bounded subset: the only supported type-boosting item is Hard Stone. */
static const u8 sHoldEffectToType[][2] = { { HOLD_EFFECT_ROCK_POWER, TYPE_ROCK } };
