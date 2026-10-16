#!/usr/bin/env python3
"""Import Route17, the north gate upstairs and initial Cycling Road single battles.

Route16, Route18 and the existing first-floor gates retain their ownership. This
offline converter reuses the ordinary map renderer, shared trainers and NPC art.
After importing, rebuild field tilesets and map atlases with their usual tools.
"""
from __future__ import annotations

import argparse
import ast
import importlib.util
import json
from pathlib import Path
import re
import shutil
import struct
import sys
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT
from map_runtime import encode, publish_map_package
from shared_constants import package_documents
from shared_trainers import read_trainers
from PIL import Image

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PACKAGE = 'maps/regions/cycling-road'
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_CeladonCity': 'secondary/celadon_city',
            'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding2': 'secondary/generic_building_2'}
MAPS = (('Route17', 'map.route-17'), ('Route16_NorthEntrance_2F', 'map.route-16-north-entrance-2f'))
ROUTES = ('Route16', 'Route17', 'Route18')


def require(value, message):
    if not value:
        raise ValueError(message)


def safe(path):
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'Symlink path rejected: ' + str(path))
    return path


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


class RawFiles(dict):
    def __init__(self, root):
        super().__init__()
        self.root = root

    def __missing__(self, relative):
        path = Path(relative)
        require(not path.is_absolute() and '..' not in path.parts and '\\' not in relative, 'Unsafe raw path')
        value = safe(self.root / relative).read_bytes()
        self[relative] = value
        return value


class Source:
    def __init__(self, raw):
        self.data = RawFiles(raw)

    def text(self, path):
        return self.data[path].decode('utf-8')

    def json(self, path):
        return json.loads(self.data[path])

    def units(self, path, code):
        data = self.data[path]
        require(len(data) % struct.calcsize(code) == 0, 'Misaligned source binary: ' + path)
        return [value for value, in struct.iter_unpack('<' + code, data)]

    def ref(self, path, symbol, line=None):
        result = dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path, symbol=symbol)
        if line is not None:
            result['line'] = line
        return result


def trainer_battles(src, generated):
    """Select initial singles by actual map actors, never rematches or the Route16 pair."""
    path = 'data/scripts/trainers.inc'
    text = src.text(path)
    shared = read_trainers(generated)
    trainers, battles = [], []
    for route in ROUTES:
        for actor in src.json(f'data/maps/{route}/map.json')['object_events']:
            if actor['trainer_type'] != 'TRAINER_TYPE_NORMAL':
                continue
            label = actor['script']
            match = re.search(r'^' + re.escape(label) + r'::\n(.*?)(?=^\w+::|\Z)', text, re.M | re.S)
            require(match is not None, 'Missing trainer script: ' + label)
            body = match[1]
            if re.search(r'^\s*trainerbattle_double\b', body, re.M):
                continue
            single = re.search(r'^\s*trainerbattle_single (TRAINER_\w+), (\w+), (\w+)\s*$', body, re.M)
            post = re.search(r'^\s*msgbox (\w+), MSGBOX_AUTOCLOSE\s*$', body, re.M)
            require(single is not None and post is not None, 'Unsupported initial trainer script: ' + label)
            symbol, intro, defeat = single.groups()
            identity = 'pret:' + symbol
            require(identity in shared, 'Missing shared trainer: ' + identity)
            require(identity not in trainers, 'Duplicate initial trainer: ' + identity)
            trainers.append(identity)
            # These spans identify executable battle source bindings, not asset proofs.
            source = dict(path=path, symbol=label, startLine=text.count('\n', 0, match.start()) + 1,
                          endLine=text.count('\n', 0, match.start(1) + len(body.rstrip())) + 1)
            battles.append(dict(trainer=dict(value=identity), scriptEntry=label, mode=0,
                                introText=intro, defeatText=defeat, victoryText='', postBattleText=post[1],
                                battleTypeFlags=8, entryScript='EventScript_TryDoNormalTrainerBattle',
                                callback='CB2_EndTrainerBattle', rivalFlags=0, musicOption=None,
                                postBattleScript=None, source=source))
    require(bool(battles), 'No Cycling Road trainer battles found')
    return dict(trainers=trainers, battles=battles)


def hidden_constants(src, events):
    definitions = dict(re.findall(r'^#define\s+(\w+)\s+([^\n]+)', src.text('include/constants/flags.h'), re.M))
    resolved = {}

    def resolve(name, active=()):
        require(name not in active and name in definitions, 'Unresolved hidden-item flag: ' + name)
        if name in resolved:
            return resolved[name]
        expression = definitions[name].split('//')[0].strip()

        def evaluate(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name):
                return resolve(node.id, (*active, name))
            if isinstance(node, ast.BinOp) and isinstance(node.op, ast.Add):
                return evaluate(node.left) + evaluate(node.right)
            raise ValueError('Unsupported hidden-item flag expression: ' + expression)

        resolved[name] = evaluate(ast.parse(expression, mode='eval').body)
        return resolved[name]

    return {event['flag']: resolve(event['flag']) for event in events['bg_events'] if event['type'] == 'hidden_item'}


def land_encounters(src):
    wild = src.json('src/data/wild_encounters.json')
    tables = []
    for number in (17, 18):
        selected = [(group, row) for group in wild['wild_encounter_groups']
                    if group.get('for_maps') is True
                    for row in group.get('encounters', [])
                    if row.get('map') == f'MAP_ROUTE{number}'
                    and row.get('base_label') == f'sRoute{number}_FireRed']
        require(len(selected) == 1, f'Expected one Route{number} FireRed encounter table')
        group, row = selected[0]
        fields = [field for field in group['fields'] if field['type'] == 'land_mons']
        require(len(fields) == 1, 'Expected one land encounter weight table')
        weights = fields[0]['encounter_rates']
        land = row['land_mons']
        require(len(weights) == 12 and len(land['mons']) == len(weights), 'Invalid land encounter slot count')
        require(all(type(weight) is int and weight > 0 for weight in weights)
                and sum(weights) == 100, 'Invalid land encounter weights')
        rate = land['encounter_rate']
        require(type(rate) is int and 0 < rate <= 255, 'Invalid land encounter rate')
        slots = []
        for index, (mon, weight) in enumerate(zip(land['mons'], weights)):
            symbol, low, high = mon['species'], mon['min_level'], mon['max_level']
            require(re.fullmatch(r'SPECIES_[A-Z0-9_]+', symbol), 'Invalid encounter species: ' + symbol)
            require(type(low) is int and type(high) is int and 1 <= low <= high <= 100,
                    'Invalid encounter level range')
            slots.append(dict(index=index, species=dict(value='pret:' + symbol),
                              minimumLevel=low, maximumLevel=high, weight=weight))
        tables.append(dict(map=dict(value=f'map.route-{number}'), rawEncounterRate=rate, slots=slots))
    return tables


def convert(root, raw, check=False):
    generated = root / 'content/generated'
    output = safe(generated / PACKAGE)
    src = Source(raw)
    events = src.json('data/maps/Route17/map.json')
    # Missing story inputs fail before publishing a room with inert actors.
    map_events = {name: src.json(f'data/maps/{name}/map.json') for name, _ in MAPS}
    for map_name, _ in MAPS:
        for name in ('scripts.inc', 'text.inc'):
            src.text(f'data/maps/{map_name}/' + name)
    renderer = module(PROJECT_ROOT / 'tools/import-regional-service-maps/_regional_service_shared.py', 'cycling_map_renderer')
    renderer.TILESETS = TILESETS
    renderer.PROFILE = 'firered-cycling-road-v1'
    documents = {
        'catalog.json': dict(schemaVersion=1, maps=[dict(stableId=identity, sourceName=name,
            sourceMapSymbol=map_events[name]['id'], mapPath=identity.removeprefix('map.') + '.json') for name, identity in MAPS],
            mapSymbols={**{f'MAP_ROUTE{number}': f'map.route-{number}' for number in (16, 17, 18)},
                        **{map_events[name]['id']: identity for name, identity in MAPS},
                        'MAP_ROUTE16_NORTH_ENTRANCE_1F': 'map.route-16-north-entrance-1f'}, tilesets=TILESETS),
        'npc-source.json': dict(schemaVersion=1, dynamicGraphics=[], graphics=[dict(graphicsId=identity)
            for identity in sorted({actor['graphics_id'] for data in map_events.values() for actor in data['object_events']} | {'OBJ_EVENT_GFX_RED_NORMAL'})]),
        'trainers.json': trainer_battles(src, generated),
        'land-encounters.json': land_encounters(src),
    }
    with tempfile.TemporaryDirectory(prefix='firered-cycling-road-') as temporary:
        stage = Path(temporary)
        for name, identity in MAPS:
            renderer.import_map(src, name, identity, stage / identity.removeprefix('map.'))
        for name, value in documents.items():
            (stage / name).write_bytes(encode(value))
        publish_map_package(stage)
        if check:
            atlas = None
            for path in sorted(stage.iterdir()):
                destination = safe(output / path.name)
                if path.suffix == '.png':
                    with Image.open(path) as expected_image:
                        expected = expected_image.convert('RGBA')
                    if destination.is_file():
                        with Image.open(destination) as actual_image:
                            actual = actual_image.convert('RGBA')
                    else:
                        if atlas is None:
                            atlas_module = module(PROJECT_ROOT / 'scripts/imports/maps/import-map-atlases.py', 'cycling_atlas_reader')
                            atlas = atlas_module.AtlasReader(generated / 'maps/atlases')
                        actual = atlas.read(PACKAGE + '/' + path.name)
                    require(actual.size == expected.size and actual.tobytes() == expected.tobytes(), 'Map RGBA differs: ' + path.name)
                else:
                    require(destination.is_file() and destination.read_bytes() == path.read_bytes(), 'Regeneration differs: ' + str(destination))
        else:
            output.mkdir(parents=True, exist_ok=True)
            for path in sorted(stage.iterdir()):
                shutil.copyfile(path, safe(output / path.name))
        package_documents(generated, hidden_constants(src, events), {}, check=check)
    return dict(checked=check, maps=len(MAPS), trainerBattles=len(documents['trainers.json']['battles']),
                landEncounterTables=len(documents['land-encounters.json']),
                signs=sum(event['type'] == 'sign' for event in events['bg_events']),
                hiddenItems=sum(event['type'] == 'hidden_item' for event in events['bg_events']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--raw', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    root = args.root.resolve()
    raw = (args.raw or root / 'content/raw/pokefirered/037335f4').resolve()
    print(json.dumps(convert(root, raw, args.check), sort_keys=True))


if __name__ == '__main__':
    main()
