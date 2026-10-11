import hashlib,json,pathlib,re,subprocess,sys
root=pathlib.Path(sys.argv[1]);product=pathlib.Path(sys.argv[2]);out=pathlib.Path(sys.argv[3]);out.mkdir(parents=True,exist_ok=True)
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()==commit
for name in ['src/pokemon.c','include/constants/abilities.h']:
 assert (root/name).read_bytes()==subprocess.check_output(['git','-C',str(root),'show',commit+':'+name]),name
source=(root/'src/pokemon.c').read_text();body=source[source.index('#define APPLY_STAT_MOD'):source.index('\nu8 CountAliveMonsInBattle(u8 caseId)')]
abilities=(root/'include/constants/abilities.h').read_text();ids={k:int(v) for k,v in re.findall(r'#define (ABILITY_\w+) (\d+)',abilities)}
harness=product/'tests/Game.DamageAbilityGaps.Tests'
prefix_source=(harness/'source-prefix.h').read_text();main_source=(harness/'source-main.c').read_text()
assert prefix_source.count('ITEM_METAL_POWDER, ITEM_ENIGMA_BERRY')==1
# Only the first return belongs to ItemId_GetHoldEffect; do not change the
# following GetHoldEffectParam stub (Choice Band has parameter zero).
prefix=prefix_source.replace('ITEM_METAL_POWDER, ITEM_ENIGMA_BERRY','ITEM_METAL_POWDER, ITEM_CHOICE_BAND, ITEM_ENIGMA_BERRY',1).replace('return item == ITEM_HARD_STONE ?', 'return item == ITEM_CHOICE_BAND ? HOLD_EFFECT_CHOICE_BAND : item == ITEM_HARD_STONE ?',1)
assert prefix.count('return item == ITEM_CHOICE_BAND ? HOLD_EFFECT_CHOICE_BAND :')==1
(out/'choice-damage.c').write_text(prefix+'\n'+abilities+'\n'+body+'\n'+main_source)
subprocess.run(['cc','-std=c11','-O0','-Wall','-Wextra','-Werror','-Wno-unused-but-set-variable',str(out/'choice-damage.c'),'-o',str(out/'choice-damage')],check=True)
rows=[];inputs=[]
for attack in [53,55,99,201]:
 for ability in ['ABILITY_NONE','ABILITY_HUGE_POWER','ABILITY_HUSTLE','ABILITY_GUTS']:
  for band in [False,True]:
   for special in [False,True]:
    for critical in [False,True]:
     for badge in [False,True]:
      for burn in [False,True]:
       row=dict(attack=attack,defense=71,ability=ability,band=band,special=special,critical=critical,badge=badge,burn=burn,stageA=4 if critical else 7,stageD=8 if critical else 5,percent=85 if burn else 100)
       inputs.append(' '.join(map(str,[attack,71,ids[ability],0,16 if burn else 0,0,row['stageA'],row['stageD'],int(critical),40 if special else 35,10 if special else 0,0,4 if band else 0,0,0,int(badge),0])))
       rows.append(row)
run=subprocess.run([str(out/'choice-damage')],input='\n'.join(inputs)+'\n',text=True,capture_output=True,check=True)
results=list(map(int,run.stdout.splitlines()));assert len(results)==len(rows)==512
for row,value in zip(rows,results):row.update(baseDamage=value,damage=max(1,value*(2 if row['critical'] else 1)*row['percent']//100))
(out/'choice-damage-vectors.json').write_text(json.dumps(dict(sourceCommit=commit,sourceFunctionSha256=hashlib.sha256(body.encode()).hexdigest(),
 sourceFiles={name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in ['src/pokemon.c','include/constants/abilities.h']},
 harnessOrigin='Reconstructed controlled dependency stubs, 2026-10-11; missing original harness bytes/hashes are not recovered. CalculateBaseDamage and its macros are verbatim pinned source.',
 harnessFiles={name:hashlib.sha256((harness/name).read_bytes()).hexdigest() for name in ['source-prefix.h','source-main.c']},
 compiledPrefixSha256=hashlib.sha256(prefix.encode()).hexdigest(),
 fixtureScope='Level 37 Cubone-to-Ditto stat registers, 500/500 HP, singles damage scaling, no weather/sport/ally/Flash Fire, synthetic item transport IDs. Badge inputs are independent arithmetic switches with both register sides reporting player; this is not an authentic battle layout. Critical/random post-base factors are applied by this Python adapter; separate real-turn tests use authentic species and moves.',vectors=rows),separators=(',',':'))+'\n')
print('Original complete damage vectors:',len(rows))
