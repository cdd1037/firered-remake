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


cue_spec = importlib.util.spec_from_file_location('build_cue_catalog', Path(__file__).resolve().parents[1] / 'imports/audio/build-cue-catalog.py')
cue_importer = importlib.util.module_from_spec(cue_spec)
cue_spec.loader.exec_module(cue_importer)


class CueCatalogTests(unittest.TestCase):
    def sequence(self, instructions):
        data = bytearray(cue_importer.PACK_END - cue_importer.PACK_BASE)
        data[:len(instructions)] = instructions
        return cue_importer.Pack(data)

    def test_finite_sequence_with_running_status(self):
        self.assertFalse(cue_importer.track_loops(self.sequence(bytes((0xBD, 1, 0xD0, 60, 100, 0x81, 64, 110, 0x81, 0xB1))), cue_importer.PACK_BASE))

    def test_loop_follows_pointer(self):
        code = bytes((0x81, 0xB2)) + struct.pack('<I', cue_importer.PACK_BASE)
        self.assertTrue(cue_importer.track_loops(self.sequence(code), cue_importer.PACK_BASE))

    def test_pattern_returns_do_not_look_like_infinite_loops(self):
        base = cue_importer.PACK_BASE
        code = bytes((0xB3,)) + struct.pack('<I', base + 11) + bytes((0xB3,)) + struct.pack('<I', base + 11) + bytes((0xB1, 0x81, 0xB4))
        self.assertFalse(cue_importer.track_loops(self.sequence(code), base))

    def test_operands_are_not_scanned_as_opcodes(self):
        self.assertFalse(cue_importer.track_loops(self.sequence(bytes((0xBD, 0xB2, 0xB1))), cue_importer.PACK_BASE))

    def test_bad_pointer_fails(self):
        with self.assertRaisesRegex(ValueError, 'outside pack'):
            cue_importer.track_loops(self.sequence(bytes((0xB2,)) + struct.pack('<I', 1)), cue_importer.PACK_BASE)

    def test_unknown_opcode_fails(self):
        with self.assertRaisesRegex(ValueError, 'Unsupported sequence opcode'):
            cue_importer.track_loops(self.sequence(bytes((0xB9,))), cue_importer.PACK_BASE)

    def test_catalog_needs_only_source_pack_and_canonical_cries(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sources = {
                'include/constants/songs.h': '#define MUS_DUMMY 0\n#define SE_SELECT 1\n',
                'sound/song_table.inc': 'song mus_dummy, 0, 0\nsong se_select, 2, 2\n',
                'src/sound.c': 'static const struct Fanfare sFanfares[] = { { MUS_HEAL, 160 } };',
                'include/constants/species.h': '#define SPECIES_BULBASAUR 1\n',
            }
            for path, content in sources.items():
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(content)
            base = cue_importer.PACK_BASE
            data = bytearray(cue_importer.PACK_END - base)
            struct.pack_into('<IHH', data, cue_importer.SONG_TABLE - base + 8, base + 128, 2, 2)
            struct.pack_into('<BBBBII', data, 128, 1, 0, 5, 0, base + 256, base + 512)
            data[512] = 0xB1
            cries = b'FRCN0001' + struct.pack('<IHHIIB3x', 1, 1, 0, 2, 2, 0)
            result = cue_importer.build_catalog(root, data, cries)
            self.assertEqual(['CRY_BULBASAUR_ENCOUNTER', 'CRY_BULBASAUR_NORMAL', 'SE_SELECT'], [c['id'] for c in result['cues']])
            self.assertEqual(5, result['cues'][-1]['priority'])
            data[cue_importer.SONG_TABLE - base + 12] = 1
            with self.assertRaisesRegex(ValueError, 'Packed song table differs'):
                cue_importer.build_catalog(root, data, cries)

    def test_pattern_overflow_fails(self):
        with self.assertRaisesRegex(ValueError, 'stack overflow'):
            cue_importer.track_loops(self.sequence(bytes((0xB3,)) + struct.pack('<I', cue_importer.PACK_BASE)), cue_importer.PACK_BASE)


if __name__ == '__main__':
    unittest.main()
