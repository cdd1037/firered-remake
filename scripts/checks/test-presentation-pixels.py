#!/usr/bin/env python3
"""Exact presentation palette/frame semantics and real species-art regeneration."""
import io
import importlib.util
import struct
import json
from pathlib import Path
import sys
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from PIL import Image
from project_paths import PROJECT_ROOT
from presentation_pixels import PixelReader, palette, rgba
from presentation_source import safe, Source
from species_art import build, selected_species_art
from trainer_portraits import selected_trainer_art
from item_source import ItemSource
import ui_tiles
from generated_output_compare import generated_output_matches


class MemorySource:
    def __init__(self, data):
        self.data = data

    def read(self, path):
        return self.data[path]


def sheet(size, values):
    image = Image.frombytes('P', size, values)
    image.putpalette([c for i in range(256) for c in (i, i, i)])
    result = io.BytesIO()
    image.save(result, format='PNG')
    return result.getvalue()


class PresentationPixelsTests(unittest.TestCase):
    def test_distinct_rgb5_expansions_and_hidden_rgb(self):
        data = b'JASC-PAL\n0100\n2\n56 56 56\n248 0 80\n'
        self.assertEqual(palette(data), [(57, 57, 57), (255, 0, 82)])
        self.assertEqual(palette(data, True), [(57, 57, 57), (255, 0, 82)])
        # Value 3 differs between floor scaling (24) and bit replication (24);
        # value 4 distinguishes them (32 versus 33).
        data = b'JASC-PAL\n0100\n1\n32 32 32\n'
        self.assertEqual(palette(data), [(33, 33, 33)])
        self.assertEqual(palette(data, True), [(32, 32, 32)])
        self.assertEqual(rgba(b'\0', palette(data)), bytes([33,33,33,0]))

    def test_frames_crops_and_pixel_only_adapter(self):
        values = bytes((i // 1024) % 16 for i in range(8192))
        pal = ('JASC-PAL\n0100\n16\n' + ''.join(f'{i*8} 0 0\n' for i in range(16))).encode()
        reader = PixelReader(MemorySource({'sheet': sheet((64,128),values), 'pal':pal}))
        full, pixels = reader.indexed('sheet','pal',(64,128))
        self.assertEqual(full,values)
        self.assertEqual(pixels,reader.pixels('sheet','pal',(64,128)))
        self.assertEqual(reader.indexed('sheet','pal',(64,128),first_frame=True)[0],values[:1024])
        self.assertEqual(reader.indexed('sheet','pal',(64,128),second_frame=True)[0],values[4096:8192])
        cropped=reader.indexed('sheet','pal',(64,128),crop=(8,16,40,48),first_frame=True)[0]
        self.assertEqual(cropped,b''.join(values[y*64+8:y*64+40] for y in range(16,48)))

    def test_bad_palette_and_index_are_rejected(self):
        for data in (b'bad',b'JASC-PAL\n0100\n1\n256 0 0\n',b'JASC-PAL\n0100\n2\n0 0 0\n'):
            with self.assertRaises(ValueError):palette(data)
        with self.assertRaises(ValueError):rgba(bytes([2]),[(0,0,0)])
        reader=PixelReader(MemorySource({'sheet':sheet((8,8),bytes([16])*64)}))
        with self.assertRaises(ValueError):reader.indexed('sheet','missing',(8,8))
        with self.assertRaises(ValueError):reader.indexed('sheet','missing',(16,8))

    def test_path_traversal_and_symlinks_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for path in ('../escape','/absolute','a/../escape','a\\escape','a:escape',''):
                with self.assertRaises(ValueError):safe(root,path)
            (root/'link').symlink_to(root, target_is_directory=True)
            with self.assertRaises(ValueError):safe(root,'link/file')

    def test_source_reader_derives_actual_closure(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            raw=root/'content/raw/pokefirered/037335f4/example.txt'
            raw.parent.mkdir(parents=True);raw.write_bytes(b'original')
            reader=Source(root)
            self.assertEqual(reader.read('example.txt'),b'original')
            self.assertEqual(reader.ref('example.txt','symbol'),dict(path='example.txt',symbol='symbol'))
            self.assertEqual(reader.raw,{'example.txt':b'original'})
            with self.assertRaises(FileNotFoundError):reader.read('missing.txt')
            with self.assertRaises(ValueError):reader.read('../escape')

    def test_species_profiles_reproduce_actual_runtime_art(self):
        profiles=json.loads((PROJECT_ROOT/'scripts/recipes/presentation/species-art.json').read_bytes())
        self.assertTrue(profiles)
        with tempfile.TemporaryDirectory() as directory:
            for profile in profiles:
                with self.subTest(profile=profile):
                    outputs=build(PROJECT_ROOT,profile)
                    self.assertGreater(len(outputs),1)
                    dex=json.loads(outputs['manifest.json'])['dex']
                    for entry in dex if isinstance(dex,list) else [dex]:
                        self.assertEqual(set(entry), {'sourceSpeciesId','heightDecimeters','weightHectograms'})
                    for relative,data in outputs.items():
                        temporary=Path(directory)/profile/relative
                        temporary.parent.mkdir(parents=True,exist_ok=True);temporary.write_bytes(data)
                        retained=PROJECT_ROOT/'content/generated/sprites/pokemon'/profile/relative
                        self.assertTrue(generated_output_matches(retained,temporary.read_bytes()),str(retained))


def importer(name):
    path = PROJECT_ROOT / 'scripts/imports/presentation' / ('import-' + name + '.py')
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def asset(identity, width, height, pixels, inputs):
    return dict(id=identity, width=width, height=height, pixels=pixels)


class SharedPresentationTests(unittest.TestCase):
    def test_regional_builds_match_retained_pixels_and_metadata(self):
        for region in ('five-island', 'six-island'):
            with self.subTest(region=region):
                output, report = importer(region + '-presentation').build(PROJECT_ROOT, None, PROJECT_ROOT)
                self.assertTrue(report['passed'])
                generated = {name: data for name, data in output.items() if name.startswith('content/generated/')}
                self.assertTrue(generated)
                for name, data in generated.items():
                    self.assertTrue(generated_output_matches(PROJECT_ROOT / name, data), name)

    def test_ui_converters_rebuild_without_importer_code_dependencies(self):
        for name in ('celadon-approach-art', 'rocktunnel-art', 'route11-art',
                     'celadon-gym-art', 'rocket-hideout-art', 'rocket-hideout-terrain',
                     'pokemon-tower-art', 'pokemon-tower-presentation'):
            with self.subTest(importer=name), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                recipe_path = 'scripts/recipes/presentation/' + name + '.lock.json'
                recipe = json.loads((PROJECT_ROOT / recipe_path).read_bytes())
                self.assertFalse(any(row['path'].startswith('scripts/') for row in recipe.get('inputs', [])))
                copies = [recipe_path]
                copies += [row.get('localPath', 'content/raw/pokefirered/037335f4/' + row['path'])
                           for row in recipe['files']]
                copies += [row['path'] for row in recipe.get('inputs', [])]
                for relative in copies:
                    target = root / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes((PROJECT_ROOT / relative).read_bytes())
                module = importer(name)
                def build_at(base):
                    if name in ('celadon-approach-art', 'rocktunnel-art'):
                        data = {row['path']: (base / 'content/raw/pokefirered/037335f4' / row['path']).read_bytes()
                                for row in recipe['files']}
                        return module.build(data)
                    if name == 'route11-art':
                        locked = module.read_lock(base / recipe_path)
                        source = module.read_sources(locked, base / 'content/raw/pokefirered/037335f4')
                        return module.convert(locked, source)
                    if name in ('celadon-gym-art', 'rocket-hideout-art'):
                        _, source = module.authenticated(base)
                        return module.build(base, source)
                    if name == 'rocket-hideout-terrain':
                        return module.build(module.authenticated(base), base)
                    return module.build([base])
                self.assertEqual(build_at(root), build_at(PROJECT_ROOT))
                self.assertFalse((root / 'scripts/imports').exists())

    def test_selected_species_uses_source_positions_and_exact_floor_palette(self):
        source = Source(PROJECT_ROOT)
        sprites, icons, footprints = selected_species_art(source, [('Hoppip', 187)], [('Hoppip', 187)], asset)
        for row in sprites:
            image = source.read('graphics/pokemon/hoppip/' + row['side'] + '.png')
            with Image.open(io.BytesIO(image)) as im:
                indices = im.tobytes()
            colors = palette(source.read('graphics/pokemon/hoppip/' + row['paletteVariant'] + '.pal'), floor=True)
            self.assertEqual(row['pixels'], rgba(indices, colors))
            self.assertEqual(row['y'], (8 + row['sourceYOffset'] - row['sourceEnemyElevation'])
                             if row['side'] == 'front' else 48 + row['sourceYOffset'])
        self.assertEqual(icons[0]['sourceFrameIndex'], 0)
        self.assertEqual(len(icons[0]['pixels']), 32 * 32 * 4)
        self.assertEqual(len(footprints[0]['pixels']), 16 * 16 * 4)
        path = 'src/data/pokemon_graphics/front_pic_coordinates.h'
        original = source.text(path)
        import re
        source.raw[path] = re.sub(r'(\[SPECIES_HOPPIP\].*?\.y_offset = )(\d+)',
            lambda match: match[1] + str(int(match[2]) + 1), original, count=1, flags=re.S).encode()
        changed, _, _ = selected_species_art(source, [('Hoppip', 187)], [], asset)
        self.assertEqual(changed[0]['y'], sprites[0]['y'] + 1)
        self.assertEqual(changed[0]['pixels'], sprites[0]['pixels'])
        self.assertEqual(changed[2]['y'], sprites[2]['y'])

    def test_selected_species_rejects_binding_mutation(self):
        source = Source(PROJECT_ROOT)
        path = 'src/data/graphics/pokemon.h'
        source.raw[path] = source.read(path).replace(b'gMonFrontPic_Hoppip[]', b'gMonFrontPic_Other[]')
        with self.assertRaisesRegex(ValueError, 'Species image binding'):
            selected_species_art(source, [('Hoppip', 187)], [], asset)

    def test_selected_trainers_keep_source_music_modes_and_inherited_pixels(self):
        module = importer('six-island-presentation')
        source, assets = Source(PROJECT_ROOT), []
        trainers, portraits = selected_trainer_art(source, module.TRAINERS, module.INHERITED, asset, assets)
        twins = next(row for row in trainers if row['trainer'] == 'pret:TRAINER_TWINS_MIU_MIA')
        self.assertTrue(twins['doubleBattle'])
        self.assertFalse(twins['encounterMusicGender'] & 128)
        rose = next(row for row in trainers if row['trainer'] == 'pret:TRAINER_AROMA_LADY_ROSE')
        self.assertTrue(rose['encounterMusicGender'] & 128)
        self.assertTrue(any(row['inherited'] for row in portraits))
        for row in portraits:
            self.assertEqual((row['x'], row['y']), (144, 8))
        path = module.INHERITED['TWINS']
        original_reuse = source.reuse
        def changed_reuse(name):
            data = original_reuse(name)
            if name != path:
                return data
            with Image.open(io.BytesIO(data)) as im:
                im = im.convert('RGBA')
                red, green, blue, alpha = im.getpixel((0, 0))
                im.putpixel((0, 0), (red ^ 1, green, blue, alpha))
                output = io.BytesIO(); im.save(output, format='PNG')
                return output.getvalue()
        source.reuse = changed_reuse
        with self.assertRaisesRegex(ValueError, 'Inherited trainer source pixels'):
            selected_trainer_art(source, module.TRAINERS, module.INHERITED, asset, [])

    def test_shared_item_binding_has_one_source_of_truth(self):
        source = Source(PROJECT_ROOT)
        items = ItemSource(source.read)
        image, colors = items.icon_sources('ITEM_SUN_STONE')
        row, encoded = items.icon('ITEM_SUN_STONE')
        expected = PixelReader(source).pixels(image, colors, (24, 24))
        with Image.open(io.BytesIO(encoded)) as im:
            self.assertEqual(im.convert('RGBA').tobytes(), expected)
        self.assertEqual((row['width'], row['height']), (24, 24))

    def test_ui_tilemap_screenblock_flip_and_hidden_rgb(self):
        tiles = [bytes((x + y * 3) % 16 for y in range(8) for x in range(8))]
        colors = [(i * 10 + 1, i * 10 + 2, i * 10 + 3) for i in range(16)]
        entries = [0] * 2048
        entries[31 * 32] = 0x1400
        entries[1024] = 0x1800
        pixels = ui_tiles.render_map(tiles, struct.pack('<2048H', *entries), {1: colors}, 8, 16, top=31)
        expected = ui_tiles.tile_rgba(tiles[0], colors, hflip=True) + ui_tiles.tile_rgba(tiles[0], colors, vflip=True)
        self.assertEqual(pixels, expected)
        data = ('JASC-PAL\n0100\n80\n' + '32 64 96\n' * 80).encode()
        self.assertEqual(ui_tiles.palette(data, 80), [(33, 66, 99)] * 80)
        with self.assertRaises(ValueError):
            ui_tiles.palette(data)
        with self.assertRaises(ValueError):
            ui_tiles.palette(data, 16)


if __name__ == '__main__':
    unittest.main()
