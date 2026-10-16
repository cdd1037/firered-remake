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
        cases = [(f'story/import-{region}-encounters.py', f'data/encounters/{region}.json')
                 for region in ('route11', 'vermilion', 'rocktunnel')]
        cases += [('pokemon/import-pokemon.py', 'data/pokemon/starters.json'),
                  ('story/import-old-rod.py', 'data/services/old-rod.json'),
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


    def test_grid_menu_and_mail_use_current_paths_without_source_copies(self):
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary)
            cases = [
                ('presentation/import-source-grid-menus.py', ['--reference', str(raw), '--target', str(target), '--dependency-root', str(ROOT)], ['ui/grid-menus.json']),
                ('story/import-mail-composer.py', ['--reference-root', str(raw), '--project-root', str(target)], ['data/services/mail-composer/catalog.json', 'data/services/mail-composer/contracts.json'])]
            for entry, arguments, outputs in cases:
                result = subprocess.run([sys.executable, str(ROOT / 'scripts/imports' / entry), *arguments],
                                        cwd=temporary, capture_output=True, text=True, timeout=60)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                for relative in outputs:
                    actual = target / 'content/generated' / relative
                    self.assertTrue(actual.is_file(), actual)
                    self.assertEqual(json.loads((ROOT / 'content/generated' / relative).read_bytes()),
                                     json.loads(actual.read_bytes()))
            self.assertFalse((target / 'content/raw').exists())
            self.assertFalse((target / 'scripts').exists())
            self.assertFalse(any(p.is_dir() and p.suffix == '.json' for p in target.rglob('*')))

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
            for region in ('pewter', 'bill', 'cerulean', 'route11'):
                with self.subTest(region=region):
                    relative = f'content/generated/data/machine-learning/{region}.json'
                    target = project / relative
                    entry = 'import-pewter-machines.py' if region == 'pewter' else 'import-machine-views.py'
                    selection = [] if region == 'pewter' else ['--region', region]
                    result = subprocess.run([sys.executable, str(ROOT / 'scripts/imports/items' / entry),
                                             *selection, '--out', str(target)], cwd=temporary,
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
                    self.assertEqual({'schemaVersion', 'icons'}, set(actual))
            self.assertFalse(any(p.name == 'manifest.json' for p in project.rglob('*')))
            self.assertFalse(any(p.is_dir() and p.suffix in ('.json', '.png') for p in project.rglob('*')))


    def test_machine_icons_need_only_raw_images_and_check_never_writes(self):
        from unittest.mock import patch
        import importlib.util
        spec = importlib.util.spec_from_file_location('early_machine_icons',
            ROOT / 'scripts/imports/items/import-machine-views.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        from item_source import ItemSource
        source = ROOT / 'content/raw/pokefirered/037335f4'
        paths = {'src/data/item_icon_table.h', 'src/data/graphics/items.h'}
        reader = ItemSource(lambda path: (source / path).read_bytes())
        for names in module.ICONS.values():
            for name in names:
                paths.update(reader.icon_sources('ITEM_' + name))
        with tempfile.TemporaryDirectory() as temporary:
            project = Path(temporary)
            raw = project / 'raw'
            for name in paths:
                target = raw / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((source / name).read_bytes())
            def snapshot():
                return {str(path.relative_to(project)): (path.read_bytes(), path.stat().st_mtime_ns)
                        for path in project.rglob('*') if path.is_file()}
            for region in module.ICONS:
                with self.subTest(region=region):
                    output = project / ('generated/data/machine-learning/' + region + '.json')
                    command = [sys.executable, str(ROOT / 'scripts/imports/items/import-machine-views.py'),
                               '--region', region, '--project-root', str(project),
                               '--raw', str(raw), '--out', str(output.relative_to(project))]
                    def invoke(*arguments):
                        return subprocess.run(command + list(arguments), cwd=temporary,
                                              capture_output=True, text=True, timeout=30)
                    before = snapshot()
                    missing = invoke('--check')
                    self.assertNotEqual(0, missing.returncode)
                    self.assertEqual(before, snapshot())
                    built = invoke()
                    self.assertEqual(0, built.returncode, built.stdout + built.stderr)
                    before = snapshot()
                    checked = invoke('--check')
                    self.assertEqual(0, checked.returncode, checked.stdout + checked.stderr)
                    self.assertEqual(before, snapshot())
                    document = json.loads(output.read_bytes())
                    self.assertEqual({'schemaVersion', 'icons'}, set(document))
                    self.assertEqual(['pret:ITEM_' + name for name in module.ICONS[region]],
                                     [icon['item']['value'] for icon in document['icons']])
                    document['icons'][0]['item']['value'] = 'pret:ITEM_TM00'
                    output.write_text(json.dumps(document))
                    before = snapshot()
                    rejected = invoke('--check')
                    self.assertNotEqual(0, rejected.returncode)
                    self.assertEqual(before, snapshot())
            # No recipe, canonical catalog or generated source dependency exists
            # in this raw-only tree, and a real icon binding mutation must fail.
            binding = raw / 'src/data/item_icon_table.h'
            binding.write_bytes(binding.read_bytes().replace(b'[ITEM_TM03]', b'[ITEM_TM00]', 1))
            with patch('subprocess.run', side_effect=AssertionError('Icon conversion invoked a process')):
                with self.assertRaisesRegex(ValueError, 'Nonunique source match'):
                    module.convert('cerulean', project, raw)
            self.assertFalse((project / 'scripts').exists())
            self.assertFalse((project / 'content/generated/data/machines.json').exists())

if __name__ == '__main__':
    unittest.main()
