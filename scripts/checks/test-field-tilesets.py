#!/usr/bin/env python3
"""Offline regression checks from current flat maps to complete runtime tile tables."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
IMPORTER = ROOT / 'scripts/imports/maps/import-field-tilesets.py'
RAW = Path('content/raw/pokefirered/037335f4')
OUTPUT = Path('content/generated/maps/tilesets.json')
LEGACY_OUTPUT = Path('content/generated/field-tilesets.json')


def current_maps():
    maps = []
    index = json.loads((ROOT / 'content/generated/maps/world.json').read_bytes())
    for entry in index['maps']:
        path = ROOT / 'content/generated' / entry['path']
        document = json.loads(path.read_bytes())
        maps.append((path.relative_to(ROOT), document))
    if not maps:
        raise AssertionError('Current schema-3 map inputs are missing')
    return maps


def prepare_inputs(destination, maps):
    index = json.loads((ROOT / 'content/generated/maps/world.json').read_bytes())
    chosen = {str(path.relative_to('content/generated')) for path, _ in maps}
    entries = [e for e in index['maps'] if e['path'] in chosen]
    target = destination / 'content/generated/maps/world.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(dict(maps=entries)))
    paths = [path for path, _ in maps]
    paths += [RAW / 'data/layouts/layouts.json', RAW / 'src/data/tilesets/headers.h',
              RAW / 'src/data/tilesets/metatiles.h']
    paths += [path.relative_to(ROOT) for path in (ROOT / RAW / 'data/tilesets').rglob('metatile_attributes.bin')]
    for relative in paths:
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / relative, target)


def fields(word):
    # Independent masks and division, rather than calling the converter decoder.
    return [word & 0x1ff, (word & 0x3e00) // 0x200,
            (word & 0x07000000) // 0x01000000, (word & 0x60000000) // 0x20000000]


class FieldTilesetTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='field-tileset-check-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.maps = current_maps()

    def invoke(self, *arguments, succeeds=True):
        result = subprocess.run([sys.executable, str(IMPORTER), '--root', str(self.root), *arguments],
                                cwd=self.root, capture_output=True, text=True)
        if succeeds:
            self.assertEqual(0, result.returncode, result.stderr)
        else:
            self.assertNotEqual(0, result.returncode, result.stdout)
        return result

    def test_current_flat_maps_regenerate_complete_tables_and_check(self):
        prepare_inputs(self.root, self.maps)
        self.assertTrue(any(path.name != 'runtime-map.json' for path, _ in self.maps))
        self.assertFalse((self.root / OUTPUT).exists())
        self.invoke()
        before = (self.root / OUTPUT).read_bytes()
        actual = json.loads(before)
        index = json.loads((self.root / 'content/generated/maps/world.json').read_bytes())
        by_path = {str(path.relative_to('content/generated')):doc for path,doc in self.maps}
        self.assertEqual({e.get('layout', by_path[e['path']]['events']['layout']) for e in index['maps']}, set(actual['layouts']))
        self.assertTrue(actual['tilesets'])
        self.assertEqual({name for pair in actual['layouts'].values() for name in pair}, set(actual['tilesets']))
        self.assertFalse((self.root / LEGACY_OUTPUT).exists())

        # Preserve every previously verified definition without treating a partial
        # existing table as the authority for which current maps must be included.
        previous = json.loads((ROOT / OUTPUT).read_bytes())
        for category in ('layouts', 'tilesets'):
            self.assertTrue(previous[category])
            for identity, value in previous[category].items():
                self.assertEqual(value, actual[category][identity], identity)

        rows_checked = 0
        for name, rows in actual['tilesets'].items():
            raw = (ROOT / RAW / 'data/tilesets' / name / 'metatile_attributes.bin').read_bytes()
            self.assertGreater(len(raw), 0)
            self.assertEqual(len(raw) // 4, len(rows), name)
            for index, row in enumerate(rows):
                word = int.from_bytes(raw[index * 4:index * 4 + 4], 'little')
                self.assertEqual(fields(word), row, f'{name}:{index}')
                rows_checked += 1
        self.assertGreater(rows_checked, 0)

        # Map rows carry independent raw attribute words. Comparing every initial
        # metatile catches primary/secondary pair or layout-reference mistakes.
        for _, document in self.maps:
            pair = actual['layouts'][document['events']['layout']]
            self.assertTrue(pair[0].startswith('primary/'))
            self.assertTrue(pair[1].startswith('secondary/'))
            for metatile, word in document['metatiles']:
                bank, index = (0, metatile) if metatile < 640 else (1, metatile - 640)
                self.assertEqual(fields(word), actual['tilesets'][pair[bank]][index],
                                 f"{document['stableId']}:{metatile}")

        # The off-state Vermilion beam is deliberately absent from its initial
        # grid. It must still come from the full secondary source table.
        gym = next(document for _, document in self.maps
                   if document['events']['layout'] == 'LAYOUT_VERMILION_CITY_GYM')
        off_beam = 0x29c  # METATILE_VermilionGym_Beam_Node_BottomLeft_Edge_Off
        self.assertNotIn(off_beam, {row[0] for row in gym['metatiles']})
        secondary = actual['layouts'][gym['events']['layout']][1]
        raw = (ROOT / RAW / 'data/tilesets' / secondary / 'metatile_attributes.bin').read_bytes()
        index = off_beam - 640
        self.assertEqual(fields(int.from_bytes(raw[index * 4:index * 4 + 4], 'little')),
                         actual['tilesets'][secondary][index])
        self.invoke('--check')
        self.invoke()
        self.assertEqual(before, (self.root / OUTPUT).read_bytes())
        self.assertFalse((self.root / LEGACY_OUTPUT).exists())

    def test_empty_effective_map_input_fails_without_output(self):
        directory = self.root / 'content/generated/maps/regions/empty'
        directory.mkdir(parents=True)
        (self.root / 'content/generated/maps/world.json').write_text('{"schemaVersion":3,"maps":[]}')
        result = self.invoke(succeeds=False)
        self.assertIn('No schema-3 field maps', result.stderr)
        self.assertFalse((self.root / OUTPUT).exists())
        self.assertFalse((self.root / LEGACY_OUTPUT).exists())

    def test_missing_layout_is_not_silently_skipped(self):
        prepare_inputs(self.root, self.maps[:1])
        path = self.root / RAW / 'data/layouts/layouts.json'
        path.write_text('{"layouts":[]}')
        result = self.invoke(succeeds=False)
        self.assertIn('Missing source layouts', result.stderr)
        self.assertFalse((self.root / OUTPUT).exists())

    def test_missing_tileset_declaration_is_not_silently_skipped(self):
        prepare_inputs(self.root, self.maps[:1])
        path = self.root / RAW / 'data/layouts/layouts.json'
        document = json.loads(path.read_bytes())
        used = self.maps[0][1]['events']['layout']
        next(row for row in document['layouts'] if row.get('id') == used)['secondary_tileset'] = 'gTileset_Missing'
        path.write_text(json.dumps(document))
        result = self.invoke(succeeds=False)
        self.assertIn('Missing tileset declaration', result.stderr)
        self.assertFalse((self.root / OUTPUT).exists())

    def test_check_detects_drift_without_rewriting(self):
        prepare_inputs(self.root, self.maps[:1])
        self.invoke()
        path = self.root / OUTPUT
        wrong = b'{"layouts":{},"tilesets":{}}\n'
        path.write_bytes(wrong)
        result = self.invoke('--check', succeeds=False)
        self.assertIn('Regenerate content/generated/maps/tilesets.json', result.stderr)
        self.assertEqual(wrong, path.read_bytes())
        self.assertFalse((self.root / LEGACY_OUTPUT).exists())


if __name__ == '__main__':
    unittest.main()
