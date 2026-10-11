#!/usr/bin/env python3
"""Rebuild the bounded original General_StatsChange material without source tools or ROMs."""
import argparse,hashlib,importlib.util,io,json,re,struct
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
LOCK_SHA='7cbdcf2f9f5bcb485620871c5bcc2a8b133ef31229efd21b445fbe6e59baaaed'
def sha(b):return hashlib.sha256(b).hexdigest()
def authenticated(reference):
    spec=importlib.util.spec_from_file_location('hit_importer',ROOT/'scripts/import-source-hit-animations.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
    # Same authenticated bounded input routine, with this independent additive lock.
    data=(ROOT/'scripts/source-stat-mask.lock.json').read_bytes()
    if len(data)>65536 or sha(data)!=LOCK_SHA:raise ValueError('Stat mask lock differs')
    lock=json.loads(data);result={}
    import subprocess
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
    raw=authenticated(reference);source=raw['data/battle_anim_scripts.s'].decode();start=re.search(r'^General_StatsChange:\n',source,re.M);commands=[]
    for offset,line in enumerate(source[start.end():].splitlines()):
        op,*tail=line.strip().split(None,1)
        args=[s.strip() for s in tail[0].split(',')] if tail else []
        commands.append(dict(op=op,args=args,line=source[:start.end()].count('\n')+offset+1))
        if op=='end':break
    if [(x['op'],x['args']) for x in commands]!=[('createvisualtask',['AnimTask_StatsChange','5']),('waitforvisualfinish',[]),('end',[])]:raise ValueError('General root differs')
    with Image.open(io.BytesIO(raw['graphics/battle_anims/masks/stat.png'])) as image:
        if image.size!=(128,32) or image.mode!='P':raise ValueError('Source mask shape differs')
        indices=image.tobytes()
    if max(indices)>15:raise ValueError('Non-4bpp mask')
    maps=b''.join(raw[f'graphics/battle_anims/masks/stat_tilemap_{i}.bin'] for i in (1,2))
    if len(maps)!=4096 or any((v&1023)>=64 or v>>12!=8 for v in struct.unpack('<2048H',maps)):raise ValueError('Mask tilemap differs')
    palettes=[]
    for i in range(1,9):
        lines=raw[f'graphics/battle_anims/masks/stat{i}.pal'].decode().splitlines()
        if lines[:3]!=['JASC-PAL','0100','16'] or len(lines)!=19:raise ValueError('Palette shape differs')
        for line in lines[3:]:
            r,g,b=map(int,line.split());palettes.append((r>>3)|((g>>3)<<5)|((b>>3)<<10))
    material=indices+maps+struct.pack('<128H',*palettes)
    manifest=dict(schemaVersion=1,sourceCommit=COMMIT,sourceLockSha256=LOCK_SHA,script=dict(moveId=0,label='General_StatsChange',commands=commands),material=dict(path='stat-mask.bin',bytes=len(material),sha256=sha(material),sheetWidth=128,sheetHeight=32,tilemapSize=32,paletteCount=8),dedicatedMoveScriptsAdded=0)
    return json.dumps(manifest,indent=2).encode()+b'\n',material
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=Path((ROOT/'.tools/reference-root').read_text().strip()));p.add_argument('--output',type=Path,default=ROOT/'content/generated/source-stat-mask');p.add_argument('--check',action='store_true');a=p.parse_args()
    manifest,material=build(a.reference)
    for name,data in [('manifest.json',manifest),('stat-mask.bin',material)]:
        if a.check:
            if (a.output/name).read_bytes()!=data:raise SystemExit('Generated output differs: '+name)
        else:a.output.mkdir(parents=True,exist_ok=True);(a.output/name).write_bytes(data)
    json.loads(manifest);print(json.dumps(dict(manifestSha256=sha(manifest),materialSha256=sha(material),materialBytes=len(material),sourceFiles=31)))
