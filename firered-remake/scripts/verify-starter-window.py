#!/usr/bin/env python3
"""Check captured native starter flows without inventing or rewriting expected source state."""
import hashlib,json,pathlib,sys
ROOT=pathlib.Path(__file__).resolve().parent.parent

def flow(stem):
 p=ROOT/'evidence'/stem
 runtime=json.loads(p.with_suffix('.json').read_text(encoding='utf-8'))
 if runtime['display']=='headless' or runtime['runtimeMajor']!=10 or not runtime['engine'].startswith('4.7.2'):raise ValueError('native runtime gate')
 rows=[json.loads(l) for l in p.with_suffix('.jsonl').read_text(encoding='utf-8').splitlines()]
 return runtime,rows

def main():
 if len(sys.argv)!=1:raise ValueError('no arguments accepted')
 runtime,rows=flow('starter-runtime');commands=[x['data'] for x in rows if x['kind']=='command'];accepted=[x for x in commands if x['status']=='Accepted']
 first=next(x['data']['state'] for x in rows if x['kind']=='ready')
 if first['World']['Player']['MapId']['Value']!='map.players-house-2f':raise ValueError('full route did not start upstairs')
 golden=(ROOT/'tests/Game.StoryAudit.Tests/Fixtures/local/pallet-opening-76.json').read_bytes()
 if hashlib.sha256(golden).hexdigest()!='db0662eb595260db583e7c869fbd5269190f3c8b8ae1d172a8d542d7276c706b':raise ValueError('opening golden hash')
 directions=['North','East','South','West']
 for actual,step in zip(accepted[:72],json.loads(golden)['steps'][:72],strict=True):
  s=actual['state'];p=s['World']['Player'];expect=step['expected'];c=actual['command'];gc=step['command']
  if c['Kind']!=gc['kind'] or directions[c['Direction']]!=gc['direction'] or c['Choice']!=gc['choice'] or (p['MapId']['Value'],p['X'],p['Y'],directions[p['Facing']])!=(expect['map'],expect['x'],expect['y'],expect['facing']):raise ValueError('native opening route mismatch')
 states=[x['state'] for x in accepted]
 decline=[s for s in states if s['Script']['Completed'] and s['Script']['Variables'].get('VAR_RESULT')==0 and s['Script']['Variables'].get('VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB')==2 and s['LastTalked']=='LOCALID_BULBASAUR_BALL']
 if not decline or any(s['Party'] or s['World']['Rng']['Cursor'] for s in decline):raise ValueError('decline side effects')
 if not any(x['status']=='Rejected' and x['Error']=='nickname_character_unsupported' for x in commands):raise ValueError('missing invalid nickname rejection')
 final=next(s for s in reversed(states) if s['Script']['Completed'] and s['Script']['Variables'].get('VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB')==3)
 party=final['Party']
 if len(party)!=1 or party[0]['Pokemon']['SourceSpeciesId']!=1 or party[0]['Nickname']!='sprout' or final['World']['Rng']['Cursor']!=4:raise ValueError('final gifted identity')
 saves=[x['data']['hash'] for x in rows if x['kind']=='saved'];loads=[x['data']['hash'] for x in rows if x['kind']=='loaded']
 if not saves or not loads or saves[-1]!=loads[-1]:raise ValueError('native save/load mismatch')
 if not any(x['kind']=='saveRejected' for x in rows) or not any(x['kind']=='loadRejected' for x in rows):raise ValueError('event save/load policy not exercised')
 report={'passed':True,'scope':'full native upstairs-to-gift flow; separate updated-widget check uses a validated native checkpoint','fullRouteRuntime':runtime,'starter':1,'nickname':'sprout','creationRngDraws':4,'scene':3,'stableSaveHash':saves[-1],'declineNoGift':True,'invalidNicknameRejected':True,'eventSaveAndLoadRejected':True,'limits':['Initial native pass discovered editing-state and fanfare-wait presentation defects; verify final checkpoint pass separately.','Original message frame/font/naming layout, audible music/cry and first battle not accepted here.']}
 final_runtime,final_rows=flow('starter-final-runtime')
 checkpoint=next(x['data'] for x in final_rows if x['kind']=='checkpointLoaded')
 final_commands=[x for x in final_rows if x['kind']=='command']
 rejected=[x for x in final_commands if x['data']['status']=='Rejected' and x['data']['Error']=='nickname_character_unsupported']
 accepted_names=[x for x in final_commands if x['data']['status']=='Accepted' and x['data']['command']['Kind']=='nickname']
 if not rejected or len(accepted_names)!=1 or accepted_names[0]['data']['command']['Text']!='splash':raise ValueError('updated input recovery not exercised')
 if accepted_names[0]['data']['state']['Party'][0]['Nickname']!='splash':raise ValueError('updated input did not commit chosen name')
 waits=[]
 for i,row in enumerate(final_commands):
  state=row['data']['state'];wait=state['Script']['Wait']
  if wait and wait['Kind']==3 and any(e['Opcode']=='waitfanfare' for e in state['PresentationEffects']):
   end=next((n for n in final_commands[i+1:] if n['data']['state']['Script']['Wait']!=wait),None)
   if not end:raise ValueError('incomplete fanfare wait')
   waits.append(end['presentationMillis']-row['presentationMillis'])
 if len(waits)!=2 or any(ms<2800 for ms in waits):raise ValueError('fanfare presentation counter not retained')
 stable=next(x['data']['state'] for x in reversed(final_commands) if x['data']['state']['Script']['Completed'])
 if stable['Script']['Variables'].get('VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB')!=3 or len(stable['Party'])!=1 or stable['Party'][0]['Pokemon']['SourceSpeciesId']!=7 or stable['World']['Rng']['Cursor']!=4:raise ValueError('checkpoint route final state')
 saved=[x['data']['hash'] for x in final_rows if x['kind']=='saved'];loaded=[x['data']['hash'] for x in final_rows if x['kind']=='loaded']
 if not saved or not loaded or saved[-1]!=loaded[-1]:raise ValueError('updated native save/load mismatch')
 report['updatedCheckpointPass']={'passed':True,'runtime':final_runtime,'checkpointHash':checkpoint['hash'],'starter':7,'nickname':'splash','fanfareWaitMilliseconds':waits,'stableSaveHash':saved[-1],'scope':'legitimate source scene2 player-save restore; native correction without refocusing verified by UI actions'}
 report['limits'][0]='Updated editing-state and source fanfare-counter guards were independently exercised from a validated native scene2 checkpoint; the full initial run keeps its own module identity.'
 (ROOT/'evidence/starter-native-flow.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n');print(json.dumps(report,indent=2));return 0
try:sys.exit(main())
except Exception as e:print(json.dumps({'passed':False,'error':str(e)}));sys.exit(2)
