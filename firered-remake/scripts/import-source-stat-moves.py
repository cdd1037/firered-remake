#!/usr/bin/env python3
"""Authenticate and import shared stat-move roots and metallic OBJ-window material."""
import argparse,hashlib,importlib.util,io,json,re,struct,subprocess
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
LOCK_SHA='bd560934a4ff721e8b904b6b7c885db388a446d458fa6c960bfff02357494f43'
def sha(b):return hashlib.sha256(b).hexdigest()
def authenticated(reference):
    spec=importlib.util.spec_from_file_location('hit_importer',ROOT/'scripts/import-source-hit-animations.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
    data=(ROOT/'scripts/source-stat-moves.lock.json').read_bytes()
    if len(data)>65536 or sha(data)!=LOCK_SHA:raise ValueError('Stat moves lock differs')
    lock=json.loads(data);result={}
    if lock['sourceCommit']!=COMMIT:raise ValueError('Wrong revision')
    for row in lock['files']:
        path=row['path'];raw=mod.read_bounded(reference/path,2_000_000,row['bytes'])
        if sha(raw)!=row['sha256']:raise ValueError('Source differs: '+path)
        if (reference/'.git').exists():
            blob=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+path])
            normalized=raw.replace(b'\r\n',b'\n') if row.get('gitTextLineEndings')=='lf' else raw
            if blob!=normalized or sha(blob)!=row.get('gitBlobSha256',row['sha256']):raise ValueError('Pinned source differs: '+path)
        result[path]=raw
    return result

def build(reference):
    raw=authenticated(reference);source=raw['data/battle_anim_scripts.s'].decode()
    def commands(label,end):
        start=re.search(r'^'+label+r':\n',source,re.M)
        if not start:raise ValueError('Missing original root '+label)
        result=[]
        for offset,line in enumerate(source[start.end():].splitlines()):
            text=line.split('@',1)[0].strip()
            if not text:continue
            if text.endswith(':'):raise ValueError('Unexpected label')
            op,*tail=text.split(None,1);args=[s.strip() for s in tail[0].split(',')] if tail else []
            if op not in {'call','return','waitforvisualfinish','end','createvisualtask','playsewithpan','loopsewithpan','createsprite','setalpha','blendoff'}:raise ValueError('Unadmitted command')
            result.append(dict(op=op,args=args,line=source[:start.end()].count('\n')+offset+1))
            if op==end:return result
        raise ValueError('Unterminated root')
    scripts=[dict(moveId=n,label='Move_'+label,commands=commands('Move_'+label,'end')) for n,label in [(74,'GROWTH'),(106,'HARDEN'),(334,'IRON_DEFENSE'),(110,'WITHDRAW'),(107,'MINIMIZE')]]
    subs={'GrowthEffect':commands('GrowthEffect','return')}
    with Image.open(io.BytesIO(raw['graphics/battle_anims/masks/metal_shine.png'])) as im:
        if im.mode!='P' or im.size!=(64,64):raise ValueError('Metal sheet shape differs '+str(im.size))
        indices=im.tobytes();palette=im.getpalette()
    if max(indices)>15:raise ValueError('4bpp overflow')
    tilemap=raw['graphics/battle_anims/masks/metal_shine.bin']
    if len(tilemap)!=2048 or any(v&1023>=64 for v in struct.unpack('<1024H',tilemap)):raise ValueError('Metal map shape differs')
    pal=struct.pack('<16H',*[(palette[i*3]>>3)|((palette[i*3+1]>>3)<<5)|((palette[i*3+2]>>3)<<10) for i in range(16)])
    material=indices+tilemap+pal
    manifest=dict(schemaVersion=1,sourceCommit=COMMIT,sourceLockSha256=LOCK_SHA,scripts=scripts,subroutines=subs,material=dict(path='metal-shine.bin',bytes=len(material),sha256=sha(material),sheetWidth=64,sheetHeight=64,tilemapSize=32))
    return (json.dumps(manifest,indent=2)+'\n').encode(),material
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=Path((ROOT/'.tools/reference-root').read_text().strip()));p.add_argument('--output',type=Path,default=ROOT/'content/generated/source-stat-moves');p.add_argument('--check',action='store_true');a=p.parse_args()
    manifest,material=build(a.reference)
    for name,data in [('manifest.json',manifest),('metal-shine.bin',material)]:
        if a.check:
            if (a.output/name).read_bytes()!=data:raise SystemExit('Generated output differs: '+name)
        else:a.output.mkdir(parents=True,exist_ok=True);(a.output/name).write_bytes(data)
    print(json.dumps(dict(manifestSha256=sha(manifest),materialSha256=sha(material),materialBytes=len(material),dedicatedMoveRoots=5)))
