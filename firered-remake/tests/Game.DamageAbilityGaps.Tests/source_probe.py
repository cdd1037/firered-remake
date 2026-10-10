#!/usr/bin/env python3
"""Compile verbatim pinned source damage with reviewed, bounded dependency stubs."""
import hashlib, json, pathlib, re, subprocess, sys
if len(sys.argv) != 3:
    raise SystemExit('source_probe.py REFERENCE_ROOT EVIDENCE_DIRECTORY')
reference, output = map(pathlib.Path, sys.argv[1:])
here = pathlib.Path(__file__).resolve().parent
commit = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
assert commit == '037335f4c725d7c9aecdac87066f2002b4bd7e14'
source = (reference / 'src/pokemon.c').read_text()
assert source == subprocess.check_output(['git', '-C', str(reference), 'show', commit + ':src/pokemon.c'], text=True), 'Dirty source oracle input'
body = source[source.index('#define APPLY_STAT_MOD'):source.index('\nu8 CountAliveMonsInBattle(u8 caseId)')]
abilities = (reference / 'include/constants/abilities.h').read_text()
assert abilities == subprocess.check_output(['git', '-C', str(reference), 'show', commit + ':include/constants/abilities.h'], text=True), 'Dirty ability constants'
ids = {k: int(v) for k, v in re.findall(r'#define (ABILITY_\w+) (\d+)', abilities)}
types = {'NORMAL':0, 'ROCK':5, 'FIRE':10}
moves = {'TACKLE':(35,'NORMAL',0), 'ROCK_THROW':(50,'ROCK',0), 'EMBER':(40,'FIRE',0), 'EXPLOSION':(250,'NORMAL',7)}
items = {'ITEM_NONE':0,'ITEM_HARD_STONE':1,'ITEM_THICK_CLUB':2,'ITEM_METAL_POWDER':3}
vectors = []
def add(name, **changes):
    row = dict(name=name, attack=55, defense=47, abilityA='ABILITY_NONE', abilityD='ABILITY_NONE',
        statusA=0,statusD=0,stageA=6,stageD=6,critical=False,move='TACKLE',itemA='ITEM_NONE',
        itemD='ITEM_NONE',reflect=False,badgeAttack=False,badgeDefense=False,percent=100)
    row.update(changes); vectors.append(row)
add('plain-predecessor')
for ability in ['HUGE_POWER','PURE_POWER']:
    add(ability+'-before-badge-odd-rounding',abilityA='ABILITY_'+ability,badgeAttack=True)
    add(ability+'-stages-normal',abilityA='ABILITY_'+ability,stageA=4,stageD=8)
    add(ability+'-critical-ignores-adverse-stages',abilityA='ABILITY_'+ability,stageA=4,stageD=8,critical=True)
    add(ability+'-critical-keeps-favorable-stages',abilityA='ABILITY_'+ability,stageA=8,stageD=4,critical=True,percent=85)
    add(ability+'-burn-reflect',abilityA='ABILITY_'+ability,statusA=16,reflect=True)
    add(ability+'-special-unaffected',abilityA='ABILITY_'+ability,move='EMBER',badgeAttack=True)
add('hustle-after-badge-and-type-item',abilityA='ABILITY_HUSTLE',attack=53,badgeAttack=True,itemA='ITEM_HARD_STONE',move='ROCK_THROW')
add('hustle-after-species-item',abilityA='ABILITY_HUSTLE',attack=53,itemA='ITEM_THICK_CLUB',stageA=7)
add('hustle-critical-burn-keeps-boost',abilityA='ABILITY_HUSTLE',attack=53,statusA=16,stageA=4,stageD=8,critical=True,reflect=True)
add('hustle-special-unaffected',abilityA='ABILITY_HUSTLE',move='EMBER',badgeAttack=True)
add('marvel-scale-healthy-unaffected',abilityD='ABILITY_MARVEL_SCALE')
for status in [1,3,8,16,32,64,128,384]:
    add('marvel-scale-status-'+str(status),abilityD='ABILITY_MARVEL_SCALE',statusD=status)
add('marvel-after-defense-badge',abilityD='ABILITY_MARVEL_SCALE',statusD=8,badgeDefense=True,defense=55)
add('marvel-after-metal-powder-before-explosion',abilityD='ABILITY_MARVEL_SCALE',statusD=8,badgeDefense=True,defense=55,itemD='ITEM_METAL_POWDER',move='EXPLOSION')
add('marvel-normal-defense-stage-reflect',abilityD='ABILITY_MARVEL_SCALE',statusD=8,stageD=8,reflect=True)
add('marvel-critical-remains-ignores-defense-stage-screen',abilityD='ABILITY_MARVEL_SCALE',statusD=8,stageD=8,critical=True,reflect=True)
add('marvel-special-unaffected',abilityD='ABILITY_MARVEL_SCALE',statusD=16,move='EMBER',badgeDefense=True,itemD='ITEM_METAL_POWDER')
add('combined-huge-marvel-burn-explosion',abilityA='ABILITY_HUGE_POWER',abilityD='ABILITY_MARVEL_SCALE',statusA=16,statusD=8,stageA=7,stageD=5,move='EXPLOSION',percent=85)
add('guts-predecessor-burn-reflect',abilityA='ABILITY_GUTS',statusA=16,reflect=True)
output.mkdir(parents=True,exist_ok=True)
(output/'extracted-source.c').write_text((here/'source-prefix.h').read_text()+'\n'+abilities+'\n'+body+'\n'+(here/'source-main.c').read_text())
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror','-Wno-unused-but-set-variable','-O0',str(output/'extracted-source.c'),'-o',str(output/'source-oracle')],check=True)
lines=[]
for v in vectors:
    power,type_name,effect=moves[v['move']]
    lines.append(' '.join(map(str,[v['attack'],v['defense'],ids[v['abilityA']],ids[v['abilityD']],v['statusA'],v['statusD'],v['stageA'],v['stageD'],int(v['critical']),power,types[type_name],effect,items[v['itemA']],items[v['itemD']],int(v['reflect']),int(v['badgeAttack']),int(v['badgeDefense'])])))
run=subprocess.run([str(output/'source-oracle')],input='\n'.join(lines)+'\n',text=True,capture_output=True,check=True)
bases=list(map(int,run.stdout.splitlines())); assert len(bases)==len(vectors)>0
for v,base in zip(vectors,bases):
    v['sourceBaseDamage']=base
    # Tested Cubone -> Ditto moves have no STAB or type modifier. Source post-base crit/random only.
    v['damage']=max(1,base*(2 if v['critical'] else 1)*v['percent']//100)
record={'sourceCommit':commit,'sourceFunctionSha256':hashlib.sha256(body.encode()).hexdigest(),
    'abilityIds':ids,
    'fixtureScope':'Controlled Domain registers, not authentic species admission or new-ability full turns',
    'vectors':vectors}
(output/'source-vectors.json').write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps({'sourceCommit':commit,'vectors':len(vectors),'sourceFunctionSha256':record['sourceFunctionSha256']}))
