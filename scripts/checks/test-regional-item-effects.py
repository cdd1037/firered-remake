#!/usr/bin/env python3
"""Shared effect, pickup and icon conversion checks against real raw inputs."""
import importlib.util
import io
import json
from pathlib import Path
import re
import sys
import tempfile
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from item_effects import read_effects, read_effects_text
from item_source import ItemSource
from presentation_source import Source
from PIL import Image

RAW = ROOT / 'content/raw/pokefirered/037335f4'


class SharedItemEffectsTests(unittest.TestCase):
    def test_source_macro_and_signed_effects(self):
        effects = read_effects(RAW)
        self.assertEqual([0, 0, 0, 64, 68, 224, 253, 5, 3, 2], effects['ITEM_RARE_CANDY'])
        self.assertEqual([0, 0, 0, 0, 0, 232, 10, 5, 3, 2], effects['ITEM_CALCIUM'])
        self.assertEqual([0, 16, 0, 0, 0, 96, 1, 1], effects['ITEM_X_DEFEND'])

    def test_bad_source_fails(self):
        binding = '\n[ITEM_POTION - ITEM_POTION] = sItemEffect_Potion,'
        valid = 'static const u8 sItemEffect_Potion[1] = {[0] = 1,};'
        for source in ['', valid, valid + binding * 2,
                       valid.replace('[0] = 1', '[1] = 1') + binding,
                       valid.replace('[0] = 1', '[0] = 1, [0] = 2') + binding,
                       valid.replace('= 1,', '= dangerous(),') + binding,
                       valid.replace('= 1,', '= 256,') + binding,
                       valid + binding.replace('= sItemEffect_Potion', '= sItemEffect_Missing')]:
            with self.subTest(source=source), self.assertRaises(ValueError):
                read_effects_text('', source)

    def test_real_regional_converters(self):
        for region in ('bill', 'mtmoon', 'ssanne', 'route11', 'rocktunnel', 'rocket-hideout'):
            with self.subTest(region=region):
                regional = region in ('ssanne', 'route11')
                path = ROOT / 'scripts/imports/items' / ('import-regional-items.py' if regional else f'import-{region}-items.py')
                spec = importlib.util.spec_from_file_location(region, path)
                module = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(module)
                if regional:
                    source = Source(ROOT)
                    outputs = module.convert(source, region)
                    data = source.raw
                else:
                    recipe = json.loads((ROOT / f'scripts/recipes/items/{region}-items.lock.json').read_bytes())
                    data = {entry['path']: (RAW / entry['path']).read_bytes() for entry in recipe['files']}
                    outputs = module.outputs(recipe, data, ROOT) if region == 'rocket-hideout' else module.convert(recipe, data)
                doc = json.loads(outputs[region + '-items.json'])
                self.assertTrue(doc['items'])
                pickup_doc = json.loads(outputs['rocket-hideout-pickups.json']) if region == 'rocket-hideout' else doc
                self.assertTrue(pickup_doc['pickups'])
                effects = read_effects(RAW)
                admitted = [row for row in doc['items'] if row['effectBytes']]
                self.assertTrue(admitted)
                for row in admitted:
                    self.assertEqual(effects[row['metadata']['item'].removeprefix('pret:')], row['effectBytes'])
                # Unadmitted effects must stay empty despite an original array.
                if region == 'ssanne':
                    self.assertEqual([], doc['berryPouch']['effectBytes'])
                for icon in doc['icons']:
                    symbol = icon['item']['value'].removeprefix('pret:')
                    table = data['src/data/item_icon_table.h'].decode()
                    binding = re.search(r'\[' + symbol + r'\]\s*= \{(\w+), (\w+)\}', table)
                    graphics = data['src/data/graphics/items.h'].decode()
                    paths = [re.search(asset + r'\[\]\s*= INCBIN_U32\("([^"]+)"\)', graphics)[1]
                             for asset in binding.groups()]
                    image = paths[0].replace('.4bpp.lz', '.png')
                    colors = paths[1].replace('.gbapal.lz', '.pal')
                    palette = [tuple(((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2) for c in line.split())
                               for line in data[colors].decode().splitlines()[3:]]
                    with Image.open(io.BytesIO(data[image])) as source:
                        expected = bytes(c for i in source.tobytes() for c in (*palette[i & 15], 255 if i & 15 else 0))
                    with Image.open(io.BytesIO(outputs[icon['pngPath']])) as actual:
                        self.assertEqual((24, 24), actual.size)
                        self.assertEqual(expected, actual.convert('RGBA').tobytes())

    def test_regional_cli_isolated_raw_closure(self):
        path = ROOT / 'scripts/imports/items/import-regional-items.py'
        spec = importlib.util.spec_from_file_location('regional_items', path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        for region in ('ssanne', 'route11'):
            with self.subTest(region=region), tempfile.TemporaryDirectory() as temporary:
                source = Source(ROOT)
                expected = module.convert(source, region)
                isolated = Path(temporary)
                closure = dict(source.raw)
                closure['include/constants/items.h'] = (RAW / 'include/constants/items.h').read_bytes()
                for relative, data in closure.items():
                    target = isolated / source.raw_prefix / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(data)
                command = [sys.executable, str(path), '--group', region, '--project-root', str(isolated)]
                subprocess.run(command, check=True, capture_output=True)
                subprocess.run([*command, '--check'], check=True, capture_output=True)
                package = isolated / 'content/generated/data/items' / region
                from item_icon_exports import output_materials
                for (directory, relative), data in output_materials(package, expected).items():
                    self.assertEqual(data, (directory / relative).read_bytes())
                self.assertFalse(any(p.is_dir() and p.suffix in ('.json', '.png') for p in isolated.rglob('*')))
                from item_icon_exports import pack_project
                pack_project(isolated)
                subprocess.run([*command, '--check'], check=True, capture_output=True)
                document = package / (region + '-items.json')
                original = document.read_bytes()
                changed = json.loads(original)
                changed['items'][0]['metadata']['description'] += ' altered'
                document.write_text(json.dumps(changed))
                with self.assertRaisesRegex(ValueError, 'business data'):
                    module.check_outputs(package, expected, region)
                document.write_bytes(original)
                manifest = package / 'manifest.json'
                original_manifest = manifest.read_bytes()
                changed_manifest = json.loads(original_manifest)
                changed_manifest['outputs'].pop()
                manifest.write_text(json.dumps(changed_manifest))
                with self.assertRaisesRegex(ValueError, 'manifest'):
                    module.check_outputs(package, expected, region)
                manifest.write_bytes(original_manifest)
                icon = json.loads(original)['icons'][0]
                atlas = isolated / icon['pngPath']
                with Image.open(atlas) as image:
                    changed_pixels = image.convert('RGBA')
                point = (icon['region']['x'], icon['region']['y'])
                color = changed_pixels.getpixel(point)
                changed_pixels.putpixel(point, ((color[0] + 1) % 256, *color[1:]))
                changed_pixels.save(atlas)
                with self.assertRaisesRegex(ValueError, 'pixels differ'):
                    module.check_outputs(package, expected, region)

    def test_default_packed_cli_checks_both_groups(self):
        path = ROOT / 'scripts/imports/items/import-regional-items.py'
        for region in ('ssanne', 'route11'):
            with self.subTest(region=region):
                subprocess.run([sys.executable, str(path), '--group', region, '--check'],
                               check=True, capture_output=True)


    def test_pickup_subset_is_explicit(self):
        paths = ['data/maps/Route4/map.json', 'data/scripts/item_ball_scripts.inc']
        source = ItemSource({p: (RAW / p).read_bytes() for p in paths})
        visible = source.pickups(['Route4'], hidden_maps=[])
        all_pickups = source.pickups(['Route4'])
        self.assertTrue(visible)
        self.assertGreater(len(all_pickups), len(visible))
        self.assertTrue(all(row['kind'] == 'visible' for row in visible))

    def test_missing_or_ambiguous_icon_binding_fails(self):
        path = 'src/data/item_icon_table.h'
        for table in [b'', b'[ITEM_POTION] = {a, b},\n    [ITEM_POTION] = {a, b},\n    [ITEM_POTION] = {a, b},']:
            with self.subTest(table=table), self.assertRaises(ValueError):
                ItemSource({path: table}).icon('ITEM_POTION')


if __name__ == '__main__':
    unittest.main()
