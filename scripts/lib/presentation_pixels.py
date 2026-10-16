"""Shared exact GBA palette expansion and source-frame decoding for presentation."""
import io
import struct
from PIL import Image


def palette(data, floor=False):
    lines = data.decode('ascii').splitlines()
    if len(lines) < 3 or lines[:2] != ['JASC-PAL', '0100'] or len(lines) != int(lines[2]) + 3:
        raise ValueError('Source palette framing differs')
    colors = []
    for line in lines[3:]:
        rgb = tuple(map(int, line.split()))
        if len(rgb) != 3 or any(c < 0 or c > 255 for c in rgb):
            raise ValueError('Source palette channel outside RGB8')
        colors.append(tuple((c >> 3) * 255 // 31 if floor else ((c >> 3) << 3) | ((c >> 3) >> 2) for c in rgb))
    return colors


def rgba(indices, colors):
    if not colors or any(i >= len(colors) for i in indices):
        raise ValueError('Palette index exceeds available colors')
    return bytes(c for i in indices for c in (*colors[i], 255 if i else 0))


class PixelReader:
    """Bind source reads once; keep frame slicing distinct from color expansion."""
    def __init__(self, source):
        self.source = source

    def palette(self, path, floor=False):
        return palette(self.source.read(path), floor)

    def indexed(self, image, pal, shape, floor=False, first_frame=False, crop=None, second_frame=False):
        with Image.open(io.BytesIO(self.source.read(image))) as original:
            if original.mode != 'P' or original.size != shape or max(original.tobytes()) >= 16:
                raise ValueError('Source indexed image differs: ' + image)
            indices = original.crop(crop).tobytes() if crop else original.tobytes()
        if second_frame:
            indices = indices[4096:8192]
        elif first_frame and crop is None:
            indices = indices[:1024]
        return indices, rgba(indices, self.palette(pal, floor))

    def pixels(self, *args, **kwargs):
        return self.indexed(*args, **kwargs)[1]


def tilemap(tiles, data, colors, width=256, height=256, bank_base=0, transparent=False):
    if tiles.mode != 'P' or len(data) != width * height // 32:
        raise ValueError('Invalid indexed tilemap geometry')
    out = Image.new('RGBA', (width, height))
    for n, (word,) in enumerate(struct.iter_unpack('<H', data)):
        index, bank = word & 1023, (word >> 12) - bank_base
        tx, ty = index % (tiles.width // 8) * 8, index // (tiles.width // 8) * 8
        if word == 0:
            continue
        if ty + 8 > tiles.height or bank < 0 or (bank+1)*16 > len(colors):
            raise ValueError(f'Invalid tile reference {word:x}')
        for y in range(8):
            for x in range(8):
                pixel = tiles.getpixel((tx + (7-x if word & 1024 else x), ty + (7-y if word & 2048 else y)))
                out.putpixel((n % (width//8)*8+x,n//(width//8)*8+y), (*colors[bank*16+pixel], 0 if transparent and pixel == 0 else 255))
    return out
