#!/usr/bin/env python3
"""Regenerate presentation JSON from retained raw inputs, without historical stage inventories."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json
import io
import tempfile
from PIL import Image
from generated_output_compare import generated_output_matches
from pathlib import Path
import runpy
import unittest

ROOT = _PROJECT_ROOT
PACKAGES = ('celio', 'four-island', 'five-island', 'six-island', 'seven-island',
            'sevii-ruby', 'event-island', 'mewtwo', 'league-rematch')
INVENTORIES = {'inputs', 'inheritedInputs', 'sourcePaths', 'sourceFiles', 'localInputs', 'dependencyInputs'}


class PresentationImportMetadataTests(unittest.TestCase):
    def test_generated_comparison_ignores_only_serialization(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'data.json'
            path.write_bytes(b'{"a":1,"b":[true,2]}')
            self.assertTrue(generated_output_matches(path, b'{ "b": [true, 2], "a": 1 }'))
            self.assertFalse(generated_output_matches(path, b'{"a":1,"b":[1,2]}'))
            self.assertFalse(generated_output_matches(path, b'{"a":1,"b":[2,true]}'))
            self.assertFalse(generated_output_matches(path, b'{"a":1}'))
            image = Image.new('RGBA', (2, 3), (12, 34, 56, 0))
            path = Path(directory) / 'image.png'
            image.save(path, compress_level=0)
            encoded = io.BytesIO(); image.save(encoded, format='PNG', compress_level=9)
            self.assertTrue(generated_output_matches(path, encoded.getvalue()))
            image.putpixel((0, 0), (13, 34, 56, 0))
            encoded = io.BytesIO(); image.save(encoded, format='PNG')
            self.assertFalse(generated_output_matches(path, encoded.getvalue()))
            encoded = io.BytesIO(); Image.new('RGBA', (3, 2)).save(encoded, format='PNG')
            self.assertFalse(generated_output_matches(path, encoded.getvalue()))
            path = Path(directory) / 'audio.bin'; path.write_bytes(b'abc')
            self.assertTrue(generated_output_matches(path, b'abc'))
            self.assertFalse(generated_output_matches(path, b'abd'))

    def test_real_writers_reproduce_runtime_json_without_stage_inventories(self):
        for package in PACKAGES:
            with self.subTest(package=package):
                module = runpy.run_path(str(ROOT / f'scripts/imports/presentation/import-{package}-presentation.py'))
                args = (ROOT, None) if package in ('four-island', 'sevii-ruby') else (ROOT, None, ROOT)
                outputs, _ = module['build'](*args)
                runtime_count = 0
                for path, data in outputs.items():
                    if path.startswith('content/generated/') and path.endswith('.json'):
                        runtime_count += 1
                        self.assertEqual((ROOT / path).read_bytes(), data, path)
                        self.assert_runtime_fields(json.loads(data), path)
                self.assertGreater(runtime_count, 0)
                self.assertFalse(any(path.startswith('scripts/recipes/') for path in outputs))
                self.assertTrue(any(path.startswith('content/raw/') for path in outputs))

    def assert_runtime_fields(self, value, path):
        if isinstance(value, dict):
            for key, item in value.items():
                self.assertFalse(key in INVENTORIES and isinstance(item, list), f'{path}: {key}')
                self.assertNotEqual(key, 'decodedSha256', path)
                self.assert_runtime_fields(item, path)
        elif isinstance(value, list):
            for item in value:
                self.assert_runtime_fields(item, path)


if __name__ == '__main__':
    unittest.main()
