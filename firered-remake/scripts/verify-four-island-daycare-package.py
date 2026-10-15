#!/usr/bin/env python3
"""Independent bounded source/map evidence checks, without a game build or reference code execution."""
import argparse
import base64
import json
from pathlib import Path
import struct


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--dependency-root', type=Path, required=True)
    parser.add_argument('--result', type=Path, required=True)
    args = parser.parse_args()
    root, dependency = args.project_root.resolve(), args.dependency_root.resolve()
    raw = root / 'content/raw/pokefirered/037335f4'; gen = root / 'content/generated/four-island-daycare'
    checks = []
    def need(value, name):
        if not value: raise ValueError(name)
        checks.append(name)
    def doc(path): return json.loads(path.read_bytes())
    try:
        lock_bytes = (root / 'scripts/four-island-daycare.lock.json').read_bytes()
        manifest_bytes = (gen / 'manifest.json').read_bytes()
        lock, manifest = json.loads(lock_bytes), json.loads(manifest_bytes)
        need(len(lock['files']) == 82 and len(manifest['outputs']) == 15, 'bounded 82-input 15-output scope')
        need(all((raw / row['path']).is_file() for row in lock['files']), 'source files present')
        need(all((gen / row['path']).is_file() for row in manifest['outputs']), 'output files present')
        catalog, contract, scripts = [doc(gen / (n + '.json')) for n in ['catalog', 'contracts', 'scripts']]
        runtime = doc(gen / catalog['maps'][0]['path'])
        original = doc(raw / 'data/maps/FourIsland_PokemonDayCare/map.json')
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
        need(json.loads(b)['events'] == doc(raw / 'data/maps/FourIsland/map.json'), 'existing FourIsland events equal pinned original')
        need([(e['x'], e['y'], e['dest_map'], e['dest_warp_id']) for e in original['warp_events']] ==
             [(4, 7, 'MAP_FOUR_ISLAND', '1')], 'single exit returns to FourIsland entrance 1')
        need(len(scripts['blocks']) == 58 and not scripts['unresolvedLabelReferences'], 'complete nonempty 58-label closure')
        need(scripts['futureUnsupportedRoots'] == ['EventScript_EggHatch'] and not contract['service']['eggHatchingAdmitted'], 'egg hatching remains future unsupported')
        need(retained['entrances'] == [dict(index=1, event=dict(x=12, y=13, elevation=0,
            dest_map='MAP_FOUR_ISLAND_POKEMON_DAY_CARE', dest_warp_id='0'))], 'original exterior entrance selects indoor warp 0')
        man = retained['dayCareMan']
        need(man['localId'] == 1 and man['actor']['graphics_id'] == 'OBJ_EVENT_GFX_OLD_MAN_1' and
            (man['actor']['x'], man['actor']['y'], man['actor']['elevation']) == (16, 13, 3) and
            man['pendingEggPosition'] == dict(x=16,y=14), 'original outside man and pending-egg position')
        labels = {b['label'] for b in scripts['blocks']}
        need(not labels.intersection(scripts['excludedRoots']), 'unrelated and unused roots excluded')
        need(set(scripts['roots']).issubset(labels), 'every bounded root has source closure')
        for block in scripts['blocks']:
            source = block['sourceRef']; original_lines = (raw / source['path']).read_text().splitlines()
            need(original_lines[source['line']-1].strip() in (block['label'] + ':', block['label'] + '::') and
                all(original_lines[row['line']-1] == row['text'] for row in block['lines']), 'exact original script block ' + block['label'])
        blocks = {b['label']: b for b in scripts['blocks']}
        callback = '\n'.join(r['text'] for r in blocks['FourIsland_EventScript_TrySetDayCareManPos']['lines'])
        need('goto_if_unset FLAG_PENDING_DAYCARE_EGG, FourIsland_EventScript_EndSetDayCareManPos' in callback and
             'setobjectxyperm LOCALID_FOUR_ISLAND_DAYCARE_MAN, 16, 14' in callback and
             any(r['text'].strip() == 'return' for r in blocks['FourIsland_EventScript_EndSetDayCareManPos']['lines']),
             'source pending-egg callback and lexical return')
        need(scripts['requiredApplicationSpecials'] == sorted(scripts['specialCounts']) and len(scripts['specialCounts']) == 19,
             'explicit 19-special Application integration boundary')
        need(contract['service']['slotCount'] == 2 and contract['service']['states'] == dict(empty=0, pendingEgg=1, oneMon=2, twoMons=3),
             'original two-slot state and pending-egg precedence')
        need([m['score'] for m in contract['service']['compatibilityMessages']] == [70,50,20,0] and
             all(m['symbol'] in (raw / 'src/strings.c').read_text() for m in contract['service']['compatibilityMessages']),
             'four original compatibility texts in source order')
        graphics = doc(dependency / 'content/generated/shared-overworld/catalog.json')
        for actor in doc(gen / 'npc-source.json')['graphics']:
            need(actor['graphicsId'] in graphics, 'shared actor ' + actor['graphicsId'])
        need(len(checks) > 100, 'nonempty independent package verification')
        result = dict(passed=True, checkCount=len(checks), checks=checks, scope='Source and compact map evidence only; Application, save flow, UI, breeding and audio acceptance are separate; no C# compilation was run by this verifier.')
    except Exception as ex:
        result = dict(passed=False, checkCount=len(checks), checks=checks, error=str(ex))
        raise
    finally:
        args.result.parent.mkdir(parents=True, exist_ok=True)
        args.result.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k:v for k,v in result.items() if k != 'checks'}, indent=2))

if __name__ == '__main__': main()
