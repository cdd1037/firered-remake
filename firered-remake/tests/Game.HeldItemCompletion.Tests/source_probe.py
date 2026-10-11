"""Compile pinned source item cases into a private bounded oracle; no copied source tables are authored here."""
from pathlib import Path
import hashlib,json,re,subprocess,sys
source=Path(sys.argv[1]); out=Path(sys.argv[2]); out.mkdir(parents=True,exist_ok=True)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()==pin
names=['src/battle_util.c','src/battle_main.c','src/battle_script_commands.c','src/pokemon.c','src/data/items.json','data/battle_scripts_1.s','include/constants/hold_effects.h','include/constants/battle.h','include/constants/pokemon.h','include/constants/battle_string_ids.h','include/constants/items.h','include/battle.h']
pins={}
for name in names:
 raw=(source/name).read_bytes();assert raw==subprocess.check_output(['git','show',pin+':'+name],cwd=source),name;pins[name]=hashlib.sha256(raw).hexdigest()
util=(source/'src/battle_util.c').read_text();pokemon=(source/'src/pokemon.c').read_text()
body=util.split('u8 ItemBattleEffects(')[1]
normal=body.split('    case ITEMEFFECT_NORMAL:')[1].split('    case ITEMEFFECT_DUMMY:')[0]
switchin=body.split('    case ITEMEFFECT_ON_SWITCH_IN:')[1].split('    case ITEMEFFECT_NORMAL:')[0]
moveend=body.split('    case ITEMEFFECT_MOVE_END:')[1].split('    case ITEMEFFECT_KINGSROCK_SHELLBELL:')[0]
macros=util[util.index('#define TRY_EAT_CONFUSE_BERRY'):util.index('u8 ItemBattleEffects(')]
flavors=pokemon[pokemon.index('static const s8 sPokeblockFlavorCompatibilityTable'):pokemon.index('#define PP_UP_SHIFTS')]
commands=(source/'src/battle_script_commands.c').read_text()
confusion_assignment=re.search(r'gBattleMons\[gEffectBattler\]\.status2 \|= STATUS2_CONFUSION_TURN\(.*?;',commands)[0]
pp=pokemon[pokemon.index('u8 CalculatePPWithBonus('):pokemon.index('void RemoveMonPPBonus(')]
scripts=sorted(set(re.findall(r'BattleScript_[A-Za-z0-9_]+',normal+switchin+moveend+macros)))
strings=sorted(set(re.findall(r'gStatusConditionString_[A-Za-z0-9_]+',normal+moveend)))
preamble='''#include <stdio.h>
#include <string.h>
#include <stdint.h>
typedef unsigned char u8;typedef unsigned short u16;typedef signed char s8;typedef uint32_t u32;typedef int bool8;
#define TRUE 1
#define FALSE 0
#define MAX_MON_MOVES 4
#define MULTISTRING_CHOOSER 0
#define NUM_NATURES 25
#define FLAVOR_COUNT 5
#define FLAVOR_SPICY 0
#define FLAVOR_DRY 1
#define FLAVOR_SWEET 2
#define FLAVOR_BITTER 3
#define FLAVOR_SOUR 4
#define ITEM_NO_EFFECT 0
#define ITEM_STATUS_CHANGE 1
#define ITEM_EFFECT_OTHER 2
#define ITEM_PP_CHANGE 3
#define ITEM_HP_CHANGE 4
#define ITEM_STATS_CHANGE 5
#define B_BUFF_PLACEHOLDER_BEGIN 0
#define B_BUFF_STRING 0
#define EOS 0
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 100
#define REQUEST_STATUS_BATTLE 90
#define PREPARE_MOVE_BUFFER(...) ((void)0)
#define PREPARE_STAT_BUFFER(...) ((void)0)
#define PREPARE_STRING_BUFFER(...) ((void)0)
#define PREPARE_FLAVOR_BUFFER(...) ((void)0)
#define SET_STATCHANGER(stat,amount,negative) (changeStat=(stat),changeAmount=(amount))
#define MOVE_IS_PERMANENT(battler,slot) (!(gBattleMons[battler].status2 & STATUS2_TRANSFORMED) && !(mimicked & (1u<<(slot))))
'''+''.join('#include "'+str(source/'include/constants'/name)+'"\n' for name in ['battle.h','hold_effects.h','pokemon.h','battle_string_ids.h','items.h'])+'''
struct Pokemon {int moves[4];int pp[4];int bonuses;} gPlayerParty[1],gEnemyParty[1];
struct {int hp,maxHP,item;u32 status1,status2,personality;int statStages[8];int pp[4];} gBattleMons[1];
struct {int animArg1,animArg2,battler;} gBattleScripting;
struct {int moneyMultiplier;} battleStruct,*gBattleStruct=&battleStruct;
struct {int holdEffect,holdEffectParam;} gEnigmaBerries[1];
struct {int pp;} gBattleMoves[4];
const u8 gPPUpGetMask[4]={3,12,48,192};
int gBattlerPartyIndexes[1],battlerId,battlerHoldEffect,battlerHoldEffectParam,gLastUsedItem,gBattlersCount=1;
int gBattleMoveDamage,gBattlescriptCurrInstr,gEffectBattler,gPotentialItemEffectBattler,gActiveBattler,gBattlerAttacker;
int gBattleTextBuff1[20],gBattleTextBuff2[20],gBattleCommunication[32];
int changeStat,changeAmount,scriptChosen,mimicked,draws;u32 seed;
'''+''.join('enum { '+name+' = '+str(i+1)+' };\n' for i,name in enumerate(scripts))+''.join('int '+name+'[2];\n' for name in strings)+'''
u16 Random(void){seed=seed*0x41C64E6Du+0x6073u;draws++;return seed>>16;}
void BattleScriptExecute(int s){scriptChosen=s;}
void BattleScriptPushCursor(void){}
void RecordItemEffectBattle(int b,int e){(void)b;(void)e;}
void StringCopy(int *a,int *b){(void)a;(void)b;}
int GetBattlerSide(int b){(void)b;return 0;}
int ItemId_GetHoldEffect(int i){(void)i;return battlerHoldEffect;}
int ItemId_GetHoldEffectParam(int i){(void)i;return battlerHoldEffectParam;}
int GetMonData(struct Pokemon *m,int field){if(field>=MON_DATA_MOVE1&&field<=MON_DATA_MOVE4)return m->moves[field-MON_DATA_MOVE1];if(field>=MON_DATA_PP1&&field<=MON_DATA_PP4)return m->pp[field-MON_DATA_PP1];return m->bonuses;}
void BtlController_EmitSetMonData(int a,int request,int c,int len,void *value){(void)a;(void)c;(void)len;if(request>=100&&request<104)gPlayerParty[0].pp[request-100]=*(u8*)value;}
void MarkBattlerForControllerExec(int b){(void)b;}
'''+flavors+'''
int GetFlavorRelationByPersonality(u32 p,int flavor){return sPokeblockFlavorCompatibilityTable[(p%25)*5+flavor];}
'''+pp+macros+'''
u8 original(int phase){int i=0;u8 effect=0,changedPP=0;int moveTurn=phase==1;
switch(phase){case 0:case 1:
'''+normal+'''case 2:
'''+moveend+'''case 3:
'''+switchin+'''}return effect;}
int main(void){int phase,hold,param,hp,max,ppBase,bonus,ppNow,transformed;unsigned status,confusion,pid;
while(scanf("%d%d%d%d%d%u%u%u%u%d%d%d%d%d%d%d%d%d",&phase,&hold,&param,&hp,&max,&status,&confusion,&pid,&seed,&ppBase,&bonus,&ppNow,&transformed,&gBattleMons[0].statStages[1],&gBattleMons[0].statStages[2],&gBattleMons[0].statStages[3],&gBattleMons[0].statStages[4],&gBattleMons[0].statStages[5])==18){
gBattleMons[0].hp=hp;gBattleMons[0].maxHP=max;gBattleMons[0].status1=status;gBattleMons[0].status2=confusion|(transformed?STATUS2_TRANSFORMED:0);gBattleMons[0].personality=pid;
gBattleMons[0].statStages[0]=6;gBattleMons[0].statStages[6]=gBattleMons[0].statStages[7]=6;gBattleMons[0].pp[0]=transformed?5:ppNow;
memset(gPlayerParty,0,sizeof(gPlayerParty));gPlayerParty[0].moves[0]=1;gPlayerParty[0].pp[0]=ppNow;gPlayerParty[0].bonuses=bonus;gBattleMoves[1].pp=ppBase;
battlerId=gEffectBattler=gActiveBattler=gBattlerAttacker=0;battlerHoldEffect=hold;battlerHoldEffectParam=param;changeStat=changeAmount=draws=scriptChosen=gBattleMoveDamage=gBattlescriptCurrInstr=0;
int result=original(phase);if(scriptChosen==BattleScript_BerryConfuseHealEnd2 && !(gBattleMons[0].status2 & STATUS2_CONFUSION)){
'''+confusion_assignment+'''
}
printf("%d %u %u %d %d %d %d %d %d %u %d %d %d %d %d %d\\n",result,gBattleMons[0].status1,gBattleMons[0].status2,-gBattleMoveDamage,changeStat,changeAmount,gPlayerParty[0].pp[0],gBattleMons[0].pp[0],draws,seed,scriptChosen?scriptChosen:gBattlescriptCurrInstr,gBattleMons[0].statStages[1],gBattleMons[0].statStages[2],gBattleMons[0].statStages[3],gBattleMons[0].statStages[4],gBattleMons[0].statStages[5]);
}return 0;}
'''
(out/'original-held-cases.c').write_text(preamble)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'original-held-cases.c'),'-o',str(out/'original-held-cases')],check=True)
items=json.loads((source/'src/data/items.json').read_text())['items'];holds=(source/'include/constants/hold_effects.h').read_text()
selected=[i for i in items if i['itemId'] in ['ITEM_SITRUS_BERRY','ITEM_LUM_BERRY','ITEM_LEPPA_BERRY','ITEM_FIGY_BERRY','ITEM_WIKI_BERRY','ITEM_MAGO_BERRY','ITEM_AGUAV_BERRY','ITEM_IAPAPA_BERRY','ITEM_LIECHI_BERRY','ITEM_GANLON_BERRY','ITEM_SALAC_BERRY','ITEM_PETAYA_BERRY','ITEM_APICOT_BERRY','ITEM_LANSAT_BERRY','ITEM_STARF_BERRY','ITEM_WHITE_HERB','ITEM_MENTAL_HERB','ITEM_LEFTOVERS']]
assert len(selected)==18
rows=[]
for item in selected:
 for phase in range(4):
  for hp in [0,1,20,21,40,41]:
   rows.append(dict(item=item['itemId'],phase=phase,hp=hp,maxHp=81,status=3,confusion=4,pid=5,seed=31,ppBase=5,bonus=3,ppNow=0,transformed=0,stages=[3,12,6,5,11]))
for pid in range(25):
 for item in selected:
  if not item['holdEffect'].startswith('HOLD_EFFECT_CONFUSE_'):continue
  rows.append(dict(item=item['itemId'],phase=0,hp=20,maxHp=81,status=0,confusion=0,pid=pid,seed=31,ppBase=10,bonus=0,ppNow=1,transformed=0,stages=[6]*5))
for seed in range(30):
 rows.append(dict(item='ITEM_STARF_BERRY',phase=0,hp=1,maxHp=81,status=0,confusion=0,pid=0,seed=seed,ppBase=10,bonus=0,ppNow=1,transformed=0,stages=[12,12,12,12,11]))
for phase in range(4):
 rows.append(dict(item='ITEM_MENTAL_HERB',phase=phase,hp=40,maxHp=81,status=0,confusion=0x10000,pid=0,seed=31,ppBase=5,bonus=0,ppNow=1,transformed=0,stages=[6]*5))
for transformed in [0,1]:
 for bonus in range(4):
  rows.append(dict(item='ITEM_LEPPA_BERRY',phase=0,hp=81,maxHp=81,status=0,confusion=0,pid=0,seed=31,ppBase=5,bonus=bonus,ppNow=0,transformed=transformed,stages=[6]*5))
byid={i['itemId']:i for i in selected}
inputs=[]
for row in rows:
 item=byid[row['item']];hold=int(re.search(r'#define\s+'+item['holdEffect']+r'\s+(\d+)',holds)[1])
 inputs.append(' '.join(map(str,[row['phase'],hold,item['holdEffectParam'],row['hp'],row['maxHp'],row['status'],row['confusion'],row['pid'],row['seed'],row['ppBase'],row['bonus'],row['ppNow'],row['transformed'],*row['stages']])))
outputs=subprocess.run([str(out/'original-held-cases')],input='\n'.join(inputs)+'\n',text=True,capture_output=True,check=True).stdout.splitlines()
assert len(outputs)==len(rows)>500
for row,line in zip(rows,outputs,strict=True):
 values=list(map(int,line.split()));assert len(values)==16;row['expected']=values;row['script']=scripts[values[10]-1] if values[10] else None
result=dict(sourceCommit=pin,pins=pins,vectors=rows)
(out/'source-vectors.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(dict(vectors=len(rows),files=len(pins),sourceCommit=pin)))
print('HELD_SOURCE_ORACLE_END')
# Independent CalculateBaseDamage held-stat block; extract original mapping and arithmetic.
table=pokemon[pokemon.index('static const u8 sHoldEffectToType'):pokemon.index('const struct SpriteTemplate gSpriteTemplates_Battlers')]
block=pokemon[pokemon.index('    // Apply type-bonus hold item'):pokemon.index('    if (defender->ability == ABILITY_THICK_FAT')]
stat_c='''#include <stdio.h>
#include <stdint.h>
typedef uint8_t u8;typedef uint16_t u16;
#define ARRAY_COUNT(a) (sizeof(a)/sizeof((a)[0]))
#define IS_TYPE_PHYSICAL(t) ((t)<TYPE_MYSTERY)
'''+''.join('#include "'+str(source/'include/constants'/name)+'"\n' for name in ['pokemon.h','hold_effects.h','species.h','battle.h'])+table+'''
int main(void){int attackerHoldEffect,defenderHoldEffect,attackerHoldEffectParam,type,as,ds,base;
while(scanf("%d%d%d%d%d%d%d",&attackerHoldEffect,&defenderHoldEffect,&attackerHoldEffectParam,&type,&as,&ds,&base)==7){
struct {int species;} a={as},d={ds},*attacker=&a,*defender=&d;unsigned i;int gBattleTypeFlags=0;u16 attack=base,defense=base,spAttack=base,spDefense=base;
'''+block+'''
printf("%u %u %u %u\\n",attack,defense,spAttack,spDefense);
}return 0;}
'''
(out/'original-held-stats.c').write_text(stat_c)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'original-held-stats.c'),'-o',str(out/'original-held-stats')],check=True)
species_text=(source/'include/constants/species.h').read_text()
def species_id(s):return int(re.search(r'#define\s+SPECIES_'+s+r'\s+(\d+)\b',species_text)[1])
statrows=[]
for item in items:
 effect=item['holdEffect']
 if effect.endswith('_POWER'):
  targets=['MAGIKARP'];types=range(18)
 elif effect=='HOLD_EFFECT_SOUL_DEW':targets=['LATIAS','LATIOS','MAGIKARP'];types=[10]
 elif effect in ['HOLD_EFFECT_DEEP_SEA_TOOTH','HOLD_EFFECT_DEEP_SEA_SCALE']:targets=['CLAMPERL','MAGIKARP'];types=[11]
 elif effect=='HOLD_EFFECT_LIGHT_BALL':targets=['PIKACHU','RAICHU','MAGIKARP'];types=[13,0]
 else:continue
 for s in targets:
  for typ in types:
   for base in [1,99,101,40000]:statrows.append(dict(item=item['itemId'],effect=effect,param=item['holdEffectParam'],species=s,speciesId=species_id(s),type=typ,basis=base))
statinputs=[]
for r in statrows:
 hold=int(re.search(r'#define\s+'+r['effect']+r'\s+(\d+)',holds)[1]);statinputs.append(' '.join(map(str,[hold,hold,r['param'],r['type'],r['speciesId'],r['speciesId'],r['basis']])))
statouts=subprocess.run([str(out/'original-held-stats')],input='\n'.join(statinputs)+'\n',capture_output=True,text=True,check=True).stdout.splitlines()
for row,line in zip(statrows,statouts,strict=True):row['expected']=list(map(int,line.split()))
result['statVectors']=statrows
result['pins']['include/constants/species.h']=hashlib.sha256((source/'include/constants/species.h').read_bytes()).hexdigest()
assert (source/'include/constants/species.h').read_bytes()==subprocess.check_output(['git','show',pin+':include/constants/species.h'],cwd=source)
(out/'source-vectors.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(dict(itemPhaseVectors=len(rows),statVectors=len(statrows),files=len(result['pins']))))
print('HELD_STATS_ORACLE_END')
