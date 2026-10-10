from pathlib import Path
import hashlib,json,subprocess,sys
source=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()
assert commit=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
pins={
 'src/battle_util.c':'87cf810eb2d58361d1beb1537b3bd71c9160a77c2a17d703399f6ed373bb5720',
 'src/battle_main.c':'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928',
 'src/battle_script_commands.c':'3b7341db8cd58c54fb0b54b47e6eef89c5c05e8a60ab4e9b45bdc907845c19b3',
 'include/battle_util.h':'38d93e081d1886494489c0fc44bf7d77c0753b096a29f2f327a6b2909b112370',
 'include/constants/battle.h':'b9d0493af4629d450050637e9aace63b684b282f841bed2a0d97b1d71e9d55e3',
 'data/battle_scripts_1.s':'27e9c39f3147b05e5ba2ed36ca0c89fcdc9b26718778083bd362c582de800034',
 'src/data/pokemon/species_info.h':'9c2da1284f7859700b315abda0499334c422f2c6776d576c788fcd1e4050f71e',
 'src/pokemon.c':'116a470cd9ebfa226aa8d85c2eb63591e176d726837c67baaa1ac6260d50f2ec',
 'include/constants/abilities.h':'1c1b93de5d5ff65d8b3066e9b40229b5d30e02842e587e3851a1d708ece1292b',
 'src/data/pokemon/tmhm_learnsets.h':'92bf603f90469ccae0398902db943770e2cb12cb910883b42a552cff07cff31f',
 'src/data/pokemon/tutor_learnsets.h':'d09053c6fa423a6838fa1eb2c5c28701305e3895ee6b894dc46113be57019e76',
 'src/data/pokemon/level_up_learnsets.h':'d6563a88fa893d1433a02027cb13ba9574a6828a96b1a730b1d767fd7c5a0cc6'}
for name,expected in pins.items():
 assert hashlib.sha256((source/name).read_bytes()).hexdigest()==expected, 'Pinned source changed: '+name
util=(source/'src/battle_util.c').read_text();main=(source/'src/battle_main.c').read_text();cmd=(source/'src/battle_script_commands.c').read_text();script=(source/'data/battle_scripts_1.s').read_text()
entry=util[util.index('            case ABILITY_DRIZZLE:'):util.index('            case ABILITY_INTIMIDATE:',util.index('            case ABILITY_DRIZZLE:'))]
rain=util[util.index('                case ABILITY_RAIN_DISH:'):util.index('                case ABILITY_SHED_SKIN:',util.index('                case ABILITY_RAIN_DISH:'))]
constants='\n'.join(l for l in (source/'include/constants/battle.h').read_text().splitlines() if l.startswith('#define B_WEATHER_'))
abilities='\n'.join(l for l in (source/'include/constants/abilities.h').read_text().splitlines() if any(l.startswith('#define '+a+' ') for a in ['ABILITY_DRIZZLE','ABILITY_DROUGHT','ABILITY_SAND_STREAM','ABILITY_RAIN_DISH']))
harness='''#include <stdio.h>
'''+constants+'\n'+abilities+'''
int gBattleWeather,gBattleMoveDamage,gLastUsedAbility,gBattlerAttacker,callbacks,suppressed;
struct {int hp,maxHP;} gBattleMons[2];
struct {int battler;} gBattleScripting;
int BattleScript_DrizzleActivates,BattleScript_SandstreamActivates,BattleScript_DroughtActivates,BattleScript_RainDishActivates;
#define WEATHER_HAS_EFFECT (!suppressed)
void BattleScriptPushCursorAndCallback(int script){(void)script;callbacks++;}
int main(void){int mode,ability,weather,hp,maximum;while(scanf("%d%d%d%d%d%d",&mode,&ability,&weather,&hp,&maximum,&suppressed)==6){
int effect=0,battler=0;callbacks=0;gBattleMoveDamage=0;gBattleWeather=weather;
gBattleMons[0].hp=hp;gBattleMons[0].maxHP=maximum;
if(mode==0){switch(ability){
'''+entry+'''
}}else if(gBattleMons[battler].hp!=0){gBattlerAttacker=battler;switch(ability){
'''+rain+'''
}}
printf("%d %d %d %d\\n",gBattleWeather,effect,callbacks,-gBattleMoveDamage);
}return 0;}
'''
(out/'weather-extract.c').write_text(harness)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'weather-extract.c'),'-o',str(out/'weather-extract')],check=True)
abilityIds={k:int(l.split()[2]) for l in abilities.splitlines() for k in [l.split()[1]]}
entries=[(0,abilityIds[a],w,100,200,0,a) for a in ['ABILITY_DRIZZLE','ABILITY_DROUGHT','ABILITY_SAND_STREAM'] for w in (0,1,5,8,24,32,96,128)]
rains=[(1,abilityIds['ABILITY_RAIN_DISH'],5 if rain else 0,hp,maximum,suppressed,'ABILITY_RAIN_DISH') for maximum in (15,16,160) for hp in (0,1,maximum-1,maximum) for rain in (0,1) for suppressed in (0,1)]
# 24 source entry vectors and 48 Rain Dish vectors. Include each transient weather encoding in Rain Dish checks.
rains += [(1,abilityIds['ABILITY_RAIN_DISH'],weather,hp,maximum,suppressed,'ABILITY_RAIN_DISH') for maximum in (15,160) for hp in (1,maximum-1) for weather in (1,8,32) for suppressed in (0,1)]
rows=entries+rains
result=subprocess.run([str(out/'weather-extract')],input=''.join(' '.join(map(str,r[:6]))+'\n' for r in rows),text=True,capture_output=True,check=True)
entryRows=[];rainRows=[]
for row,line in zip(rows,result.stdout.splitlines(),strict=True):
 after,effect,callbacks,recovery=map(int,line.split())
 if row[0]==0:entryRows.append(dict(ability=row[6],before=row[2],after=after,effect=effect,callbacks=callbacks))
 else:rainRows.append(dict(hp=row[3],maximum=row[4],rain=int(bool(row[2]&7)),suppressed=row[5],recovery=recovery,effect=effect))
assert len(entryRows)==24 and len(rainRows)==72
check=util[util.index('        case ABILITYEFFECT_CHECK_ON_FIELD:'):util.index('        case ABILITYEFFECT_CHECK_FIELD_EXCEPT_BATTLER:')]
sport=util[util.index('        case ABILITYEFFECT_FIELD_SPORT:'):util.index('        case ABILITYEFFECT_CHECK_ON_FIELD:')]
assert 'gBattleMons[i].hp != 0' in check and '.hp' not in sport and 'gAbsentBattlerFlags' not in check+sport
assert 'if (GetWhoStrikesFirst(gBattlerByTurnOrder[i], gBattlerByTurnOrder[j], TRUE) != 0)' in main
assert 'if (!(gBattleWeather & B_WEATHER_RAIN_PERMANENT))' in util and '&& --gWishFutureKnock.weatherDuration == 0' in util
assert script.index('BattleScript_EffectRolePlay::')<script.index('BattleScript_EffectWish::')
for start,end in [('BattleScript_EffectRolePlay::','BattleScript_EffectWish::'),('BattleScript_EffectSkillSwap::','BattleScript_EffectImprison::')]:
 assert 'switchineffects' not in script[script.index(start):script.index(end)]
paths=list(pins)
data=dict(sourceCommit=commit,sha256={p:hashlib.sha256((source/p).read_bytes()).hexdigest() for p in paths},extractSha256=hashlib.sha256((entry+rain).encode()).hexdigest(),entryRows=entryRows,rainRows=rainRows)
(out/'source-vectors.json').write_text(json.dumps(data,indent=2)+'\n')
print(f'PASS {len(entryRows)+len(rainRows)} original C vectors; source query/order/entry-change pins verified')
