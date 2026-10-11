import hashlib,json,pathlib,re,subprocess,sys
root=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14';assert subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()==commit
files=['src/battle_script_commands.c','src/pokemon.c','src/data/items.json','include/constants/items.h','include/constants/hold_effects.h']
for name in files:assert (root/name).read_bytes()==subprocess.check_output(['git','-C',str(root),'show',commit+':'+name]),name
text=(root/files[0]).read_text();start=text.index('        case MOVEEND_CHOICE_MOVE:');end=text.index('        case MOVEEND_CHANGED_ITEMS:',start);case=text[start:end]
item=next(x for x in json.loads((root/files[2]).read_text())['items'] if x['itemId']=='ITEM_CHOICE_BAND')
assert item['holdEffect']=='HOLD_EFFECT_CHOICE_BAND' and item['holdEffectParam']==0
assert re.search(r'^#define ITEM_CHOICE_BAND\s+186$',(root/files[3]).read_text(),re.M)
assert re.search(r'^#define HOLD_EFFECT_CHOICE_BAND\s+29$',(root/files[4]).read_text(),re.M)
attack=(root/files[1]).read_text();attack=attack[attack.index('    if (attackerHoldEffect == HOLD_EFFECT_CHOICE_BAND)'):attack.index('    if (attackerHoldEffect == HOLD_EFFECT_SOUL_DEW ')]
prefix=r'''#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;
#define MOVEEND_CHOICE_MOVE 6
#define HOLD_EFFECT_CHOICE_BAND 29
#define HITMARKER_OBEYS 1
#define MOVE_RESULT_FAILED 1
#define MOVE_NONE 0
#define MOVE_UNAVAILABLE 65535
#define MOVE_STRUGGLE 165
#define MOVE_BATON_PASS 226
#define MAX_MON_MOVES 4
struct Mon {u16 moves[4];} gBattleMons[2];
struct Scripting {u8 moveendState;} gBattleScripting;
u32 gHitMarker;u8 holdEffectAtk,gBattlerAttacker;u16 gChosenMove,choice,*choicedMoveAtk=&choice;u32 gMoveResultFlags;
void RunChoice(void){int i;switch(gBattleScripting.moveendState){
'''
main=r'''int main(void){unsigned locks[]={0,65535,45,33,226},chosen[]={0,65535,165,226,45,33};
for(unsigned obey=0;obey<2;obey++)for(unsigned held=0;held<2;held++)for(unsigned lock=0;lock<5;lock++)for(unsigned move=0;move<6;move++)for(unsigned fail=0;fail<2;fail++)for(unsigned set=0;set<4;set++){
 gHitMarker=obey;holdEffectAtk=held?29:0;choice=locks[lock];gChosenMove=chosen[move];gMoveResultFlags=fail;gBattleScripting.moveendState=6;
 gBattleMons[0]=(struct Mon){.moves={set==0?45:set==1?33:set==2?226:0,0,0,0}};
 RunChoice();printf("L,%u,%u,%u,%u,%u,%u,%u,%u\n",obey,held,locks[lock],chosen[move],fail,set,choice,gBattleScripting.moveendState);
}
for(unsigned a=1;a<=65535;a+=113){printf("A,%u,%u,%u\n",a,Attack(a,0),Attack(a,29));}
return 0;}
'''
program=prefix+case+'}}\nu32 Attack(u16 attack,u8 attackerHoldEffect){\n'+attack+'return attack;\n}\n'+main
(out/'choice-source.c').write_text(program)
subprocess.run(['cc','-std=c99','-O2','-Wall','-Wextra',str(out/'choice-source.c'),'-o',str(out/'choice-source')],check=True)
result=subprocess.check_output([str(out/'choice-source')],text=True);(out/'choice-vectors.csv').write_text(result)
(out/'choice-source-proof.json').write_text(json.dumps(dict(sourceCommit=commit,sourceFiles={name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in files},item=dict(id=186,effect='HOLD_EFFECT_CHOICE_BAND',parameter=0),vectors=len(result.splitlines()),scope='Exact original MOVEEND_CHOICE_MOVE and Choice Band Attack branch compiled with explicit surrounding state; not ROM execution'),indent=2))
print('Original Choice vectors:',len(result.splitlines()))
