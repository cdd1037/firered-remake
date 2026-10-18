from pathlib import Path as _ReferencePath
import sys as _reference_sys
_reference_sys.path.insert(0, str(next(p for p in _ReferencePath(__file__).resolve().parents if (p / 'Reference/reference_c.py').is_file()) / 'Reference'))
from reference_c import compile_c, verify_reference
from pathlib import Path
import hashlib,json,re,subprocess,sys
import atexit
import tempfile
fixtures_only = "--fixtures-only" in sys.argv
arguments = [argument for argument in sys.argv[1:] if argument != "--fixtures-only"]
if len(arguments) != 2:
    raise SystemExit("Usage: source_probe.py PINNED_REFERENCE OUTPUT_DIRECTORY [--fixtures-only]")
source, fixture_output = (Path(argument).resolve() for argument in arguments)
fixture_output.mkdir(parents=True, exist_ok=True)
if fixtures_only:
    temporary_build = tempfile.TemporaryDirectory(prefix="firered-items-reference-")
    atexit.register(temporary_build.cleanup)
    out = Path(temporary_build.name)
else:
    out = fixture_output

commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
verify_reference(source)
# Verify only the six directly exercised source files, including item definitions and constants.
names=['src/battle_util.c','src/battle_script_commands.c','data/battle_scripts_1.s','src/data/items.json','include/constants/hold_effects.h','include/constants/battle.h']
pins={}
for name in names:
 raw=(source/name).read_bytes()
 pins[name]=hashlib.sha256(raw).hexdigest()
util=(source/names[0]).read_text();commands=(source/names[1]).read_text();scripts=(source/names[2]).read_text()
items=json.loads((source/names[3]).read_text())['items']
berries=['ORAN','CHERI','CHESTO','PECHA','RAWST','ASPEAR','PERSIM','SPELON','PAMTRE','WATMEL','DURIN','BELUE']
selected={b:next(x for x in items if x['itemId']=='ITEM_'+b+'_BERRY') for b in berries}
assert selected['ORAN']['holdEffect']=='HOLD_EFFECT_RESTORE_HP' and selected['ORAN']['holdEffectParam']==10
assert all(selected[b]['holdEffect']=='HOLD_EFFECT_NONE' for b in berries[7:])
normal=util.split('    case ITEMEFFECT_NORMAL:')[1].split('    case ITEMEFFECT_DUMMY:')[0]
moveend=util.split('    case ITEMEFFECT_MOVE_END:')[1].split('    case ITEMEFFECT_KINGSROCK_SHELLBELL:')[0]
cases=['RESTORE_HP','CURE_PAR','CURE_SLP','CURE_PSN','CURE_BRN','CURE_FRZ','CURE_CONFUSION']
def extract(text,case):
 start=text.index('            case HOLD_EFFECT_'+case+':');end=text.find('            case HOLD_EFFECT_',start+1)
 assert end>=0
 return text[start:end]
normalcases=''.join(extract(normal,x) for x in cases)
movecases=''.join(extract(moveend,x) for x in cases[1:])
symbols=sorted(set(re.findall(r'BattleScript_[A-Za-z0-9_]+',normalcases+movecases)))
harness='''#include <stdio.h>
#include <string.h>
'''+''.join('#include "'+str(source/'include/constants'/f)+'"\n' for f in ('battle.h','hold_effects.h'))+'''
#define ITEM_HP_CHANGE 1
#define ITEM_STATUS_CHANGE 2
#define ITEM_EFFECT_OTHER 3
struct {int hp,maxHP;unsigned status1,status2;} gBattleMons[1];
int effect,battlerId,battlerHoldEffect,battlerHoldEffectParam,moveTurn,gBattleMoveDamage,gBattlescriptCurrInstr;
void BattleScriptExecute(int script){(void)script;}
void BattleScriptPushCursor(void){}
'''+''.join('int '+x+';\n' for x in symbols)+'''
void originalNormal(void){if(gBattleMons[battlerId].hp){switch(battlerHoldEffect){
'''+normalcases+'''}}}
void originalMoveEnd(void){switch(battlerHoldEffect){
'''+movecases+'''}}
int main(void){int phase,hold,param,hp,max;unsigned status,confusion;
while(scanf("%d%d%d%d%d%u%u",&phase,&hold,&param,&hp,&max,&status,&confusion)==7){
memset(gBattleMons,0,sizeof(gBattleMons));gBattleMons[0].hp=hp;gBattleMons[0].maxHP=max;
gBattleMons[0].status1=status;gBattleMons[0].status2=confusion;effect=gBattleMoveDamage=0;
battlerHoldEffect=hold;battlerHoldEffectParam=param;moveTurn=phase==1;
if(phase==2)originalMoveEnd();else originalNormal();
printf("%d %u %u %d\\n",effect,gBattleMons[0].status1,gBattleMons[0].status2,-gBattleMoveDamage);
}return 0;}
'''
(out/'original-berry-cases.c').write_text(harness)
compile_c(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'original-berry-cases.c'),'-o',str(out/'original-berry-cases')])
# A bounded boundary matrix, not random bulk coverage: all effects and relevant threshold/timing edges.
def hold(b):
 symbol=selected[b]['holdEffect'];return int(re.search(r'#define\s+'+symbol+r'\s+(\d+)',(source/names[4]).read_text())[1])
rows=[]
for b,status,confusion in [('CHERI',64,0),('CHESTO',3,0),('PECHA',8,0),('PECHA',128+3840,0),('RAWST',16,0),('ASPEAR',32,0),('PERSIM',0,4)]:
 for phase in [0,1,2]:rows.append((b,phase,40,81,status,confusion))
for phase,hp,maxhp in [(0,40,81),(0,41,81),(1,40,81),(2,40,81),(0,0,81),(0,5,11)]:rows.append(('ORAN',phase,hp,maxhp,0,0))
for phase in [0,1,2]:rows.append(('PECHA',phase,0,81,8,0))
for b in berries[7:]:rows.append((b,0,1,81,8,4))
inputs=[' '.join(map(str,[phase,hold(b),selected[b]['holdEffectParam'],hp,maxhp,status,confusion])) for b,phase,hp,maxhp,status,confusion in rows]
outputs=subprocess.run([str(out/'original-berry-cases')],input='\n'.join(inputs)+'\n',text=True,capture_output=True,check=True).stdout.splitlines()
vectors=[dict(item='ITEM_'+r[0]+'_BERRY',phase=r[1],hp=r[2],maxHp=r[3],status=r[4],confusion=r[5],effect=int(o.split()[0]),afterStatus=int(o.split()[1]),afterConfusion=int(o.split()[2]),healed=int(o.split()[3])) for r,o in zip(rows,outputs,strict=True)]
assert len(vectors)==35
switch=scripts.split('BattleScript_ActionSwitch::')[1].split('\n\tend2')[0]
assert 'moveendcase MOVEEND_IMMUNITY_ABILITIES' in switch and 'MOVEEND_ITEM_EFFECTS_ALL' not in switch
assert 'ItemBattleEffects(ITEMEFFECT_NORMAL, 0, TRUE)' in util
assert 'REQUEST_HELDITEM_BATTLE' in commands.split('static void Cmd_removeitem(void)\n{')[1].split('\n}')[0]
assert 'if (gBattleMons[battlerId].hp)' in normal and 'gBattleMons[battlerId].hp' not in movecases
(out/'source-berry-vectors.json').write_text(json.dumps(dict(sourceCommit=commit,pins=pins,items={b:{k:selected[b][k] for k in ['itemId','holdEffect','holdEffectParam']} for b in berries},vectors=vectors),indent=2)+'\n')
print('35 independently compiled original C berry cases; six source files pinned; twelve campaign berry definitions verified')

# Only independent numeric output is published in fixture preparation mode.
if fixtures_only:
    for name in ['source-berry-vectors.json']:
        target = fixture_output / name
        candidate = (out / name).read_bytes()
        if target.exists() and target.read_bytes() != candidate:
            raise SystemExit("Existing reference differs; review a new candidate directory: " + str(target))
        target.write_bytes(candidate)
