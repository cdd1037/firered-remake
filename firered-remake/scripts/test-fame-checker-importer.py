#!/usr/bin/env python3
"""Bounded source semantics checks for the Fame Checker extractor."""
import argparse
import importlib.util
import json
from pathlib import Path
import re

parser = argparse.ArgumentParser()
parser.add_argument('--git-dir', type=Path, required=True)
args = parser.parse_args()
path = Path(__file__).with_name('import-fame-checker.py')
spec = importlib.util.spec_from_file_location('fame_importer', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
checks = []
def check(condition, label):
    if not condition:
        raise AssertionError(label)
    checks.append(label)

lock, sources = module.read_sources(args.git_dir, path.with_name('fame-checker.lock.json'))
result = module.extract(lock, sources)
check(module.json_bytes(result) == module.json_bytes(module.extract(lock, sources)), 'deterministic_output')
check(len(result['persons']) == 16 and sum(len(p['facts']) for p in result['persons']) == 96, 'complete_table_cross_product')
check(len(result['collectionEvents']) == 118, 'complete_famechecker_macro_inventory')
check(sum(e['explicitlyUnused'] for e in result['collectionEvents']) == 5, 'unused_original_duplicates_explicit')
check(all(e['sourceMaps'] for e in result['collectionEvents'] if not e['explicitlyUnused']), 'all_live_events_have_source_map_context')
source = sources['src/fame_checker.c']
def body(name):
    match = re.search(r'\b' + name + r'\([^;{}]*\)\n\{(.*?)\n\}', source, re.S)
    check(match is not None, 'source_function_' + name)
    return re.sub(r'\s+', ' ', match[1])
reset = body('ResetFameChecker')
check('fameChecker[FAMECHECKER_OAK].pickState = FCPICKSTATE_COLORED;' in reset and 'flavorTextFlags = 0;' in reset, 'source_reset_oak_and_no_facts')
collect = body('SetFlavorTextFlagFromSpecialVars')
check('gSpecialVar_0x8004 < NUM_FAMECHECKER_PERSONS && gSpecialVar_0x8005 < 6' in collect and
      'flavorTextFlags |= (1 << gSpecialVar_0x8005);' in collect and 'gSpecialVar_0x8005 = FCPICKSTATE_SILHOUETTE;' in collect,
      'source_fact_bounds_bit_or_and_special_variable_write')
pick = body('UpdatePickStateFromSpecialVar8005')
check('gSpecialVar_0x8005 < 3' in pick and 'gSpecialVar_0x8005 == FCPICKSTATE_NO_DRAW) return;' in pick and
      'FCPICKSTATE_SILHOUETTE' in pick and 'pickState == FCPICKSTATE_COLORED' in pick,
      'source_no_hidden_update_or_colored_regression')
read = body('GetPickModeText')
check('pickState != FCPICKSTATE_COLORED' in read and 'HasUnlockedAllFlavorTextsForCurrentPerson() == TRUE' in read and
      'whichText = NUM_FAMECHECKER_PERSONS;' in read, 'source_colored_and_all_six_personal_message')
order = body('AdjustGiovanniIndexIfBeatenInGym')
check('HasTrainerBeenFought(TRAINER_LEADER_GIOVANNI)' in order and 'a0 == 9' in order and 'return a0 - 1;' in order,
      'source_gym_giovanni_order')
check('u16 pickState:2;' in sources['include/global.h'] and 'u16 flavorTextFlags:12;' in sources['include/global.h'],
      'original_storage_widths')
macro = sources['asm/macros/event.inc']
check('setvar VAR_0x8004, \\person\n\tsetvar VAR_0x8005, \\index\n\tspecial \\function' in macro,
      'source_macro_special_variable_order')
print(json.dumps(dict(checks=len(checks), passed=checks)))
