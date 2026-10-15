#!/usr/bin/env python3
"""Import gameplay pickup definitions from local pokefirered maps and item data."""
import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SCOPE = [('ViridianCity', 'object_events', 17, 5),
         ('ViridianForest', 'object_events', 5, 41),
         ('ViridianForest', 'object_events', 40, 21),
         ('ViridianForest', 'object_events', 21, 34),
         ('ViridianForest', 'object_events', 49, 60),
         ('ViridianForest', 'bg_events', 3, 22),
         ('ViridianForest', 'bg_events', 28, 57),
         ('PewterCity', 'bg_events', 6, 3)]


def import_catalog(raw, out):
    raw, out = Path(raw), Path(out)
    items = {item['itemId']: item for item in json.loads((raw / 'src/data/items.json').read_text())['items']}
    headers = {name: (raw / ('include/constants/' + name + '.h')).read_text() for name in ('items', 'flags')}
    scripts = (raw / 'data/scripts/item_ball_scripts.inc').read_text()

    def constant(header, symbol):
        match = re.search(r'^#define\s+' + re.escape(symbol) + r'\s+([^\n]+)', headers[header], re.M)
        if match is None:
            raise ValueError('Missing constant: ' + symbol)
        expression = match[1].split('//')[0].strip()
        if re.fullmatch(r'0x[0-9a-fA-F]+|[0-9]+', expression):
            return int(expression, 16 if expression.startswith('0x') else 10)
        offset = re.fullmatch(r'\(FLAG_HIDDEN_ITEMS_START\s*\+\s*([0-9]+)\)', expression)
        if offset:
            return constant(header, 'FLAG_HIDDEN_ITEMS_START') + int(offset[1])
        raise ValueError('Unsupported constant expression: ' + expression)

    pickups = []
    for map_name, array_name, x, y in SCOPE:
        data = json.loads((raw / ('data/maps/' + map_name + '/map.json')).read_text())
        matches = [(index, event) for index, event in enumerate(data[array_name]) if (event['x'], event['y']) == (x, y)]
        if len(matches) != 1:
            raise ValueError('Expected one pickup at ' + map_name + ':' + str((x, y)))
        index, event = matches[0]
        hidden = array_name == 'bg_events'
        if hidden:
            symbol, quantity, script = event['item'], event['quantity'], 'EventScript_HiddenItemScript'
        else:
            script = event['script']
            match = re.search(r'^' + re.escape(script) + r'::?\n\s*finditem (ITEM_\w+)(?:, (\d+))?', scripts, re.M)
            if match is None:
                raise ValueError('Missing item-ball command: ' + script)
            symbol, quantity = match[1], int(match[2] or '1')
        item = items[symbol]
        pickups.append(dict(id=event['flag'], mapSymbol=data['id'], kind='hiddenItem' if hidden else 'itemBall',
            sourceEventIndex=index, sourceLocalId=None if hidden else index + 1, x=x, y=y, elevation=event['elevation'],
            script=script, itemSymbol=symbol, sourceItemId=constant('items', symbol), itemId='pret:' + symbol,
            itemName=item['english'], pocket=item['pocket'], quantity=quantity, flag=event['flag'],
            sourceFlagId=constant('flags', event['flag']), underfoot=event.get('underfoot', False)))
    out.mkdir(parents=True, exist_ok=True)
    (out / 'early-pickups.json').write_text(json.dumps(pickups, ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8')
    return {'pickups': len(pickups)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=ROOT / 'content/generated/early-pickups')
    args = parser.parse_args()
    print(json.dumps(import_catalog(args.raw, args.out)))
