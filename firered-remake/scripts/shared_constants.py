"""Store source-imported numeric symbols once, alongside generated packages.

Importers may retain detailed source records while resolving expressions. Only
actual integer values are published, and conflicting definitions fail explicitly.
The runtime has no package-local constants representation or fallback.
"""
import json
from pathlib import Path


def numeric_values(document):
    """Project an importer's resolved source inventory to actual runtime values."""
    records = document.get('values', document)
    result = {}
    for name, record in records.items():
        value = record['value'] if isinstance(record, dict) else record
        if not isinstance(name, str) or not name.strip() or type(value) is not int or not -(1 << 31) <= value < (1 << 31):
            raise ValueError('Invalid source constant: ' + str(name))
        result[name] = value
    if not result:
        raise ValueError('Source constant table is empty')
    return result


def read_values(generated_directory):
    """Read the sole generated runtime representation, without legacy fallback."""
    path = Path(generated_directory) / 'shared-constants/values.json'
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Duplicate shared constant: ' + key)
            result[key] = value
        return result
    result = json.loads(path.read_text(), object_pairs_hook=unique)
    if not isinstance(result, dict) or not result or any(not name.strip() or type(value) is not int or not -(1 << 31) <= value < (1 << 31) for name, value in result.items()):
        raise ValueError('Shared constants must be a nonempty numeric symbol table')
    return result


def merged_values(generated_directory, document):
    """Merge one source contribution, rejecting a changed existing definition."""
    path = Path(generated_directory) / 'shared-constants/values.json'
    result = read_values(generated_directory) if path.is_file() else {}
    for name, value in numeric_values(document).items():
        if name in result and result[name] != value:
            raise ValueError(f'Conflicting shared constant {name}: {result[name]} != {value}; use an explicit semantic scope')
        result[name] = value
    return result


def encode_values(values):
    return (json.dumps(values, sort_keys=True, separators=(',', ':')) + '\n').encode()


def publish_constants(generated_directory, document, *, check=False):
    """Publish to the generated root; check mode verifies this contribution only."""
    generated = Path(generated_directory)
    path = generated / 'shared-constants/values.json'
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError('Symlink in shared constants output: ' + str(path))
    if check:
        shared = read_values(generated)
        for name, value in numeric_values(document).items():
            if shared.get(name) != value:
                raise ValueError('Stale shared constant: ' + name)
        return
    data = encode_values(merged_values(generated, document))
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.is_file() or path.read_bytes() != data:
        temporary = path.with_suffix('.tmp')
        temporary.write_bytes(data)
        temporary.replace(path)


def package_documents(generated_directory, constants, documents, *, check=False):
    """Publish source constants globally and return only package-owned data."""
    documents = dict(documents)
    # Geometry inventories are import-time proofs. Runtime maps already own the
    # decoded cells, collision and tileset data; never publish the duplicate report.
    documents.pop('geometry.json', None)
    # The story importer owns executable IR. Per-region transcript indexes are
    # only working values for source closure and must not enter runtime output.
    documents.pop('scripts.json', None)
    publish_constants(generated_directory, constants, check=check)
    if 'movementBindings' in constants:
        documents['movement-bindings.json'] = numeric_values(constants['movementBindings'])
    return documents
