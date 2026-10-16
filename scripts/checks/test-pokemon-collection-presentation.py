#!/usr/bin/env python3
"""Source-pixel and regeneration checks for shared collection presentation additions."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from generated_output_compare import generated_output_matches
from pokemon_import_io import load_recipe, read_sources, write_files

spec = importlib.util.spec_from_file_location('collection_art', ROOT / 'scripts/imports/presentation/import-pokemon-collection-presentation.py')
compiler = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compiler)


class PokemonCollectionPresentationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.recipe = load_recipe(compiler.RECIPE, commit=compiler.COMMIT)
        cls.sources = read_sources(cls.recipe, ROOT / 'content/raw/pokefirered/037335f4')

    def test_generated_outputs_match_independent_regeneration(self):
        output = compiler.convert(self.recipe, self.sources, ROOT)
        self.assertTrue(output)
        self.assertEqual(output, compiler.convert(self.recipe, self.sources, ROOT))
        for path, data in output.items():
            self.assertTrue(generated_output_matches(ROOT / compiler.BASE / path, data), path)
        with tempfile.TemporaryDirectory() as temp:
            write_files(Path(temp), output)
            for path, data in output.items():
                self.assertTrue(generated_output_matches(Path(temp) / path, data))

    def test_dunsparce_icon_and_footprint_match_original_palette_pixels(self):
        output = compiler.convert(self.recipe, self.sources, ROOT)
        def palette(path):
            rows = [tuple(map(int, line.split())) for line in self.sources[path].decode().splitlines()[3:]]
            return [tuple((value >> 3) * 8 + (value >> 5) for value in row) for row in rows]
        with Image.open(io.BytesIO(self.sources['graphics/pokemon/dunsparce/icon.png'])) as source:
            self.assertEqual((32, 64), source.size)
            indices = source.crop((0, 0, 32, 32)).tobytes()
        colors = palette('graphics/pokemon/icon_palettes/icon_palette_2.pal')
        expected = bytes(channel for index in indices for channel in (*colors[index], 255 if index else 0))
        with Image.open(io.BytesIO(output['dunsparce-icon.png'])) as actual:
            self.assertEqual((32, 32), actual.size)
            self.assertEqual(expected, actual.convert('RGBA').tobytes())
        with Image.open(io.BytesIO(self.sources['graphics/pokemon/dunsparce/footprint.png'])) as source:
            indices = [int(pixel == 0) for pixel in source.convert('L').tobytes()]
        colors = palette('graphics/pokedex/kanto_dex_bgpals.pal')
        expected = bytes(channel for index in indices for channel in (*colors[index], 255 if index else 0))
        with Image.open(io.BytesIO(output['dunsparce-footprint.png'])) as actual:
            self.assertEqual((16, 16), actual.size)
            self.assertEqual(expected, actual.convert('RGBA').tobytes())
        data = json.loads(output['catalog.json'])
        row = next(row for row in data['entries'] if row['species']['value'] == 'pret:SPECIES_DUNSPARCE')
        self.assertEqual(206, row['sourceSpeciesId'])
        self.assertEqual(self.recipe['species'][0]['portraitPath'], row['portrait']['texturePath'])
        self.assertNotIn('dunsparce-front-normal.png', output)

    def test_new_battle_sprites_match_original_indexed_pixels(self):
        output = compiler.convert(self.recipe, self.sources, ROOT)
        sprites = json.loads(output['catalog.json'])['sprites']
        for selection in self.recipe['species']:
            if 'portraitPath' in selection:
                continue
            symbol = selection['symbol']
            rows = [row for row in sprites if row['species']['value'] == 'pret:SPECIES_' + symbol]
            self.assertEqual({(side, shiny) for side in ('front', 'back') for shiny in (False, True)},
                             {(row['side'], row['shiny']) for row in rows})
            for row in rows:
                stem, side = symbol.lower(), row['side']
                variant = 'shiny' if row['shiny'] else 'normal'
                colors = [tuple((int(c) // 8) * 255 // 31 for c in line.split()) for line in
                          self.sources[f'graphics/pokemon/{stem}/{variant}.pal'].decode().splitlines()[3:]]
                with Image.open(io.BytesIO(self.sources[f'graphics/pokemon/{stem}/{side}.png'])) as original:
                    indices = original.tobytes()
                expected = bytes(c for index in indices for c in (*colors[index], 255 if index else 0))
                with Image.open(io.BytesIO(output[f'{stem}-{side}-{variant}.png'])) as actual:
                    self.assertEqual(expected, actual.convert('RGBA').tobytes())

    def test_moltres_menu_pixels_and_flying_battle_coordinates_match_source(self):
        output = compiler.convert(self.recipe, self.sources, ROOT)
        def palette(path):
            return [tuple((int(c) >> 3) * 8 + (int(c) >> 5) for c in line.split())
                    for line in self.sources[path].decode().splitlines()[3:]]
        for kind, size, palette_path in (
            ('icon', 32, 'graphics/pokemon/icon_palettes/icon_palette_0.pal'),
            ('footprint', 16, 'graphics/pokedex/kanto_dex_bgpals.pal')):
            with Image.open(io.BytesIO(self.sources[f'graphics/pokemon/moltres/{kind}.png'])) as source:
                indices = source.crop((0, 0, 32, 32)).tobytes() if kind == 'icon' else \
                    [int(pixel == 0) for pixel in source.convert('L').tobytes()]
            colors = palette(palette_path)
            expected = bytes(channel for index in indices for channel in (*colors[index], 255 if index else 0))
            with Image.open(io.BytesIO(output[f'moltres-{kind}.png'])) as actual:
                self.assertEqual((size, size), actual.size)
                self.assertEqual(expected, actual.convert('RGBA').tobytes())
        data = json.loads(output['catalog.json'])
        entry = next(row for row in data['entries'] if row['species']['value'] == 'pret:SPECIES_MOLTRES')
        self.assertEqual(146, entry['sourceSpeciesId'])
        self.assertEqual(compiler.BASE + 'moltres-front-normal.png', entry['portrait']['texturePath'])
        sprites = [row for row in data['sprites'] if row['species']['value'] == 'pret:SPECIES_MOLTRES']
        self.assertEqual(4, len(sprites))
        for row in sprites:
            self.assertEqual((144, 9) if row['side'] == 'front' else (40, 52), (row['x'], row['y']))

    def test_empty_duplicate_and_unowned_species_rejected(self):
        for selection in ([], self.recipe['species'] * 2,
                          [dict(symbol='TREECKO', graphicsName='Treecko', portraitPath='unused')]):
            recipe = dict(self.recipe, species=selection)
            with self.subTest(selection=selection), self.assertRaises(ValueError):
                compiler.convert(recipe, self.sources, ROOT)

    def test_source_bindings_and_missing_inputs_are_not_silent_success(self):
        sources = dict(self.sources)
        sources['src/pokemon_icon.c'] = sources['src/pokemon_icon.c'].replace(b'gMonIcon_Dunsparce,', b'gMonIcon_Rattata,')
        with self.assertRaises(ValueError):
            compiler.convert(self.recipe, sources, ROOT)
        sources = dict(self.sources)
        del sources['graphics/pokemon/dunsparce/footprint.png']
        with self.assertRaises(KeyError):
            compiler.convert(self.recipe, sources, ROOT)

    def test_reused_portrait_pixels_and_path_are_verified(self):
        recipe = copy.deepcopy(self.recipe)
        recipe['species'][0]['portraitPath'] = 'content/generated/../raw/portrait.png'
        with self.assertRaises(ValueError):
            compiler.convert(recipe, self.sources, ROOT)
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for path in ('data/game-pokemon.json', 'content/generated/data/pokemon/canonical/catalog.json'):
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((ROOT / path).read_bytes())
            portrait = root / self.recipe['species'][0]['portraitPath']
            portrait.parent.mkdir(parents=True, exist_ok=True)
            Image.new('RGBA', (64, 64), (0, 0, 0, 0)).save(portrait)
            with self.assertRaises(ValueError):
                compiler.convert(self.recipe, self.sources, root)


if __name__ == '__main__':
    unittest.main()
