#!/usr/bin/env python3
"""Exact presentation palette/frame semantics and real species-art regeneration."""
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from PIL import Image
from project_paths import PROJECT_ROOT
from presentation_pixels import PixelReader, palette, rgba
from presentation_source import safe, Source
from species_art import build
from generated_output_compare import generated_output_matches


class MemorySource:
    def __init__(self, data):
        self.data = data

    def read(self, path):
        return self.data[path]


def sheet(size, values):
    image = Image.frombytes('P', size, values)
    image.putpalette([c for i in range(256) for c in (i, i, i)])
    result = io.BytesIO()
    image.save(result, format='PNG')
    return result.getvalue()


class PresentationPixelsTests(unittest.TestCase):
    def test_distinct_rgb5_expansions_and_hidden_rgb(self):
        data = b'JASC-PAL\n0100\n2\n56 56 56\n248 0 80\n'
        self.assertEqual(palette(data), [(57, 57, 57), (255, 0, 82)])
        self.assertEqual(palette(data, True), [(57, 57, 57), (255, 0, 82)])
        # Value 3 differs between floor scaling (24) and bit replication (24);
        # value 4 distinguishes them (32 versus 33).
        data = b'JASC-PAL\n0100\n1\n32 32 32\n'
        self.assertEqual(palette(data), [(33, 33, 33)])
        self.assertEqual(palette(data, True), [(32, 32, 32)])
        self.assertEqual(rgba(b'\0', palette(data)), bytes([33,33,33,0]))

    def test_frames_crops_and_pixel_only_adapter(self):
        values = bytes((i // 1024) % 16 for i in range(8192))
        pal = ('JASC-PAL\n0100\n16\n' + ''.join(f'{i*8} 0 0\n' for i in range(16))).encode()
        reader = PixelReader(MemorySource({'sheet': sheet((64,128),values), 'pal':pal}))
        full, pixels = reader.indexed('sheet','pal',(64,128))
        self.assertEqual(full,values)
        self.assertEqual(pixels,reader.pixels('sheet','pal',(64,128)))
        self.assertEqual(reader.indexed('sheet','pal',(64,128),first_frame=True)[0],values[:1024])
        self.assertEqual(reader.indexed('sheet','pal',(64,128),second_frame=True)[0],values[4096:8192])
        cropped=reader.indexed('sheet','pal',(64,128),crop=(8,16,40,48),first_frame=True)[0]
        self.assertEqual(cropped,b''.join(values[y*64+8:y*64+40] for y in range(16,48)))

    def test_bad_palette_and_index_are_rejected(self):
        for data in (b'bad',b'JASC-PAL\n0100\n1\n256 0 0\n',b'JASC-PAL\n0100\n2\n0 0 0\n'):
            with self.assertRaises(ValueError):palette(data)
        with self.assertRaises(ValueError):rgba(bytes([2]),[(0,0,0)])
        reader=PixelReader(MemorySource({'sheet':sheet((8,8),bytes([16])*64)}))
        with self.assertRaises(ValueError):reader.indexed('sheet','missing',(8,8))
        with self.assertRaises(ValueError):reader.indexed('sheet','missing',(16,8))

    def test_path_traversal_and_symlinks_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for path in ('../escape','/absolute','a/../escape','a\\escape','a:escape',''):
                with self.assertRaises(ValueError):safe(root,path)
            (root/'link').symlink_to(root, target_is_directory=True)
            with self.assertRaises(ValueError):safe(root,'link/file')

    def test_source_reader_derives_actual_closure(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            raw=root/'content/raw/pokefirered/037335f4/example.txt'
            raw.parent.mkdir(parents=True);raw.write_bytes(b'original')
            reader=Source(root)
            self.assertEqual(reader.read('example.txt'),b'original')
            self.assertEqual(reader.ref('example.txt','symbol'),dict(path='example.txt',symbol='symbol'))
            self.assertEqual(reader.raw,{'example.txt':b'original'})
            with self.assertRaises(FileNotFoundError):reader.read('missing.txt')
            with self.assertRaises(ValueError):reader.read('../escape')

    def test_species_profiles_reproduce_actual_runtime_art(self):
        profiles=json.loads((PROJECT_ROOT/'scripts/recipes/presentation/species-art.json').read_bytes())
        self.assertTrue(profiles)
        with tempfile.TemporaryDirectory() as directory:
            for profile in profiles:
                with self.subTest(profile=profile):
                    outputs=build(PROJECT_ROOT,profile)
                    self.assertGreater(len(outputs),1)
                    for relative,data in outputs.items():
                        temporary=Path(directory)/profile/relative
                        temporary.parent.mkdir(parents=True,exist_ok=True);temporary.write_bytes(data)
                        retained=PROJECT_ROOT/'content/generated/sprites/pokemon'/profile/relative
                        self.assertTrue(generated_output_matches(retained,temporary.read_bytes()),str(retained))


if __name__ == '__main__':
    unittest.main()
