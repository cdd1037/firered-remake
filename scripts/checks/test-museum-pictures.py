#!/usr/bin/env python3
"""Source-bound fossil pictures, complete RGBA and isolated conversion failures."""
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts/imports/presentation/import-museum-pictures.py'
RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated/ui/museum-pictures'
spec = importlib.util.spec_from_file_location('museum_pictures', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class MuseumPictureImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = module.SourceFiles(RAW)
        cls.results = module.build(cls.source)

    def test_original_species_bindings_and_full_source_pixels(self):
        assets = json.loads(self.results['pictures.json'])['assets']
        self.assertEqual({'aerodactyl-fossil': 142, 'kabutops-fossil': 141},
                         {a['id']: a['sourceSpeciesId'] for a in assets})
        self.assertEqual({'pictures.json', *(a['texturePath'] for a in assets)}, set(self.results))
        for asset in assets:
            path = 'graphics/script_menu/' + asset['id'].replace('-', '_') + '.png'
            with Image.open(io.BytesIO(self.source[path])) as original:
                self.assertEqual(('P', (64, 64), 0), (original.mode, original.size, original.info['transparency']))
                colors = original.getpalette('RGB')
                expected = bytes(channel for index in original.tobytes() for channel in (
                    *[(((colors[index * 3 + c] >> 3) << 3) | ((colors[index * 3 + c] >> 3) >> 2)) for c in range(3)],
                    0 if index == 0 else 255))
            with Image.open(io.BytesIO(self.results[asset['texturePath']])) as generated:
                self.assertEqual((64, 64), generated.size)
                self.assertEqual(expected, generated.convert('RGBA').tobytes())
        self.assertFalse(any(b'content/raw' in data for name, data in self.results.items() if name.endswith('.json')))

    def test_generated_outputs_are_current(self):
        for name, data in self.results.items():
            self.assertTrue(module.generated_output_matches(GENERATED / name, data), name)

    def test_missing_input_and_changed_source_bindings_fail(self):
        source = dict(self.source)
        del source['graphics/script_menu/aerodactyl_fossil.png']
        with self.assertRaises(KeyError):
            module.build(source)
        source = dict(self.source)
        source['src/script_menu.c'] = source['src/script_menu.c'].replace(
            b'gSpecialVar_0x8005, gSpecialVar_0x8006, 8, 8',
            b'gSpecialVar_0x8005, gSpecialVar_0x8006, 7, 8')
        with self.assertRaisesRegex(ValueError, 'layout changed'):
            module.build(source)

    def test_cold_input_conversion_and_read_only_check(self):
        with tempfile.TemporaryDirectory(prefix='firered-museum-import-') as temporary:
            base = Path(temporary)
            raw, output = base / 'raw', base / 'generated'
            for name, data in self.source.items():
                path = raw / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            command = [sys.executable, str(SCRIPT), '--raw', str(raw), '--out', str(output)]
            result = subprocess.run(command, cwd=base, capture_output=True, text=True, timeout=30)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            before = {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in output.iterdir()}
            result = subprocess.run([*command, '--check'], cwd=base, capture_output=True, text=True, timeout=30)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual(before, {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in output.iterdir()})
            (output / 'pictures.json').write_text('{}')
            result = subprocess.run([*command, '--check'], cwd=base, capture_output=True, text=True, timeout=30)
            self.assertNotEqual(0, result.returncode)
            self.assertEqual('{}', (output / 'pictures.json').read_text())

    def test_missing_source_cli_does_not_partially_publish(self):
        with tempfile.TemporaryDirectory(prefix='firered-museum-missing-') as temporary:
            base = Path(temporary)
            raw, output = base / 'raw', base / 'generated'
            for name, data in self.source.items():
                if name.endswith('kabutops_fossil.png'):
                    continue
                path = raw / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            result = subprocess.run([sys.executable, str(SCRIPT), '--raw', str(raw), '--out', str(output)],
                                    cwd=base, capture_output=True, text=True, timeout=30)
            self.assertNotEqual(0, result.returncode)
            self.assertFalse(output.exists())


if __name__ == '__main__':
    unittest.main(verbosity=2)
