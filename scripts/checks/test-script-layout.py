#!/usr/bin/env python3
"""Offline checks for the actual import layout and source materialization boundary."""
import ast
import contextlib
import io
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / 'scripts'


def load_materializer():
    path = SCRIPTS / 'imports/source/materialize-source.py'
    spec = importlib.util.spec_from_file_location('source_materializer_test', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def load_resource_entry():
    path = SCRIPTS / 'imports/rebuild-resources.py'
    spec = importlib.util.spec_from_file_location('resource_entry_test', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ScriptLayoutTests(unittest.TestCase):
    def test_only_stable_entry_and_documentation_at_root(self):
        self.assertEqual({'README.md', 'env.sh'}, {p.name for p in SCRIPTS.iterdir() if p.is_file()})
        self.assertFalse(any(p.is_symlink() for p in SCRIPTS.rglob('*')))
        self.assertTrue((SCRIPTS / 'lib/project_paths.py').is_file())

    def test_python_sources_parse(self):
        files = sorted(SCRIPTS.rglob('*.py'))
        self.assertTrue(files)
        for path in files:
            with self.subTest(path=path.relative_to(ROOT)):
                ast.parse(path.read_text(encoding='utf-8'), filename=str(path))

    def test_explicit_script_references_resolve(self):
        for directory in (SCRIPTS, ROOT / 'tools'):
            for path in directory.rglob('*.py'):
                tree = ast.parse(path.read_text(encoding='utf-8'))
                for node in ast.walk(tree):
                    if not isinstance(node, ast.Constant) or not isinstance(node.value, str):
                        continue
                    value = node.value
                    if value.startswith('scripts/') and value.endswith('.py') and '\n' not in value:
                        with self.subTest(caller=path.relative_to(ROOT), target=value):
                            self.assertTrue((ROOT / value).is_file(), value)

    def test_entry_points_do_not_depend_on_working_directory(self):
        entries = ('imports/source/materialize-source.py', 'imports/rebuild-resources.py')
        with tempfile.TemporaryDirectory() as temporary:
            for entry in entries:
                with self.subTest(entry=entry):
                    result = subprocess.run([sys.executable, str(SCRIPTS / entry), '--help'],
                                            cwd=temporary, capture_output=True, text=True)
                    self.assertEqual(0, result.returncode, result.stderr)
                    self.assertIn('usage:', result.stdout)


class ResourceEntryTests(unittest.TestCase):
    def setUp(self):
        self.module = load_resource_entry()
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.raw = self.root / 'raw'
        self.raw.mkdir()
        self.out = self.root / 'generated'

    def arguments(self, *targets):
        return [*targets, '--raw', str(self.raw), '--out', str(self.out)]

    def invoke(self, *arguments):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return self.module.main(list(arguments))

    def test_targets_are_final_resource_producers(self):
        commands = self.module.build_commands(['field-services', 'field-rules', 'world'], self.raw, self.out)
        self.assertEqual(['import-world.py', 'import-early-field-rules.py',
                          'import-world-field-rules.py', 'import-city-services.py',
                          'import-field-services.py', 'item_icon_exports.py'],
                         [Path(command[1]).name for command in commands])
        for command in commands[:-1]:
            self.assertEqual(['--raw', str(self.raw), '--out', str(self.out)], command[2:])
        self.assertEqual(['--generated', str(self.out)], commands[-1][2:])
        for retired in ('rebuild-services.py', 'rebuild-postgame.py'):
            self.assertFalse((SCRIPTS / 'imports' / retired).exists())

    def test_empty_duplicate_unknown_and_overlapping_targets_fail(self):
        for targets in ([], ['world', 'world'], ['all']):
            with self.subTest(targets=targets), self.assertRaises(ValueError):
                self.module.build_commands(targets, self.raw, self.out)
        for out in (self.raw, self.raw / 'generated', self.root):
            with self.subTest(out=out), self.assertRaises(ValueError):
                self.module.build_commands(['field-rules'], self.raw, out)

    def test_check_is_forwarded_to_every_producer(self):
        commands = self.module.build_commands(['world', 'field-rules', 'field-services', 'audio-cues'],
                                               self.raw, self.out, check=True)
        self.assertEqual(6, len(commands))
        self.assertTrue(all(command[-1] == '--check' for command in commands))
        self.assertNotIn('item_icon_exports.py', [Path(command[1]).name for command in commands])

    def test_world_check_uses_read_only_producer(self):
        result = subprocess.CompletedProcess([], 0)
        with patch.object(self.module.subprocess, 'run', return_value=result) as run:
            self.assertEqual(0, self.invoke(*self.arguments('world'), '--check'))
        run.assert_called_once()
        command = run.call_args.args[0]
        self.assertEqual('import-world.py', Path(command[1]).name)
        self.assertEqual('--check', command[-1])
        self.assertFalse(self.out.exists())

    def test_audio_uses_selected_inputs_and_final_output(self):
        pack, cries = self.root / 'kept.pack', self.root / 'kept-cries.bin'
        commands = self.module.build_commands(['audio-cues'], self.raw, self.out, pack=pack, cries=cries)
        self.assertEqual(['--reference', str(self.raw), '--pack', str(pack), '--cries', str(cries),
                          '--output', str(self.out / 'audio/cues.json')], commands[0][2:])
        with self.assertRaises(ValueError):
            self.module.build_commands(['field-rules'], self.raw, self.out, pack=pack)
        with self.assertRaises(ValueError):
            self.module.build_commands(['audio-cues'], self.raw, self.out, pack=self.out / 'audio/cues.json')

    def test_missing_audio_input_fails_before_other_targets_write(self):
        with patch.object(self.module.subprocess, 'run') as run:
            with self.assertRaises(SystemExit) as error:
                self.invoke(*self.arguments('world', 'audio-cues'))
        self.assertEqual(2, error.exception.code)
        run.assert_not_called()
        self.assertFalse(self.out.exists())

    def test_missing_raw_or_producer_is_an_error(self):
        commands = self.module.build_commands(['field-rules'], self.raw, self.out)
        with self.assertRaises(FileNotFoundError):
            self.module.preflight(commands, self.root / 'missing-raw')
        with self.assertRaises(FileNotFoundError):
            self.module.preflight([[sys.executable, str(self.root / 'missing-producer.py')]], self.raw)

    def test_dry_run_does_not_execute_or_write(self):
        with patch.object(self.module.subprocess, 'run') as run:
            self.assertEqual(0, self.invoke(*self.arguments('field-rules'), '--dry-run'))
        run.assert_not_called()
        self.assertFalse(self.out.exists())

    def test_failure_stops_run_and_preserves_return_code(self):
        result = subprocess.CompletedProcess([], 7)
        with patch.object(self.module.subprocess, 'run', return_value=result) as run:
            self.assertEqual(7, self.invoke(*self.arguments('field-rules', 'field-services')))
        run.assert_called_once()
        self.assertEqual(self.module.PROJECT_ROOT, run.call_args.kwargs['cwd'])

    def test_success_runs_every_selected_producer(self):
        result = subprocess.CompletedProcess([], 0)
        with patch.object(self.module.subprocess, 'run', return_value=result) as run:
            self.assertEqual(0, self.invoke(*self.arguments('field-rules', 'field-services'), '--check'))
        self.assertEqual(4, run.call_count)
        self.assertTrue(all(call.args[0][-1] == '--check' for call in run.call_args_list))


class MaterializeSourceTests(unittest.TestCase):
    def setUp(self):
        self.module = load_materializer()
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.raw = self.root / 'raw'
        self.recipe = self.root / 'recipe.json'
        config = json.loads((SCRIPTS / 'recipes/config/source.json').read_text())
        self.commit = config['source']['sourceCommit']
        self.repository = config['source']['repository']

    def recipe_for(self, paths):
        self.recipe.write_text(json.dumps({'commit': self.commit, 'repository': self.repository,
                                          'files': [{'path': p} for p in paths]}))

    def test_cache_and_existing_inputs_are_offline(self):
        self.recipe_for(['a/source.inc'])
        cache = self.root / 'cache'
        (cache / 'a').mkdir(parents=True)
        (cache / 'a/source.inc').write_bytes(b'local source')
        with patch('urllib.request.urlopen', side_effect=AssertionError('Unexpected network')):
            self.assertEqual(1, self.module.materialize(self.recipe, self.raw, cache=cache))
            self.assertEqual(1, self.module.materialize(self.recipe, self.raw))
        self.assertEqual(b'local source', (self.raw / 'a/source.inc').read_bytes())

    def test_git_reads_the_selected_revision(self):
        self.recipe_for(['src/source.c'])
        reference = self.root / 'reference'
        with patch('subprocess.check_output', return_value=b'pinned blob') as read:
            self.assertEqual(1, self.module.materialize(self.recipe, self.raw, reference=reference))
        read.assert_called_once_with(['git', '-C', str(reference), 'show', self.commit + ':src/source.c'])

    def test_missing_input_fails_before_any_write(self):
        self.recipe_for(['available', 'missing'])
        cache = self.root / 'cache'
        cache.mkdir()
        (cache / 'available').write_bytes(b'available')
        with self.assertRaises(FileNotFoundError):
            self.module.materialize(self.recipe, self.raw, cache=cache)
        self.assertFalse(self.raw.exists())

    def test_unsafe_and_duplicate_selections_are_rejected(self):
        for paths in (['../escape'], ['/absolute'], ['a\\b'], ['same', 'same'], []):
            with self.subTest(paths=paths), self.assertRaises(ValueError):
                self.recipe_for(paths)
                self.module.materialize(self.recipe, self.raw)


if __name__ == '__main__':
    unittest.main()
