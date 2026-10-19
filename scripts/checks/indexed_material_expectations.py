"""Test-only decoder for comparing packed output with independent source pixels."""
import struct


def decode_indexed(width, height, packed, palette_bytes):
    """Return visible RGB555 words in raster order; source index zero is transparent."""
    if width <= 0 or height <= 0 or width % 8 or height % 8:
        raise ValueError('Indexed expectation requires complete 8x8 tiles')
    if len(packed) != width * height // 2 or len(palette_bytes) != 32:
        raise ValueError('Indexed expectation shape differs')
    colors = struct.unpack('<16H', palette_bytes)
    if any(color > 32767 for color in colors):
        raise ValueError('Indexed expectation palette exceeds RGB555')
    raster = []
    for y in range(height):
        for x in range(width):
            offset = ((y // 8) * (width // 8) + x // 8) * 32 + (y % 8) * 4 + x % 8 // 2
            index = (packed[offset] >> ((x & 1) * 4)) & 15
            raster.append(0 if index == 0 else colors[index] | 32768)
    return tuple(raster)
