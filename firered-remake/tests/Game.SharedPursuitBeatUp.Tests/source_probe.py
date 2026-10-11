#!/usr/bin/env python3
"""Authored host harness executes two unedited commands from pinned pret source once."""
import argparse,hashlib,json,pathlib,re,subprocess
p=argparse.ArgumentParser();p.add_argument('--source',required=True);p.add_argument('--output',required=True);a=p.parse_args()
r=pathlib.Path(a.source);o=pathlib.Path(a.output);o.mkdir(parents=True,exist_ok=True)
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','-C',str(r),'rev-parse','HEAD'],text=True).strip()==commit
files=['src/battle_script_commands.c','data/battle_scripts_1.s','src/data/battle_moves.h','include/constants/battle_script_commands.h']
inputs=[];texts={}
for f in files:
 b=(r/f).read_bytes();assert b==subprocess.check_output(['git','-C',str(r),'show',commit+':'+f]);texts[f]=b.decode();inputs.append({'path':str(r/f),'sha256':hashlib.sha256(b).hexdigest()})
s=texts[files[0]]
def extract(name):
 start=s.index('static void '+name+'(void)\n{');end=s.index('\n}\n',start)+3;return s[start:end]
commands=[extract('Cmd_jumpifnopursuitswitchdmg'),extract('Cmd_trydobeatup')]
script=texts[files[1]]
beat=script[script.index('BattleScript_EffectBeatUp::'):script.index('BattleScript_EffectSemiInvulnerable::')]
assert 'typecalc' not in beat and 'moveendto MOVEEND_NEXT_TARGET' in beat and 'critcalc' in beat
pursuit=script[script.index('BattleScript_PursuitDmgOnSwitchOut::'):script.index('BattleScript_Pausex20::')]
assert 'accuracycheck' not in pursuit and 'attackcanceler' not in pursuit and 'seteffectwithchance' not in pursuit
assert pursuit.index('tryfaintmon BS_TARGET')<pursuit.index('moveendfromto MOVEEND_ON_DAMAGE_ABILITIES, MOVEEND_CHOICE_MOVE')<pursuit.index('getexp BS_TARGET')
prefix=r'''
#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8; typedef int32_t s32;
#define B_SIDE_PLAYER 0
#define B_POSITION_OPPONENT_LEFT 1
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_RIGHT 3
#define B_POSITION_PLAYER_RIGHT 2
#define B_ACTION_USE_MOVE 0
#define B_ACTION_TRY_FINISH 12
#define STATUS1_SLEEP 7
#define STATUS1_FREEZE 32
#define MOVE_PURSUIT 228
#define HITMARKER_ATTACKSTRING_PRINTED 1
#define PARTY_SIZE 6
#define SPECIES_EGG 412
#define MON_DATA_HP 0
#define MON_DATA_SPECIES_OR_EGG 1
#define MON_DATA_STATUS 2
#define MON_DATA_SPECIES 3
#define MON_DATA_LEVEL 4
#define PREPARE_MON_NICK_WITH_PREFIX_BUFFER(a,b,c) ((void)0);
static u8 code[256],*gBattlescriptCurrInstr;
#define T1_READ_PTR(x) (code + ((x)==code+1?100:200))
static u8 gMultiHitCounter,gBattlerAttacker,gBattlerTarget,gBattlersCount=4,gCurrMovePos,gChosenMovePos;
static u8 gChosenActionByBattler[4],gBattlerByTurnOrder[4]={3,1,2,0},gActionsByTurnOrder[4];
static int gChosenMoveByBattler[4],gCurrentMove,gHitMarker,gBattleMoveDamage;
static struct {unsigned status1;int hp,species;} gBattleMons[4];
static struct {int truantCounter;} gDisableStructs[4];
static struct {u8 moveTarget[4],chosenMovePositions[4];} store,*gBattleStruct=&store;
static struct {int animTurn;} gBattleScripting;
static u8 gBattleCommunication[8];
struct Pokemon {int data[5];};static struct Pokemon gPlayerParty[6],gEnemyParty[6];
static struct {int baseAttack,baseDefense;} gSpeciesInfo[413];
static struct {int power;} gBattleMoves[355];
static struct {int helpingHand;} gProtectStructs[4];
static int GetBattlerSide(int i){return i&1;}static int GetBattlerAtPosition(int i){return i;}
static int GetMonData(struct Pokemon *p,int kind){return p->data[kind];}
'''
main=r'''
int main(void){
 printf("{\"pursuit\":[");int first=1;
 for(int out=0;out<4;out++)for(int counter=1;counter<=2;counter++)for(int state=0;state<6;state++)for(int match=0;match<2;match++){
 memset(gBattleMons,0,sizeof(gBattleMons));memset(gDisableStructs,0,sizeof(gDisableStructs));memset(gActionsByTurnOrder,0,sizeof(gActionsByTurnOrder));
 memset(gChosenMoveByBattler,0,sizeof(gChosenMoveByBattler));gBattlerAttacker=out;gMultiHitCounter=counter;gBattlescriptCurrInstr=code;
 int actor=(out^1)&1;if(counter==2)actor|=2;
 gBattleMons[out].hp=state==5?0:100;gBattleMons[actor].hp=100;gBattleMons[actor].status1=state==1?3:state==2?32:state==3?64:0;
 gDisableStructs[actor].truantCounter=state==4;gBattleStruct->moveTarget[actor]=match?out:out^2;gChosenMoveByBattler[actor]=228;gBattleStruct->chosenMovePositions[actor]=2;
 Cmd_jumpifnopursuitswitchdmg();int accepted=gBattlescriptCurrInstr==code+5;int consumed=0;for(int i=0;i<4;i++)if(gActionsByTurnOrder[i]==12)consumed++;
 printf("%s{\"outgoing\":%d,\"counter\":%d,\"actor\":%d,\"state\":%d,\"matched\":%d,\"accepted\":%d,\"consumed\":%d}",first?"":",",out,counter,gBattlerTarget,state,match,accepted,consumed);first=0;
 }
 printf("],\"beatUp\":[");first=1;
 for(int attack=5;attack<=125;attack+=40)for(int defense=5;defense<=205;defense+=100)for(int level=5;level<=100;level+=19)for(int helping=0;helping<2;helping++){
 memset(gPlayerParty,0,sizeof(gPlayerParty));memset(gBattleCommunication,0,sizeof(gBattleCommunication));gBattlerAttacker=0;gBattlerTarget=3;gCurrentMove=251;gBattleMons[3].hp=100;gBattleMons[3].species=2;gBattleMoves[251].power=10;gSpeciesInfo[1].baseAttack=attack;gSpeciesInfo[2].baseDefense=defense;gProtectStructs[0].helpingHand=helping;
 // Original party order excludes a status member, a fainted member and an egg.
 gPlayerParty[0]=(struct Pokemon){{100,1,8,1,level}};gPlayerParty[1]=(struct Pokemon){{0,1,0,1,level}};gPlayerParty[2]=(struct Pokemon){{100,412,0,1,level}};gPlayerParty[3]=(struct Pokemon){{100,1,0,1,level}};
 gBattlescriptCurrInstr=code;Cmd_trydobeatup();
 printf("%s{\"attack\":%d,\"defense\":%d,\"level\":%d,\"helping\":%d,\"member\":%d,\"damage\":%d}",first?"":",",attack,defense,level,helping,gBattleCommunication[0]-1,gBattleMoveDamage);first=0;
 }
 printf("]}\n");return 0;
}
'''
source=o/'original-command-host.c';source.write_text(prefix+'\n'.join(commands)+main)
exe=o/'original-command-host';command=['cc','-std=c11','-O2','-Wall','-Wextra','-o',str(exe),str(source)];compile=subprocess.run(command,capture_output=True,text=True);(o/'compile.log').write_text(compile.stdout+compile.stderr);assert compile.returncode==0
result=subprocess.check_output([str(exe)],text=True);(o/'vectors.json').write_text(result);vectors=json.loads(result)
assert len(vectors['pursuit'])==96 and len(vectors['beatUp'])==144
for v in vectors['pursuit']:assert v['accepted']==int(v['matched'] and v['state'] in (0,3));assert v['consumed']==v['accepted']
for v in vectors['beatUp']:
 d=v['attack']*10*(v['level']*2//5+2)//v['defense']//50+2
 if v['helping']:d=d*15//10
 assert v['member']==3 and v['damage']==d
(o/'report.json').write_text(json.dumps({'commit':commit,'inputs':inputs,'commands':[hashlib.sha256(c.encode()).hexdigest() for c in commands],'compileCommand':command,'hostSourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'pursuitVectors':len(vectors['pursuit']),'beatUpVectors':len(vectors['beatUp']),'scriptOrderVerified':True},indent=2)+'\n')
print('PASS original C: 96 Pursuit guards and 144 Beat Up eligibility/base damage vectors; script order authenticated')
