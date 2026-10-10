from pathlib import Path
import hashlib,json,subprocess,sys
source=Path(sys.argv[1]); out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()
assert commit=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
util=(source/'src/battle_util.c').read_text();main=(source/'src/battle_main.c').read_text();cmd=(source/'src/battle_script_commands.c').read_text()
start=util.index('                case ABILITY_SPEED_BOOST:',util.index('case ABILITYEFFECT_ENDTURN:'))
end=util.index('\n                }',start)
blocks=util[start:end]
assert 'truantCounter ^= 1' in blocks
# Compile original, unedited switch case statements. Only surrounding globals and callback are stubs.
harness='''#include <stdio.h>
#define ABILITY_SPEED_BOOST 3
#define ABILITY_TRUANT 54
#define STAT_SPEED 3
#define MAX_STAT_STAGE 12
struct { int hp; int statStages[8]; } gBattleMons[2];
struct { int isFirstTurn; int truantCounter; } gDisableStructs[2];
struct { int animArg1,animArg2,battler; } gBattleScripting;
int gBattlerAttacker, callbacks;
int BattleScript_SpeedBoostActivates;
void BattleScriptPushCursorAndCallback(int script) {(void)script;callbacks++;}
int main(void) { int ability,hp,speed,first,truant;
while(scanf("%d%d%d%d%d",&ability,&hp,&speed,&first,&truant)==5){
int battler=0,effect=0; callbacks=0;
gBattleMons[0].hp=hp;gBattleMons[0].statStages[STAT_SPEED]=speed;
gDisableStructs[0].isFirstTurn=first;gDisableStructs[0].truantCounter=truant;
if(gBattleMons[battler].hp!=0) { gBattlerAttacker=battler;switch(ability){
'''+blocks+'''
}}
printf("%d %d %d %d\\n",gBattleMons[0].statStages[STAT_SPEED],gDisableStructs[0].truantCounter,effect,callbacks);
}return 0;}
'''
(out/'endturn-extract.c').write_text(harness)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'endturn-extract.c'),'-o',str(out/'endturn-extract')],check=True)
inputs=[(a,h,s,f,t) for a in (3,54) for h in (0,1) for s in (6,11,12) for f in (0,1,2) for t in (0,1)]
res=subprocess.run([str(out/'endturn-extract')],input=''.join(' '.join(map(str,x))+'\n' for x in inputs),text=True,capture_output=True,check=True)
rows=[]
for i,line in zip(inputs,res.stdout.splitlines(),strict=True):
 speed,truant,effect,callbacks=map(int,line.split());rows.append(dict(ability=i[0],hp=i[1],speed=i[2],first=i[3],truant=i[4],afterSpeed=speed,afterTruant=truant,effect=effect,callbacks=callbacks))
assert len(rows)==72
order=['CANCELLER_ASLEEP','CANCELLER_FROZEN','CANCELLER_TRUANT','CANCELLER_RECHARGE','CANCELLER_FLINCH','CANCELLER_CONFUSED','CANCELLER_PARALYSED']
assert [util.index('case '+s+':') for s in order]==sorted(util.index('case '+s+':') for s in order)
assert '&& !(gDisableStructs[gBattlerAttacker].truantCounter)' in main
assert 'gDisableStructs[gActiveBattler].truantCounter = 1;' in cmd
assert '&& !gDisableStructs[gBattlerTarget].truantCounter' in cmd
assert 'if (gDisableStructs[gActiveBattler].rechargeTimer == 0)' in main
# Extract the original cleanup timer branch too; no rewritten countdown formula.
start=main.index('            if (gDisableStructs[gActiveBattler].rechargeTimer)')
end=main.index('\n        }',start)
cleanup=main[start:end]
assert 'gDisableStructs[gActiveBattler].rechargeTimer = 2;' in cmd.replace('gEffectBattler','gActiveBattler')
cleanup_c='#include <stdio.h>\n#define STATUS2_RECHARGE 0x400000\nstruct { int status2; } gBattleMons[2];\nstruct { int rechargeTimer; } gDisableStructs[2];\nint gActiveBattler;\nint main(void) { int timer; while(scanf("%d",&timer)==1){\ngDisableStructs[0].rechargeTimer=timer;gBattleMons[0].status2=STATUS2_RECHARGE;\n'+cleanup+'\nprintf("%d %d\\n",gDisableStructs[0].rechargeTimer,(gBattleMons[0].status2 & STATUS2_RECHARGE)!=0);\n}return 0;}\n'
(out/'recharge-extract.c').write_text(cleanup_c)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'recharge-extract.c'),'-o',str(out/'recharge-extract')],check=True)
recharge=subprocess.run([str(out/'recharge-extract')],input='1\n2\n',text=True,capture_output=True,check=True)
recharge_rows=[dict(before=before,after=int(line.split()[0]),recharging=bool(int(line.split()[1]))) for before,line in zip((1,2),recharge.stdout.splitlines(),strict=True)]
result=dict(sourceCommit=commit,extractSha256=hashlib.sha256(blocks.encode()).hexdigest(),cancellerOrder=order,rows=rows,rechargeRows=recharge_rows)
(out/'source-vectors.json').write_text(json.dumps(result,indent=2)+'\n')
print('PASS 72 source C end-turn vectors; pinned cancellation, setup, switch and recharge statements verified')
