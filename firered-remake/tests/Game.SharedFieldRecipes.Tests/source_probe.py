#!/usr/bin/env python3
import hashlib,json,pathlib,re,subprocess,sys,tempfile
here=pathlib.Path(__file__).resolve().parent
source=pathlib.Path(sys.argv[1]);pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()==pin
files={p:(source/p).read_text() for p in ['src/battle_script_commands.c','src/battle_main.c','src/battle_util.c','data/battle_scripts_1.s']}
def block(text,pattern):
 m=re.search(pattern,text,re.M);assert m,pattern
 start=text.index('{',m.start());depth=0
 for i in range(start,len(text)):
  depth+=(text[i]=='{')-(text[i]=='}')
  if depth==0:return text[m.start():i+1]
 raise AssertionError('tail')
commands=files['src/battle_script_commands.c'];main=files['src/battle_main.c']
extracts=[block(commands,r'^static void Cmd_rapidspinfree\(void\)\n'),block(commands,r'^bool8 UproarWakeUpCheck\(u8 battlerId\)\n'),block(main,r'^u8 IsRunningFromBattleImpossible\(void\)\n')]
start=commands.index('            case MOVE_EFFECT_PAYDAY:');end=commands.index('            case MOVE_EFFECT_TRI_ATTACK:',start)
pay=commands[start:end];extracts.append('static void PayDay(void) { switch(gBattleCommunication[MOVE_EFFECT_BYTE]) {\n'+pay+'} }')
header='\n\n'.join(extracts)+'\n'
manifest={'pin':pin,'inputs':{p:hashlib.sha256(v.encode()).hexdigest() for p,v in files.items()},'extractedSha256':hashlib.sha256(header.encode()).hexdigest()}
for p in ['constants/battle.h','constants/battle_script_commands.h','constants/abilities.h','constants/pokemon.h','constants/items.h','constants/hold_effects.h']:
 manifest['inputs']['include/'+p]=hashlib.sha256((source/'include'/p).read_bytes()).hexdigest()
with tempfile.TemporaryDirectory() as tmp:
 p=pathlib.Path(tmp);(p/'probe.c').write_text((here/'source-prefix.h').read_text()+header+(here/'source-main.c').read_text())
 result=subprocess.run(['cc','-std=c99','-w','-I'+str(source/'include'),str(p/'probe.c'),'-o',str(p/'probe')],capture_output=True,text=True);assert result.returncode==0,result.stderr
 output=subprocess.check_output([str(p/'probe')],text=True);data=json.loads(output);assert data['complete'] and len(data['vectors'])==78
 if '--capture' in sys.argv:
  assert not (here/'source-vectors.json').exists()
  (here/'source-vectors.json').write_text(json.dumps(data,separators=(',',':'))+'\n');(here/'source-provenance.json').write_text(json.dumps(manifest,indent=2)+'\n')
 else:
  assert data==json.loads((here/'source-vectors.json').read_text())
  assert manifest==json.loads((here/'source-provenance.json').read_text())
  # The exact extracted command hash is already verified by manifest equality above.
print('SOURCE FIELD PROOF COMPLETE: 78 exact C vectors')
