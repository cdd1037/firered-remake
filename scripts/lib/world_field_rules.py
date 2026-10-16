"""Compile live field-service data from original sources, independently of map art."""
import re
from map_runtime import require
from source_features import script_blocks, feature_constants, feature_worlds, topology
from wild_encounter_headers import default_version_headers
from world_selection import load_selection

REGIONS = ('sevii-ruby','four-island','five-island','six-island','seven-island',
           'event-island','trainer-tower','lost-cave','safari','cycling-road','silph','saffron-gym','regional-gifts','seafoam','cerulean-cave','power-plant','cinnabar')


def pickups(worlds, scripts):
    blocks = {b['label']:b for b in scripts['blocks']}
    visible, hidden = [], []
    for w in worlds:
        for i,e in enumerate(w['events']['object_events'],1):
            if e['graphics_id'] not in ('OBJ_EVENT_GFX_ITEM_BALL','OBJ_EVENT_GFX_RUBY'): continue
            row=dict(map=w['events']['id'],localId=i,event=e)
            if e['graphics_id']=='OBJ_EVENT_GFX_ITEM_BALL':
                b=blocks[e['script']]
                matches=re.findall(r'^\s*finditem\s+(ITEM_\w+)(?:,\s*(\d+))?\s*$', '\n'.join(r['text'] for r in b['lines']), re.M)
                require(len(matches)==1,'Pickup source instruction differs: '+e['script'])
                row.update(item=matches[0][0],quantity=int(matches[0][1] or 1),sourceRef=b['sourceRef'])
            visible.append(row)
        hidden.extend(dict(map=w['events']['id'],event=e) for e in w['events']['bg_events'] if 'item' in e)
    return dict(itemPickups=visible,hiddenItems=hidden)


def dynamic_graphics(worlds, scripts):
    result=[]
    for w in worlds:
        for i,e in enumerate(w['events']['object_events'],1):
            if not e['graphics_id'].startswith('OBJ_EVENT_GFX_VAR_'): continue
            variable=e['graphics_id'].replace('OBJ_EVENT_GFX_VAR_','VAR_OBJ_GFX_ID_')
            matches=[(b,m[1]) for b in scripts['blocks'] if b['sourceRef']['path']=='data/maps/'+w['sourceName']+'/scripts.inc'
                     for r in b['lines'] if (m:=re.fullmatch(r'\s*setvar '+variable+r',\s*(OBJ_EVENT_GFX_\w+)\s*',r['text']))]
            require(len(matches)==1,'Dynamic actor requires one source selector')
            b,g=matches[0]
            result.append(dict(map=w['events']['id'],localId=i,sourceGraphics=e['graphics_id'],variable=variable,graphicsId=g,script=b['label']))
    return result


def compile_region(src, region, names):
    if region=='cycling-road':
        from cycling_road_features import compile_cycling_road
        return compile_cycling_road(src)
    if region=='saffron-gym':
        from saffron_mart_features import compile_saffron
        return {"contracts.json":compile_saffron(src)}
    worlds=feature_worlds(src,names)
    extra=['EventScript_ResetAllMapFlags'] if region=='four-island' else []
    scripts=script_blocks(src,names,extra)
    constants=feature_constants(src,worlds,scripts)
    documents={}
    if region=='silph':
        from silph_field_features import compile_silph
        return {'contracts.json':compile_silph(src,worlds,scripts,constants)}
    if region in ('safari','trainer-tower'):
        import importlib
        module=importlib.import_module(region.replace('-','_')+'_features')
        contract=getattr(module,'compile_'+region.replace('-','_'))(src,worlds,scripts,constants)
        if region=='safari':documents['encounters.json']=module.compile_safari_encounters(src)
    else:
        links=topology(src,worlds) if region in ('seven-island','event-island') else None
        contract=dict(schemaVersion=1)
        if region in ('sevii-ruby','four-island','five-island','six-island','lost-cave'):
            contract.update(pickups(worlds,scripts))
            if region=='sevii-ruby':
                for row in contract['itemPickups']:row.pop('sourceRef',None)
            if region=='lost-cave':
                for row in contract['itemPickups']:row['sourceRef']={'path':row['sourceRef']['path']}
            if region in ('four-island','five-island','six-island'):
                contract['itemPickups']=[{k:r[k] for k in ('map','localId','event')} for r in contract['itemPickups']]
        if region=='four-island':
            from icefall_features import doll_collection_contract, sticker_milestones, ice_fall_pairs
            contract.update(doll_collection_contract(src,worlds,{b['label']:b for b in scripts['blocks']}))
            contract['stickerMilestones']=sticker_milestones(src)
            contract['iceFallPairs']=ice_fall_pairs(src,worlds)
        elif region=='seven-island':
            from tanoby_features import field_contract
            contract=field_contract(src,worlds,constants,scripts,links)
        elif region=='event-island':
            from event_island_features import field_contract
            contract=field_contract(src,worlds,constants,links,dict(dynamicGraphics=dynamic_graphics(worlds,scripts)))
        elif region=='lost-cave':
            from selphy_features import selphy_service
            anchor=src.json('data/maps/FiveIsland/map.json')
            north=next(c for c in anchor['connections'] if c['map']=='MAP_FIVE_ISLAND_WATER_LABYRINTH')
            contract['retainedAnchor']=dict(connection=north)
            contract['serviceBindings']=[selphy_service(src,worlds,scripts)]
            wanted={p['item'] for p in contract['itemPickups']}|{p['event']['item'] for p in contract['hiddenItems']}
            documents['items.json']=dict(declarations=[{k:r[k] for k in ('itemId','english','pocket','price')} for r in src.json('src/data/items.json')['items'] if r['itemId'] in wanted])
        if region=='six-island':
            documents['encounters.json']=default_version_headers(src.json('src/data/wild_encounters.json')['wild_encounter_groups'], {w['events']['id'] for w in worlds})
    documents['contracts.json']=contract
    return documents


def compile_rules(src, selection, regions=REGIONS):
    selected=load_selection(selection)
    result={}
    for region in regions:
        if region=='regional-gifts':
            from regional_gift_features import compile_gifts
            result['data/services/regional-tea-eevee.json']=compile_gifts(src)
            continue
        if region in ('seafoam','cerulean-cave','power-plant','cinnabar'):
            from region_pickup_features import compile_pickups
            result.update(compile_pickups(src,selection,[region]))
            continue
        names=[r['sourceName'] for r in selected if r['path'].startswith('maps/regions/'+region+'/') and 'variant' not in r]
        require(names,'No selected source maps for field rules: '+region)
        for name,document in compile_region(src,region,names).items():
            result['maps/regions/'+region+'/'+name]=document
    return result
