#!/usr/bin/env python3
"""Offline importer-to-atlas RGBA parity and deterministic partial regeneration."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from PIL import Image

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from item_icon_exports import ATLAS_PATH, item_icon_path, output_materials, pack_project

PACKAGES = (
    ('bill-items', 'data/items/bill/bill-items.json'),
    ('ssanne-items', 'data/items/ssanne/ssanne-items.json'),
    ('route11-items', 'data/items/route11/route11-items.json'),
    ('mtmoon-items', 'data/items/mtmoon/mtmoon-items.json'),
    ('rocktunnel-items', 'data/items/rocktunnel/rocktunnel-items.json'),
    ('cerulean-items', 'data/items/cerulean/cerulean-items.json'),
    ('early-shop', 'data/services/early-shop/early-shop.json'),
    ('silph-item-art', 'data/services/silph-item-art.json'),
)

def load(package):
    spec = importlib.util.spec_from_file_location(package, ROOT / f'scripts/imports/items/import-{package}.py')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module

def rgba(data):
    with Image.open(io.BytesIO(data)) as image:
        return image.size, image.convert('RGBA').tobytes()

def pixels(root, icon):
    with Image.open(root / icon['pngPath']) as page:
        r=icon['region']; image=page.convert('RGBA').crop((r['x'],r['y'],r['x']+r['width'],r['y']+r['height']))
        return image.size,image.tobytes()

def logical(data):
    if isinstance(data,list): return [logical(v) for v in data]
    if not isinstance(data,dict): return data
    result={k:logical(v) for k,v in data.items() if k not in ('region','outputs')}
    if 'pngPath' in result and 'item' in result:
        # Pixel equality is checked by item identity below; atlas placement and
        # transient importer filenames are deliberately different.
        result.pop('pngPath')
    return result

class ItemIconExportsTests(unittest.TestCase):
    def test_palette_is_part_of_semantic_name(self):
        self.assertEqual(item_icon_path('icons/tm.png','palettes/normal.pal'),'content/generated/sprites/item-icons/tm-normal.png')
        self.assertNotEqual(item_icon_path('icons/tm.png','palettes/normal.pal'),item_icon_path('icons/tm.png','palettes/fire.pal'))
        self.assertEqual(item_icon_path('icons/super_potion.png','palettes/super_potion.pal'),'content/generated/sprites/item-icons/super-potion.png')

    def test_custom_output_keeps_shared_files_beside_package(self):
        output=Path('/tmp/custom/generated/example')
        self.assertEqual(output_materials(output,{'content/generated/sprites/item-icons/potion.png':b'x'}),{(output.parent,'sprites/item-icons/potion.png'):b'x'})

    def test_all_eight_packages_regenerate_exact_regions(self):
        count=0
        for package, relative_document in PACKAGES:
            with self.subTest(package=package):
                module=load(package)
                if package=='silph-item-art': outputs=module.build(ROOT)
                elif package=='early-shop':
                    with tempfile.TemporaryDirectory() as directory:
                        out=Path(directory)/'generated'/package
                        module.convert(ROOT/'content/raw/pokefirered/037335f4',out)
                        outputs={str(p.relative_to(out)) if p.is_relative_to(out) else 'content/generated/'+str(p.relative_to(out.parent)):p.read_bytes()
                                 for p in out.parent.rglob('*') if p.is_file()}
                else:
                    lock=module.lock_read(); outputs=module.convert(lock,module.read_sources(lock,ROOT/'content/raw/pokefirered/037335f4'))
                document_path=ROOT/'content/generated'/relative_document
                document=json.loads(document_path.read_bytes())
                rebuilt_icons = {}
                for name, data in outputs.items():
                    if not name.endswith('.json'): continue
                    rebuilt = json.loads(data)
                    for row in rebuilt.get('icons', [rebuilt.get('icon')]):
                        if row is not None: rebuilt_icons[row['item']['value']] = row
                for icon in document.get('icons',[document.get('icon')]):
                    count+=1
                    self.assertEqual(icon['pngPath'],ATLAS_PATH)
                    source=rebuilt_icons[icon['item']['value']]['pngPath']
                    self.assertEqual(pixels(ROOT,icon),rgba(outputs[source]),f'{package}: {icon["item"]}')
                    self.assertFalse((ROOT/source).exists())
                for path,data in outputs.items():
                    if path.startswith('content/generated/sprites/item-icons/'):continue
                    target=document_path if package=='silph-item-art' else document_path.parent/path
                    if path.endswith('.png'):self.assertEqual(rgba(data),rgba(target.read_bytes()))
                    else:self.assertEqual(logical(json.loads(data)),logical(json.loads(target.read_bytes())),str(target))
        self.assertEqual(count,82)

    def test_idempotence_and_single_family_reimport_preserve_other_regions(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            def family(package,color):
                folder=root/'content/generated/data/items'/package;folder.mkdir(parents=True,exist_ok=True)
                image=Image.new('RGBA',(24,24),color);image.putpixel((7,8),(99,88,77,0))
                image.save(folder/'item.png')
                data={'icons':[{'item':{'value':'pret:'+package},'width':24,'height':24,'pngPath':'item.png'}],
                      'outputs':[{'path':'item.png'}]}
                (folder/'items.json').write_text(json.dumps(data))
                return image.tobytes()
            first=family('bill',(10,20,30,255));second=family('ssanne',(50,60,70,255))
            dependency=root/'content/generated/sprites/pokemon/power-plant/manifest.json'
            dependency.parent.mkdir(parents=True)
            dependency.write_text(json.dumps({'inheritedInputs':[{'path':'content/generated/data/items/bill/item.png'}]}))
            self.assertEqual(pack_project(root)['retiredPngFiles'],2)
            self.assertEqual(json.loads(dependency.read_text())['inheritedInputs'][0]['path'],ATLAS_PATH)
            before={str(p.relative_to(root)):p.read_bytes() for p in root.rglob('*') if p.is_file()}
            self.assertEqual(pack_project(root)['updatedDocuments'],0)
            self.assertEqual(before,{str(p.relative_to(root)):p.read_bytes() for p in root.rglob('*') if p.is_file()})
            family('bill',(10,20,30,255))
            self.assertEqual(pack_project(root)['retiredPngFiles'],1)
            for package,expected in [('bill',first),('ssanne',second)]:
                row=json.loads((root/'content/generated/data/items'/package/'items.json').read_text())['icons'][0]
                self.assertEqual(pixels(root,row)[1],expected)
            self.assertEqual(before,{str(p.relative_to(root)):p.read_bytes() for p in root.rglob('*') if p.is_file()})
            # A dependent family can retain a region even when its original owner
            # is reimported with different pixels. Repacking must retain both.
            retained=json.loads((root/'content/generated/data/items/bill/items.json').read_text())['icons'][0]
            dependency.write_text(json.dumps({'icons':[retained]}))
            replacement=family('bill',(200,100,50,255))
            self.assertEqual(pack_project(root)['uniqueImages'],3)
            updated=json.loads((root/'content/generated/data/items/bill/items.json').read_text())['icons'][0]
            inherited=json.loads(dependency.read_text())['icons'][0]
            self.assertEqual(pixels(root,updated)[1],replacement)
            self.assertEqual(pixels(root,inherited)[1],first)
            self.assertEqual(pack_project(root)['updatedDocuments'],0)

if __name__=='__main__':unittest.main()
