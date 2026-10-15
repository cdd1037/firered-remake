#!/usr/bin/env python3
"""Compile original source fragments, with only unrelated host interfaces stubbed."""
import argparse,hashlib,json,pathlib,re,subprocess,tempfile
R=pathlib.Path(__file__).resolve().parents[1]; O=R; commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
p=argparse.ArgumentParser();p.add_argument('--reference',default='/workspace/scratch/18c02ed4ed0d/firered-reference-20261010/pokefirered');p.add_argument('--check',action='store_true');a=p.parse_args()
paths=['src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','src/pokemon.c','src/data/battle_moves.h','src/data/items.json','data/battle_scripts_1.s','data/battle_ai_scripts.s','src/random.c','include/random.h']
raw=O/'content/raw/pokefirered/037335f4';lockpath=O/'content/rocket-hideout-battle-source-lock.json'
if not lockpath.exists():
 entries=[]
 for x in paths:
  b=subprocess.check_output(['git','-C',a.reference,'show',commit+':'+x]);q=raw/x;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
  entries.append(dict(path=x))
 lockpath.write_text(json.dumps(dict(schemaVersion=1,profile='firered-rocket-hideout-battle-source-v1',sourceCommit=commit,files=entries),indent=2)+'\n')
lock=json.loads(lockpath.read_text());assert lock['sourceCommit']==commit and [e['path'] for e in lock['files']]==paths
sources={}
for e in lock['files']:
 b=(raw/e['path']).read_bytes();sources[e['path']]=b.decode()
def block(text,start):
 i=text.index('{',start);n=1;j=i+1
 while n:n+=(text[j]=='{')-(text[j]=='}');j+=1
 return text[start:j]
util=sources['src/battle_util.c'];start=util.index('                    u8 toSub;');end=util.index('                    if (gBattleMons[gBattlerAttacker].status1 & STATUS1_SLEEP)',start);sleep=util[start:end]
commands=sources['src/battle_script_commands.c'];fake=block(commands,commands.index('static void Cmd_jumpifnotfirstturn(void)\n{'))
main=sources['src/battle_main.c'];counter=re.search(r'            if \(gDisableStructs\[gActiveBattler\].isFirstTurn\)\n                --gDisableStructs\[gActiveBattler\].isFirstTurn;',main).group(0)
boost=re.search(r'spAttack = \(spAttack \* \(attackerHoldEffectParam \+ 100\)\) / 100;',sources['src/pokemon.c']).group(0)
random=block(sources['src/random.c'],sources['src/random.c'].index('u16 Random(void)'))
macro=re.search(r'^#define RAND_MULT .*$' ,sources['include/random.h'],re.M).group(0)+'\n'+re.search(r'^#define ISO_RANDOMIZE1\(.*$',sources['include/random.h'],re.M).group(0)
assert 'jumpifnotfirstturn BattleScript_ButItFailedAtkStringPpReduce\n\tsetmoveeffect MOVE_EFFECT_FLINCH | MOVE_EFFECT_CERTAIN\n\tgoto BattleScript_EffectHit' in sources['data/battle_scripts_1.s']
assert 'if (gBattleCommunication[MOVE_EFFECT_BYTE] & MOVE_EFFECT_CERTAIN\n        && !(gMoveResultFlags & MOVE_RESULT_NO_EFFECT))' in commands
assert 'AI_CBM_FakeOut::\n\tis_first_turn_for AI_USER\n\tif_equal 0, Score_Minus10\n\tend' in sources['data/battle_ai_scripts.s']
c='''#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;
#define STATUS1_SLEEP 7
#define ABILITY_EARLY_BIRD 48
struct {u32 status1;int ability;} gBattleMons[1];int gBattlerAttacker=0,gActiveBattler=0;
struct {int isFirstTurn;} gDisableStructs[1];u8 script[32],failed[32];const u8 *gBattlescriptCurrInstr;
#define T1_READ_PTR(x) (failed)
u32 gRngValue;
'''+macro+'\n'+random+'\n'+fake+'''
int Sleep(int before,int bird){gBattleMons[0].status1=before;gBattleMons[0].ability=bird?ABILITY_EARLY_BIRD:0;
'''+sleep+'''
return gBattleMons[0].status1;}
int First(int before){gDisableStructs[0].isFirstTurn=before;gBattlescriptCurrInstr=script;Cmd_jumpifnotfirstturn();return gBattlescriptCurrInstr==script+5;}
int Cleanup(int before){gDisableStructs[0].isFirstTurn=before;
'''+counter+'''
return gDisableStructs[0].isFirstTurn;}
int Boost(int basis){int spAttack=basis,attackerHoldEffectParam=10;
'''+boost+'''
return spAttack;}
int main(void){
printf("{\\\"sleep\\\":[");for(int bird=0;bird<2;bird++)for(int before=0;before<=7;before++)printf("%s%d",bird||before?",":"",Sleep(before,bird));
printf("],\\\"firstTurn\\\":[");for(int n=0;n<=2;n++)printf("%s[%d,%d]",n?",":"",First(n),Cleanup(n));
printf("],\\\"boost\\\":[");for(int n=0;n<=512;n++)printf("%s%d",n?",":"",Boost(n));
printf("],\\\"fakeOutRng\\\":[");for(int seed=0;seed<64;seed++){gRngValue=seed;int draws=0;u16 raw;do{raw=Random();draws++;}while((raw&3)!=0);int choices=draws;int accuracy=Random(),critical=Random(),damage=Random(),ready=Random();printf("%s{\\\"seed\\\":%d,\\\"choices\\\":%d,\\\"accuracy\\\":%d,\\\"critical\\\":%d,\\\"damage\\\":%d,\\\"ready\\\":%d,\\\"after\\\":%u}",seed?",":"",seed,choices,accuracy,critical,damage,ready,gRngValue);}
printf("]}\\n");return 0;}
'''
# C string escaping remains plain source. No game or Domain code is called.
with tempfile.TemporaryDirectory(prefix='rocket-source-oracle-',dir=None) as d:
 src=pathlib.Path(d)/'oracle.c';src.write_text(c);exe=pathlib.Path(d)/'oracle';subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(src),'-o',str(exe)],check=True);data=json.loads(subprocess.check_output([str(exe)],text=True))
data['sourceCommit']=commit;data['sources']=[e['path'] for e in lock['files']];data['oracle']='Direct source Early Bird countdown block, Cmd_jumpifnotfirstturn, source first-turn cleanup, special-attack item arithmetic, and Random. Fake Out RNG driver follows the original successful first-turn script against one-slot Splash and a slower nonimmune target; GUI/controller calls are excluded.'
output=O/'tests/Game.RocketHideoutDomain.Tests/source-vectors.json';output.parent.mkdir(parents=True,exist_ok=True);b=(json.dumps(data,separators=(',',':'))+'\n').encode()
if a.check:assert output.read_bytes()==b,'Oracle output differs';assert output.with_suffix('.c').read_text()==c
else:output.write_bytes(b);output.with_suffix('.c').write_text(c)
print(json.dumps(dict(sleep=len(data['sleep']),firstTurn=len(data['firstTurn']),boost=len(data['boost']),rng=len(data['fakeOutRng']),sha256=hashlib.sha256(b).hexdigest())))
