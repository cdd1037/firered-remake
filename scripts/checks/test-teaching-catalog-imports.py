#!/usr/bin/env python3
"""Verify teaching imports preserve source semantics without shipping C to runtime."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import json
import pathlib
import re
import unittest

ROOT = _PROJECT_ROOT
RAW = ROOT / 'content/raw/pokefirered/037335f4'


def importer(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / f'scripts/imports/story/import-{name}.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class TeachingCatalogImports(unittest.TestCase):
    def load(self, family):
        lock = json.loads((ROOT / f'scripts/recipes/story/{family}.lock.json').read_text())
        files = {row['path']: (RAW / row['path']).read_bytes() for row in lock['files']}
        generated = importer(family).catalog(files, lock['commit'])
        self.assertEqual(generated, (ROOT / f'content/generated/data/services/{family}.json').read_bytes())
        return files, json.loads(generated)

    def test_every_tutor_compatibility_matches_source(self):
        files, document = self.load('move-tutors')
        source = files['src/data/pokemon/tutor_learnsets.h'].decode()
        rows = dict(re.findall(r'\[(SPECIES_[A-Z0-9_]+)\]\s*=\s*([^,]+),', source))
        self.assertEqual(len(rows), len(document['compatibility']))
        self.assertEqual(15, len(document['moves']))
        for row in document['compatibility']:
            expression = rows[row['species']['value'][5:]]
            for move in document['moves']:
                expected = 'TUTOR(' + move['move']['value'][5:] + ')' in expression
                self.assertEqual(expected, move['index'] in row['tutors'])

    def test_teaching_texts_preserve_native_control_sequences(self):
        for family in ('move-tutors', 'move-reminder'):
            files, document = self.load(family)
            self.assertEqual(13, len(document['texts']))
            source = files['src/strings.c'].decode()
            for symbol, text in document['texts'].items():
                expected = re.search(r'const u8 ' + symbol + r'\[\] = _\("([^\"]*)"\);', source)[1]
                self.assertEqual(expected, text)

    def test_old_rod_result_texts_keep_pause_control(self):
        module = importer('old-rod')
        sources = {path: (RAW / path).read_bytes() for path in module.PATHS}
        document = module.convert(sources)
        self.assertEqual(document, json.loads((ROOT / 'content/generated/data/services/old-rod.json').read_text()))
        self.assertEqual(2, len(document['texts']))
        self.assertTrue(all(text.endswith('{PAUSE_UNTIL_PRESS}') for text in document['texts'].values()))

    def test_name_rater_prompts_match_source(self):
        document = json.loads((ROOT / 'content/generated/data/services/name-rater.json').read_text())
        source = (RAW / 'src/strings.c').read_text()
        for key, symbol in [('choosePokemonText', 'gText_ChoosePokemon'), ('choosePokemonCancelText', 'gText_ChoosePokemonCancel')]:
            expected = re.search(r'const u8 ' + symbol + r'\[\] = _\("([^"]*)"\);', source)[1]
            self.assertEqual(expected, document[key])

    def test_unsupported_tutor_expression_is_not_silently_ignored(self):
        files, _ = self.load('move-tutors')
        files['src/data/pokemon/tutor_learnsets.h'] = files['src/data/pokemon/tutor_learnsets.h'].replace(
            b'[SPECIES_NONE] = 0,', b'[SPECIES_NONE] = UNKNOWN_MACRO,')
        with self.assertRaises(ValueError):
            importer('move-tutors').catalog(files, 'test')


if __name__ == '__main__':
    unittest.main()
