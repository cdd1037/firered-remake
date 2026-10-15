#!/usr/bin/env python3
"""Check the generated vocabulary against retained original inputs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
from pathlib import Path
import unittest

ROOT = _PROJECT_ROOT
spec = importlib.util.spec_from_file_location('mail_composer', ROOT / 'scripts/imports/story/import-mail-composer.py')
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)

class LocalInputs:
    def text(self, relative):
        return (ROOT / 'content/raw/pokefirered/037335f4' / relative).read_text()

class MailComposerTests(unittest.TestCase):
    def test_generated_gameplay_data_reproduces(self):
        source = LocalInputs()
        for name, document in [('catalog.json', importer.vocabulary(source)),
                               ('contracts.json', importer.contracts(source))]:
            self.assertEqual((ROOT / 'content/generated/data/services/mail-composer' / name).read_bytes(), importer.canonical(document))
            def check(value):
                if isinstance(value, dict):
                    self.assertFalse(set(value) & {'source', 'definitionSource', 'textSource', 'nameSource', 'references'})
                    for child in value.values(): check(child)
                elif isinstance(value, list):
                    for child in value: check(child)
            check(document)

if __name__ == '__main__':
    unittest.main()
