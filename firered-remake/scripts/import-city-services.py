#!/usr/bin/env python3
"""Authenticated city gift and exchange source; two new maps and a reused Cerulean Bike Shop."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse
import collections
import importlib.util
import json
import pathlib
import re
import shutil
import tempfile
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-city-services-import-v1'
MAPS = [('VermilionCity_PokemonFanClub', 'map.vermilion-city-pokemon-fan-club', 'vermilion-city-pokemon-fan-club'),
        ('VermilionCity_House1', 'map.vermilion-city-house-1', 'vermilion-city-house1')]
SERVICE_MAPS = [name for name, _, _ in MAPS] + ['CeruleanCity_BikeShop']
TILESETS = {'gTileset_Building':'primary/building', 'gTileset_FanClubDaycare':'secondary/fan_club_daycare', 'gTileset_GenericBuilding1':'secondary/generic_building_1'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_city_services_shared_map_import', ROOT / 'scripts/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    # Preserve existing profiles when the caller overrides --raw.
    raw=pathlib.Path(raw)
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if kind=='raw' and (protected.name=='city-services' or (protected.name=='pokefirered' and raw.resolve()==(ROOT/'content/raw/pokefirered/037335f4').resolve())):continue
            if raw.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(raw.resolve()):
                raise ValueError('Existing profile overlap')
    SHARED.materialize(raw,reference_repo,source_package='city-services')

def read_lock(path=None):
    target = path or ROOT / 'scripts/import-config/city-services.json'
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or lock.get('repository') != 'pret/pokefirered' or (lock.get('commit') != COMMIT) or (lock.get('profile') != PROFILE) or (lock.get('gameVersion') != 'FIRERED') or (type(lock.get('revision')) is not int) or (lock['revision'] != 0) or (lock.get('completeGame') is not False) or (lock.get('maps') != [x[0] for x in MAPS]) or (not lock.get('files'))):
        raise ValueError('Invalid locked source identity or scope')
    seen = set()
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path']:
            raise ValueError('Unsafe source path')
        if entry['path'] in seen:
            raise ValueError('Duplicate source path')
        seen.add(entry['path'])
        if (not entry['roles']):
            raise ValueError('Malformed source entry')
    # The lock itself is reviewed and fixed; changing source AND its hash does not evade validation.
    return lock

for key in ('ROOT', 'COMMIT', 'TREE',  'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
    setattr(SHARED, key, globals()[key])

def script_inventory(src, worlds):
    blocks, aliases, preambles, local_macros = {}, {}, {}, []
    script_paths = [p for p, e in src.entries.items() if 'script-label-closure' in e['roles'] and p.endswith(('.inc', '.s'))]
    for path in sorted(script_paths):
        current = None
        macro = None
        preambles[path] = []
        for number, line in enumerate(src.text(path).splitlines(), 1):
            definition = re.fullmatch(r'\s*\.macro\s+(\w+)(.*)', line)
            if definition:
                if macro is not None: raise ValueError('Nested regional macro')
                macro = dict(name=definition[1], arguments=definition[2].strip(), lines=[], sourceRef=src.ref(path, definition[1], number))
                local_macros.append(macro)
                current = None
                continue
            if macro is not None:
                if line.strip() == '.endm': macro = None
                else: macro['lines'].append(dict(line=number, text=line))
                continue
            label = re.fullmatch(r'(\w+)::?', line.split('@')[0].strip())
            if label:
                name = label[1]
                if name in blocks:
                    raise ValueError('Duplicate script label: ' + name)
                current = dict(label=name, sourceRef=src.ref(path, name, number), lines=[])
                blocks[name] = current
            elif current is not None:
                current['lines'].append(dict(line=number, text=line))
            else:
                preambles[path].append(dict(line=number, text=line))
            alias = re.fullmatch(r'\s*\.(?:equ|set)\s+(\w+),\s*(\w+)\s*', line)
            if alias:
                aliases[alias[1]] = alias[2]
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name in SERVICE_MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo', 'Std_ReceivedItem', 'Std_PutItemAway'])
    required = {'VermilionCity_PokemonFanClub_EventScript_Chairman', 'VermilionCity_House1_EventScript_FishingGuru', 'CeruleanCity_BikeShop_EventScript_Clerk'}
    if not required <= roots: raise ValueError('Critical city-service entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    selected, pending = set(), sorted(roots)
    references, unresolved = [], []
    commands, specials, movements = collections.defaultdict(list), collections.defaultdict(list), collections.defaultdict(list)
    while pending:
        label = pending.pop()
        if label in selected:
            continue
        selected.add(label)
        block = blocks[label]
        for line in block['lines']:
            text = line['text'].split('@')[0].strip()
            if not text or text.startswith(('.string', '.include', '#')):
                continue
            op = text.split()[0]
            location = dict(label=label, path=block['sourceRef']['path'], line=line['line'])
            commands[op].append(location)
            if op in ('special', 'specialvar'):
                specials[text.split(',')[-1].strip().split()[-1]].append(location)
            if label.endswith('Movement') or 'Movement_' in label:
                movements[op].append(location)
            # Target operands are validated independently of naming conventions. A renamed
            # unknown label must not disappear just because it lacks an EventScript prefix.
            operands = text.split(None, 1)[1] if ' ' in text else ''
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'):
                target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement') or op.startswith(('goto_if_', 'call_if_')):
                target = operands.split(',')[-1].strip()
            elif op == 'case':
                target = re.split(r'[,\s]+', operands.strip())[-1]
            elif op in ('msgbox', 'giveitem_msg', 'msgreceiveditem', 'pokemart'):
                target = operands.split(',')[0].strip()
            if op in ('trainerbattle_single', 'trainerbattle_no_intro'):
                for extra in operands.split(',')[1:4] if op == 'trainerbattle_single' else operands.split(',')[1:2]:
                    if extra.strip() not in ('0', 'NULL') and aliases.get(extra.strip(), extra.strip()) not in blocks:
                        unresolved.append(dict(symbol=extra.strip(), **location))
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(symbol=target, **location))
            for token in re.findall(r'\b[A-Za-z_]\w*\b', text):
                token = aliases.get(token, token)
                if token in blocks:
                    references.append(dict(source=label, target=token, line=line['line']))
                    if token not in selected:
                        pending.append(token)
                elif re.match(r'(?:EventScript_|Common_Movement_|Movement_|Text_|.*_(?:EventScript|Movement|Text)_|CableClub_On).*', token):
                    unresolved.append(dict(symbol=token, **location))
    if unresolved:
        raise ValueError('Unresolved source label dependencies: ' + ', '.join(sorted({x['symbol'] for x in unresolved})))
    return dict(schemaVersion=1, profile=PROFILE, executable=False, roots=sorted(roots),
                blocks=[blocks[x] for x in sorted(selected)], aliases=aliases,
                filePreambles=preambles, localMacros=local_macros, references=references, unresolvedLabelReferences=unresolved,
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='sourceOnly') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])


def constants_inventory(src, worlds, scripts):
    alias_lines = [dict(text=scripts['aliases'][name]) for name in ['SPOKE_TO_WOMAN_LAST','SPOKE_TO_FAT_MAN_LAST']]
    result = SHARED.constants_inventory(src, worlds, dict(scripts, blocks=scripts['blocks']+[dict(lines=alias_lines)]))
    for name in ['SPOKE_TO_WOMAN_LAST', 'SPOKE_TO_FAT_MAN_LAST']:
        target = scripts['aliases'][name]
        if target not in result['values']: raise ValueError('Missing alias constant: ' + target)
        result['values'][name] = dict(value=result['values'][target]['value'], sourceRef=src.ref('data/maps/VermilionCity_PokemonFanClub/scripts.inc', name))
    return result

def declaration(src, path, symbol, pattern=None):
    pattern = pattern or r'^(?:static )?[^\n;]+\b' + re.escape(symbol) + r'\([^;\n]*\)\n\{.*?^\}'
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1: raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))

def item_inventory(src, constants, stage):
    """Convert the three original key item declarations/icons without admitting use handlers."""
    import base64
    codec_path = ROOT/'scripts/import-lab-battle-ui.py'
    spec=importlib.util.spec_from_file_location('_city_services_icon_codec',codec_path)
    codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
    def ref(path,symbol,match=None):
        text=src.text(path) if not path.endswith('.png') else ''
        return dict(path=path,symbol=symbol,startLine=1 if match is None else text.count('\n',0,match.start())+1,
                    endLine=max(1,text.count('\n')) if match is None else text.count('\n',0,match.end())+1)
    items=[];icons=[]
    for symbol in ['ITEM_BIKE_VOUCHER','ITEM_OLD_ROD','ITEM_BICYCLE']:
        source_items=src.json('src/data/items.json')['items'];original=next(i for i in source_items if i['itemId']==symbol)
        source=ref('src/data/items.json',symbol)
        secondary=original['secondaryId']
        if isinstance(secondary,str): secondary=constants['values'][secondary]['value']
        item=dict(id=dict(value='pret:'+symbol),sourceItemSymbol=symbol,sourceItemId=constants['values'][symbol]['value'],
            name=original['english'],price=original['price'],importance=original['importance'],registrability=original['registrability'],
            pocket=original['pocket'],type=str(original['type']),holdEffect=original['holdEffect'],holdEffectParam=original['holdEffectParam'],
            fieldUseFunction=original['fieldUseFunc'],battleUsage=original['battleUsage'],battleUseFunction=original['battleUseFunc'],
            secondaryId=secondary,canToss=original['importance']==0,canSell=original['price']!=0,canGive=original['importance']==0,source=source)
        desc=original['description_english'].replace('\\n','\n')
        items.append(dict(item=item,description=desc))
        p='src/data/item_icon_table.h';match=re.search(r'^    \['+symbol+r'\]\s*=\s*\{(\w+), (\w+)\},',src.text(p),re.M)
        if match is None: raise ValueError('Missing original icon: '+symbol)
        binding=ref(p,symbol,match);paths=[]
        for token in match.groups():
            m=re.search(r'\b'+token+r'\[\]\s*= INCBIN_U32\("(.*?)"\)',src.text('src/data/graphics/items.h'))
            if m is None: raise ValueError('Missing icon asset binding: '+token)
            paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
        image,palette=paths;w,h,idx,_=codec.indexed_png(src.data[image]);rgba=codec.rgba(idx,codec.palette(src.data[palette]))
        if (w,h)!=(24,24): raise ValueError('Item icon dimensions differ')
        png=codec.png(w,h,rgba);filename=symbol.lower()+'.png';(stage/filename).write_bytes(png)
        icons.append(dict(item=item['id'],width=w,height=h,
            pngPath=filename,source=ref(image,match[1]),paletteSource=ref(palette,match[2]),bindingSource=binding))
    if [i['item']['sourceItemId'] for i in items] != [352,262,360]: raise ValueError('Key item identities differ')
    return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,items=items,icons=icons)

def dependency_inventory(src, scripts, constants):
    blocks={b['label']:b for b in scripts['blocks']}
    def block(label,*tokens):
        b=blocks[label];text='\n'.join(x['text'] for x in b['lines'])
        if any(token not in text for token in tokens):raise ValueError('Service contract differs: '+label)
        return dict(label=label,sourceRef=b['sourceRef'],sourceText=text)
    voucher=block('VermilionCity_PokemonFanClub_EventScript_ChairmanStory','checkitemspace ITEM_BIKE_VOUCHER','setflag FLAG_GOT_BIKE_VOUCHER','giveitem_msg')
    rod=block('VermilionCity_House1_EventScript_GiveOldRod','checkitemspace ITEM_OLD_ROD','additem ITEM_OLD_ROD','msgreceiveditem','setflag FLAG_GOT_OLD_ROD')
    exchange=block('CeruleanCity_BikeShop_EventScript_ExchangeBikeVoucher','msgreceiveditem','setflag FLAG_GOT_BICYCLE','additem ITEM_BICYCLE','removeitem ITEM_BIKE_VOUCHER')
    if 'checkitemspace' in exchange['sourceText'] or 'checkitem ' in exchange['sourceText']: raise ValueError('Original unchecked exchange differs')
    def service(stable,map,entry,gift,flag,body):
        events=src.json('data/maps/'+map+'/map.json')
        actor=next((n,e) for n,e in enumerate(events['object_events'],1) if e['script']==entry)
        return dict(id=stable,map=map,mapId={'VermilionCity_PokemonFanClub':'map.vermilion-city-pokemon-fan-club','VermilionCity_House1':'map.vermilion-city-house-1','CeruleanCity_BikeShop':'map.cerulean-city-bike-shop'}[map],
            entryLabel=entry,localObjectId=actor[0],actor=actor[1],giftItem=gift,quantity=1,flag=flag,flagValue=constants['values'][flag]['value'],script=body,runtimeStatus='requiresApplicationIntegration')
    gifts=[service('city-service.bike-voucher','VermilionCity_PokemonFanClub','VermilionCity_PokemonFanClub_EventScript_Chairman','ITEM_BIKE_VOUCHER','FLAG_GOT_BIKE_VOUCHER',voucher),
           service('city-service.old-rod','VermilionCity_House1','VermilionCity_House1_EventScript_FishingGuru','ITEM_OLD_ROD','FLAG_GOT_OLD_ROD',rod),
           service('city-service.bicycle-exchange','CeruleanCity_BikeShop','CeruleanCity_BikeShop_EventScript_Clerk','ITEM_BICYCLE','FLAG_GOT_BICYCLE',exchange)]
    for gift in gifts[:2]:gift.update(choice='MSGBOX_YESNO',successChoice='YES',capacityCheckBeforeMutation=True,noRoomPreservesFlagAndInventory=True)
    gifts[0]['orderedEffects']=['checkitemspace ITEM_BIKE_VOUCHER','setflag FLAG_GOT_BIKE_VOUCHER','additem ITEM_BIKE_VOUCHER','msgreceiveditem MUS_OBTAIN_KEY_ITEM']
    gifts[1]['orderedEffects']=['checkitemspace ITEM_OLD_ROD','additem ITEM_OLD_ROD','msgbox TakeThisAndFish','msgreceiveditem MUS_LEVEL_UP','setflag FLAG_GOT_OLD_ROD']
    gifts[2].update(requiredFlag='FLAG_GOT_BIKE_VOUCHER',requiresVoucherPresenceCheck=False,capacityCheckBeforeMutation=False,
        orderedEffects=['msgreceiveditem ITEM_BICYCLE MUS_OBTAIN_KEY_ITEM','setflag FLAG_GOT_BICYCLE','additem ITEM_BICYCLE','removeitem ITEM_BIKE_VOUCHER'],
        noRoomLabel='CeruleanCity_BikeShop_EventScript_NoRoomForBicycle',noRoomLabelReachable=False,
        sourceEdgeCase='The original sets the reward flag and ignores additem/removeitem failure; a full key-item pocket can lose the voucher without receiving a bicycle. Preserve source ordering or explicitly version any compatibility change.')
    c=constants['values'];capacity=[declaration(src,'src/item.c',n) for n in ['CheckBagHasSpace','AddBagItem','RemoveBagItem']]
    if c['BAG_KEYITEMS_COUNT']['value']!=30 or 'quantity + count <= 999' not in capacity[0]['sourceText']:raise ValueError('Key item capacity differs')
    moneybox=[declaration(src,'src/scrcmd.c',n) for n in ['ScrCmd_showmoneybox','ScrCmd_hidemoneybox']]
    use=[declaration(src,'src/item_use.c',n) for n in ['FieldUseFunc_Bike','ItemUseOnFieldCB_Bicycle','FieldUseFunc_Rod','CanFish','ItemUseOnFieldCB_Rod']]
    menu=declaration(src,'src/script_menu.c','sMultichoiceList_BikeShop',r'^static const struct MenuAction sMultichoiceList_BikeShop\[\] = \{.*?^\};')
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,services=gifts,
        capacity=dict(keyItemSlots=30,maximumStack=999,sourceRef=c['BAG_KEYITEMS_COUNT']['sourceRef'],implementations=capacity),
        purchase=dict(priceDisplayOnly=1000000,checksMoney=False,chargesMoney=False,canPurchase=False,multichoiceId=c['MULTICHOICE_BIKE_SHOP']['value'],
            resultBuy=0,resultNoThanks=1,resultCancel=127,menu=menu,moneybox=moneybox,
            script=block('CeruleanCity_BikeShop_EventScript_TryPurchaseBicycle','SorryYouCantAffordIt')),
        useHandlers=use,
        scriptCommands=[declaration(src,'src/scrcmd.c',n) for n in ['ScrCmd_additem','ScrCmd_removeitem','ScrCmd_checkitemspace']],
        receiptMacros=[declaration(src,'asm/macros/event.inc',n,r'^\t\.macro '+n+r'[^\n]*\n.*?^\t\.endm') for n in ['msgreceiveditem','giveitem_msg']],
        unsupported=dict(fieldUse=['FieldUseFunc_Bike','FieldUseFunc_Rod'],bikeAvatarFlags='PLAYER_AVATAR_FLAG_MACH_BIKE | PLAYER_AVATAR_FLAG_ACRO_BIKE',
            bicycleNotes='FireRed has one ITEM_BICYCLE. The combined avatar flags are not separate collectible Mach/Acro bikes.',
            moneyboxOpcodes=['showmoneybox','hidemoneybox'],nativeSpecials=[r['symbol'] for r in scripts['specials']],
            fanClubPostgame='famechecker FAMECHECKER_DAISY, 1 expands to SetFlavorTextFlagFromSpecialVars; do not silently skip it.',
            cries=['CRY_PIKACHU_NORMAL','CRY_SEEL_NORMAL']),
        limitations=['This profile authenticates sources and assets, not executable game behavior.',
            'Fishing encounters, rod timing, mounted movement, bicycle restrictions and registered-item input remain separate integrations.',
            'The reused Bike Shop map/art is authenticated by the existing Cerulean profile; it is not re-rendered here.',
            'No game plot, service text, substitute art or repair of original script edge cases is invented.'])

def reused_maps(src):
    # Authenticate the existing map certificate and its original source identity; do not duplicate assets.
    directory=ROOT/'content/generated/cerulean-region';manifest=directory/'manifest.json'
    entries={e['path']:e for e in json_bytes(manifest.read_bytes())['outputs']}
    relative='cerulean-city-bike-shop';world_path=relative+'/runtime-map.json';data=(directory/world_path).read_bytes()
    world=json_bytes(data)
    if world['events']!=src.json('data/maps/CeruleanCity_BikeShop/map.json'):raise ValueError('Reused Bike Shop source events differ')
    outputs=[dict(path=e['path']) for e in entries.values() if e['path'].startswith(relative+'/')]
    for e in outputs:
        data=(directory/e['path']).read_bytes()
    return [dict(sourceName='CeruleanCity_BikeShop',stableId='map.cerulean-city-bike-shop',sourceMapSymbol='MAP_CERULEAN_CITY_BIKE_SHOP',
        profile='firered-cerulean-region-import-v1',
        directory='cerulean-region/'+relative,outputs=outputs)]


def import_catalog(raw,out,lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if protected.name=='city-services':continue
            if any(q.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(q.resolve()) for q in [raw,out]):raise ValueError('Existing profile overlap')
    src=Source(raw,lock_path);reused=reused_maps(src);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.city-services-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=connections_and_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=npc_inventory(src,worlds,stage/'npc');items=item_inventory(src,constants,stage);dependencies=dependency_inventory(src,scripts,constants)
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            reusedMaps=reused,connections=connections,warps=warps)
        for filename,doc in package_documents(out.parent, constants, {'city-services-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies,'items.json':items}).items():
            (stage/filename).write_bytes(canonical(doc).encode())

        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=len(worlds),reusedMapCount=len(reused),cellCount=sum(len(w['cells']) for w in worlds),
            borderCellCount=sum(len(w['borderCells']) for w in worlds),warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),
            itemCount=len(items['items']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode()
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir():dest.mkdir(parents=True,exist_ok=True)
            else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/city-services')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
