"""Compile pinned original selection/target bodies and recoil command scalar; no reference scripts run."""
import hashlib,json,pathlib,subprocess,sys
ref,out=map(pathlib.Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=True)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==pin
util=(ref/'src/battle_util.c').read_text();cmd=(ref/'src/battle_script_commands.c').read_text()
main=(ref/'src/battle_main.c').read_text();script=(ref/'data/battle_scripts_1.s').read_text()
assert 'gCurrentMove = gChosenMove = MOVE_STRUGGLE;' in main and 'gHitMarker |= HITMARKER_NO_PPDEDUCT;' in main
assert 'if (gCurrentMove == MOVE_STRUGGLE)\n    {\n        gBattlescriptCurrInstr++;\n        return;\n    }' in cmd
assert 'setmoveeffect MOVE_EFFECT_RECOIL_25 | MOVE_EFFECT_AFFECTS_USER | MOVE_EFFECT_CERTAIN' in script
opponent=(ref/'src/battle_controller_opponent.c').read_text();player=(ref/'src/battle_controller_player.c').read_text()
assert '#define BATTLE_OPPOSITE(id) ((id) ^ 1)' in (ref/'include/constants/battle.h').read_text()
assert 'gBattleCommunication[GetBattlerAtPosition(BATTLE_PARTNER(position))] == STATE_WAIT_ACTION_CONFIRMED' in main
assert 'static void OpponentHandlePrintSelectionString(void)\n{\n    OpponentBufferExecCompleted();\n}' in opponent
assert 'gBattlerControllerFuncs[gActiveBattler] = HandleChooseActionAfterDma3;' in player

def extract(text,signature):
 start=text.index(signature+'\n{');brace=text.index('{',start);n=1;i=brace+1
 while n:n+=(text[i]=='{')-(text[i]=='}');i+=1
 return text[start:i]
functions='\n\n'.join(extract(util,s) for s in ['u8 CheckMoveLimitations(u8 battlerId, u8 unusableMoves, u8 check)','bool8 AreAllMovesUnusable(void)','u8 GetMoveTarget(u16 move, u8 setTarget)'])
recoil=cmd.split('case MOVE_EFFECT_RECOIL_25: // 25% recoil',1)[1].split('BattleScriptPush',1)[0]
head=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int32_t s32;typedef int bool8;
#define MAX_MON_MOVES 4
#define MOVE_NONE 0
#define MOVE_UNAVAILABLE 65535
#define ITEM_ENIGMA_BERRY 175
#define HOLD_EFFECT_CHOICE_BAND 29
#define MOVE_LIMITATION_ZEROMOVE 1
#define MOVE_LIMITATION_PP 2
#define MOVE_LIMITATION_DISABLED 4
#define MOVE_LIMITATION_TORMENTED 8
#define MOVE_LIMITATION_TAUNT 16
#define MOVE_LIMITATION_IMPRISON 32
#define MOVE_LIMITATIONS_ALL 255
#define STATUS2_TORMENT 1
#define ALL_MOVES_MASK 15
#define TRUE 1
#define FALSE 0
#define BATTLE_TYPE_DOUBLE 1
#define BATTLE_OPPOSITE(a) ((a)^1)
#define BIT_SIDE 1
#define BIT_FLANK 2
#define B_SIDE_PLAYER 0
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_LEFT 1
#define B_POSITION_PLAYER_RIGHT 2
#define B_POSITION_OPPONENT_RIGHT 3
#define NO_TARGET_OVERRIDE 0
#define MOVE_TARGET_SELECTED 0
#define MOVE_TARGET_DEPENDS 1
#define MOVE_TARGET_USER_OR_SELECTED 2
#define MOVE_TARGET_RANDOM 4
#define MOVE_TARGET_BOTH 8
#define MOVE_TARGET_USER 16
#define MOVE_TARGET_FOES_AND_ALLY 32
#define MOVE_TARGET_OPPONENTS_FIELD 64
#define TYPE_ELECTRIC 13
#define ABILITYEFFECT_COUNT_OTHER_SIDE 1
#define ABILITY_LIGHTNING_ROD 31
static uint32_t seed;static int draws;
static u16 Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
static struct Mon{int item,moves[4],pp[4],status2,hp,ability;}gBattleMons[4];
static struct Disable{int disabledMove,tauntTimer,encoreTimer,encoredMove;}gDisableStructs[4];
static struct Move{int power,target,type;}gBattleMoves[355];
static struct Battle{u16 choicedMove[4];u8 moveTarget[4];}battle,*gBattleStruct=&battle;
static struct Enigma{int holdEffect;}gEnigmaBerries[4];
static struct Protect{int noValidMoves;}gProtectStructs[4];
static struct Side{int followmeTimer,followmeTarget;}gSideTimers[2];
static struct Special{int lightningRodRedirected;}gSpecialStatuses[4];
static int gPotentialItemEffectBattler,gLastMoves[4],gActiveBattler,gBattlerAttacker,gBattlersCount=4,gAbsentBattlerFlags,gBattleTypeFlags=BATTLE_TYPE_DOUBLE;
static int gBitTable[4]={1,2,4,8};static u8 gBattleBufferB[4][4],scriptByte,*BattleScript_NoMovesLeft=&scriptByte,*gSelectionBattleScripts[4];
static int ItemId_GetHoldEffect(int item){(void)item;return 0;}
static int GetImprisonedMovesCount(int battler,int move){(void)battler;(void)move;return 0;}
static int GetBattlerAtPosition(int battler){return battler;}
static int GetBattlerPosition(int battler){return battler;}
static int GetBattlerSide(int battler){return battler&1;}
static int AbilityBattleEffects(int c,int a,int b,int d,int e){(void)c;(void)a;(void)b;(void)d;(void)e;return 0;}
static void RecordAbilityBattle(int a,int b){(void)a;(void)b;}
'''
runner=r'''
int main(void){
 printf("{\"targets\":[");int count=0;gBattleMoves[165].power=50;
 for(int actor=0;actor<4;actor++)for(int mode=0;mode<3;mode++)for(int s=0;s<16;s++){
  memset(gSideTimers,0,sizeof(gSideTimers));memset(gBattleMons,0,sizeof(gBattleMons));
  gActiveBattler=gBattlerAttacker=actor;int opponent=(actor&1)^1;
  for(int i=0;i<4;i++)gBattleMons[i].hp=100;
  gAbsentBattlerFlags=mode==1?(1<<opponent):0;
  if(mode==2){gSideTimers[opponent].followmeTimer=1;gSideTimers[opponent].followmeTarget=opponent;}
  seed=s;draws=0;int required=AreAllMovesUnusable(),selection=gBattleBufferB[actor][3];int target=GetMoveTarget(165,0);
  printf("%s{\"actor\":%d,\"mode\":%d,\"seed\":%d,\"required\":%d,\"selection\":%d,\"target\":%d,\"finalSeed\":%u,\"draws\":%d}",count++?",":"",actor,mode,s,required,selection,target,seed,draws);
 }
 printf("],\"limits\":[");count=0;
 for(int mask=0;mask<16;mask++)for(int disabled=0;disabled<5;disabled++){
  gActiveBattler=0;memset(gDisableStructs,0,sizeof(gDisableStructs));gDisableStructs[0].disabledMove=disabled;
  for(int i=0;i<4;i++){gBattleMons[0].moves[i]=i+1;gBattleMons[0].pp[i]=(mask&(1<<i))?0:10;}
  printf("%s{\"mask\":%d,\"disabled\":%d,\"required\":%d}",count++?",":"",mask,disabled,AreAllMovesUnusable());
 }
 printf("],\"schedule\":[");count=0;
 for(int s=0;s<16;s++){
  int order[4]={1,3,0,2};memset(gBattleMons,0,sizeof(gBattleMons));memset(gDisableStructs,0,sizeof(gDisableStructs));seed=s;draws=0;
  printf("%s{\"seed\":%d,\"targets\":[",count++?",":"",s);
  for(int i=0;i<4;i++){gActiveBattler=order[i];AreAllMovesUnusable();printf("%s%d",i?",":"",gBattleBufferB[gActiveBattler][3]);}
  printf("],\"finalSeed\":%u,\"draws\":%d}",seed,draws);
 }
 printf("],\"recoil\":[");
 for(int lost=0;lost<256;lost++){int gHpDealt=lost,gBattleMoveDamage=0;
 RECOIL_COMMAND
 printf("%s%d",lost?",":"",gBattleMoveDamage);}
 printf("]}\n");return 0;
}
'''.replace('RECOIL_COMMAND',recoil)
(out/'oracle.c').write_text(head+functions+runner)
subprocess.run(['cc','-std=c99','-Wall','-Wextra','-O0',str(out/'oracle.c'),'-o',str(out/'oracle')],check=True,capture_output=True,text=True)
v=json.loads(subprocess.check_output([str(out/'oracle')],text=True));v['sourceCommit']=pin
v['sourceFiles']={p:hashlib.sha256((ref/p).read_bytes()).hexdigest() for p in ['src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','src/pokemon.c','data/battle_scripts_1.s','src/data/battle_moves.h','include/constants/battle.h','src/battle_controller_opponent.c','src/battle_controller_player.c']}
(out/'source-vectors.json').write_text(json.dumps(v,indent=2)+'\n')
print(json.dumps({'pin':pin,'targets':len(v['targets']),'limits':len(v['limits']),'recoil':len(v['recoil']),'schedule':len(v['schedule'])}))
