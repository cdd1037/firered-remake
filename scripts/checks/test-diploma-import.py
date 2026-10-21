#!/usr/bin/env python3
"""Independent pixel oracle for the horizontal GBA screenblocks, plus cold rebuild and fail-closed checks."""
import importlib.util
import io
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
from import_check_fixtures import snapshot_files, write_files
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts/imports/presentation/import-diploma.py'
spec = importlib.util.spec_from_file_location('diploma_import', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class MemorySource:
    def __init__(self, data): self.data = data
    def read(self, path): return self.data[path]
    def text(self, path): return self.read(path).decode()


class DiplomaImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = module.Source(ROOT)
        cls.output = module.generate(cls.source)

    def test_all_pixels_against_independent_hardware_addressing(self):
        with Image.open(io.BytesIO(self.source.read('graphics/diploma/diploma.png'))) as image:
            self.assertEqual(('P', (128, 128)), (image.mode, image.size))
            indices, palette = image.tobytes(), image.getpalette('RGB')
        entries = struct.unpack('<2048H', self.source.read('graphics/diploma/diploma.bin'))
        variants = []
        for name, origin in [('kanto', 0), ('national', 256)]:
            expected = bytearray()
            for y in range(160):
                for x in range(240):
                    sx = x + origin
                    entry = entries[(sx // 256) * 1024 + (y // 8) * 32 + (sx // 8) % 32]
                    tile, bank = entry & 1023, entry >> 12
                    tx, ty = x % 8, y % 8
                    if entry & 1024: tx = 7 - tx
                    if entry & 2048: ty = 7 - ty
                    index = indices[((tile // 16) * 8 + ty) * 128 + (tile % 16) * 8 + tx] & 15
                    color = 0 if index == 0 else bank * 16 + index
                    expected.extend([((palette[color * 3 + c] >> 3) << 3) | ((palette[color * 3 + c] >> 3) >> 2) for c in range(3)])
                    expected.append(255)
            with Image.open(io.BytesIO(self.output[name + '.png'])) as image:
                self.assertEqual((240, 160), image.size)
                self.assertEqual(bytes(expected), image.convert('RGBA').tobytes())
            variants.append(bytes(expected))
        self.assertNotEqual(*variants)

    def test_source_text_layout_and_current_generated_output(self):
        data = json.loads(self.output['diploma.json'])
        self.assertEqual((20, 46, 14, 120, 121), (data['playerY'], data['bodyY'], data['lineHeight'], data['signatureX'], data['signatureY']))
        self.assertEqual('PLAYER: {PLAYER}', data['playerTemplate'])
        self.assertEqual(4, len(data['bodyTemplate'].splitlines()))
        self.assertEqual((16, 'MUS_OBTAIN_BADGE', 340), (data['fadeFrames'], data['fanfare'], data['fanfareCounterFrames']))
        self.assertNotIn('source', json.dumps(data).lower())
        for name, value in self.output.items():
            self.assertEqual(value, (ROOT / module.BASE / name).read_bytes())

    def test_missing_source_changed_input_gate_or_invalid_tilemap_fail(self):
        data = dict(self.source.raw)
        del data['graphics/diploma/diploma.bin']
        with self.assertRaises(KeyError): module.generate(MemorySource(data))
        data = dict(self.source.raw)
        data['src/diploma.c'] = data['src/diploma.c'].replace(b'JOY_NEW(A_BUTTON)', b'JOY_HELD(A_BUTTON)')
        with self.assertRaisesRegex(ValueError, 'Unsupported source semantics'): module.generate(MemorySource(data))
        image, tilemap = self.source.read('graphics/diploma/diploma.png'), self.source.read('graphics/diploma/diploma.bin')
        with self.assertRaises(ValueError): module.render_background(image, tilemap[:-2])
        bad = bytearray(tilemap); struct.pack_into('<H', bad, 0, 0xF000)
        with self.assertRaisesRegex(ValueError, 'palette outside'): module.render_background(image, bad)
        bad = bytearray(tilemap); struct.pack_into('<H', bad, 0, 0x03FF)
        with self.assertRaisesRegex(ValueError, 'tile or palette outside'): module.render_background(image, bad)

    def test_isolated_cold_import_and_read_only_check(self):
        with tempfile.TemporaryDirectory(prefix='firered-diploma-import-') as temporary:
            root = Path(temporary)
            write_files(root / self.source.raw_prefix, self.source.raw)
            command = [sys.executable, str(SCRIPT), '--project-root', str(root)]
            result = subprocess.run(command, capture_output=True, text=True, timeout=30)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            output = root / module.BASE
            before = snapshot_files(output.iterdir())
            result = subprocess.run([*command, '--check'], capture_output=True, text=True, timeout=30)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual(before, snapshot_files(output.iterdir()))
            (output / 'diploma.json').write_text('{}')
            result = subprocess.run([*command, '--check'], capture_output=True, text=True, timeout=30)
            self.assertNotEqual(0, result.returncode)
            self.assertEqual('{}', (output / 'diploma.json').read_text())


if __name__ == '__main__': unittest.main(verbosity=2)
