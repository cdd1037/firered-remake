from pathlib import Path
import hashlib,json,runpy,subprocess,sys
source=Path(sys.argv[1]); out=Path(sys.argv[2]); out.mkdir(parents=True,exist_ok=True)
# Reuse the source-verified GetWhoStrikesFirst/SwapTurnOrder harness. Substitute the
# actual pre-first-turn sort excerpt, whose TRUE flag ignores chosen move priorities.
base=Path(__file__).resolve().parents[1]/'Game.FourActionTurns.Tests/source_probe.py'
saved=sys.argv; sys.argv=[str(base),str(source),str(out)];runpy.run_path(str(base));sys.argv=saved
text=(source/'src/battle_main.c').read_text()
start=text.index('    if (gBattleStruct->switchInAbilitiesCounter == 0)',text.index('static void TryDoEventsBeforeFirstTurn(void)\n{'))
start=text.index('        for (i = 0;',start); end=text.index('\n    }',start)
sort='void SourceInitialEntrySort(void) { s32 i,j;\n'+text[start:end]+'\n}\n'
c=(out/'source-order-oracle.c').read_text();c=c.replace('int main(void)',sort+'int main(void)').replace('else SetActionsAndBattlersTurnOrder();','else SourceInitialEntrySort();')
(out/'source-initial-entry.c').write_text(c)
subprocess.run(['cc','-std=c99','-O2','-Wno-unused-parameter',str(out/'source-initial-entry.c'),'-o',str(out/'source-initial-entry')],check=True)
rows=subprocess.check_output([str(out/'source-initial-entry')],text=True)
(out/'source-order-vectors.csv').write_text(rows)
# Cmd_trygetintimidatetarget's unmodified loop. HP is intentionally absent from its query.
cmd=(source/'src/battle_script_commands.c').read_text()
assert (source/'src/battle_script_commands.c').read_bytes()==subprocess.check_output(['git','-C',str(source),'show','HEAD:src/battle_script_commands.c'])
start=cmd.index('    for (;gBattlerTarget < gBattlersCount;',cmd.index('static void Cmd_trygetintimidatetarget(void)\n{'));end=cmd.index('\n\n    if (gBattlerTarget >=',start)
query=cmd[start:end]
c='''#include <stdio.h>
int gBattlerTarget,gBattlersCount=4,gAbsentBattlerFlags;int gBitTable[]={1,2,4,8};
int GetBattlerSide(int i){return i&1;}
int target(int side){
'''+query+'''
return gBattlerTarget;}
int main(void){for(int absent=0;absent<16;absent++)for(int hp=0;hp<16;hp++){
gAbsentBattlerFlags=absent;gBattlerTarget=0;int mask=0;while(target(0)<4){mask|=1<<gBattlerTarget;gBattlerTarget++;}
printf("%d,%d,%d\\n",absent,hp,mask); }return 0;}
'''
(out/'source-intimidate-targets.c').write_text(c)
subprocess.run(['cc','-std=c99','-O2',str(out/'source-intimidate-targets.c'),'-o',str(out/'source-intimidate-targets')],check=True)
(out/'source-intimidate-vectors.csv').write_text(subprocess.check_output([str(out/'source-intimidate-targets')],text=True))
paths=['src/battle_main.c','src/battle_util.c','src/battle_script_commands.c','data/battle_scripts_1.s']
(out/'entry-source-proof.json').write_text(json.dumps({'sourceCommit':'037335f4c725d7c9aecdac87066f2002b4bd7e14',
 'sha256':{p:hashlib.sha256((source/p).read_bytes()).hexdigest() for p in paths},
 'entrySortExcerpt':hashlib.sha256(sort.encode()).hexdigest(),'intimidateExcerpt':hashlib.sha256(query.encode()).hexdigest(),
 'usedVectors':{'initialTieOrder':256,'intimidateTargets':256},'scope':'Compiled unchanged original C excerpts with explicit environment stubs; not a ROM or full-engine execution.'},indent=2))
print('PASS 256 original-C initial tie-order and 256 Intimidate target vectors')
