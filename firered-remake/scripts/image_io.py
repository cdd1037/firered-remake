"""Pillow image I/O for offline importers; GBA palette/tile rules stay with callers."""
import io
from PIL import Image


def indexed_png(data, *, max_pixels=32768):
    """Return source palette indices with the upstream 4-bit index reduction."""
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'PNG' or image.mode != 'P' or image.width % 8 or image.height % 8 or image.width * image.height > max_pixels:
            raise ValueError('Expected a tile-aligned indexed PNG')
        indices = bytes(index & 15 for index in image.tobytes())
        palette = image.getpalette('RGB')
        return image.width, image.height, indices, [tuple(palette[i:i+3]) for i in range(0,len(palette),3)]


def png(width, height, pixels):
    """Encode exact RGBA pixels, including RGB values in transparent pixels."""
    image = Image.frombytes('RGBA', (width,height), bytes(pixels))
    output = io.BytesIO()
    image.save(output, format='PNG', compress_level=9)
    return output.getvalue()
