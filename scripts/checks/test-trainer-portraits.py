#!/usr/bin/env python3
"""Shared trainer portraits: actual regeneration plus source/pixel failure cases."""
import io
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from generated_output_compare import generated_output_matches
from project_paths import PROJECT_ROOT
from trainer_portraits import (CONSTANTS, FRONT_TABLE, Sources, build, palette_colors,
                               portrait_png, publish, recipe, safe)
from PIL import Image


def indexed(size, indices):
    image = Image.frombytes('P', size, indices)
    image.putpalette([37, 53, 91] * 16)
    output = io.BytesIO()
    image.save(output, format='PNG')
    return output.getvalue()


def palette():
    return ('JASC-PAL\n0100\n16\n' + '\n'.join(f'{i * 7 + 11} {i * 11 + 12} {i * 13 + 13}' for i in range(16)) + '\n').encode()


class TrainerPortraitTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = recipe()
        cls.sources = Sources()
        cls.outputs = {name: build(cls.sources, selection, cls.document['portraits'])
                       for name, selection in cls.document['groups'].items()}
        if not cls.outputs or not all(cls.outputs.values()):
            raise ValueError('Empty trainer portrait regeneration suite')

    def fresh_sources(self):
        result = Sources()
        result.cache = dict(self.sources.cache)
        return result

    def test_every_group_matches_retained_fields_and_full_rgba(self):
        for name, outputs in self.outputs.items():
            with self.subTest(group=name):
                output = PROJECT_ROOT / 'content/generated' / self.document['groups'][name]['output']
                publish(outputs, output, check=True)

    def test_isolated_minimal_source_closure_rebuilds_every_group(self):
        # Only consumed source bytes and the two live configurations exist here.
        # No old lock, audio input, presentation importer or generated resource is used.
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for relative in ['scripts/recipes/config/source.json', 'scripts/recipes/trainers/portraits.json']:
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((PROJECT_ROOT / relative).read_bytes())
            for (local, relative), data in self.sources.cache.items():
                directory = root / 'content/raw' / ('local/' + local if local else 'pokefirered/' + self.sources.commit[:8])
                target = safe(directory, relative)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
            for name, selection in self.document['groups'].items():
                with self.subTest(group=name):
                    rebuilt = build(Sources(root), selection, self.document['portraits'])
                    self.assertEqual(self.outputs[name], rebuilt)
            self.assertFalse(any('sound/' in path for _, path in self.sources.cache))

    def test_back_picture_uses_frame_zero_and_preserves_hidden_rgb(self):
        first = bytes(range(16)) * 256
        data = indexed((64, 320), first + bytes([7]) * (64 * 256))
        colors = palette_colors(palette())
        with Image.open(io.BytesIO(portrait_png(data, palette(), 'back'))) as image:
            self.assertEqual((64, 64), image.size)
            self.assertEqual(bytes(c for i in first for c in (*colors[i], 255 if i else 0)), image.convert('RGBA').tobytes())
        self.assertNotEqual((0, 0, 0), colors[0])

    def test_rejects_wrong_geometry_mode_and_indices(self):
        for size, indices in [((32, 64), bytes(range(16)) * 128), ((64, 64), bytes([16]) * 4096),
                              ((64, 64), bytes(4096)), ((64, 64), bytes([1]) * 4096)]:
            with self.subTest(size=size, first=indices[0]), self.assertRaises(ValueError):
                portrait_png(indexed(size, indices), palette(), 'front')
        output = io.BytesIO()
        Image.new('RGB', (64, 64)).save(output, format='PNG')
        with self.assertRaises(ValueError):
            portrait_png(output.getvalue(), palette(), 'front')

    def test_rejects_malformed_palette(self):
        for value in [b'bad', palette().replace(b'16\n', b'17\n', 1),
                      palette().replace(b'11 12 13\n', b'256 4 5\n'), palette().replace(b'11 12 13\n', b'3 4\n')]:
            with self.subTest(value=value[:30]), self.assertRaises(ValueError):
                palette_colors(value)

    def test_local_palette_stays_local_even_with_git_reference(self):
        sources = Sources(reference=Path('/unread-reference'))
        path = 'graphics/trainers/palettes/bug_catcher.pal'
        with patch('trainer_portraits.subprocess.check_output', return_value=b'pinned git blob') as git:
            self.assertEqual(self.sources.read(path, 'forest-trainer-art'), sources.read(path, 'forest-trainer-art'))
            git.assert_not_called()
            self.assertEqual(b'pinned git blob', sources.read(CONSTANTS))
            git.assert_called_once_with(['git', '-C', '/unread-reference', 'show', sources.commit + ':' + CONSTANTS])

    def test_missing_local_palette_is_not_replaced_by_upstream(self):
        with tempfile.TemporaryDirectory() as temp:
            sources = Sources(reference=Path('/unread-reference'), local_palettes=Path(temp))
            with patch('trainer_portraits.subprocess.check_output') as git, self.assertRaises(FileNotFoundError):
                sources.read('graphics/trainers/palettes/red_back_pic.pal', 'forest-trainer-art')
            git.assert_not_called()

    def test_rejects_missing_input_and_unsafe_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            sources = Sources(raw=Path(temp))
            with self.assertRaises(FileNotFoundError):
                build(sources, self.document['groups']['route3'], self.document['portraits'])
            for path in ['../escape', '/escape', 'a//b', 'a/./b', 'a\\b', '']:
                with self.subTest(path=path), self.assertRaises(ValueError):
                    safe(Path(temp), path)
            with self.assertRaises(ValueError):
                sources.read('file', '../outside')
            (Path(temp) / 'link').symlink_to(PROJECT_ROOT, target_is_directory=True)
            with self.assertRaises(ValueError):
                safe(Path(temp), 'link/README.md')

    def test_rejects_empty_duplicate_or_unknown_selection(self):
        for rows in [[], ['lass-front', 'lass-front'], ['missing'], [{'portrait': 'lass-front', 'id': '../escape'}]]:
            selection = dict(self.document['groups']['route3'], portraits=rows)
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                build(self.sources, selection, self.document['portraits'])

    def test_source_bindings_and_coordinates_are_required(self):
        for path, before, after in [(CONSTANTS, 'TRAINER_PIC_LASS', 'TRAINER_PIC_MISSING'),
                                    (FRONT_TABLE, 'TRAINER_SPRITE(LASS,', 'TRAINER_SPRITE(WRONG,')]:
            sources = self.fresh_sources()
            sources.cache[(None, path)] = sources.read(path).replace(before.encode(), after.encode())
            with self.subTest(path=path), self.assertRaises(ValueError):
                build(sources, self.document['groups']['route3'], self.document['portraits'])
        sources = self.fresh_sources()
        table = sources.text(FRONT_TABLE)
        # Source sprite dimensions determine placement; unsupported sizes fail.
        sources.cache[(None, FRONT_TABLE)] = table.replace('.size = 8', '.size = 7').encode()
        with self.assertRaises(ValueError):
            build(sources, self.document['groups']['route3'], self.document['portraits'])

    def test_trainer_class_and_battle_bindings_are_required(self):
        for group, path, before, after in [('cinnabar', 'src/data/text/trainer_class_names.h', 'TRAINER_CLASS_BURGLAR', 'TRAINER_CLASS_MISSING'),
                                         ('route22', 'src/data/trainers.h', '.trainerPic = TRAINER_PIC_RIVAL_EARLY', '.trainerPic = TRAINER_PIC_MISSING')]:
            sources = self.fresh_sources()
            sources.cache[(None, path)] = sources.read(path).replace(before.encode(), after.encode())
            with self.subTest(group=group), self.assertRaises(ValueError):
                build(sources, self.document['groups'][group], self.document['portraits'])

    def test_publish_preflights_paths_and_check_never_writes(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / 'output'
            with self.assertRaises(ValueError):
                publish({'first.json': b'{}', '../escape.json': b'{}'}, output)
            self.assertFalse(output.exists())
            publish(self.outputs['forest'], output)
            manifest = output / self.document['groups']['forest']['manifest']
            manifest.write_bytes(b'{}')
            with self.assertRaises(ValueError):
                publish(self.outputs['forest'], output, check=True)
            self.assertEqual(b'{}', manifest.read_bytes())

    def test_cli_runs_outside_project_and_checks_isolated_output(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / 'portraits'
            command = [sys.executable, str(PROJECT_ROOT / 'scripts/imports/trainers/import-trainer-portraits.py'),
                       '--group', 'forest', '--output', str(output)]
            subprocess.run(command, cwd=temp, check=True, capture_output=True)
            subprocess.run(command + ['--check'], cwd=temp, check=True, capture_output=True)
            for name, data in self.outputs['forest'].items():
                self.assertTrue(generated_output_matches(output / name, data), name)
            result = subprocess.run(command + ['--group', 'pewter'], cwd=temp, capture_output=True)
            self.assertNotEqual(0, result.returncode)


if __name__ == '__main__':
    unittest.main()
