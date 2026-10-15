#!/usr/bin/env python3
"""Extract all original FireRed rock actors/tables from pinned Git blobs. No upstream code is run."""
import argparse
import json
import pathlib
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
ROOT = pathlib.Path(__file__).resolve().parents[1]
GRAPHICS = 'OBJ_EVENT_GFX_ROCK_SMASH_ROCK'
SCRIPT = 'EventScript_RockSmash'

def need(condition, reason):
    if not condition:
        raise ValueError(reason)


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', required=True, type=pathlib.Path)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/field-rock-smash')
    args = parser.parse_args()
    repo = args.reference_repo.resolve()
    output = args.output.resolve()
    need(not output.is_relative_to(repo) and not repo.is_relative_to(output), 'Reference/output overlap')
    def git(*parts):
        return subprocess.check_output(['git', '-C', str(repo), *parts])
    tree = {}
    for line in git('ls-tree', '-r', COMMIT).decode().splitlines():
        meta, path = line.split('\t', 1)
        mode, kind, blob = meta.split()
        if kind == 'blob':
            tree[path] = blob
    inputs = {}
    def read(path, retain=True):
        need(path in tree, 'Missing source: ' + path)
        data = git('cat-file', 'blob', tree[path])
        if retain:
            inputs[path] = dict(path=path)
        return data
    def source(path):
        read(path)
        return dict(path=path)
    # Scan every pinned map declaration, not a hand-picked coordinate/region list.
    maps = sorted(path for path in tree if re.fullmatch(r'data/maps/[^/]+/map.json', path))
    need(len(maps) == 425, 'Pinned map census differs')
    rocks = []
    map_names = {}
    for path in maps:
        data = json.loads(read(path, retain=False))
        selected = [(i + 1, row) for i, row in enumerate(data.get('object_events', [])) if row.get('graphics_id') == GRAPHICS]
        if not selected:
            continue
        map_id = 'map.' + data['id'][4:].lower().replace('_', '-')
        map_names[data['id']] = map_id
        ref = source(path)
        for local, row in selected:
            need(row['type'] == 'object' and row['script'] == SCRIPT and row['trainer_type'] == 'TRAINER_TYPE_NONE', 'Unexpected rock object contract')
            need(row['movement_type'] == 'MOVEMENT_TYPE_LOOK_AROUND' and row['movement_range_x'] == row['movement_range_y'] == 1, 'Unexpected rock movement')
            need(re.fullmatch(r'FLAG_TEMP_[0-9A-F]+', row['flag']) is not None, 'Rock removal flag is not temporary')
            rocks.append(dict(map={'value': map_id}, sourceMapSymbol=data['id'], sourceLocalId=local,
                x=row['x'], y=row['y'], elevation=row['elevation'], graphicsId=row['graphics_id'], scriptId=row['script'],
                movementType=row['movement_type'], movementRangeX=row['movement_range_x'], movementRangeY=row['movement_range_y'],
                hideFlag=row['flag'], source=ref))
    wild_path = 'src/data/wild_encounters.json'
    groups = json.loads(read(wild_path))['wild_encounter_groups']
    headers = [g for g in groups if g.get('for_maps') and g['label'] == 'gWildMonHeaders']
    need(len(headers) == 1, 'Wild header group differs')
    weights = next(f['encounter_rates'] for f in headers[0]['fields'] if f['type'] == 'rock_smash_mons')
    need(weights == [60, 30, 5, 4, 1], 'Source Water/Rock weights differ')
    tables = []
    for encounter in headers[0]['encounters']:
        if not encounter['base_label'].endswith('_FireRed') or 'rock_smash_mons' not in encounter:
            continue
        need(encounter['map'] in map_names, 'Table lacks source rock map')
        row = encounter['rock_smash_mons']
        need(len(row['mons']) == 5, 'Source rock slot count differs')
        tables.append(dict(map={'value': map_names[encounter['map']]}, sourceMapSymbol=encounter['map'],
            sourceSymbol=encounter['base_label'], rawEncounterRate=row['encounter_rate'],
            slots=[dict(index=i, species={'value': 'pret:' + m['species']}, minimumLevel=m['min_level'],
                maximumLevel=m['max_level'], weight=weights[i]) for i, m in enumerate(row['mons'])], source=source(wild_path)))
    tables.sort(key=lambda row: row['sourceMapSymbol'])
    need(len(rocks) == 97 and len(map_names) == 15 and len(tables) == 14, 'Source census differs')
    no_tables = sorted(set(map_names) - {t['sourceMapSymbol'] for t in tables})
    need(no_tables == ['MAP_FOUR_ISLAND'], 'No-table source maps differ')
    # Pin the actual semantics and gift path; assertions bind salient rules to their source.
    rules = {}
    for path in ['data/scripts/field_moves.inc', 'src/fldeff_rocksmash.c', 'src/wild_encounter.c',
                 'src/party_menu.c', 'include/constants/party_menu.h', 'src/event_object_movement.c',
                 'src/data/object_events/object_event_anims.h', 'src/event_data.c', 'src/overworld.c',
                 'include/constants/flags.h', 'src/scrcmd.c', 'src/battle_setup.c',
                 'data/maps/OneIsland_KindleRoad_EmberSpa/scripts.inc',
                 'data/maps/OneIsland_KindleRoad_EmberSpa/map.json']:
        rules[path] = read(path).decode()
    field = rules['data/scripts/field_moves.inc']
    start = field.index('EventScript_RockSmash::')
    end = field.index('EventScript_StrengthBoulder::', start)
    block = field[start:end]
    need('goto_if_unset FLAG_BADGE06_GET, EventScript_CantSmashRock' in block, 'Rock badge differs')
    need(block.index('removeobject VAR_LAST_TALKED') < block.index('special RockSmashWildEncounter'), 'Removal order differs')
    wild = rules['src/wild_encounter.c']
    need('rockSmashMonsInfo->encounterRate, TRUE' in wild and 'WILD_AREA_ROCKS, WILD_CHECK_REPEL' in wild, 'Rock encounter entry differs')
    need('encounterRate += sWildEncounterData.encounterRateBuff * 16 / 200;' in wild and
         'sWildEncounterData.leadMonHeldItem == ITEM_CLEANSE_TAG' in wild, 'Rate modifier source differs')
    gift_path = 'data/maps/OneIsland_KindleRoad_EmberSpa/scripts.inc'
    gift_script = 'OneIsland_KindleRoad_EmberSpa_EventScript_RockSmashMan'
    gift_rows = json.loads(rules['data/maps/OneIsland_KindleRoad_EmberSpa/map.json'])['object_events']
    gift = [(i + 1, row) for i, row in enumerate(gift_rows) if row.get('script') == gift_script]
    need(len(gift) == 1 and 'giveitem ITEM_HM06\n\tsetflag FLAG_GOT_HM06' in rules[gift_path], 'HM06 source gift differs')
    # Exact initial move closure for every source slot/level, including the high Cerulean rows.
    levels = {}
    for table in tables:
        for slot in table['slots']:
            levels.setdefault(slot['species']['value'], set()).update(range(slot['minimumLevel'], slot['maximumLevel'] + 1))
    learn_path = 'src/data/pokemon/level_up_learnsets.h'
    pointer_path = 'src/data/pokemon/level_up_learnset_pointers.h'
    info_path = 'src/data/pokemon/species_info.h'
    learnsets, pointers, info = read(learn_path).decode(), read(pointer_path).decode(), read(info_path).decode()
    numbers = read('include/constants/species.h').decode()
    pokemon_code = read('src/pokemon.c').decode()
    need('if (GiveMoveToBoxMon(boxMon, move) == MON_HAS_MAX_MOVES)' in pokemon_code and
         'DeleteFirstMoveAndGiveMoveToBoxMon(boxMon, move);' in pokemon_code, 'Initial move insertion source differs')
    species_rows, initial_rows = [], []
    for stable, values in sorted(levels.items()):
        symbol = stable[5:]
        pointer = re.search(r'\[' + symbol + r'\]\s*=\s*(\w+)', pointers)
        number = re.search(r'^#define\s+' + symbol + r'\s+(\d+)\b', numbers, re.M)
        need(pointer and number, 'Missing species pointer/number')
        learnset = re.search(r'static const u16 ' + pointer[1] + r'\[\]\s*=\s*\{(.*?)\};', learnsets, re.S)
        definition = re.search(r'\[' + symbol + r'\]\s*=\s*\{(.*?)\n\s*\},', info, re.S)
        need(learnset and definition and '#' not in learnset[1] and '#' not in definition[1], 'Unsupported conditional species block')
        rows = [(int(level), move) for level, move in re.findall(r'LEVEL_UP_MOVE\((\d+),\s*(MOVE_\w+)\)', learnset[1])]
        need(rows and [x[0] for x in rows] == sorted(x[0] for x in rows), 'Unsorted initial learnset')
        abilities = re.search(r'\.abilities\s*=\s*\{(ABILITY_\w+),\s*(ABILITY_\w+)\}', definition[1])
        common = re.search(r'\.itemCommon\s*=\s*(ITEM_\w+)', definition[1])
        rare = re.search(r'\.itemRare\s*=\s*(ITEM_\w+)', definition[1])
        need(abilities and common and rare, 'Missing species abilities/items')
        species_rows.append(dict(species={'value': stable}, sourceSpeciesId=int(number[1]), ability1=abilities[1], ability2=abilities[2],
            itemCommon=common[1], itemRare=rare[1], learnsetSymbol=pointer[1], source=source(info_path), learnsetSource=source(learn_path)))
        for level in sorted(values):
            moves = []
            for learnt_at, move in rows:
                if learnt_at > level:
                    break
                if move in moves:
                    continue
                moves.append(move)
                if len(moves) > 4:
                    moves.pop(0)
            initial_rows.append(dict(species={'value': stable}, level=level, moves=[{'value': 'pret:' + move} for move in moves]))
    need(len(species_rows) == 4 and len(initial_rows) == 139, 'Rock species/level closure differs')
    document = dict(sourceCommit=COMMIT, rocks=rocks, encounterTables=tables,
        wild=dict(species=species_rows, initialMoves=initial_rows))
    payload = (json.dumps(document, ensure_ascii=False, indent=2) + '\n').encode()
    contract = dict(schemaVersion=1, sourceCommit=COMMIT,  scannedMapCount=len(maps),
        rockCount=len(rocks), rockMapCount=len(map_names), encounterTableCount=len(tables), mapsWithoutRockEncounters=no_tables,
        upstreamScriptsExecuted=False, romDownloaded=False,
        gift=dict(map='map.one-island-kindle-road-ember-spa', sourceLocalId=gift[0][0], script=gift_script,
            item='ITEM_HM06', flag='FLAG_GOT_HM06', source=source(gift_path)),

        inputs=sorted(inputs.values(), key=lambda row: row['path']))
    output.mkdir(parents=True, exist_ok=True)
    (output / 'field.json').write_bytes(payload)
    (output / 'source-contract.json').write_text(json.dumps(contract, indent=2) + '\n')
    print(json.dumps({k: contract[k] for k in ['rockCount', 'rockMapCount', 'encounterTableCount']}, indent=2))

if __name__ == '__main__':
    main()
