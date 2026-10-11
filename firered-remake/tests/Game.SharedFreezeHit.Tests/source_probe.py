#!/usr/bin/env python3
"""Execute unchanged bounded FireRed SetMoveEffect freeze slices and chance command.
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
 freeze=full[full.index('        case STATUS1_FREEZE:'):full.index('        case STATUS1_PARALYSIS:')]
 suffix=full[full.index('        if (statusChanged == TRUE)'):full.index('    else\n    {\n        if (gBattleMons[gEffectBattler].status2')]
 freeze_function=prefix+freeze+'        }\n'+suffix+'}\n'
 chance=block(source,r'^static void Cmd_seteffectwithchance\(void\)\n')
 # The freeze entry merely sets byte 4 then jumps to the common hit script.
 scripts=sources['data/battle_scripts_1.s']
 freeze_script=scripts.split('BattleScript_EffectFreezeHit::\n')[1].split('\n\n')[0]
 assert [x.strip() for x in freeze_script.splitlines()]==['setmoveeffect MOVE_EFFECT_FREEZE','goto BattleScript_EffectHit']
 hit=scripts.split('BattleScript_HitFromAccCheck::\n')[1].split('BattleScript_MoveEnd::')[0]
 commands=['accuracycheck','attackstring','ppreduce','critcalc','damagecalc','typecalc','adjustnormaldamage','datahpupdate','critmessage','resultmessage','seteffectwithchance','tryfaintmon']
 offsets=[hit.index(x) for x in commands];assert offsets==sorted(offsets)
 adjust=block(source,r'^static void Cmd_adjustnormaldamage\(void\)\n')
 assert 'gBattleMoveDamage = gBattleMons[gBattlerTarget].hp - 1;' in adjust
 assert 'MOVE_RESULT_NO_EFFECT' not in adjust and 'MOVE_RESULT_FOE_ENDURED' in adjust and 'MOVE_RESULT_FOE_HUNG_ON' in adjust
 crit=block(source,r'^static void Cmd_critmessage\(void\)\n')
 assert 'gCritMultiplier == 2 && !(gMoveResultFlags & MOVE_RESULT_NO_EFFECT)' in crit
 metadata={}
 for name in ['ICE_PUNCH','ICE_BEAM','BLIZZARD','POWDER_SNOW']:
  fields=dict(re.findall(r'\.(\w+) = ([^,]+),',block(sources['src/data/battle_moves.h'],rf'^    \[MOVE_{name}\] =')))
  assert fields['effect']=='EFFECT_FREEZE_HIT' and fields['secondaryEffectChance']=='10'
  assert fields['target']==('MOVE_TARGET_BOTH' if name in ['BLIZZARD','POWDER_SNOW'] else 'MOVE_TARGET_SELECTED')
  metadata[name]=fields
 table=re.search(r'^    \[MOVE_EFFECT_FREEZE\].*= STATUS1_FREEZE,$',source,re.M).group()
 unit=(HERE/'source-prefix.h').read_text()+'\nstatic const u32 sStatusFlagsForMoveEffects[8]={\n'+table+'\n};\n'+freeze_function+'\n'+chance+'\n'+(HERE/'source-main.c').read_text()
 with tempfile.TemporaryDirectory(prefix='firered-freeze-oracle-') as tmp:
  tmp=Path(tmp);(tmp/'probe.c').write_text(unit);outputs=[]
  for name,flags in [('optimized',['-O2']),('ubsan',['-O1','-g','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
   run([os.environ.get('CC','cc'),'-std=c11','-Wall','-Wextra','-Werror','-Wno-unused-parameter',*flags,'-I',str(a.source/'include'),str(tmp/'probe.c'),'-o',str(tmp/name)])
   outputs.append(run([str(tmp/name)]))
 assert outputs[0]==outputs[1]
 rows=[json.loads(x) for x in outputs[0].splitlines()];assert len(rows)==2400
 for row in rows:
  mode=row['mode'];expected=8 if mode==7 else 32 if mode in [0,6,11] and (mode==11 or row['roll']<=10*(1+row['serene'])) else 0
  assert row['status']==expected,row
  assert row['draws']==(0 if mode==11 else 1),row
  assert row['writes']==row['cancels']==(1 if expected==32 else 0),row
 snap={'schema':'firered-freeze-source-vectors-v1','sourceCommit':PIN,'sourceFiles':{n:sha(v) for n,v in sources.items()},'slices':{n:sha(v) for n,v in {'preamble':prefix,'freezeArm':freeze,'statusTail':suffix,'chanceCommand':chance}.items()},'scriptOrder':commands,'moves':metadata,'vectors':rows}
 if a.capture:
  with golden.open('x') as f:json.dump(snap,f,separators=(',',':'));f.write('\n')
 else:assert snap==json.loads(golden.read_text()),'source golden mismatch'
 report={'passed':True,'vectors':len(rows),'sourceCommit':PIN,'optimizedAndUbsanEqual':True,'goldenSha256':hashlib.sha256(golden.read_bytes()).hexdigest()}
 if a.report:a.report.write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps(report))
if __name__=='__main__':main()
