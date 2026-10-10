#!/usr/bin/env python3
"""Verify the frozen native source-UI run; never record or update a golden."""
import hashlib,json,os,sys
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
REF=Path(os.environ.get('FIRERED_ORIGINAL_REFERENCE','/workspace/shared/firered-reference-build/pokefirered'))
SPECS={'source-ui-pc-native-comparison.json': {'original': 'pokefirered-9.png', 'originalSha256': 'd7ab260b3e4a7886d48a53e64fbf8dbd166d41d7b5298ba01d667d824d711e6c', 'mask': None, 'metaHash': '6e495c6774ee2ec4131f28a39029d5f15d94ac1fda53d2dcbbaed8694c0806f7'}, 'source-ui-oak-native-comparison.json': {'original': 'pokefirered-29.png', 'originalSha256': 'a420bbab3a16247f9eea711b2d00265b7bf36ef2f567b34966caaa9951229d20', 'mask': [130, 136, 140, 148], 'metaHash': 'e453158e3f22a8126882bd2372dba020035fd34d227b29cfb613acc310c4d4fe'}, 'source-ui-bag-native-comparison.json': {'original': 'pokefirered-38.png', 'originalSha256': 'c63b22556c033db816712eb5f79f78e2e47b5aceef92b6573d4e1c8a09dd43b8', 'mask': [62, 64, 80, 80], 'metaHash': '761d4719afdf466f4f358cf3232158ce4d3efa4bc3a688588266317f8d88039d'}}
INPUTS_SHA256='9e85c5a0c3090490a697c5ff51cec3d787ff89d36a2259e1b409280e4ced09ae'
REPLAY_SHA256='7688d31a4bb3e07567fdedd86252afecaf8456e6d1b774d7d32c1bc1be709691'
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def need(value,message):
 if not value:raise ValueError(message)
try:
 need(len(sys.argv)==1,'unexpected arguments')
 need(sha(ROOT/'evidence/source-ui-final-inputs.json')==INPUTS_SHA256,'source manifest changed')
 need(sha(ROOT/'evidence/source-ui-final-replay.json')==REPLAY_SHA256,'replay evidence changed')
 manifest=read(ROOT/'evidence/source-ui-final-inputs.json')
 need(len(manifest['files'])==8,'empty or incomplete source snapshot')
 for f in manifest['files']:need(sha(ROOT/f['path'])==f['sha256'],'native source snapshot changed: '+f['path'])
 runtime=read(ROOT/'evidence/source-ui-final-runtime.json')
 need(runtime['runtimeMajor']==10 and runtime['engine'].startswith('4.7.2') and runtime['display']!='headless','native toolchain was not observed')
 replay=read(ROOT/'evidence/source-ui-final-replay.json')
 need(replay['passed'] and replay['fromNewGame'] and replay['loads']==1 and replay['commands']==184,'native route incomplete')
 need(sha(ROOT/'evidence/source-ui-final-runtime.jsonl')==replay['traceSha256'],'native trace changed')
 need(replay['Level']==6 and replay['CurrentHp']==21 and replay['Money']==3080,'native reward or recovery differs')
 rows=[json.loads(x) for x in (ROOT/'evidence/source-ui-final-runtime.jsonl').read_text(encoding='utf-8').splitlines()]
 commands=[r['data'] for r in rows if r['kind']=='command']
 rejected=[r for r in commands if r['command']['Kind']=='battle_potion' and r['status']=='Rejected']
 used=[r for r in commands if r['command']['Kind']=='battle_potion' and r['status']=='Accepted']
 need(len(rejected)==1 and len(used)==1,'native failed/successful Potion use differs')
 need(not used[0]['state']['Inventory']['BagItems'] and not used[0]['state']['Inventory']['PcItems'],'Potion not consumed exactly once')
 need(sum(r['kind']=='battleStatsPage' for r in rows)==1,'two-page level display not exercised')
 evidence=[]
 for filename in ['source-ui-pc-native-comparison.json','source-ui-oak-native-comparison.json','source-ui-bag-native-comparison.json']:
  spec=SPECS[filename];need(sha(ROOT/'evidence'/filename)==spec['metaHash'],'comparison metadata changed')
  meta=read(ROOT/'evidence'/filename);native=meta.get('native',meta.get('nativeFile'));original=meta.get('original',meta.get('originalFile'))
  need(sha(ROOT/native)==meta['nativeSha256'],'native capture changed')
  need(sha(REF/original)==meta['originalSha256'],'original reference changed')
  a=Image.open(ROOT/native).convert('RGB');b=Image.open(REF/original).convert('RGB');need(a.size==b.size==(240,160),'capture dimensions differ')
  need(original==spec['original'] and meta['originalSha256']==spec['originalSha256'],'wrong reference')
  excluded=spec['mask'];count=0;different=0
  for y in range(160):
   for x in range(240):
    if excluded and excluded[0]<=x<excluded[2] and excluded[1]<=y<excluded[3]:continue
    count+=1;different+=a.getpixel((x,y))!=b.getpixel((x,y))
  need(different==0,'original native pixel comparison differs: '+filename)
  evidence.append(dict(reference=original,comparedPixels=count,differences=different,excludedAnimatedArrow=excluded))
 print(json.dumps(dict(passed=True,commands=replay['commands'],captures=evidence,note='Selected native source UI states only; animation, party adapter, remaining menu pixels and audio are separate gates.'),indent=2))
except Exception as e:
 print(json.dumps(dict(passed=False,error=str(e)),indent=2));sys.exit(1)
