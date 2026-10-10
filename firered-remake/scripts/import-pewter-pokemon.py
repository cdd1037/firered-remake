#!/usr/bin/env python3
"""Authenticated pewter Pokemon metadata and offline normal/shiny battle pixels.

Only immutable local Git objects or verified raw cache bytes are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-pewter-pokemon-v1'
LOCK_SHA256 = '7c74a69296cba5453da5948d66a3db695b8c840458e422c65562f0b792c54150'
MANIFEST_SHA256 = '8447939fa3cd286b9c024e9f0c0fe690421a69564aba08f3c5d6208a6e90ecb0'
NEW_SPECIES = ('Geodude', 'Graveler', 'Golem', 'Sandshrew', 'Sandslash', 'Onix', 'Steelix')
ART_SPECIES = (('Geodude', 74), ('Graveler', 75), ('Golem', 76), ('Sandshrew', 27),
               ('Sandslash', 28), ('Onix', 95), ('Steelix', 208))
PATHS = frozenset('''charmap.txt
include/constants/species.h
include/constants/moves.h
include/constants/pokemon.h
include/constants/abilities.h
include/constants/items.h
include/constants/battle.h
include/pokemon.h
src/pokemon.c
src/data/pokemon/species_info.h
src/data/pokemon/level_up_learnset_pointers.h
src/data/pokemon/level_up_learnsets.h
src/data/pokemon/evolution.h
src/data/pokemon/experience_tables.h
src/data/battle_moves.h
src/data/text/species_names.h
src/data/text/move_names.h
src/data/graphics/pokemon.h
src/data/pokemon_graphics/front_pic_coordinates.h
src/data/pokemon_graphics/back_pic_coordinates.h
src/data/pokemon_graphics/front_pic_table.h
src/data/pokemon_graphics/back_pic_table.h
src/data/pokemon_graphics/palette_table.h
src/data/pokemon_graphics/shiny_palette_table.h
src/data/pokemon_graphics/enemy_mon_elevation.h
src/battle_anim_mons.c
src/battle_controller_player.c
src/battle_controller_opponent.c
tools/gbagfx/gfx.c'''.splitlines()) | frozenset(
    f'graphics/pokemon/{name.lower()}/{filename}' for name, _ in ART_SPECIES
    for filename in ('front.png', 'back.png', 'normal.pal', 'shiny.pal'))
_spec = importlib.util.spec_from_file_location('pewter_battle_pixels', ROOT / 'scripts/import-route1-battle-art.py')
battle = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(battle)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unique_pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is None:
        target = root
    else:
        path = pathlib.PurePosixPath(relative)
        if path.is_absolute() or '..' in path.parts or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe pewter Pokemon path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output or ancestor rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Pewter Pokemon path escapes root')
    return target



def preflight_directory(path):
    # Check the entire existing chain before any raw/output bytes are written.
    # A missing child beneath a file can otherwise look nonexistent until mkdir.
    for directory in (path, *path.parents):
        if directory.exists() and not directory.is_dir():
            raise ValueError('Input/output ancestor is not a directory: ' + str(directory))


def load_lock(path=None):
    path = path or ROOT / 'scripts/pewter-pokemon.lock.json'
    data = safe(path).read_bytes()
    if sha(data) != LOCK_SHA256:
        raise ValueError('Reviewed pewter Pokemon source lock differs')
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('tree'),
            lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (
            1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0):
        raise ValueError('Pewter Pokemon source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Pewter Pokemon source dependency set differs')
    return lock


def verify(data, entry):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest()
    if (len(data), sha(data), blob) != (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']):
        raise ValueError('Pewter Pokemon source hash differs: ' + entry['path'])


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
                          lockSha256=LOCK_SHA256, fullSourceHash=battle.source_set_hash(lock['files']),
                          inputCount=len(lock['files'])))


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is None:
        if safe(raw, 'manifest.json').read_bytes() != raw_manifest(lock):
            raise ValueError('Pewter Pokemon raw manifest differs')
    else:
        # ls-tree is a read-only object identity check; source contents use git show only.
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
                              check=True, capture_output=True, timeout=30).stdout.decode('ascii').strip()
        if tree != TREE:
            raise ValueError('Pewter Pokemon source tree differs')
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            row = subprocess.run(['git', '-C', str(repository), 'ls-tree', COMMIT, '--', entry['path']],
                                 check=True, capture_output=True, timeout=30).stdout.decode('utf-8')
            if row != '100644 blob ' + entry['gitBlobSha1'] + '\t' + entry['path'] + '\n':
                raise ValueError('Pewter Pokemon input is not the pinned regular Git blob: ' + entry['path'])
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                                  check=True, capture_output=True, timeout=30).stdout
        verify(data, entry)
        sources[entry['path']] = data
    return sources


def metadata(lock, sources):
    text = {p: b.decode('utf-8') for p, b in sources.items() if not p.endswith('.png')}
    entries = {e['path']: e for e in lock['files']}

    def one(pattern, value):
        matches = list(re.finditer(pattern, value, re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected exactly one source match: ' + pattern)
        return matches[0]

    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text[path].count('\n', 0, match.start()) + 1,
                    endLine=text[path].count('\n', 0, match.end()) + 1, sha256=entries[path]['sha256'])

    def block(path, symbol):
        match = one(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', text[path])
        fields = {}
        for line in match[1].splitlines():
            field = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not field or field[1] in fields:
                raise ValueError('Unexpected source field: ' + line)
            fields[field[1]] = field[2]
        return fields, ref(path, symbol, match)

    def ident(symbol):
        return dict(value='pret:' + symbol)

    def constant(path, symbol):
        return one(r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$', text[path])

    def number(path, symbol):
        return int(constant(path, symbol)[1])

    def name(path, symbol):
        m = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z -]+)"\),$', text[path])
        return dict(id=ident(symbol), name=m[1], source=ref(path, symbol, m))

    species, names, move_symbols = [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for name_ in NEW_SPECIES:
        symbol = 'SPECIES_' + name_.upper()
        fields, source = block('src/data/pokemon/species_info.h', symbol)
        if fields['genderRatio'] != 'PERCENT_FEMALE(50)' or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW'):
            raise ValueError('Unsupported pewter family profile')
        types = one(r'^\{(TYPE_\w+), (TYPE_\w+)\}$', fields['types'])
        abilities = one(r'^\{(ABILITY_\w+), (ABILITY_\w+)\}$', fields['abilities'])
        learnpath = 'src/data/pokemon/level_up_learnsets.h'
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', text['src/data/pokemon/level_up_learnset_pointers.h'])[1]
        learn = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', text[learnpath])
        if not re.fullmatch(r'(?:    LEVEL_UP_MOVE\(\d+, MOVE_\w+\),\n)+    LEVEL_UP_END\n', learn[1]):
            raise ValueError('Unsupported or incomplete source learnset')
        moves = []
        for m in re.finditer(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', text[learnpath]):
            if learn.start() < m.start() < learn.end():
                moves.append(dict(level=int(m[1]), move=ident(m[2]), source=ref(learnpath, pointer, m)))
                move_symbols.add(m[2])
        evolution_path = 'src/data/pokemon/evolution.h'
        rows = list(re.finditer(r'^    \[' + symbol + r'\]\s*= (.*),$', text[evolution_path], re.M))
        evolutions = []
        if len(rows) > 1:
            raise ValueError('Duplicate species evolution row')
        if rows:
            m = rows[0]
            level = re.fullmatch(r'\{\{EVO_LEVEL,\s+(\d+), (SPECIES_\w+)\}\}', m[1])
            trade = re.fullmatch(r'\{\{EVO_TRADE, 0, (SPECIES_\w+)\}\}', m[1])
            trade_item = re.fullmatch(r'\{\{EVO_TRADE_ITEM, (ITEM_\w+), (SPECIES_\w+)\}\}', m[1])
            if level:
                evolution = dict(level=int(level[1]), target=ident(level[2]), source=ref(evolution_path, symbol, m))
            elif trade and symbol == 'SPECIES_GRAVELER' and trade[1] == 'SPECIES_GOLEM':
                evolution = dict(level=0, target=ident(trade[1]), source=ref(evolution_path, symbol, m), method='trade')
            elif trade_item and symbol == 'SPECIES_ONIX' and (trade_item[1], trade_item[2]) == ('ITEM_METAL_COAT', 'SPECIES_STEELIX'):
                evolution = dict(level=0, target=ident(trade_item[2]), source=ref(evolution_path, symbol, m),
                                 method='tradeItem', parameter=trade_item[1])
            else:
                raise ValueError('Unsupported evolution declaration: ' + m[1])
            if evolution['target']['value'] not in {'pret:SPECIES_' + n.upper() for n in NEW_SPECIES}:
                raise ValueError('Unimported forward evolution target')
            evolutions.append(evolution)
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol),
            baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)},
            type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']),
            evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}, growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow'}[fields['growthRate']],
            genderRatio=127, ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']),
            levelUpMoves=moves, evolutions=evolutions, source=source, learnsetSource=ref(learnpath, pointer, learn)))
        names.append(name('src/data/text/species_names.h', symbol))
    # Brock's original Onix custom moves require this one non-level-up move.
    # The existing authenticated move/name/constants source files already contain it.
    move_symbols.add('MOVE_ROCK_TOMB')
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(dict(id=ident(symbol), sourceMoveId=number('include/constants/moves.h', symbol),
            pp=int(f['pp']), power=int(f['power']), type=f['type'], accuracy=int(f['accuracy']), effect=f['effect'],
            secondaryEffectChance=int(f['secondaryEffectChance']), target=f['target'], priority=int(f['priority']),
            flags=[] if f['flags'] == '0' else f['flags'].split(' | '), battleEffectImplemented=False, source=source))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    for path, symbol in [('include/constants/pokemon.h', 'EVO_LEVEL'), ('include/constants/pokemon.h', 'EVO_TRADE'),
                         ('include/constants/pokemon.h', 'EVO_TRADE_ITEM'), ('include/constants/items.h', 'ITEM_METAL_COAT')]:
        m = constant(path, symbol)
        constants.append(dict(symbol=symbol, value=int(m[1]), source=ref(path, symbol, m)))
    encoding_path = 'charmap.txt'
    english = one(r"^'A'[ \t]+= BB.*?^'\$'[ \t]+= FF", text[encoding_path])
    encoding_source = ref(encoding_path, 'English A-Z and EOS', english)
    encoded_names = []
    for row in names + move_names:
        values = []
        for char in row['name'] + '$':
            m = one(r"^'" + re.escape(char) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$", text[encoding_path])
            values.append(int(m[1], 16))
        encoded_names.append(dict(symbol=row['source']['symbol'], name=row['name'], bytes=values,
                                  source=row['source'], encodingSource=encoding_source))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'],
        species=species, moves=moves, speciesNames=names, moveNames=move_names, constants=constants,
        encodedNames=encoded_names, itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False,
        tradeEvolutionExecutorImplemented=False,
        limitations=['Adds Geodude/Graveler/Golem, Sandshrew/Sandslash and Onix/Steelix to the unchanged twenty-two species.',
                     'Steelix is imported solely to preserve source evolution target closure; no campaign availability is enabled.',
                     'Complete level/trade/trade-item metadata is retained; this increment implements no evolution executor or battle effects.',
                     'Source trade-item code assigns its target before the National Dex conditional; that conditional gates held-item removal. Reachability is not certified.',
                     'Canonical names contain original English charmap bytes and EOS, not padded native save storage.',
                     'Rock Tomb is included for Brock\'s source custom moves only; breeding, eggs, general TM/HM and tutor coverage remain outside this content increment.'])


def art(lock, sources):
    display = battle.display_contract(lock, sources)
    outputs, sprites = {}, []
    elevation_path = 'src/data/pokemon_graphics/enemy_mon_elevation.h'
    elevation_source = battle.section(sources[elevation_path].decode('utf-8'), 'const u8 gEnemyMonElevation[NUM_SPECIES] =')
    body = elevation_source.split('{', 1)[1].rsplit('}', 1)[0]
    if not re.fullmatch(r'(?:\s*\[SPECIES_[A-Z0-9_]+\] = \d+,\s*)+', body):
        raise ValueError('Unsupported enemy elevation table')
    for name, sid in ART_SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        row = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', elevation_source, re.M)
        front_elevation = int(row[1]) if row else 0
        for side in ('front', 'back'):
            image_path = f'graphics/pokemon/{stem}/{side}.png'
            coords_path = f'src/data/pokemon_graphics/{side}_pic_coordinates.h'
            coords = re.search(r'    \[' + symbol + r'\] =\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+),\s*\},', sources[coords_path].decode('utf-8'))
            if coords is None:
                raise ValueError('Missing source sprite coordinates')
            width, height, offset = map(int, coords.groups())
            indices = battle.pixels.indexed_png(sources[image_path])
            opaque = [i for i, index in enumerate(indices) if index]
            if not opaque:
                raise ValueError('Empty sprite')
            left, top = min(i % 64 for i in opaque), min(i // 64 for i in opaque)
            right, bottom = max(i % 64 for i in opaque) + 1, max(i // 64 for i in opaque) + 1
            base = display['playerBaseCenter' if side == 'back' else 'opponentBaseCenter']
            elevation = 0 if side == 'back' else front_elevation
            center = dict(x=base['x'], y=base['y'] + offset - elevation)
            image_symbol = f'gMon{side.title()}Pic_{name}'
            def reference(path, symbol_, needle=None):
                return battle.reference(lock, sources, path, symbol_, needle)
            shared = [reference(image_path, image_symbol), reference(coords_path, f'gMon{side.title()}PicCoords[{symbol}]', coords[0]),
                      reference(elevation_path, f'gEnemyMonElevation[{symbol}]', elevation_source),
                      reference('src/data/graphics/pokemon.h', image_symbol,
                          f'const u32 {image_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{side}.4bpp.lz");'),
                      reference(f'src/data/pokemon_graphics/{side}_pic_table.h', f'gMon{side.title()}PicTable',
                          f'    SPECIES_SPRITE({name.upper()}, {image_symbol}),')]
            for variant in ('normal', 'shiny'):
                palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
                colors, words = battle.pixels.palette(sources[palette_path])
                rgba = battle.pixels.rgba_pixels(indices, colors)
                filename = f'{stem}-{side}-{variant}'
                outputs[filename + '.png'] = battle.pixels.rgba_png(rgba)
                outputs[filename + '.rgba'] = rgba
                palette_symbol = 'gMon' + ('Shiny' if variant == 'shiny' else '') + 'Palette_' + name
                table = 'shiny_palette_table.h' if variant == 'shiny' else 'palette_table.h'
                macro = 'SPECIES_SHINY_PAL' if variant == 'shiny' else 'SPECIES_PAL'
                refs = shared + [reference(palette_path, palette_symbol),
                    reference('src/data/graphics/pokemon.h', palette_symbol,
                        f'const u32 {palette_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{variant}.gbapal.lz");'),
                    reference('src/data/pokemon_graphics/' + table, macro,
                        f'    {macro}({name.upper()}, {palette_symbol}),')]
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, side=side,
                    paletteVariant=variant, texturePath=filename + '.png', sha256=sha(outputs[filename + '.png']),
                    rgbaPath=filename + '.rgba', rgbaSha256=sha(rgba), sourceImagePath=image_path,
                    sourcePalettePath=palette_path, indexSha256=sha(indices), width=64, height=64,
                    frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0,
                    sourceWidth=width, sourceHeight=height, sourceYOffset=offset, sourceEnemyElevation=elevation,
                    singleBattleBaseCenter=base, singleBattleCenter=center,
                    singleBattleTopLeft=dict(x=center['x'] - 32, y=center['y'] - 32),
                    alphaBounds=dict(x=left, y=top, width=right-left, height=bottom-top), transparentIndex=0,
                    paletteRgb555=words, paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False, sourceRefs=refs))
    outputs['battle-art.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        fullSourceHash=battle.source_set_hash(lock['files']), display=display, sprites=sprites,
        runtimeShadersUsed=False, limitations=[
            'Seven forward-family species, one static front/back frame each, normal and shiny offline RGBA/PNG.',
            'Default single-battle placement only; no animation, shadows, battle UI, cry audio or original-frame equivalence claim.']))
    return outputs


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        tree=TREE, lockSha256=LOCK_SHA256, fullSourceHash=battle.source_set_hash(lock['files']),
        inputCount=len(lock['files']), addedSpeciesCount=7, composedSpeciesCount=29,
        artSpeciesCount=7, textureCount=28, rgbaCount=28, upstreamScriptsExecuted=False,
        romDownloaded=False, publiclyUploaded=False, runtimeShadersUsed=False,
        outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    raw, out = safe(raw), safe(out)
    preflight_directory(raw)
    preflight_directory(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if repository is not None:
        repo = pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw, out)):
            raise ValueError('Materialization must not overlap the reference repository')
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    if sha(outputs['manifest.json']) != MANIFEST_SHA256:
        raise ValueError('Reviewed pewter Pokemon generated output hashes differ')
    # Parse/render/authenticate everything, then preflight every target before the first write.
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
    if repository is not None:
        material[(raw, 'manifest.json')] = raw_manifest(lock)
    material.update({(out, p): b for p, b in outputs.items()})
    targets = [(safe(root, p), safe(root, p + '.tmp'), b) for (root, p), b in material.items()]
    for target, _, _ in targets:
        preflight_directory(target.parent)
    if any(p.exists() and not p.is_file() for target, stage, _ in targets for p in (target, stage)):
        raise ValueError('Input/output target is not a regular file')
    for target, stage, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage.write_bytes(data)
        stage.replace(target)
    return dict(profile=PROFILE, inputCount=len(sources), outputCount=len(outputs),
                catalogSha256=sha(outputs['catalog.json']), manifestSha256=sha(outputs['manifest.json']),
                fullSourceHash=battle.source_set_hash(lock['files']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pewter-pokemon')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/pewter-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/pewter-pokemon.lock.json')
    args = parser.parse_args()
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
