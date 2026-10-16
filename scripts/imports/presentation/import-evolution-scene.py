#!/usr/bin/env python3
"""Convert pinned local evolution/hatch tilemaps; never executes upstream code."""
import argparse
import io
import json
from pathlib import Path
import re
import sys
import tempfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
sys.path.insert(0,str(ROOT/'scripts/lib'))
from presentation_pixels import palette, tilemap

def convert(raw, out):
    source = (raw/'src/evolution_scene.c').read_text()
    table = source.split('static const u8 sBgAnim_PalIndexes[][16] = {',1)[1].split('};',1)[0]
    indexes = [list(map(int,re.findall(r'\d+', row))) for row in re.findall(r'\{([^{}]+)\}',table)]
    control = source.split('static const u8 sBgAnim_PaletteControl[][4] =',1)[1].split('};',1)[0]
    controls = [list(map(int,re.findall(r'\d+',row))) for row in re.findall(r'\{([^{}]+)\}',control)]
    if not indexes or any(len(row)!=16 for row in indexes) or any(len(row)!=4 for row in controls):
        raise ValueError('Unsupported evolution palette table')
    outputs={}
    def save(image, path):
        buffer=io.BytesIO();image.save(buffer,format='PNG');outputs[path]=buffer.getvalue()
    colors = palette((raw/'graphics/evolution_scene/transition.pal').read_bytes(),floor=True)
    tiles = Image.open(raw/'graphics/evolution_scene/bg.png')
    assets=[]
    columns=10
    for layer,name in enumerate(['bg.bin','bg2.bin']):
        atlas=Image.new('RGBA',(columns*256,((len(indexes)+columns-1)//columns)*256))
        for i,row in enumerate(indexes):
            image=tilemap(tiles,(raw/'graphics/evolution_scene'/name).read_bytes(),[colors[j] for j in row],bank_base=10,transparent=True)
            atlas.paste(image,((i%columns)*256,(i//columns)*256))
        path=f'background-{layer}.png';save(atlas,path)
        assets.append(dict(id=f'background-{layer}',width=atlas.width,height=atlas.height,texturePath=path))
    sparkle=Image.open(raw/'graphics/evolution_scene/sparkle.png'); pal=sparkle.getpalette()
    spark=Image.new('RGBA',sparkle.size)
    spark.putdata([(*( (pal[i*3+j]>>3)*255//31 for j in range(3)), 255 if i else 0) for i in (sparkle.getpixel((x,y)) for y in range(sparkle.height) for x in range(sparkle.width))])
    save(spark,'sparkle.png'); assets.append(dict(id='sparkle',width=8,height=8,texturePath='sparkle.png'))
    hatch=tilemap(Image.open(raw/'graphics/trade/gba.png'),(raw/'graphics/trade/shadow_map.bin').read_bytes(),palette((raw/'graphics/trade/gba_pal2.pal').read_bytes(),floor=True),width=256,height=512,bank_base=1)
    save(hatch.crop((0,0,240,160)),'hatch.png'); assets.append(dict(id='hatch',width=240,height=160,texturePath='hatch.png'))
    outputs['scene.json']=(json.dumps(dict(schemaVersion=1,paletteControls=controls,paletteCount=len(indexes),atlasColumns=columns,assets=assets),indent=2)+'\n').encode()
    out.mkdir(parents=True,exist_ok=True)
    for path,data in outputs.items():
        (out/path).write_bytes(data)
    return dict(paletteCount=len(indexes),textures=len(assets))

if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--raw',type=Path,default=ROOT/'content/raw/pokefirered'/COMMIT[:8])
    p.add_argument('--out',type=Path,default=ROOT/'content/generated/ui/evolution-scene')
    p.add_argument('--check',action='store_true',help='Regenerate temporarily and compare current outputs without writing them')
    a=p.parse_args()
    if a.check:
        with tempfile.TemporaryDirectory(prefix='evolution-scene-check-') as directory:
            generated=Path(directory);result=convert(a.raw,generated)
            expected={path.name for path in generated.iterdir()}
            if not a.out.is_dir() or {path.name for path in a.out.iterdir()} != expected:
                raise ValueError('Evolution scene output inventory differs')
            for name in expected:
                if (generated/name).read_bytes() != (a.out/name).read_bytes():
                    raise ValueError('Evolution scene output differs: '+name)
            print(json.dumps(dict(**result,check='passed')))
    else:
        print(json.dumps(convert(a.raw,a.out)))
