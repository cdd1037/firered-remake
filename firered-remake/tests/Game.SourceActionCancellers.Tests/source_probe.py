from pathlib import Path
import subprocess,json,hashlib,sys
source=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()
assert commit=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
for name in ('src/battle_util.c','include/constants/battle.h','include/constants/abilities.h'):
 assert (source/name).read_bytes()==subprocess.check_output(['git','show',commit+':'+name],cwd=source),name
s=(source/'src/battle_util.c').read_text()
names=['ASLEEP','FROZEN','TRUANT','RECHARGE','FLINCH','CONFUSED','PARALYSED']
blocks=[]
for name in names:
 start=s.index('        case CANCELLER_'+name+':');end=s.index('        case CANCELLER_',start+15);blocks.append(s[start:end])
macros='\n'.join('#define CANCELLER_'+n+' '+str(i) for i,n in enumerate(names))
c='''#include <stdio.h>
#include <stdint.h>
typedef unsigned char u8;
#define STATUS1_SLEEP 7
#define STATUS1_FREEZE 32
#define STATUS1_PARALYSIS 64
#define STATUS2_NIGHTMARE 0x8000000
#define STATUS2_RECHARGE 0x400000
#define STATUS2_FLINCHED 8
#define STATUS2_CONFUSION 7
#define STATUS2_CONFUSION_TURN(x) (x)
#define ABILITY_EARLY_BIRD 48
#define ABILITY_TRUANT 54
#define MOVE_SNORE 173
#define MOVE_SLEEP_TALK 214
#define MOVE_POUND 1
#define EFFECT_THAW_HIT 125
#define HITMARKER_UNABLE_TO_USE_MOVE 1
#define HITMARKER_NO_ATTACKSTRING 2
#define MOVE_RESULT_MISSED 1
#define MULTISTRING_CHOOSER 0
#define B_MSG_WOKE_UP_UPROAR 0
#define B_MSG_WOKE_UP 0
#define B_MSG_DEFROSTED 0
#define B_MSG_LOAFING 0
#define FALSE 0
#define TRUE 1
#define BattleScript_MoveUsedWokeUp 1
#define BattleScript_MoveUsedIsAsleep 2
#define BattleScript_MoveUsedIsFrozen 3
#define BattleScript_MoveUsedUnfroze 4
#define BattleScript_MoveUsedLoafingAround 5
#define BattleScript_MoveUsedMustRecharge 6
#define BattleScript_MoveUsedFlinched 7
#define BattleScript_MoveUsedIsConfused 8
#define BattleScript_MoveUsedIsConfusedNoMore 9
#define BattleScript_MoveUsedIsParalyzed 10
struct { int status1,status2,ability; } gBattleMons[4];
struct { int truantCounter,rechargeTimer; } gDisableStructs[4];
struct { int flinchImmobility,confusionSelfDmg,prlzImmobility; } gProtectStructs[4];
struct { int atkCancellerTracker; } battleStruct, *gBattleStruct=&battleStruct;
struct { int effect; } gBattleMoves[1];
int gBattlerAttacker=2,gBattlerTarget,gCurrentMove,gHitMarker,gMoveResultFlags,gBattleMoveDamage;
int gBattleCommunication[1],gBattlescriptCurrInstr,draws,cancelled;
unsigned seed;
int UproarWakeUpCheck(int n){(void)n;return 0;}
void BattleScriptPushCursor(void){}
void CancelMultiTurnMoves(int n){(void)n;}
int CalculateBaseDamage(void*a,void*b,int c,int d,int e,int f,int g,int h){(void)a;(void)b;(void)c;(void)d;(void)e;(void)f;(void)g;(void)h;return 0;}
int Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
'''+macros+'''
int main(void){int status,conf,ability,loaf,recharge,flinch;unsigned input;
while(scanf("%d%d%d%d%d%d%u",&status,&conf,&ability,&loaf,&recharge,&flinch,&input)==7){
gBattleMons[2].status1=status;gBattleMons[2].status2=conf|(recharge?STATUS2_RECHARGE:0)|(flinch?STATUS2_FLINCHED:0);gBattleMons[2].ability=ability;
gDisableStructs[2].truantCounter=loaf;gDisableStructs[2].rechargeTimer=recharge;
gProtectStructs[2].confusionSelfDmg=0;gHitMarker=0;gBattleCommunication[0]=0;seed=input;draws=0;cancelled=0;gBattleStruct->atkCancellerTracker=0;
while(gBattleStruct->atkCancellerTracker<7){int effect=0;gBattlescriptCurrInstr=0;switch(gBattleStruct->atkCancellerTracker){
'''+''.join(blocks)+'''
}
(void)effect;
if(gBattlescriptCurrInstr==2||gBattlescriptCurrInstr==3||gBattlescriptCurrInstr==5||gBattlescriptCurrInstr==6||gBattlescriptCurrInstr==7||gBattlescriptCurrInstr==10||(gBattlescriptCurrInstr==8&&gProtectStructs[2].confusionSelfDmg)){
cancelled=gBattlescriptCurrInstr;if(cancelled==8)Random();break;}
}
printf("%d %d %d %d %d %d\\n",gBattleMons[2].status1,gBattleMons[2].status2&7,gDisableStructs[2].rechargeTimer,(gBattleMons[2].status2&STATUS2_FLINCHED)!=0,cancelled,draws);
}return 0;}
'''
(out/'canceller-extract.c').write_text(c)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'canceller-extract.c'),'-o',str(out/'canceller-extract')],check=True)
inputs=[(status,conf,ability,loaf,recharge,flinch,seed) for status in (0,1,2,5,32,64) for conf in (0,1,3) for ability,loaf in ((0,0),(48,0),(54,0),(54,1)) for recharge,flinch in ((0,0),(1,0),(0,1),(2,1)) for seed in (0,1,2,3,4,11)]
r=subprocess.run([str(out/'canceller-extract')],input=''.join(' '.join(map(str,x))+'\n' for x in inputs),text=True,capture_output=True,check=True)
rows=[[*x,*map(int,line.split())] for x,line in zip(inputs,r.stdout.splitlines(),strict=True)]
assert len(rows)==1728
(out/'source-cancellers.json').write_text(json.dumps(dict(sourceCommit=commit,extractSha256=hashlib.sha256(''.join(blocks).encode()).hexdigest(),rows=rows)))
print('PASS',len(rows),'original C ordered canceller vectors')
