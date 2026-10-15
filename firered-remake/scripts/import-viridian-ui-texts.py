#!/usr/bin/env python3
"""Compile the whiteout home message into runtime text data from pret strings.c."""
import argparse,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');a=p.parse_args()
 source=(ROOT/'content/raw/pokefirered/037335f4/src/strings.c').read_text()
 match=re.search(r'const u8 gText_PlayerScurriedBackHome\[\] = _\("((?:\\.|[^"\\])*)"\);',source)
 if match is None:raise ValueError('Missing whiteout home text')
 data=(json.dumps({'gText_PlayerScurriedBackHome':match.group(1)+'$'},ensure_ascii=False,indent=2)+'\n').encode()
 target=ROOT/'content/generated/viridian-region/ui-texts.json'
 if a.check:
  if target.read_bytes()!=data:raise ValueError('Generated whiteout text differs')
 else:target.write_bytes(data)
 print('Whiteout home text verified' if a.check else 'Whiteout home text generated')
if __name__=='__main__':main()
