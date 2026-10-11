#!/usr/bin/env python3
"""Authenticated bounded item commands, with the already approved King's Rock operand fix.
No upstream script, ROM, campaign, or presentation executor is run.
"""
import hashlib, importlib.util, json, pathlib, re, subprocess, sys
HERE = pathlib.Path(__file__).resolve().parent
shared = HERE.parent / 'Game.SourceHitItems.Tests'
spec = importlib.util.spec_from_file_location('canonical_hit_probe', shared/'source_probe.py')
probe = importlib.util.module_from_spec(spec); spec.loader.exec_module(probe)
reference, output = map(pathlib.Path, sys.argv[1:])
if output.exists(): raise SystemExit('Use a new source evidence directory')
probe.run(reference, output/'original')
subprocess.run([sys.executable,str(shared/'king_parameter_fix_probe.py'),str(output/'original'),str(output/'king-fix')],check=True,stdout=subprocess.DEVNULL)
entitlements={}
for name in ['src/data/pokemon/tmhm_learnsets.h','src/data/pokemon/tutor_learnsets.h']:
    body=(reference/name).read_bytes()
    assert body==subprocess.check_output(['git','-C',str(reference),'show',probe.COMMIT+':'+name])
    entitlements[name]=probe.sha(body)
for species,move in [('LAPRAS','SURF'),('SNORLAX','SUBSTITUTE'),('ABRA','SEISMIC_TOSS'),('DEWGONG','ICE_BEAM'),('GOLEM','EXPLOSION')]:
    supported=False
    for filename in entitlements:
        block=re.search(r'\[SPECIES_'+species+r'\].*?(?=\[SPECIES_|\Z)',(reference/filename).read_text(),re.S).group(0)
        supported |= bool(re.search(r'(?:TM|HM)\d\d_'+move+r'\)',block)) or 'TUTOR(MOVE_'+move+')' in block
    assert supported,(species,move)
rows=[]
def add(name, **fields):
    r=dict(probe.vectors()[0]['input'],id=len(rows));r.update(fields)
    rows.append({'name':name,'input':r})
for actor in range(4):
    for item0 in [0,219,187,21]:
        for roll in [9,10]:
            add(f'king/actor{actor}/slot0-{item0}/roll{roll}',attacker=actor,target=actor^1,battler0Item=item0,draw=roll)
    for receipt in [0,1,7,8,16,65535]:
        add(f'bell/actor{actor}/receipt{receipt}',op=2,attacker=actor,target=actor^1,item=219,battler0Item=187,receipt=receipt)
    for name, fields in [('substitute',{'substitute':1,'substituteHp':1,'physicalDmg':0}),('fainted-target',{'targetHp':0}),('fainted-holder',{'attackerHp':0}),('full-holder',{'attackerHp':100})]:
        add(f'bell/actor{actor}/{name}',op=2,attacker=actor,target=actor^1,item=219,battler0Item=187,**fields)
encoded=''.join(' '.join(str(r['input'][field]) for field in probe.FIELDS)+'\n' for r in rows)
(output/'four-slot-inputs.txt').write_text(encoded)
results=[]
for mode in ['optimized','ubsan']:
    result=subprocess.run([str(output/'king-fix'/('corrected-'+mode))],input=encoded,text=True,capture_output=True,check=True)
    assert not result.stderr
    results.append(result.stdout)
assert results[0]==results[1]
lines=results[0].splitlines();assert len(lines)==len(rows)==72
for row,line in zip(rows,lines):
    row['expected']=dict(zip(probe.OUTPUTS,map(int,line.split(','))));i,e=row['input'],row['expected']
    if row['name'].startswith('king/'):
        assert e['drawCount']==1 and e['flinch']==int(i['draw']<10)
    else:
        eligible=i['receipt'] not in (0,65535) and i['attackerHp'] not in (0,100)
        assert e['drawCount']==0 and e['attackerHp']==(min(100,i['attackerHp']+max(1,i['receipt']//8)) if eligible else i['attackerHp'])
        assert e['receipt']==(0 if eligible else i['receipt'])
    assert e['attackerItem']==i['item']
(output/'four-slot-vectors.json').write_text(json.dumps(rows,indent=2)+'\n')
# The original probe authenticates the complete source bytes before these assertions.
source=(reference/'src/battle_script_commands.c').read_bytes()
cleanup=probe.extract(source,'static void MoveValuesCleanUp(void)')
assert b'gBattleMoveDamage' not in cleanup and b'gSpecialStatuses' not in cleanup
moveend=probe.extract(source,'static void Cmd_moveend(void)')
phases=[b'case MOVEEND_CHANGED_ITEMS:',b'case MOVEEND_ITEM_EFFECTS_ALL:',b'case MOVEEND_KINGSROCK_SHELLBELL:',b'case MOVEEND_SUBSTITUTE:',b'case MOVEEND_NEXT_TARGET:']
assert [moveend.index(p) for p in phases]==sorted(moveend.index(p) for p in phases)
report={'sourceCommit':probe.COMMIT,'fourSlotVectors':len(rows),'originalVectors':160,'correctedParameterVectors':44,'fourSlotOutputsSha256':probe.sha(results[0].encode()),'stageOrder':[p.decode() for p in phases],'fixtureEntitlementSources':entitlements,
 'moveValuesCleanupPreservesCommandDamageAndReceipts':True,'fixedKingOperand':'actual attacker holdEffectParam; strict < comparison unchanged',
 'sourceC':str(output/'king-fix/corrected.c'),'sourceHeader':str(shared/'source-prefix.h'),'input':str(output/'four-slot-inputs.txt'),'vectors':str(output/'four-slot-vectors.json'),
 'limits':'Authored host stubs around extracted selected original C commands, plus approved single-operand fix. Sequence calls supplied explicitly. Not a full-engine differential.'}
(output/'source-report.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
