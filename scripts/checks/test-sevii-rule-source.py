#!/usr/bin/env python3
"""Sevii rule inputs are raw data, never retired map packages or species proofs."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from world_source import WorldSource


class SeviiRuleSourceTests(unittest.TestCase):
    def test_ruby_business_values_without_generated_inputs(self):
        path = ROOT / 'scripts/imports/story/import-sevii-ruby-rules.py'
        spec = importlib.util.spec_from_file_location('ruby_rules', path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        original_read_text, original_read_bytes = Path.read_text, Path.read_bytes
        def guarded_text(path, *args, **kwargs):
            self.assertNotIn('/content/generated/', str(path))
            return original_read_text(path, *args, **kwargs)
        def guarded_bytes(path, *args, **kwargs):
            self.assertNotIn('/content/generated/', str(path))
            return original_read_bytes(path, *args, **kwargs)
        with patch.object(Path, 'read_text', guarded_text), patch.object(Path, 'read_bytes', guarded_bytes):
            docs = module.generate(RAW)
        self.assertEqual({'trainers.json'}, docs.keys())
        self.assertEqual(17, len(docs['trainers.json']['trainers']))
        self.assertEqual(18, len(docs['trainers.json']['battles']))
        original_source_text = WorldSource.text
        def changed_trainer(source, path):
            text = original_source_text(source, path)
            return text.replace('[TRAINER_BLACK_BELT_HUGH]', '[TRAINER_MISSING_HUGH]') if path == 'src/data/trainers.h' else text
        with patch.object(WorldSource, 'text', changed_trainer):
            with self.assertRaises(ValueError):
                module.generate(RAW)

    def test_ruby_cli_publishes_final_outputs_only(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / 'content/generated/data/regions/sevii-ruby'
            command = [sys.executable, str(ROOT / 'scripts/imports/story/import-sevii-ruby-rules.py'),
                       '--raw-root', str(RAW), '--out', str(output)]
            subprocess.run(command, check=True, capture_output=True, text=True)
            self.assertFalse((Path(temporary) / 'content/raw').exists())
            self.assertEqual({'trainers.json'}, {path.name for path in output.iterdir()})
            for filename in ('trainers.json',):
                expected = json.loads((ROOT / 'content/generated/data/regions/sevii-ruby' / filename).read_text())
                self.assertEqual(expected, json.loads((output / filename).read_text()))
            subprocess.run([*command, '--check'], check=True, capture_output=True, text=True)

    def test_first_sevii_cli_needs_no_preparation(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / 'content/generated/data/regions/first-sevii'
            command = [sys.executable, str(ROOT / 'scripts/imports/story/import-first-sevii-rules.py'),
                       '--raw-root', str(RAW), '--out', str(output)]
            subprocess.run(command, check=True, capture_output=True, text=True)
            for filename in ('trainers.json', 'items.json', 'services.json', 'encounters.json'):
                expected = json.loads((ROOT / 'content/generated/data/regions/first-sevii' / filename).read_text())
                actual = json.loads((output / filename).read_text())
                # Independent pickups are emitted in the canonical map selection order.
                if filename == 'items.json':
                    for value in (expected, actual):
                        value['pickups'].sort(key=lambda row: row['id'])
                        value['renewableGroups'].sort(key=lambda row: row['mapSymbol'])
                self.assertEqual(expected, actual, filename)
            subprocess.run([*command, '--check'], check=True, capture_output=True, text=True)


if __name__ == '__main__':
    unittest.main(verbosity=2)
