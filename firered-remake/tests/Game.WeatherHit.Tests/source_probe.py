"""Compile unmodified pinned accuracy commands; validate the four script prefixes."""
import argparse,hashlib,json,pathlib,re,subprocess
p=argparse.ArgumentParser();p.add_argument('source',type=pathlib.Path);p.add_argument('output',type=pathlib.Path);p.add_argument('--capture',action='store_true');a=p.parse_args()
here=pathlib.Path(__file__).resolve().parent;a.output.mkdir(parents=True,exist_ok=True)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=a.source,text=True).strip()==pin
paths=['src/battle_script_commands.c','data/battle_scripts_1.s','src/data/battle_moves.h']
commands,scripts,moves=[(a.source/x).read_text() for x in paths]
def function(name):
 m=re.search(r'static (?:void|bool8) '+name+r'\([^;]*?\)\n\{',commands);assert m,name
 return commands[m.start():commands.index('\n}',m.end())+2]
for name in ['Gust','Twister','Thunder','SkyUppercut']:
 block=scripts.split('BattleScript_Effect'+name+'::',1)[1].split('\n\n',1)[0]
 assert 'HITMARKER_IGNORE_ON_AIR' in block,name
 if name in ['Gust','Twister']:assert 'setbyte sDMG_MULTIPLIER, 2' in block and 'STATUS3_ON_AIR' in block
assert 'gBattleScripting.dmgMultiplier = 1;' in function('MoveValuesCleanUp')
assert 'MOVEEND_NEXT_TARGET' in commands and 'MoveValuesCleanUp();\n                    BattleScriptPush(gBattleScriptsForMoveEffects' in commands
head=(here/'source-prefix.h').read_text();start=commands.index('sAccuracyStageRatios');table=commands[start:commands.index('};',start)+2];head=head.replace('SOURCE_ACCURACY_TABLE',table[table.index('{'):table.rindex('}')+1])
body='\n'.join(function(n) for n in ['JumpIfMoveAffectedByProtect','AccuracyCalcHelper','Cmd_accuracycheck'])
(a.output/'oracle.c').write_text(head+body+'\n'+(here/'source-main.c').read_text())
subprocess.run(['cc','-std=c99','-Wall','-Wextra','-O0',str(a.output/'oracle.c'),'-o',str(a.output/'oracle')],check=True,capture_output=True,text=True)
v={'sourceCommit':pin,'sourceFiles':{x:hashlib.sha256((a.source/x).read_bytes()).hexdigest() for x in paths},'vectors':json.loads(subprocess.check_output([str(a.output/'oracle')],text=True))}
golden=here/'source-vectors.json'
if a.capture:
 assert not golden.exists(),'refuse immutable golden overwrite';golden.write_text(json.dumps(v,indent=2)+'\n')
else:assert json.loads(golden.read_text())==v,'pinned C vectors differ'
(a.output/'source-report.json').write_text(json.dumps({'passed':True,'vectors':len(v['vectors']),'sourceCommit':pin},indent=2)+'\n')
print(json.dumps({'vectors':len(v['vectors']),'sourceCommit':pin}))
