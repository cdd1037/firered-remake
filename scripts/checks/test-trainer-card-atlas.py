#!/usr/bin/env python3
"""Bounded exact-RGBA packing and the Trainer Card regeneration contract."""
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'scripts/lib'))
from presentation_atlas import pack_pages

spec = importlib.util.spec_from_file_location(
    'trainer_card_import', ROOT/'scripts/imports/trainers/import-trainer-card.py')
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)


def decoded(outputs, row):
    r = row['region']
    with Image.open(io.BytesIO(outputs[row['texturePath']])) as image:
        return image.convert('RGBA').crop((r['x'], r['y'], r['x']+r['width'], r['y']+r['height']))


class TrainerCardAtlasTests(unittest.TestCase):
    def test_exact_duplicates_share_but_palette_alpha_and_hidden_rgb_do_not(self):
        images = [('first', Image.new('RGBA', (2, 2), (10, 20, 30, 0))),
                  ('same', Image.new('RGBA', (2, 2), (10, 20, 30, 0))),
                  ('hidden-rgb', Image.new('RGBA', (2, 2), (11, 20, 30, 0))),
                  ('alpha', Image.new('RGBA', (2, 2), (10, 20, 30, 1))),
                  ('palette', Image.new('RGBA', (2, 2), (10, 21, 30, 255)))]
        outputs, rows, pages = pack_pages(images, 'cards', 8)
        for (_, expected), row in zip(images, rows):
            self.assertEqual(expected.size, decoded(outputs, row).size)
            self.assertEqual(expected.tobytes(), decoded(outputs, row).tobytes())
        by_id = {row['id']: row for row in rows}
        self.assertEqual(by_id['first']['region'], by_id['same']['region'])
        self.assertEqual(4, len({tuple(row['region'].values()) for row in rows}))
        reordered, reversed_rows, reversed_pages = pack_pages(list(reversed(images)), 'cards', 8)
        self.assertEqual(outputs, reordered)
        self.assertEqual(pages, reversed_pages)
        self.assertEqual(rows, list(reversed(reversed_rows)))

    def test_pages_are_bounded_guttered_and_fail_instead_of_resizing(self):
        images = [(str(i), Image.new('RGBA', (4, 510), (i, 2, 3, 255))) for i in range(5)]
        outputs, rows, pages = pack_pages(images, 'cards', 6)
        self.assertEqual(3, len(pages))
        for page in pages:
            self.assertLessEqual(page['height'], 1024)
        for (_, expected), row in zip(images, rows):
            self.assertEqual(expected.tobytes(), decoded(outputs, row).tobytes())
            self.assertEqual(1, row['region']['x'])
        for invalid in ([], [('wide', Image.new('RGBA', (5, 1)))],
                        [('tall', Image.new('RGBA', (1, 1023)))],
                        [('duplicate', images[0][1]), ('duplicate', images[1][1])]):
            with self.assertRaises(ValueError):
                pack_pages(invalid, 'cards', 6)

    def test_actual_cohort_preserves_logical_rows_and_deterministic_regeneration(self):
        raw = ROOT/'content/raw/pokefirered/037335f4'
        sources = importer.read_sources(importer.load_lock(), raw)
        outputs = importer.convert(sources)
        document = json.loads(outputs['trainer-card.json'])
        self.assertEqual(3, document['schemaVersion'])
        self.assertEqual(59, len(document['assets']))
        self.assertEqual(59, len({row['id'] for row in document['assets']}))
        self.assertEqual(2, len(document['pages']))
        self.assertEqual(46, len({(row['texturePath'], tuple(row['region'].values()))
                                  for row in document['assets']}))
        for page in document['pages']:
            with Image.open(io.BytesIO(outputs[page['texturePath']])) as image:
                self.assertEqual((page['width'], page['height']), image.size)
        for row in document['assets']:
            self.assertEqual((row['width'], row['height']), decoded(outputs, row).size)
        with tempfile.TemporaryDirectory() as temporary:
            out = Path(temporary)
            (out/'unrelated.png').write_bytes(b'leave unrelated files alone')
            (out/'badge-5.png').write_bytes(b'obsolete owned standalone')
            importer.import_content(raw, out)
            self.assertFalse((out/'badge-5.png').exists())
            self.assertEqual(b'leave unrelated files alone', (out/'unrelated.png').read_bytes())
            before = {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in out.iterdir()}
            importer.import_content(raw, out, check=True)
            self.assertEqual(before, {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in out.iterdir()})
            for path, data in outputs.items():
                self.assertEqual(data, (out/path).read_bytes())
            (out/'cards-000.png').write_bytes(b'corrupt')
            with self.assertRaisesRegex(ValueError, 'differs'):
                importer.import_content(raw, out, check=True)


if __name__ == '__main__':
    unittest.main()
