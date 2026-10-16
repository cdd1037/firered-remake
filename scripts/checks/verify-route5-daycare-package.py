#!/usr/bin/env python3
"""Independent bounded source/map evidence checks, without a game build or reference code execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import base64
import json
from pathlib import Path
import struct


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=_PROJECT_ROOT)
    parser.add_argument('--dependency-root', type=Path, required=True)
    parser.add_argument('--result', type=Path, required=True)
    args = parser.parse_args()
    root, dependency = args.project_root.resolve(), args.dependency_root.resolve()
    raw = root / 'content/raw/pokefirered/037335f4'; gen = root / 'content/generated/maps/regions/route5-daycare'
    checks = []
    def need(value, name):
        if not value: raise ValueError(name)
        checks.append(name)
    def doc(path): return json.loads(path.read_bytes())
    try:
        lock_bytes = (root / 'scripts/recipes/story/route5-daycare.lock.json').read_bytes()
        manifest_bytes = (gen / 'manifest.json').read_bytes()
        lock, manifest = json.loads(lock_bytes), json.loads(manifest_bytes)
        need(len(lock['files']) == 80 and len(manifest['outputs']) == 15, 'bounded 80-input 15-output scope')
        need(all((raw / row['path']).is_file() for row in lock['files']), 'source files present')
        need(all((gen / row['path']).is_file() for row in manifest['outputs']), 'output files present')
        catalog, contract, scripts = [doc(gen / (n + '.json')) for n in ['catalog', 'contracts', 'scripts']]
        runtime = doc(gen / catalog['maps'][0]['path'])
        original = doc(raw / 'data/maps/Route5_PokemonDayCare/map.json')
        need(runtime['events'] == original, 'exact original map events')
        need(runtime['width'] == 12 and runtime['height'] == 9, 'original 12 by 9 geometry')
        layouts = doc(raw / 'data/layouts/layouts.json')['layouts']
        layout = next(l for l in layouts if l.get('id') == original['layout'])
        attrs = {row[0]:row[1] for row in runtime['metatiles']}
        for field, key in [('blocks', 'blockdata_filepath'), ('borderBlocks', 'border_filepath')]:
            b = base64.b64decode(runtime[field], validate=True)
            need(b == (raw / layout[key]).read_bytes(), 'original raw ' + field)
            for block, in struct.iter_unpack('<H', b):
                tile = block & 1023
                tileset = 'primary/building' if tile < 640 else 'secondary/fan_club_daycare'
                values = (raw / 'data/tilesets' / tileset / 'metatile_attributes.bin').read_bytes()
                need(attrs[tile] == struct.unpack_from('<I', values, (tile % 640) * 4)[0], 'original metatile attributes ' + str(tile))
        retained = contract['retainedTown']; package = dependency / retained['currentPackage']
        row = retained['currentResource']; b = (package / row['path']).read_bytes()
        need(json.loads(b)['events'] == doc(raw / 'data/maps/Route5/map.json'), 'existing Route5 events equal pinned original')
        need([(e['x'], e['y'], e['dest_map'], e['dest_warp_id']) for e in original['warp_events']] ==
             [(x, 7, 'MAP_ROUTE5', '1') for x in (3,4,5)], 'all three exits return to Route5 entrance 1')
        need(len(scripts['blocks']) == 34 and not scripts['unresolvedLabelReferences'], 'complete nonempty 34-label closure')
        need(scripts['futureUnsupportedRoots'] == ['EventScript_EggHatch'] and not contract['service']['eggHatchingAdmitted'], 'egg hatching remains future unsupported')
        graphics = doc(dependency / 'content/generated/sprites/overworld/catalog.json')
        for actor in doc(gen / 'npc-source.json')['graphics']:
            need(actor['graphicsId'] in graphics, 'shared actor ' + actor['graphicsId'])
        need(len(checks) > 100, 'nonempty independent package verification')
        result = dict(passed=True, checkCount=len(checks), checks=checks, scope='Source and compact map evidence only; Application, save flow, UI, breeding and audio acceptance are separate.')
    except Exception as ex:
        result = dict(passed=False, checkCount=len(checks), checks=checks, error=str(ex))
        raise
    finally:
        args.result.parent.mkdir(parents=True, exist_ok=True)
        args.result.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k:v for k,v in result.items() if k != 'checks'}, indent=2))

if __name__ == '__main__': main()
