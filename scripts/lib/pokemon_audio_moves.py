"""Resolve an audio import's bounded move repertoire from current definitions.

Regional presentation imports do not publish gameplay catalogs. This in-memory
projection combines their species selection with the actual trainer move slots.
It does not change which species or moves the game admits.
"""
import json
from pathlib import Path
from shared_trainers import resolve_trainers, party_moves


def repertoire(root, species, trainer_document, teaching_moves=()):
    generated = Path(root) / 'content/generated'
    catalog = json.loads((generated / 'data/pokemon/canonical/catalog.json').read_text())
    definitions = {row['id']['value']: row for row in catalog['species']}
    selected = [definitions['pret:SPECIES_' + name] for name in species]
    moves = {'pret:' + move for move in teaching_moves}
    moves.update(entry['move']['value'] for row in selected for entry in row['levelUpMoves'])
    trainers = json.loads((generated / trainer_document).read_text())
    moves.update(move for trainer in resolve_trainers(generated, trainers)
                 for slots in party_moves(generated, trainer) for move in slots
                 if move != 'pret:MOVE_NONE')
    return selected, sorted(moves)
