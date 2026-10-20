"""Offline image composition and bounded, guttered pages for presentation assets."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import io
import json
import struct
from PIL import Image


from image_io import encode_png as png


def pack(images, path, width=1024):
    """Shelf-pack one co-used cohort without rotating or resizing its pixels."""
    rows = []; x = y = 1; shelf = 0
    for identity, image in images:
        if image.width + 2 > width:
            raise ValueError('Image exceeds atlas width: ' + identity)
        if x + image.width + 1 > width:
            x = 1; y += shelf + 2; shelf = 0
        rows.append(dict(id=identity, width=image.width, height=image.height,
            texturePath=path, region=dict(x=x,y=y,width=image.width,height=image.height)))
        x += image.width + 2; shelf = max(shelf, image.height)
    height = y + shelf + 1
    if height > 1024:
        raise ValueError('Image cohort exceeds bounded atlas page')
    page = Image.new('RGBA', (width, height))
    for (_, image), row in zip(images, rows):
        page.paste(image, (row['region']['x'], row['region']['y']))
    return png(page), rows


def pack_pages(images, prefix, width=1024):
    """Pack a co-owned cohort into bounded pages, sharing only identical RGBA.

    Logical identities and their order remain intact. Page ordering is stable
    independently of input order, and different transparent RGB remains distinct.
    """
    images = list(images)
    if not images or not isinstance(width, int) or not 3 <= width <= 1024:
        raise ValueError('Empty atlas cohort or invalid bounded page width')
    if len({identity for identity, _ in images}) != len(images):
        raise ValueError('Duplicate atlas identity')
    unique = {}; aliases = {}
    for identity, source in sorted(images, key=lambda item: item[0]):
        if not identity or source.width + 2 > width or source.height + 2 > 1024:
            raise ValueError('Image exceeds bounded atlas page: ' + identity)
        image = source.convert('RGBA')
        key = (image.size, image.tobytes())
        canonical = unique.setdefault(key, (identity, image))[0]
        aliases[identity] = canonical
    ordered = sorted(unique.values(), key=lambda item: (-item[1].height, -item[1].width, item[0]))
    outputs = {}; pages = []; regions = {}; cohort = []; x = y = 1; shelf = 0

    def finish():
        path = f'{prefix}-{len(pages):03d}.png'
        encoded, rows = pack(cohort, path, width)
        outputs[path] = encoded
        pages.append(dict(texturePath=path, width=width, height=y + shelf + 1))
        regions.update((row['id'], row) for row in rows)

    for identity, image in ordered:
        next_x, next_y, next_shelf = x, y, shelf
        if next_x + image.width + 1 > width:
            next_x = 1; next_y += next_shelf + 2; next_shelf = 0
        if next_y + image.height + 1 > 1024:
            finish()
            cohort = []; next_x = next_y = 1; next_shelf = 0
        cohort.append((identity, image))
        x = next_x + image.width + 2; y = next_y; shelf = max(next_shelf, image.height)
    finish()
    rows = [dict(regions[aliases[identity]], id=identity) for identity, _ in images]
    return outputs, rows, pages


def bake_fame(outputs, manifest):
    base = 'content/generated/ui/fame-checker-presentation/'
    layout = json.loads(outputs['layout.json'])
    palettes = json.loads(outputs['palettes.json'])['palettes']
    originals = {row['id']: Image.open(io.BytesIO(outputs[row['texturePath'].removeprefix(base)])).convert('RGBA') for row in manifest['assets']}
    sprites = []
    for index in range(16):
        identity = 'portrait-' + str(index)
        original = originals[identity]
        sprites.append((identity, original))
        indices = outputs[identity + '.indices']
        colors = palettes['silhouette']
        silhouette = Image.frombytes('RGBA', original.size, bytes(channel for i in indices for channel in (*colors[i], 0 if i == 0 else 255)))
        sprites.append((identity+'-silhouette', silhouette))
    for icon in manifest['objectIcons']:
        x,y,w,h = (icon[k] for k in ('x','y','width','height'))
        image = originals[icon['assetId']].crop((x,y,x+w,y+h))
        sprites.append((icon['symbol'], image))
        data = bytearray(image.tobytes())
        for p in range(0,len(data),4):
            for c in range(3):
                value = (data[p+c] >> 3) * 7 >> 4
                data[p+c] = (value << 3) | (value >> 2)
        sprites.append((icon['symbol']+'-dimmed', Image.frombytes('RGBA',image.size,bytes(data))))
    sprites.extend(('cursor-'+str(i), originals['cursor'].crop((0,i*32,32,i*32+32))) for i in range(2))
    sprites.extend((name, originals[name]) for name in ('scroll-arrows','question-mark','pokeball','help-main','help-pick','help-facts'))
    # Fact symbols may reference the same source icon; each logical region remains named.
    sprites = list(dict(sprites).items())
    sprite_png, sprite_rows = pack(sprites,base+'sprites.png')
    tiles = outputs['background.indices']; colors = palettes['background']
    source_maps = [list(struct.unpack('<1024H',outputs['bg'+str(n)+'.tilemap'])) for n in (1,2,3)]
    def update(target,width,state):
        for tile,left,top,w,h,bank in layout['infoBox']['updates'][str(state)]:
            for y in range(top,top+h):
                for x in range(left,left+w):target[y*width+x]=tile | bank<<12
    def layer(entries,width,outwidth):
        data=bytearray(outwidth*160*4)
        for y in range(160):
            for x in range(outwidth):
                entry=entries[(y//8)*width+x//8]
                tx=7-x%8 if entry&1024 else x%8;ty=7-y%8 if entry&2048 else y%8
                index=tiles[(entry&1023)*64+ty*8+tx]
                if index:
                    p=(y*outwidth+x)*4;data[p:p+4]=bytes((*colors[(entry>>12)*16+index],255))
        return Image.frombytes('RGBA',(outwidth,160),bytes(data))
    backgrounds=[]
    bg3=layer(source_maps[2],32,240)
    for name,state in [('base',None),('base-restored',2),('base-pick',4)]:
        bg2=source_maps[1].copy()
        if state is not None:update(bg2,32,state)
        image=Image.new('RGBA',(240,160),(*colors[0],255));image.alpha_composite(bg3);image.alpha_composite(layer(bg2,32,240))
        backgrounds.append((name,image))
    background_png,background_rows=pack(backgrounds,base+'backgrounds.png',width=726)
    overlays=[]
    for state in range(6):
        bg1=[0]*2048
        for y in range(32):bg1[y*64+30:y*64+62]=source_maps[0][y*32:y*32+32]
        update(bg1,64,state)
        overlays.append(('overlay-'+str(state),layer(bg1,64,512)))
    overlay_png,overlay_rows=pack(overlays,base+'overlays.png',width=514)
    result={'sprites.png':sprite_png,'backgrounds.png':background_png,'overlays.png':overlay_png,
        'layout.json':json.dumps({'scrollArrows':layout['scrollArrows']},separators=(',',':')).encode(),
        'palettes.json':json.dumps({'palettes':{'text':palettes['text']}},separators=(',',':')).encode()}
    manifest=dict(manifest,assets=sprite_rows+background_rows+overlay_rows,
        outputs=[dict(path=name) for name in sorted(result)])
    return result,manifest
