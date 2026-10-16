#!/usr/bin/env python3
"""Import shared stat-move roots and metallic OBJ-window material."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,io,json,re,struct,subprocess
from pathlib import Path
from PIL import Image
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def read_sources(reference):
    lock = json.loads((_PROJECT_ROOT / 'scripts/recipes/battle/source-stat-moves.lock.json').read_bytes())
    if lock['sourceCommit'] != COMMIT:
        raise ValueError('Wrong source revision')
    result = {}
    for row in lock['files']:
        path = row['path']
        result[path] = (subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + path])
                        if (reference / '.git').exists() else (reference / path).read_bytes())
    return result

def build(reference):
    raw=read_sources(reference);source=raw['data/battle_anim_scripts.s'].decode()
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
    manifest=dict(schemaVersion=1,scripts=scripts,subroutines=subs,material=dict(path='metal-shine.bin',sheetWidth=64,sheetHeight=64,tilemapSize=32))
    return (json.dumps(manifest,indent=2)+'\n').encode(),material
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=Path((ROOT/'.tools/reference-root').read_text().strip()));p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/stat-moves');p.add_argument('--check',action='store_true');a=p.parse_args()
    manifest,material=build(a.reference)
    for name,data in [('manifest.json',manifest),('metal-shine.bin',material)]:
        if a.check:
            if (a.output/name).read_bytes()!=data:raise SystemExit('Generated output differs: '+name)
        else:a.output.mkdir(parents=True,exist_ok=True);(a.output/name).write_bytes(data)
    print(json.dumps(dict(materialBytes=len(material),dedicatedMoveRoots=5)))
