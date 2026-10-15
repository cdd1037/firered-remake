#!/usr/bin/env python3
"""Compile Memorial and retained-route pickup gameplay rows, without retaining source maps/scripts."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import pathlib
import re
ROOT = _PROJECT_ROOT

def convert(root):
    raw = root / 'content/raw/pokefirered/037335f4'
    blocks = {}
    for path in ['data/scripts/item_ball_scripts.inc', 'data/maps/Route12/scripts.inc', 'data/maps/Route15/scripts.inc', 'data/maps/FiveIsland_MemorialPillar/scripts.inc']:
        text = (raw / path).read_text()
        labels = list(re.finditer(r'^(\w+)::?(?:[ \t]*@.*)?$', text, re.M))
        for index, match in enumerate(labels):
            body = text[match.end():labels[index + 1].start() if index + 1 < len(labels) else len(text)]
            blocks[match[1]] = [line.split('@')[0].strip() for line in body.splitlines() if line.split('@')[0].strip()]

    def row(events, event, local_id, hidden):
        script = 'EventScript_HiddenItemScript' if hidden else event['script']
        if hidden:
            if event['underfoot']: raise ValueError('Unexpected Memorial underfoot item')
            item = event['item']; quantity = event['quantity']
        else:
            lines = blocks[script]
            if len(lines) != 2 or not lines[0].startswith('finditem ') or lines[1] != 'end': raise ValueError('Unsupported pickup script')
            item = lines[0][9:]; quantity = 1
        return dict(mapSymbol=events['id'], hidden=hidden, sourceLocalId=local_id, x=event['x'], y=event['y'],
            elevation=event['elevation'], scriptId=script, itemSymbol=item, quantity=quantity, flag=event['flag'])
    memorial_map = json.loads((raw / 'data/maps/FiveIsland_MemorialPillar/map.json').read_text())
    memorial = [row(memorial_map, event, i + 1, False) for i, event in enumerate(memorial_map['object_events']) if event['graphics_id'] == 'OBJ_EVENT_GFX_ITEM_BALL']
    memorial += [row(memorial_map, event, None, True) for event in memorial_map['bg_events'] if event['type'] == 'hidden_item']
    if len(memorial) != 5 or sum(e['hidden'] for e in memorial) != 4: raise ValueError('Memorial pickup closure')
    machines = []
    for map_name, label, item in [('Route12', 'Route12_EventScript_ItemTM48', 'ITEM_TM48'), ('Route15', 'Route15_EventScript_ItemTM18', 'ITEM_TM18')]:
        events = json.loads((raw / f'data/maps/{map_name}/map.json').read_text())
        actors = [(i + 1, e) for i, e in enumerate(events['object_events']) if e.get('script') == label]
        if len(actors) != 1 or actors[0][1]['graphics_id'] != 'OBJ_EVENT_GFX_ITEM_BALL': raise ValueError('Machine pickup actor')
        result = row(events, actors[0][1], actors[0][0], False)
        if result['itemSymbol'] != item: raise ValueError('Machine pickup item')
        machines.append(result)
    return dict(memorial=memorial, machines=machines)

def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    data = (json.dumps(convert(ROOT), sort_keys=True, indent=2) + '\n').encode()
    path = ROOT / 'content/generated/maps/regions/original-tm-acquisition/pickups.json'
    if args.check:
        if path.read_bytes() != data: raise ValueError('TM pickups differ')
    else: path.write_bytes(data)
    print(json.dumps(dict(pickups=7, checked=args.check)))
if __name__ == '__main__': main()
