import hashlib,json,pathlib,subprocess,sys
ref,out=map(pathlib.Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=True)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==pin
src=(ref/'src/battle_script_commands.c').read_text();main=(ref/'src/battle_main.c').read_text()
def extract(text,name,decl='static void ',arg='void'):
 start=text.index(decl+name+'('+arg+')\n{');brace=text.index('{',start);n=1;i=brace+1
 while n:
  n+=(text[i]=='{')-(text[i]=='}');i+=1
 return text[start:i]
cmd=extract(src,'Cmd_setprotectlike')
assert 'sProtectSuccessRates[] = {USHRT_MAX, USHRT_MAX / 2, USHRT_MAX / 4, USHRT_MAX / 8}' in src
cleanup=extract(main,'TurnValuesCleanUp',arg='bool8 var0')
assert cleanup.index('if (var0)')<cleanup.index('.protected = FALSE')<cleanup.index('else')
assert '.endured = FALSE' in cleanup and 'protectUses' not in cleanup
turn=extract(main,'BattleTurnPassed',decl='void ')
assert turn.index('TurnValuesCleanUp(TRUE)')<turn.index('DoFieldEndTurnEffects()')<turn.index('HandleWishPerishSongOnTurnEnd()')<turn.index('TurnValuesCleanUp(FALSE)')
head='''#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <limits.h>
typedef uint8_t bool8; typedef uint16_t u16;
#define TRUE 1
#define FALSE 0
#define MOVE_PROTECT 182
#define MOVE_DETECT 197
#define MOVE_ENDURE 203
#define EFFECT_PROTECT 1
#define EFFECT_ENDURE 2
#define MULTISTRING_CHOOSER 0
#define B_MSG_PROTECTED_ITSELF 1
#define B_MSG_BRACED_ITSELF 2
#define B_MSG_PROTECT_FAILED 3
#define MOVE_RESULT_MISSED 1
static uint32_t seed;static int draws;
static uint16_t Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
struct Disable{uint8_t protectUses;}gDisableStructs[4];
struct Protect{int protected,endured;}gProtectStructs[4];
struct Move{int effect;}gBattleMoves[355];
static uint16_t sProtectSuccessRates[]={USHRT_MAX,USHRT_MAX/2,USHRT_MAX/4,USHRT_MAX/8};
static int gBattlerAttacker,gCurrentTurnActionNumber,gBattlersCount=4,gLastResultingMoves[4],gCurrentMove,gBattleCommunication[4],gMoveResultFlags;
static unsigned char script[5],*gBattlescriptCurrInstr=script;
'''
body='''
int main(int argc,char**argv){
 if(argc!=6)return 2;
 seed=(uint32_t)strtoul(argv[1],0,10);gDisableStructs[0].protectUses=atoi(argv[2]);
 gCurrentTurnActionNumber=atoi(argv[3]);gLastResultingMoves[0]=atoi(argv[4]);gCurrentMove=atoi(argv[5]);
 gBattleMoves[182].effect=gBattleMoves[197].effect=EFFECT_PROTECT;gBattleMoves[203].effect=EFFECT_ENDURE;
 Cmd_setprotectlike();printf("{\\\"after\\\":%u,\\\"protected\\\":%d,\\\"enduring\\\":%d,\\\"missed\\\":%d,\\\"seedAfter\\\":%u,\\\"draws\\\":%d}",gDisableStructs[0].protectUses,gProtectStructs[0].protected,gProtectStructs[0].endured,gMoveResultFlags,seed,draws);return 0;
}
'''
(out/'oracle.c').write_text(head+cmd+body)
subprocess.run(['cc','-std=c99','-Wall','-Wextra','-O0',str(out/'oracle.c'),'-o',str(out/'oracle')],check=True,capture_output=True,text=True)
rows=[];inv=pow(1103515245,-1,2**32)
for use in range(4):
 for index in range(4):
  threshold=65535>>use
  for raw in sorted(set([0,threshold,min(65535,threshold+1),65535])):
   for move,previous in [(182,197),(197,203),(203,182),(182,1)]:
    seed=(((raw<<16)-24691)*inv)%2**32
    result=json.loads(subprocess.check_output([str(out/'oracle'),str(seed),str(use),str(index),str(previous),str(move)],text=True))
    rows.append(dict(seed=seed,before=use,index=index,previous=previous,move=move,roll=raw,**result))
damage_head = r'''#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
typedef uint8_t u8; typedef uint16_t u16;
#define ITEM_ENIGMA_BERRY 1
#define HOLD_EFFECT_FOCUS_BAND 1
#define EFFECT_FALSE_SWIPE 3
#define STATUS2_SUBSTITUTE 1
#define MOVE_RESULT_FOE_ENDURED 64
#define MOVE_RESULT_FOE_HUNG_ON 128
static uint32_t seed;static int draws;
static uint16_t Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
struct Mon{int hp,item,status2;}gBattleMons[4];
struct Protect{int endured;}gProtectStructs[4];
struct Special{int focusBanded;}gSpecialStatuses[4];
struct Berry{int holdEffect,holdEffectParam;}gEnigmaBerries[4];
struct Move{int effect;}gBattleMoves[355];
static int gBattlerTarget=1,gCurrentMove=1,gBattleMoveDamage,gPotentialItemEffectBattler,gMoveResultFlags,gLastUsedItem;
static unsigned char script[5],*gBattlescriptCurrInstr=script;
static int ItemId_GetHoldEffect(int item){return 0;}
static int ItemId_GetHoldEffectParam(int item){return 0;}
static void RecordItemEffectBattle(int a,int b){}
'''
damage_main = r'''
int main(int argc,char**argv){
 if(argc!=6)return 2;seed=(uint32_t)strtoul(argv[1],0,10);gBattleMons[1].hp=atoi(argv[2]);gBattleMoveDamage=atoi(argv[3]);gProtectStructs[1].endured=atoi(argv[4]);gBattleMons[1].status2=atoi(argv[5]);
 Cmd_adjustnormaldamage();printf("{\"damageAfter\":%d,\"endured\":%d,\"seedAfter\":%u,\"draws\":%d}",gBattleMoveDamage,!!(gMoveResultFlags&MOVE_RESULT_FOE_ENDURED),seed,draws);return 0;
}
'''
(out/'damage.c').write_text(damage_head+extract(src,'ApplyRandomDmgMultiplier',decl='static inline void ')+extract(src,'Cmd_adjustnormaldamage')+damage_main)
subprocess.run(['cc','-std=c99','-Wall','-Wextra','-O0',str(out/'damage.c'),'-o',str(out/'damage')],check=True,capture_output=True,text=True)
damage_rows=[]
for seed in [0,31,65535]:
 for hp,damage in [(1,1),(1,100),(2,100),(10,9),(10,10),(10,100)]:
  for endure in [0,1]:
   result=json.loads(subprocess.check_output([str(out/'damage'),str(seed),str(hp),str(damage),str(endure),'0'],text=True))
   damage_rows.append(dict(seed=seed,hp=hp,damage=damage,endure=endure,**result))
result=dict(sourceCommit=pin,damageVectors=damage_rows,vectors=rows,cleanupBeforeResidual=True,cleanupBeforeDelayed=True,sourceFiles={str(p):hashlib.sha256((ref/p).read_bytes()).hexdigest() for p in ['src/battle_script_commands.c','src/battle_main.c','data/battle_scripts_1.s']})
(out/'source-vectors.json').write_text(json.dumps(result,indent=2)+'\n')
print(f'PASS {len(rows)} verbatim C protection vectors and {len(damage_rows)} normal-damage vectors; cleanup source order; original protection table indexes 0..3 only')
