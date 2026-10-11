#include <stdio.h>
#include <stdint.h>
typedef uint8_t u8;
#define BIT_SIDE 1
#define SIDE_STATUS_REFLECT 1
#define SIDE_STATUS_LIGHTSCREEN 2
struct Protect { int physicalDmg, physicalBattlerId, specialDmg, specialBattlerId; } gProtectStructs[4];
struct Timer { int reflectTimer, lightscreenTimer; } gSideTimers[2];
struct Script { int dmgMultiplier, animTurn, animTargetsHit; } gBattleScripting;
u8 gBattlerAttacker, gBattlerTarget;
int gSideStatuses[2];
static u8 stream[100];
u8 *gBattlescriptCurrInstr=stream;
static u8 GetBattlerSide(u8 b) { return b & BIT_SIDE; }
