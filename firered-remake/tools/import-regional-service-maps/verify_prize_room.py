#!/usr/bin/env python3
"""Reconstruct only the existing original prize room through the shared reviewed importer."""
import argparse
import json
from pathlib import Path
import sys
import tempfile
sys.dont_write_bytecode = True
import import_regional_service_maps as shared


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dependency-root', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    dependency = args.dependency_root.resolve()
    package = dependency / 'content/generated/regional-service-maps'
    manifest_bytes = (package / 'manifest.json').read_bytes()
    if shared.digest(manifest_bytes) != shared.MANIFEST_SHA256:
        raise ValueError('Existing regional package identity differs')
    manifest = json.loads(manifest_bytes)
    directory = 'celadon-city-game-corner-prize-room'
    source_name = 'CeladonCity_GameCorner_PrizeRoom'
    stable_id = 'map.' + directory
    expected = {row['path']: row for row in manifest['outputs'] if row['path'].startswith(directory + '/')}
    if len(expected) != 10:
        raise ValueError('Original room output inventory differs')
    src = shared.SHARED.Source(dependency / 'content/raw/regional-service-maps')
    runs = []
    for _ in range(2):
        with tempfile.TemporaryDirectory(prefix='firered-prize-room-') as temporary:
            output = Path(temporary) / directory
            shared.SHARED.import_map(src, source_name, stable_id, output)
            rich = (output / 'world.json').read_bytes()
            compact = shared.CONVERTER.encode(shared.CONVERTER.convert(rich, shared.digest(rich), shared.SHARED.source_hash(src.lock)))
            (output / 'runtime-map.json').write_bytes(compact)
            rows = []
            for path in sorted(output.iterdir()):
                relative = directory + '/' + path.name
                data = path.read_bytes()
                row = dict(path=relative, byteLength=len(data), sha256=shared.digest(data))
                if row != expected.get(relative) or data != (package / relative).read_bytes():
                    raise ValueError('Shared reconstruction differs: ' + relative)
                rows.append(row)
            if {r['path'] for r in rows} != set(expected):
                raise ValueError('Missing or extra reconstructed outputs')
            runs.append(rows)
    if runs[0] != runs[1]:
        raise ValueError('Cold reconstruction differs')
    report = dict(status='passed', sourceCommit=shared.COMMIT, sourceTree=shared.TREE,
        sharedImporter='tools/import-regional-service-maps/import_regional_service_maps.py',
        regionalManifestSha256=shared.MANIFEST_SHA256, coldReconstructions=2,
        mapCount=1, newRuntimeResources=[], reusedOutputs=runs[0])
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(status='passed', mapCount=1, matchingOutputs=10, coldReconstructions=2)))


if __name__ == '__main__':
    main()
