"""Source-derived Silph doors, warp pads, elevator destinations and pickups."""
import re

def compile_silph(src,worlds,doc,constants=None):
    blocks={b['label']:b for b in doc['blocks']}
    def body(label):return '\n'.join(r['text'] for r in blocks[label]['lines'])
    def script(label):return dict(label=label)
    mtpath='include/constants/metatile_labels.h';mt=src.text(mtpath)
    values={n:int(v,16) for n,v in re.findall(r'^#define (METATILE_SilphCo_\w+)\s+(0x[0-9a-fA-F]+)',mt,re.M)}
    doors=[]
    for label in sorted(blocks):
        m=re.fullmatch(r'SilphCo_(\d+F)_EventScript_Door(\d*)',label)
        if not m:continue
        close='EventScript_Close'+m[1]+'Door'+m[2];opened='EventScript_Open'+m[1]+'Door'+m[2]
        def patches(key):
            rows=re.findall(r'^\s*setmetatile (\d+), (\d+), (METATILE_\w+), (\d+)$',body(key),re.M)
            if not rows:raise ValueError('Empty door patches')
            return [dict(x=int(x),y=int(y),metatileId=values[s],collision=int(c)) for x,y,s,c in rows]
        doors.append(dict(map='map.silph-co-'+m[1].lower(),script=label,doorIndex=int(re.search(r'setvar VAR_TEMP_1, (\d+)',body(label))[1]),flag=re.search(r'setvar VAR_0x8004, (FLAG_\w+)',body(label))[1],closedPatches=patches(close),openPatches=patches(opened),closedSource=script(close),openSource=script(opened)))
    if len(doors)!=20:raise ValueError('Expected20 exact source doors')
    pads=[]
    for w in worlds:
        for i,e in enumerate(w['events']['warp_events']):
            cell=w['cells'][e['y']*w['width']+e['x']]
            if cell['behaviorName']=='MB_REGULAR_WARP':pads.append(dict(map=w['stableId'],warpIndex=i,x=e['x'],y=e['y'],elevation=e['elevation'],destinationMap=e['dest_map'],destinationWarp=int(e['dest_warp_id'])))
    if len(pads)!=34:raise ValueError('Expected34 source pad endpoints: '+str(len(pads)))
    floors=[]
    for floor in range(1,12):
        label=f'SilphCo_Elevator_EventScript_To{floor}F';b=body(label);m=re.search(r'setdynamicwarp (MAP_\w+), (\d+), (\d+), (\d+)',b)
        floors.append(dict(floor=floor,menuIndex=11-floor,sourceFloorId=int(re.search(r'setvar VAR_0x8006, (\d+)',b)[1]),destinationMap=m[1],destinationWarp=int(m[2]),x=int(m[3]),y=int(m[4])))
    pickups=[]
    for w in worlds:
        for i,o in enumerate(w['events']['object_events'],1):
            if o['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
            label=o['script'];item=re.search(r'finditem (ITEM_\w+)',body(label))
            if not item:raise ValueError('Missing exact source pickup')
            pickups.append(dict(map=w['stableId'],localId=i,item=item[1],flag=o['flag'],x=o['x'],y=o['y'],source=script(label)))
    return dict(schemaVersion=1,doors=doors,pads=pads,elevatorFloors=floors,pickups=pickups,hiddenItems=[dict(map=w['stableId'],declaration=e) for w in worlds for e in w['events']['bg_events'] if e['type']=='hidden_item'])
