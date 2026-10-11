from pathlib import Path
import hashlib,json,subprocess,sys
source=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()
assert commit=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
pins={'src/battle_script_commands.c':'3b7341db8cd58c54fb0b54b47e6eef89c5c05e8a60ab4e9b45bdc907845c19b3','src/battle_main.c':'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928','data/battle_scripts_1.s':'27e9c39f3147b05e5ba2ed36ca0c89fcdc9b26718778083bd362c582de800034'}
for name in ['include/constants/battle.h','include/constants/abilities.h','include/constants/pokemon.h','include/constants/battle_move_effects.h','include/constants/battle_string_ids.h','include/battle_main.h']:
 pins[name]=hashlib.sha256(subprocess.check_output(['git','show',commit+':'+name],cwd=source)).hexdigest()
for p,h in pins.items(): assert hashlib.sha256((source/p).read_bytes()).hexdigest()==h,p
text=(source/'src/battle_script_commands.c').read_text()
def fn(name):
 import re
 m=re.search(r'static (?:void|u8) '+name+r'\([^\n]*\)\s*(?://[^\n]*)?\n\{',text);assert m,name
 start=m.start();end=text.index('\n}',m.end())+2
 return text[start:end]
constants='\n'.join(l for p in ['battle.h','abilities.h','pokemon.h','battle_move_effects.h','battle_string_ids.h'] for l in (source/'include/constants'/p).read_text().splitlines() if l.startswith(('#define TYPE_','#define ABILITY_','#define MOVE_RESULT_','#define B_WEATHER_','#define EFFECT_','#define B_MSG_','#define HITMARKER_CHARGING','#define STATUS2_FORESIGHT')))
constants += '\n'+'\n'.join(l for l in (source/'include/battle_main.h').read_text().splitlines() if l.startswith('#define TYPE_') and '(' not in l.split()[1])
main=(source/'src/battle_main.c').read_text();start=main.index('const u8 gTypeEffectiveness[336]');table=main[start:main.index('\n};',start)+3]
prefix='''#include <stdio.h>
#include <string.h>
typedef unsigned char u8; typedef unsigned short u16; typedef int s32;
'''+constants+'''
#define MOVE_STRUGGLE 165
#define MISS_TYPE 0
#define TYPE_EFFECT_ATK_TYPE(i) gTypeEffectiveness[i]
#define TYPE_EFFECT_DEF_TYPE(i) gTypeEffectiveness[(i)+1]
#define TYPE_EFFECT_MULTIPLIER(i) gTypeEffectiveness[(i)+2]
#define GET_MOVE_TYPE(m,t) t=(dynamicType<0?gBattleMoves[m].type:dynamicType)
#define IS_BATTLER_OF_TYPE(i,t) (gBattleMons[i].type1==(t)||gBattleMons[i].type2==(t))
struct {int type1,type2,ability,status2;} gBattleMons[2];
struct {int type,power,effect;} gBattleMoves[166];
struct {int targetNotAffected;} gProtectStructs[2];
int gBattlerTarget=1,gBattlerAttacker=0,gCurrentMove,gBattleMoveDamage,gMoveResultFlags,gLastUsedAbility,gLastLandedMoves[2],gLastHitByType[2],gBattleCommunication[1],gBattleWeather,gHitMarker,dynamicType,recorded;
unsigned char script[16]; const unsigned char *gBattlescriptCurrInstr;
void RecordAbilityBattle(int battler,int ability){(void)battler;recorded=ability;}
static u8 AttacksThisTurn(u8 battlerId,u16 move);
'''
fragments='\n'.join(fn(n) for n in ['ModulateDmgByType','Cmd_typecalc','CheckWonderGuardAndLevitate','Cmd_typecalc2','AttacksThisTurn'])
harness=prefix+table+'\n'+fragments+'''
int main(void){int mode,type,a,b,ability,power,foresight,charge,effect,struggle,dynamic,sun;
while(scanf("%d%d%d%d%d%d%d%d%d%d%d%d",&mode,&type,&a,&b,&ability,&power,&foresight,&charge,&effect,&struggle,&dynamic,&sun)==12){
memset(gBattleMons,0,sizeof(gBattleMons));memset(gProtectStructs,0,sizeof(gProtectStructs));
gBattlescriptCurrInstr=script;gCurrentMove=struggle?MOVE_STRUGGLE:1;gBattleMoves[gCurrentMove].type=type;gBattleMoves[gCurrentMove].power=power;gBattleMoves[gCurrentMove].effect=effect;
gBattleMons[1].type1=a;gBattleMons[1].type2=b;gBattleMons[1].ability=ability;gBattleMons[1].status2=foresight?STATUS2_FORESIGHT:0;
gBattleMons[0].type1=gBattleMons[0].type2=TYPE_MYSTERY;gBattleMoveDamage=100;gMoveResultFlags=mode>=2?MOVE_RESULT_MISSED:0;gLastUsedAbility=recorded=0;gLastLandedMoves[1]=123;gLastHitByType[1]=TYPE_FIRE;gBattleCommunication[0]=0;gHitMarker=charge?HITMARKER_CHARGING:0;gBattleWeather=sun?B_WEATHER_SUN:0;dynamicType=dynamic;
if(mode==0||mode==3)Cmd_typecalc();else if(mode==1)Cmd_typecalc2();else CheckWonderGuardAndLevitate();
printf("%d %d %d %d %d %d %d\\n",gMoveResultFlags,gBattleCommunication[0],recorded,gLastLandedMoves[1],gLastHitByType[1],gBattleMoveDamage,AttacksThisTurn(0,gCurrentMove));}}
'''
(out/'source-extract.c').write_text(harness)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror','-Wno-unused-parameter',str(out/'source-extract.c'),'-o',str(out/'source-extract')],check=True)
# Deliberately bounded table-command branches, not an all-pairs battle campaign.
rows=[]
pairs=[(6,7),(0,0),(12,11),(4,2),(10,12),(8,5),(7,0)]
for mode in range(4):
 for attack in [0,1,2,4,5,6,7,10,11,12,13,14,15,17]:
  for a,b in pairs:
   for power in [0,1,50]:rows.append([mode,attack,a,b,25,power,0,0,0,0,-1,0])
for mode in range(4):
 for a,b in pairs:
  for identified in [0,1]:
   for attack in [0,1]: rows.append([mode,attack,a,b,25,50,identified,0,0,0,-1,0])
# Source charging exception, Struggle, Levitate and dynamic typing.
for mode in range(4):
 for effect in [39,75,155,145,151,26]:
  for charge in [0,1]:
   for sun in [0,1]:rows.append([mode,11,6,7,25,50,0,charge,effect,0,-1,sun])
 for power in [0,50]:
  rows += [[mode,0,6,7,25,power,0,0,0,1,-1,0],[mode,4,6,7,26,power,0,0,0,0,-1,0],[mode,0,6,7,25,power,0,0,0,0,10,0],[mode,0,6,7,25,power,0,0,0,0,11,0]]
res=subprocess.run([str(out/'source-extract')],input=''.join(' '.join(map(str,r))+'\n' for r in rows),text=True,capture_output=True,check=True)
vectors=[dict(input=r,output=list(map(int,line.split()))) for r,line in zip(rows,res.stdout.splitlines(),strict=True)]
assert vectors
(out/'source-vectors.json').write_text(json.dumps(dict(commit=commit,pins=pins,extractSha256=hashlib.sha256(fragments.encode()).hexdigest(),vectors=vectors),separators=(',',':'))+'\n')
print('PASS',len(vectors),'pinned source type-result vectors')
