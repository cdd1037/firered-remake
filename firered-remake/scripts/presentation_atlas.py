"""Offline image composition and bounded, guttered pages for presentation assets."""
import io
import json
import struct
from PIL import Image


def png(image):
    output = io.BytesIO()
    image.save(output, format='PNG', compress_level=9)
    return output.getvalue()


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


def bake_fame(outputs, manifest):
    base = 'content/generated/fame-checker-presentation/'
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
