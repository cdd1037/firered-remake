#!/usr/bin/env python3
"""Authenticate Silph's five ID-only cues in the existing source sample pack; no audio rendering."""
import argparse,hashlib,importlib.util,json,pathlib,re,struct,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
LOCK_SHA='a49cf3c30dc77c6bfe1e41f1e3a89d3d9d363c3d6edc80a8f363e21af6385ad4'
PACK_SHA='00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d'
CRY_SHA='623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e'
def sha(b):return hashlib.sha256(b).hexdigest()
def safe(root,path):
 if pathlib.PurePosixPath(path).is_absolute() or '\\' in path or ':' in path or any(x in ('','.','..') for x in path.split('/')):raise ValueError('Unsafe Silph source path')
 p=pathlib.Path(root).absolute()/path
 if any(x.is_symlink() for x in (p,*p.parents)):raise ValueError('Silph source symlink rejected')
 return p
def source(root,reference=None):
 b=safe(root,'scripts/silph-field-presentation.lock.json').read_bytes()
 if len(b)!=2485 or sha(b)!=LOCK_SHA:raise ValueError('Silph field lock differs')
 locked=json.loads(b);sources={}
 if locked['commit']!=COMMIT or len(locked['files'])!=10:raise ValueError('Silph field lock scope differs')
 if reference and subprocess.check_output(['git','-C',str(reference),'rev-parse',COMMIT+'^{tree}']).decode().strip()!=locked['tree']:raise ValueError('Reference tree differs')
 for row in locked['files']:
  p=safe(root,'content/raw/silph-field-presentation/'+row['path'])
  b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+row['path']]) if reference else p.read_bytes()
  if (len(b),sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())!=(row['byteLength'],row['sha256'],row['gitBlobSha1']):raise ValueError('Source bytes differ: '+row['path'])
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
  sources[row['path']]=b
 return sources
def build(root,reference=None):
 s=source(root,reference);pack=safe(root,'content/generated/synth-audio/firered.pack').read_bytes();cry=safe(root,'content/generated/synth-audio/normal-cries.bin').read_bytes()
 if sha(pack)!=PACK_SHA or len(pack)!=0x086fbea4-0x08489968 or sha(cry)!=CRY_SHA or len(cry)!=6188:raise ValueError('Original audio resource identity differs')
 spec=importlib.util.spec_from_file_location('silph_midi',ROOT/'scripts/audit-early-audio.py');audit=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit)
 constants={n:int(v) for n,v in re.findall(r'^#define ((?:MUS|SE)_\w+)\s+(\d+)',s['include/constants/songs.h'].decode(),re.M)}
 table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',s['sound/song_table.inc'].decode(),re.M);cues=[]
 for name in ['MUS_SILPH','SE_WARP_OUT']:
  sid=constants[name];address,player,second=struct.unpack_from('<IHH',pack,0x084A32CC+sid*8-0x08489968)
  if table[sid]!=(name.lower(),str(player),str(second)):raise ValueError('Silph song table differs')
  path='sound/songs/midi/'+name.lower()+'.mid';midi=audit.midi(s[path]);looping=any(m['text'] in ('[',']') for m in midi['markers'])
  if looping!=(name=='MUS_SILPH') or player not in ((0,) if looping else (1,2,3)):raise ValueError('Silph sound source player/loop differs')
  cues.append(dict(id=name,sourceCommit=COMMIT,sourceSymbol=name,sourcePath=path,sourcePlayer=player,priority=pack[address-0x08489968+2],sourceCounter=None,songAddress=address,species=0,looping=looping,cryMode=0))
 species={n:int(v) for n,v in re.findall(r'^#define SPECIES_(\w+)\s+(\d+)\s*$',s['include/constants/species.h'].decode(),re.M)}
 names=re.findall(r'^\s*cry Cry_(\w+)\s*$',s['sound/cry_tables.inc'].decode().split('gCryTable::',1)[1].split('gCryTable_Reverse::',1)[0],re.M)
 rows={struct.unpack_from('<H',cry,o)[0]:struct.unpack_from('<HHIIB3x',cry,o) for o in range(12,len(cry),16)}
 for sid,name in [(65,'ALAKAZAM'),(122,'MR_MIME'),(131,'LAPRAS')]:
  row=rows[sid]
  if species[name]!=sid or names[sid-1].upper().replace('_','')!=name.replace('_','') or row[1]!=sid-1:raise ValueError('Silph cry table differs')
  tone=0x0848c914+row[1]*12-0x08489968;wave=struct.unpack_from('<I',pack,tone+4)[0]-0x08489968
  if struct.unpack_from('<HHIII',pack,wave)!=(1,0,10764288,0,row[2]):raise ValueError('Source cry sample header differs')
  cues.append(dict(id='CRY_'+name+'_NORMAL',sourceCommit=COMMIT,sourceSymbol='SPECIES_'+name,sourcePath='sound/direct_sound_samples/cries/'+name.lower()+'.wav',sourcePlayer=4,priority=10,sourceCounter=None,songAddress=0,species=sid,looping=False,cryMode=0))
 data=(json.dumps(dict(schemaVersion=1,sourceCommit=COMMIT,cues=sorted(cues,key=lambda c:c['id'])),indent=2)+'\n').encode()
 identity=f'''// Generated ID-only cues; original sample pack and synthesis core remain unchanged.
internal static class SilphSynthManifestIdentity
{{
    public const string Sha256 = "{sha(data)}";
    public const int Bytes = {len(data)};
    public const int CueCount = 5;
    public const string SourceCommit = "{COMMIT}";
}}
'''.encode()
 return {'content/generated/silph-synth-audio/manifest.json':data,'src/Game.Godot/SilphSynthManifestIdentity.cs':identity}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();out=build(a.project_root,a.reference)
 for path,b in out.items():
  target=safe(a.project_root,path)
  if a.check:
   if target.read_bytes()!=b:raise ValueError('Generated Silph synth differs: '+path)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(passed=True,cues=5,normalCries=3,manifestSha256=sha(out['content/generated/silph-synth-audio/manifest.json']),originalPackUnchanged=True,renderedAudioWritten=False)))
