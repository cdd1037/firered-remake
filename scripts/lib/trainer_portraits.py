"""Pinned trainer portrait selection, source bindings and exact GBA color conversion.

Raw input is a trusted local cache. Optional Git input reads only the revision in
source.json; neither mode runs upstream programs or rewrites source files.
"""
import io
import json
from pathlib import Path, PurePosixPath
import re
import subprocess

from PIL import Image

from generated_output_compare import generated_output_matches
from image_io import png
from project_paths import PROJECT_ROOT
from runtime_json import runtime_json_bytes
from presentation_pixels import PixelReader
from presentation_source import canonical, need

GRAPHICS = 'src/data/graphics/trainers.h'
CONSTANTS = 'include/constants/trainers.h'
FRONT_TABLE = 'src/data/trainer_graphics/front_pic_tables.h'
BACK_TABLE = 'src/data/trainer_graphics/back_pic_tables.h'
PALETTE_EXPANSION = '(rgb5 << 3) | (rgb5 >> 2)'


def safe(root, relative):
    """Accept only ordinary contained relative paths, never symlinks."""
    if (not isinstance(relative, str) or not relative or '\\' in relative
            or PurePosixPath(relative).is_absolute()
            or any(part in ('', '.', '..') for part in relative.split('/'))):
        raise ValueError('Unsafe path: ' + str(relative))
    target = Path(root) / relative
    if any(part.is_symlink() for part in (target, *target.parents)):
        raise ValueError('Symlink rejected: ' + str(target))
    return target


def recipe(root=PROJECT_ROOT):
    document = json.loads(safe(root, 'scripts/recipes/trainers/portraits.json').read_bytes())
    if not document.get('portraits') or not document.get('groups'):
        raise ValueError('Empty trainer portrait recipe')
    return document


class Sources:
    def __init__(self, root=PROJECT_ROOT, raw=None, reference=None, local_palettes=None):
        self.root = Path(root)
        config = json.loads(safe(root, 'scripts/recipes/config/source.json').read_bytes())
        if (config['source']['repository'], config['referenceGame']) != (
                'pret/pokefirered', {'gameEdition': 'FIRERED', 'romRevision': 0}):
            raise ValueError('Trainer portrait source profile differs')
        self.commit = config['source']['sourceCommit']
        if not re.fullmatch('[0-9a-f]{40}', self.commit):
            raise ValueError('Expected a pinned source revision')
        self.raw = Path(raw) if raw else self.root / 'content/raw/pokefirered' / self.commit[:8]
        self.reference = Path(reference) if reference else None
        # Local variants are deliberately never replaced by upstream palettes.
        default_local = (self.raw.parent.parent / 'local'
                         if self.raw.parts[-2:] == ('pokefirered', self.commit[:8])
                         else self.root / 'content/raw/local')
        self.local_palettes = Path(local_palettes) if local_palettes else default_local
        self.cache = {}

    def read(self, path, local=None):
        key = (local, path)
        if key not in self.cache:
            root = safe(self.local_palettes, local) if local else self.raw
            target = safe(root, path)
            if local or self.reference is None:
                self.cache[key] = target.read_bytes()
            else:
                self.cache[key] = subprocess.check_output(
                    ['git', '-C', str(self.reference), 'show', self.commit + ':' + path])
        return self.cache[key]

    def text(self, path):
        return self.read(path).decode('utf-8')

    def require(self, path, text):
        if text not in self.text(path):
            raise ValueError('Source binding differs in ' + path + ': ' + text)


def palette_colors(data):
    lines = data.decode('utf-8').splitlines()
    if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19:
        raise ValueError('Expected a 16-color JASC palette')
    colors = [tuple(map(int, line.split())) for line in lines[3:]]
    if any(len(c) != 3 or any(v < 0 or v > 255 for v in c) for c in colors):
        raise ValueError('Invalid palette channel')
    return [tuple((v // 8 << 3) | (v // 8 >> 2) for v in c) for c in colors]


def portrait_png(image_data, palette_data, side):
    """Select settled frame zero, preserving RGB even behind transparent index 0."""
    size = {'front': (64, 64), 'back': (64, 320)}[side]
    with Image.open(io.BytesIO(image_data)) as image:
        if image.format != 'PNG' or image.mode != 'P' or image.size != size:
            raise ValueError('Trainer indexed image geometry differs')
        indices = image.crop((0, 0, 64, 64)).tobytes()
    if max(indices) > 15 or not any(indices) or 0 not in indices:
        raise ValueError('Trainer palette indices differ')
    colors = palette_colors(palette_data)
    rgba = bytes(c for i in indices for c in (*colors[i], 255 if i else 0))
    return png(64, 64, rgba)


def source_geometry(sources, spec):
    """Resolve IDs and placements from source tables instead of numeric copies."""
    symbol, stem, side = spec['symbol'], spec['stem'], spec.get('side', 'front')
    if side not in ('front', 'back') or not re.fullmatch('[a-z0-9_]+', stem) or not re.fullmatch('[A-Za-z0-9]+', symbol):
        raise ValueError('Invalid trainer portrait selection')
    constant = ('TRAINER_PIC_' if side == 'front' else 'TRAINER_BACK_PIC_') + stem.upper()
    match = re.search(r'^#define ' + constant + r'\s+(\d+)\s*$', sources.text(CONSTANTS), re.M)
    if not match:
        raise ValueError('Missing trainer picture constant: ' + constant)
    picture = int(match[1])
    image = f'graphics/trainers/{side}_pics/{stem}_{side}_pic.png'
    palette = f'graphics/trainers/palettes/{stem if side == "front" else stem + "_back_pic"}.pal'
    if side == 'front':
        sources.require(GRAPHICS, f'gTrainerFrontPic_{symbol}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")')
        sources.require(GRAPHICS, f'gTrainerPalette_{symbol}[] = INCBIN_U32("{palette[:-4]}.gbapal.lz")')
        sources.require(FRONT_TABLE, f'TRAINER_SPRITE({stem.upper()}, gTrainerFrontPic_{symbol}, 0x800)')
        sources.require(FRONT_TABLE, f'TRAINER_PAL({stem.upper()}, gTrainerPalette_{symbol})')
        coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', sources.text(FRONT_TABLE).split('};', 1)[0])
        if picture >= len(coords):
            raise ValueError('Trainer picture has no source coordinates: ' + constant)
        size = int(coords[picture][0])
        sources.require('src/battle_controller_opponent.c', '(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40')
        sources.require('src/battle_controller_opponent.c', 'CreateSprite(&gMultiuseSpriteTemplate,\n                                                     176,')
        x, baseline = 176, 40
    else:
        sources.require(GRAPHICS, f'gTrainerBackPic_{symbol}[] = INCBIN_U8("{image[:-4]}.4bpp")')
        sources.require(GRAPHICS, f'gTrainerPalette_{symbol}BackPic[] = INCBIN_U32("{palette[:-4]}.gbapal.lz")')
        match = re.search(r'\[' + constant + r'\] = \{\.size = (\d+), \.y_offset = (\d+)\}', sources.text(BACK_TABLE))
        if not match:
            raise ValueError('Trainer back picture has no source coordinates: ' + constant)
        size = int(match[1])
        sources.require(BACK_TABLE, f'{{ (const u32 *)gTrainerBackPic_{symbol}, 0x2800, {picture} }}')
        sources.require(BACK_TABLE, f'{{ gTrainerPalette_{symbol}BackPic, {picture} }}')
        sources.require('src/battle_controller_player.c', 'xPos = 80;')
        sources.require('src/battle_controller_player.c', '(8 - gTrainerBackPicCoords[trainerPicId].size) * 4 + 80')
        x, baseline = 80, 80
    if size != 8:
        raise ValueError('Trainer portrait must occupy eight source tiles')
    return image, palette, picture, constant, x - 32, (8 - size) * 4 + baseline - 32


def build(sources, selection, catalog):
    """Build a complete output set before publication; groups select data only."""
    rows = selection.get('portraits', [])
    if not rows:
        raise ValueError('Empty trainer portrait selection')
    metadata = selection.get('metadata', [])
    if len(set(metadata)) != len(metadata) or set(metadata) - {'sourceTrainerPicId', 'trainerPic'}:
        raise ValueError('Unknown or duplicate portrait metadata field')
    collection = selection['collection']
    if collection not in ('assets', 'portraits'):
        raise ValueError('Unknown portrait manifest collection')
    manifest = selection['manifest']
    if Path(manifest).suffix != '.json' or '/' in manifest:
        raise ValueError('Expected a JSON manifest filename')
    safe(sources.root, manifest)
    sources.require('tools/gbagfx/gfx.c', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)')
    outputs, assets = {}, []
    for row in rows:
        key, identity = (row, row) if isinstance(row, str) else (row['portrait'], row['id'])
        safe(sources.root, identity + '.png')
        if '/' in identity or identity + '.png' in outputs:
            raise ValueError('Duplicate or invalid portrait output: ' + identity)
        if key not in catalog:
            raise ValueError('Unknown trainer portrait: ' + key)
        spec = catalog[key]
        image, palette, picture, constant, x, y = source_geometry(sources, spec)
        outputs[identity + '.png'] = portrait_png(sources.read(image), sources.read(palette, spec.get('localPalette')), spec.get('side', 'front'))
        asset = dict(id=identity, texturePath=identity + '.png', width=64, height=64, x=x, y=y,
                     sourceImage=image, sourcePalette=palette, sourceFrame=0)
        extra = {'sourceTrainerPicId': picture, 'trainerPic': constant}
        asset.update({field: extra[field] for field in metadata})
        assets.append(asset)
    for trainer, binding in selection.get('trainerBindings', {}).items():
        match = re.search(r'\[' + re.escape(trainer) + r'\]\s*=\s*\{(.*?)\n\s*\},', sources.text('src/data/trainers.h'), re.S)
        if not match or any(f'.{field} = {prefix}{binding[key]}' not in match[1]
                            for field, prefix, key in [('trainerPic', 'TRAINER_PIC_', 'picture'), ('trainerClass', 'TRAINER_CLASS_', 'class')]):
            raise ValueError('Trainer portrait binding differs: ' + trainer)
    document = {collection: assets, 'paletteExpansion': PALETTE_EXPANSION}
    if 'schemaVersion' in selection:
        document.update(schemaVersion=selection['schemaVersion'], outputs=[{'path': p} for p in sorted(outputs)])
    if selection.get('trainerClasses'):
        classes = {}
        for name in selection['trainerClasses']:
            symbol = 'TRAINER_CLASS_' + name
            match = re.search(r'\[' + re.escape(symbol) + r'\]\s*= _\("([^"]+)"\)', sources.text('src/data/text/trainer_class_names.h'))
            if not match or symbol in classes:
                raise ValueError('Missing or duplicate trainer class: ' + symbol)
            classes[symbol] = match[1]
        document['trainerClasses'] = classes
    outputs[manifest] = runtime_json_bytes(document, sort_keys=True)
    return outputs


def publish(outputs, output, *, check=False):
    if not outputs:
        raise ValueError('No trainer portrait outputs')
    targets = [(safe(output, name), data) for name, data in outputs.items()]
    if check:
        for path, data in targets:
            if not generated_output_matches(path, data):
                raise ValueError('Trainer portrait output differs: ' + str(path))
    else:
        for path, data in targets:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)


def selected_trainer_art(source, selection, inherited, emit, assets, *, inherited_ids=True):
    """Resolve selected trainer names/portraits while preserving inherited pixels.

    This reads presentation fields only; trainer_import owns complete battle
    parties. The selected source records determine IDs, music and battle mode.
    Older manifests use canonical portrait IDs even for inherited texture paths.
    """
    indexed = PixelReader(source).indexed
    trainers, portraits = [], []
    trainer_source, trainer_constants = source.text('src/data/trainers.h'), source.text('include/constants/trainers.h')
    opponents, classes = source.text('include/constants/opponents.h'), source.text('src/data/text/trainer_class_names.h')
    trainer_graphics = source.text('src/data/graphics/trainers.h')
    picture_table = source.text('src/data/trainer_graphics/front_pic_tables.h')
    coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', picture_table.split('const struct CompressedSpriteSheet')[0])
    trainer_pics = {}
    for name in selection:
        symbol = 'TRAINER_' + name
        match = re.search(r'\[' + symbol + r'\]\s*=\s*\{(.*?)\n    \}', trainer_source, re.S)
        need(match, 'Trainer source record missing: ' + symbol)
        body = match[1]
        cls, pic = re.search(r'\.trainerClass = (\w+)', body)[1], re.search(r'\.trainerPic = (\w+)', body)[1]
        display_name = re.search(r'\.trainerName = _\("([^\"]+)"\)', body)[1]
        cls_name = re.search(r'\[' + cls + r'\]\s*= _\("([^\"]+)"\)', classes)[1]
        source_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)\s*$', opponents, re.M)[1])
        pic_id = int(re.search(r'^#define ' + pic + r'\s+(\d+)\s*$', trainer_constants, re.M)[1])
        music = re.search(r'\.encounterMusic_gender = ([^,]+),', body)[1]
        music_value = 0
        for part in music.split('|'):
            part = part.strip()
            music_value |= 128 if part == 'F_TRAINER_FEMALE' else int(re.search(r'^#define ' + part + r'\s+(\d+)', trainer_constants, re.M)[1])
        key = pic.removeprefix('TRAINER_PIC_')
        portrait_id = PurePosixPath(inherited[key]).stem if inherited_ids and key in inherited else key.lower().replace('_', '-') + '-front'
        trainers.append(dict(trainer='pret:' + symbol, sourceTrainerId=source_id, name=display_name,
            trainerClass=cls, className=cls_name, trainerPic=pic, portrait=portrait_id,
            encounterMusicGender=music_value, doubleBattle='.doubleBattle = TRUE,' in body))
        trainer_pics[pic] = pic_id
    for pic, number in sorted(trainer_pics.items()):
        key = pic.removeprefix('TRAINER_PIC_')
        pic_symbol = re.search(r'TRAINER_SPRITE\(' + key + r', (\w+), 0x800\)', picture_table)[1]
        pal_symbol = re.search(r'TRAINER_PAL\(' + key + r', (\w+)\)', picture_table)[1]
        paths = []
        for symbol in (pic_symbol, pal_symbol):
            match = re.search(r'const u32 ' + symbol + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', trainer_graphics)
            need(match, 'Trainer graphic path missing')
            paths.append(match[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
        _, rgba = indexed(*paths, (64, 64))
        identity = PurePosixPath(inherited[key]).stem if inherited_ids and key in inherited else key.lower().replace('_', '-') + '-front'
        if key in inherited:
            path = inherited[key]
            need(Image.open(io.BytesIO(source.reuse(path))).convert('RGBA').tobytes() == rgba, 'Inherited trainer source pixels differ: ' + key)
            asset = dict(id=identity, width=64, height=64, texturePath=path)
            assets.append(asset)
        else:
            asset = emit(identity, 64, 64, rgba, paths)
        need(coords[number][0] == '8', 'Trainer portrait source size differs')
        portraits.append(dict(trainerPic=pic, sourceTrainerPicId=number, inherited=key in inherited,
                              x=144, y=8, sourceYOffset=int(coords[number][1]), **asset))

    return trainers, portraits
