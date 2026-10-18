#!/usr/bin/env python3
"""Bounded structural/phase checks independent of product implementations."""
import argparse,json
from pathlib import Path
HERE=Path(__file__).resolve().parent
assertions=0
p=argparse.ArgumentParser();p.add_argument('--fixtures',type=Path);a=p.parse_args()
def read(relative, family):
 path=a.fixtures/f'overlay-{family}-source-vectors.json' if a.fixtures else HERE/relative
 if not path.is_file():raise FileNotFoundError(f'Missing {path}. Generate explicitly: python tests/Reference/Bg1Overlay/check_vectors.py --repository . --prepare tests/Game.Presentation.Tests/Fixtures')
 return json.loads(path.read_text())

def check(value):
 global assertions
 assert value
 assertions+=1

overlays=read('overlay/vectors.json', 'task')
check(overlays['profile'] in ['representative','full']);check(len(overlays['cases'])==(99 if overlays['profile']=='full' else 12))
for c in overlays['cases']:
 rows=c['rows'];check(rows[0]['phase']==0);check(rows[0]['alive']==1)
 check(rows[0]['registers']==[0x3f42,0x1000,0,0]);check(rows[0]['bg1']==[0,0])
 check(rows[-1]['alive']==0);check(rows[-1]['registers'][:2]==[0,0]);check(rows[-1]['data'][12]==4)
 check(rows[-1]['events'][-1]==[12,3,0,0,0]);check([e[1] for e in rows[-1]['events'] if e[0]==11]==[1,2])
 if 'edge' not in c['name']:
  check([r['frame'] for r in rows]==list(range(len(rows))))
  expected={'scary-face':(28,49,77,78),'haze-scrolling-fog':(76,157,193,194),'mist-ball-fog':(19,100,120,121)}[c['name']]
  check(tuple(next(r['frame'] for r in rows if r['data'][12]==i) for i in range(1,5))==expected)
  check(all(r['alive']==1 for r in rows[:-1]))
  if c['name']!='scary-face':
   check(all(r['bg1'][0]==(-r['frame'])%65536 for r in rows[1:-1]));check(rows[-1]['bg1']==[0,0])
  if c['name']=='mist-ball-fog':
   check(all((r['registers'][1]&255)+(r['registers'][1]>>8)==17 for r in rows[1:101]))
  if c['name']=='scary-face':
   asset=1 if c['contest'] else 2 if c['target']&1 else 3
   check(any((e[0]==4 and e[1]==asset)or(e[0]==5 and e[2]==asset) for e in rows[0]['events']))
 for r in rows:
  check(all(-32768<=v<=32767 for v in r['data']));check(all(0<=v<=65535 for v in r['bg1']))

mist=read('mist/vectors.json', 'mist')
check(mist['profile'] in ['representative','full']);check(len(mist['cases'])==(180 if mist['profile']=='full' else 30));check({c['args'][5] for c in mist['cases'] if not c['probe']}=={0,1,256,257})
for c in mist['cases']:
 rows=c['rows'];check(rows[0]['invocation']==-1);check(rows[1]['invocation']==0);check(rows[1]['callback_after']==1)
 check(rows[-1]['alive']==0);check(rows[-1]['visual_count']==0);check(rows[-2]['alive']==1)
 check(rows[-2]['callback_before']==3 and rows[-2]['callback_after']==4)
 check(rows[-1]['callback_before']==4 and rows[-1]['events']==[[5,0,0,0,0],[6,0,0,0,0]])
 if c['name']!='u16-accumulator-wrap':
  duration=c['args'][4];check(rows[-1]['invocation']==duration+3)
  check(rows[2]['position'][2:]==[0,0]);check(rows[2]['data'][0]==duration);check(rows[2]['callback_after']==2)
  movement=[r for r in rows if r['invocation']>=2 and r['callback_after']==3]
  check(len(movement)==duration);check([r['data'][0] for r in movement]==list(range(duration-1,-1,-1)))
  check(all(r['visual_count']==1 for r in rows[:-1]))
  expected=(((-c['args'][2]) if c['attacker']&1 else c['args'][2])+32768)%65536-32768
  check(rows[2]['args'][2]==expected)
 for r in rows:
  check(all(-32768<=v<=32767 for v in r['data']+r['args']+r['position']))

material=read('material/vectors.json', 'material')
for m in material['materials']:
 check(len(m['palette_rgb555'])==16);check(all(0<=v<=32767 for p in m['control_palette_rgb555'] for v in p))
 for s in m['samples']:
  entry,tile,bank,x,y,index,color,opaque=s['value'];check(entry&1023==tile);check(entry>>12==bank);check(opaque==int(index!=0));check(color==m['control_palette_rgb555'][bank][index])
print(json.dumps({'passed':True,'assertions':assertions,'overlay_cases':len(overlays['cases']),'mist_cases':len(mist['cases']),'materials':len(material['materials'])}))

from glare.generate_glare_vectors import validate as validate_glare
glare=read('glare/glare-source-vectors.json','glare')
validate_glare(glare, full=glare['profile']=='full')
print('Glare source phases and representative boundary coverage passed.')
