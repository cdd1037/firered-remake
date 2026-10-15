#!/usr/bin/env python3
"""Rebuild the bounded original General_StatsChange material without source tools or ROMs."""
import argparse,io,json,re,struct,subprocess
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def read_sources(reference):
    lock = json.loads(Path(__file__).with_name('source-stat-mask.lock.json').read_bytes())
    if lock['sourceCommit'] != COMMIT:
        raise ValueError('Wrong source revision')
    result = {}
    for row in lock['files']:
        path = row['path']
        result[path] = (subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + path])
                        if (reference / '.git').exists() else (reference / path).read_bytes())
    return result

def build(reference):
    raw=read_sources(reference);source=raw['data/battle_anim_scripts.s'].decode();start=re.search(r'^General_StatsChange:\n',source,re.M);commands=[]
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
    manifest=dict(schemaVersion=1,sourceCommit=COMMIT,script=dict(moveId=0,label='General_StatsChange',commands=commands),material=dict(path='stat-mask.bin',sheetWidth=128,sheetHeight=32,tilemapSize=32,paletteCount=8),dedicatedMoveScriptsAdded=0)
    return json.dumps(manifest,indent=2).encode()+b'\n',material
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=Path((ROOT/'.tools/reference-root').read_text().strip()));p.add_argument('--output',type=Path,default=ROOT/'content/generated/source-stat-mask');p.add_argument('--check',action='store_true');a=p.parse_args()
    manifest,material=build(a.reference)
    for name,data in [('manifest.json',manifest),('stat-mask.bin',material)]:
        if a.check:
            if (a.output/name).read_bytes()!=data:raise SystemExit('Generated output differs: '+name)
        else:a.output.mkdir(parents=True,exist_ok=True);(a.output/name).write_bytes(data)
    json.loads(manifest);print(json.dumps(dict(materialBytes=len(material),sourceFiles=31)))
