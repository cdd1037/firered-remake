#!/usr/bin/env python3
"""Rebuild the authenticated Eevee normal cry ID; no rendered audio or download."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('eevee_metadata', ROOT / 'scripts/import-eevee-pokemon.py')
meta = importlib.util.module_from_spec(spec)
spec.loader.exec_module(meta)
MANIFEST_SHA = '9fa31db7c63aa529a1b757a4cb795ad011d23d9834d959bad9f4c90277a2cbb3'
MANIFEST_BYTES = 484
RELATIVE_PATH = 'content/generated/eevee-synth-audio/manifest.json'


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
    cue = dict(id='CRY_EEVEE_NORMAL', sourceCommit=meta.COMMIT,
               sourceSymbol='SPECIES_EEVEE', sourcePath='sound/direct_sound_samples/cries/eevee.wav',
               sourcePlayer=4, priority=10, sourceCounter=None, songAddress=0,
               species=133, looping=False, cryMode=0)
    return (json.dumps(dict(schemaVersion=1, sourceCommit=meta.COMMIT, cues=[cue]), indent=2) + '\n').encode()


def run(root=ROOT, check=False):
    data = build(root)
    if len(data) != MANIFEST_BYTES or hashlib.sha256(data).hexdigest() != MANIFEST_SHA:
        raise ValueError('Reviewed Eevee synth manifest differs')
    path = root / RELATIVE_PATH
    if check:
        if meta.read(root, RELATIVE_PATH) != data:
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
