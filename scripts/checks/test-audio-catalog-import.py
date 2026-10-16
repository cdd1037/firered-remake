"""Focused tests for the maintained catalog importer, without private resources."""
import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest
import wave

spec = importlib.util.spec_from_file_location('build_cry_catalog', Path(__file__).resolve().parents[1] / 'imports/audio/build-cry-catalog.py')
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)


class CryCatalogTests(unittest.TestCase):
    def test_sample_boundaries(self):
        for samples, size in ((1, 1), (2, 2), (3, 3), (63, 33), (64, 33), (65, 34)):
            self.assertEqual(size, importer.required_bytes(samples))
        for count in (0, -1, 1_000_001):
            with self.assertRaises(ValueError):
                importer.required_bytes(count)

    def test_dpcm_nibble_order(self):
        self.assertEqual(bytes((100, 101, 110, 114)), importer.decode(bytes((100, 0x01, 0x32)), 4))

    def test_truncated_payload(self):
        with self.assertRaisesRegex(ValueError, 'Truncated'):
            importer.decode(b'\x00', 2)

    def test_pack_bounds(self):
        with self.assertRaises(ValueError):
            importer.read_pack(b'\x00', importer.PACK_BASE - 1, 1)
        with self.assertRaises(ValueError):
            importer.read_pack(b'\x00', importer.PACK_BASE, 2)

    def test_builds_catalog_from_source_assets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            files = {
                'include/constants/species.h': '#define SPECIES_BULBASAUR 1\n',
                'include/constants/hoenn_cries.h': '',
                'src/data/pokemon/cry_ids.h': '',
                'sound/cry_tables.inc': '\tcry Cry_Bulbasaur\n',
                'sound/direct_sound_data.inc': 'Cry_Bulbasaur::\n.incbin "cry.bin"\n',
            }
            for path, content in files.items():
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(content)
            address = importer.CRY_TABLE + 12
            asset = struct.pack('<HHIII', 1, 0, 10512 * 1024, 0, 2) + bytes((100, 0x01))
            (root / 'cry.bin').write_bytes(asset)
            with wave.open(str(root / 'cry.wav'), 'wb') as wav:
                wav.setparams((1, 1, 10512, 0, 'NONE', 'not compressed'))
                wav.writeframes(bytes((228, 229, 229)))
            pack = bytearray(importer.PACK_END - importer.PACK_BASE)
            offset = importer.CRY_TABLE - importer.PACK_BASE
            pack[offset:offset + 12] = struct.pack('<4BI4B', 32, 60, 0, 0, address, 255, 0, 255, 0)
            pack[offset + 12:offset + 12 + len(asset)] = asset
            result = importer.build_catalog(root, pack)
            self.assertEqual(b'FRCN0001' + struct.pack('<IHHIIB3x', 1, 1, 0, 2, 2, 0), result)
            pack[offset + 12] = 0
            with self.assertRaisesRegex(ValueError, 'does not match'):
                importer.build_catalog(root, pack)


if __name__ == '__main__':
    unittest.main()
