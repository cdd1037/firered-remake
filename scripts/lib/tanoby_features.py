"""Seven Island puzzle, renewable pickups, clone actors and travel bindings."""
import re
from map_runtime import require


def field_contract(src, worlds, constants, scripts, links):
    maps={w['events']['id']:w for w in worlds}
    blocks={b['label']:b for b in scripts['blocks']}
    renewable=src.text('src/renewable_hidden_items.c')
    flags={}
    for match in re.finditer(r'    \{\n        \.mapGroup = MAP_GROUP\((MAP_\w+)\),(.*?)\n    \},',renewable,re.S):
        if match[1] not in maps:continue
        for rarity in ('rare','uncommon','common'):
            group=re.search(r'\.'+rarity+r' = \{(.*?)\}',match[2],re.S)
            require(group,'Missing renewable rarity')
            for i,flag in enumerate(re.findall(r'HIDDEN_ID\((FLAG_\w+)\)',group[1])):
                flags[flag]=(match[1],rarity,i)
    visible=[];hidden=[];clones=[]
    for w in worlds:
        symbol=w['events']['id']
        for i,e in enumerate(w['events']['object_events'],1):
            if e.get('type')=='clone':
                targets=[j for j,t in enumerate(maps[e['target_map']]['events']['object_events'],1) if t.get('local_id')==e['target_local_id']]
                require(len(targets)==1,'Clone target differs')
                clones.append(dict(map=symbol,localId=i,declaration={k:e[k] for k in ('x','y','target_local_id')},targetMap=e['target_map'],targetLocalId=targets[0]))
            if e['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
            tokens=re.findall(r'^\s*finditem (ITEM_\w+)(?:,\s*(\d+))?', '\n'.join(r['text'] for r in blocks[e['script']]['lines']),re.M)
            require(len(tokens)==1,'Visible pickup script differs')
            visible.append(dict(map=symbol,localId=i,event=e,item=tokens[0][0],quantity=int(tokens[0][1] or '1'),renewable=False))
        for e in w['events']['bg_events']:
            if 'item' not in e:continue
            row=dict(map=symbol,event=e,item=e['item'],quantity=e['quantity'],renewable=e['flag'] in flags)
            if row['renewable']:
                group,rarity,index=flags[e['flag']];require(group==symbol,'Renewable flag bound to another map')
                row.update(renewalRarity=rarity,renewalIndex=index)
            hidden.append(row)
    key='MAP_SEVEN_ISLAND_SEVAULT_CANYON_TANOBY_KEY';w=maps[key]
    ordered=sorted(w['events']['coord_events'],key=lambda e:int(e['var'].rsplit('_',1)[1]))
    prefix='SevenIsland_SevaultCanyon_TanobyKey_'
    def text(label):return '\n'.join(r['text'] for r in blocks[prefix+label]['lines'])
    switches=[];thresholds=set();counter_names=set()
    for i,e in enumerate(ordered,1):
        body='\n'.join(r['text'] for r in blocks[e['script']]['lines'])
        pressed=re.search(r'\bsetvar '+e['var']+r', (\d+)',body)
        threshold=re.search(r'goto_if_eq (\w+), (\d+), '+prefix+r'EventScript_PuzzleSolved',body)
        require(pressed and threshold,'Missing switch state/threshold')
        thresholds.add(int(threshold[2]));counter_names.add(scripts['aliases'].get(threshold[1],threshold[1]))
        require(w['cells'][e['y']*w['width']+e['x']]['behaviorId']==0x20,'Strength switch tile differs')
        switches.append(dict(index=i,event={k:e[k] for k in ('x','y','elevation','script')},temporaryVariable=e['var'],pressedValue=int(pressed[1])))
    solved={name:(int(x),int(y)) for name,x,y in re.findall(r'setobjectxyperm (\w+), (\d+), (\d+)',text('EventScript_MoveBouldersToSolvedPos'))}
    boulders=[dict(localId=i,event={k:e[k] for k in ('local_id','x','y','elevation')},solvedX=solved[e['local_id']][0],solvedY=solved[e['local_id']][1]) for i,e in enumerate(w['events']['object_events'],1)]
    require(len(boulders)==len(switches) and thresholds=={len(switches)} and len(counter_names)==1,'Strength puzzle objects/threshold differ')
    flag=re.search(r'\bsetflag (FLAG_\w+)',text('EventScript_PuzzleSolved'))
    require(flag,'Missing solved flag')
    puzzle=dict(map=key,switches=switches,boulders=boulders,solvedFlag=flag[1],countVariable=next(iter(counter_names)),requiredCount=next(iter(thresholds)),strengthBehaviorId=0x20)
    heal=next(h for h in src.json('src/data/heal_locations.json')['heal_locations'] if h['id']=='HEAL_LOCATION_SEVEN_ISLAND')
    arrival=re.search(r'\[SEAGALLOP_SEVEN_ISLAND\]\s*=\s*\{MAP\((MAP_\w+)\),\s*(0x[0-9a-fA-F]+),\s*(0x[0-9a-fA-F]+)\}',src.text('src/seagallop.c'))
    require(arrival,'Missing Seven Island ferry landing')
    return dict(schemaVersion=1,itemPickups=visible,hiddenItems=hidden,clones=clones,tanobyKey=puzzle,
        ferryLanding=dict(map=arrival[1],x=int(arrival[2],16),y=int(arrival[3],16)),
        healLocation=dict(declaration=heal,sourceId=constants['values'][heal['id']]['value'],blackoutX=7,blackoutY=4))
