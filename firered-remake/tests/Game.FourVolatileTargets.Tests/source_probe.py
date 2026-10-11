import hashlib,json,pathlib,subprocess,sys
ref,out=map(pathlib.Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=True)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==pin
commands=(ref/'src/battle_script_commands.c').read_text();scripts=(ref/'data/battle_scripts_1.s').read_text();util=(ref/'src/battle_util.c').read_text();main=(ref/'src/battle_main.c').read_text()
def between(text,a,b):return text.split(a,1)[1].split(b,1)[0]
confuse=between(scripts,'BattleScript_EffectConfuse::','BattleScript_AlreadyConfused::')
assert [confuse.index(s) for s in ['ppreduce','ABILITY_OWN_TEMPO','STATUS2_SUBSTITUTE','STATUS2_CONFUSION','accuracycheck','SIDE_STATUS_SAFEGUARD','seteffectprimary']]==sorted(confuse.index(s) for s in ['ppreduce','ABILITY_OWN_TEMPO','STATUS2_SUBSTITUTE','STATUS2_CONFUSION','accuracycheck','SIDE_STATUS_SAFEGUARD','seteffectprimary'])
assert 'accuracycheck BattleScript_ButItFailed, NO_ACC_CALC' in between(scripts,'BattleScript_EffectMeanLook::','BattleScript_EffectNightmare::')
assert 'gDisableStructs[i].battlerPreventingEscape == gActiveBattler' in main and '*(gBattleStruct->wrappedBy + i) == gActiveBattler' in main
confcase='case MOVE_EFFECT_CONFUSION:'+between(commands,'case MOVE_EFFECT_CONFUSION:','case MOVE_EFFECT_FLINCH:')
wrapcase='case MOVE_EFFECT_WRAP:'+between(commands,'case MOVE_EFFECT_WRAP:','case MOVE_EFFECT_RECOIL_25:')
lookcase='case MOVE_EFFECT_PREVENT_ESCAPE:'+between(commands,'case MOVE_EFFECT_PREVENT_ESCAPE:','case MOVE_EFFECT_NIGHTMARE:')
residual='case ENDTURN_WRAP:'+between(util,'case ENDTURN_WRAP:','case ENDTURN_UPROAR:')
head=r'''#include <stdio.h>
#include <stdint.h>
#define MOVE_EFFECT_CONFUSION 1
#define MOVE_EFFECT_WRAP 13
#define MOVE_EFFECT_PREVENT_ESCAPE 14
#define MOVE_EFFECT_BYTE 0
#define MULTISTRING_CHOOSER 1
#define ABILITY_OWN_TEMPO 20
#define STATUS2_CONFUSION 7
#define STATUS2_CONFUSION_TURN(x) (x)
#define STATUS2_WRAPPED 0xE000
#define STATUS2_WRAPPED_TURN(x) ((x)<<13)
#define STATUS2_ESCAPE_PREVENTION 0x04000000
#define NUM_TRAPPING_MOVES 6
#define ENDTURN_WRAP 0
#define B_BUFF_PLACEHOLDER_BEGIN 0
#define B_BUFF_MOVE 1
#define EOS 255
static uint32_t seed;static int draws;
static uint16_t Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
static struct Mon{int hp,maxHP,ability;uint32_t status2;}gBattleMons[4];
static struct Disable{int battlerPreventingEscape;}gDisableStructs[4];
static struct Battle{unsigned char wrappedMove[8],wrappedBy[4];int turnEffectsTracker;}bs,*gBattleStruct=&bs;
static struct Script{int animArg1,animArg2;}gBattleScripting;
static unsigned char script[100],*gBattlescriptCurrInstr=script,*sMoveEffectBS_Ptrs[16],*BattleScript_WrapTurnDmg=script+1,*BattleScript_WrapEnds=script+2,gBattleTextBuff1[5];
static int gBattleCommunication[2],gEffectBattler=3,gBattlerTarget=3,gBattlerAttacker=2,gCurrentMove=20,gActiveBattler=3,gBattleMoveDamage,effect,gTrappingMoves[6]={20,35,83,128,250,328};
static void BattleScriptPush(void *p){(void)p;}static void BattleScriptExecute(void *p){(void)p;}
'''
body='static void Apply(int kind){switch(kind){'+confcase+wrapcase+lookcase+'}}\nstatic void Residual(void){switch(0){'+residual+'}}\n'
run=r'''int main(void){printf("{\"vectors\":[");int first=1;for(int s=0;s<8;s++){for(int k=0;k<2;k++){seed=s;draws=0;gBattleMons[3].status2=0;gBattlescriptCurrInstr=script;Random();if(k){Random();Random();Random();}gBattleCommunication[0]=k?13:1;Apply(k?13:1);printf("%s{\"seed\":%d,\"kind\":\"%s\",\"turns\":%u,\"draws\":%d,\"finalSeed\":%u,\"source\":%d}",first?"":",",s,k?"wrap":"confusion",k?(gBattleMons[3].status2>>13):(gBattleMons[3].status2&7),draws,seed,bs.wrappedBy[3]);first=0;}}
printf("],\"residual\":[");for(int t=1;t<=6;t++){gBattleMons[3].status2=STATUS2_WRAPPED_TURN(t);gBattleMons[3].hp=10;gBattleMons[3].maxHP=100;gBattleMoveDamage=0;Residual();printf("%s{\"before\":%d,\"after\":%u,\"damage\":%d}",t==1?"":",",t,gBattleMons[3].status2>>13,gBattleMoveDamage);}Apply(14);printf("],\"meanLookSource\":%d}\n",gDisableStructs[3].battlerPreventingEscape);return 0;}
'''
(out/'oracle.c').write_text(head+body+run)
subprocess.run(['cc','-std=c99','-Wall','-Wextra','-O0',str(out/'oracle.c'),'-o',str(out/'oracle')],check=True,capture_output=True,text=True)
v=json.loads(subprocess.check_output([str(out/'oracle')],text=True));v['sourceCommit']=pin
v['sourceFiles']={p:hashlib.sha256((ref/p).read_bytes()).hexdigest() for p in ['src/battle_script_commands.c','src/battle_main.c','src/battle_util.c','data/battle_scripts_1.s','src/data/battle_moves.h']}
(out/'source-vectors.json').write_text(json.dumps(v,indent=2)+'\n');print(json.dumps({'pin':pin,'vectors':len(v['vectors']),'residual':len(v['residual'])}))
