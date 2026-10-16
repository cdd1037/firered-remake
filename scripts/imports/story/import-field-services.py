#!/usr/bin/env python3
"""Compile final field-service items, trades, encounters and battle bindings from raw."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
import argparse
import json
from field_service_features import trade_offers, presentation_text, north_trainers, north_passage, service_bindings, memorial_trainers, cape_encounters, TM_MAPS
from item_source import ItemSource
from map_runtime import encode
from generated_output_compare import check_item_outputs
from source_features import feature_worlds, script_blocks
from project_paths import PROJECT_ROOT
from shared_trainers import trainer_json
from world_source import WorldSource


def generate(raw):
    source = WorldSource(raw)
    outputs = {}
    def document(package, name, value):
        outputs[f'maps/regions/{package}/{name}.json'] = value
    items = {r['itemId']: r for r in source.json('src/data/items.json')['items']}
    icons = ItemSource(source.data)
    for package, symbol in [('good-rod-service', 'ITEM_GOOD_ROD'), ('super-rod-service', 'ITEM_SUPER_ROD'), ('npc-trade-elyssa', 'ITEM_STICK')]:
        icon, png = icons.icon(symbol)
        outputs[icon['pngPath'].removeprefix('content/generated/')] = png
        item = dict(item='pret:' + symbol, description=items[symbol]['description_english'].replace('\\n', '\n'))
        if symbol == 'ITEM_STICK':
            item.update(healHp=0, cureStatus=0, effectBytes=[], useSupport=10)
            icon['texturePath'] = icon.pop('pngPath')
            icon['bindingSource'] = source.ref('src/data/item_icon_table.h', symbol)
            document(package, 'items', dict(additions=[item], icons=[icon]))
        else:
            document(package, 'items', dict(schemaVersion=1, items=[item], icons=[icon]))
    document('npc-trades', 'trades', trade_offers(source))
    document('npc-trades', 'presentation-text', presentation_text(source))
    document('cape-brink-tutor', 'encounters-items', cape_encounters(source))
    north = feature_worlds(source, ['Route16', 'Route16_NorthEntrance_1F', 'Route16_House'])
    document('route16-fly-house', 'passage', north_passage(north[:2], north[2]))
    document('route16-fly-house', 'trainers', north_trainers(source))
    worlds = feature_worlds(source, TM_MAPS)
    scripts = script_blocks(source, TM_MAPS, extra=('Std_FindItem','Std_ObtainItem','Std_ReceivedItem','Std_PutItemAway'))
    document('original-tm-acquisition', 'services', service_bindings(worlds, scripts))
    document('original-tm-acquisition', 'memorial-trainers', memorial_trainers(source))
    group = next(g for g in source.json('src/data/wild_encounters.json')['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    rows = [e for e in group['encounters'] if e['map'] == 'MAP_FIVE_ISLAND_MEMORIAL_PILLAR' and e['base_label'].endswith('_FireRed')]
    if len(rows) != 1: raise ValueError('Memorial encounter header differs')
    document('original-tm-acquisition', 'memorial-encounters', dict(slotTables=group['fields'], encounters=rows))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs = generate(args.raw)
    outputs = {relative: value if isinstance(value, bytes) else
               trainer_json(args.out, value, check=args.check) if relative.endswith('trainers.json') else encode(value)
               for relative, value in outputs.items()}
    if args.check:
        check_item_outputs(args.out, outputs)
    else:
        for relative, data in outputs.items():
            target = args.out / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(files=len(outputs), checked=args.check)))


if __name__ == '__main__': main()
