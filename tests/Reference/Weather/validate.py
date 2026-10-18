#!/usr/bin/env python3
"""Structural and lifetime checks independent of product implementations."""
import json,hashlib
from pathlib import Path
here=Path(__file__).resolve().parent;n=0

def check(v):
 global n
 assert v
 n+=1

def read(f):return json.loads((here/f).read_text())
bg=read('bg-vectors.json')
for c in bg['cases']:
 rows=c['rows'];check(rows[0]['registers']==[0x3f42,0x1000,0,0]);check(rows[-1]['alive']==0);check(rows[-1]['registers'][:2]==[0,0]);check(rows[-1]['bg1']==[0,0]);check(rows[-1]['data'][12]==4)
 if not c['cleanupProbe']:
  check([r['frame'] for r in rows]==list(range(160)));check(tuple(next(r['frame'] for r in rows if r['data'][12]==i) for i in range(1,5))==(28,129,157,158));direction=6 if c['flag'] and c['attacker']&1 else -6
  for r in rows[1:-1]:check(r['bg1']==[(direction*r['frame'])%65536,(-r['frame'])%65536])
engine=read('engine-vectors.json');ends=[]
for c in engine['cases']:
 rows=c['rows'];check(rows)
 if c['scenario']==10:
  check(len(rows)==63);check(rows[0]['affine']==[0,0,0,80,80,0]);check(rows[0]['bios']==[819,819,0]);check(rows[-1]['visualCount']==0);check(rows[-1]['sprite'][13]==0);check(rows[-2]['sprite'][6]==5 and rows[-2]['sprite'][13]==1)
  for row in rows[:-1]:
   f=row['frame'];check(row['affine'][3:]==[80+2*f,80+2*f,(10*f*256)%65536]);check(row['bios'][:2]==[65536//(80+2*f)]*2)
  check(rows[-1]['sprite'][4:6]==[139,79]);continue
 check([(r['frame'],r['phase']) for r in rows]==[(0,0)]+[(f,p) for f in range(1,rows[-1]['frame']+1) for p in [1,2]])
 events=[(r['frame'],e) for r in rows for e in r['events']];alloc=[(f,e) for f,e in events if e[0]==2];randoms=[e for f,e in events if e[0]==1];check(len(randoms)==2*len(alloc))
 state=c['seed']
 for e in randoms:state=(state*1103515245+24691)&0xffffffff;check(e[4]==state and e[2]==state>>16)
 check(rows[-1]['rng']==state);check(rows[-1]['visualCount']==0);check(not rows[-1]['children']);birth={e[7]:f for f,e in alloc if e[6]!=64};deaths=[(f,e) for f,e in events if e[0]==4];check(len(birth)==len(deaths))
 for f,e in deaths:check(f==birth[e[2]]+20 and e[3:5]==[19,1])
 for row in rows:
  for s in row['children']:
   age=row['frame']-birth[s[1]];check(s[4]==min(13,max(0,age-1)) and s[5]==4*min(13,max(0,age-1)));check(s[14]==max(0,age-1));check(s[9]==int(age==19));check(s[6]==(0 if age==0 else 1))
 if c['scenario']==6:check(len(alloc)==80 and rows[-1]['frame']==137);check([f for f,e in alloc]==[f for f in range(0,120,3) for _ in [0,1]])
 if c['scenario']<=3:ends.append((c['seed'],state));check(len(alloc)==40)
check(len({state for seed,state in ends if seed==1})==1)
m=read('motion-vectors.json')
for c in m['crescents']:
 r=c['rows'];check(r[-1]['alive']==0 and r[-1]['visualCount']==0);check(r[-2]['alive']==1 and r[-2]['callback']==1);check(r[1]['flip']==(8 if c['args'][3] and c['attacker']&1 else 0));check(r[1]['subspriteMode']==1);check(r[1]['args'][1]==(-c['args'][1] if c['args'][3] and c['attacker']&1 else c['args'][1]))
for c in m['heat']:
 r=c['rows'];check(len(r)==130);check(r[-1]['alive']==0);check([next(x['frame'] for x in r if x['data'][0]==p) for p in [1,2,3]]==[16,112,128]);check(r[0]['data'][12]==(-1 if c['attacker']&1 else 1));check(r[0]['data'][15]==(c['target']^2 if c['visibleMask']>>(c['target']^2)&1 else 255))
for c in m['blends']:check(c['rows'][-1]['frame']==([13,13,14,14,0,0][c['kind']]));check(c['rows'][-1]['alive']==0)
for c in m['abi']:
 p=c['row']['pieces'];check(len(p)==c['row']['nextIndex']-c['initialIndex']);check(c['row']['nextIndex']<=c['limit'] or c['initialIndex']>=c['limit']);check(all(0<=v[0]<512 and 0<=v[1]<256 and 0<=v[4]<1024 for v in p))
mat=read('material-vectors.json');check(len(mat['fragmentRenders'])==len([c for c in m['abi'] if not c['nullTable']]));check(len(mat['sprites'])==3);check(len(mat['background']['samples'])>0);check(mat['background']['tilemapBanks']==[8]);check(len(mat['background']['paletteRgb555'])==16)
for r in mat['fragmentRenders']:
 check(len(r['rgb555FieldSha256'])==64);check(r['rgb555FieldSha256']!=r['rgb555Sha256'])
pan=read('pan-vectors.json');check(len(pan['cases'])==7);check(pan['cases'][0]['rows'][-1][0]==127)
for c in pan['cases']:check(c['rows'][-1][1:3]==[0,0]);check(c['rows'][-1][3]==c['args'][1]);check(c['rows'][0][3]==c['args'][0]);check(c['rows'][-1][4]==1)
schedule=read('schedule-vectors.json');check(len(schedule['cases'])==8)
for c in schedule['cases']:
 start=21 if c['move']==201 else 23
 births=[start+12*i for i in range(7)];deaths=[b+n-1 for b,n in zip(births,[39,44,35,39,45,32,35])]
 check(c['birthFrames']==births);check(c['destructionFrames']==deaths);check(c['peakRawCount']==4);check(c['peakVisibleOamCount']==4)
 for row in c['rows']:
  f=row['frame'];check(row['preSpriteCount']==sum(b<=f<=d for b,d in zip(births,deaths)));check(row['postSpriteCount']==sum(b<=f<d for b,d in zip(births,deaths)));check(row['visibleOamCount']==row['postSpriteCount']);check(row['created']==sum(b<=f for b in births));check(0<=row['fieldIntersectCount']<=row['visibleOamCount'])
for f in here.glob('*-vectors.json'):
 txt=f.read_text();check('/tmp/' not in txt and '/workspace/' not in txt and 'compilerVersion' not in txt)
summary={'passed':True,'assertions':n,'fixtures':{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(here.glob('*-vectors.json'))},'rows':{'background':sum(len(c['rows']) for c in bg['cases']),'spriteEngine':sum(len(c['rows']) for c in engine['cases']),'crescents':sum(len(c['rows']) for c in m['crescents']),'heat':sum(len(c['rows']) for c in m['heat']),'palette':sum(len(c['rows']) for c in m['blends']),'oam':len(m['abi']),'fragmentHashes':len(mat['fragmentRenders']),'sound':sum(len(c['rows']) for c in pan['cases']),'scheduling':sum(len(c['rows']) for c in schedule['cases'])}}
(here/'verification.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary))
