/* Host declarations and observable presentation stubs; mechanics are extracted verbatim. */
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
#define MOVE_TARGET_DEPENDS 1
#define MOVE_TARGET_USER_OR_SELECTED 2
#define MOVE_TARGET_RANDOM 4
#define MOVE_TARGET_BOTH 8
#define MOVE_TARGET_USER 16
#define MOVE_TARGET_FOES_AND_ALLY 32
#define MOVE_TARGET_OPPONENTS_FIELD 64
#define MOVE_IS_PERMANENT(battler,position) ((void)(battler),(void)(position),1)
#define ABILITYEFFECT_COUNT_OTHER_SIDE 16
#define ABILITYEFFECT_COUNT_ON_FIELD 18
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 9
struct MonHost { u32 status1,status2; u8 ability,pp[4]; };
struct DisableHost { u8 rolloutTimer,furyCutterCounter,rechargeTimer; };
struct ProtectHost { u8 notFirstStrike,prlzImmobility,targetNotAffected,usedImprisonedMove,loveImmobility,usedDisabledMove,usedTauntedMove,flag2Unknown,flinchImmobility,confusionSelfDmg; };
struct SpecialHost { u8 ppNotAffectedByPressure; };
struct MoveHost { u8 effect,power,type,accuracy,pp,secondaryEffectChance,target; s8 priority; u32 flags; };
static struct MonHost gBattleMons[4];
static struct DisableHost gDisableStructs[4];
static struct ProtectHost gProtectStructs[4];
static struct SpecialHost gSpecialStatuses[4];
static struct { u8 battler,multihitMoveEffect; } gBattleScripting;
static struct { u8 turnEffectsTracker; } battleStruct, *gBattleStruct=&battleStruct;
static u8 gBattlerAttacker,gBattlerTarget,gEffectBattler,gActiveBattler,gCurrMovePos,gMoveResultFlags,gAbsentBattlerFlags;
static u8 gBattleCommunication[16];
static u16 gCurrentMove,gLockedMoves[4];
static u32 gStatuses3[4],gHitMarker,gBattleControllerExecFlags,gRngValue;
static const u32 gBitTable[4]={1,2,4,8};
static u8 script[128],effectScript[128],BattleScript_ThrashConfuses[1];
static const u8 *gBattlescriptCurrInstr;
static const u8 *sMoveEffectBS_Ptrs[64]={[MOVE_EFFECT_CONFUSION]=effectScript};
static unsigned ppWrites,announcements;
static u8 GetBattlerSide(u8 battler){return battler&1;}
static u8 GetBattlerPosition(u8 battler){return battler;}
static u8 GetBattlerAtPosition(u8 position){return position;}
static void BtlController_EmitSetMonData(u8 a,u8 b,u8 c,u8 n,const void *p){(void)a;(void)b;(void)c;(void)n;(void)p;ppWrites++;}
static void MarkBattlerForControllerExec(u8 battler){(void)battler;}
static u8 AbilityBattleEffects(u8 a,u8 b,u8 c,u8 d,u16 e){(void)a;(void)b;(void)c;(void)d;(void)e;return 0;}
static void BattleScriptPush(const u8 *p){(void)p;}
static void BattleScriptExecute(const u8 *p){(void)p;announcements++;}
static void Reset(u32 seed){
 memset(gBattleMons,0,sizeof(gBattleMons));memset(gDisableStructs,0,sizeof(gDisableStructs));memset(gProtectStructs,0,sizeof(gProtectStructs));
 memset(gSpecialStatuses,0,sizeof(gSpecialStatuses));memset(gStatuses3,0,sizeof(gStatuses3));memset(gLockedMoves,0,sizeof(gLockedMoves));memset(gBattleCommunication,0,sizeof(gBattleCommunication));
 gBattlerAttacker=0;gBattlerTarget=1;gEffectBattler=0;gActiveBattler=0;gCurrMovePos=0;gRngValue=seed;
 gMoveResultFlags=0;gAbsentBattlerFlags=0;gHitMarker=0;gBattleControllerExecFlags=0;gBattlescriptCurrInstr=script;ppWrites=0;announcements=0;
}
