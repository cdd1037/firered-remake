"""Bake source slot maps/palettes into bounded, co-used native image regions."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import io
import json
import struct
from PIL import Image
from presentation_atlas import pack

BASE = 'content/generated/ui/slot-machine-presentation/'

def bake(outputs, manifest):
    document = json.loads(outputs['layout.json'])
    layout = document['layout']
    palettes = json.loads(outputs['palettes.json'])['palettes']
    maps = {name: struct.unpack('<640H', outputs[name+'.tilemap']) for name in ('bg','combos_window')}
    tiles = {name: outputs[name+'.indices'] for name in ('bg','combos_window','button_pressed')}
    def tile(entry, name='bg', light=None):
        number, bank = entry & 1023, entry >> 12
        source = tiles['button_pressed'] if number >= 192 else tiles[name]
        if number >= 192: number -= 192
        colors = (palettes['combos_window'][(bank-7)*16:(bank-6)*16] if bank >= 7 else
                  palettes['match_lines'] if bank in (5,6) else
                  palettes['payout_lights'][light*16:(light+1)*16] if bank == 1 and light is not None else
                  palettes['bg'][bank*16:(bank+1)*16])
        data = bytearray()
        for y in range(8):
            for x in range(8):
                index = source[number*64 + (7-y if entry & 2048 else y)*8 + (7-x if entry & 1024 else x)]
                data.extend((*colors[index], 0 if index == 0 else 255))
        return Image.frombytes('RGBA',(8,8),bytes(data))
    def layer(name,width):
        result=Image.new('RGBA',(width,160))
        for y in range(20):
            for x in range(width//8):result.paste(tile(maps[name][y*32+x],name),(x*8,y*8))
        return result
    images=[]
    for name in ('reel-icons','clefairy','digits','controls'):
        images.append((name,Image.open(io.BytesIO(outputs[name+'.png'])).convert('RGBA')))
    images.extend([('background',layer('bg',240)),('help',layer('combos_window',256))])
    patches={}
    def patch(name,cells,entries,light=None):
        result=Image.new('RGBA',(len(cells)*8,8))
        rects=[]
        for i,(cell,entry) in enumerate(zip(cells,entries)):
            result.paste(tile(entry,light=light),(i*8,0))
            rects.append([i*8, 0, cell%32*8, cell//32*8, 8, 8])
        images.append((name,result));patches[name]=rects
    for line in layout['matchLines']:
        for state in (4,5):
            patch('line-%d-%d'%(line['id'],state),line['tileIndices'],[(maps['bg'][cell]&4095)|(state<<12) for cell in line['tileIndices']])
    for reel,cells in enumerate(layout['buttonTileIndices']):patch('button-'+str(reel),cells,range(192,196))
    cells=[i for i,entry in enumerate(maps['bg']) if entry>>12==1 and i%32<30]
    for light in range(3):patch('light-'+str(light),cells,[maps['bg'][i] for i in cells],light)
    page,rows=pack(images,BASE+'presentation.png')
    layout['preparedPatches']=patches
    # Source-format instructions never reach the runtime compositor.
    for line in layout['matchLines']:line.pop('tileIndices')
    for key in ('buttonTileIndices','normalLinePalette','betLinePalette','winningLinePalette','pressedButtonTileBase'):
        layout.pop(key,None)
    result={'presentation.png':page,'layout.json':(json.dumps(document,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n').encode()}
    manifest=dict(manifest,assets=rows,outputs=[dict(path=p) for p in sorted(result)])
    return result,manifest
