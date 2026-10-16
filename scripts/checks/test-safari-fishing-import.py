#!/usr/bin/env python3
"""Verify offline Safari fishing regeneration from the pinned local source."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
IMPORT = ROOT / 'scripts/imports/story/import-safari-fishing.py'


class SafariFishingImportTests(unittest.TestCase):
    def test_cli_reproduces_runtime_document_and_check_detects_change(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / 'fishing.json'
            command = [sys.executable, str(IMPORT), '--raw', str(RAW), '--out', str(output)]
            for _ in range(2):
                result = subprocess.run(command, cwd=temporary, capture_output=True, text=True)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual((ROOT / 'content/generated/maps/regions/safari/fishing.json').read_bytes(), output.read_bytes())
            self.assertEqual(0, subprocess.run(command + ['--check'], capture_output=True).returncode)
            document = json.loads(output.read_text())
            self.assertEqual({'schemaVersion', 'tables', 'heldItems'}, set(document))
            self.assertEqual(12, len(document['tables']))
            self.assertEqual({'old', 'good', 'super'}, {t['rod'] for t in document['tables']})
            self.assertEqual(7, len(document['heldItems']))
            output.write_text('{}')
            self.assertNotEqual(0, subprocess.run(command + ['--check'], capture_output=True).returncode)


if __name__ == '__main__':
    unittest.main(verbosity=2)
