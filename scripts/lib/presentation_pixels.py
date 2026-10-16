"""Shared exact GBA palette expansion and source-frame decoding for presentation."""
import io
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
