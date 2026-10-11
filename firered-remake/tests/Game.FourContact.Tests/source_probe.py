"""Compile the exact Cute Charm and infatuation source branches, with only environment stubs."""
from pathlib import Path
import hashlib,json,subprocess,sys
source,out=map(Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=True)
commit="037335f4c725d7c9aecdac87066f2002b4bd7e14"
assert subprocess.check_output(["git","rev-parse","HEAD"],cwd=source,text=True).strip()==commit
paths=["src/battle_util.c","include/constants/abilities.h","include/constants/pokemon.h","include/constants/battle.h"]
text={};pins={}
for path in paths:
 data=(source/path).read_bytes();assert data==subprocess.check_output(["git","show",commit+":"+path],cwd=source)
 text[path]=data.decode();pins[path]=hashlib.sha256(data).hexdigest()
src=text[paths[0]]
charm=src.split("            case ABILITY_CUTE_CHARM:",1)[1].split("                break;",1)[0]
love=src.split("        case CANCELLER_IN_LOVE:",1)[1].split("        case CANCELLER_BIDE:",1)[0].rsplit("            break;",1)[0]
pre=r'''#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
'''+''.join('#include "'+str(source/p)+'"\n' for p in paths[1:])+r'''
#define TARGET_TURN_DAMAGED (gSpecialStatuses[gBattlerTarget].physicalDmg || gSpecialStatuses[gBattlerTarget].specialDmg)
unsigned gBitTable[4]={1,2,4,8};
struct {int hp,ability,status2;} gBattleMons[4];
struct {int confusionSelfDmg,loveImmobility;}gProtectStructs[4];
struct {int physicalDmg,specialDmg;}gSpecialStatuses[4];
struct {int flags;}gBattleMoves[1];
struct {int battler;}gBattleScripting;
struct {int atkCancellerTracker;}store,*gBattleStruct=&store;
int gBattlerAttacker,gBattlerTarget,gMoveResultFlags,move,speciesAtk,speciesDef,pidAtk,pidDef,effect;
int BattleScript_CuteCharmActivates[1],BattleScript_MoveUsedIsInLoveCantAttack[1],BattleScript_MoveUsedIsInLove[1],*gBattlescriptCurrInstr;
unsigned gHitMarker;unsigned long rng;int draws,cancelled;
int GetGenderFromSpeciesAndPersonality(int species,int pid){return pid;}
unsigned Random(void){rng=(rng*0x41C64E6D+0x6073)&0xffffffff;draws++;return rng>>16;}
void BattleScriptPushCursor(void){} void BattleScriptPush(void *p){}
void CancelMultiTurnMoves(int b){cancelled++;} int CountTrailingZeroBits(unsigned n){return __builtin_ctz(n);}
void charm(void){'''+charm+r'''}
void love(void){'''+love+r'''}
int main(int argc,char**argv){
 if(argc!=14)return 2;
 int mode=atoi(argv[1]);rng=strtoul(argv[2],0,10);gBattlerAttacker=atoi(argv[3]);gBattlerTarget=atoi(argv[4]);
 gMoveResultFlags=atoi(argv[5]);gBattleMons[gBattlerAttacker].hp=atoi(argv[6]);gBattleMons[gBattlerTarget].hp=atoi(argv[7]);
 gBattleMoves[0].flags=atoi(argv[8]);gSpecialStatuses[gBattlerTarget].physicalDmg=atoi(argv[9]);gSpecialStatuses[gBattlerTarget].specialDmg=atoi(argv[10]);
 gBattleMons[gBattlerAttacker].ability=atoi(argv[11])?ABILITY_OBLIVIOUS:ABILITY_NONE;pidAtk=atoi(argv[12]);pidDef=atoi(argv[13]);
 if(mode==2||mode==3)gBattleMons[gBattlerAttacker].status2=STATUS2_INFATUATED_WITH(gBattlerTarget);
 if(mode==3)love();else charm();
 printf("%d %d %d %lu %d %d\n",effect,draws,gBattleMons[gBattlerAttacker].status2,rng,cancelled,gBattleScripting.battler);
}
'''
(out/'source-contact.c').write_text(pre)
subprocess.run(['cc','-std=c11','-Werror','-Wno-unused-function',str(out/'source-contact.c'),'-o',str(out/'source-contact')],check=True)
(out/'source-manifest.json').write_text(json.dumps({'commit':commit,'files':pins,'branches':['ABILITY_CUTE_CHARM','CANCELLER_IN_LOVE']},indent=2)+'\n')
print('Compiled exact pinned Cute Charm and in-love branches')
