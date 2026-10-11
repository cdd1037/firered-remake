from pathlib import Path
import hashlib,json,subprocess,sys,itertools
source=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
pins={'src/battle_util.c':'87cf810eb2d58361d1beb1537b3bd71c9160a77c2a17d703399f6ed373bb5720','src/battle_script_commands.c':'3b7341db8cd58c54fb0b54b47e6eef89c5c05e8a60ab4e9b45bdc907845c19b3','data/battle_scripts_1.s':'27e9c39f3147b05e5ba2ed36ca0c89fcdc9b26718778083bd362c582de800034'}
for name,sha in pins.items():assert hashlib.sha256((source/name).read_bytes()).hexdigest()==sha,name
for name in ('include/constants/battle.h','include/constants/pokemon.h','include/constants/abilities.h'):
 pinned=subprocess.check_output(['git','show','037335f4c725d7c9aecdac87066f2002b4bd7e14:'+name],cwd=source)
 assert (source/name).read_bytes()==pinned,name
 pins[name]=hashlib.sha256(pinned).hexdigest()
util=(source/'src/battle_util.c').read_text();cmd=(source/'src/battle_script_commands.c').read_text()
def function(text,name):
 a=text.index(name+'\n{');p=text.index('{',a);depth=1;e=p+1
 while depth:
  depth+=(text[e]=='{')-(text[e]=='}');e+=1
 return text[a:e]
weather=function(cmd,'static void Cmd_weatherdamage(void)')
status=util[util.index('            case ENDTURN_POISON:'):util.index('            case ENDTURN_NIGHTMARES:')]
constants='\n'.join('#include "'+str(source/'include/constants'/f)+'"' for f in ('battle.h','pokemon.h','abilities.h'))
harness='''#include <stdio.h>
#include <stdint.h>
'''+constants+'''
#define GetBattlerSide(battler) ((battler)&1)
#define IS_BATTLER_OF_TYPE(battler,type) (gBattleMons[battler].type1==(type)||gBattleMons[battler].type2==(type))
int gBattleTypeFlags,ghost,gBattlerAttacker,gActiveBattler,gBattleWeather,gBattleMoveDamage,gAbsentBattlerFlags,effect;
unsigned gBitTable[4]={1,2,4,8},gStatuses3[4];unsigned char script[8],*gBattlescriptCurrInstr;
struct {int hp,maxHP,type1,type2,ability;unsigned status1;} gBattleMons[4];
struct Counters {int turnEffectsTracker;} counters,*gBattleStruct=&counters;
int BattleScript_PoisonTurnDmg,BattleScript_BurnTurnDmg;
void BattleScriptExecute(int scriptId){(void)scriptId;}
int weatherHasEffect(void){for(int i=0;i<4;i++)if(gBattleMons[i].hp&&(gBattleMons[i].ability==ABILITY_CLOUD_NINE||gBattleMons[i].ability==ABILITY_AIR_LOCK))return 0;return 1;}
#define WEATHER_HAS_EFFECT weatherHasEffect()
#define ENDTURN_POISON 4
#define ENDTURN_BAD_POISON 5
#define ENDTURN_BURN 6
'''+weather+'''
void SourceStatusCase(void){switch(gBattleStruct->turnEffectsTracker){
'''+status+'''
}}
int main(void){int mode,a,b,c,d,e,f,g,h;while(scanf("%d%d%d%d%d%d%d%d%d",&mode,&a,&b,&c,&d,&e,&f,&g,&h)==9){
for(int i=0;i<4;i++){gBattleMons[i].hp=100;gBattleMons[i].ability=0;gStatuses3[i]=0;}gBattlerAttacker=gActiveBattler=2;gBattleMoveDamage=effect=0;
if(mode==0){gBattleWeather=a;gBattleMons[2].maxHP=b;gBattleMons[2].hp=c;gBattleMons[2].type1=d;gBattleMons[2].type2=0;gBattleMons[2].ability=e;gBattleMons[3].ability=ABILITY_CLOUD_NINE;gBattleMons[3].hp=f;gAbsentBattlerFlags=g;gStatuses3[2]=h;ghost=0;gBattlescriptCurrInstr=script;Cmd_weatherdamage();printf("0 %d\\n",gBattleMoveDamage);}
else{gBattleMons[2].maxHP=b;gBattleMons[2].hp=c;gBattleMons[2].status1=d;gBattleStruct->turnEffectsTracker=a;SourceStatusCase();printf("1 %u %d %d\\n",gBattleMons[2].status1,gBattleMoveDamage,effect);}
}return 0;}
'''
(out/'source-residual-oracle.c').write_text(harness)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'source-residual-oracle.c'),'-o',str(out/'source-residual-oracle')],check=True)
# Source numeric constants are fixed by the authenticated headers. Field scan intentionally includes absent holders.
weatherInputs=[(0,w,m,100,t,a,s,absent,0) for w,m,t,a,s,absent in itertools.product((24,128),(1,15,16,17,99,100,255,511),(0,5,15),(0,8),(0,100),(0,4,8))]
statusInputs=[(1,phase,m,h,status,0,0,0,0) for phase,status in [(4,8),(6,16)]+[(5,128+(i<<8)) for i in (0,1,7,14,15)] for m,h in itertools.product((1,15,16,17,99,100,255,511),(0,1000))]
inputs=weatherInputs+statusInputs
res=subprocess.run([str(out/'source-residual-oracle')],input=''.join(' '.join(map(str,x))+'\n' for x in inputs),text=True,capture_output=True,check=True)
rows=[list(i)+list(map(int,o.split())) for i,o in zip(inputs,res.stdout.splitlines(),strict=True)]
(out/'source-residual-vectors.json').write_text(json.dumps({'sourceCommit':'037335f4c725d7c9aecdac87066f2002b4bd7e14','pins':pins,'weatherFunction':'Cmd_weatherdamage (unchanged complete function)','statusCases':'DoBattlerEndTurnEffects poison/bad-poison/burn (unchanged source cases)','vectors':rows},indent=2)+'\n')
print(f'{len(weatherInputs)} original weather-function and {len(statusInputs)} original status-case vectors')
