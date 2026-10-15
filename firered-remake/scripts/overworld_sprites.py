"""Source metadata and rendering shared by regional overworld importers."""
import io
import re
from PIL import Image
from image_io import encode_png


def graphics_records(src, graphics, out, *, render=False):
    if not render:
        return [dict(graphicsId=identity) for identity in sorted(graphics)]
    root = 'src/data/object_events/'
    records = []
    if render:
        out.mkdir()
    for graphics_id in sorted(graphics):
        symbol = re.search(r'\[' + graphics_id + r'\]\s*=\s*&(\w+)', src.text(root + 'object_event_graphics_info_pointers.h'))[1]
        body = re.search(r'\b' + symbol + r' = \{(.*?)\n\};', src.text(root + 'object_event_graphics_info.h'), re.S)[1]
        fields = dict(re.findall(r'\.(\w+)\s*=\s*([^,\n]+)', body))
        rows = re.search(r'\b' + fields['images'] + r'\[\] = \{(.*?)\n\};', src.text(root + 'object_event_pic_tables.h'), re.S)[1]
        frames = []
        for pic, width, height, index in re.findall(r'overworld_frame\((\w+), (\d+), (\d+), (\d+)\)', rows):
            path = re.search(r'\b' + pic + r'\[\] = INCBIN_U16\("(.*?)"\)', src.text(root + 'object_event_graphics.h'))[1].replace('.4bpp', '.png')
            frames.append(dict(pic=pic, widthTiles=int(width), heightTiles=int(height), index=int(index), sourceRef=src.ref(path, pic)))
        palette = re.search(r'\{\s*(\w+)\s*,\s*' + fields['paletteTag'] + r'\s*\}', src.text('src/event_object_movement.c'))[1]
        palette_path = re.search(r'\b' + palette + r'\[\] = INCBIN_U16\("(.*?)"\)', src.text(root + 'object_event_graphics.h'))[1].replace('.gbapal', '.pal')
        width, height = int(fields['width']), int(fields['height'])
        colors = [tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in line.split())
                  for line in src.text(palette_path).splitlines()[3:]]
        if len(colors) != 16 or any(len(c) != 3 for c in colors) or not frames:
            raise ValueError('Invalid NPC palette/frame table')
        sheet = Image.new('RGBA', (width * len(frames), height))
        for i, frame in enumerate(frames):
            if frame['widthTiles'] * frame['heightTiles'] * 64 != width * height:
                raise ValueError('NPC frame byte span differs')
            image = Image.open(io.BytesIO(src.data[frame['sourceRef']['path']]))
            if image.mode != 'P' or image.height != height or (frame['index'] + 1) * width > image.width:
                raise ValueError('Unsupported NPC source sheet')
            crop = image.crop((frame['index'] * width, 0, (frame['index'] + 1) * width, height))
            rgba = Image.new('RGBA', crop.size)
            rgba.putdata([(*colors[c], 0 if c == 0 else 255) for c in crop.get_flattened_data()])
            sheet.paste(rgba, (i * width, 0))
        filename = graphics_id.removeprefix('OBJ_EVENT_GFX_').lower().replace('_', '-') + '.png'
        if render:
            (out / filename).write_bytes(encode_png(sheet))
        records.append(dict(graphicsId=graphics_id, declaration=fields, frames=frames,
                            texturePath=('npc/' if render else '../shared-overworld/') + filename,
                            frameWidth=width, frameHeight=height, frameCount=len(frames),
                            tileCenterAnchor=dict(x=width // 2, y=height - 8),
                            sourceRef=src.ref(root + 'object_event_graphics_info.h', symbol),
                            paletteSourceRef=src.ref(palette_path, palette), runtimeStatus='sourceOnly'))
    return records
