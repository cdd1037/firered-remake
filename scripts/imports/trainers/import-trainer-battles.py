#!/usr/bin/env python3
"""Import regional trainer selections through the shared declaration converter."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
import argparse
import json
import subprocess

from project_paths import PROJECT_ROOT
from shared_trainers import trainer_json
from trainer_import import convert_battles

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def safe(root, relative):
    if not relative or Path(relative).is_absolute() or '\\' in relative or any(part in ('', '.', '..') for part in relative.split('/')):
        raise ValueError('Unsafe relative source path: ' + relative)
    path = Path(root) / relative
    if any(part.is_symlink() for part in (path, *path.parents)):
        raise ValueError('Symlink path rejected: ' + str(path))
    return path


class Sources(dict):
    """Load only conversion inputs, optionally from pinned Git objects."""
    def __init__(self, raw, reference=None):
        super().__init__()
        self.raw, self.reference = Path(raw), reference

    def __missing__(self, path):
        target = safe(self.raw, path)
        data = (subprocess.check_output(['git', '-C', str(self.reference), 'show', COMMIT + ':' + path])
                if self.reference else target.read_bytes())
        self[path] = data
        return data


def run(recipe_path, raw, out=None, generated_root=None, reference=None, check=False):
    recipe_path = Path(recipe_path)
    recipe = json.loads(recipe_path.read_text())
    generated_root = Path(generated_root).absolute() if generated_root else None
    out = Path(out).absolute() if out else (generated_root or PROJECT_ROOT / 'content/generated') / recipe['output']
    if generated_root is None:
        relative = Path(recipe['output'])
        if tuple(out.parts[-len(relative.parts):]) != relative.parts:
            if out.parent.parts[-2:] != ('data', 'trainer-battles'):
                raise ValueError('Use --generated-root for an output outside data/trainer-battles')
            generated_root = out.parents[2]
        else:
            generated_root = out.parents[len(relative.parts) - 1]
    roots = [Path(raw).absolute(), out, generated_root]
    if reference:
        roots.append(Path(reference).absolute())
    for path in roots:
        if '..' in path.parts:
            raise ValueError('Parent traversal root rejected')
        if any(part.is_symlink() for part in (path, *path.parents)):
            raise ValueError('Symlink root rejected: ' + str(path))
    def overlaps(a, b):
        return a == b or a.is_relative_to(b) or b.is_relative_to(a)
    if overlaps(roots[0], generated_root) or (reference and (overlaps(roots[-1], roots[0]) or overlaps(roots[-1], generated_root))):
        raise ValueError('Source and generated roots must be disjoint')
    for path in ('scripts', 'src', 'tests', '.git'):
        if overlaps(generated_root, PROJECT_ROOT / path):
            raise ValueError('Protected project path overlap')
    if out == generated_root / 'data/trainers.json':
        raise ValueError('Regional output cannot replace shared trainer definitions')
    sources = Sources(raw, reference)
    document = convert_battles(recipe, sources)
    # Validate output locations before the shared publisher writes anything.
    for path in (out, generated_root / 'data/trainers.json'):
        if path.exists() and not path.is_file():
            raise ValueError('Output is not a regular file')
        if any(parent.exists() and not parent.is_dir() for parent in path.parents):
            raise ValueError('Output parent is not a directory')
    payload = trainer_json(generated_root, document, check=check)
    if check:
        if out.read_bytes() != payload:
            raise ValueError('Regional trainer output differs: ' + str(out))
    else:
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(payload)
        if reference:
            for name, data in sources.items():
                target = safe(raw, name)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
    return dict(trainers=len(document['trainers']), battles=len(document['battles']), inputs=len(sources), checked=check)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recipe', type=Path, required=True)
    parser.add_argument('--raw', '--raw-root', dest='raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-repo', '--reference-root', dest='reference', type=Path)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--generated-root', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.recipe, args.raw, args.out, args.generated_root, args.reference, args.check)))


if __name__ == '__main__':
    main()
