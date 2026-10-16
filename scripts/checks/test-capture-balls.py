#!/usr/bin/env python3
"""Verify native ball palette-index overlays and generated-only atlas reconstruction."""
import importlib.util, pathlib, tempfile, subprocess, sys, json
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('capture_import',ROOT/'scripts/imports/presentation/import-capture-balls.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
raw=module.RAW
with tempfile.TemporaryDirectory() as tmp:
    subprocess.run([sys.executable,str(ROOT/'scripts/imports/presentation/import-capture-balls.py'),'--raw',str(raw),'--out',tmp],check=True)
    subprocess.run([sys.executable,str(ROOT/'scripts/imports/presentation/import-capture-balls.py'),'--out',tmp,'--check'],check=True)
    atlas=Image.open(pathlib.Path(tmp)/'balls.png').convert('RGBA')
    opening=Image.open(raw/'graphics/interface/ball_open.png')
    for i,name in enumerate(module.BALLS):
        image=Image.open(raw/f'graphics/interface/ball/{name}.png')
        indices=list(image.getdata());pal=image.getpalette()
        if name not in ('dive','luxury','premier'):indices[512:]=list(opening.getdata())
        expected=[]
        for index in indices:
            rgb=pal[index*3:index*3+3]
            expected.append(tuple(((c>>3)<<3)|((c>>3)>>2) for c in rgb)+(0 if index==0 else 255,))
        actual=list(atlas.crop((i*16,0,i*16+16,48)).getdata())
        assert actual==expected,name
    manifest=json.loads((pathlib.Path(tmp)/'balls.json').read_text())
    assert len(manifest['balls'])==12 and len(manifest['sine'])==320
    assert 'source' not in json.dumps(manifest).lower()
    assert Image.open(pathlib.Path(tmp)/'particles.png').size==(8,64)
    # Check mode must fail for corrupted output and leave it unchanged.
    path=pathlib.Path(tmp)/'balls.png';path.write_bytes(b'bad')
    result=subprocess.run([sys.executable,str(ROOT/'scripts/imports/presentation/import-capture-balls.py'),'--out',tmp,'--check'],capture_output=True)
    assert result.returncode!=0 and path.read_bytes()==b'bad'
print('PASS: all 36 native ball frames, palette overlay exceptions, 8 particle tiles, independent output and failure checks')
