"""Source-derived selphy features; no map/pixel or inventory publication."""
import re
import collections
from map_runtime import require as need
from field_item_services import service_script_graph


def selphy_service(src, worlds, inventory):
    root='FiveIsland_ResortGorgeous_House_EventScript_Selphy'
    graph=service_script_graph(inventory,root)
    text=src.text('src/field_specials.c')
    reward_body=re.search(r'static const u16 sResortGorgeousDeluxeRewards\[\] = \{(.*?)\};',text,re.S)[1]
    items=['ITEM_LUXURY_BALL']+re.findall(r'ITEM_\w+',reward_body)
    need(items==['ITEM_LUXURY_BALL','ITEM_BIG_PEARL','ITEM_PEARL','ITEM_STARDUST','ITEM_STAR_PIECE','ITEM_NUGGET','ITEM_RARE_CANDY'], 'Original reward cohort differs')
    need('return ITEM_LUXURY_BALL;' in text,'Original standard reward differs')
    world=next(w for w in worlds if w['sourceName']=='FiveIsland_ResortGorgeous_House');event=world['events']['object_events'][0]
    need(event['script']==root and event['local_id']=='LOCALID_RESORT_GORGEOUS_HOUSE_SELPHY','Original Selphy actor binding differs')
    return dict(id='lost-cave-region:selphy-request-reward',mapId=world['stableId'],scriptId=root,
        sourceLocalId=1,sourceEvent=event,
        programIds=graph['programIds'],itemSymbols=items)
