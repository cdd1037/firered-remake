"""Transient raw inputs shared by small gameplay feature compilers.

No generated artifacts, renderers, source inventories or region CLI dependencies.
"""
import re
from map_scripts import parse_scripts
from map_source import decode_geometry, tileset_bindings, source_topology
from source_constants import constants_inventory
from field_item_services import implicit_script_targets


def script_blocks(src, names, extra=()):
    for path in sorted(src.tree):
        if path.endswith(('.inc', '.s')) and path.startswith(('data/maps/', 'data/scripts/', 'data/text/')) or path == 'data/event_scripts.s':
            src.read(path, 'script-label-closure')
    blocks, aliases, _, _ = parse_scripts(src, alias_directives=('equ', 'set'))
    paths = {'data/maps/' + n + '/scripts.inc' for n in names}
    pending = [k for k, b in blocks.items() if b['sourceRef']['path'] in paths] + list(extra)
    for name in names:
        events = src.json('data/maps/' + name + '/map.json')
        pending.extend(e['script'] for kind in ('object_events','coord_events','bg_events')
                       for e in events[kind] if e.get('script') not in (None,'0','0x0','NULL'))
    selected = set()
    while pending:
        token = pending.pop()
        label = aliases.get(token, token)
        if not label: raise ValueError('Empty script label')
        if label in selected: continue
        if label not in blocks: raise ValueError('Missing feature script: ' + label)
        selected.add(label)
        for row in blocks[label]['lines']:
            code = row['text'].split('@')[0].strip()
            if code.startswith(('.string', '#')): continue
            for token in re.findall(r'\b[A-Za-z_]\w*\b', code) + implicit_script_targets(code):
                token = aliases.get(token, token)
                if token in blocks and token not in selected: pending.append(token)
    return dict(blocks=[blocks[k] for k in sorted(selected)], aliases=aliases)


def feature_constants(src, worlds, scripts):
    result = constants_inventory(src, worlds, scripts)
    values = result['values']
    path = 'include/constants/metatile_labels.h'
    for match in re.finditer(r'^#define\s+(METATILE_\w+)\s+(0x[0-9a-fA-F]+)\s*$', src.text(path), re.M):
        values[match[1]] = dict(value=int(match[2], 16), sourceRef=src.ref(path, match[1]))
    for path,key in [('src/data/heal_locations.json','heal_locations'), ('data/layouts/layouts.json','layouts')]:
        for i,row in enumerate(src.json(path)[key],1):
            if 'id' in row: values[row['id']] = dict(value=i,sourceRef=src.ref(path,row['id']))
    return result


def feature_worlds(src, names):
    tilesets = tileset_bindings(src)
    return [decode_geometry(src, name, tilesets=tilesets) for name in names]


def topology(src, worlds):
    return source_topology(src, worlds)
