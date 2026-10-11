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
pins.update({'src/battle_gfx_sfx_util.c':'c4c009637d19310decd87c89781f6798f677e727fc6acd0a0e6071d74ae23b3b','include/constants/pokemon.h':'9cabd203100ae7246bc595b7295e7e04145f6292abfab83c3bd0597a2cfbcfb5','include/constants/species.h':'ec3f9f9c2717dcd453aa7a27506c32e1e6ca067864534dee494695d74344c51a'})
for name,expected in pins.items():
 assert hashlib.sha256((source/name).read_bytes()).hexdigest()==expected, 'Pinned source changed: '+name

util=(source/'src/battle_util.c').read_text();script=(source/'data/battle_scripts_1.s').read_text()
forecast=util[util.index('enum\n{\n    CASTFORM_NO_CHANGE'):util.index('u8 AbilityBattleEffects')]
trace=util[util.index('        case ABILITYEFFECT_TRACE:'):util.index('        case ABILITYEFFECT_INTIMIDATE2:')]
constants='\n'.join(l for l in (source/'include/constants/battle.h').read_text().splitlines() if l.startswith('#define B_WEATHER_'))
abilities='\n'.join(l for l in (source/'include/constants/abilities.h').read_text().splitlines() if l.startswith('#define ABILITY_'))
types='\n'.join(l for l in (source/'include/constants/pokemon.h').read_text().splitlines() if l.startswith('#define TYPE_'))
harness='#include <stdio.h>\n#include <string.h>\ntypedef unsigned char u8;\n'+constants+'\n'+abilities+'\n'+types+r"""
#define SPECIES_CASTFORM 385
#define ABILITYEFFECT_TRACE 11
#define STATUS3_TRACE 1
#define BIT_SIDE 1
#define BIT_FLANK 2
#define BATTLE_TYPE_DOUBLE 1
struct {int species,ability,hp,type1,type2;} gBattleMons[4];
int gBattleWeather,suppressed,gBattlersCount,gBattleTypeFlags,gStatuses3[4],gActiveBattler,gLastUsedAbility,draws,raw;
struct {int battler;} gBattleScripting;
int BattleScript_TraceActivates;
#define WEATHER_HAS_EFFECT (!suppressed)
#define IS_BATTLER_OF_TYPE(i,t) (gBattleMons[i].type1==(t)||gBattleMons[i].type2==(t))
#define SET_BATTLER_TYPE(i,t) (gBattleMons[i].type1=gBattleMons[i].type2=(t))
#define PREPARE_MON_NICK_WITH_PREFIX_BUFFER(a,b,c) ;
#define PREPARE_ABILITY_BUFFER(a,b) ;
void BattleScriptPushCursorAndCallback(int script){(void)script;}
int GetBattlerPosition(int i){return i;}
int GetBattlerAtPosition(int i){return i;}
int Random(void){draws++;return raw;}
"""+forecast+r"""
int main(void){int mode;while(scanf("%d",&mode)==1){memset(gBattleMons,0,sizeof(gBattleMons));memset(gStatuses3,0,sizeof(gStatuses3));draws=0;
if(mode==0){scanf("%d%d%d%d%d%d%d",&gBattleMons[0].species,&gBattleMons[0].ability,&gBattleMons[0].hp,&suppressed,&gBattleWeather,&gBattleMons[0].type1,&gBattleMons[0].type2);
int result=CastformDataTypeChange(0);printf("%d %d %d\n",result,gBattleMons[0].type1,gBattleMons[0].type2);
}else{int tracer,a,ah,b,bh;scanf("%d%d%d%d%d%d%d",&gBattlersCount,&tracer,&a,&ah,&b,&bh,&raw);
gBattleTypeFlags=gBattlersCount==4?BATTLE_TYPE_DOUBLE:0;gBattleMons[tracer].ability=ABILITY_TRACE;gBattleMons[tracer].hp=30;gStatuses3[tracer]=STATUS3_TRACE;
int first=(tracer^1)&1;gBattleMons[first].ability=a;gBattleMons[first].hp=ah;gBattleMons[first+2].ability=b;gBattleMons[first+2].hp=bh;
int effect=0,i,side,target1;gActiveBattler=-1;switch(ABILITYEFFECT_TRACE){
"""+trace+r"""
}
printf("%d %d %d %d %d\n",effect,effect?gActiveBattler:-1,gBattleMons[tracer].ability,draws,gStatuses3[tracer]);
}}return 0;}
"""
(out/'trace-forecast-extract.c').write_text(harness)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'trace-forecast-extract.c'),'-o',str(out/'trace-forecast-extract')],check=True)
abilityNames={int(l.split()[2]):l.split()[1] for l in abilities.splitlines() if len(l.split())==3 and l.split()[2].isdigit()}
typeNames={int(l.split()[2]):l.split()[1] for l in types.splitlines() if len(l.split())==3 and l.split()[2].isdigit()}
forecasts=[(0,sp,ab,hp,sup,w,a,b) for sp in (385,137) for ab in (59,36) for hp in (0,30) for sup in (0,1) for w in (0,5,96,128,24,101,133,229) for a,b in ((0,0),(10,10),(11,11),(15,15),(12,12),(0,10),(11,12))]
donors=((0,30),(33,0),(25,30),(36,30),(59,30),(2,30))
traces=[(1,n,tr,a,ah,b,bh,raw) for n in (2,4) for tr in range(n) for a,ah in donors for b,bh in (donors if n==4 else ((0,0),)) for raw in (0,1,65535)]
rows=forecasts+traces
result=subprocess.run([str(out/'trace-forecast-extract')],input=''.join(' '.join(map(str,row))+'\n' for row in rows),capture_output=True,text=True,check=True)
frows=[];trows=[]
for row,line in zip(rows,result.stdout.splitlines(),strict=True):
 if row[0]==0:
  form,a,b=map(int,line.split());frows.append(dict(castform=row[1]==385,ability=abilityNames[row[2]],hp=row[3],suppressed=bool(row[4]),weather=row[5],type1=typeNames[row[6]],type2=typeNames[row[7]],change=form,after1=typeNames[a],after2=typeNames[b]))
 else:
  effect,target,ability,draws,pending=map(int,line.split());trows.append(dict(count=row[1],tracer=row[2],a=abilityNames[row[3]],ah=row[4],b=abilityNames[row[5]],bh=row[6],raw=row[7],effect=effect,target=target,ability=abilityNames[ability],draws=draws,pending=pending))
assert frows and trows
traceScript=script[script.index('BattleScript_TraceActivates::'):script.index('BattleScript_RainDishActivates::')]
assert 'switchineffects' not in traceScript and 'end3' in traceScript
assert 'gBattleMonForms[battlerAtk] = gBattleMonForms[battlerDef];' in (source/'src/battle_gfx_sfx_util.c').read_text()
data=dict(sourceCommit=commit,sha256=pins,extractSha256=hashlib.sha256((forecast+trace).encode()).hexdigest(),forecastRows=frows,traceRows=trows)
(out/'source-vectors.json').write_text(json.dumps(data,indent=2)+'\n')
print(f'PASS {len(frows)} Forecast and {len(trows)} Trace vectors from original C branches')
