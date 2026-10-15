#!/usr/bin/env python3
"""Compile TM39 compatibility extensions for Route 22 and Route 3."""
import argparse
import json
import pathlib
import re
from machine_catalog import load_catalog, compatibility_reference
ROOT = pathlib.Path(__file__).resolve().parents[1]
GROUPS = {'route22': [('SPEAROW',21),('FEAROW',22),('MANKEY',56)],
          'route3': [('EKANS',23),('NIDORAN_F',29),('NIDORINA',30),('NIDORAN_M',32),('NIDORINO',33),('JIGGLYPUFF',39),('MAGIKARP',129),('GYARADOS',130)]}

def convert(source, species):
    catalog = load_catalog(ROOT)
    return [compatibility_reference(catalog, 'pret:ITEM_TM39', 'pret:SPECIES_' + name) for name, _ in species]

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');a=p.parse_args()
    source=(ROOT/'content/raw/pokefirered/037335f4/src/data/pokemon/tmhm_learnsets.h').read_text()
    for name,species in GROUPS.items():
        path=ROOT/f'content/generated/pewter-machines/{name}-compatibility.json'
        data=(json.dumps(convert(source,species),separators=(',',':'))+'\n').encode()
        if a.check:
            if path.read_bytes()!=data:raise ValueError('Compatibility output differs: '+name)
        else:path.write_bytes(data)
    print(json.dumps(dict(species=11,checked=a.check)))
if __name__=='__main__':main()
