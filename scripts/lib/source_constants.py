"""Numeric constants used by source-derived gameplay feature converters."""
import ast
import re


def constants_inventory(src, worlds=(), scripts=None, *, extra_names=()):
    """Resolve numeric source constants and local object IDs without executing C."""
    scripts = scripts or {"blocks": []}
    for path in sorted(getattr(src, "tree", src.data)):
        if path.startswith("include/constants/") and path.endswith(".h"):
            src.text(path)
    values = {}
    for path in sorted(src.data):
        if not path.startswith('include/constants/'):
            continue
        for match in re.finditer(r'^#define\s+([A-Za-z_][A-Za-z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$', src.text(path), re.M):
            name, token = match.groups()
            value = int(token, 16 if token.startswith('0x') else 10)
            if name in values and values[name]['value'] != value:
                raise ValueError('Conflicting source constant: ' + name)
            values[name] = dict(value=value, sourceRef=src.ref(path, name, src.text(path)[:match.start()].count('\n') + 1))
    # Resolve only the arithmetic needed by regional flags and trainer AI. No eval,
    # C preprocessing, functions, or upstream execution is permitted.
    expressions = {}
    for path in sorted(src.data):
        if path.startswith('include/constants/'):
            for match in re.finditer(r'^#define[ \t]+([A-Za-z_][A-Za-z0-9_]+)[ \t]+([^\n]+)', src.text(path), re.M):
                token = match[2].split('//')[0].strip()
                expressions[match[1]] = (token, path, src.text(path)[:match.start()].count('\n') + 1)
    wanted = {'AI_SCRIPT_CHECK_BAD_MOVE', 'AI_SCRIPT_TRY_TO_FAINT', 'AI_SCRIPT_CHECK_VIABILITY', *extra_names}
    for world in worlds:
        for kind in ('object_events','coord_events','bg_events'):
            for event in world['events'][kind]:
                if event.get('flag','').startswith('FLAG_'):
                    wanted.add(event['flag'])
    for block in scripts['blocks']:
        for row in block['lines']:
            wanted.update(re.findall(r'\bFLAG_[A-Za-z0-9_]+\b', row['text']))
    pending = set()
    def resolve(name):
        if name in values:
            return values[name]['value']
        if name in pending or name not in expressions:
            raise ValueError('Missing/cyclic regional constant: ' + name)
        pending.add(name)
        expression, path, line = expressions[name]
        def number(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name):
                return resolve(node.id)
            if isinstance(node, ast.BinOp):
                left, right = number(node.left), number(node.right)
                if isinstance(node.op, ast.Add): return left + right
                if isinstance(node.op, ast.Sub): return left - right
                if isinstance(node.op, ast.LShift) and 0 <= right < 32: return left << right
            raise ValueError('Unsupported regional constant expression: ' + expression)
        value = number(ast.parse(expression, mode='eval').body)
        if not 0 <= value <= 0x7fffffff:
            raise ValueError('Regional constant outside supported range')
        values[name] = dict(value=value, sourceRef=src.ref(path,name,line))
        pending.remove(name)
        return value
    for name in sorted(wanted):
        resolve(name)
    generator = src.text('tools/mapjson/mapjson.cpp')
    if 'json_to_string(obj_event, "local_id") << " " << i + 1' not in generator:
        raise ValueError('Unsupported local object ID generation')
    for path in sorted(src.data):
        if path.startswith('data/maps/') and path.endswith('/map.json'):
            for i, obj in enumerate(src.json(path)['object_events'], 1):
                if 'local_id' in obj:
                    values[obj['local_id']] = dict(value=i, sourceRef=src.ref(path, obj['local_id']))
    return dict(schemaVersion=1, values=values)


def world_constants(src):
    """Collect all map-local IDs and numeric symbols used by the raw world."""
    worlds = [dict(events=src.json(path)) for path in sorted(src.tree)
              if path.startswith('data/maps/') and path.endswith('/map.json')]
    scripts = {'blocks': [{'lines': [{'text': src.text(path)}]} for path in sorted(src.tree)
                          if path.endswith(('.inc', '.s')) and path.startswith(('data/maps/', 'data/scripts/'))]}
    values = constants_inventory(src, worlds, scripts)['values']
    for i, row in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
        values[row['id']] = dict(value=i, sourceRef=src.ref('src/data/heal_locations.json', row['id']))
    for i, row in enumerate(src.json('data/layouts/layouts.json')['layouts'], 1):
        if 'id' in row: values[row['id']] = dict(value=i, sourceRef=src.ref('data/layouts/layouts.json', row['id']))
    return dict(schemaVersion=1, values=values)
