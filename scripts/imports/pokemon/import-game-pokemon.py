#!/usr/bin/env python3
"""Compile the current game's explicit Pokemon admissions against the canonical business catalog."""
from __future__ import annotations
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import re
from pathlib import Path

ROOT = _PROJECT_ROOT
METHODS = {
    'level': 'EVO_LEVEL', 'item': 'EVO_ITEM', 'trade': 'EVO_TRADE',
    'tradeItem': 'EVO_TRADE_ITEM', 'friendship': 'EVO_FRIENDSHIP',
    'levelAttackGreaterDefense': 'EVO_LEVEL_ATK_GT_DEF',
    'levelAttackEqualDefense': 'EVO_LEVEL_ATK_EQ_DEF',
    'levelAttackLessDefense': 'EVO_LEVEL_ATK_LT_DEF',
}


def compile_admissions(policy: dict, canonical: dict) -> dict:
    rosters = ('owned', 'opponents', 'trainerTowerOpponents')
    if set(policy) != {'schemaVersion', *rosters} or policy['schemaVersion'] != 1:
        raise ValueError('game_pokemon_schema')
    species = {row['id']['value']: row for row in canonical['species']}
    metadata = {row['id']['value']: row for row in canonical['metadata']}
    forms = {(row['species']['value'], row['scope']) for row in canonical['generationContract']['formStatOverrides']}
    sets = {}
    for roster in rosters:
        rows = policy[roster]
        ids = [row['id']['value'] for row in rows]
        if not ids or len(ids) != len(set(ids)) or not set(ids) <= species.keys():
            raise ValueError('game_pokemon_admissions')
        sets[roster] = set(ids)
        for row in rows:
            if set(row) - {'id', 'evolutions', 'formScope'} or set(row['id']) != {'value'}:
                raise ValueError('game_pokemon_reference')
            id = row['id']['value']
            if 'formScope' in row and (id, row['formScope']) not in forms:
                raise ValueError('game_pokemon_form')
            edges = set()
            for edge in row['evolutions']:
                method = edge.get('method', 'level')
                target = edge['target']['value']
                level, parameter = edge['level'], edge.get('parameter')
                if set(edge) - {'level', 'target', 'method', 'parameter'} or target not in sets[roster]:
                    raise ValueError('game_pokemon_evolution')
                signature = (method, target, level, parameter)
                if signature in edges or method not in METHODS:
                    raise ValueError('game_pokemon_evolution')
                edges.add(signature)
                expected_parameter = str(level) if method.startswith('level') else parameter or '0'
                if not any(original['method'] == METHODS[method] and original['target']['value'] == target
                           and original['parameter'] == expected_parameter
                           for original in metadata[id]['sourceEvolutions']):
                    raise ValueError('game_pokemon_evolution')
    if not sets['owned'] <= sets['opponents']:
        raise ValueError('game_pokemon_owned_opponents')
    return policy


def compile_breeding(root: Path, policy: dict, canonical: dict) -> dict:
    raw = root / 'content/raw/pokefirered/037335f4'
    egg_text = (raw / 'src/data/pokemon/egg_moves.h').read_text()
    egg_moves = {name: re.findall(r'MOVE_\w+', body) for name, body in
                 re.findall(r'egg_moves\(([A-Z0-9_]+),\s*(.*?)\)', egg_text, re.S)}
    table = (raw / 'src/data/party_menu.h').read_text()
    body = re.search(r'static const u16 sTMHMMoves\[\]\s*=\s*\{(.*?)\};', table, re.S)[1]
    all_moves = re.findall(r'MOVE_\w+', body)
    if len(all_moves) != 58 or len(set(all_moves)) != 58:
        raise ValueError('breeding_machine_order')
    metadata = {row['id']['value']: row for row in canonical['metadata']}
    definitions = sorted(canonical['species'], key=lambda row: row['sourceSpeciesId'])
    def base_species(species):
        visited = set()
        while species not in visited:
            visited.add(species)
            parent = next((row['id']['value'] for row in definitions if any(
                edge['target']['value'] == species for edge in metadata[row['id']['value']]['sourceEvolutions'])), None)
            if parent is None:
                return species
            species = parent
        raise ValueError('breeding_evolution_cycle')
    return {'species': [{'species': row['id'], 'baseEggSpecies': {'value': base_species(row['id']['value'])},
                         'eggMoves': [{'value': 'pret:' + move} for move in egg_moves.get(row['id']['value'][13:], [])]}
                        for row in policy['owned']],
            'allMachineMoves': [{'value': 'pret:' + move} for move in all_moves]}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    args = parser.parse_args()
    root = args.root
    policy = json.loads((root / 'data/game-pokemon.json').read_text())
    canonical = json.loads((root / 'content/generated/data/pokemon/canonical/catalog.json').read_text())
    output = compile_admissions(policy, canonical)
    path = root / 'content/generated/data/pokemon/game/catalog.json'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(output, indent=2) + '\n')
    breeding = compile_breeding(root, output, canonical)
    (path.parent / 'breeding.json').write_text(json.dumps(breeding, indent=2) + '\n')
    print('Pokemon admissions: ' + ', '.join(f'{key}={len(output[key])}' for key in ('owned', 'opponents', 'trainerTowerOpponents')))


if __name__ == '__main__':
    main()
