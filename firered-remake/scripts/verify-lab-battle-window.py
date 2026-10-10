#!/usr/bin/env python3
"""Verify retained local native battle evidence. Does not generate input or bless changed recordings."""
import hashlib,json,pathlib,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
def read(path):return json.loads((ROOT/path).read_text(encoding='utf-8'))
def sha(path):return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()
if len(sys.argv)!=1:raise SystemExit(2)
try:
 baseline=read('evidence/lab-battle-ui-inputs.json')
 for path,expected in baseline['fullRunSources'].items():assert sha(path)==expected,('changed_source',path)
 result={'passed':True,'sourceCommit':'037335f4c725d7c9aecdac87066f2002b4bd7e14','scope':'bounded native no-items first-lab-battle route','runs':[],'audioAccepted':False,'originalVisualComparisonAccepted':False,'staticArtCatalogSha256':sha('content/generated/lab-battle-art/lab-battle-art.json')}
 for name,outcome in [('win',1),('full',2)]:
  runtime=read(f'evidence/lab-battle-{name}-runtime.json');replay=read(f'evidence/lab-battle-{name}-replay.json');trace=f'evidence/lab-battle-{name}-runtime.jsonl'
  assert replay['passed'] and replay['traceSha256']==sha(trace) and replay['history']['Outcome']==outcome
  assert runtime['runtimeMajor']==10 and runtime['engine'].startswith('4.7.2') and runtime['display']=='X11' and runtime['nodeEnteredSceneTree']
  assert replay['loads']==1 and replay['rejectedSaves']>=1 and replay['history']['ExperienceGained']==(69 if outcome==1 else 0) and replay['Money']==(3080 if outcome==1 else 3000)
  assert replay['Level']==(6 if outcome==1 else 5) and replay['CurrentHp']==(22 if outcome==1 else 20)
  result['runs'].append({'name':name,'runtime':runtime,'replay':replay,'scope':'validated scene3 checkpoint through victory/save/load' if name=='win' else 'new game upstairs through source opening/gift/loss/field/save/load'})
 assert sha('tests/Game.StoryAudit.Tests/Fixtures/local/pallet-opening-76.json')=='db0662eb595260db583e7c869fbd5269190f3c8b8ae1d172a8d542d7276c706b'
 rows=[json.loads(line) for line in (ROOT/'evidence/lab-battle-full-runtime.jsonl').read_text(encoding='utf-8').splitlines()]
 commands=[row['data'] for row in rows if row['kind']=='command'];gold=read('tests/Game.StoryAudit.Tests/Fixtures/local/pallet-opening-76.json')['steps'];directions=['North','East','South','West'];waits=['None','Dialog','Choice','Presentation','NativeEffect','MessagePrinting','Nickname','Battle']
 for i,(actual,expected) in enumerate(zip(commands[:72],gold[:72])):
  state=actual['state'];p=state['World']['Player'];e=expected['expected'];wait=state['Script']['Wait'];dialogue=state['Dialogue']
  assert actual['status']=='Accepted' and actual['command']['Kind']==expected['command']['kind'],('command',i)
  assert (p['MapId']['Value'],p['X'],p['Y'],p['Elevation'],directions[p['Facing']])==(e['map'],e['x'],e['y'],e['elevation'],e['facing']),('pose',i)
  assert (waits[wait['Kind']] if wait else None)==e['waitKind'] and (wait['CorrelationId'] if wait else None)==e['correlationId'],('wait',i)
  assert (dialogue['TextSymbol'] if dialogue else None)==e['dialogue'] and state['Script']['Variables'].get('VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB',0)==e['scene'],('scene',i)
 assert len(commands)>=72
 assert any(r['kind']=='saveRejected' and r['data']['error']=='save_semantic_invalid: save_event_active' for r in rows)
 assert any(r['kind']=='loadRejected' and r['data']['error']=='current_event_or_presentation_active' for r in rows)
 battle=[c['state']['Battle'] for c in commands if c['state']['Battle']]
 assert any(b['State']['Opponent']['AttackStage']==0 for b in battle)
 assert any(e['kind']=='stat-at-minimum' for b in battle for e in b['Events'])
 result['opening72MatchReviewedGolden']=True
 result['screenshotsLocalOnly']={path.name:hashlib.sha256(path.read_bytes()).hexdigest() for path in (ROOT/'evidence').glob('lab-battle-*.png')}
 result['limitations']=['Scene backgrounds, UI frames/fonts and move/faint/trainer animations are provisional.','Native window did not run a Draw or all-PP-zero battle; those are independently tested at application/domain layers.','No PC Potion or battle bag use yet. No original frame-driven VBlank RNG equivalence.','Win run precedes save-prevalidation/20-percent HP color fixes; full new-game loss run verifies those changes.','Dummy audio is not audio acceptance. Quest-log requests are not original journal persistence.']
 (ROOT/'evidence/lab-battle-native-validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8',newline='\n');print(json.dumps({'passed':True,'nativeWinCommands':result['runs'][0]['replay']['commands'],'nativeFullCommands':result['runs'][1]['replay']['commands'],'openingGoldenCommands':72,'audioAccepted':False}))
except Exception as error:
 print(json.dumps({'passed':False,'error':str(error)}));raise SystemExit(1)
