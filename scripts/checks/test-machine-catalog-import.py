#!/usr/bin/env python3
"""Exact source semantics, regional parity and negative cases for shared machines."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import copy
import importlib.util
import json
from pathlib import Path
import re
import tempfile
import unittest

ROOT = _PROJECT_ROOT
SPEC = importlib.util.spec_from_file_location('machine_catalog_import', ROOT / 'scripts/imports/items/import-machine-catalog.py')
IMPORT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(IMPORT)


def source_learnsets():
    """Independent line-oriented source reader; provenance stays in this test."""
    source = (ROOT / IMPORT.RAW_PATH / 'src/data/pokemon/tmhm_learnsets.h').read_text()
    entries = {}
    symbol = None
    for number, line in enumerate(source.splitlines(), 1):
        entry = re.match(r'\s*\[(SPECIES_\w+)\]\s*=', line)
        if entry:
            symbol = entry[1]
            if symbol in entries:
                raise AssertionError('Duplicate source learnset: ' + symbol)
            entries[symbol] = dict(items=set(), start=number, end=number)
        if symbol is not None:
            entries[symbol]['items'].update(re.findall(r'TMHM\(([TH]M\d\d)_', line))
            entries[symbol]['end'] = number
            if '),' in line:
                symbol = None
    return entries


class SharedMachineCatalogImport(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = IMPORT.read_json(ROOT / IMPORT.CATALOG_PATH)
        cls.catalog = IMPORT.Catalog(cls.document)
        cls.results = IMPORT.process_documents(ROOT, cls.catalog)

    def test_rebuild_is_deterministic_and_all_regional_files_are_compact(self):
        result = IMPORT.run(ROOT, check=True)
        self.assertEqual({'schemaVersion', 'machines', 'compatibility', 'current'}, set(self.document))
        self.assertEqual(57, result['sharedMachineDefinitions'])
        self.assertEqual(386, result['species'])
        self.assertEqual(22388, result['compatibilityValues'])

    def test_regional_reference_roundtrip_preserves_order(self):
        self.assertTrue(self.results, "No regional machine views found")
        for path, compact, expected_compact, expanded, _ in self.results:
            self.assertEqual(compact, expected_compact, path)
            self.assertEqual(compact, IMPORT.transform(expanded, self.catalog, path))

    def test_machine_item_values_match_independent_raw_items(self):
        raw = ROOT / IMPORT.RAW_PATH
        items = {row['itemId']: row for row in json.loads((raw / 'src/data/items.json').read_text())['items']}
        descriptions = (raw / 'src/move_descriptions.c').read_text()
        battle_moves = (raw / 'src/data/battle_moves.h').read_text()
        fields = {'name': 'english', 'price': 'price', 'importance': 'importance',
                  'registrability': 'registrability', 'pocket': 'pocket',
                  'holdEffect': 'holdEffect', 'holdEffectParam': 'holdEffectParam',
                  'fieldUseFunction': 'fieldUseFunc', 'battleUsage': 'battleUsage',
                  'battleUseFunction': 'battleUseFunc', 'secondaryId': 'secondaryId'}
        self.assertEqual(57, len(self.catalog.machines))
        for key, row in self.catalog.machines.items():
            symbol = key[5:]
            item = items[symbol]
            actual = row['machine']['item']
            for target, origin in fields.items():
                self.assertEqual(item[origin], actual[target], (symbol, target))
            self.assertEqual(str(item['type']), actual['type'])
            self.assertEqual(item['importance'] == 0, actual['canToss'])
            self.assertEqual(item['importance'] == 0, actual['canGive'])
            self.assertEqual(item['price'] != 0, actual['canSell'])
            if symbol in ('ITEM_HM06', 'ITEM_HM07', 'ITEM_TM36'):
                description = item['description_english'].replace('\\n', '\n')
            else:
                match = re.search(r'gMoveDescription_' + item['moveId'] + r'\[\] = _\(("(?:[^"\\]|\\.)*")\)', descriptions)
                self.assertIsNotNone(match, symbol)
                description = json.loads(match[1])
            self.assertEqual(description, row['machine']['description'], symbol)
            move = row['move']['value'][5:]
            block = re.search(r'\[' + move + r'\]\s*=\s*\{(.*?)\}', battle_moves, re.S)
            self.assertIsNotNone(block, move)
            self.assertEqual(int(re.search(r'\.pp\s*=\s*(\d+)', block[1])[1]), row['movePp'])

    def test_compatibility_schema_rejects_retired_source_proof(self):
        for row in self.document['compatibility']:
            self.assertEqual({'species', 'sourceSpeciesId', 'compatibleItems'}, set(row))
            self.assertEqual({'species', 'sourceSpeciesId', 'canLearn'},
                             set(self.catalog.compatibility('pret:ITEM_TM39', row['species']['value'])))
        bad = copy.deepcopy(self.document)
        bad['compatibility'][0]['source'] = dict(path='retired-proof.h')
        with self.assertRaisesRegex(ValueError, 'Unexpected compatibility fields'):
            IMPORT.Catalog(bad)
        cell = self.catalog.compatibility('pret:ITEM_TM39', 'pret:SPECIES_BULBASAUR')
        cell['source'] = dict(path='retired-proof.h')
        with self.assertRaisesRegex(ValueError, 'Unexpected compatibility fields'):
            IMPORT.transform(cell, self.catalog, 'fixture.json', inherited='pret:ITEM_TM39')

    def test_all_386_by_58_cells_match_source_tokens_and_source_species_ids(self):
        raw = ROOT / IMPORT.RAW_PATH
        numbers = {m[1]: int(m[2]) for m in re.finditer(
            r'^#define (SPECIES_\w+)\s+(\d+)\b',
            (raw / 'include/constants/species.h').read_text(), re.M)}
        entries = source_learnsets()
        actual = 0
        for row in self.document['compatibility']:
            symbol = row['species']['value'][5:]
            self.assertEqual(numbers[symbol], row['sourceSpeciesId'])
            for machine in IMPORT.MACHINES:
                cell = self.catalog.compatibility('pret:ITEM_' + machine, 'pret:' + symbol)
                self.assertEqual(machine in entries[symbol]['items'], cell['canLearn'], (symbol, machine))
                actual += 1
        self.assertEqual(22388, actual)
        self.assertNotIn('pret:SPECIES_NONE', self.catalog.species)
        self.assertNotIn('pret:SPECIES_EGG', self.catalog.species)
        self.assertFalse(any('OLD_UNOWN' in key for key in self.catalog.species))

    def test_hm08_compatibility_does_not_admit_hm08_as_owned_definition(self):
        self.assertNotIn('pret:ITEM_HM08', self.catalog.machines)
        self.assertTrue(self.catalog.compatibility('pret:ITEM_HM08', 'pret:SPECIES_MEW')['canLearn'])
        self.assertFalse(self.catalog.compatibility('pret:ITEM_HM08', 'pret:SPECIES_EEVEE')['canLearn'])
        with self.assertRaisesRegex(ValueError, 'Unknown admitted machine'):
            self.catalog.machine('pret:ITEM_HM08', ['pret:SPECIES_MEW'])
        original_hm08 = []
        def visit(value):
            if isinstance(value, dict):
                if value.get('item') == 'pret:ITEM_HM08' and isinstance(value.get('species'), str):
                    original_hm08.append(value['species'])
                for child in value.values():
                    visit(child)
            elif isinstance(value, list):
                for child in value:
                    visit(child)
        for _, compact, _, _, _ in self.results:
            visit(compact)
        # HM08 eligibility remains in the shared compatibility table. No regional
        # learning view admits it as a teachable machine.
        self.assertEqual([], original_hm08)

    def test_generated_item_wrapper_schema_has_no_retired_proof_fields(self):
        count = 0
        def visit(value):
            nonlocal count
            if isinstance(value, list):
                for child in value:
                    visit(child)
            elif isinstance(value, dict):
                if 'description' in value and isinstance(value.get('item'), str) and set(value) == {'item', 'description'}:
                    count += 1
                    self.assertEqual({'item', 'description'}, set(value))
                for child in value.values():
                    visit(child)
        for path in (ROOT / 'content/generated').rglob('*.json'):
            visit(json.loads(path.read_text()))
        self.assertGreater(count, 0, "No generated item wrappers inspected")

    def test_displayed_descriptions_are_preserved_without_proof_fields(self):
        source = json.loads((ROOT / IMPORT.RAW_PATH / 'src/data/items.json').read_text())
        items = {row['itemId']: row for row in source['items']}
        for name in ('HM06', 'HM07', 'TM36'):
            definition = self.catalog.machines['pret:ITEM_' + name]['machine']
            self.assertEqual(items['ITEM_' + name]['description_english'].replace('\\n', '\n'),
                             definition['description'])
        for definition in self.catalog.machines.values():
            self.assertEqual({'item', 'description'}, set(definition['machine']))
        for field in ('descriptionSource', 'declarationDescription'):
            bad = copy.deepcopy(self.document)
            bad['machines'][0]['machine'][field] = 'retired proof'
            with self.assertRaisesRegex(ValueError, 'Unexpected machine item fields'):
                IMPORT.Catalog(bad)
        current = self.document['current']
        self.assertEqual('pret:ITEM_TM39', current['order'][0]['value'])
        self.assertEqual('pret:ITEM_TM_CASE', current['tmCase']['item'])
        self.assertEqual(57, len(current['order']))
        self.assertEqual(58, current['machineSlots'])
        pewter = IMPORT.read_json(ROOT / 'content/generated/data/machine-learning/pewter.json')
        self.assertNotIn('catalog', pewter)

    def test_conflicting_compatibility_is_rejected_instead_of_deduplicated(self):
        item, species = 'pret:ITEM_TM39', 'pret:SPECIES_BULBASAUR'
        good = self.catalog.compatibility(item, species)
        for mutate in ('canLearn', 'sourceSpeciesId'):
            bad = copy.deepcopy(good)
            if mutate == 'canLearn':
                bad[mutate] = not bad[mutate]
            else:
                bad[mutate] += 1
            with self.subTest(mutate=mutate), self.assertRaisesRegex(ValueError, 'Conflicting compatibility'):
                IMPORT.transform(bad, self.catalog, 'fixture.json', inherited=item)
        with self.assertRaisesRegex(ValueError, 'Missing item context'):
            IMPORT.transform(good, self.catalog, 'fixture.json')

    def test_conflicting_definition_and_unknown_references_fail_closed(self):
        machine = self.catalog.machine('pret:ITEM_TM39', ['pret:SPECIES_BULBASAUR'])
        machine['movePp'] += 1
        with self.assertRaisesRegex(ValueError, 'Conflicting machine'):
            IMPORT.transform(machine, self.catalog, 'fixture.json')
        for reference in (
            dict(item='pret:ITEM_TM00', species='pret:SPECIES_BULBASAUR'),
            dict(item='pret:ITEM_TM39', species='pret:SPECIES_MISSING'),
            dict(item='pret:ITEM_TM39', species=['pret:SPECIES_MISSING']),
        ):
            with self.subTest(reference=reference), self.assertRaises(ValueError):
                IMPORT.transform(reference, self.catalog, 'fixture.json')

    def test_invalid_source_tokens_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            raw = Path(temporary)
            for relative in IMPORT.SOURCE_PATHS:
                target = raw / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((ROOT / IMPORT.RAW_PATH / relative).read_bytes())
            path = raw / 'src/data/pokemon/tmhm_learnsets.h'
            original = path.read_text()
            for value in ('TMHM(TM99_MISSING)', 'UNKNOWN_MACRO'):
                path.write_text(original.replace('TMHM(TM06_TOXIC)', value, 1))
                with self.subTest(value=value), self.assertRaises(ValueError):
                    IMPORT.compile_catalog(raw, IMPORT.read_json(ROOT / IMPORT.RECIPE_PATH))


    def test_pipeline_reference_only_guard_rejects_legacy_expansion(self):
        IMPORT.require_direct_references(self.results)
        path, _, compact, expanded, counts = self.results[0]
        with self.assertRaisesRegex(ValueError, 'Importer emitted expanded machine data'):
            IMPORT.require_direct_references([(path, expanded, compact, expanded, counts)])

    def test_flat_admissions_match_all_target_views_without_predecessors(self):
        import sys
        sys.path.insert(0, str(ROOT / 'scripts/lib'))
        import machine_catalog
        recipe = IMPORT.read_json(ROOT / 'scripts/recipes/config/machine-admissions.json')
        self.assertEqual({path for path, *_ in self.results}, set(recipe['files']))
        count = 0
        for relative, groups in recipe['files'].items():
            document = IMPORT.read_json(ROOT / relative)
            for group in groups:
                for view in group['views']:
                    actual = document
                    for part in view['pointer'].split('/')[1:]:
                        actual = actual[int(part)] if isinstance(actual, list) else actual[part]
                    self.assertEqual(actual, machine_catalog.admission_view(ROOT, relative, view['pointer']))
                    count += 1
        self.assertGreater(count, 0)

    def test_duplicate_catalog_keys_and_duplicate_json_members_fail(self):
        bad = copy.deepcopy(self.document)
        bad['machines'].append(copy.deepcopy(bad['machines'][0]))
        with self.assertRaisesRegex(ValueError, 'Duplicate/missing machines'):
            IMPORT.Catalog(bad)
        with self.assertRaisesRegex(ValueError, 'Duplicate JSON member'):
            json.loads('{"item":1,"item":2}', object_pairs_hook=IMPORT.unique_pairs)


if __name__ == '__main__':
    unittest.main()
