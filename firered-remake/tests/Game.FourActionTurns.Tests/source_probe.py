import hashlib, json, pathlib, subprocess, sys
root=pathlib.Path(sys.argv[1]); out=pathlib.Path(sys.argv[2]); out.mkdir(parents=True,exist_ok=True)
path=root/'src/battle_main.c'; text=path.read_text()
assert subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
for source_file in ['src/battle_main.c','src/battle_util.c','src/data/items.json']:
    assert (root/source_file).read_bytes()==subprocess.check_output(['git','-C',str(root),'show','HEAD:'+source_file]),source_file+' differs from pinned blob'

def extract(signature):
    start=text.index(signature+'\n{'); body=text.index('{',start); depth=1; pos=body+1
    while depth:
        depth += (text[pos]=='{')-(text[pos]=='}'); pos+=1
    return text[start:pos]
prefix='''#include <stdio.h>
#include <stdint.h>
#include <limits.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32; typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define WEATHER_HAS_EFFECT 1
#define ABILITY_SWIFT_SWIM 1
#define ABILITY_CHLOROPHYLL 2
#define B_WEATHER_RAIN 1
#define B_WEATHER_SUN 2
#define STAT_SPEED 3
#define ITEM_ENIGMA_BERRY 99
#define HOLD_EFFECT_MACHO_BRACE 1
#define HOLD_EFFECT_QUICK_CLAW 2
#define FLAG_BADGE03_GET 3
#define BATTLE_TYPE_LINK 1
#define BATTLE_TYPE_SAFARI 2
#define B_SIDE_PLAYER 0
#define STATUS1_PARALYSIS 64
#define MOVE_NONE 0
#define MOVE_STRUGGLE 5
#define B_ACTION_USE_MOVE 0
#define B_ACTION_USE_ITEM 1
#define B_ACTION_SWITCH 2
#define B_ACTION_RUN 3
#define SWAP(a,b,temp) do {temp=a;a=b;b=temp;} while(0)
struct Mon {u16 speed,item,moves[4];u8 ability,statStages[8];u32 status1;} gBattleMons[4];
struct Berry {u8 holdEffect,holdEffectParam;} gEnigmaBerries[4];
struct Move {signed char priority;} gBattleMoves[6];
struct Protect {u8 noValidMoves;} gProtectStructs[4];
struct Battle {u8 chosenMovePositions[4],focusPunchBattlerId;} battle, *gBattleStruct=&battle;
u8 gActionsByTurnOrder[4],gBattlerByTurnOrder[4],gChosenActionByBattler[4],gActiveBattler,gBattlersCount=4;
u32 gBattleTypeFlags=0,gBattleWeather=0,seed,draws;u16 gRandomTurnNumber;u8 badge;
const u8 gStatStageRatios[13][2]={{2,8},{2,7},{2,6},{2,5},{2,4},{2,3},{2,2},{3,2},{4,2},{5,2},{6,2},{7,2},{8,2}};
u8 ItemId_GetHoldEffect(u16 item){return item;} u8 ItemId_GetHoldEffectParam(u16 item){return item==2?20:0;}
u8 GetBattlerSide(u8 battler){return battler&1;}u8 FlagGet(u8 flag){return badge;}
u16 Random(void){seed=seed*0x41c64e6du+0x6073u;draws++;return seed>>16;}
void CheckFocusPunch_ClearVarsBeforeTurnStarts(void){} void (*gBattleMainFunc)(void);
'''
main='''int main(void) {
for(unsigned mode=0;mode<8;mode++)for(unsigned initial=0;initial<256;initial++) {
seed=initial;draws=0;badge=0;gBattleWeather=0;gRandomTurnNumber=100;
for(unsigned i=0;i<4;i++){gBattleMons[i]=(struct Mon){.speed=50,.moves={i+1}};gBattleMons[i].statStages[STAT_SPEED]=6;gBattleMoves[i+1].priority=0;gChosenActionByBattler[i]=B_ACTION_USE_MOVE;}
if(mode==1){unsigned speeds[]={50,80,100,70};for(unsigned i=0;i<4;i++)gBattleMons[i].speed=speeds[i];}
if(mode==2||mode==7){gBattleMoves[1].priority=1;gBattleMoves[3].priority=-1;}
if(mode==3)for(unsigned i=0;i<4;i++)gBattleMoves[i+1].priority=1;
if(mode==4){gBattleMons[0].item=HOLD_EFFECT_MACHO_BRACE;gBattleMons[1].item=HOLD_EFFECT_QUICK_CLAW;}
if(mode==5){badge=1;gBattleWeather=B_WEATHER_RAIN;gBattleMons[1].ability=ABILITY_SWIFT_SWIM;gBattleMons[2].status1=STATUS1_PARALYSIS;gBattleMons[3].statStages[STAT_SPEED]=8;}
if(mode==6){gChosenActionByBattler[1]=B_ACTION_USE_ITEM;gChosenActionByBattler[2]=B_ACTION_SWITCH;}
if(mode==7){for(int i=0;i<4;i++)gBattlerByTurnOrder[i]=i;for(int i=0;i<3;i++)for(int j=i+1;j<4;j++)if(GetWhoStrikesFirst(gBattlerByTurnOrder[i],gBattlerByTurnOrder[j],FALSE))SwapTurnOrder(i,j);}
else SetActionsAndBattlersTurnOrder();
printf("%u,%u,%u,%u,%u,%u,%u,%u\\n",mode,initial,gBattlerByTurnOrder[0],gBattlerByTurnOrder[1],gBattlerByTurnOrder[2],gBattlerByTurnOrder[3],seed,draws);
}return 0;}
'''
util=(root/'src/battle_util.c').read_text()
start=util.index('        case ENDTURN_ORDER:')+len('        case ENDTURN_ORDER:')
end=util.index('            {\n                u8 *var',start)
endturn='void SourceEndTurnSort(void) { s32 i;'+util[start:end]+'}\n'
assert next(i for i in json.loads((root/'src/data/items.json').read_text())['items'] if i['itemId']=='ITEM_QUICK_CLAW')['holdEffectParam']==20
main=main.replace('for(int i=0;i<4;i++)gBattlerByTurnOrder[i]=i;for(int i=0;i<3;i++)for(int j=i+1;j<4;j++)if(GetWhoStrikesFirst(gBattlerByTurnOrder[i],gBattlerByTurnOrder[j],FALSE))SwapTurnOrder(i,j);','SourceEndTurnSort();')
source=prefix+'\n'.join(extract(s) for s in ['void SwapTurnOrder(u8 id1, u8 id2)','u8 GetWhoStrikesFirst(u8 battler1, u8 battler2, bool8 ignoreChosenMoves)','static void SetActionsAndBattlersTurnOrder(void)'])+'\n'+endturn+main
(out/'source-order-oracle.c').write_text(source)
subprocess.run(['cc','-std=c99','-O2','-Wall','-Wextra','-Wno-unused-parameter',str(out/'source-order-oracle.c'),'-o',str(out/'source-order-oracle')],check=True)
result=subprocess.check_output([str(out/'source-order-oracle')],text=True);(out/'source-order-vectors.csv').write_text(result)
(out/'source-probe.json').write_text(json.dumps({'sourceCommit':'037335f4c725d7c9aecdac87066f2002b4bd7e14','file':'src/battle_main.c','sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sourceFunctions':['GetWhoStrikesFirst','SetActionsAndBattlersTurnOrder','SwapTurnOrder','DoFieldEndTurnEffects:ENDTURN_ORDER'], 'additionalSourceHashes': {name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in ['src/battle_util.c','src/data/items.json']},'vectors':len(result.splitlines()),'scope':'Compiled extracted original C functions with explicit source-environment stubs; not original ROM execution.'},indent=2))
print('original C ordering vectors',len(result.splitlines()))
# One focused CHECK_ON_FIELD vector: source-absent retained Cloud Nine still has HP.
header=(root/'include/battle_util.h').read_text()
assert (root/'include/battle_util.h').read_bytes()==subprocess.check_output(['git','-C',str(root),'show','HEAD:include/battle_util.h'])
weather_macro=next(line for line in header.splitlines() if line.startswith('#define WEATHER_HAS_EFFECT '))
check_start=util.index('        case ABILITYEFFECT_CHECK_ON_FIELD:')
check_end=util.index('        case ABILITYEFFECT_CHECK_FIELD_EXCEPT_BATTLER:',check_start)
check_branch=util[check_start:check_end]
weather_prefix=prefix.replace('#define WEATHER_HAS_EFFECT 1',weather_macro+'\n#define ABILITYEFFECT_CHECK_ON_FIELD 19\n#define ABILITY_CLOUD_NINE 3\n#define ABILITY_AIR_LOCK 4')
weather_prefix=weather_prefix.replace('u16 speed,item,moves[4]','u16 speed,item,hp,moves[4]')
weather_prefix+='\nu8 gLastUsedAbility,gAbsentBattlerFlags;\nu8 AbilityBattleEffects(u8 caseID,u8 battler,u8 ability,u8 special,u16 move){u8 effect=0;s32 i;switch(caseID){'+check_branch+'}return effect;}\n'
weather_main='''int main(void){seed=31;draws=0;gBattleWeather=B_WEATHER_RAIN;gAbsentBattlerFlags=8;
unsigned speeds[]={50,80,60,30};for(unsigned i=0;i<4;i++){gBattleMons[i]=(struct Mon){.speed=speeds[i],.hp=100,.moves={i+1}};gBattleMons[i].statStages[STAT_SPEED]=6;gChosenActionByBattler[i]=B_ACTION_USE_MOVE;}
gBattleMons[0].ability=ABILITY_SWIFT_SWIM;gBattleMons[3].ability=ABILITY_CLOUD_NINE;gChosenActionByBattler[3]=4;
SetActionsAndBattlersTurnOrder();printf("%u,%u,%u,%u,%u,%u\\n",gBattlerByTurnOrder[0],gBattlerByTurnOrder[1],gBattlerByTurnOrder[2],gBattlerByTurnOrder[3],seed,draws);return 0;}
'''
weather_source=weather_prefix+'\n'.join(extract(s) for s in ['void SwapTurnOrder(u8 id1, u8 id2)','u8 GetWhoStrikesFirst(u8 battler1, u8 battler2, bool8 ignoreChosenMoves)','static void SetActionsAndBattlersTurnOrder(void)'])+'\n'+weather_main
(out/'source-absent-weather-oracle.c').write_text(weather_source)
subprocess.run(['cc','-std=c99','-O2','-Wall','-Wextra','-Wno-unused-parameter',str(out/'source-absent-weather-oracle.c'),'-o',str(out/'source-absent-weather-oracle')],check=True)
weather_result=subprocess.check_output([str(out/'source-absent-weather-oracle')],text=True)
(out/'source-absent-weather-vector.csv').write_text(weather_result)
print('original C absent Cloud Nine vector',weather_result.strip())
