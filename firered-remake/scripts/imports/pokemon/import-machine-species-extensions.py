#!/usr/bin/env python3
"""Compile complete TM/HM compatibility additions for four campaign species."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import pathlib
import re
from machine_catalog import load_catalog, compatibility_reference
ROOT=_PROJECT_ROOT
GROUPS={'pokemon-tower':[('GASTLY',92),('HAUNTER',93)],'snorlax':[('SNORLAX',143)],'eevee':[('EEVEE',133)]}

def convert(source, species):
    catalog = load_catalog(ROOT)
    return [dict(item=dict(value='pret:ITEM_' + item), compatibility=[
        compatibility_reference(catalog, 'pret:ITEM_' + item, 'pret:SPECIES_' + name) for name, _ in species])
        for item in [f'TM{i:02}' for i in range(1,51)] + [f'HM{i:02}' for i in range(1,9)]]

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');a=p.parse_args()
    source=(ROOT/'content/raw/pokefirered/037335f4/src/data/pokemon/tmhm_learnsets.h').read_text()
    for name,species in GROUPS.items():
        path=ROOT/f'content/generated/{name}-machines/compatibility.json'
        data=(json.dumps(convert(source,species),sort_keys=True,separators=(',',':'))+'\n').encode()
        if a.check:
            if path.read_bytes()!=data:raise ValueError('Machine extension differs: '+name)
        else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    print(json.dumps(dict(species=4,machines=58,checked=a.check)))
if __name__=='__main__':main()
