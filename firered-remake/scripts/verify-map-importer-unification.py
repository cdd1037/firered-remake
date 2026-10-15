#!/usr/bin/env python3
"""Bounded local checks for v2 map generation; never runs a full resource import."""
import argparse
import ast
import base64
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import tempfile

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'scripts'))
sys.path.insert(0, str(ROOT / 'tools/import-regional-service-maps'))
from map_runtime import COMMIT, VERSION, encode, normalize_runtime
from runtime_map_manifest import compose
import import_regional_service_maps as regional


def module(relative):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(path.stem, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def need(value, message):
    if not value:
        raise ValueError(message)


class LocalBytes(dict):
    def __missing__(self, path):
        value = (ROOT / 'content/raw/pokefirered/037335f4' / path).read_bytes()
        self[path] = value
        return value


class Source:
    def __init__(self): self.data = LocalBytes()
    def read(self, path): return self.data[path]
    def text(self, path): return self.read(path).decode()
    def json(self, path): return json.loads(self.read(path))
    def units(self, path, code):
        data = self.read(path)
        return list(struct.unpack('<' + str(len(data) // struct.calcsize(code)) + code, data))
    def ref(self, path, symbol, line=None):
        return dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path, symbol=symbol)
    def scripts(self, name):
        for leaf in ('scripts.inc', 'text.inc'):
            path = f'data/maps/{name}/{leaf}'
            if (ROOT / 'content/raw/pokefirered/037335f4' / path).is_file(): self.read(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    cases = []
    with tempfile.TemporaryDirectory(prefix='map-importer-v2-check-') as temporary:
        temporary = Path(temporary)
        shared = regional.SHARED
        src = shared.Source(ROOT / 'content/raw/pokefirered/037335f4')
        directory = 'celadon-city-game-corner-prize-room'; out = temporary / directory
        world = shared.import_map(src, 'CeladonCity_GameCorner_PrizeRoom', 'map.' + directory, out)
        data = (out / 'runtime-map.json').read_bytes(); doc = json.loads(data)
        original = normalize_runtime(json.loads((ROOT / 'content/generated/regional-service-maps' / directory / 'runtime-map.json').read_bytes()))
        need(doc == original and doc['events'] == world['events'], 'Ordinary room reconstruction differs')
        need(not (out / 'world.json').exists() and not {'cells', 'borderCells', 'embeddedSource', 'scripts'} & doc.keys(), 'Retained rich map data')
        cases.append(dict(case='shared-prize-room-render', status='passed', outputFiles=len(list(out.iterdir())), runtimeBytes=len(data), sourceEqual=True))

        tower = module('scripts/import-trainer-tower-region.py')
        seafoam = module('scripts/import-seafoam-resources.py')
        reader = module('scripts/import-cinnabar-source-preparation.py')
        src = Source(); name = 'TrainerTower_1F'; events = src.json(f'data/maps/{name}/map.json'); src.scripts(name)
        layout = next(v for v in src.json('data/layouts/layouts.json')['layouts'] if v.get('id') == 'LAYOUT_TRAINER_TOWER_1F_DOUBLES')
        shared.TILESETS = tower.TILESETS
        world = shared.import_map(tower.LayoutSource(src, name, layout), name, 'map.trainer-tower-1f', temporary / 'tower')
        world['events'] = events; variant = tower.compact(world, src)
        need(variant['events'] == events and variant['schemaVersion'] == 2 and 'scripts' not in variant, 'Tower header/format differs')
        need(base64.b64decode(variant['blocks']) == src.read(layout['blockdata_filepath']), 'Tower selected layout differs')
        cases.append(dict(case='tower-doubles-layout', status='passed', originalEventsPreserved=True))
        name = 'SeafoamIslands_B3F'; src.scripts(name)
        shared.TILESETS = reader.TILESETS = seafoam.TILESETS
        base = reader.decode(src, name)
        world, layout_variant = seafoam.alternate_world(src, reader, shared, base, temporary / 'seafoam')
        variant = json.loads((temporary / 'seafoam/runtime-map.json').read_bytes())
        need(variant['events'] == base['events'] and variant['schemaVersion'] == 2, 'Seafoam header/format differs')
        need(base64.b64decode(variant['blocks']) == src.read(world['layout']['blockdata_filepath']), 'Seafoam selected layout differs')
        need(not {'cells', 'embeddedSource', 'scripts'} & variant.keys(), 'Seafoam retained rich data')
        cases.append(dict(case='seafoam-stopped-current-layout', status='passed', originalEventsPreserved=True))

        borders = module('scripts/import-map-borders.py'); borders.pallet.MAPS = borders.pallet.MAPS[:1]
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        with contextlib.redirect_stdout(io.StringIO()):
            borders.generate(raw, raw, ROOT / 'scripts/map-borders-source.lock.json', temporary / 'borders')
        border = json.loads((temporary / 'borders/pallet/border.json').read_bytes())
        need(set(border) == {'schemaVersion', 'stableId', 'mapPath', 'sourceRef', 'ruleSourceRef', 'repeatRule', 'artifacts'}, 'Border stores duplicate geometry')
        need(border['schemaVersion'] == 2 and border['mapPath'] == 'pallet/runtime-map.json', 'Border map reference differs')
        for artifact in border['artifacts']:
            need((temporary / 'borders/pallet' / artifact['path']).read_bytes() == (ROOT / 'content/generated/map-borders/pallet' / artifact['path']).read_bytes(), 'Border atlas pixels changed')
        cases.append(dict(case='pallet-border-reference', status='passed', unchangedAtlasCount=len(border['artifacts'])))

        generated = temporary / 'generated'; generated.mkdir()
        registry = json.loads((ROOT / 'content/generated/map-source-registry.json').read_bytes())
        borders.pallet.write_source_registry(generated, registry['metadata'])
        need(json.loads((generated / 'map-source-registry.json').read_bytes()) == registry, 'Registry reconstruction differs')
        for number in (1, 2):
            directory = generated / f'region/map-{number}'; directory.mkdir(parents=True)
            payload = dict(doc, stableId=f'map.test-{number}')
            (directory / 'runtime-map.json').write_bytes(encode(payload))
        original_maps = {p: p.read_bytes() for p in generated.rglob('runtime-map.json')}
        first, second = generated / 'collection-one', generated / 'collection-two'
        compose(generated, first, ['region/map-1'], expected_count=1)
        stale = second / 'region/map-1'; stale.mkdir(parents=True); (stale / 'runtime-map.json').write_text('obsolete')
        compose(generated, second, ['region/map-2'], previous=first, expected_count=2)
        compose(generated, second, ['region/map-2'], previous=first, expected_count=2, check=True)
        manifest = json.loads((second / 'manifest.json').read_bytes())
        need(manifest['sourceRegistry'] == {'path': 'map-source-registry.json'} and all(set(e) == {'stableId', 'path'} for e in manifest['maps']), 'Manifest contains copied metadata')
        need(not list(second.rglob('runtime-map.json')) and all(p.read_bytes() == b for p, b in original_maps.items()), 'Canonical map duplicated or modified')
        bad = generated / 'region/map-2/runtime-map.json'; legacy = json.loads(bad.read_bytes()); legacy['schemaVersion'] = 1; bad.write_bytes(encode(legacy))
        try: compose(generated, second, ['region/map-2'], previous=first)
        except ValueError: pass
        else: raise ValueError('Legacy runtime schema accepted')
        cases.append(dict(case='reference-only-composition-and-registry', status='passed', maps=2, obsoleteCopiesRemoved=True, canonicalMapsUnchanged=True, schema1Rejected=True))
    fly = module('scripts/import-route16-fly-house.py')
    source = Source(); retained, anchors = fly.retained_maps(source, ROOT)
    house = dict(stableId='map.route-16-house', events=source.json('data/maps/Route16_House/map.json'))
    contract = fly.north_contract(source, retained, house, anchors)
    existing = json.loads((ROOT / 'content/generated/route16-fly-house/contracts.json').read_bytes())
    for key in ('upperFootAdmissions', 'allowedWarps', 'topology', 'cutTree', 'presetDouble', 'excludedWarps'):
        need(contract[key] == existing[key], 'Retained Route16 contract differs: ' + key)
    for world, anchor in zip(retained, anchors, strict=True):
        compact = json.loads((ROOT / anchor['resourcePath']).read_bytes())
        need('cells' not in compact and len(world['cells']) == world['width'] * world['height'], 'Runtime cell inspection altered stored format')
    cases.append(dict(case='retained-route16-north-contract', status='passed',
                      anchorMaps=len(retained), inspectedCells=sum(len(w['cells']) for w in retained),
                      upperFootCounts=[row['cellCount'] for row in contract['upperFootAdmissions']],
                      sourceContractUnchanged=True, storedMapsRemainCompact=True))
    files = [p for folder in ('scripts', 'tools') for p in (ROOT / folder).rglob('*.py')]
    for path in files: ast.parse(path.read_text(), filename=str(path))
    cases.append(dict(case='python-syntax', status='passed', scripts=len(files)))
    report = dict(status='passed', converterVersion=VERSION, command='python3 scripts/verify-map-importer-unification.py --report evidence/map-importer-unification.json',
                  cases=cases, fullResourceImportRun=False, canonicalGeneratedAssetsModified=False,
                  correctedInitialChecks=['Prize-room temporary output must be a new child directory.', 'Border rule inventories compare source paths, because removed hash/length fields may remain in older raw metadata.'])
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True); args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, sort_keys=True))


if __name__ == '__main__': main()
