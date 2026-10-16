#!/usr/bin/env python3
"""Rebuild the authenticated Eevee normal cry ID; no rendered audio or download."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
spec = importlib.util.spec_from_file_location('eevee_metadata', ROOT / 'scripts/imports/pokemon/import-eevee-pokemon.py')
meta = importlib.util.module_from_spec(spec)
spec.loader.exec_module(meta)
RELATIVE_PATH = 'content/generated/audio/cues/eevee.json'


def build(root):
    _, source = meta.authenticated(root)
    species = re.search(r'^#define SPECIES_EEVEE\s+(\d+)\s*$', source['include/constants/species.h'].decode(), re.M)
    if species is None or int(species[1]) != 133:
        raise ValueError('Eevee species constant differs')
    table = source['sound/cry_tables.inc'].decode()
    normal = table.split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0]
    cries = re.findall(r'^\s*cry (Cry_\w+)\s*$', normal, re.M)
    if len(cries) != 388 or cries[132] != 'Cry_Eevee':
        raise ValueError('Eevee normal cry table mapping differs')
    cue = dict(id='CRY_EEVEE_NORMAL', sourceSymbol='SPECIES_EEVEE', sourcePlayer=4, priority=10, sourceCounter=None, songAddress=0, species=133, looping=False, cryMode=0)
    return runtime_manifest_bytes(dict(schemaVersion=1, cues=[cue]))


def run(root=ROOT, check=False):
    data = build(root)
    path = root / RELATIVE_PATH
    if check:
        if path.read_bytes() != data:
            raise ValueError('Eevee synth manifest bytes differ')
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    print('PASS Eevee one normal synth cue ID, source species and cry table, exact manifest bytes; no audio produced')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    run(args.project_root, args.check)
