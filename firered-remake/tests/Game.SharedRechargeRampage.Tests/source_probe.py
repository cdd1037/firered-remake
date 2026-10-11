#!/usr/bin/env python3
"""Authenticate pinned inputs and execute unchanged bounded C command bodies; never import remake output."""
import argparse,hashlib,json,os,re,subprocess,tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent
PIN='037335f4c725d7c9aecdac87066f2002b4bd7e14'
NAMES=('HYPER_BEAM','FRENZY_PLANT','BLAST_BURN','HYDRO_CANNON','THRASH','OUTRAGE','PETAL_DANCE')
def run(argv,**kwargs):
 r=subprocess.run(argv,text=True,capture_output=True,**kwargs)
 if r.returncode:raise RuntimeError(f'{argv!r}\n{r.stdout}\n{r.stderr}')
 return r.stdout
def block(text,pattern):
 m=re.search(pattern,text,re.M);assert m,pattern
 depth=0
 for i in range(text.index('{',m.start()),len(text)):
  depth+=(text[i]=='{')-(text[i]=='}')
  if depth==0:return text[m.start():i+1]
 raise AssertionError('Unclosed block')
def sha(text):return hashlib.sha256(text.encode()).hexdigest()
def commands(s,label):return [x.strip() for x in s.split(label+'::\n',1)[1].split('\n\n',1)[0].splitlines() if x.strip()]
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--source',type=Path,required=True);p.add_argument('--capture',action='store_true');p.add_argument('--report',type=Path);a=p.parse_args()
 root=a.source.resolve();assert run(['git','rev-parse','HEAD'],cwd=root).strip()==PIN
 paths=['src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','src/random.c','src/data/battle_moves.h','data/battle_scripts_1.s','include/random.h']+[f'include/constants/{n}.h' for n in ('battle','battle_move_effects','battle_script_commands','pokemon','moves','abilities')]
 c={p:(root/p).read_text() for p in paths}
 for p in paths:assert c[p]==run(['git','show',f'{PIN}:{p}'],cwd=root),f'Modified source {p}'
 src,util,main,scripts=(c[p] for p in ('src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','data/battle_scripts_1.s'))
 assert commands(scripts,'BattleScript_EffectRampage')==['attackcanceler','accuracycheck BattleScript_PrintMoveMissed, ACC_CURR_MOVE','attackstring','jumpifstatus2 BS_ATTACKER, STATUS2_MULTIPLETURNS, BattleScript_EffectRampage2','ppreduce','BattleScript_EffectRampage2:','confuseifrepeatingattackends','goto BattleScript_HitFromCritCalc']
 assert 'MOVE_EFFECT_RECHARGE | MOVE_EFFECT_AFFECTS_USER | MOVE_EFFECT_CERTAIN' in '\n'.join(commands(scripts,'BattleScript_EffectRecharge'))
 hit='\n'.join(commands(scripts,'BattleScript_EffectHit'));assert hit.index('critcalc')<hit.index('adjustnormaldamage')<hit.index('seteffectwithchance')
 for label in ('BattleScript_MoveUsedIsParalyzed','BattleScript_DoSelfConfusionDmg'):assert 'cancelmultiturnmoves BS_ATTACKER' in commands(scripts,label)
 assert 'cancelmultiturnmoves BS_ATTACKER' not in commands(scripts,'BattleScript_MoveUsedIsFrozen')
 for name in ('TRUANT','FLINCH','DISABLED','RECHARGE','IN_LOVE'):assert 'CancelMultiTurnMoves' in util.split('case CANCELLER_'+name+':',1)[1].split('case CANCELLER_',1)[0]
 sleep=block(main,r'^static void HandleEndTurn_ContinueBattle\(void\)\n');assert 'STATUS1_SLEEP' in sleep and 'STATUS2_MULTIPLETURNS' in sleep and 'CancelMultiTurnMoves(i)' in sleep
 extracted={n:block(src,rf'^static void {n}\(void\)\n') for n in ('Cmd_ppreduce','Cmd_confuseifrepeatingattackends','Cmd_seteffectwithchance')}
 extracted['CancelMultiTurnMoves']=block(util,r'^void CancelMultiTurnMoves\(u8 battler\)\n');extracted['WasUnableToUseMove']=block(util,r'^bool8 WasUnableToUseMove\(u8 battler\)\n');extracted['Random']=block(c['src/random.c'],r'^u16 Random\(void\)\n')
 cases={name:src.split('            case MOVE_EFFECT_'+name+':\n',1)[1].split('            case MOVE_EFFECT_',1)[0] for name in ('RECHARGE','THRASH','CONFUSION')}
 # These cases end with their own break, before the next source case label.
 for name,body in cases.items():extracted['SetMoveEffect_'+name]=body
 thrash=util.split('            case ENDTURN_THRASH:  // thrash\n',1)[1].split('            case ENDTURN_DISABLE:',1)[0];assert thrash.rstrip().endswith('break;');thrash=thrash.rstrip()[:-6]
 extracted['ENDTURN_THRASH']=thrash
 target=block(main,r'^    else if \(gBattleTypeFlags & BATTLE_TYPE_DOUBLE\n          && gBattleMoves\[gChosenMove\].target & MOVE_TARGET_RANDOM\)\n');extracted['HandleAction_RandomTarget']=target
 moves={name:dict(re.findall(r'\.(\w+) = ([^,]+),',block(c['src/data/battle_moves.h'],rf'^    \[MOVE_{name}\] ='))) for name in NAMES}
 assert all(v['target']==('MOVE_TARGET_RANDOM' if k in NAMES[4:] else 'MOVE_TARGET_SELECTED') for k,v in moves.items())
 unit=(HERE/'source-prefix.h').read_text()+'\n'+ '\n'.join(re.findall(r'^#define (?:RAND_MULT|ISO_RANDOMIZE1).*$',c['include/random.h'],re.M))+'\n'
 unit+='static const struct MoveHost gBattleMoves[355]={\n'+',\n'.join(block(c['src/data/battle_moves.h'],rf'^    \[MOVE_{n}\] =') for n in NAMES)+'\n};\n'
 unit+='\n'.join(extracted[n] for n in ('Random','CancelMultiTurnMoves','WasUnableToUseMove','Cmd_ppreduce','Cmd_confuseifrepeatingattackends'))
 unit+='\nstatic void SetMoveEffect(bool8 primary,u8 certain){(void)primary;(void)certain;gEffectBattler=gBattlerAttacker;gBattleCommunication[MOVE_EFFECT_BYTE]&=~MOVE_EFFECT_AFFECTS_USER;switch(gBattleCommunication[MOVE_EFFECT_BYTE]){\n'+'\n'.join('case MOVE_EFFECT_'+n+':\n'+body for n,body in cases.items())+'\n}}\n'
 unit+=extracted['Cmd_seteffectwithchance']+'\nstatic void SourceThrashResidual(void){unsigned effect=0;\n'+thrash+'\n(void)effect;}\n'
 unit+='static void SourceRandomTarget(void)'+target[target.index('{'):]+'\n'+(HERE/'source-main.c').read_text()
 with tempfile.TemporaryDirectory(prefix='firered-recharge-rampage-c-') as tmp:
  tmp=Path(tmp);(tmp/'probe.c').write_text(unit);outputs=[]
  for tag,flags in [('optimized',['-O2']),('ubsan',['-O1','-g','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
   run(['flock','/tmp/firered-remake-build.lock',os.environ.get('CC','cc'),'-std=c11','-Wall','-Wextra','-Werror',*flags,'-I',str(root/'include'),str(tmp/'probe.c'),'-o',str(tmp/tag)]);outputs.append(run([str(tmp/tag)]))
 assert outputs[0]==outputs[1]
 rows=[json.loads(x) for x in outputs[0].splitlines()];assert len(rows)==35
 data={'schema':'firered-recharge-rampage-c-v1','sourceCommit':PIN,'sourceFiles':{p:sha(t) for p,t in c.items()},'extractedC':{n:sha(t) for n,t in extracted.items()},'moves':moves,'vectors':rows}
 dest=HERE/'source-vectors.json'
 if a.capture:
  with dest.open('x') as f:json.dump(data,f,indent=2);f.write('\n')
 else:assert data==json.loads(dest.read_text()),'Immutable source vectors changed'
 result={'passed':True,'vectors':len(rows),'extractedC':len(extracted),'builds':['O2','UBSan'],'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()}
 if a.report:a.report.write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps(result,indent=2))
if __name__=='__main__':main()
