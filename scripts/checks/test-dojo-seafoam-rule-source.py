#!/usr/bin/env python3
"""Exact live trainer and encounter outputs, independently of cumulative species proofs."""
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from shared_trainers import trainer_json, runtime_trainer, read_trainers
from world_source import WorldSource


def load(family):
    path = ROOT / f'scripts/imports/story/import-{family}-rules.py'
    spec = importlib.util.spec_from_file_location(family.replace('-', '_'), path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class DojoSeafoamRuleSourceTests(unittest.TestCase):
    def test_source_only_generation_preserves_all_live_values(self):
        current = read_trainers(ROOT / 'content/generated')
        original_read = Path.read_bytes
        def raw_only(path):
            self.assertNotIn('/content/generated/', str(path))
            return original_read(path)
        for family in ('fighting-dojo', 'seafoam'):
            with self.subTest(family=family):
                with patch.object(Path, 'read_bytes', raw_only):
                    docs, files = load(family).generate(RAW)
                self.assertEqual({'trainers.json'}, docs.keys())
                trainers = docs['trainers.json']['trainers']
                for trainer in trainers:
                    self.assertEqual(current[trainer['id']['value']], runtime_trainer(trainer))
                actual = json.loads(trainer_json(ROOT / 'content/generated', docs['trainers.json'], check=True))
                expected = json.loads((ROOT / f'content/generated/data/regions/{family}/trainers.json').read_text())
                self.assertEqual(expected, actual)
                self.assertEqual(1 if family == 'fighting-dojo' else 2, len(files))
                for path, contents in files.items():
                    self.assertEqual((ROOT / path).read_bytes(), contents, path)

    def test_clis_publish_and_check_only_live_final_resources(self):
        for family in ('fighting-dojo', 'seafoam'):
            with self.subTest(family=family), tempfile.TemporaryDirectory() as temporary:
                output = Path(temporary)
                command = [sys.executable, str(ROOT / f'scripts/imports/story/import-{family}-rules.py'),
                           '--raw-root', str(RAW), '--out', str(output)]
                subprocess.run(command, check=True, capture_output=True, text=True)
                subprocess.run([*command, '--check'], check=True, capture_output=True, text=True)
                self.assertFalse((output / 'content/raw').exists())
                self.assertEqual({'trainers.json'}, {path.name for path in
                    (output / f'content/generated/data/regions/{family}').iterdir()})

    def test_seafoam_held_item_tracks_raw_source(self):
        original = WorldSource.text
        def changed(source, path):
            text = original(source, path)
            if path == 'src/data/pokemon/species_info.h':
                text, count = re.subn(r'(\[SPECIES_ARTICUNO\] =\s*\n    \{.*?\.itemCommon = )ITEM_NONE',
                                      r'\1ITEM_ORAN_BERRY', text, count=1, flags=re.S)
                self.assertEqual(1, count)
            return text
        with patch.object(WorldSource, 'text', changed):
            _, files = load('seafoam').generate(RAW)
        self.assertIn(b'new(new("pret:SPECIES_ARTICUNO"),"ITEM_ORAN_BERRY","ITEM_NONE")',
                      files['src/Game.Domain/Encounters/SeafoamEncounterTables.cs'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
