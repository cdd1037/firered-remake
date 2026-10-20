#!/usr/bin/env python3
"""Add native Fusion Pixel masks beside, never over, original bitmap glyphs."""
import argparse
import io
import json
from pathlib import Path
import re

from fontTools.ttLib import TTFont, TTLibError
from PIL import Image, ImageFont

ROOT = Path(__file__).resolve().parents[3]
FONT_RELEASE = '2026.10.16'
FONT_DIRECTORY = ROOT / 'content/raw/fonts/fusion-pixel' / FONT_RELEASE
NORMAL_FONT = FONT_DIRECTORY / 'fusion-pixel-12px-monospaced-zh_hans.otf'
SMALL_FONT = FONT_DIRECTORY / 'fusion-pixel-10px-monospaced-zh_hans.otf'
NATIVE_SIZES = {'normal': 12, 'small': 10}
CELL_HEIGHTS = {'normal': 14, 'small': 13}
# Explicitly deferred by the selected edition. These remain unsupported in the
# small face; runtime displays its missing-glyph marker and rejects name input.
ALLOWED_MISSING = {'normal': frozenset(), 'small': frozenset(map(ord, '靓黾'))}
OUTPUT_NAMES = ('cjk-font.json', 'cjk-normal.png', 'cjk-small.png')
# Ordinary Chinese names beyond the translated corpus. Keep original Latin glyphs;
# include only mapped Han and a deliberate bounded set of ordinary punctuation.
NAME_PUNCTUATION = '·・〇、。，．：；？！“”‘’「」『』（）《》〈〉【】〔〕［］｛｝—…'


def naming_characters(normal_font, small_font):
    supported = unicode_characters(normal_font, 12) & unicode_characters(small_font, 10)
    requested = (set(range(0x3400, 0x4DC0)) | set(range(0x4E00, 0xA000)) |
                 set(range(0xF900, 0xFB00)) | set(range(0x20000, 0x323B0)) |
                 set(map(ord, NAME_PUNCTUATION)))
    return requested & supported


def unicode_characters(path, native_size):
    """Read only the selected official native face; never substitute another font."""
    if native_size not in NATIVE_SIZES.values():
        raise ValueError('Fusion Pixel must use its native 12px normal / 10px small faces')
    expected = (f'Expected Fusion Pixel {native_size}px Mono zh-Hans Regular '
                f'{FONT_RELEASE} native OTF')
    try:
        loaded = TTFont(path)
    except TTLibError as error:
        raise ValueError(expected) from error
    with loaded as font:
        name = font['name']
        if (font.sfntVersion != 'OTTO' or font.flavor is not None or
                name.getBestFamilyName() != f'Fusion Pixel {native_size}px Mono zh-Hans' or
                name.getBestSubFamilyName() != 'Regular' or
                name.getDebugName(5) != FONT_RELEASE or
                font['head'].unitsPerEm != native_size * 100):
            raise ValueError(expected)
        cmap = font.getBestCmap()
        if not cmap:
            raise ValueError('Fusion Pixel has no Unicode cmap')
        characters = {cp for cp, glyph in cmap.items() if font.getGlyphID(glyph) != 0}
        if any(not valid_codepoint(cp) for cp in characters):
            raise ValueError('Fusion Pixel cmap contains invalid Unicode')
        return characters


def valid_codepoint(cp):
    return type(cp) is int and 0 <= cp <= 0x10FFFF and not 0xD800 <= cp <= 0xDFFF


def literal_characters(text):
    # Collect glyphs only. Runtime owns all expansion/binding; this is not a second parser.
    if any(0xD800 <= ord(c) <= 0xDFFF for c in text):
        raise ValueError('Text pack contains invalid Unicode')
    text = re.sub(r'\{[^{}]*\}', '', text)
    text = re.sub(r'\\[npl]', '\n', text)
    return {ord(c) for c in text if c not in '\r\n\f\t$'}


def pack_characters(directory):
    paths = list(Path(directory).glob('*.json')) + list((Path(directory) / 'source').glob('*.json'))
    if not paths:
        raise ValueError('Text pack contains no JSON input files')
    characters = set()
    def visit(value):
        if isinstance(value, str): characters.update(literal_characters(value))
        elif isinstance(value, dict):
            for child in value.values(): visit(child)
        elif isinstance(value, list):
            for child in value: visit(child)
    for path in sorted(paths): visit(json.loads(path.read_text(encoding='utf-8')))
    return characters


def rasterize(font, codepoint, height, baseline):
    char = chr(codepoint)
    mask, (left, top) = font.getmask2(char, mode='1', anchor='ls')
    coverage = Image.frombytes('L', mask.size, bytes(mask))
    ink = coverage.getbbox()
    native_advance = font.getlength(char)
    if native_advance != int(native_advance) or not 1 <= native_advance <= 32:
        raise ValueError(f'Invalid native glyph advance for U+{codepoint:04X}')
    advance = int(native_advance)
    if ink is None:
        if char not in ('\u3000', '\u00a0'):
            raise ValueError(f'Empty glyph U+{codepoint:04X}')
        return advance, advance, bytearray(advance * height)
    # One face-wide baseline; punctuation retains its actual vertical position.
    x0, y0, x1, y1 = left + ink[0], baseline + top + ink[1], left + ink[2], baseline + top + ink[3]
    # Width is storage/render geometry, not text advance. Native 10px 朴/竍 have
    # a one-pixel shadow overhang which must survive without widening all text.
    width = max(advance, x1 + 1)
    if x0 < 0 or y0 < 0 or width > 32 or y1 + 1 > height:
        raise ValueError(f'Glyph U+{codepoint:04X} exceeds native {width}x{height} cell with shadow '
                         f'at fixed baseline {baseline}: ink {(x0, y0, x1, y1)}. '
                         'Never crop, rescale or normalize individual glyphs.')
    pixels = bytearray(width * height)
    # Shadow first; foreground wins where masks overlap. The output has exactly two roles.
    for y in range(mask.size[1]):
        for x in range(mask.size[0]):
            if coverage.getpixel((x, y)):
                pixels[(baseline + top + y + 1) * width + left + x + 1] = 2
    for y in range(mask.size[1]):
        for x in range(mask.size[0]):
            if coverage.getpixel((x, y)):
                pixels[(baseline + top + y) * width + left + x] = 1
    return advance, width, pixels


def convert(normal_font, small_font, source_catalog, requested, normal_size=12, small_size=10,
            allowed_missing=None, missing_report=None):
    if source_catalog.get('schemaVersion') != 1:
        raise ValueError('Unsupported source font catalog')
    if (normal_size, small_size) != (12, 10):
        raise ValueError('Fusion Pixel must use its native 12px normal / 10px small faces')
    if any(not valid_codepoint(cp) or cp < 32 or 0x7F <= cp < 0xA0 for cp in requested):
        raise ValueError('Requested glyph is not a Unicode display character')
    if allowed_missing is None:
        allowed_missing = ALLOWED_MISSING
    if set(allowed_missing) - set(NATIVE_SIZES):
        raise ValueError('Unknown face in allowed missing glyphs')
    if any(not valid_codepoint(cp) for values in allowed_missing.values() for cp in values):
        raise ValueError('Allowed missing glyph is not valid Unicode')
    default_fg = source_catalog['foregroundRgba']
    default_shadow = source_catalog['shadowRgba']
    if (default_fg == default_shadow or
            any(len(c) != 4 or c[3] != 255 or
                any(type(v) is not int or not 0 <= v <= 255 for v in c)
                for c in (default_fg, default_shadow))):
        raise ValueError('Expected source foreground/shadow colors')
    sources = source_catalog['fonts']
    if len(sources) != 2 or {f['id'] for f in sources} != set(NATIVE_SIZES):
        raise ValueError('Expected original normal and small source faces')
    outputs, faces, missing_faces = {}, [], {}
    for face_id, font_path in [('normal', normal_font), ('small', small_font)]:
        size = NATIVE_SIZES[face_id]
        supported = unicode_characters(font_path, size)
        source = next(f for f in sources if f['id'] == face_id)
        height = source['pixelSize']
        if height != CELL_HEIGHTS[face_id] or source['baseline'] != height:
            raise ValueError('Original source face height/baseline differs')
        source_codepoints = [g['codepoint'] for g in source['glyphs']]
        if (not source_codepoints or len(set(source_codepoints)) != len(source_codepoints) or
                any(not valid_codepoint(cp) for cp in source_codepoints)):
            raise ValueError('Invalid original source glyph inventory')
        old = set(source_codepoints) | {0xE000, 0x2025}
        extra = sorted(requested - old)
        if not extra:
            raise ValueError('Text corpus has no supplementary glyphs')
        missing_faces[face_id] = sorted(set(extra) - supported)
        unexpected = sorted(set(missing_faces[face_id]) - set(allowed_missing.get(face_id, ())))
        if unexpected:
            raise ValueError(f'Fusion Pixel {face_id} face lacks ' +
                             ', '.join(f'U+{cp:04X}' for cp in unexpected))
        extra = sorted(set(extra) & supported)
        if not extra:
            raise ValueError(f'Text corpus has no supported supplementary glyphs in {face_id}')
        font = ImageFont.truetype(str(font_path), size, layout_engine=ImageFont.Layout.BASIC)
        baseline = 10 if face_id == 'normal' else 9
        if font.getname() != (f'Fusion Pixel {size}px Mono zh-Hans', 'Regular'):
            raise ValueError('Rasterized font face differs from selected Fusion Pixel face')
        glyphs, prepared = [], []
        # Shelf-pack at actual image widths, retaining separate native advances.
        width, cell_height = 1024, 16
        x = y = 0
        for cp in extra:
            advance, glyph_width, roles = rasterize(font, cp, height, baseline)
            if x + glyph_width > width:
                x = 0; y += cell_height
            prepared.append((x, y, glyph_width, roles))
            glyphs.append(dict(codepoint=cp, x=x, y=y, width=glyph_width, height=height, advance=advance))
            x += glyph_width
        atlas_height = y + cell_height
        atlas = Image.new('RGBA', (width, atlas_height), (0, 0, 0, 0))
        palette = [(0, 0, 0, 0), tuple(default_fg), tuple(default_shadow)]
        for x, y, glyph_width, roles in prepared:
            rgba = bytes(channel for role in roles for channel in palette[role])
            atlas.paste(Image.frombytes('RGBA', (glyph_width, height), rgba), (x, y))
        png = io.BytesIO(); atlas.save(png, format='PNG', compress_level=9)
        outputs[f'cjk-{face_id}.png'] = png.getvalue()
        faces.append(dict(id=face_id, pixelSize=height, baseline=height, textureWidth=width,
                          textureHeight=atlas_height, glyphs=glyphs))
    outputs['cjk-font.json'] = (json.dumps(dict(schemaVersion=1, foregroundRgba=default_fg,
        shadowRgba=default_shadow, fonts=faces), ensure_ascii=False, separators=(',', ':')) + '\n').encode()
    if missing_report is not None:
        missing_report.clear()
        missing_report.update(missing_faces)
    return outputs


def license_outputs(normal_font, small_font):
    """Redistribute the complete release notices with the generated derivative."""
    directory = Path(normal_font).parent
    if directory.resolve() != Path(small_font).parent.resolve():
        raise ValueError('Both Fusion Pixel faces must share their release license directory')
    notices = directory / 'LICENSES'
    paths = [directory / 'OFL.txt'] + sorted(notices.rglob('*'))
    if not notices.is_dir() or not any(path.is_file() for path in paths[1:]):
        raise ValueError('Fusion Pixel release LICENSES notices are missing')
    outputs = {}
    for path in paths:
        if any(part.is_symlink() for part in (path, *path.parents)):
            raise ValueError('Refusing font license input symlink')
        if path.is_dir():
            continue
        if not path.is_file() or not path.stat().st_size:
            raise ValueError('Fusion Pixel release license is missing/empty: ' + str(path))
        outputs[path.relative_to(directory).as_posix()] = path.read_bytes()
    return outputs


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--normal-font', type=Path, default=NORMAL_FONT)
    p.add_argument('--small-font', type=Path, default=SMALL_FONT)
    p.add_argument('--source-font', type=Path, default=ROOT / 'content/generated/ui/font/source-font.json')
    p.add_argument('--text-pack', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--normal-size', type=int, default=12)
    p.add_argument('--small-size', type=int, default=10)
    p.add_argument('--check', action='store_true')
    p.add_argument('--include-naming-characters', action='store_true',
                   help='Include Han, compatibility forms and name punctuation mapped by both native faces')
    args = p.parse_args()
    requested = pack_characters(args.text_pack)
    if args.include_naming_characters:
        requested |= naming_characters(args.normal_font, args.small_font)
    missing_report = {}
    outputs = convert(args.normal_font, args.small_font, json.loads(args.source_font.read_text()), requested,
                      args.normal_size, args.small_size, missing_report=missing_report)
    outputs.update(license_outputs(args.normal_font, args.small_font))
    if args.out.resolve() in {path.parent.resolve() for path in
                              (args.source_font, args.normal_font, args.small_font)}:
        raise ValueError('Supplement must not be written into an input font directory')
    if args.check:
        def matches(name, expected):
            path = args.out / name
            if not path.is_file(): return False
            if name.endswith('.json'):
                return json.loads(path.read_text()) == json.loads(expected)
            if name.endswith('.png'):
                with Image.open(path) as actual, Image.open(io.BytesIO(expected)) as wanted:
                    return actual.size == wanted.size and actual.convert('RGBA').tobytes() == wanted.convert('RGBA').tobytes()
            return path.read_bytes() == expected
        changed = [name for name, data in outputs.items() if not matches(name, data)]
        if changed: raise ValueError('CJK font outputs missing/different: ' + ', '.join(changed))
    else:
        targets = [args.out / name for name in outputs] + [args.out / (name + '.tmp') for name in outputs]
        for path in targets:
            if any(part.is_symlink() for part in (path, *path.parents)):
                raise ValueError('Refusing font output symlink')
        args.out.mkdir(parents=True, exist_ok=True)
        for name, data in outputs.items():
            path, temporary = args.out / name, args.out / (name + '.tmp')
            path.parent.mkdir(parents=True, exist_ok=True)
            temporary.write_bytes(data); temporary.replace(path)
    print(json.dumps(dict(status='checked' if args.check else 'imported', files=len(outputs),
                         normalSize=args.normal_size, smallSize=args.small_size,
                         missingCodepoints={face: [f'U+{cp:04X}' for cp in missing]
                                            for face, missing in missing_report.items()})))


if __name__ == '__main__':
    main()
