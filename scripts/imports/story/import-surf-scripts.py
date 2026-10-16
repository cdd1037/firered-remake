#!/usr/bin/env python3
"""Materialize or check the exact Surf script and text blobs; execute no upstream scripts."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,json,pathlib,subprocess
ROOT=_PROJECT_ROOT
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);a=p.parse_args()
 b=(a.project_root/'scripts/recipes/story/surf-scripts.lock.json').read_bytes()
 lock=json.loads(b)
 for row in lock['files']:
  path=a.project_root/'content/raw/pokefirered/037335f4'/row['path'];b=subprocess.check_output(['git','-C',str(a.reference_root),'show',lock['sourceCommit']+':'+row['path']]) if a.reference_root else path.read_bytes()
  if a.reference_root:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
 print('Authenticated3 Surf script/text/constants blobs')
if __name__=='__main__':main()
