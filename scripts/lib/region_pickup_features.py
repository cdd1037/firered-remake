"""Direct source pickups and item facts for coastal and optional dungeon maps."""
import re
from source_features import script_blocks, feature_worlds
from world_selection import load_selection


def compile_pickups(src, selection, regions=('seafoam','cerulean-cave','power-plant','cinnabar')):
    selected = load_selection(selection)
    documents = {}
    for region in regions:
        names = [r['sourceName'] for r in selected if r['path'].startswith('maps/regions/'+region+'/') and 'variant' not in r]
        worlds = feature_worlds(src,names)
        scripts = script_blocks(src,names)
        blocks = {b['label']:b for b in scripts['blocks']}
        pickups,hidden = [],[]
        for world in worlds:
            for i,event in enumerate(world['events']['object_events'],1):
                if event['graphics_id'] != 'OBJ_EVENT_GFX_ITEM_BALL': continue
                block = blocks[event['script']]
                code = '\n'.join(r['text'] for r in block['lines'])
                match = re.search(r'\bfinditem (ITEM_\w+)(?:, (\d+))?',code)
                if not match:
                    if re.search(r'\bsetwildbattle SPECIES_',code): continue
                    raise ValueError('Missing pickup command: '+event['script'])
                row = dict(map=world['events']['id'],localId=i,event=event,item=match[1],quantity=int(match[2] or 1),sourceRef=dict(path=block['sourceRef']['path']))
                if region == 'cinnabar': row = dict(map=world['stableId'],localId=i,event=event,script=dict(label=event['script']))
                pickups.append(row)
            hidden.extend(dict(map=world['stableId'] if region=='cinnabar' else world['events']['id'],event=e) for e in world['events']['bg_events'] if 'item' in e)
        contract = dict(itemPickups=pickups,hiddenItems=hidden)
        if region == 'cinnabar':
            patches = []
            for block in scripts['blocks']:
                changes = []
                for row in block['lines']:
                    m = re.fullmatch(r'\s*setmetatile\s+(\d+),\s*(\d+),\s*(\w+),\s*([01])',row['text'])
                    if m: changes.append(dict(x=int(m[1]),y=int(m[2]),metatile=m[3],collision=int(m[4])))
                if changes: patches.append(dict(label=block['label'],changes=changes))
            falls = []
            for world in worlds:
                for i,event in enumerate(world['events']['warp_events']):
                    if world['cells'][event['y']*world['width']+event['x']]['behaviorName']=='MB_FALL_WARP':
                        falls.append(dict(map=world['stableId'],sourceWarpIndex=i,source=event))
            city = src.json('data/maps/CinnabarIsland/map.json')
            contract.update(tilePatches=patches,fallWarps=falls,mansionSwitchFlag='FLAG_POKEMON_MANSION_SWITCH_STATE',
                            secretKeyPickup=next(p for p in pickups if p['event']['script'].endswith('ItemSecretKey')),
                            gymDoorTrigger=next(e for e in city['coord_events'] if e['script']=='CinnabarIsland_EventScript_GymDoorLocked'))
        prefix='maps/regions/'+region+'/'
        documents[prefix+'contracts.json']=contract
        if region != 'cinnabar':
            symbols={p['item'] for p in pickups}|{p['event']['item'] for p in hidden}
            documents[prefix+'items.json']=dict(declarations=[{k:r[k] for k in ('itemId','pocket','price')} for r in src.json('src/data/items.json')['items'] if r['itemId'] in symbols])
    return documents
