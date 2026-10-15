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


def encode_png(image):
    """Use an exact RGBA palette when smaller; never quantize or discard hidden RGB."""
    rgba = image.convert('RGBA')
    output = io.BytesIO()
    rgba.save(output, format='PNG', compress_level=9)
    encoded = output.getvalue()
    colors = rgba.getcolors(256)
    if colors is None:
        return encoded
    palette = [color for _, color in colors]
    indices = {color: index for index, color in enumerate(palette)}
    indexed = Image.new('P', rgba.size)
    indexed.putdata([indices[color] for color in rgba.get_flattened_data()])
    indexed.putpalette([channel for color in palette for channel in color[:3]])
    indexed.info['transparency'] = bytes(color[3] for color in palette)
    output = io.BytesIO()
    indexed.save(output, format='PNG', compress_level=9)
    candidate = output.getvalue()
    return candidate if len(candidate) < len(encoded) else encoded


def png(width, height, pixels):
    """Encode exact RGBA pixels, including RGB values in transparent pixels."""
    return encode_png(Image.frombytes('RGBA', (width, height), bytes(pixels)))
