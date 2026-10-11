#include <stdio.h>
#include <string.h>
#include "constants/moves.h"
#include "constants/abilities.h"
typedef unsigned char u8; typedef unsigned short u16; typedef unsigned u32; typedef int s32;
#define MAX_MON_MOVES 4
#define B_SIDE_PLAYER 0
#define MON_DATA_SPECIES 0
#define MON_DATA_PERSONALITY 1
#define MON_GENDERLESS 255
#define STATUS2_INFATUATION 0xF0000
#define STATUS2_INFATUATED_WITH(n) (1u << ((n)+16))
#define STATUS2_TORMENT 4
#define STATUS2_TRANSFORMED 8
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 0
#define STR_CONV_MODE_LEFT_ALIGN 0
#define T1_READ_PTR(p) failure
#define PREPARE_MOVE_BUFFER(a,b)
#define PREPARE_BYTE_NUMBER_BUFFER(a,b,c)
struct Pokemon {u16 species;u32 personality;} gPlayerParty[6],gEnemyParty[6];
struct Mon {u16 moves[4];u8 pp[4],ability;u32 status2;} gBattleMons[4];
struct Disable {u8 tauntTimer,tauntTimer2,mimickedMoves;} gDisableStructs[4];
u16 gLastMoves[4],roll;u8 gBattlerTarget=1,gBattlerAttacker=0,gActiveBattler,gLastUsedAbility,gBattlerPartyIndexes[4]={0,0,1,1};
u8 instructions[16],bad[1],oblivious[1],*gBattlescriptCurrInstr,*failure=bad,*BattleScript_ObliviousPreventsAttraction=oblivious;
u8 gBattleTextBuff1[32],gBattleTextBuff2[32];unsigned gBitTable[]={1,2,4,8};int draws,updates,cancels,abilities;
u16 Random(void){draws++;return roll;}
u8 GetBattlerSide(u8 n){return n&1;}
u32 GetMonData(struct Pokemon*m,int field){return field==MON_DATA_SPECIES?m->species:m->personality;}
u8 GetGenderFromSpeciesAndPersonality(u16 species,u32 personality){return personality;}
void RecordAbilityBattle(int a,int b){abilities++;}
void ConvertIntToDecimalStringN(void*a,int b,int c,int d){}
void BtlController_EmitSetMonData(int a,int b,int c,int d,void*e){updates++;}
void MarkBattlerForControllerExec(int a){}
void CancelMultiTurnMoves(int a){cancels++;}
void reset(void){memset(gBattleMons,0,sizeof(gBattleMons));memset(gDisableStructs,0,sizeof(gDisableStructs));memset(gLastMoves,0,sizeof(gLastMoves));memset(gPlayerParty,0,sizeof(gPlayerParty));memset(gEnemyParty,0,sizeof(gEnemyParty));gBattlescriptCurrInstr=instructions;draws=updates=cancels=abilities=0;roll=0;gBattlerAttacker=0;gBattlerTarget=1;}
