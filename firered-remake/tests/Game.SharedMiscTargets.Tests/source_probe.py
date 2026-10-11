#!/usr/bin/env python3
"""One grouped oracle for the unchanged pinned C commands and audited script flow.
Teeter's host loop is a bounded script driver, not the whole battle engine.
"""
import argparse,hashlib,json,subprocess,tempfile,re
from pathlib import Path
HERE=Path(__file__).resolve().parent
PIN='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def run(args,**kwargs):
 p=subprocess.run(args,text=True,capture_output=True,**kwargs)
 if p.returncode:raise RuntimeError(str(args)+'\n'+p.stdout+p.stderr)
 return p.stdout
def sha(s):return hashlib.sha256(s.encode()).hexdigest()
def block(text,name):
 m=re.search(r'^static void '+name+r'\(void\)\n',text,re.M);assert m,name
 start=text.index('{',m.start());depth=0
 for i in range(start,len(text)):
  depth+=(text[i]=='{')-(text[i]=='}')
  if depth==0:return text[m.start():i+1]
def script(text,label):
 lines=text.split(label+'::\n',1)[1].splitlines();out=[]
 for line in lines:
  if not line.startswith('\t'):break
  out.append(line)
 return out
a=argparse.ArgumentParser();a.add_argument('--source',type=Path,required=True);a.add_argument('--report',type=Path,required=True);a.add_argument('--capture',action='store_true');args=a.parse_args()
paths=['src/battle_script_commands.c','data/battle_scripts_1.s','src/data/battle_moves.h','include/battle_util.h','include/constants/battle.h','include/constants/battle_move_effects.h','include/constants/battle_script_commands.h','include/constants/pokemon.h','include/constants/abilities.h']
assert run(['git','rev-parse','HEAD'],cwd=args.source).strip()==PIN
src={p:(args.source/p).read_text() for p in paths}
for p,s in src.items():assert s==run(['git','show',PIN+':'+p],cwd=args.source),p
scripts=src['data/battle_scripts_1.s'];commands={label:[l.strip() for l in script(scripts,label)] for label in ['BattleScript_EffectPresent','BattleScript_EffectSecretPower','BattleScript_EffectTeeterDance','BattleScript_TeeterDanceLoop','BattleScript_TeeterDanceLoopIncrement','BattleScript_TeeterDanceSafeguardProtected','BattleScript_PresentHealTarget']}
assert commands['BattleScript_EffectPresent']==['attackcanceler','accuracycheck BattleScript_PrintMoveMissed, ACC_CURR_MOVE','attackstring','ppreduce','typecalc','presentdamagecalculation']
assert commands['BattleScript_EffectSecretPower']==['getsecretpowereffect','goto BattleScript_EffectHit']
assert commands['BattleScript_EffectTeeterDance']==['attackcanceler','attackstring','ppreduce','setbyte gBattlerTarget, 0']
loop=commands['BattleScript_TeeterDanceLoop'];assert loop[:9]==['movevaluescleanup','setmoveeffect MOVE_EFFECT_CONFUSION','jumpifbyteequal gBattlerAttacker, gBattlerTarget, BattleScript_TeeterDanceLoopIncrement','jumpifability BS_TARGET, ABILITY_OWN_TEMPO, BattleScript_TeeterDanceOwnTempoPrevents','jumpifstatus2 BS_TARGET, STATUS2_SUBSTITUTE, BattleScript_TeeterDanceSubstitutePrevents','jumpifstatus2 BS_TARGET, STATUS2_CONFUSION, BattleScript_TeeterDanceAlreadyConfused','accuracycheck BattleScript_TeeterDanceMissed, ACC_CURR_MOVE','jumpifsideaffecting BS_TARGET, SIDE_STATUS_SAFEGUARD, BattleScript_TeeterDanceSafeguardProtected','attackanimation']
assert commands['BattleScript_TeeterDanceLoopIncrement']==['moveendto MOVEEND_NEXT_TARGET','addbyte gBattlerTarget, 1','jumpifbytenotequal gBattlerTarget, gBattlersCount, BattleScript_TeeterDanceLoop','end']
assert commands['BattleScript_TeeterDanceSafeguardProtected'][-1]=='goto BattleScript_TeeterDanceLoopIncrement'
assert 'orword gHitMarker, HITMARKER_IGNORE_SUBSTITUTE' in commands['BattleScript_PresentHealTarget']
assert '#define MOVEEND_UPDATE_LAST_MOVES               14' in src['include/constants/battle_script_commands.h']
assert '#define MOVEEND_NEXT_TARGET                     16' in src['include/constants/battle_script_commands.h']
assert 'gLastLandedMoves[gBattlerTarget] = gCurrentMove;' in src['src/battle_script_commands.c']
assert '&& !primary && gBattleCommunication[MOVE_EFFECT_BYTE] <= 9)' in src['src/battle_script_commands.c']
assert 'gBattleMons[gEffectBattler].status2 & STATUS2_SUBSTITUTE' in src['src/battle_script_commands.c']
functions={n:block(src['src/battle_script_commands.c'],n) for n in ['Cmd_presentdamagecalculation','Cmd_getsecretpowereffect','Cmd_seteffectwithchance','Cmd_jumpifability','Cmd_jumpifstatus2','Cmd_jumpifsideaffecting']}
macros='\n'.join(l for l in src['include/battle_util.h'].splitlines() if l.startswith('#define ABILITYEFFECT_CHECK_OTHER_SIDE ') or l.startswith('#define ABILITYEFFECT_CHECK_BATTLER_SIDE '))
unit=(HERE/'source-prefix.h').read_text().replace('/* ABILITY_MACROS */',macros)+'\n'+'\n'.join(functions.values())+'\n'+(HERE/'source-main.c').read_text()
with tempfile.TemporaryDirectory(prefix='firered-misc-target-oracle-') as t:
 t=Path(t);(t/'oracle.c').write_text(unit);out=[]
 for name,flags in [('optimized',['-O2']),('ubsan',['-O1','-g','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
  run(['cc','-std=c11','-Wall','-Wextra','-Werror',*flags,'-I',str(args.source/'include'),str(t/'oracle.c'),'-o',str(t/name)])
  out.append(run([str(t/name)]))
 assert out[0]==out[1]
rows=[json.loads(x) for x in out[0].splitlines()]
assert len(rows)==14884,len(rows)
snapshot={'sourceCommit':PIN,'sourceFiles':{p:sha(s) for p,s in src.items()},'functions':{n:sha(s) for n,s in functions.items()},'scripts':commands,'harness':{p:sha((HERE/p).read_text()) for p in ['source-prefix.h','source-main.c']},'vectors':rows}
output=HERE/'source-vectors.json'
if args.capture:
 with output.open('x') as f:json.dump(snapshot,f,separators=(',',':'))
else:assert json.loads(output.read_text())==snapshot
report={'passed':True,'sourceCommit':PIN,'counts':{k:sum(r['kind']==k for r in rows) for k in ['present','secret','chance','teeter']},'vectorSha256':hashlib.sha256(output.read_bytes()).hexdigest(),'builds':['optimized','ubsan'],'scope':'unchanged six C commands, exact scripts, bounded Teeter host driver; no full engine claim'}
args.report.write_text(json.dumps(report,indent=2));print(json.dumps(report))
