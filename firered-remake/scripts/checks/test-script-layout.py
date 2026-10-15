#!/usr/bin/env python3
"""Offline checks for the actual import layout and source materialization boundary."""
import ast
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
        entries = ('imports/source/materialize-source.py', 'imports/rebuild-services.py', 'imports/rebuild-postgame.py')
        with tempfile.TemporaryDirectory() as temporary:
            for entry in entries:
                with self.subTest(entry=entry):
                    result = subprocess.run([sys.executable, str(SCRIPTS / entry), '--help'],
                                            cwd=temporary, capture_output=True, text=True)
                    self.assertEqual(0, result.returncode, result.stderr)
                    self.assertIn('usage:', result.stdout)


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
