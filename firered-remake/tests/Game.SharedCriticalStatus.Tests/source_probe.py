#!/usr/bin/env python3
"""Execute unchanged bounded FireRed poison, burn, toxic and chance commands.
The omitted switch arms cannot run in this fixture. No upstream scripts execute.
Capture refuses to overwrite a golden. Default verifies all immutable vectors.
"""
import argparse,hashlib,json,os,re,subprocess,tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent
PIN='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def run(args,**kw):
 r=subprocess.run(args,text=True,capture_output=True,**kw)
 if r.returncode:raise RuntimeError(str(args)+'\n'+r.stdout+'\n'+r.stderr)
 return r.stdout
def block(text,pattern):
 m=re.search(pattern,text,re.M);assert m,pattern
 start=text.index('{',m.start());depth=0
 for i in range(start,len(text)):
  depth+=(text[i]=='{')-(text[i]=='}')
  if depth==0:return text[m.start():i+1]
 raise AssertionError('Unclosed block')
def sha(text):return hashlib.sha256(text.encode()).hexdigest()
def main():
 p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--capture',action='store_true');p.add_argument('--report',type=Path);a=p.parse_args()
 golden=HERE/'source-vectors.json';assert not a.capture or not golden.exists(),'immutable golden exists'
 assert run(['git','rev-parse','HEAD'],cwd=a.source).strip()==PIN
 paths=['src/battle_script_commands.c','src/data/battle_moves.h','data/battle_scripts_1.s']+[f'include/constants/{n}.h' for n in ['battle','battle_move_effects','battle_script_commands','pokemon','moves','abilities','battle_string_ids']]
 sources={n:(a.source/n).read_text() for n in paths}
 for n in paths:assert sources[n]==run(['git','show',f'{PIN}:{n}'],cwd=a.source),n
 source=sources[paths[0]];full=block(source,r'^void SetMoveEffect\(bool8 primary, u8 certain\)\n')
 prefix=full[:full.index('        case STATUS1_SLEEP:')]
 arms=full[full.index('        case STATUS1_POISON:'):full.index('        if (statusChanged == TRUE)')].rsplit('        }',1)[0]
 tail=full[full.index('        if (statusChanged == TRUE)'):full.index('    else\n    {\n        if (gBattleMons[gEffectBattler].status2')]
 function=prefix+arms+'        }\n'+tail+'}\n'
 chance=block(source,r'^static void Cmd_seteffectwithchance\(void\)\n')
 scripts=sources['data/battle_scripts_1.s']
 for label,effect in [('BurnHit','BURN'),('PoisonHit','POISON'),('PoisonFang','TOXIC')]:
  script=scripts.split('BattleScript_Effect'+label+'::\n')[1].split('\n\n')[0]
  assert [x.strip() for x in script.splitlines()]==['setmoveeffect MOVE_EFFECT_'+effect,'goto BattleScript_EffectHit']
 for effect,label in [('BLAZE_KICK','BurnHit'),('POISON_TAIL','PoisonHit'),('POISON_FANG','PoisonFang')]:
  assert re.search(r'\.4byte BattleScript_Effect'+label+r'\s+@ EFFECT_'+effect,scripts)
 hit=scripts.split('BattleScript_HitFromAccCheck::\n')[1].split('BattleScript_MoveEnd::')[0]
 commands=['accuracycheck','attackstring','ppreduce','critcalc','damagecalc','typecalc','adjustnormaldamage','datahpupdate','critmessage','resultmessage','seteffectwithchance','tryfaintmon'];assert sorted(hit.index(x) for x in commands)==[hit.index(x) for x in commands]
 crit=block(source,r'^static void Cmd_critcalc\(void\)\n')
 assert all('EFFECT_'+effect in crit for effect in ['BLAZE_KICK','POISON_TAIL'])
 adjust=block(source,r'^static void Cmd_adjustnormaldamage\(void\)\n')
 assert 'gBattleMons[gBattlerTarget].hp - 1' in adjust
 tables='\n'.join(re.search(r'^    \[MOVE_EFFECT_'+('TOXIC' if n=='TOXIC_POISON' else n)+r'\].*= STATUS1_'+n+r',$',source,re.M).group() for n in ['POISON','BURN','FREEZE','PARALYSIS','TOXIC_POISON'])
 unit=(HERE/'source-prefix.h').read_text()+'\nstatic const u32 sStatusFlagsForMoveEffects[8]={\n'+tables+'\n};\n'+function+'\n'+chance+'\n'+(HERE/'source-main.c').read_text()
 with tempfile.TemporaryDirectory(prefix='firered-trithaw-source-') as tmp:
  tmp=Path(tmp);(tmp/'probe.c').write_text(unit);outputs=[]
  for name,flags in [('optimized',['-O2']),('ubsan',['-O1','-g','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
   run([os.environ.get('CC','cc'),'-std=c11','-Wall','-Wextra','-Werror','-Wno-unused-parameter','-Wno-unused-variable','-Wno-unused-function',*flags,'-I',str(a.source/'include'),str(tmp/'probe.c'),'-o',str(tmp/name)])
   outputs.append(run([str(tmp/name)]))
 assert outputs[0]==outputs[1]
 rows=[json.loads(x) for x in outputs[0].splitlines()];assert len(rows)==7200
 for row in rows:
  effect,mode=row['effect'],row['mode'];rolled=row['roll']<=(30 if effect==2 else 10)*(1+row['serene'])
  eligible=rolled and mode not in [1,2,3,4,5,11] and not (effect==0 and mode in [9,10] or effect!=0 and mode in [6,7,8])
  expected=8 if mode==3 else [16,8,128][effect] if eligible else 0
  assert row['status']==expected and row['draws']==1,row
  assert row['writes']==row['synchronize']==int(eligible),row
  assert row['noEffect']==int(mode==11 or rolled and effect==2 and mode in [6,7]),row
 snap={'schema':'firered-critical-status-source-v1','sourceCommit':PIN,'sourceFiles':{n:sha(v) for n,v in sources.items()},'slices':{n:sha(v) for n,v in {'preamble':prefix,'statusArms':arms,'statusTail':tail,'chance':chance,'critical':crit,'adjust':adjust}.items()},'scriptOrder':commands,'vectors':rows}
 if a.capture:
  with golden.open('x') as f:json.dump(snap,f,separators=(',',':'));f.write('\n')
 else:assert snap==json.loads(golden.read_text()),'source golden mismatch'
 report={'passed':True,'vectors':len(rows),'sourceCommit':PIN,'optimizedAndUbsanEqual':True,'goldenSha256':hashlib.sha256(golden.read_bytes()).hexdigest()}
 if a.report:a.report.write_text(json.dumps(report,indent=2)+'\n');json.loads(a.report.read_text())
 print(json.dumps(report))
if __name__=='__main__':main()
