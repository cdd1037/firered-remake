#!/usr/bin/env python3
"""Compile the whiteout home message into runtime text data from pret strings.c."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,json,re
from pathlib import Path
ROOT=_PROJECT_ROOT
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');a=p.parse_args()
 source=(ROOT/'content/raw/pokefirered/037335f4/src/strings.c').read_text()
 match=re.search(r'const u8 gText_PlayerScurriedBackHome\[\] = _\("((?:\\.|[^"\\])*)"\);',source)
 if match is None:raise ValueError('Missing whiteout home text')
 data=(json.dumps({'gText_PlayerScurriedBackHome':match.group(1)+'$'},ensure_ascii=False,indent=2)+'\n').encode()
 target=ROOT/'content/generated/maps/regions/viridian/ui-texts.json'
 if a.check:
  if target.read_bytes()!=data:raise ValueError('Generated whiteout text differs')
 else:target.write_bytes(data)
 print('Whiteout home text verified' if a.check else 'Whiteout home text generated')
if __name__=='__main__':main()
