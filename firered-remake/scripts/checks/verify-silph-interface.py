#!/usr/bin/env python3
"""Separate cold reconstruction and rejection checks for the Silph elevator scroll sprites."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,importlib.util,io,json,pathlib,shutil,sys,tempfile
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
spec=importlib.util.spec_from_file_location('silph_interface',ROOT/'scripts/imports/presentation/import-silph-interface.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path,required=True);a=p.parse_args();results=[]
with tempfile.TemporaryDirectory(prefix='silph-interface-cold-') as temp:
 root=pathlib.Path(temp);(root/'scripts/recipes/presentation').mkdir(parents=True);shutil.copy2(a.project_root/'scripts/recipes/presentation/silph-interface.lock.json',root/'scripts/recipes/presentation/silph-interface.lock.json');first=m.build(root,a.reference_root);second=m.build(root)
 assert first==second and len(first)==3;results.append('Two cold generations from authenticated source are identical')
 for path,b in first.items():assert (a.project_root/'content/generated/ui/silph-interface'/path).read_bytes()==b
 results.append('Every cold output equals retained artifact')
 for name in ('scroll-up','scroll-down'):assert Image.open(io.BytesIO(first[name+'.png'])).size==(16,16)
 results.append('Both generated PNG sprites retain the source dimensions')
 for relative in ('scripts/recipes/presentation/silph-interface.lock.json','content/raw/pokefirered/037335f4/graphics/interface/red_arrow.pal','content/raw/pokefirered/037335f4/graphics/interface/red_arrow_other.png','content/raw/pokefirered/037335f4/src/trig.c'):
  target=root/relative;original=target.read_bytes();bad=bytearray(original);bad[len(bad)//2]^=1;target.write_bytes(bad)
  try:
   try:m.build(root);raise AssertionError('Corruption accepted')
   except ValueError:results.append('Corruption rejected: '+relative)
  finally:target.write_bytes(original)
assert len(results)==7
print(json.dumps(dict(passed=True,tests=len(results),checks=results),indent=2))
