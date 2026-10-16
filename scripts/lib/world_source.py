"""Safe, lazy access to the selected pinned source tree; never generated inputs."""
import json
import struct
from pathlib import Path
from map_source import require_safe_target

class SourceFiles(dict):
    def __init__(self, root):
        self.root = Path(root)
        require_safe_target(self.root, self.root)
    def __missing__(self, name):
        target = self.root / name
        require_safe_target(self.root, target)
        value = target.read_bytes()
        self[name] = value
        return value

class WorldSource:
    def __init__(self, root):
        self.data = SourceFiles(root)
        self.entries = {}
        self.tree = {p.relative_to(root).as_posix(): None for p in Path(root).rglob("*") if p.is_file()}
    def exists(self, name):
        target = self.data.root / name
        require_safe_target(self.data.root, target)
        return target.is_file()
    def text(self, name): return self.data[name].decode('utf-8')
    def json(self, name): return json.loads(self.data[name])
    def units(self, name, code):
        data = self.data[name]
        if len(data) % struct.calcsize(code): raise ValueError('Misaligned source binary: ' + name)
        return [value[0] for value in struct.iter_unpack('<' + code, data)]
    def read(self, name, role=None):
        data = self.data[name]
        entry = self.entries.setdefault(name, dict(path=name, roles=[]))
        if role and role not in entry['roles']: entry['roles'].append(role)
        return data
    def peek(self, name): return self.data[name]
    def ref(self, path, symbol, line=None):
        self.data[path]
        result = dict(kind='pret', repository='pret/pokefirered',
                      commit='037335f4c725d7c9aecdac87066f2002b4bd7e14', path=path, symbol=symbol)
        if line is not None: result['line'] = line
        return result
