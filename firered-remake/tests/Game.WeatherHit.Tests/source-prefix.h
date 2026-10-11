#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef unsigned char u8,bool8;typedef signed char s8;typedef uint16_t u16;
#define TRUE 1
#define FALSE 0
#define STATUS3_ALWAYS_HITS 3
#define STATUS3_ALWAYS_HITS_TURN(n) (n)
#define STATUS3_ON_AIR 4
#define STATUS3_UNDERGROUND 8
#define STATUS3_UNDERWATER 16
#define STATUS2_FORESIGHT 1
#define HITMARKER_IGNORE_ON_AIR 1
#define HITMARKER_IGNORE_UNDERGROUND 2
#define HITMARKER_IGNORE_UNDERWATER 4
#define MOVE_RESULT_MISSED 1
#define BATTLE_TYPE_FIRST_BATTLE 1
#define BATTLE_TYPE_POKEDUDE 2
#define BATTLE_TYPE_DOUBLE 4
#define B_SIDE_PLAYER 0
#define NO_ACC_CALC 65535
#define NO_ACC_CALC_CHECK_LOCK_ON 65534
#define ACC_CURR_MOVE 0
#define B_WEATHER_RAIN 1
#define B_WEATHER_SUN 2
#define B_WEATHER_SANDSTORM 4
#define EFFECT_THUNDER 99
#define EFFECT_ALWAYS_HIT 98
#define EFFECT_VITAL_THROW 97
#define STAT_ACC 0
#define STAT_EVASION 1
#define DEFAULT_STAT_STAGE 6
#define MIN_STAT_STAGE 0
#define MAX_STAT_STAGE 12
#define ABILITY_COMPOUND_EYES 14
#define ABILITY_SAND_VEIL 8
#define ABILITY_HUSTLE 55
#define ITEM_ENIGMA_BERRY 175
#define HOLD_EFFECT_EVASION_UP 6
#define MOVE_TARGET_BOTH 8
#define MOVE_TARGET_FOES_AND_ALLY 32
#define MISS_TYPE 0
#define B_MSG_PROTECTED 1
#define B_MSG_AVOIDED_ATK 2
#define B_MSG_MISSED 0
#define WEATHER_HAS_EFFECT (!suppressed)
#define IS_TYPE_PHYSICAL(type) ((type)<9)
#define GET_MOVE_TYPE(move,type) ((type)=gBattleMoves[move].type)
#define T2_READ_16(p) (gCurrentMove)
#define T1_READ_PTR(p) (script+8)
#define DEFENDER_IS_PROTECTED protectedTarget
static struct Mon { int ability,item,statStages[2];uint32_t status2; } gBattleMons[4];
static struct Move {int power,type,effect,accuracy,target;} gBattleMoves[400];
static struct Disable {int battlerWithSureHit;}gDisableStructs[4];
static struct Berry{int holdEffect,holdEffectParam;}gEnigmaBerries[4];
static struct Ratio{int dividend,divisor;}sAccuracyStageRatios[13]=SOURCE_ACCURACY_TABLE;
static uint32_t gStatuses3[4],gHitMarker,seed;
static u8 script[100],*gBattlescriptCurrInstr=script;
static int gBattlerAttacker=2,gBattlerTarget=3,gBattleWeather,gBattleTypeFlags=4,gCurrentMove=1,gPotentialItemEffectBattler,gBattleCommunication[1],gMoveResultFlags,protectedTarget,suppressed,draws;
static u16 Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
static int BtlCtrl_OakOldMan_TestState2Flag(int x){return x;}
static int GetBattlerSide(int x){return x&1;}
static void JumpIfMoveFailed(int n,int move){(void)move;gBattlescriptCurrInstr+=gMoveResultFlags?8:n;}
static int ItemId_GetHoldEffect(int item){return item?HOLD_EFFECT_EVASION_UP:0;}
static int ItemId_GetHoldEffectParam(int item){return item==1?10:5;}
static void CheckWonderGuardAndLevitate(void){}
