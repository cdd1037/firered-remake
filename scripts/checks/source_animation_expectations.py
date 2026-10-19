"""Independent source identities for generated animation import checks."""
import re


def assert_source_move_identities(test, scripts, reference):
    """Check admitted roots against original constants and positional dispatch."""
    constants = dict((name, int(value)) for name, value in re.findall(
        r'^#define (MOVE_\w+)\s+(\d+)\s*$',
        (reference / 'include/constants/moves.h').read_text(), re.M))
    table = re.search(r'^gBattleAnims_Moves::\n((?:\s*\.4byte Move_\w+\n)+)',
                      (reference / 'data/battle_anim_scripts.s').read_text(), re.M)
    test.assertIsNotNone(table, 'Original move animation dispatch is required')
    dispatch = re.findall(r'\.4byte (Move_\w+)', table[1])
    test.assertTrue(scripts, 'Generated move animation roots must not be empty')
    test.assertEqual(len(scripts), len({row['moveId'] for row in scripts}))
    for row in scripts:
        move, label = row['moveId'], row['label']
        test.assertEqual(constants.get(label.replace('Move_', 'MOVE_', 1)), move, label)
        test.assertGreaterEqual(move, 0, label)
        test.assertLess(move, len(dispatch), label)
        test.assertEqual(dispatch[move], label)
