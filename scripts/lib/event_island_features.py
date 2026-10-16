"""Source-derived event island features; no map/pixel or inventory publication."""
import re
from map_runtime import require as need


def field_contract(src, worlds, const, links, npcs):
    maps={w['events']['id']:w for w in worlds}
    need(len(worlds)==24 and sum(len(w['events']['object_events']) for w in worlds)==8, 'Map/object scope differs')
    need(len(links['warps'])==44 and not links['connections'] and all(r['destinationInSlice'] and r['reciprocalIndex'] for r in links['warps']), 'Event island topology differs')
    coord=[dict(map=w['events']['id'],event=e,sourceRef={k:w['sourceRef'][k] for k in ('path','symbol')}) for w in worlds for e in w['events']['coord_events']]
    need(len(coord)==1 and coord[0]['event']==dict(type='trigger',x=9,y=12,elevation=3,var='VAR_TEMP_1',var_value='0',script='NavelRock_Summit_EventScript_HoOh'), 'Ho-Oh coordinate trigger differs')
    hidden=[dict(map=w['events']['id'],event=e,item=e['item'],quantity=e['quantity'],requiresItemfinder=True,sourceRef={k:w['sourceRef'][k] for k in ('path','symbol')}) for w in worlds for e in w['events']['bg_events'] if 'item' in e]
    need(len(hidden)==1 and hidden[0]['event']['underfoot'] and hidden[0]['item']=='ITEM_SACRED_ASH' and (hidden[0]['event']['x'],hidden[0]['event']['y'])==(9,11), 'Sacred Ash source differs')
    encounters=src.json('src/data/wild_encounters.json')
    active=set(maps)
    headers=[e for group in encounters['wild_encounter_groups'] for e in group.get('encounters',[]) if e.get('map') in active]
    need(not headers,'Event maps unexpectedly have normal random encounter headers')
    sea=src.text('src/seagallop.c');landings=[]
    for name in ['NAVEL_ROCK','BIRTH_ISLAND']:
        need(re.search(r'\[SEAGALLOP_'+name+r'\]\s*=\s*\{MAP\(MAP_'+name+r'_HARBOR\),\s+0x08, 0x05\}',sea),'Ferry landing differs')
        landings.append(dict(map='MAP_'+name+'_HARBOR',x=8,y=5,facing='DIR_SOUTH',sourceRef=dict(path='src/seagallop.c',symbol='sSeag')))
    control=src.text('src/field_control_avatar.c');finder=src.text('src/itemfinder.c')
    need('if (GetHiddenItemAttr(bgEvent->bgUnion.hiddenItem, HIDDEN_ITEM_UNDERFOOT) == TRUE)\n            return NULL;' in control,
        'Underfoot item must refuse facing interaction')
    need('GetHiddenItemAttr(events->bgEvents[i].bgUnion.hiddenItem, HIDDEN_ITEM_UNDERFOOT) == TRUE' in finder,
        'Itemfinder underfoot prerequisite differs')
    return dict(schemaVersion=1,
        hiddenItems=hidden,coordinateTriggers=coord,
        ferryLandings=landings,
        mapHeaders=[dict(map=w['events']['id'],sourceRef={k:w['sourceRef'][k] for k in ('path','symbol')},**{k:w['events'][k] for k in ['music','weather','map_type','allow_cycling','allow_escaping','allow_running','requires_flash','show_map_name','floor_number','battle_scene']}) for w in worlds],
        dynamicGraphics=npcs['dynamicGraphics'],
        nativeCamera=dict(sourceId=const['values']['LOCALID_CAMERA']['value']),
        sacredAshBoundary=dict(runtimePickupAdmitted=False))
