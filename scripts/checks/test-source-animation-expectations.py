#!/usr/bin/env python3
"""Source identity contract probes, independent of production animation imports."""
import tempfile
import unittest
from pathlib import Path
from source_animation_expectations import assert_source_move_identities


class SourceAnimationIdentityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.reference = Path(self.temp.name)
        (self.reference / 'include/constants').mkdir(parents=True)
        (self.reference / 'data').mkdir()
        self.constants = self.reference / 'include/constants/moves.h'
        self.table = self.reference / 'data/battle_anim_scripts.s'
        self.constants.write_text('#define MOVE_NONE 0\n#define MOVE_POUND 1\n#define MOVE_KARATE_CHOP 2\n')
        self.dispatch = 'gBattleAnims_Moves::\n\t.4byte Move_NONE\n\t.4byte Move_POUND\n\t.4byte Move_KARATE_CHOP\n'
        self.table.write_text(self.dispatch)
        self.pound = {'moveId': 1, 'label': 'Move_POUND'}

    def test_valid_emitted_subset_does_not_require_complete_move_catalog(self):
        assert_source_move_identities(self, [self.pound], self.reference)

    def test_empty_duplicate_or_mismatched_emitted_roots_fail(self):
        for rows in [[], [self.pound, self.pound],
                     [{'moveId': 2, 'label': 'Move_POUND'}],
                     [{'moveId': 1, 'label': 'Move_UNKNOWN'}]]:
            with self.subTest(rows=rows), self.assertRaises(AssertionError):
                assert_source_move_identities(self, rows, self.reference)

    def test_missing_or_misaligned_dispatch_fails(self):
        for dispatch in ['', self.dispatch.replace('.4byte Move_POUND', '.4byte Move_KARATE_CHOP')]:
            self.table.write_text(dispatch)
            with self.subTest(dispatch=dispatch), self.assertRaises(AssertionError):
                assert_source_move_identities(self, [self.pound], self.reference)


if __name__ == '__main__':
    unittest.main()
