"""Explicit current map choices and exceptional identities, expanded for converters."""
import json
import re
from pathlib import Path
from map_source import source_directory


def load_selection(path):
    selection = json.loads(Path(path).read_bytes())
    result = []
    overrides = selection.get('overrides', {})
    for group, choices in selection['groups'].items():
        if not re.fullmatch(r'[a-z0-9-]+', group):
            raise ValueError('Unsafe world output group')
        for choice in choices:
            choice = {'sourceName': choice} if isinstance(choice, str) else dict(choice)
            name = choice['sourceName']
            if not isinstance(name, str) or not re.fullmatch(r'[A-Za-z0-9_]+', name):
                raise ValueError('Unsafe source map name')
            row = dict(overrides.get(name, {}), **choice)
            row.setdefault('stableId', 'map.' + source_directory(name))
            filename = row.pop('filename', row['stableId'].removeprefix('map.') + '.json')
            if Path(filename).name != filename or '\\' in filename or ':' in filename:
                raise ValueError('Unsafe world map filename')
            row.setdefault('stableId', 'map.' + source_directory(name))
            row['path'] = 'maps/regions/' + group + '/' + filename
            result.append(row)
    if overrides.keys() - {row['sourceName'] for row in result}:
        raise ValueError('Override has no selected source map')
    return result
