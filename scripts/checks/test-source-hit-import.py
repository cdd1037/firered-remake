#!/usr/bin/env python3
"""Current hit/explosion importer format, determinism and publication contracts."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import importlib.util
import json
import io
import re
import struct
from PIL import Image
from indexed_material_expectations import decode_indexed
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT=_PROJECT_ROOT
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('output',type=Path)
parser.add_argument('--reference-root',type=Path,default=ROOT/'content/raw/pokefirered/037335f4')
args=parser.parse_args()
args.output.mkdir(parents=True,exist_ok=False)
checks=[]
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append(label)

for name,asset in [('hit','graphics/battle_anims/sprites/impact.png'),('explosion','graphics/battle_anims/sprites/explosion.png')]:
    script=ROOT/'scripts/imports/battle'/f'import-source-{name}-animations.py'
    spec=importlib.util.spec_from_file_location('import_'+name,script)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    original=module.build(args.reference_root)
    if name=='hit':
        raw=module.read_sources(args.reference_root);files=original['source-hit-animations'];manifest=json.loads(files['manifest.json'])
        indexed=manifest['indexedAssets'];check(len(indexed)==1 and indexed[0]['id']=='impact','one canonical indexed impact material')
        row=indexed[0];check((row['width'],row['height'],row['paletteTag'])==(32,32,'ANIM_TAG_IMPACT'),'impact native canvas and palette identity')
        with Image.open(io.BytesIO(raw[asset])) as im:indices=im.tobytes();palette=im.getpalette()[:48]
        packed=files[row['indicesPath']];decoded=bytes((packed[(y//8*4+x//8)*32+(y%8)*4+(x%8)//2]>>((x&1)*4))&15 for y in range(32) for x in range(32))
        colors=[(palette[i]>>3)|((palette[i+1]>>3)<<5)|((palette[i+2]>>3)<<10) for i in range(0,48,3)]
        check(decoded==indices and len(packed)==512,'impact every native 4bpp index retained')
        check(files[row['palettePath']]==struct.pack('<16H',*colors),'impact all sixteen source palette entries retained')
        check(decode_indexed(32,32,packed,files[row['palettePath']])==tuple(colors[i]|32768 if i else 0 for i in indices),'impact indexed output preserves every visible source RGB555 pixel')
        check('impact.rgb555' not in files and all(r['id']!='impact' for r in manifest['assets']),'impact has one canonical indexed output')
        constants=raw['include/constants/battle_anim.h'].decode();tag_base=int(re.search(r'^#define ANIM_SPRITES_START\s+(\d+)',constants,re.M)[1]);tags={symbol:tag_base+int(offset) for symbol,offset in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',constants,re.M)}
        check(all(r['spriteTags']=={c['args'][0]:tags[c['args'][0]] for c in r['commands'] if c['op']=='loadspritegfx'} for r in manifest['scripts']),'basic hit roots bind only their actual source load tags')
        for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Impact, 0x0200, ANIM_TAG_IMPACT}',b'{gBattleAnimSpriteGfx_Impact, 0x0400, ANIM_TAG_IMPACT}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Impact, ANIM_TAG_IMPACT}',b'{gBattleAnimSpritePal_Impact2, ANIM_TAG_IMPACT}'),('src/graphics.c',b'graphics/battle_anims/sprites/impact.gbapal.lz',b'graphics/battle_anims/sprites/impact_2.gbapal.lz')]:
            altered=dict(raw);check(old in altered[path],'impact mutation anchor exists');altered[path]=altered[path].replace(old,new);rejected=False
            try:module.indexed_impact(altered)
            except ValueError:rejected=True
            check(rejected,'impact changed source registration/material rejected')
    check(module.build(args.reference_root)==original,name+' deterministic generation')
    with tempfile.TemporaryDirectory(prefix='firered-animation-test-') as temporary:
        cache=Path(temporary)/'source';cache.mkdir()
        for path,data in module.read_sources(args.reference_root).items():
            target=cache/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
        check(module.build(cache)==original,name+' equivalent extracted source cache')
        image=cache/asset;valid=image.read_bytes();image.write_bytes(b'not-a-PNG')
        output=Path(temporary)/'output'
        def command():
            return [sys.executable,str(script),str(cache),str(output)] if name=='hit' else [sys.executable,str(script),'--reference',str(cache),'--output',str(output)]
        result=subprocess.run(command(),capture_output=True,text=True)
        check(result.returncode!=0 and not output.exists(),name+' invalid image publishes no partial output')
        image.write_bytes(valid)
        result=subprocess.run(command(),capture_output=True,text=True)
        check(result.returncode==0,name+' valid CLI generation: '+result.stderr[-300:])
        if name=='hit':
            expected={folder+'/'+file:data for folder,files in original.items() for file,data in files.items()}
            check(len(json.loads(original['campaign-hit-art']['manifest.json'])['geometry'])==880,'complete front/back geometry')
        else:
            expected=dict(zip(['manifest.json','explosion.rgb555'],original))
            recipe=json.loads(original[0])
            check(len(recipe['scripts'])==2,'both source explosion scripts')
            flash=next(c for r in recipe['scripts'] if r['moveId']==153 for c in r['commands'] if c['op']=='createsprite')
            check(flash['args'][6]=='RGB(26, 8, 8)' and flash['args'][-1]=='8','original blend operands')
        check(all((output/path).read_bytes()==data for path,data in expected.items()),name+' exact CLI bytes')
        before={p.relative_to(output):p.read_bytes() for p in output.rglob('*') if p.is_file()}
        result=subprocess.run(command(),capture_output=True,text=True)
        expected_status=result.returncode!=0 if name=='hit' else result.returncode==0
        check(expected_status and before=={p.relative_to(output):p.read_bytes() for p in output.rglob('*') if p.is_file()},name+' repeated CLI retains identical bytes')

form_result=subprocess.run([sys.executable,str(ROOT/'scripts/checks/test-castform-art.py'),
    '--root',str(ROOT),'--reference',str(args.reference_root)],capture_output=True,text=True)
check(form_result.returncode==0,'source form pixels, geometry, cache and malformed inputs: '+form_result.stderr[-300:])

report=dict(passed=True,checks=len(checks),details=checks)
(args.output/'result.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report))
