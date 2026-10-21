#!/usr/bin/env python3
"""Real-source special UI regeneration, exact pixels and isolated CLI failures."""
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from import_check_fixtures import snapshot_files, snapshot_tree, write_files
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from generated_output_compare import generated_output_matches
from item_icon_exports import decode_icon_page
from PIL import Image
from project_paths import PROJECT_ROOT as ROOT

RAW = ROOT / 'content/raw/pokefirered/037335f4'
SCRIPTS = {
    'trainer': 'trainers/import-trainer-card.py',
    'slot': 'items/import-slot-machine-presentation.py',
    'capture': 'pokemon/import-capture-dex.py',
    'gym': 'story/import-vermilion-gym-assets.py',
    'field': 'story/import-vermilion-gym-assets-field-move.py',
}
RETAINED = {
    'trainer': ROOT / 'content/generated/ui/trainer-card',
    'slot': ROOT,
    'capture': ROOT / 'content/generated/sprites/pokemon/capture-dex',
    'gym': ROOT / 'content/generated/ui/vermilion-gym-assets',
    'field': ROOT / 'content/generated/ui/vermilion-gym-field-move-art',
}


def load(name):
    path = ROOT / 'scripts/imports' / SCRIPTS[name]
    spec = importlib.util.spec_from_file_location('special_' + name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def build(name, module):
    if name == 'slot':
        outputs = module.generate(None, ROOT)
        sources = {path.removeprefix(module.RAW): data for path, data in outputs.items()
                   if path.startswith(module.RAW)}
        sources.update({path: (ROOT / local).read_bytes() for path, local in module.SHARED.items()})
        return sources, outputs
    if name == 'trainer':
        lock = module.load_lock()
    elif name == 'capture':
        lock = module.load_lock(ROOT / 'scripts/recipes/pokemon/capture-dex.lock.json')
    else:
        lock = module.read_lock()
    sources = module.read_inputs(lock, RAW) if name == 'field' else module.read_sources(lock, RAW)
    if name == 'trainer':
        outputs = module.convert(sources)
    elif name == 'capture':
        outputs = module.convert(lock, sources)
    elif name == 'gym':
        outputs = module.build(sources)
    else:
        outputs = module.build(lock, sources)
    return sources, outputs


def command(name, out, raw=RAW, dependency=ROOT):
    script = ROOT / 'scripts/imports' / SCRIPTS[name]
    arguments = [str(out), '--dependency', str(dependency)] if name == 'slot' else [
        '--raw', str(raw), '--out', str(out)]
    return [sys.executable, str(script), *arguments]


def invoke(arguments, cwd):
    return subprocess.run(arguments, cwd=cwd, capture_output=True, text=True, timeout=45)


class SpecialPresentationImporterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.modules = {name: load(name) for name in SCRIPTS}
        cls.fixtures = {name: build(name, module) for name, module in cls.modules.items()}

    def test_real_source_json_and_full_rgba_match_retained(self):
        for name in ('trainer', 'slot', 'capture', 'field'):
            sources, outputs = self.fixtures[name]
            self.assertTrue(sources)
            self.assertTrue(outputs)
            self.assertTrue(any(path.endswith('.png') for path in outputs))
            self.assertTrue(any(path.endswith('.json') for path in outputs))
            for relative, data in outputs.items():
                with self.subTest(importer=name, output=relative):
                    self.assertTrue(generated_output_matches(RETAINED[name] / relative, data), relative)

    def test_gym_retains_icon_atlas_pixels_and_portrait_contract(self):
        _, outputs = self.fixtures['gym']
        actual = json.loads(outputs['assets.json'])
        retained = json.loads((RETAINED['gym'] / 'assets.json').read_bytes())
        self.assertEqual({'schemaVersion', 'icon', 'portraits'}, set(actual))
        self.assertEqual(retained['schemaVersion'], actual['schemaVersion'])
        self.assertEqual(retained['portraits'], actual['portraits'])
        expected_icon = retained['icon']
        self.assertEqual({key: value for key, value in expected_icon.items() if key not in ('pngPath', 'region')},
                         {key: value for key, value in actual['icon'].items() if key != 'pngPath'})
        page_path = ROOT / expected_icon['pngPath'] if expected_icon['pngPath'].startswith('content/') else RETAINED['gym'] / expected_icon['pngPath']
        decoded = decode_icon_page(page_path.read_bytes(), expected_icon)
        with Image.open(io.BytesIO(outputs[actual['icon']['pngPath']])) as rebuilt:
            self.assertEqual(decoded.size, rebuilt.size)
            self.assertEqual(decoded.tobytes(), rebuilt.convert('RGBA').tobytes())
        self.assertTrue(actual['portraits'])
        for row in actual['portraits']:
            name = row['id'] + '.png'
            self.assertTrue(generated_output_matches(RETAINED['gym'] / name, outputs[name]), name)
        manifest = json.loads(outputs['manifest.json'])
        self.assertEqual(set(outputs) - {'manifest.json'}, {row['path'] for row in manifest['outputs']})

    def test_capture_front_bindings_and_back_palette_rounding(self):
        sources, outputs = self.fixtures['capture']
        dex = json.loads(outputs['capture-dex.json'])
        for entry in dex['entries'].values():
            self.assertTrue((ROOT / 'content/generated' / entry['frontSpritePath']).is_file())
        back = json.loads(outputs['capture-back-art.json'])
        self.assertEqual({'schemaVersion', 'repository', 'display', 'sprites'}, set(back))
        self.assertTrue(back['sprites'])
        for sprite in back['sprites']:
            with self.subTest(sprite=sprite['texturePath']):
                with Image.open(io.BytesIO(sources[sprite['sourceImagePath']])) as original:
                    indices = original.tobytes()
                rows = sources[sprite['sourcePalettePath']].decode().splitlines()[3:]
                colors = [tuple((int(c) // 8) * 255 // 31 for c in row.split()) for row in rows]
                expected = bytes(c for index in indices for c in (*colors[index], 255 if index else 0))
                with Image.open(io.BytesIO(outputs[sprite['texturePath']])) as image:
                    self.assertEqual(expected, image.convert('RGBA').tobytes())

    def test_conversion_does_not_load_borrowed_cli_code(self):
        original_spec = importlib.util.spec_from_file_location
        original_read = Path.read_bytes
        blocked = {'import-lab-battle-ui.py', 'fetch-lab-battle-ui.py', 'import-route1-battle-art.py'}
        def guarded_spec(name, location, *args, **kwargs):
            if Path(location).name in blocked:
                raise AssertionError('Importer executed borrowed CLI: ' + str(location))
            return original_spec(name, location, *args, **kwargs)
        def guarded_read(path):
            if path.name in blocked:
                raise AssertionError('Importer authenticated borrowed CLI: ' + str(path))
            return original_read(path)
        with patch('importlib.util.spec_from_file_location', guarded_spec), patch.object(Path, 'read_bytes', guarded_read):
            for name in SCRIPTS:
                with self.subTest(importer=name):
                    self.assertEqual(self.fixtures[name], build(name, load(name)))

    def test_clis_generate_and_check_without_source_writes(self):
        for name, (sources, outputs) in self.fixtures.items():
            with self.subTest(importer=name), tempfile.TemporaryDirectory() as temporary:
                temporary = Path(temporary)
                out = temporary / 'output'
                arguments = command(name, out)
                before = snapshot_files(RAW / relative for relative in sources)
                if name in ('slot', 'gym', 'field'):
                    missing = invoke([*arguments, '--check'], temporary)
                    self.assertNotEqual(0, missing.returncode)
                    self.assertFalse(out.exists(), 'Missing-output check created files')
                generated = invoke(arguments, temporary)
                self.assertEqual(0, generated.returncode, generated.stdout + generated.stderr)
                self.assertEqual(set(outputs), {path.relative_to(out).as_posix() for path in out.rglob('*') if path.is_file()})
                for relative, data in outputs.items():
                    self.assertEqual(data, (out / relative).read_bytes(), relative)
                written = snapshot_tree(out)
                checked = invoke([*arguments, '--check'] if name in ('slot', 'gym', 'field') else arguments, temporary)
                self.assertEqual(0, checked.returncode, checked.stdout + checked.stderr)
                for relative, data in outputs.items():
                    self.assertEqual(data, (out / relative).read_bytes(), relative)
                if name in ('slot', 'gym', 'field'):
                    self.assertEqual(written, snapshot_tree(out), '--check rewrote generated files')
                    target = next(path for path in out.rglob('*.json') if 'content/raw' not in path.as_posix())
                    value = json.loads(target.read_bytes())
                    value['schemaVersion'] = -1
                    target.write_text(json.dumps(value))
                    altered = snapshot_tree(out)
                    rejected = invoke([*arguments, '--check'], temporary)
                    self.assertNotEqual(0, rejected.returncode, 'Corrupt JSON passed --check')
                    self.assertEqual(altered, snapshot_tree(out))
                self.assertEqual(before, snapshot_files(RAW / relative for relative in sources), 'CLI changed input sources')

    def test_source_mutations_fail_before_writing_outputs(self):
        mutations = {
            'trainer': ('src/trainer_card.c', b'badgeFlag <= FLAG_BADGE08_GET', b'badgeFlag <= FLAG_BADGE07_GET'),
            'slot': ('src/slot_machine.c', b'80 + 40 * i, 44 + 24 * j, 2', b'80 + 40 * i, 44 + 24 * j, 3'),
            'capture': ('src/battle_anim_mons.c', b'return GetBattlerSpriteCoord(battlerId, BATTLER_COORD_Y_PIC_OFFSET_DEFAULT);', b'return 0;'),
            'gym': ('tools/gbagfx/gfx.c', b'#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)', b'#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 4)'),
            'field': ('src/field_effect.c', b'task->data[5] -= 16;', b'task->data[5] -= 8;'),
        }
        errors = {
            'trainer': 'Unsupported Trainer Card semantics: badgeFlag',
            'slot': 'Slot presentation source contract differs: 80 + 40 * i',
            'capture': 'Unsupported battle-coordinate semantics',
            'gym': 'Source contract missing: #define DOWNCONVERT_BIT_DEPTH',
            'field': 'Missing source field-move contract: task->data[5]',
        }
        for name, (relative, needle, replacement) in mutations.items():
            with self.subTest(importer=name), tempfile.TemporaryDirectory() as temporary:
                temporary = Path(temporary)
                dependency = temporary / 'dependency'
                raw = dependency / 'content/raw/pokefirered/037335f4'
                write_files(raw, self.fixtures[name][0])
                if name == 'slot':
                    # Font code stays untouched; this fixture only supplies the existing CLI dependency.
                    relative_font = 'scripts/imports/presentation/import-source-font.py'
                    font = dependency / relative_font
                    font.parent.mkdir(parents=True, exist_ok=True)
                    font.write_bytes((ROOT / relative_font).read_bytes())
                target = raw / relative
                original = target.read_bytes()
                self.assertIn(needle, original)
                target.write_bytes(original.replace(needle, replacement))
                out = temporary / 'output'
                out.mkdir()
                (out / 'sentinel').write_bytes(b'keep existing output')
                before = snapshot_tree(out)
                source_before = snapshot_tree(raw)
                rejected = invoke(command(name, out, raw, dependency), temporary)
                self.assertNotEqual(0, rejected.returncode, 'Changed source semantics passed')
                self.assertIn(errors[name], rejected.stderr)
                self.assertEqual(before, snapshot_tree(out), 'Invalid source changed output')
                self.assertEqual(source_before, snapshot_tree(raw), 'Invalid source was rewritten')

    def test_field_check_preserves_retained_encoding_and_rejects_pixel_change(self):
        sources, outputs = self.fixtures['field']
        before = snapshot_files([*(RAW / relative for relative in sources),
                           *(RETAINED['field'] / relative for relative in outputs)])
        result = invoke([sys.executable, str(ROOT / 'scripts/imports' / SCRIPTS['field']), '--check'], ROOT)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(before, snapshot_files(Path(path) for path in before))
        with tempfile.TemporaryDirectory() as temporary:
            out = Path(temporary) / 'output'
            self.assertEqual(0, invoke(command('field', out), temporary).returncode)
            target = out / 'outdoor-streaks.png'
            with Image.open(target) as original:
                image = original.convert('RGBA')
            pixel = image.getpixel((0, 0))
            image.putpixel((0, 0), ((pixel[0] + 1) % 256, *pixel[1:]))
            image.save(target)
            altered = snapshot_tree(out)
            result = invoke([*command('field', out), '--check'], temporary)
            self.assertNotEqual(0, result.returncode, 'Changed RGBA passed --check')
            self.assertEqual(altered, snapshot_tree(out))

    def test_gym_and_field_refuse_output_over_source(self):
        for name in ('gym', 'field'):
            with self.subTest(importer=name):
                module = self.modules[name]
                with self.assertRaisesRegex(ValueError, 'overlap'):
                    module.run(RAW, RAW / 'invalid-output', check=True)
                with tempfile.TemporaryDirectory() as temporary:
                    with self.assertRaisesRegex(ValueError, 'project raw'):
                        module.run(Path(temporary), RAW / 'invalid-output', check=True)


if __name__ == '__main__':
    unittest.main()
