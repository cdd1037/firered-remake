#!/usr/bin/env python3
"""Direct world conversion regressions, without historical regional stages.

Selected maps are regenerated into temporary output and compared against final
JSON and all decoded RGBA channels. The separate world oracle checks all maps.
"""
import importlib.util
import json
from pathlib import Path
import sys
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from import_check_fixtures import snapshot_tree
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as ROOT
from generated_output_compare import generated_output_matches
from map_scripts import parse_scripts, script_closure
from map_source import source_topology, tileset_bindings
from source_features import script_blocks
from world_map_renderer import render_map
from world_selection import load_selection
from world_source import WorldSource

RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'
SELECTION = ROOT / 'data/world-maps.json'


def module(relative):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(path.stem.replace('-', '_'), path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def script_source(text):
    path = 'data/maps/Test/scripts.inc'
    return SimpleNamespace(entries={path: {'roles': ['script-label-closure']}},
        text=lambda _: text, ref=lambda path, symbol, line=None: dict(path=path, symbol=symbol, line=line))


class MapConsolidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.selected = load_selection(SELECTION)
        cls.source = WorldSource(RAW)
        cls.tilesets = tileset_bindings(cls.source)
        cls.atlas = module('scripts/imports/maps/import-map-atlases.py').AtlasReader(GENERATED / 'maps/atlases')

    def test_aliases_macros_and_transitive_closure(self):
        source = script_source('.macro local arg\n call Hidden\n.endm\n.equ Alias, Branch\n'
            'Root::\n call Alias\n goto End\nBranch::\n message Text\nEnd::\n end\n'
            'Text::\n .string "label-like text$"\nHidden::\n end\n')
        result = script_closure(parse_scripts(source), {'Root'})
        self.assertEqual(['Branch', 'End', 'Root', 'Text'], [b['label'] for b in result['blocks']])
        self.assertEqual('local', result['localMacros'][0]['name'])
        self.assertFalse(result['unresolvedLabelReferences'])
        set_source = script_source('.set Alias, Root\nRoot::\n end\n')
        self.assertEqual({}, parse_scripts(set_source)[1])
        self.assertEqual({'Alias': 'Root'}, parse_scripts(set_source, alias_directives=('equ', 'set'))[1])
        with self.assertRaises(ValueError):
            parse_scripts(source, alias_directives=('include',))

    def test_unknown_targets_and_duplicate_labels_fail(self):
        for text in ('Root::\n call RenamedUnknown\n', 'Root::\n trainerbattle_no_intro T, Missing\n'):
            with self.subTest(text=text), self.assertRaises(ValueError):
                script_closure(parse_scripts(script_source(text)), {'Root'})
        with self.assertRaises(ValueError):
            parse_scripts(script_source('Root::\n end\nRoot::\n end\n'))

    def test_raw_reads_reject_escape_symlinks_missing_and_misaligned_data(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            raw = root / 'raw'; raw.mkdir()
            (raw / 'odd.bin').write_bytes(b'123')
            (root / 'outside').write_bytes(b'outside')
            (raw / 'link').symlink_to(root / 'outside')
            source = WorldSource(raw)
            for name in ('../outside', str(root / 'outside'), 'link'):
                with self.subTest(name=name), self.assertRaises(ValueError):
                    source.read(name)
            with self.assertRaises(FileNotFoundError):
                source.read('missing')
            with self.assertRaisesRegex(ValueError, 'Misaligned'):
                source.units('odd.bin', 'H')
            self.assertEqual(b'123', source.read('odd.bin'))

    def test_final_selection_rejects_unsafe_empty_and_duplicate_outputs(self):
        importer = module('scripts/imports/maps/import-world.py')
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); selection = root / 'selection.json'; out = root / 'out'
            for group, filename in (('../escape', 'map.json'), ('safe', '../map.json'),
                                    ('safe', 'nested\\map.json'), ('safe', 'drive:map.json')):
                selection.write_text(json.dumps({'groups': {group: [{'sourceName': 'PalletTown', 'filename': filename}]}}))
                with self.subTest(group=group, filename=filename), self.assertRaises(ValueError):
                    load_selection(selection)
            for groups in ({}, {'opening': ['PalletTown', 'PalletTown']}):
                selection.write_text(json.dumps({'groups': groups}))
                with self.subTest(groups=groups), self.assertRaises(ValueError):
                    importer.compile_world(RAW, out, selection)
                self.assertFalse(out.exists())

    def test_topology_preserves_reciprocal_connection_semantics(self):
        a = dict(id='MAP_A', warp_events=[], connections=[dict(map='MAP_B', direction='right', offset=3)])
        b = dict(id='MAP_B', warp_events=[], connections=[dict(map='MAP_A', direction='left', offset=-3)])
        documents = {'data/maps/A/map.json': a, 'data/maps/B/map.json': b}
        source = SimpleNamespace(tree=documents, json=documents.__getitem__)
        result = source_topology(source, [dict(events=a), dict(events=b)])
        self.assertEqual(2, len(result['connections']))
        self.assertTrue(all(row['reciprocal'] for row in result['connections']))
        b['connections'][0]['offset'] = 2
        with self.assertRaises(ValueError):
            source_topology(source, [dict(events=a), dict(events=b)])

    def assert_selected_maps(self, rows):
        self.assertTrue(rows)
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            for row in rows:
                with self.subTest(map=row['sourceName'], path=row['path']):
                    target = output / row['path']
                    document = render_map(self.source, row['sourceName'], row['stableId'], target,
                        tilesets=self.tilesets, render_border=row.get('renderBorder', True),
                        layout_symbol=row.get('layout'), technical_spawn=row.get('technicalSpawn'))
                    self.assertEqual(json.loads((GENERATED / row['path']).read_bytes()), document)
                    self.assertEqual(self.source.json('data/maps/' + row['sourceName'] + '/map.json'), document['events'])
                    self.assertTrue(document['artifacts'])
                    for artifact in document['artifacts']:
                        logical = (Path(row['path']).parent / artifact['path']).as_posix()
                        expected = self.atlas.read(logical)
                        with Image.open(target.parent / artifact['path']) as actual:
                            self.assertEqual(expected.size, actual.size, logical)
                            self.assertEqual(expected.tobytes(), actual.convert('RGBA').tobytes(), logical)

    def test_early_maps_and_gyms_match_final_json_and_atlas_pixels(self):
        groups = {'opening', 'viridian', 'viridian-center', 'forest', 'route22', 'pewter', 'celadon-gym', 'fuchsia-gym'}
        rows = [r for r in self.selected if r['path'].split('/')[2] in groups]
        self.assertEqual(groups, {r['path'].split('/')[2] for r in rows})
        self.assert_selected_maps(rows)

    def test_cleared_vram_interiors_and_shared_graphics_alias(self):
        names = {'CeladonCity_Condominiums_RoofRoom', 'SilphCo_1F'}
        rows = [r for r in self.selected if r['path'].split('/')[2] == 'school-house-interiors' or r['sourceName'] in names]
        self.assertTrue(names <= {r['sourceName'] for r in rows})
        self.assert_selected_maps(rows)

    def test_actual_source_script_closures_remain_nonempty(self):
        source = WorldSource(RAW)
        groups = ('viridian', 'viridian-center', 'vermilion', 'vermilion-gym', 'ssanne', 'route3',
                  'route22', 'route11', 'rocktunnel', 'pewter', 'mtmoon', 'forest', 'cerulean', 'celadon-approach', 'bill')
        # Register original include/text inputs once. Regional stage catalogs are
        # never used as either source selection or expected output.
        script_blocks(source, ['ViridianCity'])
        parsed = parse_scripts(source, alias_directives=('equ', 'set'))
        for group in groups:
            names = [r['sourceName'] for r in self.selected if r['path'].split('/')[2] == group and 'variant' not in r]
            with self.subTest(group=group):
                self.assertTrue(names)
                roots = {e['script'] for name in names
                    for kind in ('object_events', 'coord_events', 'bg_events')
                    for e in source.json('data/maps/' + name + '/map.json')[kind]
                    if e.get('script') not in (None, '0', '0x0', 'NULL')}
                self.assertTrue(roots)
                result = script_closure(parsed, roots)
                self.assertTrue(result['blocks'])
                self.assertFalse(result['unresolvedLabelReferences'])
                self.assertTrue(roots <= {b['label'] for b in result['blocks']})

    def test_final_comparison_preserves_types_array_order_and_transparent_rgb(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            document = dict(meaningful=[1, 2], enabled=False)
            target = root / 'data.json'
            expected = json.dumps(document, indent=2).encode()
            target.write_text(json.dumps(document, sort_keys=True))
            self.assertTrue(generated_output_matches(target, expected))
            for changed in (dict(document, enabled=0), dict(document, meaningful=[2, 1])):
                target.write_text(json.dumps(changed))
                self.assertFalse(generated_output_matches(target, expected))
            image = Image.new('RGBA', (16, 16), (11, 22, 33, 0))
            target = root / 'map.png'; expected_path = root / 'expected.png'
            image.save(target, compress_level=9); image.save(expected_path, compress_level=0)
            self.assertTrue(generated_output_matches(target, expected_path.read_bytes()))
            Image.new('RGBA', (16, 16), (12, 22, 33, 0)).save(target)
            self.assertFalse(generated_output_matches(target, expected_path.read_bytes()))

    def test_partial_map_option_is_rejected_without_writing(self):
        with tempfile.TemporaryDirectory() as temporary:
            out = Path(temporary)
            for relative in ('maps/world.json', 'maps/regions/opening/pallet-town.json',
                             'maps/atlases/catalog.json', 'maps/atlases/world.png'):
                target = out / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(b'existing complete world must remain unchanged')
            before = snapshot_tree(out)
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/imports/maps/import-world.py'),
                                     '--raw', str(RAW), '--out', str(out), '--map', 'PalletTown'],
                                    capture_output=True, text=True)
            self.assertEqual(2, result.returncode)
            self.assertIn('unrecognized arguments: --map PalletTown', result.stderr)
            self.assertEqual(before, snapshot_tree(out))

    def test_world_check_is_read_only_and_rejects_final_data_drift(self):
        importer = module('scripts/imports/maps/import-world.py')
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); out = root / 'resources'
            selection = root / 'world.json'
            selection.write_text(json.dumps({'groups': {'opening': ['PalletTown']}}))
            importer.compile_world(RAW, out, selection)
            command = [sys.executable, str(ROOT / 'scripts/imports/maps/import-world.py'),
                       '--raw', str(RAW), '--out', str(out), '--selection', str(selection), '--check']
            before = snapshot_tree(out)
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual(before, snapshot_tree(out))
            target = out / 'maps/world.json'
            changed = json.loads(target.read_bytes())
            changed['maps'][0]['stableId'] = 'map.wrong'
            target.write_text(json.dumps(changed))
            damaged = snapshot_tree(out)
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('Final world output differs', result.stderr)
            self.assertEqual(damaged, snapshot_tree(out))


if __name__ == '__main__':
    unittest.main()
