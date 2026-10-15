#!/usr/bin/env python3
"""Import source fossil species art, dex entries, two/three-row menus and normal cry bindings."""
import argparse
import importlib.util
import io
import json
import re
import sys
import struct
import wave
from pathlib import Path
from PIL import Image

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'fossil-lab-presentation'
BASE = 'content/generated/' + PACKAGE + '/'
PROFILE = 'firered-fossil-lab-presentation-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SPECIES = [('Omanyte', 138), ('Omastar', 139), ('Kabuto', 140), ('Kabutops', 141), ('Aerodactyl', 142)]


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()


def need(condition, message):
    if not condition:
        raise ValueError(message)


def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))


def build(target, dependency, reference, check):
    reader_path = dependency / 'scripts/import-cinnabar-source-preparation.py'
    spec = importlib.util.spec_from_file_location('fossil_lab_presentation_reader', reader_path)
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

    def palette(path, floor):
        lines = source.text(path).splitlines()
        need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Source palette differs')
        return [tuple((int(c) >> 3) * 255 // 31 if floor else ((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2)
                      for c in line.split()) for line in lines[3:]]

    def indexed(image, pal, shape, floor, first_frame=False):
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    def emit(identity, width, height, rgba, paths, **fields):
        buf = io.BytesIO()
        Image.frombytes('RGBA', (width, height), rgba).save(buf, format='PNG', optimize=False, compress_level=9)
        outputs[identity + '.png'] = buf.getvalue()
        return dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png', sourcePaths=paths, **fields)

    league_base = 'content/generated/pokemon-league-presentation/'
    league_bytes = reuse(league_base + 'manifest.json')
    league = json.loads(league_bytes)
    inherited_sprites, inherited_icons = [], []

    def inherit_asset(identity, width, height, rgba, paths, **fields):
        row = next(a for a in league['assets'] if a['id'] == identity)
        need((row['width'], row['height'], row['sourceSpeciesId']) == (width, height, 142), 'League Aerodactyl dimensions differ')
        data = reuse(league_base + row['texturePath'])
        im = Image.open(io.BytesIO(data))
        need(im.mode == 'RGBA' and im.size == (width, height) and im.tobytes() == rgba, 'Reused Aerodactyl PNG differs')
        for key in ['speciesId', 'sourceSpeciesId', 'side', 'paletteVariant', 'x', 'y']:
            if key in fields:
                need(row[key] == fields[key], 'Reused Aerodactyl source geometry differs')
        return dict(id=identity, width=width, height=height, texturePath=league_base + row['texturePath'], sourcePaths=paths, **fields)

    dex = json.loads(reuse('content/generated/pokedex-menu/pokedex-menu.json'))
    species_constants = source.text('include/constants/species.h')
    graphics = source.text('src/data/graphics/pokemon.h')
    sprites, icons, footprints, entries, geometry = [], [], [], [], []
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
                pal = f'graphics/pokemon/{stem}/{variant}.pal'
                rgba = indexed(image, pal, (64, 64), True)
                destination, emit_sprite = (inherited_sprites, inherit_asset) if number == 142 else (sprites, emit)
                destination.append(emit_sprite(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal], sourceSpeciesId=number,
                    speciesId='pret:' + symbol, side=side, paletteVariant=variant,
                    x=144 if side == 'front' else 40, y=8 + y - elevation if side == 'front' else 48 + y,
                    sourceYOffset=y, sourceElevation=elevation if side == 'front' else 0,
                    sourceWidth=width, sourceHeight=height))
            geometry.append(dict(species=number, side=side, yOffset=y, elevation=elevation if side == 'front' else 0,
                                 sourceWidth=width, sourceHeight=height))
        pal_index = int(re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', source.text('src/pokemon_icon.c'))[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        rgba = indexed(image, pal, (32, 64), False, True)
        destination, emit_icon = (inherited_icons, inherit_asset) if number == 142 else (icons, emit)
        destination.append(emit_icon(stem + '-icon', 32, 32, rgba, [image, pal], sourceSpeciesId=number,
                          speciesId='pret:' + symbol, paletteIndex=pal_index, sourceFrame=0))
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
        foot = dict(id=identity, sourceSpeciesId=number, speciesId='pret:' + symbol, width=16, height=16, texturePath=BASE + identity + '.png', sourcePaths=[image, pal], inherited=False)
        footprints.append(foot)
        entry = next(e for e in dex['entries'] if e['sourceSpeciesId'] == number)
        declaration = re.search(r'\[NATIONAL_DEX_' + name.upper() + r'\]\s*=\s*\{(.*?)\n\s*\}', source.text('src/data/pokemon/pokedex_entries.h'), re.S)[1]
        need(re.search(r'\.categoryName\s*=\s*_\("' + entry['category'] + r'"\)', declaration) and
             int(re.search(r'\.height\s*=\s*(\d+)', declaration)[1]) == entry['heightDecimeters'] and
             int(re.search(r'\.weight\s*=\s*(\d+)', declaration)[1]) == entry['weightHectograms'], 'Dex category/measures differ')
        description = re.search(r'const u8 g' + name + r'PokedexText\[\]\s*=\s*_\((.*?)\);', source.text('src/data/pokemon/pokedex_text_fr.h'), re.S)[1]
        text = ''.join(json.loads('"' + s + '"') for s in re.findall(r'"([^"\n]*)"', description))
        need(text == entry['description'], 'Dex description differs')
        front = next(s for s in sprites + inherited_sprites if (s['sourceSpeciesId'], s['side'], s['paletteVariant']) == (number, 'front', 'normal'))
        texture = lambda r: {k: r[k] for k in ['texturePath',  'width', 'height']}
        entries.append(dict(entry, portrait=texture(front), footprint=texture(foot)))

    geometry_path = 'content/generated/campaign-hit-art/manifest.json'
    inherited_geometry = json.loads(reuse(geometry_path))['geometry']
    need(all(row in inherited_geometry for row in geometry), 'Existing source move geometry differs')
    menu_source, strings = source.text('src/script_menu.c'), source.text('src/strings.c')
    menu_constants = source.text('include/constants/menu.h')
    script_path = 'data/maps/CinnabarIsland_PokemonLab_ExperimentRoom/scripts.inc'
    script = source.text(script_path)
    menus = []
    for name, suffix, number in [('Helix', 'HELIX', 21), ('Dome', 'DOME', 22), ('Amber', 'AMBER', 23),
                                  ('HelixAmber', 'HELIX_AMBER', 24), ('DomeAmber', 'DOME_AMBER', 25)]:
        menu = 'MULTICHOICE_' + suffix
        need(re.search(r'^#define ' + menu + r'\s+' + str(number) + r'\s*$', menu_constants, re.M), 'Fossil menu ID differs')
        block = re.search(r'sMultichoiceList_' + name + r'\[\] = \{(.*?)\};', menu_source, re.S)[1]
        symbols = re.findall(r'\{ (\w+) \}', block)
        expected = (['gText_HelixFossil'] if name.startswith('Helix') else ['gText_DomeFossil'] if name.startswith('Dome') else [])
        expected += (['gText_OldAmber'] if 'Amber' in name else []) + ['gOtherText_Exit']
        need(symbols == expected, 'Fossil menu rows differ')
        rows = [re.search(r'const u8 ' + symbol + r'\[\] = _\("([^"\n]+)"\);', strings)[1] for symbol in symbols]
        program = 'CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_ChooseFossil' + name
        need('multichoice 0, 0, ' + menu + ', FALSE' in script and program + '::' in script, 'Fossil menu source command differs')
        height = int(re.search(r'case ' + str(len(rows)) + r':\s*return (\d+);', menu_source)[1])
        need(height == len(rows) * 2, 'Source fossil menu height differs')
        menus.append(dict(menuId=menu, sourceMenuId=number, programId=program, rows=rows, left=0, top=0,
                          ignoreB=False, cancelResult=127, rowHeight=14, interiorHeightTiles=height, wrap=False))
    need('width = (strWidth + 9) / 8 + 1;' in menu_source and
         'MultichoiceList_PrintItems(windowId, FONT_NORMAL, 8, 2, 14, count, list, 0, 2);' in menu_source and
         'Menu_InitCursor(windowId, FONT_NORMAL, 0, 2, 14, count, initPos);' in menu_source and
         'if (count > 3)\n        gTasks[taskId].tWrapAround = TRUE;\n    else\n        gTasks[taskId].tWrapAround = FALSE;' in menu_source,
         'Fossil source menu geometry/input differs')
    item = next(i for i in json.loads(source.text('src/data/items.json'))['items'] if i.get('itemId') == 'ITEM_OLD_AMBER')
    need(re.search(r'^#define ITEM_OLD_AMBER\s+354\s*$', source.text('include/constants/items.h'), re.M) and
         item['english'] == 'OLD AMBER' and item['pocket'] == 'POCKET_KEY_ITEMS' and item['importance'] == 1 and
         item['registrability'] == 0 and item['fieldUseFunc'] == 'FieldUseFunc_OakStopsYou', 'Old Amber item source differs')
    binding = re.search(r'\[ITEM_OLD_AMBER\]\s*=\s*\{(\w+),\s*(\w+)\}', source.text('src/data/item_icon_table.h'))
    paths = [re.search(r'const u32 ' + symbol + r'\[\]\s*=\s*INCBIN_U32\("([^"]+)"\)',
             source.text('src/data/graphics/items.h'))[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal') for symbol in binding.groups()]
    item_asset = emit('old-amber-icon', 24, 24, indexed(*paths, (24, 24), False), paths, sourceItemId=354)
    old_amber = dict(id='pret:ITEM_OLD_AMBER', sourceSymbol='ITEM_OLD_AMBER', sourceItemId=354,
                    name=item['english'], description=item['description_english'].replace('\\n', '\n'),
                    fieldUseFunction=item['fieldUseFunc'], icon=item_asset)
    pack = reuse('content/generated/synth-audio/firered.pack')
    cries = reuse('content/generated/synth-audio/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    cues, cry_evidence, inherited_cry_cues = [], [], []
    for name, species in SPECIES[:-1]:
        row = cry_rows[species]
        need(cry_table[species - 1] == name and row[1] == species - 1, 'Species cry table differs')
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave_offset = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
        need(struct.unpack_from('<HHIII', pack, wave_offset) == (1, 0, 10764288, 0, row[2]), 'Species packed cry header differs')
        path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
        with wave.open(io.BytesIO(source.read(path))) as original:
            need((original.getnchannels(), original.getsampwidth(), original.getframerate(), original.getnframes(), original.getcomptype()) ==
                 (1, 1, 10512, row[2] + 1, 'NONE'), 'Source cry sample/header dimensions differ')
            pcm = original.readframes(original.getnframes())
        delta = (0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1)
        decoded = bytearray()
        for index in range(row[2] + row[4]):
            block, i = divmod(index, 64)
            start = wave_offset + 16 + block * 33
            if i == 0:
                value = pack[start]
            else:
                packed = pack[start + (1 if i == 1 else i // 2 + 1)]
                nibble = packed & 15 if i == 1 or i % 2 else packed >> 4
                value = (value + delta[nibble]) & 255
            decoded.append((value + 128) & 255)
        need(decoded == pcm[:len(decoded)], 'Cry pack/source PCM differs: ' + name)
        cry_evidence.append(dict(species=species, cryIndex=row[1], sampleCount=row[2], payloadBytes=row[3], guardStored=bool(row[4]),
            decodedBytes=len(decoded),  sourcePath=path, sourcePlayer=4, priority=priority))
        cues.append(dict(id='CRY_' + name.upper() + '_NORMAL', sourceCommit=COMMIT, sourceSymbol='SPECIES_' + name.upper(),
            sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))

    league_synth_bytes = reuse(league_base + 'synth-manifest.json')
    inherited_cry = next(c for c in json.loads(league_synth_bytes)['cues'] if c['id'] == 'CRY_AERODACTYL_NORMAL')
    need(inherited_cry['species'] == 142 and inherited_cry['sourcePlayer'] == 4 and inherited_cry['priority'] == priority and
         inherited_cry['cryMode'] == 0 and cry_table[141] == 'Aerodactyl' and cry_rows[142][1] == 141, 'Inherited Aerodactyl cry binding differs')
    inherited_cry_cues.append(inherited_cry)
    outputs['synth-manifest.json'] = canonical(dict(schemaVersion=1, sourceCommit=COMMIT, cues=cues))
    outputs['dex-entries.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, entries=entries))
    lock = source.lock()
    lock['inheritedInputs'] = [inherited[p] for p in sorted(inherited)]
    lb = canonical(lock)
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
                    inputs=lock['files'], inheritedInputs=lock['inheritedInputs'], newOwnedSpecies=[s for _, s in SPECIES],
                    sprites=sprites, pokemonIcons=icons, inheritedSprites=inherited_sprites, inheritedPokemonIcons=inherited_icons,
                    footprints=footprints, menus=menus, oldAmber=old_amber,
                    dexEntriesPath=BASE + 'dex-entries.json', moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry),
                    audio=dict(newCueCount=4, normalCries=cry_evidence, inheritedCues=inherited_cry_cues, physicalAudioVerified=False),
                    limits=['Room, border and reused scientist/boy/player sheets are supplied by fossil-lab-region.',
                            'Aerodactyl sprites, icon and normal cry reuse the authenticated League package; only its owned footprint and dex entry are added.',
                            'No new move animation coverage; native visual and audible output remain unverified.'],
                    authorDependencies=[dict(path='scripts/import-cinnabar-source-preparation.py')],
                    outputs=[dict(path=p) for p, b in sorted(outputs.items())])
    mb = canonical(manifest)
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
                         newFootprints=5, dexEntries=5, menus=5, newBattleSprites=16, inheritedBattleSprites=4, newAudioCues=4, inheritedAudioCues=1,
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
