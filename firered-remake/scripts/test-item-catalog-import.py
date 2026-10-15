#!/usr/bin/env python3
"""Independent raw-value and generated-reference checks for item business ownership."""
import json
from pathlib import Path
import re
import unittest
from item_catalog import ROOT, RAW, compile_catalog, item_definition, item_json


class ItemCatalogTests(unittest.TestCase):
    def test_all_real_items_match_source_business_values(self):
        raw = ROOT / RAW
        constants = {key: int(value) for key, value in re.findall(r'^#define (\w+)\s+(\d+)\b',
            (raw / 'include/constants/items.h').read_text(), re.M)}
        original = {row['itemId']: row for row in json.loads((raw / 'src/data/items.json').read_text())['items']
                    if row['itemId'] != 'ITEM_NONE'}
        document = compile_catalog(ROOT)
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
        self.assertEqual(document, json.loads((ROOT / 'content/generated/item-catalog/catalog.json').read_text()))

    def test_compiler_item_values_emit_references_only(self):
        potion = item_definition('ITEM_POTION')
        self.assertEqual(300, potion['price'])
        self.assertEqual('"pret:ITEM_POTION"', json.dumps(potion, default=item_json))
        with self.assertRaisesRegex(ValueError, 'Unknown item'):
            item_definition('ITEM_NONE')

    def test_regional_documents_do_not_own_business_definitions(self):
        count = 0
        for path in (ROOT / 'content/generated').rglob('*.json'):
            if path.parent.name == 'item-catalog':
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
