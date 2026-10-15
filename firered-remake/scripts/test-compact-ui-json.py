#!/usr/bin/env python3
"""Reproduce compact UI JSON with the current writers and retained raw inputs."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / 'content/raw/pokefirered/037335f4'


def importer(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), ROOT / f'scripts/import-{name}.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class CompactUiJsonTests(unittest.TestCase):
    def test_mail_writer_reproduces_all_gameplay_json(self):
        module = importer('mail-composer')
        class Sources:
            def text(self, relative):
                return (RAW / relative).read_text()
        for name, value in [('catalog.json', module.vocabulary(Sources())),
                            ('contracts.json', module.contracts(Sources()))]:
            self.assertEqual((ROOT / 'content/generated/mail-composer' / name).read_bytes(), module.canonical(value))

    def test_fame_checker_writer_reproduces_complete_catalog(self):
        module = importer('fame-checker')
        lock = json.loads((ROOT / 'scripts/fame-checker.lock.json').read_text())
        sources = {entry['path']: (RAW / entry['path']).read_text() for entry in lock['files']}
        value = module.extract(sources)
        self.assertEqual(16, len(value['persons']))
        self.assertEqual(96, sum(len(person['facts']) for person in value['persons']))
        self.assertEqual((ROOT / 'content/generated/fame-checker/catalog.json').read_bytes(), module.json_bytes(value))

    def test_fly_map_writer_reproduces_catalog_and_manifest(self):
        module = importer('fly-map')
        lock = json.loads((ROOT / 'scripts/import-config/fly-map.json').read_text())
        sources = {entry['path']: (RAW / entry['path']).read_bytes() for entry in lock['files']}
        outputs = module.pack_backgrounds(module.convert(sources), ROOT / 'content/generated')
        for name in ['fly-map.json', 'manifest.json']:
            self.assertEqual((ROOT / 'content/generated/fly-map' / name).read_bytes(), outputs[name])


if __name__ == '__main__':
    unittest.main()
