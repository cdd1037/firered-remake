#!/usr/bin/env python3
"""Offline MtMoon Pokemon content; reconstructed source after workspace loss.
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
PROFILE = 'firered-mtmoon-pokemon-v1'
NEW_SPECIES = ('Arbok', 'Clefairy', 'Zubat', 'Golbat', 'Oddish', 'Paras', 'Parasect', 'Bellsprout', 'Magnemite', 'Grimer', 'Voltorb', 'Koffing', 'Nidoqueen', 'Nidoking', 'Clefable', 'Wigglytuff')
ART_SPECIES = tuple(zip(NEW_SPECIES, (24, 35, 41, 42, 43, 46, 47, 69, 81, 88, 100, 109, 31, 34, 36, 40)))
LEGACY_SPECIES = (
    'BULBASAUR', 'IVYSAUR', 'VENUSAUR', 'CHARMANDER', 'CHARMELEON', 'CHARIZARD', 'SQUIRTLE',
    'WARTORTLE', 'BLASTOISE', 'PIDGEY', 'PIDGEOTTO', 'PIDGEOT', 'RATTATA', 'RATICATE', 'WEEDLE',
    'KAKUNA', 'BEEDRILL', 'CATERPIE', 'METAPOD', 'BUTTERFREE', 'PIKACHU', 'RAICHU', 'GEODUDE',
    'GRAVELER', 'GOLEM', 'SANDSHREW', 'SANDSLASH', 'ONIX', 'STEELIX', 'SPEAROW', 'FEAROW', 'MANKEY',
    'EKANS', 'NIDORAN_F', 'NIDORINA', 'NIDORAN_M', 'NIDORINO', 'JIGGLYPUFF', 'MAGIKARP', 'GYARADOS',
)


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/recipes/pokemon/mtmoon-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


REQUIRED_NEW_MOVES = ('ABSORB', 'AERIAL_ACE', 'ASTONISH', 'BODY_SLAM', 'CHARGE', 'DOUBLE_SLAP', 'DRAGON_RAGE',
    'ENCORE', 'FOLLOW_ME', 'GROWTH', 'HELPING_HAND', 'LEECH_LIFE', 'MAGNITUDE', 'METAL_SOUND',
    'MINIMIZE', 'MUD_SPORT', 'POISON_GAS', 'RAGE', 'RAPID_SPIN', 'ROCK_THROW', 'SCREECH',
    'SELF_DESTRUCT', 'SMOG', 'SPORE', 'SWEET_SCENT', 'THRASH', 'WING_ATTACK')
NATURAL_MIN_LEVEL = dict(BULBASAUR=1, IVYSAUR=16, CHARMANDER=1, CHARMELEON=16, SQUIRTLE=1,
    WARTORTLE=16, CATERPIE=1, METAPOD=7, BUTTERFREE=10, WEEDLE=1, KAKUNA=7, BEEDRILL=10,
    PIDGEY=1, PIDGEOTTO=18, RATTATA=1, RATICATE=20, PIKACHU=1, SPEAROW=1, FEAROW=20,
    MANKEY=1, NIDORAN_F=1, NIDORINA=16, NIDORAN_M=1, NIDORINO=16, JIGGLYPUFF=1,
    MAGIKARP=1, GYARADOS=20, EKANS=1, ARBOK=22, GEODUDE=1, GRAVELER=25, CLEFAIRY=1,
    ZUBAT=1, GOLBAT=22, PARAS=1, PARASECT=24, NIDOQUEEN=16, NIDOKING=16, CLEFABLE=8, WIGGLYTUFF=3)


def supplement(lock, sources):
    return pokemon.supplement(lock, sources, profile=PROFILE, species=ART_SPECIES,
        weight_species=LEGACY_SPECIES + tuple(source_symbol(name) for name in NEW_SPECIES),
        natural_level_limit=25)


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Sixteen MtMoon dependency species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs.update(supplement(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, inputCount=len(lock['files']), addedSpeciesCount=16, composedSpeciesCount=56, artSpeciesCount=16, textureCount=96, rgbaCount=96, outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    return import_region(raw, out, recipe=load_lock(lock_path), convert=convert,
                         repository=repository)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/sprites/pokemon/mtmoon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/pokemon/mtmoon-pokemon.lock.json')
    args = parser.parse_args()
    if args.reference_repo is None and not args.raw.exists(): args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))

if __name__ == '__main__': main()
