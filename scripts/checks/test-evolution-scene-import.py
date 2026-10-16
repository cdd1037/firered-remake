#!/usr/bin/env python3
"""Offline scene import parity, atlas geometry and malformed-source checks."""
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('evolution_scene',ROOT/'scripts/imports/presentation/import-evolution-scene.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
raw=ROOT/'content/raw/pokefirered/037335f4'
with tempfile.TemporaryDirectory(prefix='evolution-scene-import-') as directory:
    work=Path(directory);out=work/'generated'
    result=module.convert(raw,out)
    assert result==dict(paletteCount=50,textures=4),result
    current=ROOT/'content/generated/ui/evolution-scene'
    assert {p.name for p in out.iterdir()}=={p.name for p in current.iterdir()}
    for path in out.iterdir():
        assert path.read_bytes()==(current/path.name).read_bytes(),path.name
    for layer in range(2):
        image=Image.open(out/f'background-{layer}.png')
        assert image.size==(2560,1280)
        assert image.getbbox() is not None
        # Initial/final palette is black but nonzero tiles retain their shape/alpha.
        assert image.crop((0,0,256,256)).getbbox() is not None
        assert image.crop((3*256,256,4*256,512)).getbbox() is not None
    assert Image.open(out/'hatch.png').size==(240,160)
    bad=work/'raw'
    paths=['src/evolution_scene.c','graphics/evolution_scene/bg.bin','graphics/evolution_scene/bg2.bin','graphics/evolution_scene/bg.png','graphics/evolution_scene/sparkle.png','graphics/evolution_scene/transition.pal','graphics/trade/gba.png','graphics/trade/gba_pal2.pal','graphics/trade/shadow_map.bin']
    for name in paths:
        target=bad/name;target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(raw/name,target)
    path=bad/'graphics/evolution_scene/bg.bin';data=bytearray(path.read_bytes());data[0]=255;data[1]=163;path.write_bytes(data)
    try:module.convert(bad,work/'rejected')
    except ValueError as error:assert 'Invalid tile reference' in str(error)
    else:raise AssertionError('Out-of-range tilemap was accepted')
    assert not (work/'rejected').exists()
print('PASS: five generated files byte-identical; two packed palette atlases; hatch background; out-of-range tile rejected before output.')
