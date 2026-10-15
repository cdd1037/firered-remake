#!/usr/bin/env python3
"""Regenerate single-file resources from real raw inputs into an isolated tree."""
import json
from PIL import Image
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class SingleFileImportTests(unittest.TestCase):
    def test_cli_outputs_are_files_and_preserve_gameplay_data(self):
        cases = [(f'trainers/import-{region}-trainers.py', f'data/trainer-battles/{region}.json')
                 for region in ('bill', 'vermilion', 'ssanne', 'route11', 'rocktunnel',
                                'vermilion-gym', 'fuchsia-gym', 'celadon-gym')]
        cases += [(f'story/import-{region}-encounters.py', f'data/encounters/{region}.json')
                  for region in ('route11', 'vermilion', 'rocktunnel')]
        cases += [('pokemon/import-pokemon.py', 'data/pokemon/starters.json'),
                  ('story/import-old-rod.py', 'data/services/old-rod.json'),
                  ('items/import-vermilion-machines.py', 'data/machine-learning/vermilion.json'),
                  ('items/import-early-pickups.py', 'data/items/early-pickups.json'),
                  ('story/import-old-man-tutorial.py', 'data/services/old-man-tutorial.json')]
        with tempfile.TemporaryDirectory() as temporary:
            generated = Path(temporary) / 'generated'
            for script, relative in cases:
                with self.subTest(script=script):
                    target = generated / relative
                    result = subprocess.run([sys.executable, str(ROOT / 'scripts/imports' / script),
                                             '--out', str(target)], cwd=temporary,
                                            capture_output=True, text=True, timeout=60)
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    self.assertTrue(target.is_file(), target)
                    self.assertEqual(json.loads((ROOT / 'content/generated' / relative).read_bytes()),
                                     json.loads(target.read_bytes()))
            self.assertFalse(any(p.is_dir() and p.suffix in ('.json', '.png') for p in generated.rglob('*')))
            shared = json.loads((generated / 'data/trainers.json').read_bytes())['trainers']
            current = {row['id']['value']: row for row in json.loads(
                (ROOT / 'content/generated/data/trainers.json').read_bytes())['trainers']}
            self.assertTrue(shared)
            for row in shared:
                self.assertEqual(current[row['id']['value']], row)


    def test_machine_cli_outputs_preserve_values_and_exact_icon_pixels(self):
        def pixels(path, region=None):
            with Image.open(path) as source:
                image = source.convert('RGBA')
                if region:
                    x, y, width, height = (region[k] for k in ('x', 'y', 'width', 'height'))
                    image = image.crop((x, y, x + width, y + height))
                return image.size, image.tobytes()

        def logical(value):
            if isinstance(value, list):
                return [logical(child) for child in value]
            if not isinstance(value, dict):
                return value
            return {key: logical(child) for key, child in value.items() if key not in ('pngPath', 'region')}

        with tempfile.TemporaryDirectory() as temporary:
            project = Path(temporary)
            for region in ('pewter', 'bill', 'cerulean', 'ssanne', 'route11'):
                with self.subTest(region=region):
                    relative = f'content/generated/data/machine-learning/{region}.json'
                    target = project / relative
                    result = subprocess.run([sys.executable, str(ROOT / f'scripts/imports/items/import-{region}-machines.py'),
                                             '--out', str(target)], cwd=temporary,
                                            capture_output=True, text=True, timeout=60)
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    self.assertTrue(target.is_file(), target)
                    actual = json.loads(target.read_bytes())
                    expected = json.loads((ROOT / relative).read_bytes())
                    self.assertEqual(logical(expected), logical(actual))
                    self.assertFalse({'profile', 'sourceCommit', 'limitations'} & actual.keys())
                    previous = expected.get('icons', [expected.get('icon')])
                    emitted = actual.get('icons', [actual.get('icon')])
                    self.assertTrue(previous)
                    for old, new in zip(previous, emitted, strict=True):
                        self.assertEqual(pixels(ROOT / old['pngPath'], old.get('region')),
                                         pixels(project / new['pngPath']))
            self.assertFalse(any(p.name == 'manifest.json' for p in project.rglob('*')))
            self.assertFalse(any(p.is_dir() and p.suffix in ('.json', '.png') for p in project.rglob('*')))

if __name__ == '__main__':
    unittest.main()
