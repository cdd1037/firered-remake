#!/usr/bin/env python3
"""Reproduce the regional service-map input inventory from regular blobs of the fixed local source commit."""
from pathlib import Path
import argparse, collections, json, os, re, subprocess
ROOT = Path(__file__).resolve().parents[2]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-regional-service-maps-import-v1'
NAMES = ['CeladonCity_Condominiums_' + x for x in ('1F','2F','3F','Roof','RoofRoom')] + ['CeladonCity_DepartmentStore_' + x for x in ('1F','2F','3F','4F','5F','Roof','Elevator')] + ['CeladonCity_' + x for x in ('GameCorner_PrizeRoom','Hotel','House1','Restaurant')] + ['LavenderTown_House2','Route5_SouthEntrance','Route6_NorthEntrance','Route7_EastEntrance','Route8_WestEntrance']
def directory(name):
    parts=re.sub(r'([a-z])([A-Z])',r'\1-\2',name).replace('_','-').lower()
    parts=re.sub(r'house([12])$',r'house-\1',parts)
    return parts.replace('route5','route-5').replace('route6','route-6').replace('route7','route-7').replace('route8','route-8')
MAPS = [(n,'map.'+directory(n),directory(n)) for n in NAMES]
REUSED_MAPS = MAPS[-4:]
NEW_MAPS = MAPS[:-4]
TILESETS = {'gTileset_Building':'primary/building','gTileset_Condominiums':'secondary/condominiums','gTileset_School':'secondary/school','gTileset_DepartmentStore':'secondary/department_store','gTileset_SilphCo':'secondary/silph_co','gTileset_GameCorner':'secondary/game_corner','gTileset_RestaurantHotel':'secondary/restaurant_hotel','gTileset_GenericBuilding1':'secondary/generic_building_1','gTileset_GenericBuilding2':'secondary/generic_building_2'}

def build(reference):
    env = {k:v for k,v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull, GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')
    def git(*args): return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(reference),*args],env=env)
    tree={}
    for row in git('ls-tree','-rz',COMMIT).split(b'\0'):
        if row:
            fields,name=row.split(b'\t');tree[name.decode()]=tuple(fields.decode().split())
    cache={};roles=collections.defaultdict(set)
    def data(p):
        if p not in cache:
            if tree[p][:2]!=('100644','blob'): raise ValueError('Not a regular source blob: '+p)
            cache[p]=git('cat-file','blob',tree[p][2])
        return cache[p]
    def read(p): return data(p).decode()
    def add(p,role='rule-reference'): data(p);roles[p].add(role)
    for p in ['tileset_rules.mk','src/data/tilesets/headers.h','src/data/tilesets/graphics.h','src/graphics.c','src/event_object_movement.c','src/data/object_events/object_event_graphics_info_pointers.h','src/data/object_events/object_event_graphics_info.h','src/data/object_events/object_event_pic_tables.h','src/data/object_events/object_event_graphics.h','src/data/object_events/object_event_anims.h','src/data/object_events/movement_type_func_tables.h','tools/mapjson/mapjson.cpp','src/battle_setup.c','src/field_control_avatar.c','src/overworld.c','src/scrcmd.c','src/field_player_avatar.c','src/fieldmap.c','include/constants/metatile_behaviors.h','include/constants/flags.h','include/constants/vars.h','include/constants/event_objects.h','include/constants/maps.h','include/constants/map_scripts.h','include/constants/trainers.h','include/constants/opponents.h','include/constants/items.h','include/constants/species.h','include/constants/moves.h','include/constants/songs.h','include/constants/battle.h','include/constants/pokemon.h','include/constants/pokedex.h','include/constants/battle_ai.h','include/constants/sound.h','include/constants/field_effects.h','include/constants/global.h']:
        if p not in tree: raise ValueError('Missing declared rule source: '+p)
        add(p)
    for p in ['include/constants/menu.h','include/constants/event_bg.h','include/constants/event_object_movement.h','src/new_menu_helpers.c','src/field_specials.c','src/naming_screen.c','src/script_pokemon_util.c','src/move_tutor.c','data/scripts/flavor_text.inc','include/constants/heal_locations.h']:
        if p in tree: add(p,'service-rule-reference')
    add('data/layouts/layouts.json','map-layout-registry')
    layouts={x['id']:x for x in json.loads(read('data/layouts/layouts.json'))['layouts'] if x.get('id')}
    names=[x[0] for x in MAPS];maps=[json.loads(read(f'data/maps/{n}/map.json')) for n in names]
    source_symbols={json.loads(read(p))['id']:p for p in tree if p.startswith('data/maps/') and p.endswith('/map.json')}
    for n,m in zip(names,maps):
        add(f'data/maps/{n}/map.json','map-events')
        for k in ['blockdata_filepath','border_filepath']:add(layouts[m['layout']][k],'map-layout')
        for dest in {c['map'] for c in m['connections'] or []}|{w['dest_map'] for w in m['warp_events']}:
            if dest != 'MAP_DYNAMIC' and dest not in {m['id'] for m in maps}: add(source_symbols[dest],'external-boundary')
    for base in TILESETS.values():
        for p in tree:
            if p.startswith('data/tilesets/'+base+'/') and Path(p).suffix in ['.png','.pal','.bin']:add(p,'map-graphics')
    blocks={};aliases={}
    paths=[p for p in tree if p.endswith('.inc') and (p.startswith(('data/scripts/','data/text/')) or any(p.startswith(f'data/maps/{n}/') for n in names))]+['data/event_scripts.s']
    for p in paths:
        current=None
        for line in read(p).splitlines():
            label=re.fullmatch(r'(\w+)::?',line.split('@')[0].strip())
            if label:
                current=label[1]
                if current in blocks:raise ValueError('Duplicate label '+current)
                blocks[current]=[p,[]]
            elif current:blocks[current][1].append(line)
            alias=re.fullmatch(r'\s*\.(?:equ|set)\s+(\w+),\s*(\w+)\s*',line)
            if alias:aliases[alias[1]]=alias[2]
    roots={b for b,(p,_) in blocks.items() if any(p.startswith(f'data/maps/{n}/') for n in names)}
    for m in maps:
        for kind in ['object_events','coord_events','bg_events']:roots.update(e['script'] for e in m[kind] if e.get('script') and e['script'] not in ('0','0x0','NULL'))
    roots.update(['Std_MsgboxNPC','Std_MsgboxDefault','Std_MsgboxSign','Std_MsgboxYesNo','Std_ReceivedItem','Std_PutItemAway'])
    pending=list(roots);selected=set()
    while pending:
        label=pending.pop()
        if label in selected:continue
        if label not in blocks:raise ValueError('Missing root '+label)
        selected.add(label);p,lines=blocks[label];add(p,'script-label-closure')
        for token in re.findall(r'\b[A-Za-z_]\w*\b','\n'.join(x.split('@')[0] for x in lines)):
            token=aliases.get(token,token)
            if token in blocks and token not in selected:pending.append(token)
    r='src/data/object_events/'
    for gfx in sorted(({o['graphics_id'] for m in maps for o in m['object_events']} - {'0'})|{'OBJ_EVENT_GFX_RED_NORMAL'}):
        symbol=re.search(r'\['+gfx+r'\]\s*=\s*&(\w+)',read(r+'object_event_graphics_info_pointers.h'))[1]
        body=re.search(r'\b'+symbol+r' = \{(.*?)\n\};',read(r+'object_event_graphics_info.h'),re.S)[1]
        fields=dict(re.findall(r'\.(\w+)\s*=\s*([^,\n]+)',body))
        row=re.search(r'\b'+fields['images']+r'\[\] = \{(.*?)\n\};',read(r+'object_event_pic_tables.h'),re.S)[1]
        for pic in re.findall(r'overworld_frame\((\w+),',row):add(re.search(r'\b'+pic+r'\[\] = INCBIN_U16\("(.*?)"\)',read(r+'object_event_graphics.h'))[1].replace('.4bpp','.png'),'npc-graphics')
        pal=re.search(r'\{\s*(\w+)\s*,\s*'+fields['paletteTag']+r'\s*\}',read('src/event_object_movement.c'))[1]
        add(re.search(r'\b'+pal+r'\[\] = INCBIN_U16\("(.*?)"\)',read(r+'object_event_graphics.h'))[1].replace('.gbapal','.pal'),'npc-palette')
    entries=[]
    for p,rs in sorted(roles.items()):
        entries.append(dict(path=p,roles=sorted(rs)))
    lock=dict(schemaVersion=1,repository='pret/pokefirered',commit=COMMIT,profile=PROFILE,gameVersion='FIRERED',revision=0,completeGame=False,maps=names,files=entries)
    return (json.dumps(lock,indent=2)+'\n').encode()

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path);p.add_argument('--write',action='store_true');a=p.parse_args()
    raw=build(a.reference);target=ROOT/'scripts/recipes/config/regional-service-maps.json'
    if a.write:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
    elif target.read_bytes()!=raw:raise ValueError('Fixed source lock does not reproduce')
    print(json.dumps(dict(inputCount=len(json.loads(raw)['files']))))
