#!/usr/bin/env python3
"""Current hit/explosion importer format, determinism and publication contracts."""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT=Path(__file__).resolve().parents[1]
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
    script=ROOT/'scripts'/f'import-source-{name}-animations.py'
    spec=importlib.util.spec_from_file_location('import_'+name,script)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    original=module.build(args.reference_root)
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

report=dict(passed=True,checks=len(checks),details=checks)
(args.output/'result.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report))
