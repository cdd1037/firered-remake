#!/usr/bin/env python3
"""Independent raw-value and generated-reference checks for item business ownership."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from contextlib import redirect_stdout
import importlib.util
from io import StringIO
import json
from pathlib import Path
import re
import shutil
import unittest
import tempfile
from item_catalog import ROOT, RAW, compile_catalog, described_items, item_definition, item_json


class ItemCatalogTests(unittest.TestCase):
    def test_all_real_items_match_source_business_values(self):
        raw = ROOT / RAW
        constants = {key: int(value) for key, value in re.findall(r'^#define (\w+)\s+(\d+)\b',
            (raw / 'include/constants/items.h').read_text(), re.M)}
        original = {row['itemId']: row for row in json.loads((raw / 'src/data/items.json').read_text())['items']
                    if row['itemId'] != 'ITEM_NONE'}
        document = compile_catalog(ROOT)
        self.assertEqual({'schemaVersion', 'items', 'descriptions'}, set(document))
        self.assertEqual(2, document['schemaVersion'])
        self.assertEqual(307, len(document['items']))
        self.assertEqual(set(original), {row['sourceItemSymbol'] for row in document['items']})
        fields = dict(name='english', price='price', importance='importance', registrability='registrability',
            pocket='pocket', holdEffect='holdEffect', holdEffectParam='holdEffectParam',
            fieldUseFunction='fieldUseFunc', battleUsage='battleUsage', battleUseFunction='battleUseFunc')
        for item in document['items']:
            symbol = item['sourceItemSymbol']; source = original[symbol]
            self.assertEqual('pret:' + symbol, item['id']['value'])
            self.assertEqual(constants[symbol], item['sourceItemId'])
            self.assertEqual(str(source['type']), item['type'])
            secondary = source['secondaryId']
            self.assertEqual(constants[secondary] if isinstance(secondary, str) else secondary, item['secondaryId'])
            for target, origin in fields.items():
                self.assertEqual(source[origin], item[target], (symbol, target))
            self.assertEqual(source['importance'] == 0, item['canGive'])
            self.assertEqual(source['importance'] == 0, item['canToss'])
            self.assertEqual(source['price'] != 0, item['canSell'])
            self.assertNotIn('source', item)
        self.assertEqual(document, json.loads((ROOT / 'content/generated/data/items.json').read_text()))

    def test_all_descriptions_match_source_and_current_inventory_bytes(self):
        raw = ROOT / RAW
        source = json.loads((raw / 'src/data/items.json').read_bytes())['items']
        moves = (raw / 'src/move_descriptions.c').read_text()
        document = compile_catalog(ROOT)
        descriptions = document['descriptions']
        self.assertEqual({item['id']['value'] for item in document['items']}, set(descriptions))
        for item in source:
            symbol = item['itemId']
            if symbol == 'ITEM_NONE':
                continue
            expected = item['description_english'].replace('\\n', '\n')
            if re.fullmatch(r'ITEM_(?:TM|HM)\d+', symbol):
                match = re.search(r'^const u8 gMoveDescription_' + re.escape(item['moveId']) +
                                  r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$', moves, re.M)
                self.assertIsNotNone(match, symbol)
                expected = json.loads(match[1])
            actual = descriptions['pret:' + symbol]
            self.assertTrue(actual.strip(), symbol)
            self.assertEqual(expected.encode('utf-8'), actual.encode('utf-8'), symbol)
        current = json.loads((ROOT / 'content/generated/data/available-items.json').read_bytes())
        self.assertTrue(current['items'])
        for item in current['items']:
            self.assertEqual(item['description'].encode('utf-8'),
                             descriptions[item['item']].encode('utf-8'), item['item'])

    def test_complete_descriptions_use_explicit_source_root_and_reject_empty_text(self):
        raw = ROOT / RAW
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for relative in ('include/constants/items.h', 'src/data/items.json', 'src/move_descriptions.c'):
                target = root / RAW / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(raw / relative, target)
            item_path = root / RAW / 'src/data/items.json'
            source = json.loads(item_path.read_bytes())
            potion = next(row for row in source['items'] if row['itemId'] == 'ITEM_POTION')
            potion['description_english'] = 'Independent item text.'
            item_path.write_text(json.dumps(source))
            move_path = root / RAW / 'src/move_descriptions.c'
            move_source = move_path.read_text()
            machine = next(row for row in source['items'] if row['itemId'] == 'ITEM_TM04')
            pattern = r'(^const u8 gMoveDescription_' + re.escape(machine['moveId']) + r'\[\] = _\()"(?:[^"\\]|\\.)*"(\);$)'
            move_path.write_text(re.sub(pattern, r'\1"Independent machine text."\2', move_source, flags=re.M))
            document = compile_catalog(root)
            self.assertEqual('Independent item text.', document['descriptions']['pret:ITEM_POTION'])
            self.assertEqual('Independent machine text.', document['descriptions']['pret:ITEM_TM04'])
            for invalid in ('', ' \t\n'):
                potion['description_english'] = invalid
                item_path.write_text(json.dumps(source))
                with self.subTest(kind='item', text=repr(invalid)), self.assertRaisesRegex(ValueError, 'nonempty'):
                    compile_catalog(root)
            potion['description_english'] = 'Independent item text.'
            item_path.write_text(json.dumps(source))
            move_path.write_text(re.sub(pattern, r'\1""\2', move_source, flags=re.M))
            with self.subTest(kind='machine'), self.assertRaisesRegex(ValueError, 'nonempty'):
                compile_catalog(root)
            move_path.unlink()
            with self.assertRaises(FileNotFoundError):
                compile_catalog(root)

    def test_storage_catalogs_regenerate_without_runtime_proofs(self):
        for kind in ('opening', 'quest'):
            with self.subTest(catalog=kind), tempfile.TemporaryDirectory() as temporary:
                spec = importlib.util.spec_from_file_location(f'{kind}_items',
                    ROOT / f'scripts/imports/items/import-{kind}-items.py')
                compiler = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(compiler)
                output = Path(temporary)
                with redirect_stdout(StringIO()):
                    compiler.import_catalog(ROOT / RAW, output)
                name = f'{kind}-items.json'
                document = json.loads((output / name).read_text())
                self.assertEqual(document, json.loads((ROOT / f'content/generated/data/items/{kind}' / name).read_text()))
                self.assertEqual({'schemaVersion', 'catalog'}, set(document))
                self.assertEqual(1, document['schemaVersion'])
                self.assertFalse({'sourceCommit', 'profile', 'completeItemCatalog'} & document['catalog'].keys())
                self.assertEqual([name], [path.name for path in output.iterdir()])

    def test_compiler_item_values_emit_references_only(self):
        potion = item_definition('ITEM_POTION')
        self.assertEqual(300, potion['price'])
        self.assertEqual('"pret:ITEM_POTION"', json.dumps(potion, default=item_json))
        with self.assertRaisesRegex(ValueError, 'Unknown item'):
            item_definition('ITEM_NONE')

    def test_selected_descriptions_preserve_source_and_shared_identity(self):
        raw = ROOT / RAW
        rows = json.loads((raw / 'src/data/items.json').read_bytes())['items']
        descriptions = (raw / 'src/move_descriptions.c').read_text()
        selected = ('ITEM_CARD_KEY', 'ITEM_TM04')
        values = described_items(lambda path: (raw / path).read_bytes(), selected)
        for symbol, value in zip(selected, values):
            source = next(row for row in rows if row['itemId'] == symbol)
            if symbol == 'ITEM_TM04':
                match = re.search(r'gMoveDescription_' + re.escape(source['moveId']) +
                                  r'\[\] = _\(("(?:[^"\\]|\\.)*")\);', descriptions)
                expected = json.loads(match[1])
            else:
                expected = source['description_english'].replace('\\n', '\n')
            self.assertEqual(item_definition(symbol).identity, value['metadata']['item'])
            self.assertEqual(expected, value['metadata']['description'])
            self.assertEqual([], value['effectBytes'])
            self.assertEqual({'item', 'description'}, value['metadata'].keys())

    def test_selected_descriptions_reject_missing_duplicate_or_ambiguous_source(self):
        raw = ROOT / RAW
        source = json.loads((raw / 'src/data/items.json').read_bytes())
        symbol = 'ITEM_TM04'
        row = next(row for row in source['items'] if row['itemId'] == symbol)
        description = (raw / 'src/move_descriptions.c').read_bytes()
        for selection in ([], [symbol, symbol]):
            with self.subTest(selection=selection), self.assertRaisesRegex(ValueError, 'nonempty and unique'):
                described_items(lambda path: (raw / path).read_bytes(), selection)
        for rows in ([entry for entry in source['items'] if entry['itemId'] != symbol],
                     source['items'] + [row]):
            with self.subTest(rows=len(rows)), self.assertRaisesRegex(ValueError, 'one item declaration'):
                described_items(lambda path: json.dumps(dict(items=rows)).encode(), [symbol])
        for invalid in (b'', description + b'\n' + description):
            def read(path):
                return invalid if path == 'src/move_descriptions.c' else (raw / path).read_bytes()
            with self.subTest(description_bytes=len(invalid)), self.assertRaisesRegex(ValueError, 'one machine move description'):
                described_items(read, [symbol])

    def test_regional_documents_do_not_own_business_definitions(self):
        count = 0
        for path in (ROOT / 'content/generated').rglob('*.json'):
            if path == ROOT / 'content/generated/data/items.json':
                continue
            def visit(value):
                nonlocal count
                if isinstance(value, dict):
                    self.assertFalse('sourceItemSymbol' in value and 'registrability' in value, str(path))
                    self.assertFalse('sourceItemSymbol' in value and 'useSupport' in value, str(path))
                    if isinstance(value.get('item'), str) and value['item'].startswith('pret:ITEM_'):
                        item_definition(value['item']); count += 1
                    for child in value.values():
                        visit(child)
                elif isinstance(value, list):
                    for child in value:
                        visit(child)
            visit(json.loads(path.read_text()))
        self.assertGreater(count, 1000)


if __name__ == '__main__':
    unittest.main()
