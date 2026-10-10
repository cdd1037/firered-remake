#!/usr/bin/env python3
"""Verify observed native commands against the separately reviewed opening golden. Never generate it."""
import hashlib,json,pathlib,sys
ROOT=pathlib.Path(__file__).resolve().parent.parent
GOLDEN=ROOT/'tests/Game.StoryAudit.Tests/Fixtures/local/pallet-opening-76.json'
def main():
 if len(sys.argv)!=1:raise ValueError('No arguments accepted')
 data=GOLDEN.read_bytes()
 if hashlib.sha256(data).hexdigest()!='db0662eb595260db583e7c869fbd5269190f3c8b8ae1d172a8d542d7276c706b':raise ValueError('golden_hash_mismatch')
 golden=json.loads(data);runtime=json.loads((ROOT/'evidence/story-runtime.json').read_text(encoding='utf-8'))
 rows=[json.loads(l) for l in (ROOT/'evidence/story-runtime.jsonl').read_text(encoding='utf-8').splitlines()]
 ready=next(x['data'] for x in rows if x['kind']=='ready')
 if ready['ContentVersion']!=golden['contentVersion']:raise ValueError('content_version_mismatch')
 if runtime['display']=='headless' or runtime['runtimeMajor']!=10 or not runtime['engine'].startswith('4.7.2'):raise ValueError('native_runtime_mismatch')
 commands=[x['data'] for x in rows if x['kind']=='command'];directions=['North','East','South','West'];waits=['None','Dialog','Choice','Presentation','NativeEffect','MessagePrinting']
 if len(commands)!=76:raise ValueError('expected_exactly_76_commands')
 for actual,step in zip(commands,golden['steps'],strict=True):
  s=actual['state'];p=s['World']['Player'];w=s['Script']['Wait'];d=s['Dialogue'];c=actual['command']
  command={'kind':c['Kind'],'direction':directions[c['Direction']],'choice':c['Choice']}
  expect={'map':p['MapId']['Value'],'x':p['X'],'y':p['Y'],'elevation':p['Elevation'],'facing':directions[p['Facing']],'waitKind':waits[w['Kind']] if w else None,'correlationId':w['CorrelationId'] if w else None,'dialogue':d['TextSymbol'] if d else None,'scene':s['Script']['Variables'].get('VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB',0),'revision':actual['Revision']}
  if actual['status']!='Accepted' or command!=step['command'] or expect!=step['expected']:raise ValueError('native_golden_step_mismatch:'+str(step['number']))
 final=commands[-1]['state'];oak=final['Actors']['LOCALID_OAKS_LAB_PROF_OAK']
 if not final['Script']['Completed'] or final['Dialogue'] is not None or not oak['Visible']:raise ValueError('final_stability_or_actor_failed')
 if not any(x['kind']=='saveRejected' and 'save_event_active' in x['data']['error'] for x in rows):raise ValueError('missing_event_save_rejection')
 if not any(x['kind']=='saved' and x['data']['hash']==commands[71]['StateHash'] for x in rows):raise ValueError('stable_save_not_verified')
 result={'passed':True,'commands':76,'goldenSha256':hashlib.sha256(data).hexdigest(),'runtime':runtime,'sourceContentVersion':ready['ContentVersion'],'finalStateHash':commands[-1]['StateHash'],'scene':2,'oakVisible':True,'eventSaveRejected':True,'stableSaveRecorded':True,'limits':['Window rendering/keyboard were manually inspected; this script checks captured command evidence.','Sound output, original font/frame art, original door transition animation, random NPC idle motion and starter selection remain incomplete.','The story view does not expose player save loading yet.']}
 (ROOT/'evidence/story-native-opening.json').write_text(json.dumps(result,indent=2)+'\n', encoding='utf-8', newline='\n');print(json.dumps(result,indent=2));return 0
try:sys.exit(main())
except Exception as e:print(json.dumps({'passed':False,'error':str(e)}));sys.exit(2)
