#include <stdio.h>
#include <string.h>
#include "constants/moves.h"
typedef unsigned char u8; typedef unsigned short u16; typedef unsigned u32; typedef int s32; typedef unsigned char bool8;
#define TRUE 1
#define FALSE 0
#define GET_BATTLER_SIDE(b) ((b)&1)
#define T1_READ_PTR(p) failure
#define MULTISTRING_CHOOSER 0
#define B_MSG_FUTURE_SIGHT 0
#define B_MSG_DOOM_DESIRE 1
#define PREPARE_MOVE_BUFFER(a,b)
#define PREPARE_BYTE_NUMBER_BUFFER(a,b,c)
#define PREPARE_MON_NICK_WITH_PREFIX_BUFFER(a,b,c)
#define HITMARKER_GRUDGE 1
#define HITMARKER_SKIP_DMG_TRACK 2
#define STATUS3_PERISH_SONG 4
struct Mon {int hp,maxHP;}gBattleMons[4];
struct {int helpingHand;}gProtectStructs[4];
struct {int futureSightCounter[4],futureSightMove[4],futureSightAttacker[4],futureSightDmg[4],wishCounter[4],wishMonId[4];}gWishFutureKnock;
struct Counters {u8 wishPerishSongState,wishPerishSongBattlerId;}counters,*gBattleStruct=&counters;
struct {int dmg;}gSpecialStatuses[4];struct {int perishSongTimer;}gDisableStructs[4];
u8 memory[32],bad[1],futureScript[1],perishScript[1],countScript[1],*gBattlescriptCurrInstr,*failure=bad;
#define BattleScript_MonTookFutureAttack futureScript
#define BattleScript_PerishSongTakesLife perishScript
#define BattleScript_PerishSongCountGoesDown countScript
int gBattlerAttacker,gBattlerTarget,gActiveBattler,gCurrentMove,gBattleMoveDamage,gAbsentBattlerFlags,gHitMarker,gBattlersCount=4;
int gSideStatuses[2],gBattleCommunication[8],gBattlerPartyIndexes[4]={0,0,1,1},gBattleTextBuff1[8],gBattlerByTurnOrder[4]={3,2,1,0};
int gStatuses3[4],gBitTable[4]={1,2,4,8},callbacks[12],callbackCount,baseCalls,baseArgs[6];
int CalculateBaseDamage(struct Mon*a,struct Mon*b,int move,int side,int power,int type,int attacker,int target){baseCalls++;baseArgs[0]=move;baseArgs[1]=side;baseArgs[2]=power;baseArgs[3]=type;baseArgs[4]=attacker;baseArgs[5]=target;return 101;}
void BattleScriptExecute(const u8 *script){callbacks[callbackCount++]=(script==futureScript?0:10)+gActiveBattler;}
void reset(void){memset(&gWishFutureKnock,0,sizeof gWishFutureKnock);memset(gBattleMons,0,sizeof gBattleMons);memset(gProtectStructs,0,sizeof gProtectStructs);memset(&counters,0,sizeof counters);memset(gDisableStructs,0,sizeof gDisableStructs);memset(gStatuses3,0,sizeof gStatuses3);gBattlescriptCurrInstr=memory;gAbsentBattlerFlags=0;baseCalls=callbackCount=0;gBattlerAttacker=0;gBattlerTarget=1;}
