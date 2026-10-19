#!/usr/bin/env python3
"""Source trainer-class display extraction, without business-definition changes."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT

import unittest
from trainer_import import class_names
from shared_trainers import read_trainers
from runtime_json import runtime_json_bytes


class TrainerDisplayImportTests(unittest.TestCase):
    def test_actual_source_labels_cover_every_current_trainer_class(self):
        labels = class_names(PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
        trainers = read_trainers(PROJECT_ROOT / 'content/generated')
        self.assertTrue(trainers)
        self.assertTrue({row['trainerClass'] for row in trainers.values()} <= labels.keys())
        self.assertEqual('SWIMMER♂', labels['TRAINER_CLASS_SWIMMER_M'])
        self.assertEqual('SWIMMER♀', labels['TRAINER_CLASS_SWIMMER_F'])
        self.assertEqual('POKéMON RANGER', labels['TRAINER_CLASS_PKMN_RANGER'])
        self.assertEqual('POKéMON BREEDER', labels['TRAINER_CLASS_PKMN_BREEDER'])

    def test_duplicate_class_unknown_control_and_empty_name_are_rejected(self):
        key = 'src/data/text/trainer_class_names.h'
        for declaration in [
            '[TRAINER_CLASS_YOUNGSTER] = _("A"),\n[TRAINER_CLASS_YOUNGSTER] = _("B"),',
            '[TRAINER_CLASS_YOUNGSTER] = _("{UNKNOWN}"),',
            '[TRAINER_CLASS_YOUNGSTER] = _(" "),',
            'unsupported declaration',
        ]:
            with self.subTest(declaration=declaration):
                raw = ('const u8 gTrainerClassNames[][13] = {\n' + declaration + '\n};\n').encode()
                with self.assertRaises(ValueError):
                    class_names({key: raw})

    def test_generated_class_names_are_exactly_the_source_projection(self):
        labels = class_names(PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
        actual = PROJECT_ROOT / 'content/generated/data/trainer-class-names.json'
        self.assertEqual(runtime_json_bytes(labels, sort_keys=True), actual.read_bytes())


if __name__ == '__main__':
    unittest.main()
