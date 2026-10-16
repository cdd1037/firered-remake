#!/usr/bin/env python3
"""Import two source Dojo dex footprints and bind existing art; no duplicate sprites or new audio."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from presentation_pixels import PixelReader
import argparse
import importlib.util
import io
import json
import re
import sys
from pathlib import Path
from PIL import Image

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PACKAGE = 'fighting-dojo-presentation'
BASE = 'content/generated/sprites/pokemon/fighting-dojo/'
PROFILE = 'firered-fighting-dojo-presentation-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SPECIES = [('Hitmonlee', 106), ('Hitmonchan', 107)]
TRAINERS = [('KOICHI', 317), ('MIKE', 318), ('HIDEKI', 319), ('AARON', 320), ('HITOSHI', 321)]


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()


def need(condition, message):
    if not condition:
        raise ValueError(message)


def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))


def build(target, dependency, reference, check):
    reader_path = dependency / 'scripts/imports/source/import-cinnabar-source-preparation.py'
    spec = importlib.util.spec_from_file_location('dojo_presentation_reader', reader_path)
    reader = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(reader)
    reader.PROFILE = PROFILE
    source = reader.Source(reference)
    inherited, outputs = {}, {}

    def reuse(path):
        need(not Path(path).is_absolute() and '..' not in Path(path).parts, 'Unsafe inherited path')
        p = dependency / path
        safe(p)
        data = p.read_bytes()
        inherited[path] = dict(path=path)
        return data

    pixels = PixelReader(source)
    palette, indexed = pixels.palette, pixels.pixels

    league_base = 'content/generated/sprites/pokemon/pokemon-league/'
    league = json.loads(reuse(league_base + 'manifest.json'))
    dex = json.loads(reuse('content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json'))
    species_constants = source.text('include/constants/species.h')
    graphics = source.text('src/data/graphics/pokemon.h')
    sprites, icons, footprints, entries = [], [], [], []
    for name, number in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        need(re.search(r'^#define ' + symbol + r'\s+' + str(number) + r'\s*$', species_constants, re.M), 'Species identity differs')
        for side in ['front', 'back']:
            coordinate_path = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text(coordinate_path))
            need(match, 'Source battler coordinates missing')
            width, height, y = map(int, match.groups())
            elevation = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source.text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
            elevation = int(elevation[1]) if elevation else 0
            image = f'graphics/pokemon/{stem}/{side}.png'
            need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Source sprite declaration differs')
            for variant in ['normal', 'shiny']:
                old = next(r for r in league['sprites'] if (r['sourceSpeciesId'], r['side'], r['paletteVariant']) == (number, side, variant))
                png_path = league_base + old['texturePath']
                encoded = reuse(png_path)
                rgba = Image.open(io.BytesIO(encoded)).convert('RGBA').tobytes()
                expected = indexed(image, f'graphics/pokemon/{stem}/{variant}.pal', (64, 64), True)
                need(rgba == expected and Image.open(io.BytesIO(encoded)).convert('RGBA').tobytes() == rgba, 'Inherited Hitmon sprite differs')
                need((old['x'], old['y']) == (144, 8 + y - elevation) if side == 'front' else (old['x'], old['y']) == (40, 48 + y), 'Inherited Hitmon sprite position differs')
                sprites.append(dict(old, texturePath=png_path, inherited=True))
        old = next(r for r in league['pokemonIcons'] if r['sourceSpeciesId'] == number)
        pal_index = int(re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', source.text('src/pokemon_icon.c'))[1])
        png_path = league_base + old['texturePath']
        encoded = reuse(png_path)
        rgba = Image.open(io.BytesIO(encoded)).convert('RGBA').tobytes()
        need(rgba == indexed(f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal', (32, 64), False, True), 'Inherited Hitmon icon differs')
        need(Image.open(io.BytesIO(encoded)).convert('RGBA').tobytes() == rgba, 'Inherited icon PNG differs')
        icons.append(dict(old, texturePath=png_path, inherited=True))
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source shape differs')
        colors = palette(pal, True)
        indices = [1 if v == 0 else 0 for v in im.get_flattened_data()]
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        buf = io.BytesIO()
        Image.frombytes('RGBA', (16, 16), rgba).save(buf, format='PNG', optimize=False, compress_level=9)
        identity = stem + '-footprint'
        outputs[identity + '.png'] = buf.getvalue()
        foot = dict(id=identity, sourceSpeciesId=number, speciesId='pret:' + symbol, width=16, height=16, texturePath=BASE + identity + '.png',  inherited=False)
        footprints.append(foot)
        entry = next(e for e in dex['entries'] if e['sourceSpeciesId'] == number)
        declaration = re.search(r'\[NATIONAL_DEX_' + name.upper() + r'\]\s*=\s*\{(.*?)\n\s*\}', source.text('src/data/pokemon/pokedex_entries.h'), re.S)[1]
        need(re.search(r'\.categoryName\s*=\s*_\("' + entry['category'] + r'"\)', declaration) and
             int(re.search(r'\.height\s*=\s*(\d+)', declaration)[1]) == entry['heightDecimeters'] and
             int(re.search(r'\.weight\s*=\s*(\d+)', declaration)[1]) == entry['weightHectograms'], 'Dex category/measures differ')
        description = re.search(r'const u8 g' + name + r'PokedexText\[\]\s*=\s*_\((.*?)\);', source.text('src/data/pokemon/pokedex_text_fr.h'), re.S)[1]
        text = ''.join(json.loads('"' + s + '"') for s in re.findall(r'"([^"\n]*)"', description))
        need(text == entry['description'], 'Dex description differs')
        front = next(s for s in sprites if (s['sourceSpeciesId'], s['side'], s['paletteVariant']) == (number, 'front', 'normal'))
        texture = lambda r: {k: r[k] for k in ['texturePath',  'width', 'height']}
        entries.append(dict(entry, portrait=texture(front), footprint=texture(foot)))

    gym = json.loads(reuse('content/generated/ui/viridian-gym-presentation/manifest.json'))
    portrait = next(p for p in gym['portraits'] if p['id'] == 'black-belt-front')
    rgba = Image.open(io.BytesIO(reuse(portrait['texturePath']))).convert('RGBA').tobytes()
    need(rgba == indexed(portrait['sourceImage'], portrait['sourcePalette'], (64, 64), False), 'Inherited Black Belt portrait differs')
    portrait = dict(portrait, inherited=True)
    trainers = []
    declarations = source.text('src/data/trainers.h')
    opponents = source.text('include/constants/opponents.h')
    trainer_constants = source.text('include/constants/trainers.h')
    need(re.search(r'^#define TRAINER_PIC_BLACK_BELT\s+105\s*$', trainer_constants, re.M), 'Black Belt picture ID differs')
    class_name = re.search(r'\[TRAINER_CLASS_BLACK_BELT\]\s*=\s*_\("([^"]+)"\)', source.text('src/data/text/trainer_class_names.h'))[1]
    for name, number in TRAINERS:
        need(re.search(r'^#define TRAINER_BLACK_BELT_' + name + r'\s+' + str(number) + r'\s*$', opponents, re.M), 'Trainer source ID differs')
        record = re.search(r'\[TRAINER_BLACK_BELT_' + name + r'\]\s*=\s*\{(.*?)\n\s*\}', declarations, re.S)[1]
        need('.trainerClass = TRAINER_CLASS_BLACK_BELT' in record and '.trainerPic = TRAINER_PIC_BLACK_BELT' in record and
             '.encounterMusic_gender = TRAINER_ENCOUNTER_MUSIC_INTENSE' in record and
             '.trainerName = _("' + name + '")' in record, 'Dojo trainer presentation differs')
        trainers.append(dict(trainer='pret:TRAINER_BLACK_BELT_' + name, sourceTrainerId=number, name=name, trainerClass='TRAINER_CLASS_BLACK_BELT',
                             className=class_name, trainerPic='TRAINER_PIC_BLACK_BELT', sourceTrainerPicId=105,
                             portrait='black-belt-front', encounterMusicGender=4))
    synth = json.loads(reuse(league_base + 'synth-manifest.json'))
    cries = [next(c for c in synth['cues'] if c['id'] == 'CRY_' + name.upper() + '_NORMAL') for name, _ in SPECIES]
    need([c['species'] for c in cries] == [106, 107], 'Inherited cry species differs')
    outputs['dex-entries.json'] = canonical(dict(schemaVersion=1,   entries=entries))
    lock = source.lock()
    lock['inheritedInputs'] = [inherited[p] for p in sorted(inherited)]
    lb = canonical(lock)
    manifest = dict(schemaVersion=1,
                      newOwnedSpecies=[106, 107],
                    sprites=sprites, pokemonIcons=icons, footprints=footprints, portraits=[portrait], trainers=trainers,
                    npc=dict(graphicsId='OBJ_EVENT_GFX_BLACK_BELT'),
                    dexEntriesPath=BASE + 'dex-entries.json', audio=dict(newCueCount=0, inheritedCries=cries),
                    limits=['Room and border layers are supplied by fighting-dojo-region.',
                            'Existing League sprites, party/storage icons and cries are reused.',
                            'No new move animation coverage; native visual and audible output remain unverified.'],
                    authorDependencies=[dict(path='scripts/imports/source/import-cinnabar-source-preparation.py')],
                    outputs=[dict(path=p) for p, b in sorted(outputs.items())])
    mb = runtime_json_bytes(manifest, sort_keys=True)
    writes = [(target / BASE / 'manifest.json', mb)]
    writes += [(target / 'content/raw/pokefirered/037335f4' / p, b) for p, b in sorted(source.data.items())]
    writes += [(target / BASE / p, b) for p, b in sorted(outputs.items())]
    for p, b in writes:
        safe(p)
        if p.suffix == '.json':
            json.loads(b)
        if check:
            need(p.is_file() and p.read_bytes() == b, 'Regeneration differs: ' + str(p))
        else:
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b)
    print(json.dumps(dict(passed=True, check=check, sourceFiles=len(source.entries), inheritedInputs=len(inherited),
                         newFootprints=2, dexEntries=2, trainerBindings=5, newBattleSprites=0, newAudioCues=0,
                          lockBytes=len(lb),  manifestBytes=len(mb)), indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', required=True, type=Path)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    for p in [args.reference_root, args.project_root, args.dependency_root]:
        safe(p.absolute())
    target, dependency, reference = [p.resolve() for p in [args.project_root, args.dependency_root, args.reference_root]]
    need(not (target == reference or target.is_relative_to(reference) or reference.is_relative_to(target)), 'Source/output overlap')
    build(target, dependency, reference, args.check)
