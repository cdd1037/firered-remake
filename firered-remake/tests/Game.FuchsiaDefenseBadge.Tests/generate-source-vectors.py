#!/usr/bin/env python3
"""Extract pinned CalculateBaseDamage unchanged; compile only this local harness."""
import argparse, hashlib, json, pathlib, subprocess, tempfile
p=argparse.ArgumentParser(); p.add_argument('--reference-root',required=True); p.add_argument('--check',action='store_true'); a=p.parse_args()
ref=pathlib.Path(a.reference_root); out=pathlib.Path(__file__).resolve().parent
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
paths=['src/pokemon.c','src/battle_util.c','src/battle_script_commands.c']
headers=['pokemon','battle','abilities','items','hold_effects','species','moves','battle_move_effects','opponents','trainers','flags']
paths += ['include/constants/'+x+'.h' for x in headers]
files={x:subprocess.check_output(['git','-C',str(ref),'show',pin+':'+x]) for x in paths}
s=files['src/pokemon.c'].decode()
start=s.index('#define APPLY_STAT_MOD'); end=s.index('\nu8 CountAliveMonsInBattle(',start)
function=s[start:end]
assert function.count('s32 CalculateBaseDamage(')==1
ratios=s[s.index('const u8 gStatStageRatios'):s.index('static const u8 sHoldEffectToType')]
ratios=ratios[:ratios.index('};')+2]
start=s.index('static const u8 sHoldEffectToType'); items=s[start:s.index('};',start)+2]
header=r'''#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32;
'''+''.join('#include "constants/'+h+'.h"\n' for h in headers)+r'''
#define ARRAY_COUNT(x) (sizeof(x)/sizeof((x)[0]))
#define IS_TYPE_PHYSICAL(t) ((t)<TYPE_MYSTERY)
#define IS_TYPE_SPECIAL(t) ((t)>TYPE_MYSTERY)
#define ABILITY_ON_FIELD2(a) 0
#define WEATHER_HAS_EFFECT2 1
#define RESOURCE_FLAG_FLASH_FIRE 1
#define DYNAMIC_TYPE_MASK 0x3F
#define ABILITYEFFECT_FIELD_SPORT 14
#define ABILITYEFFECT_MUD_SPORT 253
#define ABILITYEFFECT_WATER_SPORT 254
#define BATTLE_ALIVE_DEF_SIDE 2
#define MOVE_TARGET_BOTH (1 << 3)
struct BattlePokemon {u16 attack,defense,spAttack,spDefense,species,item,hp,maxHP;u8 ability,level,statStages[8];u32 status1;};
struct {u8 holdEffect,holdEffectParam;} gEnigmaBerries[2];
struct ResourcesFlags {u8 flags[2];}; struct Resources {struct ResourcesFlags *flags;};
struct ResourcesFlags resourceFlags;struct Resources resourceValue={&resourceFlags};struct Resources *gBattleResources=&resourceValue;
struct {u16 power;u8 type,effect,target;} gBattleMoves[355];
u32 gBattleTypeFlags;u16 gBattleMovePower,gCurrentMove,gBattleWeather;u8 gCritMultiplier;int badge;
int FlagGet(int flag){return flag==FLAG_BADGE05_GET && badge;}
int GetBattlerSide(int side){return side&1;}
u8 ItemId_GetHoldEffect(u16 item){return item==ITEM_METAL_POWDER?HOLD_EFFECT_METAL_POWDER:HOLD_EFFECT_NONE;}
u8 ItemId_GetHoldEffectParam(u16 item){(void)item;return 0;}
int AbilityBattleEffects(int a,int b,int c,int d,int e){(void)a;(void)b;(void)c;(void)d;(void)e;return 0;}
u8 CountAliveMonsInBattle(u8 mode){(void)mode;return 1;}
'''
main=r'''
int main(void){int first=1;printf("[");int defenses[]={9,10,19,29,31,99,199};int stages[]={3,6,7};
for(int d=0;d<7;d++)for(int explosion=0;explosion<2;explosion++)for(int stage=0;stage<3;stage++)
for(int critical=0;critical<2;critical++)for(int burn=0;burn<2;burn++)for(int reflect=0;reflect<2;reflect++)
for(int metal=0;metal<2;metal++)for(badge=0;badge<2;badge++)for(int special=0;special<2;special++){
struct BattlePokemon a={0},b={0};a.attack=a.spAttack=157;a.level=43;a.hp=a.maxHP=200;a.status1=burn?STATUS1_BURN:0;
b.defense=b.spDefense=defenses[d];b.species=metal?SPECIES_DITTO:SPECIES_RATTATA;b.item=metal?ITEM_METAL_POWDER:ITEM_NONE;
for(int j=0;j<8;j++){a.statStages[j]=6;b.statStages[j]=6;}b.statStages[STAT_DEF]=b.statStages[STAT_SPDEF]=stages[stage];
gCurrentMove=1;gCritMultiplier=critical?2:1;gBattleMoves[1].power=special?(explosion?90:40):(explosion?200:35);
gBattleMoves[1].type=special?TYPE_PSYCHIC:TYPE_NORMAL;gBattleMoves[1].effect=explosion&&!special?EFFECT_EXPLOSION:0;
int damage=CalculateBaseDamage(&a,&b,1,reflect?SIDE_STATUS_REFLECT:0,0,0,1,0);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",defenses[d],explosion,stages[stage],critical,burn,reflect,metal,badge,special,damage);first=0;
}printf("]\n");return 0;}
'''
with tempfile.TemporaryDirectory(prefix='firered-defense-oracle-') as temp:
    temp=pathlib.Path(temp); (temp/'constants').mkdir()
    for h in headers: (temp/'constants'/f'{h}.h').write_bytes(files[f'include/constants/{h}.h'])
    (temp/'oracle.c').write_text(header+ratios+'\n'+items+'\n'+function+main)
    subprocess.run(['cc','-std=c99','-O0','-Wall','-Wextra',str(temp/'oracle.c'),'-I',str(temp),'-o',str(temp/'oracle')],check=True)
    vectors=json.loads(subprocess.check_output([str(temp/'oracle')]))
result={'sourceCommit':pin,'files':{x:hashlib.sha256(v).hexdigest() for x,v in files.items()},'functionSha256':hashlib.sha256(function.encode()).hexdigest(),'vectorColumns':['defense','explosion','defenseStage','critical','burn','reflect','metalPowder','defenseBadge','special','sourceBaseDamage'],'vectors':vectors}
assert len(vectors)==2688
if a.check:
    assert json.loads((out/'source-vectors.json').read_text()) == result, 'Pinned source vectors differ; fixture was not overwritten'
else:
    (out/'source-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
print(json.dumps({'sourceCommit':pin,'vectors':len(vectors),'unchangedSourceFunctionSha256':result['functionSha256']}))
