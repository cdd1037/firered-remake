#!/usr/bin/env python3
"""Regenerate trainer bindings and story contributions from retained source inputs."""
import importlib.util
import json
import re
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from import_check_fixtures import snapshot_tree
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from shared_trainers import read_trainers, trainer_json
from trainer_import import TextSources, generate, natural_moves, selected_trainers

RAW = ROOT / 'content/raw/pokefirered/037335f4'
REGIONS = ('bill', 'vermilion', 'route11', 'rocktunnel', 'ssanne',
           'celadon-gym', 'vermilion-gym', 'fuchsia-gym', 'pokemon-tower')


def load(relative):
    spec = importlib.util.spec_from_file_location(Path(relative).stem, ROOT / 'scripts' / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class TrainerImportTests(unittest.TestCase):
    def test_early_bindings_compile_without_map_dependency_documents(self):
        early = load('imports/trainers/import-early-trainer-catalogs.py')
        cerulean = load('imports/trainers/import-cerulean-trainer-catalog.py')
        original = Path.read_text
        def raw_only(path, *args, **kwargs):
            if 'content/generated/maps' in path.as_posix():
                raise AssertionError('Trainer compilation read a retired map stage')
            return original(path, *args, **kwargs)
        with patch.object(Path, 'read_text', raw_only):
            documents = [(region, early.compile_catalog(ROOT, region)) for region in early.COUNTS]
            documents.append(('cerulean/trainers', cerulean.compile_catalog(ROOT)))
            for name, document in documents:
                actual = trainer_json(ROOT / 'content/generated', document, check=True)
                self.assertEqual((ROOT / f'content/generated/data/trainer-battles/{name}.json').read_bytes(), actual)

    def test_route4_admission_preserves_actual_call_and_shared_party(self):
        early = load('imports/trainers/import-early-trainer-catalogs.py')
        document = early.compile_catalog(ROOT, 'route3')
        battle = next(row for row in document['battles'] if row['trainer']['value'] == 'pret:TRAINER_LASS_CRISSY')
        self.assertEqual(battle['scriptEntry'], 'Route4_EventScript_Crissy')
        self.assertEqual(battle['postBattleText'], 'Route4_Text_CrissyPostBattle')
        self.assertEqual(battle['mode'], 0)
        actor = next(row for row in json.loads((RAW / 'data/maps/Route4/map.json').read_text())['object_events']
                     if row['script'] == battle['scriptEntry'])
        self.assertEqual((actor['x'], actor['y'], actor['trainer_type']), (75, 3, 'TRAINER_TYPE_NORMAL'))
        trainer = next(row for row in document['trainers'] if row['id'] == battle['trainer'])
        self.assertEqual(trainer, read_trainers(ROOT / 'content/generated')[trainer['id']['value']])
        original = Path.read_text
        for replacement in ('trainerbattle_rematch', 'trainerbattle_double'):
            with self.subTest(replacement=replacement):
                def mutate(path, *args, **kwargs):
                    text = original(path, *args, **kwargs)
                    if path == RAW / 'data/scripts/trainers.inc':
                        text = text.replace('trainerbattle_single TRAINER_LASS_CRISSY,', replacement + ' TRAINER_LASS_CRISSY,')
                    return text
                with patch.object(Path, 'read_text', mutate), self.assertRaises(ValueError):
                    early.compile_catalog(ROOT, 'route3')
        # A changed original operand must be reflected, not concealed by a handwritten binding.
        def mutate_post(path, *args, **kwargs):
            text = original(path, *args, **kwargs)
            return text.replace('Route4_Text_CrissyPostBattle', 'Route4_Text_Changed') if path == RAW / 'data/scripts/trainers.inc' else text
        with patch.object(Path, 'read_text', mutate_post):
            changed = early.compile_catalog(ROOT, 'route3')
        self.assertEqual(next(row for row in changed['battles'] if row['trainer'] == battle['trainer'])['postBattleText'], 'Route4_Text_Changed')

    def test_all_regional_cli_outputs_and_shared_contributions(self):
        with tempfile.TemporaryDirectory() as temporary:
            generated = Path(temporary) / 'generated'
            for region in REGIONS:
                with self.subTest(region=region):
                    recipe = ROOT / f'scripts/recipes/trainers/{region}.json'
                    relative = json.loads(recipe.read_text())['output']
                    target = generated / relative
                    command = [sys.executable, str(ROOT / 'scripts/imports/trainers/import-trainer-battles.py'),
                               '--recipe', str(recipe), '--generated-root', str(generated), '--out', str(target)]
                    subprocess.run(command, cwd=temporary, check=True, capture_output=True)
                    self.assertTrue(target.is_file(), target)
                    self.assertEqual((ROOT / 'content/generated' / relative).read_bytes(),
                                     (generated / relative).read_bytes())
                    before = snapshot_tree(generated)
                    subprocess.run([*command, '--check'], cwd=temporary, check=True, capture_output=True)
                    self.assertEqual(before, snapshot_tree(generated))
            self.assertFalse(any(path.is_dir() and path.suffix in ('.json', '.png')
                                 for path in generated.rglob('*')))
            shared = read_trainers(generated)
            current = read_trainers(ROOT / 'content/generated')
            self.assertTrue(shared)
            self.assertEqual(shared, {identity: current[identity] for identity in shared})

    def test_story_contributions_preserve_bindings_and_shared_definitions(self):
        for region in ('four-island', 'five-island', 'six-island', 'seven-island',
                       'fighting-dojo', 'viridian-gym', 'seafoam', 'victory-road', 'pokemon-league', 'league-rematch', 'sevii-ruby'):
            with self.subTest(region=region):
                module = load(f'imports/story/import-{region}-rules.py')
                args = [RAW] if region in ('sevii-ruby', 'fighting-dojo', 'seafoam') else [RAW, ROOT]
                result = module.generate(*args)
                docs = result[0] if isinstance(result, tuple) else result
                document = docs['trainers.json']
                self.assertTrue(document['trainers'])
                actual = trainer_json(ROOT / 'content/generated', document, check=True)
                self.assertEqual((ROOT / f'content/generated/data/regions/{region}/trainers.json').read_bytes(), actual)

    def test_fuchsia_route_cli_preserves_source_occurrences_and_shared_parties(self):
        with tempfile.TemporaryDirectory() as temporary:
            generated = Path(temporary) / 'generated'
            command = [sys.executable, str(ROOT / 'scripts/imports/trainers/import-fuchsia-route-trainers.py'),
                       '--raw', str(RAW), '--out', str(generated)]
            subprocess.run(command, check=True, capture_output=True)
            relative = 'data/pokemon/fuchsia-route/trainers.json'
            self.assertEqual((ROOT / 'content/generated' / relative).read_bytes(), (generated / relative).read_bytes())
            current, rebuilt = read_trainers(ROOT / 'content/generated'), read_trainers(generated)
            self.assertTrue(rebuilt)
            self.assertEqual(rebuilt, {identity: current[identity] for identity in rebuilt})
            before = snapshot_tree(generated)
            subprocess.run([*command, '--check'], check=True, capture_output=True)
            self.assertEqual(before, snapshot_tree(generated))

    def test_vs_seeker_raw_cli_preserves_all_parties_and_occurrences(self):
        subprocess.run([sys.executable, str(ROOT / 'scripts/imports/trainers/import-vs-seeker-parties.py'),
                        '--raw-root', str(RAW), '--dependency-root', str(ROOT),
                        '--rules-root', str(ROOT), '--check'], check=True, capture_output=True)

    def test_route16_double_team_uses_shared_parser(self):
        from field_service_features import north_trainers
        from world_source import WorldSource
        document = north_trainers(WorldSource(RAW))
        self.assertEqual(1, len(document['trainers']))
        self.assertTrue(document['trainers'][0]['doubleBattle'])
        self.assertEqual(2, len(document['battles']))
        trainer_json(ROOT / 'content/generated', document, check=True)

    def test_all_shared_party_shapes_names_and_move_slots(self):
        current = read_trainers(ROOT / 'content/generated')
        rebuilt = generate(RAW, current)
        self.assertEqual(current, {row['id']['value']: row for row in rebuilt})
        self.assertEqual({0, 1, 2, 3}, {row['partyFlags'] for row in rebuilt})
        self.assertTrue(any(row['doubleBattle'] for row in rebuilt))
        self.assertTrue(any("'" in member['speciesName'] for row in rebuilt for member in row['party']))
        self.assertTrue(any(slot['value'] == 'pret:MOVE_NONE' for row in rebuilt
                            for member in row['party'] for slot in member['customMoves']))

    def test_selection_order_is_not_numeric_catalog_order(self):
        identities = list(read_trainers(ROOT / 'content/generated'))[:3][::-1]
        result = selected_trainers(lambda path: (RAW / path).read_text(), [x[5:] for x in identities])
        self.assertEqual(identities, [row['id']['value'] for row in result])

    def test_integrated_item_catalog_trainer_bindings(self):
        cases = [('rocket-hideout-catalog', 'data/trainer-battles/rocket-hideout/trainers.json'),
                 ('silph-catalogs', 'data/regions/silph/trainers.json'),
                 ('saffron-gym-catalogs', 'data/regions/saffron-gym/trainers.json')]
        for stem, relative in cases:
            with self.subTest(stem=stem):
                module = load(f'imports/items/import-{stem}.py')
                sources = TextSources(lambda path: (RAW / path).read_text())
                if not module.TRAINERS:
                    module.TRAINERS = tuple(symbol[8:] for path in module.SCRIPT_PATHS for symbol in
                        re.findall(r'trainerbattle_(?:single|no_intro) (TRAINER_\w+)', sources[path].decode()))
                document = module.trainer_convert(sources)
                actual = trainer_json(ROOT / 'content/generated', document, check=True)
                self.assertEqual((ROOT / 'content/generated' / relative).read_bytes(), actual)

    def test_late_rival_special_bindings(self):
        module = load('imports/story/import-league-rival-rules.py')
        document = module.extract(RAW)
        actual = trainer_json(ROOT / 'content/generated', document, check=True)
        self.assertEqual((ROOT / 'content/generated/data/trainer-battles/league-rival-rules.json').read_bytes(), actual)

    def test_early_prepared_catalogs_current_layout(self):
        module = load('imports/trainers/import-early-trainer-catalogs.py')
        result = subprocess.run(
            [sys.executable, str(ROOT / 'scripts/imports/trainers/import-early-trainer-catalogs.py'), '--check'],
            check=True, capture_output=True, text=True)
        self.assertTrue(json.loads(result.stdout)['checked'])
        with tempfile.TemporaryDirectory() as temporary:
            isolated = Path(temporary)
            raw = isolated / 'content/raw/pokefirered/037335f4'
            raw.parent.mkdir(parents=True)
            raw.symlink_to(RAW, target_is_directory=True)
            # Exercise the writer from pinned raw, with no generated maps or catalogs.
            command = [sys.executable, '-c',
                       "import runpy,sys; from pathlib import Path; "
                       "m=runpy.run_path(sys.argv.pop(1)); "
                       "m['main'].__globals__['ROOT']=Path(sys.argv.pop(1)); m['main']()",
                       str(ROOT / 'scripts/imports/trainers/import-early-trainer-catalogs.py'), temporary]
            subprocess.run(command, check=True, capture_output=True)
            for region in module.COUNTS:
                relative = Path('content/generated/data/trainer-battles') / (region + '.json')
                self.assertEqual((ROOT / relative).read_bytes(), (isolated / relative).read_bytes())
            rebuilt = read_trainers(isolated / 'content/generated')
            current = read_trainers(ROOT / 'content/generated')
            self.assertTrue(rebuilt)
            self.assertEqual(rebuilt, {identity: current[identity] for identity in rebuilt})
            subprocess.run([*command, '--check'], check=True, capture_output=True)

    def test_lab_custom_early_rival_dispatch(self):
        with tempfile.TemporaryDirectory() as temporary:
            generated = Path(temporary) / 'generated'
            target = generated / 'data/trainer-battles/lab'
            command = [sys.executable, str(ROOT / 'scripts/imports/trainers/import-lab-trainers.py'),
                       '--raw-root', str(RAW), '--out-root', str(target)]
            subprocess.run(command, check=True, capture_output=True)
            actual = json.loads((target / 'lab-trainers.json').read_bytes())
            expected = json.loads((ROOT / 'content/generated/data/trainer-battles/lab/lab-trainers.json').read_bytes())
            self.assertEqual(expected, actual)
            current = read_trainers(ROOT / 'content/generated')
            rebuilt = read_trainers(generated)
            self.assertEqual(rebuilt, {identity: current[identity] for identity in rebuilt})
            self.assertFalse((target.parent / 'data/trainers.json').exists())
            before = {path: path.read_bytes() for path in generated.rglob('*') if path.is_file()}
            subprocess.run([*command, '--check'], check=True, capture_output=True)
            self.assertEqual(before, {path: path.read_bytes() for path in generated.rglob('*') if path.is_file()})

    def test_lost_cave_and_route8_cli_bindings(self):
        for stem, source_flag, output_flag, relative in (
                ('lost-cave-encounter-rules', '--reference-root', '--project-root', 'data/encounters/lost-cave.json'),
                ('route8-twins', '--source', '--output-root', 'data/trainer-battles/route8-twins.json')):
            with self.subTest(stem=stem), tempfile.TemporaryDirectory() as temporary:
                subprocess.run([sys.executable, str(ROOT / f'scripts/imports/story/import-{stem}.py'),
                                source_flag, str(RAW), output_flag, temporary], check=True, capture_output=True)
                generated = Path(temporary) / 'content/generated'
                self.assertEqual((ROOT / 'content/generated' / relative).read_bytes(),
                                 (generated / relative).read_bytes())
                current, rebuilt = read_trainers(ROOT / 'content/generated'), read_trainers(generated)
                self.assertEqual(rebuilt, {identity: current[identity] for identity in rebuilt})

    def test_source_learnset_uses_firered_branch(self):
        sources = {
            'src/data/pokemon/level_up_learnset_pointers.h': '    [SPECIES_TEST] = sTest,\n',
            'src/data/pokemon/level_up_learnsets.h': 'static const u16 sTest[] = {\n#if defined(FIRERED)\n    LEVEL_UP_MOVE(1, MOVE_TACKLE),\n#elif defined(LEAFGREEN)\n    LEVEL_UP_MOVE(1, MOVE_GROWL),\n#endif\n    LEVEL_UP_END\n};\n',
        }
        self.assertEqual(['MOVE_TACKLE'], natural_moves(sources.__getitem__, 'SPECIES_TEST', 5))
        sources['src/data/pokemon/level_up_learnsets.h'] = sources['src/data/pokemon/level_up_learnsets.h'].replace('#endif', '#unsupported')
        with self.assertRaises(ValueError):
            natural_moves(sources.__getitem__, 'SPECIES_TEST', 5)

    def test_missing_inputs_fail_before_output(self):
        importer = load('imports/trainers/import-trainer-battles.py')
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.assertRaises(FileNotFoundError):
                importer.run(ROOT / 'scripts/recipes/trainers/bill.json', root / 'raw',
                             generated_root=root / 'generated')
            self.assertFalse((root / 'generated').exists())

    def test_unsafe_output_and_empty_or_duplicate_selection_fail(self):
        from trainer_import import convert_battles
        for trainers in ([], ['LASS_KAY', 'LASS_KAY']):
            with self.assertRaisesRegex(ValueError, 'nonempty and unique'):
                convert_battles({'trainers': trainers}, {})
        importer = load('imports/trainers/import-trainer-battles.py')
        with self.assertRaisesRegex(ValueError, 'generated-root'):
            importer.run(ROOT / 'scripts/recipes/trainers/bill.json', RAW, out=Path('/tmp/custom-trainers.json'))


if __name__ == '__main__':
    unittest.main()
