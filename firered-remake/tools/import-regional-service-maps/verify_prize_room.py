#!/usr/bin/env python3
"""Reconstruct only the existing original prize room through the shared reviewed importer."""
import argparse
import json
from pathlib import Path
import sys
import tempfile
sys.dont_write_bytecode = True
import import_regional_service_maps as shared


def content(value):
    # Older generated packages may still include descriptive identity fields.
    if isinstance(value, dict):
        omitted = {'sha256', 'byteLength', 'fullSourceHash', 'sourceWorldSha256', 'converterSha256'}
        return {key: content(item) for key, item in value.items() if key not in omitted}
    if isinstance(value, list):
        return [content(item) for item in value]
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dependency-root', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    dependency = args.dependency_root.resolve()
    package = dependency / 'content/generated/maps/regions/regional-service-maps'
    manifest_bytes = (package / 'manifest.json').read_bytes()
    manifest = json.loads(manifest_bytes)
    directory = 'celadon-city-game-corner-prize-room'
    source_name = 'CeladonCity_GameCorner_PrizeRoom'
    stable_id = 'map.' + directory
    expected = {row['path']: row for row in manifest['outputs'] if row['path'].startswith(directory + '/')}
    if not expected or directory + '/runtime-map.json' not in expected:
        raise ValueError('Original room output inventory differs')
    src = shared.SHARED.Source(dependency / 'content/raw/pokefirered/037335f4')
    runs = []
    for _ in range(2):
        with tempfile.TemporaryDirectory(prefix='firered-prize-room-') as temporary:
            output = Path(temporary) / directory
            shared.SHARED.import_map(src, source_name, stable_id, output)
            rich = (output / 'runtime-map.json').read_bytes()
            compact = shared.CONVERTER.encode(shared.CONVERTER.normalize_runtime(json.loads(rich)))
            (output / 'runtime-map.json').write_bytes(compact)
            rows = []
            for path in sorted(output.iterdir()):
                relative = directory + '/' + path.name
                data = path.read_bytes()
                row = dict(path=relative)
                actual = (package / relative).read_bytes()
                matches = content(json.loads(data)) == content(json.loads(actual)) if path.suffix == '.json' else data == actual
                if relative not in expected or not matches:
                    raise ValueError('Shared reconstruction differs: ' + relative)
                rows.append(row)
            if {r['path'] for r in rows} != set(expected):
                raise ValueError('Missing or extra reconstructed outputs')
            runs.append(rows)
    if runs[0] != runs[1]:
        raise ValueError('Cold reconstruction differs')
    report = dict(status='passed', sourceCommit=shared.COMMIT,
        sharedImporter='tools/import-regional-service-maps/import_regional_service_maps.py',
        coldReconstructions=2,
        mapCount=1, newRuntimeResources=[], reusedOutputs=runs[0])
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(status='passed', mapCount=1, matchingOutputs=len(expected), coldReconstructions=2)))


if __name__ == '__main__':
    main()
