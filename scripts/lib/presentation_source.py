"""Read the actual presentation input closure, without stage inventory snapshots.

Raw mode trusts the selected pinned cache; it does not claim Git verification.
Reference mode reads fixed-commit blobs and never executes reference scripts.
"""
import json
import subprocess
from project_paths import PROJECT_ROOT
from pathlib import Path

SOURCE_COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"


def need(condition, message):
    if not condition:
        raise ValueError(message)


def safe(root, relative):
    if not relative or '\\' in relative or ':' in relative or any(p in ('', '.', '..') for p in relative.split('/')) or Path(relative).is_absolute():
        raise ValueError('Unsafe presentation path: ' + relative)
    path = Path(root) / relative
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError('Presentation symlink rejected: ' + str(path))
    return path


class Source:
    def __init__(self, root, reference=None, dependency=None):
        self.root, self.reference = root, reference
        self.dependency = dependency or root
        config = json.loads((PROJECT_ROOT / 'scripts/recipes/config/source.json').read_bytes())
        source = config['source']
        if (source['repository'], source['sourceCommit']) != ('pret/pokefirered', SOURCE_COMMIT) or config['referenceGame'] != dict(gameEdition='FIRERED', romRevision=0):
            raise ValueError('Unsupported presentation source identity')
        self.commit = source['sourceCommit']
        self.raw_prefix = 'content/raw/pokefirered/' + self.commit[:8] + '/'
        self.raw, self.inherited = {}, {}
        self.data = self.raw

    def read(self, path):
        if path not in self.raw:
            target = safe(self.root, self.raw_prefix + path)
            self.raw[path] = subprocess.check_output(
                ['git', '-C', str(self.reference), 'show', self.commit + ':' + path]
            ) if self.reference else target.read_bytes()
        return self.raw[path]

    def text(self, path):
        return self.read(path).decode()

    def ref(self, path, symbol):
        self.read(path)
        return dict(path=path, symbol=symbol)

    def reuse(self, path):
        data = safe(self.dependency, path).read_bytes()
        self.inherited[path] = dict(path=path)
        return data
