#!/usr/bin/env python3
"""Offline RockTunnel Pokemon presentation and source RGBA art.
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
PROFILE = 'firered-rocktunnel-pokemon-v1'
NEW_SPECIES = ('Cubone', 'Electrode', 'Hypno', 'Lickitung', 'Machoke', 'Marowak', 'Primeape', 'Rhyhorn', 'Seadra', 'Seaking', 'Tentacruel', 'Venomoth', 'Venonat', 'Weezing')
ART_SPECIES = (('Cubone', 104), ('Electrode', 101), ('Hypno', 97), ('Lickitung', 108), ('Machoke', 67), ('Marowak', 105), ('Primeape', 57), ('Rhyhorn', 111), ('Seadra', 117), ('Seaking', 119), ('Tentacruel', 73), ('Venomoth', 49), ('Venonat', 48), ('Weezing', 110))
LEGACY_SPECIES = (
    'ABRA', 'ARBOK', 'BEEDRILL', 'BELLSPROUT', 'BLASTOISE', 'BULBASAUR', 'BUTTERFREE', 'CATERPIE',
    'CHARIZARD', 'CHARMANDER', 'CHARMELEON', 'CLEFABLE', 'CLEFAIRY', 'DIGLETT', 'DROWZEE',
    'DUGTRIO', 'EKANS', 'FEAROW', 'GEODUDE', 'GLOOM', 'GOLBAT', 'GOLDEEN', 'GOLEM', 'GRAVELER',
    'GRIMER', 'GROWLITHE', 'GYARADOS', 'HORSEA', 'IVYSAUR', 'JIGGLYPUFF', 'KADABRA', 'KAKUNA',
    'KOFFING', 'MACHOP', 'MAGIKARP', 'MAGNEMITE', 'MAGNETON', 'MANKEY', 'MEOWTH', 'METAPOD',
    'NIDOKING', 'NIDOQUEEN', 'NIDORAN_F', 'NIDORAN_M', 'NIDORINA', 'NIDORINO', 'ODDISH', 'ONIX',
    'PARAS', 'PARASECT', 'PERSIAN', 'PIDGEOT', 'PIDGEOTTO', 'PIDGEY', 'PIKACHU', 'POLIWAG',
    'POLIWHIRL', 'PONYTA', 'RAICHU', 'RATICATE', 'RATTATA', 'SANDSHREW', 'SANDSLASH', 'SHELLDER',
    'SLOWPOKE', 'SPEAROW', 'SQUIRTLE', 'STARMIE', 'STARYU', 'STEELIX', 'TENTACOOL', 'VENUSAUR',
    'VOLTORB', 'VULPIX', 'WARTORTLE', 'WEEDLE', 'WEEPINBELL', 'WIGGLYTUFF', 'ZUBAT',
)


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/recipes/pokemon/rocktunnel-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


def supplement(lock, sources):
    return pokemon.supplement(lock, sources, profile=PROFILE, species=ART_SPECIES,
        weight_species=LEGACY_SPECIES + tuple(source_symbol(name) for name in NEW_SPECIES),
        natural_level_limit=None)


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Fourteen RockTunnel dependency species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs.update(supplement(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, inputCount=len(lock['files']), addedSpeciesCount=14, composedSpeciesCount=93, artSpeciesCount=14, textureCount=84, rgbaCount=84, outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    return import_region(raw, out, recipe=load_lock(lock_path), convert=convert,
                         repository=repository)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, help='Explicit raw cache rebuilds offline unless --reference-repo is also supplied')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/sprites/pokemon/rocktunnel')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/pokemon/rocktunnel-pokemon.lock.json')
    args = parser.parse_args()
    explicit_raw = args.raw is not None
    args.raw = args.raw or ROOT / 'content/raw/pokefirered/037335f4'
    if args.reference_repo is None and not explicit_raw:
        configured = os.environ.get('FIRERED_ORIGINAL_REFERENCE')
        if configured: args.reference_repo = pathlib.Path(configured)
        elif not args.raw.exists(): args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))

if __name__ == '__main__': main()
