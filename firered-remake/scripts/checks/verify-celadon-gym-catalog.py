#!/usr/bin/env python3
"""Verify current Celadon Pokemon presentation, without historical stage catalogs."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'lib'))
from project_paths import PROJECT_ROOT
import argparse
import importlib.util
import io
import json
from PIL import Image


def verify(project, repository=None):
    path = PROJECT_ROOT / 'scripts/imports/items/import-celadon-gym-catalog.py'
    spec = importlib.util.spec_from_file_location('celadon_verification', path)
    importer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(importer)
    raw = project / 'content/raw/pokefirered/037335f4'
    lock, sources = importer.load_sources('celadon-gym-pokemon', raw, repository)
    first = importer.pokemon_outputs(lock, sources, project)
    second = importer.pokemon_outputs(lock, sources, project)
    if first != second or {'catalog.json', 'source-species.json'} & first.keys():
        raise AssertionError('Presentation must be repeatable and publish no retired catalogs')
    directory = project / 'content/generated/sprites/pokemon/celadon-gym'
    pixels = 0
    for name, data in first.items():
        actual = (directory / name).read_bytes()
        if name.endswith('.png'):
            with Image.open(io.BytesIO(data)) as a, Image.open(io.BytesIO(actual)) as b:
                if a.size != b.size or a.convert('RGBA').tobytes() != b.convert('RGBA').tobytes():
                    raise AssertionError('Decoded pixels differ: ' + name)
                pixels += a.width * a.height
        elif name.endswith('.json'):
            if json.loads(data) != json.loads(actual):
                raise AssertionError('Presentation semantics differ: ' + name)
        elif data != actual:
            raise AssertionError('Presentation bytes differ: ' + name)
    return dict(outputCount=len(first), decodedPixels=pixels, repeatedConversions=2,
                retiredCatalogsPublished=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--reference-repo', type=Path)
    parser.add_argument('--evidence', type=Path)
    args = parser.parse_args()
    report = verify(args.project_root, args.reference_repo)
    if args.evidence:
        args.evidence.parent.mkdir(parents=True, exist_ok=True)
        args.evidence.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, sort_keys=True))


if __name__ == '__main__':
    main()
