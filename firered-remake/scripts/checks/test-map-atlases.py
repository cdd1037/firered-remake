#!/usr/bin/env python3
"""Small offline regression tests for map-atlas import, recovery and explicit prune."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image

SPEC = importlib.util.spec_from_file_location('map_atlases', (_PROJECT_ROOT / 'scripts/imports/maps/import-map-atlases.py'))
ATLAS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ATLAS)


class AtlasTests(unittest.TestCase):
    def test_prune_and_incremental_reimport_preserve_logical_images(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / 'project'
            generated = root / 'content/generated'
            region = generated / 'maps/regions/test-region'
            room = region / 'room'
            room.mkdir(parents=True)
            colors = {'bottom.png': (27, 80, 31, 255), 'top.png': (13, 19, 211, 0), 'player.png': (255, 9, 4, 255)}
            for name, rgba in colors.items():
                Image.new('RGBA', (32, 32), rgba).save(room / name)
            artifacts = [dict(path=name, width=32, height=32) for name in colors]
            runtime = room / 'runtime-map.json'
            runtime.write_text(json.dumps(dict(schemaVersion=3,blocks='',artifacts=artifacts)))
            manifest = region / 'manifest.json'
            manifest.write_text(json.dumps(dict(outputCount=3, outputs=[dict(path='room/' + name) for name in colors])))
            output = generated / 'maps/atlases'
            images = ATLAS.inventory(generated)
            self.assertEqual(len(images), 2)  # Sprite sheets are deliberately outside this importer.
            ATLAS.compile_atlases(generated, output, images)
            self.assertEqual(ATLAS.verify(generated, output)['originalImagesCompared'], 2)
            before = {p.name: p.read_bytes() for p in output.iterdir()}
            original_runtime = runtime.read_bytes()
            backup = Path(temp) / 'backup'
            result = ATLAS.prune_originals(root, generated, output, images, backup)
            self.assertEqual((result['prunedImages'], result['manifestsUpdated']), (2, 1))
            self.assertTrue((room / 'player.png').is_file())
            self.assertEqual(runtime.read_bytes(), original_runtime)
            self.assertEqual(json.loads(manifest.read_text())['outputs'], [dict(path='room/player.png')])
            self.assertEqual(json.loads(manifest.read_text())['outputCount'], 1)
            self.assertTrue((backup / 'content/generated/maps/regions/test-region/room/top.png').is_file())
            checked = ATLAS.verify(generated, output)
            self.assertEqual(checked['originalImagesCompared'], 0)
            self.assertFalse(checked['originalPixelParity'])
            self.assertEqual(checked['packedOnlyImages'], 2)
            # Repacking absent PNGs deterministically preserves every output byte.
            stage = Path(temp) / 'stage'
            ATLAS.compile_atlases(generated, stage, images, ATLAS.AtlasReader(output))
            self.assertEqual(before, {p.name: p.read_bytes() for p in stage.iterdir()})
            # Freshly regenerated bottom overrides only that image; absent alpha-RGB
            # top layer is recovered exactly from the existing packed output.
            new_color = (88, 45, 233, 128)
            Image.new('RGBA', (32, 32), new_color).save(room / 'bottom.png')
            stage2 = Path(temp) / 'stage2'
            ATLAS.compile_atlases(generated, stage2, images, ATLAS.AtlasReader(output))
            stats = ATLAS.verify(generated, stage2, images, ATLAS.AtlasReader(output))
            self.assertEqual((stats['originalImagesCompared'], stats['recoveredImagesCompared']), (1, 1))
            reader = ATLAS.AtlasReader(stage2)
            self.assertEqual(reader.read('maps/regions/test-region/room/bottom.png').getpixel((0, 0)), new_color)
            self.assertEqual(reader.read('maps/regions/test-region/room/top.png').getpixel((0, 0)), colors['top.png'])
            # Reject corruption before any original can be removed.
            catalog = json.loads((stage2 / 'catalog.json').read_text())
            catalog['images']['maps/regions/test-region/room/bottom.png']['length'] = 999999
            (stage2 / 'catalog.json').write_text(json.dumps(catalog))
            with self.assertRaises(ValueError):
                ATLAS.verify(generated, stage2)
            self.assertTrue((room / 'bottom.png').is_file())


if __name__ == '__main__':
    unittest.main()
