#!/usr/bin/env python3
"""Independent source form pixels, geometry and extracted-cache importer checks."""
import argparse,importlib.util,json,struct,sys,tempfile
from pathlib import Path
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path,required=True);p.add_argument('--producer-root',type=Path);a=p.parse_args()
sys.path.insert(0,str(a.root/'scripts/lib'));producer=a.producer_root or a.root
spec=importlib.util.spec_from_file_location('hit_forms',producer/'scripts/imports/battle/import-source-hit-animations.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);m._PROJECT_ROOT=producer
raw=m.read_sources(a.reference);built=m.build(a.reference);art=built['campaign-hit-art'];doc=json.loads(art['manifest.json']);count=0
for row in doc['assets']:
 if 'form' not in row:continue
 folder=['normal','sunny','rainy','snowy'][row['form']];source=a.reference/f'graphics/pokemon/castform/{folder}'
 with Image.open(source/(row['side']+'.png')) as image:indices=image.tobytes();assert image.size==(64,64)
 colors=[tuple(map(int,line.split())) for line in (source/(row['paletteVariant']+'.pal')).read_text().splitlines()[3:]]
 pixels=art[row['path']];assert struct.unpack_from('<HH',pixels)==(64,64)
 for index,value in zip(indices,struct.unpack_from('<4096H',pixels,4)):
  red,green,blue=colors[index];assert value==red//8+(green//8)*32+(blue//8)*1024+(32768 if index else 0);count+=1
expected=[(32,32,17,13),(48,48,9,14),(32,48,9,13),(64,48,8,13)]
assert len(doc['formGeometry'])==8 and count==65536
for row in doc['formGeometry']:
 assert tuple(row[k] for k in ('sourceWidth','sourceHeight','yOffset','elevation'))==(expected[row['form']] if row['side']=='front' else (32,40,0,0))
with tempfile.TemporaryDirectory(prefix='castform-inputs-') as temp:
 cache=Path(temp)
 for path,data in raw.items():
  dest=cache/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
 assert m.build(cache)==built
 # Explicitly reject mismatched source palette ordering and malformed source palette dimensions.
 rules=cache/'graphics_file_rules.mk';original=rules.read_text();rules.write_text(original.replace('$(CASTFORMGFXDIR)/sunny/shiny.gbapal','$(CASTFORMGFXDIR)/rainy/shiny.gbapal'))
 try:m.build(cache)
 except ValueError:pass
 else:raise AssertionError('mismatched palette order was accepted')
 rules.write_text(original)
 palette=cache/'graphics/pokemon/castform/normal/normal.pal';palette.write_text(palette.read_text().replace('16\n','15\n',1))
 try:m.build(cache)
 except ValueError:pass
 else:raise AssertionError('malformed palette size was accepted')
print('65,536 independent pixels, eight geometry rows, extracted-cache equality and two malformed-input rejections passed.')
