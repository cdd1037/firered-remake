#!/usr/bin/env python3
"""Import the bounded Power Plant catalog from local raw files or pinned Git objects; --check is byte exact.
No reference repository code is executed. Includes the Thunder Stone ownership closure.
"""
import argparse,  json, re
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pokemon_import_io import safe, read_source, write_files
from machine_catalog import compatibility_additions, machine_definitions, admission_species

ROOT=Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
SPECIES=['SPECIES_ELECTABUZZ','SPECIES_JOLTEON','SPECIES_ZAPDOS']
BASE='content/generated/power-plant-pokemon/'
RAW='content/raw/pokefirered/037335f4/'
DEPENDENCIES=(
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/five-island-rules/pokemon.json',
    'content/generated/celio-pokemon/pokemon.json',
    'content/generated/seven-island-rules/pokemon.json',
    'content/generated/event-island-species/catalog.json',
    'content/generated/league-rematch-rules/pokemon.json',
    'scripts/five-island-predecessor.json',
)
from image_io import png as encode_rgba_png
from item_icon_exports import decode_icon_page

def encoded(v):return (json.dumps(v,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def ident(s):return dict(value='pret:'+s)
def need(v,m):
 if not v:raise ValueError(m)
def build_power_plant_presentation(read_source_bytes,dependency_root):
 """Source-authored Power Plant presentation builder; never executes reference code.

 build_presentation(read_source_bytes, dependency_root) returns (files, metadata).
 files uses repository-relative paths and includes generated art, raw source cache,
 an authenticated presentation manifest, and its lock. The caller writes files.
 metadata contains the decoded manifest plus lock/report and MtMoon-schema itemIcons.
 The source callback must return bytes from COMMIT; every read is hash authenticated.
 """
 import base64
 import io
 import json
 import pathlib
 import re
 import struct
 import wave
 import zlib
 from PIL import Image

 COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
 TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
 PROFILE = 'firered-power-plant-presentation-v1'
 BASE = 'content/generated/power-plant-presentation/'
 RAW = 'content/raw/pokefirered/037335f4/'
 SPECIES = [('Electabuzz', 125), ('Jolteon', 135), ('Zapdos', 145)]
 SOURCE_PINS = {'graphics/items/icon_palettes/electric_tm_hm.pal': {
                                                      'path': 'graphics/items/icon_palettes/electric_tm_hm.pal'},
  'graphics/items/icon_palettes/normal_tm_hm.pal': {
                                                    'path': 'graphics/items/icon_palettes/normal_tm_hm.pal'},
  'graphics/items/icon_palettes/thunder_stone.pal': {
                                                     'path': 'graphics/items/icon_palettes/thunder_stone.pal'},
  'graphics/items/icons/thunder_stone.png': {
                                             'path': 'graphics/items/icons/thunder_stone.png'},
  'graphics/items/icons/tm_hm.png': {
                                     'path': 'graphics/items/icons/tm_hm.png'},
  'graphics/pokedex/kanto_dex_bgpals.pal': {
                                            'path': 'graphics/pokedex/kanto_dex_bgpals.pal'},
  'graphics/pokemon/electabuzz/back.png': {
                                           'path': 'graphics/pokemon/electabuzz/back.png'},
  'graphics/pokemon/electabuzz/footprint.png': {
                                                'path': 'graphics/pokemon/electabuzz/footprint.png'},
  'graphics/pokemon/electabuzz/front.png': {
                                            'path': 'graphics/pokemon/electabuzz/front.png'},
  'graphics/pokemon/electabuzz/icon.png': {
                                           'path': 'graphics/pokemon/electabuzz/icon.png'},
  'graphics/pokemon/electabuzz/normal.pal': {
                                             'path': 'graphics/pokemon/electabuzz/normal.pal'},
  'graphics/pokemon/electabuzz/shiny.pal': {
                                            'path': 'graphics/pokemon/electabuzz/shiny.pal'},
  'graphics/pokemon/icon_palettes/icon_palette_0.pal': {
                                                        'path': 'graphics/pokemon/icon_palettes/icon_palette_0.pal'},
  'graphics/pokemon/icon_palettes/icon_palette_1.pal': {
                                                        'path': 'graphics/pokemon/icon_palettes/icon_palette_1.pal'},
  'graphics/pokemon/jolteon/back.png': {
                                        'path': 'graphics/pokemon/jolteon/back.png'},
  'graphics/pokemon/jolteon/footprint.png': {
                                             'path': 'graphics/pokemon/jolteon/footprint.png'},
  'graphics/pokemon/jolteon/front.png': {
                                         'path': 'graphics/pokemon/jolteon/front.png'},
  'graphics/pokemon/jolteon/icon.png': {
                                        'path': 'graphics/pokemon/jolteon/icon.png'},
  'graphics/pokemon/jolteon/normal.pal': {
                                          'path': 'graphics/pokemon/jolteon/normal.pal'},
  'graphics/pokemon/jolteon/shiny.pal': {
                                         'path': 'graphics/pokemon/jolteon/shiny.pal'},
  'graphics/pokemon/zapdos/back.png': {
                                       'path': 'graphics/pokemon/zapdos/back.png'},
  'graphics/pokemon/zapdos/footprint.png': {
                                            'path': 'graphics/pokemon/zapdos/footprint.png'},
  'graphics/pokemon/zapdos/front.png': {
                                        'path': 'graphics/pokemon/zapdos/front.png'},
  'graphics/pokemon/zapdos/icon.png': {
                                       'path': 'graphics/pokemon/zapdos/icon.png'},
  'graphics/pokemon/zapdos/normal.pal': {
                                         'path': 'graphics/pokemon/zapdos/normal.pal'},
  'graphics/pokemon/zapdos/shiny.pal': {
                                        'path': 'graphics/pokemon/zapdos/shiny.pal'},
  'include/constants/items.h': {
                                'path': 'include/constants/items.h'},
  'include/constants/sound.h': {
                                'path': 'include/constants/sound.h'},
  'include/constants/species.h': {
                                  'path': 'include/constants/species.h'},
  'sound/cry_tables.inc': {
                           'path': 'sound/cry_tables.inc'},
  'sound/direct_sound_samples/cries/electabuzz.wav': {
                                                      'path': 'sound/direct_sound_samples/cries/electabuzz.wav'},
  'sound/direct_sound_samples/cries/jolteon.wav': {
                                                   'path': 'sound/direct_sound_samples/cries/jolteon.wav'},
  'sound/direct_sound_samples/cries/zapdos.wav': {
                                                  'path': 'sound/direct_sound_samples/cries/zapdos.wav'},
  'src/data/graphics/items.h': {
                                'path': 'src/data/graphics/items.h'},
  'src/data/graphics/pokemon.h': {
                                  'path': 'src/data/graphics/pokemon.h'},
  'src/data/item_icon_table.h': {
                                 'path': 'src/data/item_icon_table.h'},
  'src/data/pokemon_graphics/back_pic_coordinates.h': {
                                                       'path': 'src/data/pokemon_graphics/back_pic_coordinates.h'},
  'src/data/pokemon_graphics/enemy_mon_elevation.h': {
                                                      'path': 'src/data/pokemon_graphics/enemy_mon_elevation.h'},
  'src/data/pokemon_graphics/footprint_table.h': {
                                                  'path': 'src/data/pokemon_graphics/footprint_table.h'},
  'src/data/pokemon_graphics/front_pic_coordinates.h': {
                                                        'path': 'src/data/pokemon_graphics/front_pic_coordinates.h'},
  'src/data/text/species_names.h': {
                                    'path': 'src/data/text/species_names.h'},
  'src/pokemon_icon.c': {
                         'path': 'src/pokemon_icon.c'},
  'src/sound.c': {
                  'path': 'src/sound.c'}}
 DEPENDENCY_PINS = {'content/generated/item-icons/items.png': {'path':'content/generated/item-icons/items.png'},'content/generated/campaign-hit-art/manifest.json': {
                                                       'path': 'content/generated/campaign-hit-art/manifest.json'},
  'content/generated/mtmoon-items/mtmoon-items.json': {
                                                   'path': 'content/generated/mtmoon-items/mtmoon-items.json'},
  'content/generated/synth-audio/firered.pack': {
                                                 'path': 'content/generated/synth-audio/firered.pack'},
  'content/generated/synth-audio/manifest.json': {
                                                  'path': 'content/generated/synth-audio/manifest.json'},
  'content/generated/synth-audio/normal-cries.bin': {
                                                     'path': 'content/generated/synth-audio/normal-cries.bin'},
  'content/generated/vermilion-gym-assets/assets.json': {
                                                           'path': 'content/generated/vermilion-gym-assets/assets.json'}}


 SOURCE_PINS.update({'data/maps/PowerPlant/map.json': {
                                    'path': 'data/maps/PowerPlant/map.json'},
  'data/maps/PowerPlant/scripts.inc': {
                                       'path': 'data/maps/PowerPlant/scripts.inc'},
  'include/constants/songs.h': {
                                'path': 'include/constants/songs.h'},
  'sound/direct_sound_samples/cries/electrode.wav': {
                                                     'path': 'sound/direct_sound_samples/cries/electrode.wav'},
  'sound/song_table.inc': {
                           'path': 'sound/song_table.inc'},
  'sound/songs/midi/mus_encounter_gym_leader.mid': {
                                                    'path': 'sound/songs/midi/mus_encounter_gym_leader.mid'},
  'sound/songs/midi/mus_poke_mansion.mid': {
                                            'path': 'sound/songs/midi/mus_poke_mansion.mid'},
  'sound/songs/midi/mus_vs_legend.mid': {
                                         'path': 'sound/songs/midi/mus_vs_legend.mid'},
  'sound/songs/midi/mus_vs_wild.mid': {
                                       'path': 'sound/songs/midi/mus_vs_wild.mid'},
  'src/battle_setup.c': {
                         'path': 'src/battle_setup.c'}})
 DEPENDENCY_PINS.update({'content/generated/cinnabar-synth-audio/manifest.json': {
                                                           'path': 'content/generated/cinnabar-synth-audio/manifest.json'},
  'content/generated/event-island-presentation/synth-manifest.json': {
                                                                      'path': 'content/generated/event-island-presentation/synth-manifest.json'},
  'content/generated/rocktunnel-synth-audio/manifest.json': {
                                                             'path': 'content/generated/rocktunnel-synth-audio/manifest.json'}})
 def need(ok, message):
     if not ok:
         raise ValueError(message)


 def encode(value):
     return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False) + '\n').encode()


 png = encode_rgba_png


 def build_presentation(read_source_bytes, dependency_root):
     raw, inputs, inherited, out, assets = {}, {}, {}, {}, []
     dependency_root = pathlib.Path(dependency_root).absolute()

     def read(path):
         if path not in raw:
             data = read_source_bytes(path)
             need(isinstance(data, bytes), 'Source callback must return bytes: ' + path)
             row = dict(path=path)
             if SOURCE_PINS:
                 need(SOURCE_PINS.get(path) == row, 'Pinned source bytes differ: ' + path)
             raw[path], inputs[path] = data, row
         return raw[path]

     def text(path):
         return read(path).decode()

     def reuse(path):
         need(':' not in path, 'Unsafe inherited path')
         data = safe(dependency_root, path).read_bytes()
         row = dict(path=path)
         if DEPENDENCY_PINS:
             need(DEPENDENCY_PINS.get(path) == row, 'Pinned inherited bytes differ: ' + path)
         inherited[path] = row
         return data

     def ref(path, symbol, match=None):
         data = read(path)
         content = '' if path.endswith('.png') else data.decode()
         return dict(path=path, symbol=symbol, startLine=1 if match is None else content.count('\n', 0, match.start()) + 1,
                     endLine=max(1, content.count('\n')) if match is None else content.count('\n', 0, match.end()) + 1)

     def emit(identity, width, height, rgba, paths):
         need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
         out[identity + '.png'] = png(width, height, rgba)
         row = dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png', sourcePaths=paths)
         assets.append(row)
         return row

     def palette(path, floor=True):
         lines = text(path).splitlines()
         need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Palette framing differs: ' + path)
         colors = [tuple(map(int, line.split())) for line in lines[3:]]
         need(all(len(row) == 3 and all(0 <= c <= 255 for c in row) for row in colors), 'Palette channels differ')
         return [tuple((c >> 3) * 255 // 31 if floor else ((c >> 3) << 3) | ((c >> 3) >> 2) for c in row) for row in colors]

     def indexed(path, pal, shape, floor=True, first_frame=False):
         with Image.open(io.BytesIO(read(path))) as im:
             need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Indexed source differs: ' + path)
             indices = im.tobytes()[:1024] if first_frame else im.tobytes()
         colors = palette(pal, floor)
         return indices, bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

     sprites, icons, footprints, names = [], [], [], []
     graphics = text('src/data/graphics/pokemon.h')
     constants = text('include/constants/species.h')
     for name, species in SPECIES:
         stem, symbol = name.lower(), 'SPECIES_' + name.upper()
         need(re.search(r'^#define ' + symbol + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Species ID differs')
         elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
         elevation = int(elevation_match[1]) if elevation_match else 0
         for side in ('front', 'back'):
             coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
             match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', text(coordinates))
             need(match, 'Species coordinates missing')
             width, height, y_offset = map(int, match.groups())
             image = f'graphics/pokemon/{stem}/{side}.png'
             need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Species image binding differs')
             for variant in ('normal', 'shiny'):
                 pal = f'graphics/pokemon/{stem}/{variant}.pal'
                 palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                 need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Species palette binding differs')
                 indices, rgba = indexed(image, pal, (64, 64))
                 x, y = (144, 8 + y_offset - elevation) if side == 'front' else (40, 48 + y_offset)
                 sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, renderSpeciesId=species, sourceFrameIndex=0, form=None,
                     side=side, paletteVariant=variant, x=x, y=y, sourceWidth=width, sourceHeight=height,
                     sourceYOffset=y_offset, sourceEnemyElevation=elevation,  sourceCoordinatePath=coordinates,
                     paletteExpansion='floor(rgb5 * 255 / 31)', transparentIndex=0,
                     **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
         icon_table = text('src/pokemon_icon.c')
         match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
         need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
         pal_index = int(match[1])
         image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
         need(f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Species icon image binding differs')
         indices, rgba = indexed(image, pal, (32, 64), first_frame=True)
         icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, paletteIndex=pal_index, sourceFrameIndex=0,
                           sourceFrameCount=2,  **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
         image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
         need('[' + symbol + '] = gMonFootprint_' + name + ',' in text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
         with Image.open(io.BytesIO(read(image))) as im:
             need(im.mode == '1' and im.size == (16, 16), 'Footprint source differs')
             indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
         colors = palette(pal)
         rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
         footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))
         display = re.search(r'\[' + symbol + r'\]\s*=\s*_\("([^"]+)"\)', text('src/data/text/species_names.h'))
         need(display, 'Species display name missing')
         names.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, name=display[1]))

     pack = reuse('content/generated/synth-audio/firered.pack')
     cries = reuse('content/generated/synth-audio/normal-cries.bin')
     need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack differs')
     old_cues = json.loads(reuse('content/generated/synth-audio/manifest.json'))['cues']
     for prior in ['rocktunnel-synth-audio/manifest.json', 'cinnabar-synth-audio/manifest.json', 'event-island-presentation/synth-manifest.json']:
         old_cues.extend(json.loads(reuse('content/generated/' + prior))['cues'])
     cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
     cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
     priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', text('include/constants/sound.h'), re.M)[1])
     need(priority == 10, 'Normal cry priority differs')
     cues, inherited_cues, cry_evidence = [], [], []
     for name, species in [*SPECIES, ('Electrode', 101)]:
         need(re.search(r'^#define SPECIES_' + name.upper() + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Cry species ID differs')
         row = cry_rows[species]
         need(cry_table[row[1]] == name and row[1] == species - 1, 'Species cry table differs')
         tone = 0x0848c914 + row[1] * 12 - 0x08489968
         wave_offset = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
         need(struct.unpack_from('<HHIII', pack, wave_offset) == (1, 0, 10764288, 0, row[2]), 'Packed cry header differs')
         path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
         with wave.open(io.BytesIO(read(path))) as original:
             need((original.getnchannels(), original.getsampwidth(), original.getframerate(), original.getnframes(), original.getcomptype()) ==
                  (1, 1, 10512, row[2] + 1, 'NONE'), 'Source cry sample/header differs')
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
         cue = dict(id='CRY_' + name.upper() + '_NORMAL', sourceCommit=COMMIT, sourceSymbol='SPECIES_' + name.upper(),
             sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0)
         if species in (101, 135):
             old = next(c for c in old_cues if c['id'] == cue['id'])
             need(dict(old, cryMode=old.get('cryMode', 0)) == cue, 'Inherited cry cue differs')
             inherited_cues.append(cue)
         else:
             need(not any(c['id'] == cue['id'] for c in old_cues), 'New cry already admitted')
             cues.append(cue)
         if species in (101, 145):
             cues.append(dict(cue, id='CRY_' + name.upper() + '_ENCOUNTER', cryMode=2))
     sound = text('src/sound.c')
     need(re.search(r'case CRY_MODE_ENCOUNTER:\s*release = 225;\s*pitch = 15600;\s*chorus = 20;\s*volume = 90;\s*break;', sound), 'Encounter cry parameters differ')


     need(re.search(r'#define CRY_MODE_ENCOUNTER\s+2', text('include/constants/sound.h')) and
          re.search(r'length = 140;\s*reverse = FALSE;\s*release = 0;\s*pitch = 15360;\s*chorus = 0;', sound),
          'Encounter cry default length/mode differs')
     script_path = 'data/maps/PowerPlant/scripts.inc'
     script = text(script_path)
     zapdos = script.split('PowerPlant_EventScript_Zapdos::', 1)[1].split('\nPowerPlant_EventScript_DefeatedZapdos::', 1)[0]
     need('setwildbattle SPECIES_ZAPDOS, 50' in zapdos and 'playmoncry SPECIES_ZAPDOS, CRY_MODE_ENCOUNTER' in zapdos and
          'playbgm MUS_ENCOUNTER_GYM_LEADER, 0' in zapdos and 'special StartLegendaryBattle' in zapdos,
          'Zapdos encounter presentation source differs')
     for number in (1, 2):
         electrode = script.split(f'PowerPlant_EventScript_Electrode{number}::', 1)[1].split(f'\nPowerPlant_EventScript_FoughtElectrode{number}::', 1)[0]
         need('setwildbattle SPECIES_ELECTRODE, 34' in electrode and 'playmoncry SPECIES_ELECTRODE, CRY_MODE_ENCOUNTER' in electrode and
              '\n\tdowildbattle\n' in electrode and 'StartLegendaryBattle' not in electrode, 'Electrode encounter presentation source differs')
     need(json.loads(read('data/maps/PowerPlant/map.json'))['music'] == 'MUS_POKE_MANSION', 'Power Plant map music differs')
     setup_path = 'src/battle_setup.c'
     setup = text(setup_path)
     legendary = setup.split('void StartLegendaryBattle(void)', 1)[1].split('void StartGroudonKyogreBattle(void)', 1)[0]
     need(re.search(r'case SPECIES_ZAPDOS:\s*case SPECIES_HO_OH:\s*case SPECIES_LUGIA:\s*CreateBattleStartTask\(B_TRANSITION_BLUR, MUS_VS_LEGEND\);', legendary),
          'Zapdos source battle music binding differs')
     songs = text('include/constants/songs.h')
     song_table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', text('sound/song_table.inc'), re.M)
     for symbol in ['MUS_POKE_MANSION', 'MUS_ENCOUNTER_GYM_LEADER', 'MUS_VS_WILD', 'MUS_VS_LEGEND']:
         song_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
         address, player, second = struct.unpack_from('<IHH', pack, 0x084A32CC + song_id * 8 - 0x08489968)
         need(song_table[song_id] == (symbol.lower(), str(player), str(second)) and player == 0, 'Inherited song table differs')
         path = 'sound/songs/midi/' + symbol.lower() + '.mid'
         midi = read(path)
         need(midi[:4] == b'MThd' and b'\xff\x06\x01[' in midi and b'\xff\x06\x01]' in midi, 'Inherited source music loop differs')
         cue = dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath=path, sourcePlayer=player,
                    priority=pack[address - 0x08489968 + 2], sourceCounter=None, songAddress=address, species=0, looping=True, cryMode=0)
         old = next(c for c in old_cues if c['id'] == symbol)
         need(dict(old, cryMode=old.get('cryMode', 0)) == cue, 'Inherited music cue differs')
         inherited_cues.append(cue)
     need(len(cues) == 4 and len(inherited_cues) == 6, 'Power Plant audio cue inventory differs')

     geometry_path = 'content/generated/campaign-hit-art/manifest.json'
     geometry = json.loads(reuse(geometry_path))['geometry']
     geometry_rows = []
     for row in sprites:
         if row['paletteVariant'] != 'normal':
             continue
         expected = dict(species=row['sourceSpeciesId'], side=row['side'], sourceWidth=row['sourceWidth'], sourceHeight=row['sourceHeight'],
                         yOffset=row['sourceYOffset'], elevation=row['sourceEnemyElevation'] if row['side'] == 'front' else 0)
         need(expected in geometry, 'Inherited battle geometry differs')
         geometry_rows.append(expected)

     # Bag icons keep the earlier item importers' replicated-RGB5 palette convention.
     item_icons = []
     item_table, item_graphics = text('src/data/item_icon_table.h'), text('src/data/graphics/items.h')
     for symbol, item_id, inherited_png in [
         ('ITEM_THUNDER_STONE', 96, None),
         ('ITEM_TM17', 305, ('content/generated/mtmoon-items/mtmoon-items.json','pret:ITEM_TM05')),
         ('ITEM_TM25', 313, ('content/generated/vermilion-gym-assets/assets.json','pret:ITEM_TM34'))]:
         need(re.search(r'^#define ' + symbol + r'\s+' + str(item_id) + r'\s*(?://[^\n]*)?$', text('include/constants/items.h'), re.M), 'Item ID differs')
         binding = re.search(r'^    \[' + symbol + r'\][ \t]*= \{(\w+), (\w+)\},', item_table, re.M)
         need(binding, 'Item icon binding missing')
         paths = []
         for sym in binding.groups():
             definition = re.search(r'^const u32 ' + sym + r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);', item_graphics, re.M)
             need(definition, 'Item graphics source missing')
             paths.append(definition[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
         image, pal = paths
         _, rgba = indexed(image, pal, (24, 24), floor=False)
         if inherited_png:
             descriptor,item = inherited_png
             document=json.loads(reuse(descriptor))
             icon_row=next(i for i in document.get('icons',[document.get('icon')]) if i['item']['value']==item)
             png_path=icon_row['pngPath'];png_bytes=reuse(png_path)
             im=decode_icon_page(png_bytes,icon_row)
             need(im.tobytes()==rgba,'Inherited TM icon differs')
         else:
             emitted = emit('items/' + symbol.lower(), 24, 24, rgba, paths)
             png_path = emitted['texturePath']
             png_bytes = out['items/' + symbol.lower() + '.png']
         item_icons.append(dict(item=dict(value='pret:' + symbol), sourceItemId=item_id, width=24, height=24,
             pngPath=png_path,
             source=ref(image, binding[1]), paletteSource=ref(pal, binding[2]), bindingSource=ref('src/data/item_icon_table.h', symbol, binding),
             inherited=bool(inherited_png)))
         if inherited_png and icon_row.get('region'):item_icons[-1]['region']=icon_row['region']

     if SOURCE_PINS:
         need(set(inputs) == set(SOURCE_PINS), 'Source input inventory differs')
     if DEPENDENCY_PINS:
         need(set(inherited) == set(DEPENDENCY_PINS), 'Inherited input inventory differs')
     out['synth-manifest.json'] = encode(dict(schemaVersion=1, sourceCommit=COMMIT, cues=cues, inheritedCues=inherited_cues))
     lock = dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', commit=COMMIT,
                 files=[inputs[p] for p in sorted(inputs)], inheritedInputs=[inherited[p] for p in sorted(inherited)])
     lock_bytes = encode(lock)
     manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
         assets=assets, sprites=sprites, pokemonIcons=icons,
         footprints=footprints, speciesNames=names, newOwnedSpecies=[s for _, s in SPECIES], itemIcons=item_icons,
         moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry_rows),
         audio=dict(additionalCueIds=[c['id'] for c in cues], inheritedCueIds=[c['id'] for c in inherited_cues], cries=cry_evidence,
                    originalPackUnchanged=True, newAudioAssets=0, recordingsWritten=False, physicalAudioVerified=False,
                    encounter=dict(sourcePath=script_path, battleMusicSourcePath=setup_path,
                        cryMode=2, cryParameters=dict(length=140, release=225, pitch=15600, chorus=20, volume=90),
                        zapdos=dict(species=145, level=50, cry='CRY_ZAPDOS_ENCOUNTER', introductionMusic='MUS_ENCOUNTER_GYM_LEADER', battleMusic='MUS_VS_LEGEND'),
                        electrodes=dict(species=101, level=34, count=2, cry='CRY_ELECTRODE_ENCOUNTER', battleMusic='MUS_VS_WILD'),
                        mapMusic='MUS_POKE_MANSION', playbackAuthority='existing committed presentation effects and typed battle context')),
         outputs=[dict(path=p) for p, b in sorted(out.items())])
     manifest_bytes = encode(manifest)
     out['manifest.json'] = manifest_bytes
     files = {BASE + p: b for p, b in out.items()}
     files.update({RAW + p: b for p, b in raw.items()})
     report = dict(passed=True, sourceFiles=len(raw), inheritedInputs=len(inherited), assets=len(assets), sprites=len(sprites),
         icons=len(icons), footprints=len(footprints), itemIcons=len(item_icons), inheritedItemIcons=2,
         geometry=len(geometry_rows), outputs=len(out) - 1,  lockBytes=len(lock_bytes),
          manifestBytes=len(manifest_bytes),
         synthBytes=len(out['synth-manifest.json']), newCues=len(cues), inheritedCues=len(inherited_cues),
         exactCryPcmCompared=True, nativeWindowVerified=False, physicalAudioVerified=False)
     return files, dict(manifest=manifest, sourceLock=lock, report=report, itemIcons=item_icons)
 return build_presentation(read_source_bytes,dependency_root)

def generate(reference,dependency,raw=None):
 raw_root=raw if raw is not None else ROOT/RAW
 raw={}
 def readbytes(path):
  if path not in raw:
   b=read_source(raw_root,path,commit=COMMIT,repository=reference);raw[path]=b
  return raw[path]
 def read(path):return readbytes(path).decode()
 def one(path,pattern):
  m=list(re.finditer(pattern,read(path),re.M|re.S));need(len(m)==1,'Unique source: '+pattern);return m[0]
 def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=read(path).count('\n',0,m.start())+1,endLine=read(path).count('\n',0,m.end())+1)
 def dep(path):
  b=safe(dependency,path).read_bytes()
  return json.loads(b)
 d={p:dep(p) for p in DEPENDENCIES};canonical=d['content/generated/canonical-pokemon/catalog.json']
 species={s['id']['value'][5:]:s for s in canonical['species']};metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
 seven=d['content/generated/seven-island-rules/pokemon.json']
 owned=[species[5:] for species in admission_species(dependency, BASE+'catalog.json', '/newMachines/0') if species[5:] not in SPECIES]
 opponents={s[5:] for s in d['scripts/five-island-predecessor.json']['opponentSpecies']}|set(owned)|set(d['content/generated/five-island-rules/pokemon.json']['opponentSpeciesAdded'])|{s['id']['value'][5:] for s in d['content/generated/league-rematch-rules/pokemon.json']['opponentSpecies']}|set(seven['trainerOnlySpeciesAdded'])
 need(len(owned)==len(set(owned))==149 and len(opponents)==183,'Exact Event Island predecessor')
 need(not set(SPECIES)&set(owned) and not set(SPECIES)&opponents,'Minimal additions not previously admitted')
 owned+=SPECIES;need(len(owned)==152 and len(opponents|set(SPECIES))==186,'Exact successor counts')
 for s in SPECIES:
  need(not metadata[s]['sourceEvolutions'] and not species[s]['evolutions'],'Terminal added species')
  for r in [species[s]['source'],species[s]['learnsetSource'],metadata[s]['dexSource'],metadata[s]['learnsetPointerSource']]:
   read(r['path'])
 edges=[]
 for s,t in [('SPECIES_PIKACHU','SPECIES_RAICHU'),('SPECIES_EEVEE','SPECIES_JOLTEON')]:
  e=[e for e in metadata[s]['sourceEvolutions'] if e['method']=='EVO_ITEM' and e['parameter']=='ITEM_THUNDER_STONE']
  need(len(e)==1 and e[0]['parameterValue']==96 and e[0]['target']==ident(t),'Exact Thunder Stone edge');edges.append(dict(species=ident(s),sourceEvolution=e[0]));read(e[0]['source']['path'])
 wildpath='src/data/wild_encounters.json';wild=json.loads(read(wildpath));entries=[e for g in wild['wild_encounter_groups'] for e in g.get('encounters',[]) if e['map']=='MAP_POWER_PLANT' and e['base_label'].endswith('_FireRed')]
 need(len(entries)==1 and set(entries[0])=={'map','base_label','land_mons'},'Only original FireRed land encounters')
 w=entries[0];weights=next(f['encounter_rates'] for g in wild['wild_encounter_groups'] for f in g.get('fields',[]) if f['type']=='land_mons')
 need(weights == [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1] and w['land_mons']['encounter_rate'] == 7, 'Original encounter weights/rate')
 land=dict(map=dict(value='map.power-plant'),sourceCommit=COMMIT,sourcePath=wildpath,sourceSymbol=w['base_label'],rawEncounterRate=7,slots=[dict(index=i,species=ident(m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=weights[i]) for i,m in enumerate(w['land_mons']['mons'])])
 map_path='data/maps/PowerPlant/map.json';mapping=json.loads(read(map_path));scriptpath='data/maps/PowerPlant/scripts.inc';scripts=read(scriptpath);statics=[]
 for local,obj in enumerate(mapping['object_events'],1):
  block=re.search(r'^'+obj['script']+r'::\n((?:(?!^\w+::).)*)',scripts,re.M|re.S)
  if not block:continue
  m=re.search(r'^\tsetwildbattle (SPECIES_\w+), (\d+)\n',block[1],re.M)
  if m:
   kind=obj['script'].split('_')[-1];statics.append(dict(kind=kind[0].lower()+kind[1:],localId=local,species=ident(m[1]),level=int(m[2]),heldItem='ITEM_NONE',modernFatefulEncounter=False,legendary=kind=='Zapdos',source=ref(scriptpath,obj['script'],block)))
 need([(s['kind'],s['localId'],s['level']) for s in statics]==[('zapdos',6,50),('electrode2',7,34),('electrode1',8,34)],'Exactly three source static objects')
 for p in ['asm/macros/event.inc','src/pokemon.c','src/script_pokemon_util.c','src/scrcmd.c','src/battle_setup.c','src/battle_main.c','include/random.h','include/constants/pokemon.h','include/constants/species.h','include/constants/items.h','src/data/pokemon/tmhm_learnsets.h','src/data/items.json','src/data/pokemon/item_effects.h','include/constants/item_effects.h','src/move_descriptions.c']:read(p)
 need('CreateMon(&gEnemyParty[0], species, level, 32, 0, 0, OT_ID_PLAYER_ID, 0);' in read('src/script_pokemon_util.c'),'Source scripted generation')
 held=[]
 for s in sorted({r['species']['value'][5:] for r in land['slots']}|{'SPECIES_ELECTRODE','SPECIES_ZAPDOS'}):
  fields=metadata[s]['originalFields'];read(metadata[s]['source']['path']) if 'source' in metadata[s] else read(species[s]['source']['path'])
  held.append(dict(species=ident(s),common=fields['itemCommon'],rare=fields['itemRare']))
 need([(r['species']['value'],r['rare']) for r in held if r['rare']!='ITEM_NONE']==[('pret:SPECIES_MAGNETON','ITEM_MAGNET')],'FireRed held-item tables')
 cells=compatibility_additions(dependency, BASE+'catalog.json', 'machines')
 itemtext=read('src/data/items.json');sourceitems=json.loads(itemtext)['items'];constants={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',read('include/constants/items.h'),re.M)}
 def item(symbol):
  i=next(i for i in sourceitems if i['itemId']==symbol);m=one('src/data/items.json',r'"itemId":\s*"'+symbol+r'"');r=ref('src/data/items.json',symbol,m)
  meta=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=constants[symbol],name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['importance']==0,canGive=i['importance']==0,source=r)
  description=i['description_english'].replace('\\n','\n')
  if symbol.startswith('ITEM_TM'):
   p='src/move_descriptions.c';m=one(p,r'^const u8 gMoveDescription_'+i['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1])
  return dict(item=meta,description=description)
 items=[]
 for symbol in ['ITEM_THUNDER_STONE','ITEM_TM17','ITEM_TM25']:
  wrapped=item(symbol);meta=wrapped['item'];stone=symbol=='ITEM_THUNDER_STONE';effect=None
  if stone:
   p='src/data/pokemon/item_effects.h';m=one(p,r'^static const u8 sItemEffect_ThunderStone\[6\] = \{\n    \[4\] = ITEM4_EVO_STONE,\n\};');effect=ref(p,'sItemEffect_ThunderStone',m)
   need('#define ITEM4_EVO_STONE         0x80' in read('include/constants/item_effects.h'),'Stone effect byte')
  items.append({k: meta[k] for k in ['id', 'sourceItemSymbol', 'sourceItemId', 'name', 'price', 'pocket', 'importance', 'type', 'fieldUseFunction', 'battleUsage', 'battleUseFunction', 'holdEffectParam', 'source']} | dict(description=wrapped['description'], healHp=0, cureStatus=0, effectBytes=[0, 0, 0, 0, 128, 0] if stone else [], useSupport=9 if stone else 10, effectSource=effect, sourceMetadata=meta))
 newmachines=machine_definitions(dependency, BASE+'catalog.json')
 flags=read('include/constants/flags.h');ballscripts=read('data/scripts/item_ball_scripts.inc');pickups=[]
 def flagvalue(symbol):
  m=re.search(r'^#define\s+'+symbol+r'\s+([^\n]+)',flags,re.M);need(m,'Flag exists');expr=m[1].split('//')[0].strip()
  if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expr):return int(expr,0)
  a=re.fullmatch(r'\((FLAG_\w+)\s+\+\s+(\d+)\)',expr);need(a,'Flag expression');return flagvalue(a[1])+int(a[2])
 def pickup(obj,hidden,local,symbol,quantity):
  i=item(symbol)['item'];pickups.append(dict(id='power-plant:'+obj['flag'],mapSymbol='MAP_POWER_PLANT',hidden=hidden,sourceLocalId=local,x=obj['x'],y=obj['y'],elevation=obj['elevation'],script='EventScript_HiddenItemScript' if hidden else obj['script'],itemSymbol=symbol,sourceItemId=i['sourceItemId'],item=i['id'],itemName=i['name'],pocket=i['pocket'],quantity=quantity,flag=obj['flag'],sourceFlagId=flagvalue(obj['flag'])))
 for local,obj in enumerate(mapping['object_events'],1):
  m=re.search(r'^'+obj['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n',ballscripts,re.M)
  if m:pickup(obj,False,local,m[1],int(m[2] or 1))
 for obj in mapping['bg_events']:
  need(obj['type']=='hidden_item' and not obj['underfoot'],'Only ordinary hidden pickups');pickup(obj,True,None,obj['item'],obj['quantity'])
 need(len(pickups)==7 and sum(p['hidden'] for p in pickups)==2,'Five visible and two hidden pickups')
 vectors=[]
 for kind,s,level,seed in [('zapdos','SPECIES_ZAPDOS',50,42),('electrode1','SPECIES_ELECTRODE',34,0x12345678),('electrode2','SPECIES_ELECTRODE',34,0xDEADBEEF)]:
  initial=seed;draws=[]
  for _ in range(4):seed=(seed*1103515245+24691)&0xffffffff;draws.append(seed>>16)
  pid=draws[0]|draws[1]<<16;ivs=[draws[2]&31,(draws[2]>>5)&31,(draws[2]>>10)&31,draws[3]&31,(draws[3]>>5)&31,(draws[3]>>10)&31]
  vectors.append(dict(kind=kind,species=ident(s),level=level,initialSeed=initial,finalSeed=seed,rawDraws=draws,personality=pid,nature=pid%25,ivs=ivs,originalTrainerId=0x12345678))
 doc=dict(sourceCommit=COMMIT,profile='firered-power-plant-pokemon-v1',ownedSpeciesCount=152,opponentSpeciesCount=186,itemCount=123,machineCount=41,species=[dict(definition=species[s],metadata=metadata[s]) for s in SPECIES],thunderStoneEvolutions=edges,land=land,heldItems=held,staticEncounters=statics,machines=cells,newMachines=newmachines,items=items,pickups=pickups,generationVectors=vectors,generationOrder='Static: player OT, random PID then IVs, explicit low16/high16 profile; original target-compiler Random32 operand ordering unverified. Ordinary land uses existing nature rejection generation. Legendary Zapdos skips held draw; ordinary land and Electrode consume one held draw at battle entry. No fateful metadata.')
 slots=',\n            '.join(f'new({r["index"]},new("{r["species"]["value"]}"),{r["minimumLevel"]},{r["maximumLevel"]},{r["weight"]})' for r in land['slots'])
 heldrows=',\n        '.join(f'new(new("{r["species"]["value"]}"),"{r["common"]}","{r["rare"]}")' for r in held)
 code='''using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\npublic sealed record PowerPlantHeldItemSource(SpeciesId Species, string Common, string Rare);\n/// <summary>Exact FireRed Power Plant table, generated only from pinned source.</summary>\npublic static class PowerPlantEncounterTables\n{\n'''+f'''    public static MapId Map {{ get; }} = new("map.power-plant");\n    public static EarlyLandEncounterTable Land {{ get; }} = new(Map, EarlyLandEncounterRules.SourceCommit,\n        EarlyLandEncounterRules.SourcePath, "sPowerPlant_FireRed", 7,\n        [{slots}]);\n    public static ImmutableArray<PowerPlantHeldItemSource> HeldItems {{ get; }} = [\n        {heldrows}];\n}}\n'''
 return doc,raw,{'src/Game.Domain/Encounters/PowerPlantEncounterTables.cs':code.encode()}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path,nargs='?',help='Optional pinned local Git repository');p.add_argument('--raw-root',type=Path,help='Local raw input directory when no reference repository is selected');p.add_argument('output',type=Path);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference is not None and a.raw_root is not None:p.error('Choose either REFERENCE or --raw-root')
 raw_root=a.raw_root if a.raw_root is not None else ROOT/RAW
 doc,raw,files=generate(a.reference,a.dependency_root,raw_root);files[BASE+'catalog.json']=encoded(doc)
 presentation,art=build_power_plant_presentation(lambda path:read_source(raw_root,path,commit=COMMIT,repository=a.reference),a.dependency_root);files.update(presentation)
 for path,b in raw.items():files[RAW+path]=b
 lock=dict(sourceCommit=COMMIT,profile=doc['profile'],files=[dict(path=p) for p,b in sorted(files.items())])
 if a.check:
  for path,b in files.items():need(safe(a.output,path).read_bytes()==b,'Regeneration differs: '+path)
 else:write_files(a.output,files)
 print(json.dumps(dict(checked=a.check,inputs=len(raw),presentation=art['report']),indent=2))
if __name__=='__main__':main()
