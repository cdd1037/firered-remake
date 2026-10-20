"""Resolve explicit roster references without copying shared admission definitions."""
from copy import deepcopy

ROSTERS = ('owned', 'opponents', 'trainerTowerOpponents')


def _identity(value):
    if not isinstance(value, dict) or set(value) != {'value'} or \
            not isinstance(value['value'], str) or not value['value'].startswith('pret:SPECIES_'):
        raise ValueError('game_pokemon_reference')
    return value['value']


def resolve_admissions(policy):
    """Return independent, ordered roster projections for import consumers.

    Only evolution edges can differ between rosters. An override must name an
    admitted species and explicitly differ from its shared definition.
    Canonical species/form/evolution validity belongs to the game compiler.
    """
    if not isinstance(policy, dict) or set(policy) != {'schemaVersion', 'definitions', *ROSTERS} or \
            type(policy['schemaVersion']) is not int or policy['schemaVersion'] != 2:
        raise ValueError('game_pokemon_schema')
    rows = policy['definitions']
    if not isinstance(rows, list) or not rows:
        raise ValueError('game_pokemon_definitions')
    definitions = {}
    for row in rows:
        if not isinstance(row, dict) or not {'id', 'evolutions'} <= row.keys() or \
                set(row) - {'id', 'evolutions', 'formScope'} or not isinstance(row['evolutions'], list) or \
                ('formScope' in row and (not isinstance(row['formScope'], str) or not row['formScope'])):
            raise ValueError('game_pokemon_definition')
        identity = _identity(row['id'])
        if identity in definitions:
            raise ValueError('game_pokemon_duplicate_definition')
        definitions[identity] = row
    resolved = {}
    used = set()
    for name in ROSTERS:
        roster = policy[name]
        if not isinstance(roster, dict) or set(roster) != {'species', 'evolutionOverrides'}:
            raise ValueError('game_pokemon_roster')
        ids, overrides = roster['species'], roster['evolutionOverrides']
        if not isinstance(ids, list) or not ids or any(not isinstance(id, str) for id in ids) or \
                len(ids) != len(set(ids)) or not set(ids) <= definitions.keys():
            raise ValueError('game_pokemon_admissions')
        if not isinstance(overrides, list):
            raise ValueError('game_pokemon_overrides')
        edges = {}
        for row in overrides:
            if not isinstance(row, dict) or set(row) != {'id', 'evolutions'} or \
                    not isinstance(row['evolutions'], list):
                raise ValueError('game_pokemon_override')
            identity = _identity(row['id'])
            if identity not in ids or identity in edges or row['evolutions'] == definitions[identity]['evolutions']:
                raise ValueError('game_pokemon_override')
            edges[identity] = row['evolutions']
        resolved[name] = [deepcopy(dict(definitions[id], evolutions=edges.get(id, definitions[id]['evolutions'])))
                          for id in ids]
        used.update(ids)
    if used != definitions.keys():
        raise ValueError('game_pokemon_unused_definition')
    if not set(policy['owned']['species']) <= set(policy['opponents']['species']):
        raise ValueError('game_pokemon_owned_opponents')
    return resolved
