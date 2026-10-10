#!/usr/bin/env python3
"""Compare generated note events against original-bytecode inventory, without synthesis."""
import hashlib,json,pathlib,re
root=pathlib.Path(__file__).resolve().parents[1]
j=json.loads((root/'specs/inventory.json').read_text());result={}
pat=re.compile(r'track=(\d+) tick=(\d+) note key=(\d+) velocity=(\d+) gate=(\d+) program=(\d+)')
for song,file in [(j['songs'][0],'pallet.wav.events.txt'),(j['songs'][1],'select.wav.events.txt')]:
 path=root/'results'/file;data=path.read_bytes();events=[]
 for line in data.decode().splitlines():
  match=pat.search(line)
  if match:
   tr,t,k,v,g,p=map(int,match.groups())
   if t<1536:events.append((tr+1,t,g,k,v,p))
 expected=sorted([(t['index'],n['tick'],n['duration'],n['key'],n['velocity'],n['voice']) for t in song['tracks'] for n in t['notes']]);actual=sorted(events)
 result[song['name']]={'trace_path':str(path.relative_to(root)),'trace_sha256':hashlib.sha256(data).hexdigest(),'events_match_original_inventory':actual==expected,'expected_notes':len(expected),'rendered_notes':len(actual),'missing':sorted(set(expected)-set(actual))[:10],'extra':sorted(set(actual)-set(expected))[:10]}
output=json.dumps(result,indent=2)+'\n';(root/'specs/sequence-audit.json').write_text(output);print(output)
if not all(v['events_match_original_inventory'] for v in result.values()):raise SystemExit(1)
