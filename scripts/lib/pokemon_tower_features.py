"""Compile Tower gameplay contracts directly from retained pret scripts and C.

Generated files are outputs only. Map topology is owned by the current world.
"""
import re
from map_runtime import require as need
from source_features import feature_worlds


def block(text, label):
    match = re.search(r'^' + re.escape(label) + r'::\s*\n(.*?)(?=^\w+::|\Z)', text, re.M | re.S)
    need(match, 'Missing Tower script ' + label)
    return match[1]


def function(text, name):
    match = re.search(r'\b' + name + r'\([^;\n]*\)\s*\n\{(.*?)^\}', text, re.M | re.S)
    need(match, 'Missing Tower native function ' + name)
    return match[1]


def compile_tower(src, generated):
    names = [f'PokemonTower_{i}F' for i in range(1, 8)]
    events = {i: src.json(f'data/maps/{name}/map.json') for i, name in enumerate(names, 1)}
    scripts = {i: src.text(f'data/maps/{name}/scripts.inc') for i, name in enumerate(names, 1)}
    house = 'LavenderTown_VolunteerPokemonHouse'
    house_events = src.json(f'data/maps/{house}/map.json')
    house_script = src.text(f'data/maps/{house}/scripts.inc')
    # Source references retain only fields consumed by typed SourceRef construction.
    def ref(path, symbol):
        src.read(path)
        return dict(kind='pret', path=path, symbol=symbol)
    entry = next(e['script'] for e in events[5]['coord_events'] if 'special HealPlayerParty' in block(scripts[5], e['script']))
    entry_body = block(scripts[5], entry)
    variable = re.search(r'setvar (VAR_\w+), 1', entry_body)[1]
    exits = {e['script'] for e in events[5]['coord_events'] if e['script'] != entry}
    need(len(exits) == 1, 'Ambiguous purified-area exit')
    exit_label = exits.pop()
    need(f'setvar {variable}, 0' in block(scripts[5], exit_label), 'Purified-area reset differs')
    border = [e for e in events[5]['coord_events'] if e['script'] == entry]
    cells = [[x,y] for y in range(min(e['y'] for e in border), max(e['y'] for e in border)+1)
             for x in range(min(e['x'] for e in border), max(e['x'] for e in border)+1)]
    world = feature_worlds(src, ['PokemonTower_5F'])[0]
    need(all(world['cells'][y*world['width']+x]['encounterType'] == 0 for x,y in cells), 'Purified area allows encounters')
    healing = function(src.text('src/script_pokemon_util.c'), 'HealPlayerParty')
    need(all(token in healing for token in ['MON_DATA_MAX_HP','MON_DATA_HP','MON_DATA_PP1','MON_DATA_STATUS','CalculatePPWithBonus']), 'Unsupported party healing implementation')
    need('HealLocation' not in healing, 'Party healing now changes return location')
    native = src.text('src/battle_setup.c')
    create = function(native, 'StartMarowakBattle')
    mon = re.search(r'CreateMonWithGenderNatureLetter\(gEnemyParty, (SPECIES_\w+), (\d+), (\d+), MON_(\w+), (NATURE_\w+), 0\)', create)
    need(mon, 'Unsupported scripted Marowak creation')
    species, level, iv, gender, nature = mon.groups(); level = int(level)
    learnset = re.search(r'sMarowakLevelUpLearnset\[\] = \{(.*?)\};', src.text('src/data/pokemon/level_up_learnsets.h'), re.S)[1]
    moves = []
    for learned, move in re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)', learnset):
        if int(learned) <= level and move not in moves:
            moves.append(move)
            moves = moves[-4:]
    pokemon = src.text('src/pokemon.c')
    initialization = function(pokemon, 'CreateBoxMon')
    need('ZeroBoxMonData(boxMon)' in initialization and 'MON_DATA_HELD_ITEM' not in initialization, 'Scripted held item initialization differs')
    need('DeleteFirstMoveAndGiveMoveToBoxMon' in function(pokemon, 'GiveBoxMonInitialMoveset'), 'Initial move ordering differs')
    end = function(native, 'CB2_EndMarowakBattle')
    outcome = re.search(r'if \(gBattleOutcome == (B_OUTCOME_\w+)\)\s*gSpecialVar_Result = FALSE', end)[1]
    gate_var, gate_value = re.search(r'setvar (VAR_MAP_SCENE_\w+), (\d+)', scripts[6]).groups()
    rescue = block(scripts[7], 'PokemonTower_7F_EventScript_MrFuji')
    macro = src.text('asm/macros/event.inc')
    need(re.search(r'\.ifb \\c[^\n]*\n\s*\.byte WARP_ID_NONE', macro), 'Coordinate-warp encoding differs')
    warp_none = int(re.search(r'#define WARP_ID_NONE \((-?\d+)\)', src.text('include/constants/maps.h'))[1]) & 255
    destination, x, y = re.search(r'warp (MAP_\w+), (\d+), (\d+)', rescue).groups()
    clear = re.findall(r'clearflag (FLAG_\w+)', rescue)
    fuji_actor = next(e for e in house_events['object_events'] if e['script'] == house + '_EventScript_MrFuji')
    reward = block(house_script, house + '_EventScript_MrFuji')
    item, quantity = re.search(r'giveitem_msg \w+, (ITEM_\w+), (\d+)', reward).groups()
    reward_flag = re.search(r'setflag (FLAG_\w+)', reward)[1]
    need(reward.index('checkitemspace ' + item) < reward.index('setflag ' + reward_flag), 'Flute reward capacity ordering differs')
    returns = {(e['dest_map'], e['dest_warp_id']) for e in house_events['warp_events']}
    need(len(returns) == 1, 'House exterior return is ambiguous')
    return_map, return_warp = returns.pop()
    rival = []
    for starter, label in re.findall(r'call_if_eq VAR_STARTER_MON, (\d+), (\w+)', scripts[2]):
        trainer = re.search(r'trainerbattle_no_intro (TRAINER_\w+)', block(scripts[2], label))[1]
        rival.append(dict(starterVar=int(starter), trainer=trainer))
    pickups = []
    item_path = 'data/scripts/item_ball_scripts.inc'; item_scripts = src.text(item_path)
    for floor, event in events.items():
        for i, actor in enumerate(event['object_events'], 1):
            if actor['graphics_id'] != 'OBJ_EVENT_GFX_ITEM_BALL': continue
            pickup = re.search(r'finditem (ITEM_\w+)(?:, (\d+))?', block(item_scripts, actor['script']))
            need(pickup, 'Unsupported Tower item ball')
            pickups.append(dict(kind='visible', map=event['id'], localId=i, actor=actor,
                item=pickup[1], quantity=int(pickup[2] or 1), scriptSource=ref(item_path,actor['script'])))
        for i, hidden in enumerate(event['bg_events']):
            if hidden['type'] != 'hidden_item': continue
            pickups.append(dict(kind='hidden',map=event['id'],eventIndex=i,event=hidden,item=hidden['item'],
                quantity=hidden['quantity'],sourceRef=ref(f'data/maps/PokemonTower_{floor}F/map.json',event['id'])))
    fog_native = src.text('src/field_weather_effects.c')
    fog_functions = ''.join(function(fog_native, n) for n in ['FogHorizontal_InitVars','FogHorizontal_Main'])
    need('ITEM_SILPH_SCOPE' not in fog_functions, 'Fog now depends on scope')
    escape = function(src.text('src/overworld.c'), 'UpdateEscapeWarp')
    need('IsMapTypeOutdoors(currMapType)' in escape and 'IsMapTypeOutdoors(destMapType) != TRUE' in escape, 'Escape anchor semantics differ')
    contract = dict(purifiedGround=dict(temporaryVariable=variable,entryScript=dict(label=entry),exitScript=dict(label=exit_label)),
        fog=dict(floors=[i for i,e in events.items() if e['weather']=='WEATHER_FOG_HORIZONTAL'],weather='WEATHER_FOG_HORIZONTAL',dependsOnSilphScope=False,sourceRef=ref('src/field_weather_effects.c','FogHorizontal_Main')),
        marowakGate=dict(variable=gate_var,successValue=int(gate_value),successOutcome=outcome),
        fujiRescue=dict(setFlags=re.findall(r'setflag (FLAG_\w+)',rescue),clearFlags=clear,initialHidden=fuji_actor['flag']),
        dynamicExit=dict(hasMapDynamicWarp=any(e['dest_map']=='MAP_DYNAMIC' for m in events.values() for e in m['warp_events']),
            rescueWarp=dict(map=destination,warpId=warp_none,x=int(x),y=int(y)),houseExteriorReturn=dict(map=return_map,warpId=int(return_warp))),rivalBranches=rival)
    encounters = [dict(map=e['map'],land_mons=e['land_mons']) for group in src.json('src/data/wild_encounters.json')['wild_encounter_groups'] for e in group['encounters'] if e['map'] in {m['id'] for m in events.values()} and e['base_label'].endswith('_FireRed') and 'land_mons' in e]
    dependencies = dict(wildEncounters=encounters,marowak=dict(species=species,level=level,fixedIv=int(iv),gender=gender.lower(),nature=nature,heldItem='ITEM_NONE',moves=moves),
        reward=dict(item=item,quantity=int(quantity),flag=reward_flag,checkCapacityBeforeFlag=True),pickups=pickups)
    mechanics = dict(healing=dict(purifiedCoordinates=cells,fullPartyHpPpStatus=True,ppBonusesApplied=True,updatesLastHealLocation=False,
        sources=[ref('src/script_pokemon_util.c','HealPlayerParty'),ref('data/maps/PokemonTower_5F/scripts.inc',entry)]),escape=dict(fujiCoordinateWarpMustNotReplaceEscapeAnchor=True))
    directory = generated / 'maps/regions/pokemon-tower/presentation'
    directory.mkdir(parents=True,exist_ok=True)
    (directory/'fog-horizontal-source.png').write_bytes(src.read('graphics/weather/fog_horizontal.png'))
    return {'maps/regions/pokemon-tower/'+name:doc for name,doc in [('pokemon-tower.json',contract),('dependencies.json',dependencies),('mechanics.json',mechanics)]}
