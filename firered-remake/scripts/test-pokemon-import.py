#!/usr/bin/env python3
"""Focused input, parser and stateless conversion regressions. No generated writes."""
import importlib.util
import io
import json
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from PIL import Image

import pokemon_import as pokemon
import pokemon_import_io as inputs

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def importer(region):
    spec = importlib.util.spec_from_file_location('test_' + region, ROOT / f'scripts/import-{region}-pokemon.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SourceInputTests(unittest.TestCase):
    def test_invalid_relative_paths(self):
        for path in ('', '.', '..', '../a', '/a', 'a/../b', 'a//b', 'a\\b'):
            with self.subTest(path=path), self.assertRaises(ValueError):
                inputs.safe(Path(tempfile.gettempdir()), path)

    def test_missing_source_is_not_empty_success(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(FileNotFoundError):
                inputs.read_sources({'commit': COMMIT, 'files': [{'path': 'missing'}]}, Path(temp))

    def test_empty_and_duplicate_sources_rejected(self):
        for rows in ([], [{'path': 'same'}, {'path': 'same'}]):
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                inputs.read_sources({'commit': COMMIT, 'files': rows}, RAW)

    def test_source_and_output_symlinks_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'real').mkdir()
            (root / 'link').symlink_to(root / 'real', target_is_directory=True)
            for path in ('link', 'link/file'):
                with self.subTest(path=path), self.assertRaises(ValueError):
                    inputs.safe(root, path)

    def test_all_output_targets_preflight_before_write(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'first').write_bytes(b'original')
            (root / 'blocked').write_bytes(b'file')
            with self.assertRaises(ValueError):
                inputs.write_files(root, {'first': b'replaced', 'blocked/child': b'no'})
            self.assertEqual((root / 'first').read_bytes(), b'original')

    def test_overlapping_source_output_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with self.assertRaises(ValueError):
                inputs.import_region(root, root / 'out', recipe={}, convert=lambda *_: {})

    def test_recipe_duplicate_members_and_version_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'recipe.json'
            for text in ('{"commit":"a","commit":"b"}', '{"commit":"different"}'):
                path.write_text(text)
                with self.subTest(text=text), self.assertRaises(ValueError):
                    inputs.load_recipe(path, commit=COMMIT)

    def test_git_reads_selected_commit_not_worktree_or_head(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            def git(*args):
                return subprocess.check_output(['git', '-C', str(repo), *args], stderr=subprocess.DEVNULL).decode().strip()
            git('init')
            (repo / 'source.txt').write_text('pinned')
            git('add', 'source.txt')
            git('-c', 'user.name=Import test', '-c', 'user.email=import@example.invalid', 'commit', '-m', 'fixture')
            commit = git('rev-parse', 'HEAD')
            (repo / 'source.txt').write_text('working tree')
            self.assertEqual(inputs.read_source(repo, 'source.txt', commit=commit, repository=repo), b'pinned')


class PokemonConversionTests(unittest.TestCase):
    def test_palette_expansion_rules_remain_distinct(self):
        data = ('JASC-PAL\n0100\n16\n' + '32 0 0\n' * 16).encode()
        colors, words = pokemon.battle_palette(data)
        self.assertEqual(colors[0], (32, 0, 0))
        self.assertEqual(words[0], 4)
        self.assertEqual(pokemon.icon_palette(data)[0], (33, 0, 0))

    def test_native_sprite_decode_uses_original_indices(self):
        path = 'graphics/pokemon/nidoran_f/front.png'
        original = (RAW / path).read_bytes()
        indices = pokemon.battle_indices(original)
        colors, _ = pokemon.battle_palette((RAW / 'graphics/pokemon/nidoran_f/shiny.pal').read_bytes())
        expected = pokemon.rgba_pixels(indices, colors)
        with Image.open(io.BytesIO(pokemon.png(64, 64, expected))) as image:
            self.assertEqual(image.size, (64, 64))
            self.assertEqual(image.convert('RGBA').tobytes(), expected)
        self.assertEqual(pokemon.source_symbol('NidoranF'), 'NIDORAN_F')
        self.assertEqual(pokemon.source_stem('NidoranM'), 'nidoran_m')

    def test_firered_learnset_branch_and_source_positions(self):
        text = ('prefix\nstatic const u16 test[] = {\n'
                '    LEVEL_UP_MOVE(1, MOVE_TACKLE),\n#if defined(FIRERED)\n'
                '    LEVEL_UP_MOVE(5, MOVE_GROWL),\n#elif defined(LEAFGREEN)\n'
                '    LEVEL_UP_MOVE(5, MOVE_LEER),\n#endif\n    LEVEL_UP_END\n};')
        # The source parser includes the terminator's newline in the captured body.
        match = re.search(r'test\[\] = \{\n(.*?)\};', text, re.S)
        rows = pokemon.learn_rows(text, match)
        self.assertEqual([(int(row[1]), row[2]) for row in rows], [(1, 'MOVE_TACKLE'), (5, 'MOVE_GROWL')])
        self.assertTrue(all(text[row.start():row.end()] == row[0] for row in rows))
        broken = text.replace('    LEVEL_UP_END\n', '')
        with self.assertRaises(ValueError):
            pokemon.learn_rows(broken, re.search(r'test\[\] = \{\n(.*?)\};', broken, re.S))

    def test_region_conversions_are_independent_and_repeatable(self):
        first = importer('forest')
        first_lock = first.load_lock()
        first_sources = inputs.read_sources(first_lock, RAW)
        initial = first.convert(first_lock, first_sources)
        for region in ('route3', 'bill', 'rocktunnel'):
            module = importer(region)
            lock = module.load_lock()
            outputs = module.convert(lock, inputs.read_sources(lock, RAW))
            catalog = json.loads(outputs['catalog.json'])
            art = json.loads(outputs['battle-art.json'])
            self.assertEqual(catalog['profile'], module.PROFILE)
            self.assertEqual(art['profile'], module.PROFILE)
            self.assertEqual(len(art['sprites']), 4 * len(module.ART_SPECIES))
            self.assertEqual({s['id']['value'] for s in catalog['species']},
                {'pret:SPECIES_' + pokemon.source_symbol(name) for name in module.NEW_SPECIES})
            self.assertFalse(hasattr(module, 'base'))
        self.assertEqual(first.convert(first_lock, first_sources), initial)

    def test_supplement_requires_no_generated_proof_inputs(self):
        module = importer('bill')
        lock = module.load_lock()
        sources = inputs.read_sources(lock, RAW)
        outputs = pokemon.supplement(lock, sources, profile='test-bill', species=module.ART_SPECIES,
            weight_species=('SLOWPOKE',), natural_level_limit=25)
        doc = json.loads(outputs['supplement.json'])
        self.assertEqual(doc['profile'], 'test-bill')
        self.assertEqual(len(doc['weights']), 1)
        self.assertEqual(doc['weights'][0]['sourceSpeciesId'], 79)
        self.assertEqual(doc['weights'][0]['weightHectograms'], 360)


if __name__ == '__main__':
    unittest.main()
