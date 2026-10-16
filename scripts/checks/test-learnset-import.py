#!/usr/bin/env python3
"""Runtime Pokemon JSON and ordered learnsets regenerated from retained local raw inputs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import json
from pathlib import Path
import re
import shutil
import tempfile
import unittest

import import_canonical_pokemon as canonical
import pokemon_presentation as presentation
from pokemon_import_io import load_recipe, read_sources

ROOT = _PROJECT_ROOT
RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'


def module(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'scripts' / (name + '.py'))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def source_learnsets():
    """Separate line-oriented reader; does not use the production parser or row provenance."""
    pointers = dict(re.findall(r'^    \[(SPECIES_\w+)\] = (\w+),$',
        (RAW / canonical.POINTERS).read_text(), re.M))
    arrays, current, active, branch, terminated = {}, None, True, None, False
    for line in (RAW / canonical.LEARNSETS).read_text().splitlines():
        if line == '#if defined(FIRERED)':
            assert branch is None
            active, branch = True, 'FR'
            continue
        if line == '#elif defined(LEAFGREEN)':
            assert branch == 'FR'
            active, branch = False, 'LG'
            continue
        if line == '#endif':
            assert branch == 'LG'
            active, branch = True, None
            continue
        if not active:
            continue
        declaration = re.fullmatch(r'static const u16 (\w+)\[\] = \{', line)
        if declaration:
            current, terminated = declaration[1], False
            assert current not in arrays
            arrays[current] = []
        elif current is not None:
            row = re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', line)
            if row:
                assert not terminated
                arrays[current].append(dict(level=int(row[1]), move=dict(value='pret:' + row[2])))
            elif line.strip() == 'LEVEL_UP_END':
                assert not terminated
                terminated = True
            elif line == '};':
                assert terminated and arrays[current]
                current = None
            else:
                assert not line.strip(), line
    assert current is None and branch is None
    return {symbol: arrays[pointer] for symbol, pointer in pointers.items()}


class LearnsetImportTests(unittest.TestCase):
    def test_canonical_and_regional_natural_learnsets_match_ordered_raw_rows(self):
        expected = source_learnsets()
        rows, documents, species_count = 0, 0, 0
        def check(value):
            nonlocal rows, species_count
            if isinstance(value, dict):
                if 'levelUpMoves' in value and 'id' in value:
                    self.assertEqual(GENERATED / 'data/pokemon/canonical/catalog.json', path,
                                     'Regional catalogs must use species references')
                    species_count += 1
                    symbol = value['id']['value'][5:]
                    self.assertEqual(expected[symbol], value['levelUpMoves'], symbol)
                    rows += len(value['levelUpMoves'])
                if 'naturalLearnsets' in value:
                    for symbol, learnset in value['naturalLearnsets'].items():
                        self.assertEqual(expected[symbol], learnset, symbol)
                        rows += len(learnset)
                for child in value.values():
                    check(child)
            elif isinstance(value, list):
                for child in value:
                    check(child)
        for path in sorted(GENERATED.rglob('*.json')):
            before = rows
            check(json.loads(path.read_bytes()))
            documents += rows > before
        self.assertEqual(386, species_count)
        self.assertGreaterEqual(rows, 4011)
        self.assertGreaterEqual(documents, 1)

    def test_canonical_catalog_keeps_only_typed_business_fields(self):
        document = json.loads((GENERATED / 'data/pokemon/canonical/catalog.json').read_bytes())
        self.assertEqual({'schemaVersion', 'species', 'metadata', 'abilities', 'generationContract'}, set(document))
        self.assertEqual({'maximumHpOneSpecies', 'formStatOverrides'}, set(document['generationContract']))
        for form in document['generationContract']['formStatOverrides']:
            self.assertEqual({'species', 'scope', 'baseStats'}, set(form))
        self.assertEqual(76, len(document['abilities']))
        for ability in document['abilities']:
            self.assertEqual({'symbol', 'sourceAbilityId'}, set(ability))
        source = (RAW / canonical.SPECIES_INFO).read_text()
        fields_by_symbol = {symbol: dict(re.findall(r'^        \.(\w+) = (.*),$', body, re.M))
            for symbol, body in re.findall(r'^    \[(SPECIES_\w+)\] =\n    \{\n(.*?)^    \},', source, re.M | re.S)}
        self.assertEqual(386, len(document['metadata']))
        for row in document['metadata']:
            fields = fields_by_symbol[row['id']['value'][5:]]
            self.assertNotIn('originalFields', row)
            self.assertNotIn('dexSource', row)
            self.assertNotIn('learnsetPointerSource', row)
            self.assertEqual([value.strip() for value in fields['eggGroups'].strip('{}').split(',')], row['eggGroups'])
            self.assertEqual(int(fields['eggCycles']), row['eggCycles'])
            self.assertIs(type(row['eggCycles']), int)
            self.assertEqual(int(fields['safariZoneFleeRate']), row['safariZoneFleeRate'])
            self.assertIs(type(row['safariZoneFleeRate']), int)
            for name in ('itemCommon', 'itemRare', 'bodyColor'):
                self.assertEqual(fields[name], row[name])
            self.assertEqual(fields['noFlip'] == 'TRUE', row['noFlip'])
            self.assertIs(type(row['noFlip']), bool)
            for edge in row['sourceEvolutions']:
                self.assertEqual({'method', 'sourceMethodId', 'parameter', 'parameterValue', 'target'}, set(edge))
        for species in document['species']:
            self.assertNotIn('source', species)
            self.assertNotIn('learnsetSource', species)

    def test_canonical_rebuild_matches_all_fields_and_is_repeatable(self):
        expected = json.loads((GENERATED / 'data/pokemon/canonical/catalog.json').read_bytes())
        for _ in range(2):
            actual = canonical.extract(*canonical.authenticate(ROOT, None, ROOT))
            self.assertEqual(expected, actual)
            self.assertEqual(4011, sum(len(s['levelUpMoves']) for s in actual['species']))
        self.assertEqual(184, sum(len(m['sourceEvolutions']) for m in actual['metadata']))

    def test_starter_route1_and_regional_regeneration_matches_all_fields(self):
        starter = module('imports/pokemon/import-pokemon')
        lock = load_recipe(ROOT / 'scripts/recipes/source/fetch-pokemon.lock.json', commit=canonical.COMMIT)
        actual = starter.convert(dict(lock, profile='firered-starter-instances-v1'), read_sources(lock, RAW))
        self.assertEqual((GENERATED / 'data/pokemon/starters.json').read_bytes(),
                         actual['starters.json'])
        with tempfile.TemporaryDirectory() as temporary:
            out = Path(temporary)
            module('imports/pokemon/import-route1-pokemon').import_catalog(RAW, out)
            self.assertEqual((GENERATED / 'data/pokemon/route1/catalog.json').read_bytes(),
                             (out / 'catalog.json').read_bytes())
        for region in ('forest', 'rocktunnel'):
            with self.subTest(region=region):
                recipe = presentation.load_lock(region)
                actual = presentation.convert(region, recipe, read_sources(recipe, RAW))
                self.assertNotIn('catalog.json', actual)
                for name in ('battle-art.json', 'supplement.json'):
                    if name in actual:
                        self.assertEqual(json.loads((GENERATED / 'sprites/pokemon' / region / name).read_bytes()),
                                         json.loads(actual[name]))

    def test_late_encounters_use_admitted_canonical_learnsets(self):
        document = json.loads((GENERATED / 'data/pokemon/canonical/catalog.json').read_bytes())
        species = {row['id']['value']: row for row in document['species']}
        owned = {row['id']['value'] for row in json.loads((ROOT / 'data/game-pokemon.json').read_bytes())['owned']}
        raw_learnsets = source_learnsets()
        symbols = {'SPECIES_ENTEI', 'SPECIES_RAIKOU', 'SPECIES_SUICUNE', 'SPECIES_MEWTWO'}
        wild = json.loads((RAW / 'src/data/wild_encounters.json').read_bytes())
        cave_maps = {'MAP_CERULEAN_CAVE_1F', 'MAP_CERULEAN_CAVE_2F', 'MAP_CERULEAN_CAVE_B1F'}
        encounters = [row for group in wild['wild_encounter_groups'] for row in group['encounters']
                      if row['map'] in cave_maps and row['base_label'].endswith('_FireRed')]
        self.assertEqual(cave_maps, {row['map'] for row in encounters})
        for row in encounters:
            for table in row.values():
                if isinstance(table, dict) and 'mons' in table:
                    symbols.update(slot['species'] for slot in table['mons'])
        for symbol in symbols:
            with self.subTest(species=symbol):
                identity = 'pret:' + symbol
                self.assertIn(identity, owned)
                self.assertEqual(raw_learnsets[symbol], species[identity]['levelUpMoves'])
        for family in ('celio', 'cerulean-cave'):
            self.assertFalse((GENERATED / 'data/pokemon' / family / 'pokemon.json').exists())
            self.assertFalse((GENERATED / 'data/pokemon' / family / 'machines.json').exists())

    def test_power_plant_runtime_outputs_omit_unused_proof_inventories(self):
        importer = module('imports/pokemon/import-power-plant-pokemon')
        catalog, _, _ = importer.generate(None, ROOT, RAW)
        self.assertNotIn('inputs', catalog)
        self.assertNotIn('dependencyInputs', catalog)
        self.assertEqual(json.loads(importer.encoded(catalog)),
                         json.loads((GENERATED / 'data/pokemon/power-plant.json').read_bytes()))
        outputs, result = importer.build_power_plant_presentation(lambda path: (RAW / path).read_bytes(), ROOT)
        manifest = result['manifest']
        self.assertNotIn('inputs', manifest)
        self.assertNotIn('inheritedInputs', manifest)
        self.assertNotIn('cries', manifest['audio'])
        self.assertEqual('CRY_ZAPDOS_ENCOUNTER', manifest['audio']['encounter']['zapdos']['cry'])
        for path, data in outputs.items():
            if path.startswith('content/generated/') and path.endswith('.json'):
                self.assertEqual(json.loads(data), json.loads((ROOT / path).read_bytes()), path)

    def test_celadon_materializer_and_real_atlas_finalizer_match_runtime_documents(self):
        from item_icon_exports import ATLAS_PATH, pack_project
        from PIL import Image
        importer = module('imports/items/import-celadon-gym-catalog')
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary)
            # Preserve the existing atlas cohort while reimporting the two owned packages.
            for path in GENERATED.rglob('*.json'):
                destination = target / path.relative_to(ROOT)
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(path, destination)
            page = target / ATLAS_PATH
            page.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / ATLAS_PATH, page)
            for stem in ('celadon-gym-pokemon', 'celadon-gym-items'):
                recipe = json.loads((ROOT / ('scripts/recipes/' + ('pokemon' if stem.endswith('pokemon') else 'trainers' if stem.endswith('trainers') else 'items') + '/' + stem + '.lock.json')).read_bytes())
                for row in recipe['files']:
                    destination = target / 'content/raw/pokefirered/037335f4' / row['path']
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(RAW / row['path'], destination)
            importer.materialize(ROOT, target)
            result = pack_project(target)
            self.assertEqual(1, result['retiredPngFiles'])
            for name in ('celadon-gym-items.json', 'celadon-gym-machines.json', 'manifest.json'):
                path = Path('content/generated/data/items/celadon-gym') / name
                self.assertEqual(json.loads((ROOT / path).read_bytes()),
                                 json.loads((target / path).read_bytes()), str(path))
            with Image.open(ROOT / ATLAS_PATH) as original, Image.open(page) as rebuilt:
                self.assertEqual(original.size, rebuilt.size)
                self.assertEqual(original.convert('RGBA').tobytes(), rebuilt.convert('RGBA').tobytes())

    def test_item_atlas_rewrites_normalized_references_and_is_idempotent(self):
        from item_icon_exports import ATLAS_PATH, decode_icon_page, pack_project
        from PIL import Image
        with tempfile.TemporaryDirectory() as temporary:
            project = Path(temporary)
            package = project / 'content/generated/data/items'
            image = package / 'fixture/icon.asset'
            image.parent.mkdir(parents=True)
            # Image contents, not the filename suffix, determine valid input.
            Image.new('RGBA', (24, 24), (11, 22, 33, 128)).save(image, format='PNG')
            document = package / 'fixture.json'
            original = {
                'icon': {'item': {'value': 'pret:ITEM_POTION'}, 'width': 24, 'height': 24,
                         'pngPath': 'fixture//./icon.asset'},
                'references': ['fixture/icon.asset', str(image),
                               'content/generated/data/items/fixture/./icon.asset'],
                'aliases': {'relative': 'fixture//./icon.asset', 'absolute': str(image),
                            'rooted': 'content/generated/data/items/fixture/./icon.asset',
                            'unrelated': 'Text mentioning icon.asset is not a path'},
                'outputs': [{'path': 'fixture/icon.asset'}, {'path': str(image)}]}
            document.write_text(json.dumps(original))
            result = pack_project(project)
            self.assertEqual(1, result['retiredPngFiles'])
            rebuilt = json.loads(document.read_text())
            self.assertEqual(ATLAS_PATH, rebuilt['icon']['pngPath'])
            self.assertEqual({'relative': ATLAS_PATH, 'absolute': ATLAS_PATH, 'rooted': ATLAS_PATH,
                              'unrelated': original['aliases']['unrelated']}, rebuilt['aliases'])
            self.assertEqual([{'path': ATLAS_PATH}], rebuilt['outputs'])
            # Non-record string arrays have never been reference records.
            self.assertEqual(original['references'], rebuilt['references'])
            png = (project / ATLAS_PATH).read_bytes()
            decoded = decode_icon_page(png, rebuilt['icon'])
            self.assertEqual(Image.new('RGBA', (24, 24), (11, 22, 33, 128)).tobytes(), decoded.tobytes())
            self.assertFalse(image.exists())
            again = pack_project(project)
            self.assertEqual(0, again['retiredPngFiles'])
            self.assertEqual(0, again['updatedDocuments'])
            self.assertEqual(png, (project / ATLAS_PATH).read_bytes())
            self.assertEqual(rebuilt, json.loads(document.read_text()))

    def test_shared_and_late_json_writers_are_compact_and_lossless(self):
        from pokemon_import_io import canonical as encode
        value = {'text': 'Pokémon \n text', 'array': [3, 1, {'nested': True}], 'null': None}
        for writer in [encode, *(module('imports/pokemon/import-' + name + '-pokemon').encoded
                                 for name in ('power-plant',))]:
            actual = writer(value)
            self.assertEqual(value, json.loads(actual))
            self.assertEqual(actual, writer(value))
            self.assertEqual(actual, (json.dumps(json.loads(actual), ensure_ascii=False,
                             separators=(',', ':')) + '\n').encode())

    def test_invalid_canonical_learnsets_still_fail(self):
        for old, new in [('LEVEL_UP_MOVE(1, MOVE_TACKLE)', 'LEVEL_UP_MOVE(0, MOVE_TACKLE)'),
                         ('LEVEL_UP_MOVE(1, MOVE_TACKLE)', 'LEVEL_UP_MOVE(1, MOVE_UNKNOWN)'),
                         ('    LEVEL_UP_END\n', ''),
                         ('LEVEL_UP_MOVE(4, MOVE_GROWL)', 'LEVEL_UP_MOVE(2, MOVE_GROWL)')]:
            with self.subTest(new=new):
                lock, source, moves = canonical.authenticate(ROOT, None, ROOT)
                if 'MOVE_GROWL' in old:
                    # Put Growl after the next level-seven move, breaking source order.
                    old = '    LEVEL_UP_MOVE(4, MOVE_GROWL),\n    LEVEL_UP_MOVE(7, MOVE_LEECH_SEED),'
                    new = '    LEVEL_UP_MOVE(7, MOVE_LEECH_SEED),\n    LEVEL_UP_MOVE(4, MOVE_GROWL),'
                source.text[canonical.LEARNSETS] = source.text[canonical.LEARNSETS].replace(old, new, 1)
                with self.assertRaises(ValueError):
                    canonical.extract(lock, source, moves)


if __name__ == '__main__':
    unittest.main()
