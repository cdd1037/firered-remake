#!/usr/bin/env python3
"""Offline pewter Pokemon presentation and offline normal/shiny battle pixels.

Only immutable local Git objects or local raw input files are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT


import argparse
import pokemon_import as pokemon
from pokemon_import import source_symbol
from pokemon_import_io import canonical, load_recipe, import_region
import json
import os
import pathlib
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-pewter-pokemon-v1'
NEW_SPECIES = ('Geodude', 'Graveler', 'Golem', 'Sandshrew', 'Sandslash', 'Onix', 'Steelix')
ART_SPECIES = (('Geodude', 74), ('Graveler', 75), ('Golem', 76), ('Sandshrew', 27),
               ('Sandslash', 28), ('Onix', 95), ('Steelix', 208))


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/recipes/pokemon/pewter-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Seven forward-family species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, inputCount=len(lock['files']), addedSpeciesCount=7, composedSpeciesCount=29, artSpeciesCount=7, textureCount=28, rgbaCount=28, outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    return import_region(raw, out, recipe=load_lock(lock_path), convert=convert,
                         repository=repository)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/sprites/pokemon/pewter')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/pokemon/pewter-pokemon.lock.json')
    args = parser.parse_args()
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
