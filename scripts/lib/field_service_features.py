"""Actual field-service business conversion, independent of regional map stages."""
import json
import re
from map_source import source_directory
from trainer_import import selected_trainers, convert_battles
from field_item_services import service_script_graph


def need(value, message):
    if not value: raise ValueError(message)

TRADE_MAPS = ['Route2_House', 'CeruleanCity_House3', 'UndergroundPath_NorthEntrance',
              'VermilionCity_House2', 'Route11_EastEntrance_2F', 'Route18_EastEntrance_2F',
              'CinnabarIsland_PokemonLab_Lounge', 'CinnabarIsland_PokemonLab_ExperimentRoom']
TM_MAPS = ['Route12_NorthEntrance_2F', 'SaffronCity_MrPsychicsHouse', 'FiveIsland_MemorialPillar']

def firered(text):
    """This data table uses only the explicit two-version conditional form."""
    active, depth, result = True, 0, []
    for line in text.splitlines():
        token = line.strip()
        if token == '#if defined(FIRERED)': need(depth == 0, 'Nested table branch'); depth = 1; active = True
        elif token == '#elif defined(LEAFGREEN)': need(depth == 1, 'Unexpected table branch'); active = False
        elif token == '#endif': need(depth == 1, 'Unbalanced table branch'); depth = 0; active = True
        else:
            need(not token.startswith('#'), 'Unrecognized source conditional: ' + token)
            result.append(line if active else '')
            continue
        result.append('')
    need(depth == 0, 'Unclosed table branch')
    return '\n'.join(result)

def numeric(src, path):
    return {m[1]: int(m[2], 0) for m in re.finditer(r'^#define\s+(\w+)\s+(0x[\da-fA-F]+|\d+)(?:\s|$)', src.text(path), re.M)}

def trade_offers(src):
    path = 'src/data/ingame_trades.h'; original = src.text(path); selected = firered(original)
    ids = numeric(src, 'include/constants/trade.h')
    bindings = {}
    for name in TRADE_MAPS:
        script_path = f'data/maps/{name}/scripts.inc'; script = src.text(script_path)
        events = src.json(f'data/maps/{name}/map.json')
        for block in re.finditer(r'^(\w+)::?\s*\n(.*?)(?=^\w+::?|\Z)', script, re.M | re.S):
            found = re.search(r'setvar VAR_0x8008, (INGAME_TRADE_\w+)', block[2])
            if not found: continue
            actors = [(i, e) for i, e in enumerate(events['object_events'], 1) if e['script'] == block[1]]
            need(len(actors) == 1, 'Trade actor not unique')
            flags = re.findall(r'setflag (FLAG_DID_\w+_TRADE)', block[2])
            need(len(flags) == 1 and f'goto_if_set {flags[0]},' in block[2], 'Trade one-time guard differs')
            local, actor = actors[0]
            bindings[found[1]] = dict(tradeSymbol=found[1], map=('map.cerulean-city-house-3' if name == 'CeruleanCity_House3' else 'map.' + source_directory(name)), localId=local,
                scriptLabel=block[1], completionFlag=flags[0], usesInlineSequence='special DoInGameTradeScene' in block[2])
    offers = []
    required = {'nickname','species','ivs','abilityNum','otId','conditions','personality','heldItem','mailNum','otName','otGender','sheen','requestedSpecies'}
    for match in re.finditer(r'\[(INGAME_TRADE_\w+)\]\s*=\s*\{(.*?)^    \}', selected, re.M | re.S):
        symbol, body = match[1], match[2]
        fields = {m[1]: m[2].strip().rstrip(',') for m in re.finditer(r'^\s*\.(\w+)\s*=\s*(.*?)\s*$', body, re.M)}
        need(set(fields) == required, 'Original trade fields differ: ' + symbol)
        number = lambda key: int(fields[key], 0)
        string = lambda key: re.fullmatch(r'_\("([^"\n]+)"\)', fields[key])[1]
        array = lambda key: [int(n.strip(), 0) for n in fields[key].strip('{}').split(',')]
        ivs, conditions = array('ivs'), array('conditions')
        need(len(ivs) == 6 and len(conditions) == 5 and fields['otGender'] in ('MALE','FEMALE'), 'Trade array/gender differs')
        data = dict(tradeId=ids[symbol], requestedSpecies='pret:' + fields['requestedSpecies'],
            receivedSpecies='pret:' + fields['species'], nickname=string('nickname'),
            personality=number('personality'), ivs=dict(zip(['hp','attack','defense','speed','specialAttack','specialDefense'], ivs)),
            originalTrainerId=number('otId'), originalTrainerName=string('otName'), originalTrainerGender=0 if fields['otGender']=='MALE' else 1,
            abilitySlot=number('abilityNum'), heldItem=fields['heldItem'], mail=number('mailNum'),
            contestConditions=dict(zip(['cool','beauty','cute','smart','tough','sheen'], conditions+[number('sheen')])),
            **bindings[symbol])
        offers.append(data)
    need([o['tradeId'] for o in offers] == list(range(9)) and len(bindings) == 9, 'Nine original trade definitions required')
    return offers

def presentation_text(src):
    path='src/strings.c'; text=src.text(path); rows=[]
    for symbol in ['gText_XWillBeSentToY','gText_ByeByeVar1','gText_XSentOverY','gText_TakeGoodCareOfX',
                   'gText_ChoosePokemon','gText_ChoosePokemonCancel','gFameCheckerText_Cancel']:
        match=re.search(r'const u8 '+symbol+r'\[\] = _\(("[^"\n]*")\);',text)
        need(match is not None,'Original presentation string missing: '+symbol)
        value=json.loads(match[1])
        rows.append(dict(symbol=symbol,text=value))
    return rows

def north_trainers(src):
    read = src.text
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1)
    def identity(symbol):
        return dict(value="pret:" + symbol)
    trainers = selected_trainers(read, ["TRAINER_YOUNG_COUPLE_LEA_JED"])
    battles=[]
    for name in ['Lea','Jed']:
        label='Route16_EventScript_'+name; path='data/scripts/trainers.inc'
        block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        command=re.search(r'^\ttrainerbattle_double ([^\n]+)$',block[1],re.M)
        need(command is not None, 'Original Lea/Jed double command absent')
        args=command[1].split(', '); post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
        need(args[0]=='TRAINER_YOUNG_COUPLE_LEA_JED', 'Original shared trainer ID differs')
        battles.append(dict(trainer=identity(args[0]),mode=4,rivalFlags=0,battleTypeFlags=9,scriptEntry=label,
            introText=args[1],defeatText=args[2],victoryText='',postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle',
            callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None,notEnoughMonsText=args[3]))
    need(len(trainers)==1 and trainers[0]['doubleBattle'] and trainers[0]['sourceTrainerId']==489 and trainers[0]['aiFlags']==1 and
         [(m['species']['value'],m['level'],m['sourceIv']) for m in trainers[0]['party']]==[('pret:SPECIES_RAPIDASH',29,0),('pret:SPECIES_NINETALES',29,0)], 'Original Lea/Jed party differs')
    return dict(trainers=trainers,battles=battles)

def north_passage(worlds, house):
    route, gate=worlds; by_symbol={w['events']['id']:w for w in [route,gate,house]}
    allowed={'MAP_ROUTE16':[0,1,2], 'MAP_ROUTE16_NORTH_ENTRANCE_1F':[0,1], 'MAP_ROUTE16_HOUSE':[0,1,2]}
    for symbol,indexes in allowed.items():
        for index in indexes:
            event=by_symbol[symbol]['events']['warp_events'][index]
            target=by_symbol[event['dest_map']]; destination=int(event['dest_warp_id']); back=target['events']['warp_events'][destination]
            need(back['dest_map']==symbol, 'Nonreciprocal map endpoint')
            # Three original house threshold tiles intentionally share Route16 warp 0;
            # its inbound destination is the middle threshold (house warp 1).
            need(int(back['dest_warp_id'])==index or (symbol=='MAP_ROUTE16_HOUSE' and destination==0 and int(back['dest_warp_id'])==1), 'Unexpected reciprocal warp index')
    cells=[]
    for w in worlds:
        eligible=[c for c in w['cells'] if c['collision']==0 and c['elevation']==3 and
            ((5<=c['y']<=10 and 2<=c['x']<=41) or (c['x'],c['y'])==(41,11) if w is route else 2<=c['y']<=5) and
            c['behaviorName'] in ('MB_NORMAL','MB_TALL_GRASS','MB_WEST_ARROW_WARP','MB_EAST_ARROW_WARP')]
        cells.append(dict(map=w['stableId'],cells=[[c['x'],c['y']] for c in eligible],cellCount=len(eligible)))
    objects=route['events']['object_events']
    need([(objects[i]['x'],objects[i]['y'],objects[i]['script']) for i in [6,7,8]]==
         [(41,11,'EventScript_CutTree'),(30,5,'Route16_EventScript_Lea'),(31,5,'Route16_EventScript_Jed')], 'Original upper path actors differ')
    need(all(c['y']<6 for c in gate['cells'] if [c['x'],c['y']] in cells[1]['cells']), 'Upper gate scope leaked')
    # Collision-derived connectivity checks: no bypass around Cut, and the
    # upstairs/lower cycling passage remains disconnected from the upper room.
    route_cells={tuple(p) for p in cells[0]['cells']}
    def flood(points,start):
        reached={start}; pending=[start]
        while pending:
            x,y=pending.pop()
            for n in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                if n in points and n not in reached:reached.add(n);pending.append(n)
        return reached
    east=flood(route_cells,(41,11)); west=flood(route_cells,(20,6))
    need((27,6) in east and (10,6) in west and east.isdisjoint(west), 'Original separated upper outdoor pockets differ')
    need((41,10) in east and [41,11] in cells[0]['cells'] and (41,12) not in route_cells, 'Cut threshold differs')
    return dict(upperFootAdmissions=[dict(map=r['map'], cells=r['cells']) for r in cells])


def service_bindings(worlds, inventory):
    rows=[]
    for map_name,group,entry,items in [(TM_MAPS[0],'object_events','Lass',['ITEM_TM27']),
        (TM_MAPS[1],'object_events','MrPsychic',['ITEM_TM29']),
        (TM_MAPS[2],'object_events','MemorialMan',['ITEM_TM42']),
        (TM_MAPS[2],'bg_events','Memorial',['ITEM_TM42','ITEM_LEMONADE'])]:
        world=next(w for w in worlds if w['sourceName']==map_name);label=map_name+'_EventScript_'+entry
        matches=[(i,e) for i,e in enumerate(world['events'][group]) if e.get('script')==label]
        need(len(matches)==1,'Source service event binding differs');index,event=matches[0]
        graph=service_script_graph(inventory,label)
        programs,other=graph['programIds'],graph['otherReachableLabels']
        rows.append(dict(id='original-tm-acquisition:'+label,mapId=world['stableId'],scriptId=label,
            sourceLocalId=index+1 if group=='object_events' else None,
            backgroundX=event['x'] if group=='bg_events' else None,backgroundY=event['y'] if group=='bg_events' else None,
            eventGroup=group,eventIndex=index,sourceEvent=event,sourceRef=world['sourceRef'],
            programIds=programs,itemSymbols=items,otherReachableLabels=other))
    return rows

def memorial_trainers(src):
    document = convert_battles(dict(trainers=['BIRD_KEEPER_MILO', 'BIRD_KEEPER_CHAZ', 'BIRD_KEEPER_HAROLD'],
        scripts=['data/scripts/trainers.inc']), src.data)
    document.pop('schemaVersion')
    return document


def cape_encounters(src):
    group = next(g for g in src.json('src/data/wild_encounters.json')['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    rows = [e for e in group['encounters'] if e['map'] == 'MAP_TWO_ISLAND_CAPE_BRINK' and e['base_label'].endswith('_FireRed')]
    need(len(rows) == 1, 'Cape Brink encounter table differs')
    hidden = src.json('data/maps/TwoIsland_CapeBrink/map.json')['bg_events']
    symbols = {e['item'] for e in hidden}
    need(symbols == {'ITEM_PP_MAX', 'ITEM_RARE_CANDY'}, 'Cape Brink hidden items differ')
    return dict(fields=group['fields'], encounters=rows, hiddenItems=hidden,
        wildSpecies=sorted({m['species'] for k in ('land_mons','water_mons','fishing_mons') for m in rows[0][k]['mons']}),
        sourceRef=dict(symbol=rows[0]['base_label']),
        itemDeclarations=[i for i in src.json('src/data/items.json')['items'] if i['itemId'] in symbols])
