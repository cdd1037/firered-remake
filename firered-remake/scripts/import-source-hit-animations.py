#!/usr/bin/env python3
"""Authenticated, bounded Pound/Tackle and current-battler geometry import. No ROM input."""
import argparse,hashlib,io,json,re,struct,subprocess,tempfile,os
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
LOCK_SHA='9845b38fd872de73ddb8ae67aab9e19da62b635ee41048dd061acc431d78a2ec'
OPS={'loadspritegfx','monbg','setalpha','playsewithpan','createsprite','createvisualtask','delay','waitforvisualfinish','clearmonbg','blendoff','end'}
def sha(data):return hashlib.sha256(data).hexdigest()
def read_bounded(path,maximum,exact=None):
 with path.open('rb') as f:
  n=os.fstat(f.fileno()).st_size
  if n>maximum or n<0 or exact is not None and n!=exact:raise ValueError('Input length differs: '+str(path))
  data=f.read(n+1)
 if len(data)!=n:raise ValueError('Input changed while reading: '+str(path))
 return data

def authenticated(reference):
 lock_data=read_bounded(Path(__file__).with_name('source-hit-animation.lock.json'),65536)
 if sha(lock_data)!=LOCK_SHA:raise ValueError('Source lock differs')
 lock=json.loads(lock_data)
 if lock['sourceCommit']!=COMMIT:raise ValueError('Wrong source revision')
 raw={}
 for row in lock['files']:
  name=row['path'];size=row['bytes'];data=read_bounded(reference/name,2_000_000,size)
  if sha(data)!=row['sha256']:raise ValueError('Source hash differs: '+name)
  if (reference/'.git').exists():
   blob=COMMIT+':'+name
   n=int(subprocess.check_output(['git','-C',str(reference),'cat-file','-s',blob]))
   if n!=size:raise ValueError('Source blob length differs: '+name)
   expected=subprocess.check_output(['git','-C',str(reference),'show',blob])
   if expected!=data:raise ValueError('Source differs from pinned Git blob: '+name)
  raw[name]=data
 return raw

def rgb555(data,size,palette=None):
 with Image.open(io.BytesIO(data)) as im:
  if im.size!=size or im.mode!='P':raise ValueError('Original indexed sprite shape differs')
  im.load();pal=palette if palette is not None else im.getpalette()
  indices=list(im.tobytes());values=[]
  for index in indices:
   if index>15:raise ValueError('Original 4bpp index overflow')
   red,green,blue=pal[index*3:index*3+3]
   values.append((red>>3)|((green>>3)<<5)|((blue>>3)<<10)|(0x8000 if index else 0))
  return struct.pack('<HH',*size)+struct.pack('<'+'H'*len(values),*values)

def build(reference):
 raw=authenticated(reference);script=raw['data/battle_anim_scripts.s'].decode();scripts=[]
 macros=set(re.findall(r'^\s*\.macro (\w+)',raw['asm/macros/battle_anim_script.inc'].decode(),re.M))
 for move,name in [(1,'POUND'),(33,'TACKLE')]:
  start=re.search(r'^Move_'+name+r':\n',script,re.M)
  if start is None:raise ValueError('Missing original move script')
  rows=[]
  for offset,line in enumerate(script[start.end():].splitlines()):
   text=line.split('@',1)[0].strip()
   if not text:continue
   if text.endswith(':'):raise ValueError('Unexpected branch before end')
   op,*tail=text.split(None,1);args=[x.strip() for x in tail[0].split(',')] if tail else []
   if op not in OPS or op not in macros:raise ValueError('Unsupported slice command: '+op)
   rows.append({'op':op,'args':args,'line':script[:start.end()].count('\n')+offset+1})
   if op=='end':break
  if not rows or rows[-1]['op']!='end':raise ValueError('Unterminated original script')
  scripts.append({'moveId':move,'label':'Move_'+name,'commands':rows})
 impact=rgb555(raw['graphics/battle_anims/sprites/impact.png'],(32,32))
 with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/substitute.png'])) as image:pal=image.getpalette()
 front=rgb555(raw['graphics/battle_anims/sprites/substitute.png'],(64,64),pal)
 back=rgb555(raw['graphics/battle_anims/sprites/substitute_back.png'],(64,64),pal)
 species={s:int(n) for s,n in re.findall(r'^#define\s+(SPECIES_\w+)\s+(\d+)\s*$',raw['include/constants/species.h'].decode(),re.M)}
 species.update({s:species['SPECIES_EGG']+int(n) for s,n in re.findall(r'^#define\s+(SPECIES_UNOWN_\w+)\s+\(NUM_SPECIES \+ (\d+)\)',raw['include/constants/species.h'].decode(),re.M)})
 elevation={s:int(n) for s,n in re.findall(r'\[(SPECIES_\w+)\]\s*=\s*(\d+)',raw['src/data/pokemon_graphics/enemy_mon_elevation.h'].decode())}
 geometry=[]
 for side in ['back','front']:
  text=raw['src/data/pokemon_graphics/'+side+'_pic_coordinates.h'].decode()
  rows=re.findall(r'\[(SPECIES_\w+)\]\s*=\s*\{\s*\.size\s*=\s*MON_COORDS_SIZE\((\d+),\s*(\d+)\),\s*\.y_offset\s*=\s*(\d+)',text)
  for name,w,h,y in rows:
   if name not in species:raise ValueError('Unknown coordinate species: '+name)
   geometry.append({'species':species[name],'side':side,'sourceWidth':int(w),'sourceHeight':int(h),'yOffset':int(y),'elevation':elevation.get(name,0) if side=='front' else 0})
 if len(geometry)!=880:raise ValueError('Expected exact pinned 880 geometry records')
 if len({(x['species'],x['side']) for x in geometry})!=880:raise ValueError('Duplicate geometry')
 def asset(id,data):return {'id':id,'path':id+'.rgb555','bytes':len(data),'sha256':sha(data)}
 anim={'schemaVersion':1,'sourceCommit':COMMIT,'sourceLockSha256':LOCK_SHA,'scripts':scripts,'assets':[asset('impact',impact)]}
 art={'schemaVersion':1,'sourceCommit':COMMIT,'sourceLockSha256':LOCK_SHA,'geometry':geometry,'assets':[asset('substitute-front',front),asset('substitute-back',back)]}
 def encode(x):return (json.dumps(x,indent=2)+'\n').encode()
 return {'source-hit-animations':{'manifest.json':encode(anim),'impact.rgb555':impact},'campaign-hit-art':{'manifest.json':encode(art),'substitute-front.rgb555':front,'substitute-back.rgb555':back}}

def main():
 p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('new_output_root',type=Path);a=p.parse_args()
 out=a.new_output_root
 if any((out/name).exists() for name in ['source-hit-animations','campaign-hit-art']):raise SystemExit('Refuse existing generated output')
 data=build(a.reference)
 out.mkdir(parents=True,exist_ok=True)
 # Authenticate and construct all bounded bytes before creating either published directory.
 with tempfile.TemporaryDirectory(prefix='.hit-import-',dir=out) as temp:
  temp=Path(temp)
  for name,files in data.items():
   (temp/name).mkdir()
   for filename,blob in files.items():(temp/name/filename).write_bytes(blob)
  for name in data:(temp/name).rename(out/name)
 print(json.dumps({'passed':True,'sourceCommit':COMMIT,'resources':[{'path':folder+'/'+name,'bytes':len(data),'sha256':sha(data)} for folder,files in data.items() for name,data in files.items()]}))
if __name__=='__main__':main()
