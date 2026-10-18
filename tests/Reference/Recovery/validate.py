#!/usr/bin/env python3
import json,hashlib
from pathlib import Path
here=Path(__file__).resolve().parent;count=0
def check(v):
 global count
 assert v
 count+=1
def read(f):return json.loads((here/(f+'-vectors.json')).read_text())
m=read('motion');t=read('tasks');s=read('schedule');a=read('material')
check(len(m['cases'])==48);check(sum(len(c['rows']) for c in m['cases'])==1950)
for c in m['cases']:
 rows=c['rows'];kind=c['kind'];check(rows[-1]['visualCount']==0 and not rows[-1]['sprites']);check(rows[0]['sprites'][0][0]==(4 if c['doubles'] else 2));check(rows[-1]['frame']==[61,38,16,16,17,135][kind]);check([r['frame'] for r in rows]==list(range(len(rows))))
 if kind in [1,5]:
  check([sprite[7:9] for sprite in rows[0]['sprites']]==[[0,1],[0,0],[0,0]]);check([sprite[7] for sprite in rows[1]['sprites']]==[0,8,12])
 if kind==5:
  check(max(len(r['sprites']) for r in rows)==18);check(max(sum(p[8] for p in r['sprites']) for r in rows)==14);check(22+18+4<64)
 if kind==4:check(rows[-1]['frame']<17+2)
check(len(t['cases'])==28);check(sum(len(c['rows']) for c in t['cases'])==2632)
for c in t['cases']:
 rows=c['rows'];kind=c['kind'];check(rows[-1]['alive']==0)
 if kind==0:check(rows[-1]['frame']==157 and rows[-1]['bg1']==[0,0] and rows[-1]['registers']==[0,0]);check(sum(e[0]==7 for r in rows for e in r['events'])==4)
 if kind==1:
  check(rows[-1]['frame']==75);check(rows[0]['palettes'][0]==[0]+[1|(1<<5)|(1<<10)]*15);check(rows[15]['palettes'][0]==[0]+[27|(29<<5)|(31<<10)]*15)
  check(rows[-1]['signals']==[1,1,1,1,0]);check(rows[0]['signals']==[0]*5)
  for row in rows[:16]:check(all(row['palettes'][i][0]==0 for i in range(3)))
 if kind==2:check(rows[-1]['frame']==64 and rows[-1]['registers'][1]==16)
 if kind==3:check(rows[-1]['frame']==130)
check([c['birthFrames'] for c in s['cases']]==[[3,25,47],[32+7*i for i in range(15)],[3*i for i in range(40)]]);check(len(a['sprites'])==5 and sum(len(c['frames']) for c in a['sprites'])==48)
for c in a['sprites']:check(len(c['palette'])==16);check(all(len(f['indices'])==c['frameSize'][0]*c['frameSize'][1] for f in c['frames']))
for c in a['backgrounds']:check(len(c['palette'])==16 and c['samples'])
for p in here.glob('*-vectors.json'):check('/tmp/' not in p.read_text() and '/workspace/' not in p.read_text())
result=dict(passed=True,assertions=count,motionCases=48,motionRows=1950,taskCases=28,taskRows=2632,scheduleCases=3,scheduleRows=sum(len(c['rows']) for c in s['cases']),spritePixelFrames=48,backgroundSamples=sum(len(c['samples']) for c in a['backgrounds']),fixtures={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(here.glob('*-vectors.json'))})
(here/'verification.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
