"""Render the original Sea Cottage teleporter animation assets."""
import io
import re
from PIL import Image


def compile_teleporter(src, out):
    """Original teleporter task declarations and RGB atlas, never a recolored approximation."""
    out.mkdir(parents=True, exist_ok=True)
    labels_path = 'include/constants/metatile_labels.h'
    labels = dict((name, int(token, 16)) for name, token in re.findall(r'^#define\s+(METATILE_SeaCottage_\w+)\s+(0x[0-9A-Fa-f]+)', src.text(labels_path), re.M))
    if len(labels) != 10:
        raise ValueError('Sea Cottage animation metatile inventory differs')
    bases = ['data/tilesets/primary/building', 'data/tilesets/secondary/sea_cottage']
    sheets = [Image.open(io.BytesIO(src.data[b + '/tiles.png'])) for b in bases]
    metas = [src.units(b + '/metatiles.bin', 'H') for b in bases]
    attrs = [src.units(b + '/metatile_attributes.bin', 'I') for b in bases]
    palettes = [[tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in line.split())
                 for line in src.text(bases[int(i >= 7)] + f'/palettes/{i:02}.pal').splitlines()[3:]] for i in range(13)]
    palettes[0][0] = (0, 0, 0)
    def tile(entry):
        tid, palette = entry & 1023, entry >> 12
        which = int(tid >= 640); local = tid - which * 640; sheet = sheets[which]
        if palette >= 13 or local * 64 >= sheet.width * sheet.height: raise ValueError('Animation tile out of range')
        image = Image.new('RGBA', (8, 8)); pixels = []
        for y in range(8):
            for x in range(8):
                xx, yy = (7-x if entry & 1024 else x), (7-y if entry & 2048 else y)
                index = sheet.getpixel((local % (sheet.width // 8) * 8 + xx, local // (sheet.width // 8) * 8 + yy))
                pixels.append((*palettes[palette][index], 255 if index else 0))
        image.putdata(pixels); return image
    layers = [Image.new('RGBA', (len(labels) * 16, 16)) for _ in range(3)]
    records = []
    for index, (symbol, mid) in enumerate(sorted(labels.items(), key=lambda row: row[1])):
        which = int(mid >= 640); local = mid - which * 640
        attribute = attrs[which][local]; entries = metas[which][local*8:local*8+8]; kind = attribute >> 29 & 3
        groups = {0: [[0x3014]*4, entries[:4], entries[4:]], 1: [entries[:4], entries[4:], [0]*4], 2: [entries[:4], [0]*4, entries[4:]]}.get(kind)
        if groups is None: raise ValueError('Reserved animation layer')
        for layer, group in zip(layers, groups):
            for q, value in enumerate(group): layer.paste(tile(value), (index*16 + q%2*8, q//2*8))
        records.append(dict(symbol=symbol, atlasX=index*16, atlasY=0, width=16, height=16))
    composed = Image.new('RGBA', layers[0].size, (0,0,0,255))
    for layer in layers: composed = Image.alpha_composite(composed, layer)
    artifacts=[]
    for name, image in zip(['bottom','middle','top','composite'],layers+[composed]):
        filename='teleporter-'+name+'.png';image.save(out/filename,optimize=False,compress_level=9)
        artifacts.append(dict(path='animation/'+filename, width=image.width,height=image.height))
    door_path = 'graphics/door_anims/teleporter.png'
    door = Image.open(io.BytesIO(src.data[door_path]))
    if door.mode != 'P' or door.size != (16,96): raise ValueError('Original teleporter door sheet differs')
    rgba=Image.new('RGBA',door.size);rgba.putdata([(*palettes[8][p],255 if p else 0) for p in door.get_flattened_data()])
    rgba.save(out/'teleporter-door.png',optimize=False,compress_level=9)
    artifacts.append(dict(path='animation/teleporter-door.png',width=16,height=96))
    return dict(schemaVersion=1, metatiles=records, artifacts=artifacts,
                door=dict(texturePath='animation/teleporter-door.png',
                          frameWidth=16, frameHeight=32, frameCount=3))
