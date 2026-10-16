"""Raw-source inputs for Sevii trainer, encounter and item rule compilers."""
import re
from pathlib import Path

from project_paths import PROJECT_ROOT
from source_features import script_blocks
from trainer_import import natural_moves
from world_source import WorldSource
from world_selection import load_selection


def region_context(raw, region):
    raw = Path(raw)
    if (raw / ".git").exists():
        raise ValueError("Sevii rules require the pinned raw source directory, not a Git checkout")
    source = WorldSource(raw)
    manifest = load_selection(PROJECT_ROOT / 'data/world-maps.json')
    maps = [row for row in manifest if row['path'].startswith('maps/regions/' + region + '/') and not row.get('variant')]
    names = [row['sourceName'] for row in maps]
    anchors = ['CinnabarIsland', 'CinnabarIsland_PokemonCenter_1F'] if region == 'first-sevii' else ['OneIsland_PokemonCenter_1F']
    scripts = script_blocks(source, names + anchors)
    worlds = [dict(sourceName=row['sourceName'], stableId=row['stableId'],
                   events=source.json('data/maps/' + row['sourceName'] + '/map.json')) for row in maps]
    return source, worlds, scripts


def battle_occurrences(source, scripts, *, ruby=False):
    result = []
    for block in scripts['blocks']:
        for row in block['lines']:
            code = row['text'].split('@')[0].strip()
            if not code.startswith('trainerbattle_'):
                continue
            opcode, arguments = code.split(None, 1)
            arguments = [value.strip() for value in arguments.split(',')]
            result.append(dict(trainer=arguments[0], opcode=opcode, arguments=arguments,
                script=block['label'], sourceRef=source.ref(block['sourceRef']['path'], block['label'], row['line']),
                rematch='rematch' in opcode, **({'admitted': False} if ruby else {})))
    return result


def first_sevii_items(source, scripts, worlds):
    blocks = {block['label']: block for block in scripts['blocks']}
    pickups, hidden = [], []
    for world in worlds:
        events = world['events']
        for index, event in enumerate(events['object_events'], 1):
            if event['graphics_id'] == 'OBJ_EVENT_GFX_ITEM_BALL':
                pickups.append(dict(map=events['id'], localId=index, event=event, script=blocks[event['script']]))
        hidden.extend(dict(map=events['id'], event=event) for event in events['bg_events'] if 'item' in event)
    shops = [dict(label=label, sourceRef=blocks[label]['sourceRef'],
                  items=re.findall(r'\bITEM_(?!NONE\b)\w+', '\n'.join(row['text'] for row in blocks[label]['lines'])))
             for label in ('TwoIsland_Items_ShopInitial', 'TwoIsland_Items_ShopExpanded1', 'ThreeIsland_Mart_Items')]
    symbols = {'ITEM_METEORITE', 'ITEM_TRI_PASS', 'ITEM_TOWN_MAP', 'ITEM_IAPAPA_BERRY', 'ITEM_MOON_STONE', 'ITEM_FULL_RESTORE'}
    for pickup in pickups:
        symbols.update(re.findall(r'\bITEM_\w+', '\n'.join(row['text'] for row in pickup['script']['lines'])))
    symbols.update(row['event']['item'] for row in hidden)
    symbols.update(item for shop in shops for item in shop['items'])
    symbols.discard('ITEM_NONE')
    items = [item for item in source.json('src/data/items.json')['items'] if item['itemId'] in symbols]
    if {item['itemId'] for item in items} != symbols:
        raise ValueError('Missing First Sevii item declarations')
    return dict(itemDeclarations=items, shopStocks=shops, itemPickups=pickups, hiddenItems=hidden)


def first_sevii_encounters(source, worlds):
    symbols = {world['events']['id'] for world in worlds}
    encounters = [entry for group in source.json('src/data/wild_encounters.json')['wild_encounter_groups']
                  for entry in group['encounters'] if entry['map'] in symbols and entry['base_label'].endswith('_FireRed')]
    encounter_gates(source)
    return dict(wildEncounters=encounters, scriptedHypno=dict(species='SPECIES_HYPNO', level=30,
        resolvedMoves=natural_moves(source.text, 'SPECIES_HYPNO', 30),
        sourceRef=source.ref('data/maps/ThreeIsland_BerryForest/scripts.inc', 'ThreeIsland_BerryForest_EventScript_Lostelle', 10)))


def encounter_gates(source):
    path = 'data/maps/ThreeIsland_DunsparceTunnel/scripts.inc'
    text = source.text(path)
    for instruction in ('specialvar VAR_RESULT, IsNationalPokedexEnabled', 'copyvar VAR_0x8008, VAR_RESULT',
                        'call_if_eq VAR_0x8008, TRUE, ThreeIsland_DunsparceTunnel_EventScript_SetLayoutDugOut',
                        'setmaplayoutindex LAYOUT_THREE_ISLAND_DUNSPARCE_TUNNEL_DUG_OUT'):
        if instruction not in text:
            raise ValueError('Dunsparce National Dex layout gate differs')
    return [dict(map='MAP_THREE_ISLAND_PORT', species='SPECIES_DUNSPARCE', gate='IsNationalPokedexEnabled')]


def item_definition(row, constants):
    """Project an item from the selected raw tree, with no project-cache fallback."""
    from item_catalog import ItemDefinition
    symbol = row['itemId']
    return ItemDefinition(dict(id=dict(value='pret:' + symbol), sourceItemSymbol=symbol,
        sourceItemId=constants[symbol], name=row['english'], price=row['price'], importance=row['importance'],
        registrability=row['registrability'], pocket=row['pocket'], type=str(row['type']),
        holdEffect=row['holdEffect'], holdEffectParam=row['holdEffectParam'], fieldUseFunction=row['fieldUseFunc'],
        battleUsage=row['battleUsage'], battleUseFunction=row['battleUseFunc'],
        secondaryId=constants[row['secondaryId']] if isinstance(row['secondaryId'], str) else row['secondaryId'],
        canToss=row['importance'] == 0, canSell=row['price'] != 0, canGive=row['importance'] == 0))
