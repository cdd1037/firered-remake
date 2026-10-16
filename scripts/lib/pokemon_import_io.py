"""Local inputs and safe output materialization for Pokemon conversion tools."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n').encode('utf-8')


def safe(root, relative=None):
    root = Path(root).absolute()
    target = root
    if relative is not None:
        path = PurePosixPath(relative)
        if not relative or relative == '.' or path.is_absolute() or '..' in path.parts or '\\' in relative or ':' in relative or str(path) != relative:
            raise ValueError('Unsafe input/output path: ' + relative)
        target = root / relative
    if any(path.is_symlink() for path in (target, *target.parents)):
        raise ValueError('Symlink input/output or ancestor rejected: ' + str(target))
    return target


def preflight_directory(path):
    for directory in (path, *path.parents):
        if directory.exists() and not directory.is_dir():
            raise ValueError('Input/output ancestor is not a directory: ' + str(directory))


def read_source(raw, path, *, commit, repository=None):
    """Read ordinary raw files or an explicitly selected fixed Git revision."""
    target = safe(raw, path)
    if repository is None:
        return target.read_bytes()
    if not re.fullmatch(r'[0-9a-f]{40}', commit):
        raise ValueError('Expected a full source commit')
    repository = safe(repository)
    env = {key: value for key, value in os.environ.items() if not key.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull, GIT_NO_REPLACE_OBJECTS='1')
    return subprocess.run(['git', '--no-replace-objects', '-C', str(repository),
                           'cat-file', 'blob', commit + ':' + path],
                          env=env, check=True, capture_output=True, timeout=30).stdout


def read_sources(recipe, raw, repository=None):
    paths = [row['path'] for row in recipe['files']]
    if not paths or len(paths) != len(set(paths)):
        raise ValueError('Empty or duplicate source paths')
    return {path: read_source(raw, path, commit=recipe['commit'], repository=repository) for path in paths}


def load_recipe(path, *, commit, profile=None):
    recipe = json.loads(safe(path).read_bytes(), object_pairs_hook=unique_pairs)
    if recipe.get('commit') != commit or (profile is not None and recipe.get('profile') != profile):
        raise ValueError('Unsupported source version or conversion profile')
    if recipe.get('repository') != 'pret/pokefirered':
        raise ValueError('Unsupported source repository')
    for field, expected in (('schemaVersion', 1), ('gameEdition', 'FIRERED'), ('romRevision', 0)):
        if field in recipe and recipe[field] != expected:
            raise ValueError('Unsupported source ' + field)
    return recipe


def write_files(root, outputs):
    """Preflight every destination before replacing any output."""
    targets = [(safe(root, path), safe(root, path + '.tmp'), data) for path, data in outputs.items()]
    for target, stage, _ in targets:
        preflight_directory(target.parent)
        if any(path.exists() and not path.is_file() for path in (target, stage)):
            raise ValueError('Input/output target is not a regular file')
    for target, stage, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage.write_bytes(data)
        stage.replace(target)


def import_region(raw, out, *, recipe, convert, repository=None, protected=()):
    raw, out = safe(raw), safe(out)
    for path in (raw, out):
        preflight_directory(path)
    def overlaps(a, b):
        return a.resolve().is_relative_to(b.resolve()) or b.resolve().is_relative_to(a.resolve())
    if overlaps(raw, out):
        raise ValueError('Raw and output directories must be disjoint')
    for path in (*protected, *((Path(repository),) if repository is not None else ())):
        if any(overlaps(target, path) for target in (raw, out)):
            raise ValueError('Materialization must not overlap an input dependency')
    sources = read_sources(recipe, raw, repository)
    outputs = convert(recipe, sources)
    # Preflight both trees before either is written.
    for root, files in ((raw, sources if repository is not None else {}), (out, outputs)):
        for name in files:
            target, stage = safe(root, name), safe(root, name + '.tmp')
            preflight_directory(target.parent)
            if any(path.exists() and not path.is_file() for path in (target, stage)):
                raise ValueError('Input/output target is not a regular file')
    if repository is not None:
        write_files(raw, sources)
    write_files(out, outputs)
    return dict(profile=recipe['profile'], inputCount=len(sources), outputCount=len(outputs))


class SourceReader:
    """Lazy retained-raw or explicit fixed-revision bytes for offline compilers."""
    def __init__(self, raw, *, commit, repository=None):
        self.raw, self.commit, self.repository = raw, commit, repository
        self.data = {}

    def read(self, path):
        if path not in self.data:
            self.data[path] = read_source(self.raw, path, commit=self.commit, repository=self.repository)
        return self.data[path]

    def text(self, path):
        return self.read(path).decode('utf-8')

    def json(self, path):
        return json.loads(self.read(path), object_pairs_hook=unique_pairs)
