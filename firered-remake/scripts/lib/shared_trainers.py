"""Publish ordinary trainer definitions once, with ordered regional ID references.

Importers can retain source locations while parsing. This explicit projection
removes only definition provenance; executable battle source bindings and all
other document fields remain owned by their importer.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import copy
import json
from pathlib import Path

from runtime_json import runtime_json_bytes

RESOURCE = 'data/trainers.json'


def runtime_trainer(definition):
    """Copy one parsed definition without its four pure provenance fields."""
    trainer = copy.deepcopy(definition)
    if not isinstance(trainer, dict) or not isinstance(trainer.get('id'), dict):
        raise ValueError('Expected a full trainer definition')
    identity = trainer['id'].get('value')
    if not isinstance(identity, str) or not identity.startswith('pret:TRAINER_'):
        raise ValueError('Invalid trainer identity: ' + str(identity))
    if type(trainer.get('sourceTrainerId')) is not int or not trainer.get('party'):
        raise ValueError('Invalid trainer definition: ' + identity)
    trainer.pop('source', None)
    trainer.pop('moneySource', None)
    for member in trainer['party']:
        member.pop('source', None)
        member.pop('speciesNameSource', None)
    return trainer


def _index(definitions):
    result, numeric = {}, {}
    for definition in definitions:
        identity = definition['id']['value']
        if identity in result:
            raise ValueError('Duplicate shared trainer: ' + identity)
        source_id = definition['sourceTrainerId']
        if source_id in numeric:
            raise ValueError('Duplicate source trainer ID: ' + str(source_id))
        result[identity] = definition
        numeric[source_id] = identity
    if not result:
        raise ValueError('Shared trainer table is empty')
    return result


def read_trainers(generated_directory):
    """Read the sole runtime definition table, without an inline fallback."""
    path = Path(generated_directory) / RESOURCE
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Duplicate shared trainer property: ' + key)
            result[key] = value
        return result
    document = json.loads(path.read_text(), object_pairs_hook=unique)
    if not isinstance(document, dict) or set(document) != {'trainers'} or not isinstance(document['trainers'], list):
        raise ValueError('Expected shared trainer definitions')
    definitions = document['trainers']
    for row in definitions:
        if runtime_trainer(row) != row:
            raise ValueError('Shared trainer contains import provenance')
    return _index(definitions)


def publish_trainers(generated_directory, definitions, *, check=False):
    """Merge a parsed contribution, rejecting conflicting business definitions."""
    generated = Path(generated_directory)
    path = generated / RESOURCE
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError('Symlink in shared trainer output: ' + str(path))
    contribution = _index([runtime_trainer(row) for row in definitions])
    shared = read_trainers(generated) if path.is_file() else {}
    for identity, definition in contribution.items():
        if identity in shared and shared[identity] != definition:
            raise ValueError('Conflicting shared trainer business data: ' + identity)
        if check and identity not in shared:
            raise ValueError('Missing shared trainer: ' + identity)
    if check:
        return
    shared.update(contribution)
    _index(list(shared.values()))
    data = runtime_json_bytes({'trainers': sorted(shared.values(), key=lambda t: (t['sourceTrainerId'], t['id']['value']))}, sort_keys=True)
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.is_file() or path.read_bytes() != data:
        temporary = path.with_suffix('.tmp')
        temporary.write_bytes(data)
        temporary.replace(path)


def trainer_document(generated_directory, document, *, check=False):
    """Publish parsed definitions and return this package's explicit ID array.

    These named trainer-document fields were import proofs. Battle bindings and
    any other package data are preserved; this is not a generic JSON sanitizer.
    """
    definitions = document['trainers']
    publish_trainers(generated_directory, definitions, check=check)
    result = dict(document, trainers=[row['id']['value'] for row in definitions])
    for field in ('sourceCommit', 'profile', 'completeSourceRoster'):
        result.pop(field, None)
    result.pop('resolvedMoves', None)
    return result


def resolve_trainers(generated_directory, document):
    """Resolve a package's ordered IDs for offline presentation/import tools."""
    references = document['trainers']
    if not isinstance(references, list) or not references or any(not isinstance(value, str) for value in references):
        raise ValueError('Trainer references must be a nonempty string array')
    if len(set(references)) != len(references):
        raise ValueError('Duplicate regional trainer reference')
    shared = read_trainers(generated_directory)
    try:
        return [shared[identity] for identity in references]
    except KeyError as error:
        raise ValueError('Unresolved shared trainer: ' + str(error.args[0])) from error


def trainer_json(generated_directory, document, *, check=False):
    """Encode an explicitly selected trainer document in the shared format."""
    return runtime_json_bytes(trainer_document(generated_directory, document,
        check=check), sort_keys=True)


def party_moves(generated_directory, definition):
    """Resolve a team's actual custom slots or canonical level-up moves offline.

    Custom MOVE_NONE slots remain intact. Callers rendering executable move slots
    can explicitly omit them, just as the runtime party factory does.
    """
    document = json.loads((Path(generated_directory) / 'data/pokemon/canonical/catalog.json').read_text())
    species = {row['id']['value']: row for row in document['species']}
    result = []
    for member in definition['party']:
        if definition['partyFlags'] & 1:
            result.append([row['value'] for row in member['customMoves']])
            continue
        moves = []
        for row in species[member['species']['value']]['levelUpMoves']:
            move = row['move']['value']
            if row['level'] <= member['level'] and move not in moves:
                moves = (moves + [move])[-4:]
        if not moves:
            raise ValueError('Trainer species has no initial moves: ' + member['species']['value'])
        result.append(moves)
    return result
