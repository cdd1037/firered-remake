#!/usr/bin/env python3
"""Compile unchanged fixed-damage commands from authenticated FireRed source; no upstream scripts."""
import hashlib, importlib.util, json, pathlib, re, subprocess, sys
ref,out=map(pathlib.Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=False)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==pin
helper=pathlib.Path(__file__).parent.parent/'Game.SourceFocusBand.Tests/source_probe.py'
spec=importlib.util.spec_from_file_location('focus_probe',helper);focus=importlib.util.module_from_spec(spec);spec.loader.exec_module(focus)
sources={p:(ref/p).read_bytes() for p in focus.SOURCES}
for p,b in sources.items():assert b==subprocess.check_output(['git','show',pin+':'+p],cwd=ref),p
script=sources['data/battle_scripts_1.s'].decode()
segments={}
for label in ['SuperFang','DragonRage','Sonicboom','LevelDamage','Psywave','Endeavor']:
 start=script.index('BattleScript_Effect'+label+'::');end=script.index('\nBattleScript_',start+1)
 segment=script[start:end];segments[label]=segment
 assert 'typecalc' in segment and 'BattleScript_HitFromAtkAnimation' in segment
 assert ('adjustsetdamage' in segment)==(label!='SuperFang')
 assert segment.index('ppreduce')<segment.index('typecalc')
 if label=='Endeavor':assert segment.index('ppreduce')<segment.index('setdamagetohealthdifference')<segment.index('accuracycheck')<segment.index('typecalc')<segment.index('jumpifmovehadnoeffect')<segment.index('adjustsetdamage')
 else:assert segment.index('accuracycheck')<segment.index('ppreduce')
assert 'BattleScript_HitFromAtkAnimation::' in script
hit=script[script.index('BattleScript_HitFromAtkAnimation::'):script.index('BattleScript_MoveEnd::')]
assert hit.index('datahpupdate')<hit.index('seteffectwithchance')<hit.index('tryfaintmon')
assert re.search(r'\.4byte BattleScript_EffectHit\s+@ EFFECT_FALSE_SWIPE',script)
symbols=['static void Cmd_damagetohalftargethp(void)','static void Cmd_dmgtolevel(void)','static void Cmd_psywavedamageeffect(void)','static void Cmd_setdamagetohealthdifference(void)','static void Cmd_adjustsetdamage(void)','static void Cmd_datahpupdate(void)']
bodies=b'\n\n'.join(focus.extract(sources['src/battle_script_commands.c'],s) for s in symbols)
constants='ITEM_NONE ITEM_FOCUS_BAND ITEM_ENIGMA_BERRY HOLD_EFFECT_FOCUS_BAND ABILITY_STURDY EFFECT_FALSE_SWIPE STATUS2_SUBSTITUTE STATUS3_ALWAYS_HITS MOVE_RESULT_MISSED MOVE_RESULT_SUPER_EFFECTIVE MOVE_RESULT_NOT_VERY_EFFECTIVE MOVE_RESULT_DOESNT_AFFECT_FOE MOVE_RESULT_ONE_HIT_KO MOVE_RESULT_FAILED MOVE_RESULT_FOE_ENDURED MOVE_RESULT_FOE_HUNG_ON MOVE_RESULT_NO_EFFECT HITMARKER_DESTINYBOND HITMARKER_SYNCHRONISE_EFFECT HITMARKER_IGNORE_SUBSTITUTE HITMARKER_PASSIVE_DAMAGE HITMARKER_SKIP_DMG_TRACK MOVE_EFFECT_BYTE MISS_TYPE MULTISTRING_CHOOSER B_MSG_KO_MISS B_MSG_KO_UNAFFECTED F_DYNAMIC_TYPE_1 DYNAMIC_TYPE_MASK TYPE_MYSTERY MOVE_PAIN_SPLIT BS_TARGET IS_TYPE_PHYSICAL'.split()
defs=[]
for symbol in constants:
 matches=[m.group() for data in sources.values() for m in re.finditer(rb'(?m)^#define '+symbol.encode()+rb'(?:\s|\()[^\n]*',data)];assert len(matches)==1,symbol;defs+=matches
special=focus.extract(sources['include/battle.h'],'struct SpecialStatus')+b';'
prefix=(helper.parent/'source-prefix.h').read_bytes().replace(b'/* SOURCE_CONSTANTS_AND_SPECIAL_STATUS */',b'\n'.join(defs)+b'\n'+special)
a=prefix.index(b'static u16 Random(void) {');b=prefix.index(b'\nstatic void RecordItemEffectBattle',a)
prefix=prefix[:a]+b'static u32 seed;\nstatic u16 Random(void) { seed=seed*1103515245u+24691u; rngCount++; return seed>>16; }\n'+prefix[b:]
runner=(pathlib.Path(__file__).parent/'source-main.c').read_bytes()
generated=prefix+b'\n'+bodies+b'\n'+runner
(out/'oracle.c').write_bytes(generated);(out/'extracted-functions.c').write_bytes(bodies)
results=[]
for flags,name in [(['-O2'],'optimized'),(['-O1','-fsanitize=undefined','-fno-sanitize-recover=all'],'ubsan')]:
 command=['cc','-std=c99','-Wall','-Wextra','-Wno-unused-function','-Wno-unused-variable',*flags,str(out/'oracle.c'),'-o',str(out/name)]
 subprocess.run(command,check=True);results.append(subprocess.check_output([str(out/name)]))
assert results[0]==results[1]
vectors=[json.loads(line) for line in results[0].splitlines()];assert len(vectors)>1000
payload={'sourceCommit':pin,'inputs':{p:hashlib.sha256(b).hexdigest() for p,b in sources.items()},'symbols':symbols,'scriptSegments':segments,'vectors':vectors}
(out/'source-vectors.json').write_text(json.dumps(payload,separators=(',',':'))+'\n')
print(json.dumps({'vectors':len(vectors),'bytes':len(results[0]),'sha256':hashlib.sha256(results[0]).hexdigest()}))
