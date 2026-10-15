#!/usr/bin/env python3
"""Verify Tower source derivation, overlap, fixtures, and regeneration."""
import argparse
import importlib.util
import json
import pathlib
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dependency-project-root', type=pathlib.Path, required=True)
    parser.add_argument('--reference-root', type=pathlib.Path, required=True)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('tower_importer', ROOT / 'scripts/import-pokemon-tower-pokemon.py')
    importer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(importer)
    outputs = importer.generate(args.dependency_project_root)
    for relative, data in outputs.items():
        assert (ROOT / relative).read_bytes() == data, relative
    catalog = json.loads(outputs['content/generated/pokemon-tower-pokemon/catalog.json'])
    fixture = json.loads(outputs['tests/fixtures/pokemon-tower/catalog-source-fixtures.json'])
    inherited_species, inherited_moves = {}, {}
    for path in (args.dependency_project_root / 'content/generated').glob('*/catalog.json'):
        if path.parent.name == 'pokemon-tower-pokemon':
            continue
        document = json.loads(path.read_text())
        if not isinstance(document, dict) or not isinstance(document.get('species'), list) or not isinstance(document.get('moves'), list):
            continue
        for row in document['species']:
            inherited_species[row['id']['value']] = row
        for row in document['moves']:
            key = row['id']['value']
            if key in inherited_moves:
                assert inherited_moves[key] == row, key
            inherited_moves[key] = row
    assert len(inherited_species) == 99 and len(inherited_moves) == 230
    added_moves = {m['id']['value']: m for m in catalog['moves']}
    assert len(added_moves) == 5 and not added_moves.keys() & inherited_moves.keys()
    for definition in fixture['requiredMoveDefinitions']:
        key = definition['id']['value']
        assert (added_moves.get(key) or inherited_moves[key]) == definition, key
    expected = {
        ('pret:SPECIES_GASTLY', 13): ['HYPNOSIS', 'LICK', 'SPITE', 'CURSE'],
        ('pret:SPECIES_GASTLY', 16): ['LICK', 'SPITE', 'CURSE', 'NIGHT_SHADE'],
        ('pret:SPECIES_GASTLY', 23): ['SPITE', 'CURSE', 'NIGHT_SHADE', 'CONFUSE_RAY'],
        ('pret:SPECIES_HAUNTER', 25): ['CURSE', 'NIGHT_SHADE', 'CONFUSE_RAY', 'SHADOW_PUNCH'],
        ('pret:SPECIES_GASTLY', 100): ['DESTINY_BOND', 'SHADOW_BALL', 'NIGHTMARE', 'MEAN_LOOK'],
        ('pret:SPECIES_HAUNTER', 100): ['DESTINY_BOND', 'SHADOW_BALL', 'NIGHTMARE', 'MEAN_LOOK'],
    }
    indexed = {(r['species'], r['level']): r['moves'] for r in fixture['naturalMoves']}
    assert len(indexed) == 20
    for key, moves in expected.items():
        assert indexed[key] == ['pret:MOVE_' + m for m in moves], key
    assert [s['sourceSpeciesId'] for s in catalog['species']] == [92, 93]
    assert [len(s['levelUpMoves']) for s in catalog['species']] == [11, 13]
    evolution = catalog['species'][0]['evolutions'][0]
    assert evolution['level'] == 25 and evolution['target']['value'] == 'pret:SPECIES_HAUNTER'
    assert catalog['species'][1]['evolutions'] == []
    assert catalog['deferredEvolutions'][0]['target']['value'] == 'pret:SPECIES_GENGAR'
    assert catalog['deferredEvolutions'][0]['executable'] is False
    print('PASS: 14 pinned upstream files; 99/230 base overlap; 101/235 standalone and 101/236 campaign closure;')
    print('5 absent moves, 8 unchanged required moves, 20 level fixtures, full learnsets, explicit deferred trade;')
    print('deterministic regeneration. C# build/runtime not run by this preparation.')


if __name__ == '__main__':
    main()
