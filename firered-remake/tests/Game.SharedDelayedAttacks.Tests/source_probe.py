"""Execute pinned, unchanged delayed commands and the actual Future/Perish owner."""
import hashlib,json,pathlib,re,subprocess,sys,tempfile
root=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);here=pathlib.Path(__file__).parent
sha='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==sha
files=['src/battle_script_commands.c','src/battle_util.c','src/pokemon.c','data/battle_scripts_1.s','include/battle.h','include/constants/moves.h','include/constants/abilities.h']
texts={};hashes={}
for name in files:
 data=(root/name).read_bytes();assert data==subprocess.check_output(['git','show',sha+':'+name],cwd=root),name
 texts[name]=data.decode();hashes[name]=hashlib.sha256(data).hexdigest()
def extract(text,name):
 m=re.search(r'^(?:static void|bool8) '+name+r'\(void\)\n\{',text,re.M);assert m,name
 end=m.end();depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[m.start():end]
names=['Cmd_trysetfutureattack','Cmd_trywish','HandleWishPerishSongOnTurnEnd']
parts=[extract(texts['src/battle_script_commands.c' if n.startswith('Cmd') else 'src/battle_util.c'],n) for n in names]
scripts=texts['data/battle_scripts_1.s']
for name,nextname in [('BattleScript_EffectFutureSight','BattleScript_EffectGust'),('BattleScript_EffectWish','BattleScript_EffectAssist')]:
 block=scripts[scripts.index(name+'::'):scripts.index(nextname+'::')]
 assert block.index('attackcanceler')<block.index('attackstring')<block.index('ppreduce')<block.index('try')
 assert 'accuracycheck' not in block
future_script=scripts[scripts.index('BattleScript_MonTookFutureAttack::'):scripts.index('BattleScript_FutureAttackMiss::')]
assert future_script.index('accuracycheck')<future_script.index('adjustnormaldamage2')<future_script.index('datahpupdate')<future_script.index('tryfaintmon')
assert 'critcalc' not in future_script and 'typecalc' not in future_script
wish_field=texts['src/battle_util.c'].split('case ENDTURN_WISH:',1)[1].split('case ENDTURN_RAIN:',1)[0]
assert '--gWishFutureKnock.wishCounter[gActiveBattler]' in wish_field and 'gAbsentBattlerFlags' not in wish_field
with tempfile.TemporaryDirectory(prefix='firered-delayed-source-') as td:
 p=pathlib.Path(td);(p/'probe.c').write_text((here/'source-prefix.h').read_text()+'\n'.join(parts)+(here/'source-main.c').read_text())
 subprocess.run(['cc','-std=c99','-I'+str(root/'include'),str(p/'probe.c'),'-o',str(p/'probe')],check=True)
 result=json.loads(subprocess.check_output([str(p/'probe')],text=True))
assert len(result['future'])==16 and len(result['wish'])==8
assert all(v['damage']==(151 if v['help'] else 101) and v['counter']==3 for v in result['future'])
assert result['order']['callbacks']==[0,1,2,3,13,12,11,10]
assert result['absent']['futureCounters']==[0,1,0,1]
assert result['absent']['perishCounters']==[1,2,1,2]
result.update(source=sha,source_files=hashes,functions={n:hashlib.sha256(p.encode()).hexdigest() for n,p in zip(names,parts)},base_damage_boundary='Explicit CalculateBaseDamage stub returns 101 and records actor, target, move, side screen flags, zero override arguments; arithmetic damage is covered by existing source damage owners.',script_checks=['PP before scheduling; no setup accuracy','impact accuracy before random adjustment; no critical or type command','Wish field counter ticks source-absent positions'])
# Reuse the existing explicit dependency adapter around the exact full source damage function.
adapter=here.parent/'Game.CommonAbilityAdapters.Tests'
body=texts['src/pokemon.c'][texts['src/pokemon.c'].index('#define APPLY_STAT_MOD'):texts['src/pokemon.c'].index('\nu8 CountAliveMonsInBattle(u8 caseId)')]
out.parent.mkdir(parents=True,exist_ok=True)
base_c=out.parent/'source-base-damage.c';base_exe=out.parent/'source-base-damage'
base_c.write_text((adapter/'source-prefix.h').read_text()+'\n'+texts['include/constants/abilities.h']+'\n'+body+'\n'+(adapter/'source-main.c').read_text())
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror','-Wno-unused-but-set-variable','-O2',str(base_c),'-o',str(base_exe)],check=True)
result['damage_function_sha256']=hashlib.sha256(body.encode()).hexdigest()
result['damage_adapter_sha256']={n:hashlib.sha256((adapter/n).read_bytes()).hexdigest() for n in ['source-prefix.h','source-main.c']}
result['base_damage_boundary']='Command probe records exact CalculateBaseDamage call arguments; the separate unchanged full CalculateBaseDamage executable verifies actual C# monster stats, Plus/Minus, type and power.'
out.write_text(json.dumps(result,indent=2)+'\n');print('PASS pinned delayed command, counter, absence and Future/Perish order boundaries')
