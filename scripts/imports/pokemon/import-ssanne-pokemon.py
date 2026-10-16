#!/usr/bin/env python3
"""Offline SsAnne Pokemon presentation and source RGBA art.
No network or upstream execution.
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
PROFILE = 'firered-ssanne-pokemon-v1'
NEW_SPECIES = ('Growlithe', 'Ponyta', 'Tentacool')
ART_SPECIES = tuple(zip(NEW_SPECIES, (58, 77, 72)))
LEGACY_SPECIES = (
    'BULBASAUR', 'IVYSAUR', 'VENUSAUR', 'CHARMANDER', 'CHARMELEON', 'CHARIZARD', 'SQUIRTLE',
    'WARTORTLE', 'BLASTOISE', 'PIDGEY', 'PIDGEOTTO', 'PIDGEOT', 'RATTATA', 'RATICATE', 'WEEDLE',
    'KAKUNA', 'BEEDRILL', 'CATERPIE', 'METAPOD', 'BUTTERFREE', 'PIKACHU', 'RAICHU', 'GEODUDE',
    'GRAVELER', 'GOLEM', 'SANDSHREW', 'SANDSLASH', 'ONIX', 'STEELIX', 'SPEAROW', 'FEAROW', 'MANKEY',
    'EKANS', 'NIDORAN_F', 'NIDORINA', 'NIDORAN_M', 'NIDORINO', 'JIGGLYPUFF', 'MAGIKARP', 'GYARADOS',
    'ARBOK', 'CLEFAIRY', 'ZUBAT', 'GOLBAT', 'ODDISH', 'PARAS', 'PARASECT', 'BELLSPROUT',
    'MAGNEMITE', 'GRIMER', 'VOLTORB', 'KOFFING', 'NIDOQUEEN', 'NIDOKING', 'CLEFABLE', 'WIGGLYTUFF',
    'ABRA', 'KADABRA', 'MACHOP', 'DROWZEE', 'SHELLDER', 'HORSEA', 'GOLDEEN', 'STARYU', 'STARMIE',
    'SLOWPOKE', 'GLOOM', 'WEEPINBELL', 'MEOWTH', 'PERSIAN',
)


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/recipes/pokemon/ssanne-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


def supplement(lock, sources):
    return pokemon.supplement(lock, sources, profile=PROFILE, species=ART_SPECIES,
        weight_species=LEGACY_SPECIES + tuple(source_symbol(name) for name in NEW_SPECIES),
        natural_level_limit=None)


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Three S.S. Anne dependency species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs.update(supplement(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, inputCount=len(lock['files']), addedSpeciesCount=3, composedSpeciesCount=73, artSpeciesCount=3, textureCount=18, rgbaCount=18, outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    return import_region(raw, out, recipe=load_lock(lock_path), convert=convert,
                         repository=repository)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, help='Explicit raw cache rebuilds offline unless --reference-repo is also supplied')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/sprites/pokemon/ssanne')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/pokemon/ssanne-pokemon.lock.json')
    args = parser.parse_args()
    explicit_raw = args.raw is not None
    args.raw = args.raw or ROOT / 'content/raw/pokefirered/037335f4'
    if args.reference_repo is None and not explicit_raw:
        configured = os.environ.get('FIRERED_ORIGINAL_REFERENCE')
        if configured: args.reference_repo = pathlib.Path(configured)
        elif not args.raw.exists(): args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))

if __name__ == '__main__': main()
