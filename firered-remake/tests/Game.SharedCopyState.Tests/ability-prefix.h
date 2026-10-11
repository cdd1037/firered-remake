/* Authored, bounded register shim; the source commands below are copied unchanged. */
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;
#define T1_READ_PTR(address) failedScript
struct { u8 ability; } gBattleMons[4];
u8 gBattlerAttacker, gBattlerTarget, gLastUsedAbility, gMoveResultFlags;
u8 script[8], failedScript[8], *gBattlescriptCurrInstr;
