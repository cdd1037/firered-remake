#!/usr/bin/env python3
"""Compile the current game's explicit Pokemon admissions against the canonical business catalog."""
from __future__ import annotations
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from game_pokemon_admissions import ROSTERS, resolve_admissions
from runtime_json import runtime_json_bytes

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
    resolved = resolve_admissions(policy)
    species = {row['id']['value']: row for row in canonical['species']}
    metadata = {row['id']['value']: row for row in canonical['metadata']}
    forms = {(row['species']['value'], row['scope']) for row in canonical['generationContract']['formStatOverrides']}
    def validate(row):
        id = row['id']['value']
        if id not in species:
            raise ValueError('game_pokemon_admissions')
        if 'formScope' in row and (id, row['formScope']) not in forms:
            raise ValueError('game_pokemon_form')
        edges = set()
        for edge in row['evolutions']:
            if not isinstance(edge, dict) or not {'level', 'target'} <= edge.keys() or \
                    set(edge) - {'level', 'target', 'method', 'parameter'} or \
                    not isinstance(edge['target'], dict) or set(edge['target']) != {'value'} or \
                    not isinstance(edge['target']['value'], str) or \
                    type(edge['level']) is not int or not isinstance(edge.get('method', 'level'), str) or \
                    (edge.get('parameter') is not None and not isinstance(edge['parameter'], str)):
                raise ValueError('game_pokemon_evolution')
            method = edge.get('method', 'level')
            target = edge['target']['value']
            level, parameter = edge['level'], edge.get('parameter')
            signature = (method, target, level, parameter)
            if signature in edges or method not in METHODS:
                raise ValueError('game_pokemon_evolution')
            edges.add(signature)
            expected_parameter = str(level) if method.startswith('level') else parameter or '0'
            if not any(original['method'] == METHODS[method] and original['target']['value'] == target
                       and original['parameter'] == expected_parameter
                       for original in metadata[id]['sourceEvolutions']):
                raise ValueError('game_pokemon_evolution')

    for row in policy['definitions']:
        validate(row)
    for roster in ROSTERS:
        rows = resolved[roster]
        ids = {row['id']['value'] for row in rows}
        for row in rows:
            validate(row)
            if any(edge['target']['value'] not in ids for edge in row['evolutions']):
                raise ValueError('game_pokemon_evolution')
    return policy


def compile_breeding(root: Path, policy: dict, canonical: dict) -> dict:
    owned = resolve_admissions(policy)['owned']
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
                        for row in owned],
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
    path.write_bytes(runtime_json_bytes(output))
    breeding = compile_breeding(root, output, canonical)
    (path.parent / 'breeding.json').write_text(json.dumps(breeding, indent=2) + '\n')
    print('Pokemon admissions: ' + ', '.join(f'{key}={len(output[key]["species"])}' for key in ROSTERS))


if __name__ == '__main__':
    main()
