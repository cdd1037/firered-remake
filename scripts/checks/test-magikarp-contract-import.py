#!/usr/bin/env python3
"""Check Magikarp story references without raw-source runtime receipts."""
import importlib.util
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
IMPORT = ROOT / 'scripts/imports/items/import-magikarp-contract.py'
RAW = ROOT / 'content/raw/pokefirered/037335f4'
OUTPUT = ROOT / 'content/generated/data/services/magikarp-gift/magikarp-contract.json'
SPEC = importlib.util.spec_from_file_location('magikarp_import', IMPORT)
IMPORTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(IMPORTER)
EXPECTED_SCRIPTS = [
    'EventScript_GetGiftMonPartySlot', 'EventScript_TransferredToPC',
    'EventScript_TransferredSomeonesPC', 'EventScript_TransferredSomeonesPCBoxFull',
    'EventScript_TransferredBillsPC', 'EventScript_TransferredBillsPCBoxFull',
    'Std_MsgboxDefault', 'Std_MsgboxYesNo',
]
EXPECTED_TEXTS = [
    'Text_GiveNicknameToThisMon', 'Text_MonSentToBoxInSomeonesPC',
    'Text_MonSentToBoxInBillsPC', 'Text_MonSentToBoxSomeonesBoxFull',
    'Text_MonSentToBoxBillsBoxFull',
]
SCRIPTS = 'data/maps/Route4_PokemonCenter_1F/scripts.inc'


class MagikarpContractImportTests(unittest.TestCase):
    def test_runtime_document_contains_only_required_story_references(self):
        outputs = IMPORTER.build(IMPORTER.read_sources())
        self.assertEqual({'magikarp-contract.json'}, set(outputs))
        actual = json.loads(outputs['magikarp-contract.json'])
        self.assertEqual({'schemaVersion': 1, 'commonScripts': EXPECTED_SCRIPTS,
                          'commonTexts': EXPECTED_TEXTS}, actual)
        self.assertEqual(OUTPUT.read_bytes(), outputs['magikarp-contract.json'])
        story = json.loads((ROOT / 'content/generated/data/story.json').read_bytes())
        self.assertTrue(set(actual['commonScripts']) <= story['Compilation']['Programs'].keys())
        self.assertTrue(set(actual['commonTexts']) <= story['TextBank'].keys())

    def test_regenerates_with_only_actual_script_inputs_and_no_c_functions(self):
        with tempfile.TemporaryDirectory(prefix='magikarp-contract-') as temporary:
            root = Path(temporary)
            raw, output = root / 'raw', root / 'out'
            for relative in IMPORTER.SOURCE_PATHS:
                path = raw / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(RAW / relative, path)
            command = [sys.executable, str(IMPORT), '--raw', str(raw), '--output', str(output)]
            for _ in range(2):
                result = subprocess.run(command, cwd=root, capture_output=True, text=True)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual(OUTPUT.read_bytes(), (output / OUTPUT.name).read_bytes())
            self.assertEqual([OUTPUT.name], sorted(path.name for path in output.iterdir()))
            checked = subprocess.run(command + ['--check'], capture_output=True, text=True)
            self.assertEqual(0, checked.returncode, checked.stdout + checked.stderr)
            (output / OUTPUT.name).write_text('{}', encoding='utf-8')
            checked = subprocess.run(command + ['--check'], capture_output=True, text=True)
            self.assertNotEqual(0, checked.returncode)
            self.assertEqual('{}', (output / OUTPUT.name).read_text(encoding='utf-8'))
            (raw / SCRIPTS).unlink()
            missing = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(0, missing.returncode)
            self.assertEqual('{}', (output / OUTPUT.name).read_text(encoding='utf-8'))

    def test_additional_reachable_local_script_has_no_historical_count_gate(self):
        sources = IMPORTER.read_sources()
        expected = IMPORTER.build(sources)
        extra = 'Route4_PokemonCenter_1F_EventScript_AdditionalLocalStep'
        sources[SCRIPTS] = sources[SCRIPTS].replace(IMPORTER.SALESMAN + '::',
            IMPORTER.SALESMAN + '::\n\tcall ' + extra) + '\n' + extra + '::\n\treturn\n'
        self.assertEqual(expected, IMPORTER.build(sources))

    def test_missing_and_duplicate_common_labels_are_rejected(self):
        for label in EXPECTED_SCRIPTS + EXPECTED_TEXTS:
            with self.subTest(label=label):
                sources = IMPORTER.read_sources()
                for path, text in sources.items():
                    sources[path] = re.sub(r'^' + label + r'::?\s*$', '', text, flags=re.M)
                with self.assertRaises(ValueError):
                    IMPORTER.build(sources)
        sources = IMPORTER.read_sources()
        sources[SCRIPTS] += '\nEventScript_GetGiftMonPartySlot::\n\treturn\n'
        with self.assertRaisesRegex(ValueError, 'Duplicate source label'):
            IMPORTER.build(sources)

    def test_new_common_dependency_requires_explicit_selection(self):
        sources = IMPORTER.read_sources()
        sources[SCRIPTS] = sources[SCRIPTS].replace(IMPORTER.SALESMAN + '::',
            IMPORTER.SALESMAN + '::\n\tcall EventScript_NewSharedDependency')
        sources[SCRIPTS] += '\nEventScript_NewSharedDependency::\n\treturn\n'
        with self.assertRaisesRegex(ValueError, 'Unexpected common-script scope'):
            IMPORTER.build(sources)

    def test_script_and_text_roles_are_validated(self):
        for label, body, message in [
            ('Std_MsgboxDefault', '\t.string "not a script$"\n', 'Expected common script body'),
            ('Text_GiveNicknameToThisMon', '\treturn\n', 'Expected common text body'),
        ]:
            with self.subTest(label=label):
                sources = IMPORTER.read_sources()
                for path, text in sources.items():
                    sources[path] = re.sub(r'(^' + label + r'::?[^\n]*\n).*?(?=^\w+::?\s*$|\Z)',
                                           lambda match: match[1] + body, text, flags=re.M | re.S)
                with self.assertRaisesRegex(ValueError, message):
                    IMPORTER.build(sources)


if __name__ == '__main__':
    unittest.main(verbosity=2)
