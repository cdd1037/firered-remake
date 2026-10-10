#!/usr/bin/env python3
"""Audit only: existing game manifests plus known missing SE_BOO, against fixed source/ROM."""
import collections, hashlib, json, pathlib, re, struct, subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'research-integration'
GAME=ROOT.parent/'firered-remake'
REF=ROOT.parent/'audio-reference-build/pokefirered'
REV='037335f4c725d7c9aecdac87066f2002b4bd7e14'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert subprocess.check_output(['git','-C',str(REF),'rev-parse','HEAD'],text=True).strip()==REV
rom=(REF/'pokefirered.gba').read_bytes()
assert hashlib.sha1(rom).hexdigest()=='41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc'
symbols={n:int(a,16) for a,n in re.findall(r'0x([0-9a-f]{8})\s+(\w+)\s*$',(REF/'pokefirered.map').read_text(),re.M)}
song_ids={n:int(v) for n,v in re.findall(r'^#define ((?:MUS|SE)_\w+)\s+(\d+)',(REF/'include/constants/songs.h').read_text(),re.M)}
table=[(n,int(a),int(b)) for n,a,b in re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',(REF/'sound/song_table.inc').read_text(),re.M)]
counters={n:int(v) for n,v in re.findall(r'=\s*\{\s*(MUS_\w+),\s*(\d+)\s*\}',(REF/'src/sound.c').read_text())}
cry_inventory=json.loads((ROOT/'results/phase4/inventory.json').read_text())
cries={r['speciesConstant']:r for r in cry_inventory['species']}
read=lambda a,n:rom[a-0x08000000:a-0x08000000+n]
u32=lambda a:struct.unpack('<I',read(a,4))[0]

def leaf_tones(a,visited=None):
 visited=set() if visited is None else visited
 if a in visited or len(visited)>1:
  return [{'type':read(a,1)[0], 'toneAddress':a, 'unresolvedStaticCandidate':True}]
 typ,key,length,pan,data,attack,decay,sustain,release=struct.unpack('<4BI4B',read(a,12))
 if typ&0x40:
  return [r for i in sorted(set(read(u32(a+8),128))) for r in leaf_tones(data+i*12,visited|{a})]
 if typ&0x80:
  return [r for i in range(128) for r in leaf_tones(data+i*12,visited|{a})]
 return [{'type':typ,'toneAddress':a,'dataAddress':data,'length':length,'panSweep':pan,'adsr':[attack,decay,sustain,release]}]

rows=[]; manifests=[]; mismatches=[]
for folder in ['small-audio-delivery','repel-audio','town-route-audio','route22-cry-audio']:
 path=GAME/'content/generated'/folder/'manifest.json';d=json.loads(path.read_text());clips=d['clips'] if 'clips'in d else [d['metadata']]
 manifests.append({'path':path.relative_to(GAME).as_posix(),'sha256':sha(path),'count':len(clips),'profile':d['profile']})
 for c in clips:
  s=c.get('source',c)
  rows.append({'id':c['id'],'manifest':folder,'manifested':True,'sourceSymbol':s['sourceSymbol'],'sourcePath':s['sourcePath'],
   'sourceCommit':s['sourceCommit'],'sourcePlayer':s['sourcePlayer'],'originalPlayer':s['sourcePlayer'],'priority':s['priority'],'sourceCounter':s.get('sourceCounter'),'recordedLooping':c.get('loopStartFrame') is not None,
   'recordedPath':c['path'],'recordedPathExists':(path.parent/c['path']).is_file(),'recordedSha256':c.get('sha256',c.get('encodedSha256'))})
assert len(rows)==len({r['id'] for r in rows})==143
rows.append({'id':'SE_BOO','manifest':None,'manifested':False,'sourceSymbol':'SE_BOO','sourceCommit':REV,'sourcePath':'sound/songs/midi/se_boo.mid','missingRecordedReason':'Known Route22 script cue absent from the four runtime catalogs; audited independently, never included in manifested denominator.'})
selection=[]
for r in rows:
 ident=r['id']; name=ident.lower()
 if ident.startswith('CRY_'):
  c=cries[r['sourceSymbol']]
  r.update({'category':'normalcry','species':c['internalId'],'speciesId':c['internalId'],'looping':False,'cryIndex':c['cryIndex'],'cryMode':0,'toneAddress':c['toneAddress'],'toneAddressHex':f"0x{c['toneAddress']:08x}",'waveAddress':c['waveAddress'],'waveAddressHex':f"0x{c['waveAddress']:08x}",'sampleCount':c['sampleCount'],'researchPlayer':2,
   'runtimeAdmission':'existing authenticated 386-species normal catalog; not re-rendered in this audit','existingCoverage':'results/phase5-catalog/coverage.json'})
  assert u32(c['toneAddress']+4)==c['waveAddress']
  continue
 sid=song_ids[ident];a=symbols[name];entry=symbols['gSongTable']+sid*8
 assert table[sid][0]==name and u32(entry)==a and struct.unpack('<HH',read(entry+4,4))==table[sid][1:]
 op=table[sid][1];pri=read(a+2,1)[0];counter=counters.get(ident)
 for key,actual in [('originalPlayer',op),('priority',pri),('sourceCounter',counter)]:
  if key in r and r[key]!=actual:mismatches.append({'id':ident,'field':key,'manifest':r[key],'fixedSource':actual})
  r[key]=actual
 paths=list((REF/'sound/songs').rglob(name+'.s'));assert len(paths)==1
 source=paths[0].read_text();programs=sorted({int(v) for v in re.findall(r'\bVOICE\s*,\s*(\d+)',source)})
 group=u32(a+4);types=set();tones=[]
 for p in programs:
  leaves=leaf_tones(group+p*12);types.update(x['type'] for x in leaves)
  tones.append({'program':p,'toneAddress':group+p*12,'topType':read(group+p*12,1)[0],'potentialLeafTypes':sorted({x['type'] for x in leaves})})
 commands=[]
 for i,line in enumerate(source.splitlines(),1):
  if re.search(r'\b(?:XCMD|xIECV|xIECL|MODT|MEMACC|REPT|PRIO|TEMPO|LFOS|LFODL|MOD|BENDR|KEYSH|TUNE)\b',line):
   commands.append({'line':i,'text':line.strip()})
 looping=bool(re.search(r'\bGOTO\b',source))
 category='BGM' if op==0 else 'fanfare' if counter is not None else 'SE'
 r.update({'name':name,'songId':sid,'songAddress':a,'songAddressHex':f'0x{a:08x}','songTableAddress':entry,'originalPlayerSecondary':table[sid][2],
 'sourcePlayer':op,'category':category,'looping':looping,'musicalSe':ident.startswith('MUS_') and category=='SE','expectedLooping':looping,'researchPlayer':0 if op==0 else 1,
 'trackCount':read(a,1)[0],'reverb':read(a+3,1)[0],'voicegroup':group,'voicegroupSymbol':next(k for k,v in symbols.items() if v==group and k.startswith('voicegroup')),
 'assemblyPath':paths[0].relative_to(REF).as_posix(),'assemblySha256':sha(paths[0]),'declaredPrograms':programs,'declaredTones':tones,'potentialLeafTypes':sorted(types),'parameterDeclarations':commands,
 'seconds':20,'runtimeAdmission':'pending bounded render; static source/header matching alone is not render coverage'})
 selection.append(r)
manifested=[r for r in rows if r['manifested']]
result={'schemaVersion':1,'fixedRevision':REV,'romSha1':hashlib.sha1(rom).hexdigest(),
 'gameCommit':subprocess.check_output(['git','-C',str(GAME),'rev-parse','HEAD'],text=True).strip(),
 'coreCommitAtAuditStart':subprocess.check_output(['git','-C',str(ROOT),'rev-parse','HEAD'],text=True).strip(),
 'scope':'Four explicit main-game runtime catalogs, all normal cries in those catalogs, plus independently marked known-missing SE_BOO. No claim of whole-game reachability.',
 'summary':{'manifestedTotal':len(manifested),'noncry':len(selection)-1,'normalcry':sum(r['category']=='normalcry' for r in rows),'duplicateIds':0,'categories':dict(collections.Counter(r['category'] for r in manifested)),'originalPlayers':dict(collections.Counter(r['originalPlayer'] for r in manifested)),'knownMissingRecorded':['SE_BOO'],'auditedCandidatesIncludingMissing':len(rows),'manifestSourceMismatches':mismatches},
 'manifests':manifests,'classification':'BGM = original player 0; fanfare = source sFanfares counter present; SE = remaining song-table cue including player-2 musical one-shots; normalcry = mode-0 species cue.',
 'cryNormalParameters':cry_inventory['normalParameters'],
 'runtimeSourceSha256':{str(p.relative_to(ROOT)):sha(p) for p in sorted((ROOT/'src').glob('*.cs'))},
 'songs':selection,'normalCries':[r for r in rows if r['category']=='normalcry']}
(OUT/'inventory.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result['summary'],indent=2))
assert not mismatches
assert all(r['recordedPathExists'] for r in manifested)
