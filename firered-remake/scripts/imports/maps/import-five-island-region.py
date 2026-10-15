#!/usr/bin/env python3
"""Authenticate the bounded Five Island / Rocket Warehouse region; execute no upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
import argparse, ast, collections,  importlib.util, json, re, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PROFILE = 'firered-five-island-region-v1'
PACKAGE = 'maps/regions/five-island'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
CORE = ['FiveIsland', 'FiveIsland_Harbor', 'FiveIsland_Meadow', 'FiveIsland_RocketWarehouse']
SUPPORT = ['FiveIsland_PokemonCenter_1F']
ANCHOR = []
TILESETS = {'gTileset_General':'primary/general', 'gTileset_Building':'primary/building',
            'gTileset_SeviiIslands45':'secondary/sevii_islands_45',
            'gTileset_SilphCo':'secondary/silph_co',
            'gTileset_IslandHarbor':'secondary/island_harbor',
            'gTileset_PokemonCenter':'secondary/pokemon_center'}

def canonical(v): return (json.dumps(v, sort_keys=True, separators=(',', ':'))+'\n').encode()
def need(v, message):
    if not v: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink in output: '+str(path))
def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value

def script_index(src, reader):
    paths=[p for p in src.tree if p.endswith('.inc') and
      (p.startswith(('data/scripts/','data/text/')) or p.endswith('/text.inc') or
       any(p.startswith('data/maps/'+n+'/') for n in CORE+SUPPORT+ANCHOR))]+['data/event_scripts.s']
    blocks={}; aliases={}
    for p in sorted(paths):
        b=reader.git(src.reference,'cat-file','blob',src.tree[p][2]); current=None; macro=False
        for ln,line in enumerate(b.decode().splitlines(),1):
            code=line.split('@')[0].strip()
            if code.startswith('.macro'): macro=True;current=None;continue
            if code=='.endm':macro=False;continue
            if macro:continue
            m=re.fullmatch(r'(\w+)::?',code)
            if m:
                current=m[1];need(current not in blocks,'duplicate label '+current)
                blocks[current]=dict(label=current,path=p,line=ln,lines=[])
            elif current:blocks[current]['lines'].append(dict(line=ln,text=line))
            m=re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)',code)
            if m:aliases[m[1]]=m[2]
    roots={n for n,b in blocks.items() if any(b['path']==f'data/maps/{m}/scripts.inc' for m in CORE+SUPPORT+ANCHOR)}
    for name in CORE+SUPPORT:
        m=src.json(f'data/maps/{name}/map.json')
        roots.update(e['script'] for k in ('object_events','coord_events','bg_events') for e in m[k]
                     if e.get('script') not in (None,'0','0x0','NULL'))
    roots.update(['EventScript_ResetAllMapFlags','EventScript_DoNoIntroTrainerBattle','EventScript_TryDoNormalTrainerBattle',
                  'EventScript_TryDoDoubleTrainerBattle','EventScript_PC','Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo',
                  'Std_ObtainItem','Std_FindItem','Std_ReceivedItem','Std_PutItemAway'])
    pending=sorted(roots); selected=set()
    while pending:
        label=pending.pop();label=aliases.get(label,label)
        if label in selected:continue
        need(label in blocks,'missing label '+label);selected.add(label)
        for row in blocks[label]['lines']:
            for tok in re.findall(r'\b[A-Za-z_]\w*\b',row['text'].split('@')[0]):
                tok=aliases.get(tok,tok)
                if tok in blocks and tok not in selected:pending.append(tok)
    result=[]; opcodes=collections.Counter(); specials=collections.Counter();unresolved=[]
    for label in sorted(selected):
        b=blocks[label];src.read(b['path'],'script-label-closure')
        result.append(dict(label=label,sourceRef=src.ref(b['path'],label,b['line']),lines=b['lines']))
        for r in b['lines']:
            t=r['text'].split('@')[0].strip()
            if not t or t.startswith(('.','#')):continue
            op=t.split()[0];opcodes[op]+=1
            if op in ('special','specialvar'):specials[t.split(',')[-1].split()[-1]]+=1
            operands=t.split(None,1)[1] if len(t.split(None,1))==2 else '';target=None
            if op in ('call','goto','goto_if_questlog','message'):target=operands.strip()
            elif op in ('map_script','map_script_2','applymovement') or op.startswith(('goto_if_','call_if_')):target=operands.split(',')[-1].strip()
            elif op=='case':target=re.split(r'[,\s]+',operands.strip())[-1]
            elif op in ('msgbox','giveitem_msg','msgreceiveditem','pokemart'):target=operands.split(',')[0].strip()
            if target and target not in ('0','0x0','NULL') and aliases.get(target,target) not in blocks:unresolved.append(dict(source=label,line=r['line'],target=target))
    need(not unresolved,'Unresolved labels '+repr(unresolved))
    return dict(executable=False,roots=sorted(roots),blocks=result,aliases=aliases,
      opcodeCounts=dict(sorted(opcodes.items())),specialCounts=dict(sorted(specials.items())),
      unresolvedLabelReferences=unresolved,
      warning='Full label dependency inventory includes guarded postgame and multiplayer branches, not runtime admission.')

def declarations(src, scripts, worlds, proof):
    species = {s['id']['value'][5:]:s for s in proof['species']}
    metadata = {s['id']['value'][5:]:s for s in proof['metadata']}
    occurrences = []
    for block in scripts['blocks']:
        for row in block['lines']:
            text = row['text'].split('@')[0].strip()
            if not text.startswith('trainerbattle_'): continue
            opcode, args = text.split(None, 1); args = [v.strip() for v in args.split(',')]
            occurrences.append(dict(trainer=args[0], opcode=opcode, arguments=args,
                script=block['label'], sourceRef=src.ref(block['sourceRef']['path'], block['label'], row['line']),
                rematch='rematch' in opcode, admitted=False))
    def one(path, pattern):
        found = list(re.finditer(pattern, src.text(path), re.M|re.S))
        need(len(found)==1, 'Source declaration differs: '+pattern); return found[0]
    trainers = []
    for symbol in sorted({o['trainer'] for o in occurrences}):
        p = 'src/data/trainers.h'; match = one(p, r'^    \['+symbol+r'\] = \{\n(.*?)^    \},')
        fields = dict(re.findall(r'^\s*\.(\w+) = (.+),$', match[1], re.M))
        party = re.fullmatch(r'((?:NO_)?ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)', fields['party'])
        need(party, 'Unsupported trainer party '+symbol)
        shape, pname = party.groups(); custom = 'CUSTOM' in shape; held = not shape.startswith('NO_')
        pp = 'src/data/trainer_parties.h'
        pm = one(pp, r'^static const struct TrainerMon'+('Item' if held else 'NoItem')+('Custom' if custom else 'Default')+'Moves '+pname+r'\[\] = \{\n(.*?)^\};')
        rows = list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.heldItem = (ITEM_\w+),\n)?(?:        \.moves = \{([^}]+)\},\n)?    \},\n', pm[1]))
        need(''.join(r[0] for r in rows)==pm[1], 'Unsupported trainer members '+symbol)
        mons = []
        for row in rows:
            iv, level, sp = int(row[1]), int(row[2]), row[3]; moves = []
            for move in species[sp]['levelUpMoves']:
                ms = move['move']['value'][5:]
                if move['level']<=level and ms not in moves: moves=(moves+[ms])[-4:]
            need(bool(row[4])==held and bool(row[5])==custom, 'Trainer party flags differ '+symbol)
            if custom: moves=[v.strip() for v in row[5].split(',')]
            mons.append(dict(species=sp, level=level, sourceIv=iv, heldItem=row[4] or 'ITEM_NONE',
                             customMoves=custom, resolvedMoves=moves))
        trainers.append(dict(trainer=symbol, fields=fields, party=mons,
            sourceRef=src.ref(p, symbol, src.text(p).count('\n', 0, match.start())+1),
            partySource=src.ref(pp, pname, src.text(pp).count('\n', 0, pm.start())+1)))
    symbols = {w['events']['id'] for w in worlds}; encounters=[]; slots=[]
    for group in src.json('src/data/wild_encounters.json')['wild_encounter_groups']:
        chosen=[e for e in group['encounters'] if e['map'] in symbols and e['base_label'].endswith('_FireRed')]
        if chosen: encounters.extend(chosen); slots.append({k:v for k,v in group.items() if k!='encounters'})
    wild = {m['species'] for e in encounters for t in e.values() if isinstance(t,dict) and 'mons' in t for m in t['mons']}
    evolution = set(wild)
    while True:
        extra = {e['target']['value'][5:] for s in evolution for e in metadata[s]['sourceEvolutions'] if e['method']=='EVO_LEVEL'}
        if extra<=evolution: break
        evolution |= extra
    opponents = {m['species'] for t in trainers for m in t['party']}
    wanted = evolution|opponents
    item_symbols = {'ITEM_SAPPHIRE'}
    local_labels = {e.get('script') for w in worlds for e in w['events']['object_events']}
    for block in scripts['blocks']:
        if block['sourceRef']['path'].startswith(tuple('data/maps/'+n+'/' for n in CORE+SUPPORT)) or block['label'] in local_labels:
            item_symbols.update(re.findall(r'\bITEM_\w+', '\n'.join(r['text'] for r in block['lines'])))
    item_symbols.update(e['item'] for w in worlds for e in w['events']['bg_events'] if 'item' in e)
    item_symbols.update(m['heldItem'] for t in trainers for m in t['party'])
    item_symbols.update(i for t in trainers for i in re.findall(r'ITEM_\w+',t['fields']['items']))
    item_symbols.discard('ITEM_NONE')
    items=[i for i in src.json('src/data/items.json')['items'] if i['itemId'] in item_symbols]
    need({i['itemId'] for i in items}==item_symbols, 'Missing item declarations')
    return dict(schemaVersion=1,
        trainers=trainers, battleOccurrences=occurrences, wildEncounters=encounters, encounterSlotTables=slots,
        wildSpecies=sorted(wild), nationalDexLevelEvolutionSpecies=sorted(evolution), trainerSpecies=sorted(opponents),
        trainerMoves=sorted({m for t in trainers for mon in t['party'] for m in mon['resolvedMoves']}-{'MOVE_NONE'}),
        fullNaturalMoveClosure=sorted({m['move']['value'][5:] for s in wanted for m in species[s]['levelUpMoves']}),
        pokemon=[dict(id=species[s]['id'],evolutions=species[s]['evolutions']) for s in sorted(wanted)], pokemonMetadata=[metadata[s] for s in sorted(wanted)],
        itemDeclarations=items)

def constants(src, shared, worlds, scripts):
    result=shared.constants_inventory(src, worlds, scripts); definitions={}
    for path in sorted(src.data):
        if path.startswith('include/'):
            text=src.text(path)
            for m in re.finditer(r'^#define[ \t]+([A-Za-z][A-Za-z0-9_]+)[ \t]+([^\n]+)',text,re.M):
                definitions[m[1]]=(m[2].split('//')[0].strip(),path,text.count('\n',0,m.start())+1)
    text=src.text('asm/macros/event.inc')
    for m in re.finditer(r'^\s*([A-Z][A-Z0-9_]+)\s*=\s*([0-9]+)\s*$',text,re.M):
        definitions[m[1]]=(m[2],'asm/macros/event.inc',text.count('\n',0,m.start())+1)
    pending=set()
    def resolve(symbol):
        if symbol in result['values']: return result['values'][symbol]['value']
        need(symbol not in pending and symbol in definitions, 'Unresolved constant '+symbol)
        pending.add(symbol); expression,path,line=definitions[symbol]
        def number(node):
            if isinstance(node,ast.Constant) and type(node.value) is int:return node.value
            if isinstance(node,ast.Name):return resolve(node.id)
            if isinstance(node,ast.BinOp):
                x,y=number(node.left),number(node.right)
                if isinstance(node.op,ast.Add):return x+y
                if isinstance(node.op,ast.Sub):return x-y
                if isinstance(node.op,ast.LShift) and 0<=y<32:return x<<y
                if isinstance(node.op,ast.BitOr):return x|y
            raise ValueError('Unsupported constant expression '+symbol)
        value=number(ast.parse(expression,mode='eval').body); need(0<=value<=0x7fffffff, 'Constant outside range')
        result['values'][symbol]=dict(value=value,sourceRef=src.ref(path,symbol,line));pending.remove(symbol);return value
    # Quoted .string/.braille payloads are text, never numeric operands.
    operands=set(re.findall(r'\b[A-Z][A-Z0-9_]+\b','\n'.join(re.sub(r'"[^"\n]*"','',r['text'].split('@')[0]) for b in scripts['blocks'] for r in b['lines'])))
    for symbol in sorted(operands & definitions.keys()):resolve(symbol)
    for alias,target in scripts['aliases'].items():
        if target in result['values'] or target in definitions:
            value=resolve(target)
            result['values'][alias]=dict(value=value,sourceRef=result['values'][target]['sourceRef'],aliasOf=target)
    for i in range(1,10):resolve('FLAG_TEMP_'+str(i))
    healpath='src/data/heal_locations.json';template=src.text('src/data/heal_locations.constants.json.txt')
    need('HEAL_LOCATION_NONE,' in template and '{{ heal_location.id }},' in template, 'Heal enum differs')
    for i,h in enumerate(src.json(healpath)['heal_locations'],1):
        result['values'][h['id']]=dict(value=i,sourceRef=src.ref(healpath,h['id']))
    result['nonNumericOperandTokens']=sorted(operands-result['values'].keys()-{s for s in operands if s.startswith('MAP_')})
    need(result['nonNumericOperandTokens']==['STR_VAR_1','STR_VAR_2','STR_VAR_3'], 'Unresolved operand constants '+str(result['nonNumericOperandTokens']))
    path='include/constants/metatile_labels.h';text=src.text(path)
    for m in re.finditer(r'^#define\s+(METATILE_\w+)\s+(0x[0-9A-Fa-f]+)\s*$',text,re.M):
        result['values'][m[1]]=dict(value=int(m[2],16),sourceRef=src.ref(path,m[1],text.count('\n',0,m.start())+1))
    return result

def topology(src,worlds):
    allmaps={}
    for p in src.tree:
        if p.startswith('data/maps/') and p.endswith('/map.json'):
            n=p.split('/')[2]; symbol='MAP_'+re.sub(r'([a-z])([A-Z])',r'\1_\2',n).upper(); allmaps[symbol]=p
    active={w['events']['id'] for w in worlds}; warps=[]; connections=[]
    for w in worlds:
        e=w['events'];symbol=e['id']
        for i,x in enumerate(e['warp_events']):
            d=src.json(allmaps[x['dest_map']]); j=int(x['dest_warp_id']); back=d['warp_events'][j]
            warps.append(dict(sourceMap=symbol,sourceIndex=i,source=x,destination=back,
              destinationInSlice=x['dest_map'] in active,reciprocalMap=back['dest_map']==symbol,
              reciprocalIndex=back['dest_map']==symbol and int(back['dest_warp_id'])==i))
        opposite={'up':'down','down':'up','left':'right','right':'left'}
        for x in e['connections'] or []:
            d=src.json(allmaps[x['map']]);back=[y for y in d['connections'] or [] if y['map']==symbol and y['direction']==opposite[x['direction']]]
            need(len(back)==1 and back[0]['offset']==-x['offset'],'connection mismatch')
            connections.append(dict(sourceMap=symbol,source=x,destinationInSlice=x['map'] in active,reciprocal=True))
    return dict(warps=warps,connections=connections)

def field_contract(src, worlds, constants, bylabel):
    byname = {w['sourceName']: w for w in worlds}
    def cell(name,x,y): return byname[name]['cells'][y*byname[name]['width']+x]
    def block(label): return bylabel[label]
    def lines(label): return [r['text'].split('@')[0].strip() for r in block(label)['lines']]
    def tile(symbol,tileset):
        mid=constants['values'][symbol]['value'];path='data/tilesets/secondary/'+tileset+'/metatile_attributes.bin'
        attr=src.units(path,'I')[mid-640]
        return dict(symbol=symbol,metatileId=mid,rawAttributes=attr,behaviorId=attr&511,
            sourceRef=constants['values'][symbol]['sourceRef'],attributeSource=src.ref(path,'metatile_'+format(mid-640,'03x')))
    door='FiveIsland_Meadow_EventScript_SetWarehouseDoorUnlocked'
    need(lines(door)[0]=='setmetatile 12, 21, METATILE_SeviiIslands45_RocketWarehouseDoor_Unlocked, 1', 'Original warehouse door patch differs')
    doortiles=[tile('METATILE_SeviiIslands45_RocketWarehouseDoor_'+state,'sevii_islands_45') for state in ['Locked','Unlocked']]
    need(cell('FiveIsland_Meadow',12,21)['metatileId']==doortiles[0]['metatileId'], 'Initial locked door differs')
    arrows='FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry'
    need(lines(arrows)[:2]==['setmetatile 26, 20, METATILE_SilphCo_Arrow_Up, 0','setmetatile 27, 20, METATILE_SilphCo_Arrow_Up, 0'], 'Original Admin1 arrows differ')
    arrowtile=tile('METATILE_SilphCo_Arrow_Up','silph_co')
    spin=[c for c in byname['FiveIsland_RocketWarehouse']['cells'] if 0x54<=c['behaviorId']<=0x58]
    need(len(spin)==135 and arrowtile['behaviorId']==0x56, 'Original Warehouse spin cells differ')
    recovery='FiveIsland_RocketWarehouse_EventScript_DefeatedGideon';recoverylines=lines(recovery)
    need(recoverylines.index('setflag FLAG_RECOVERED_SAPPHIRE')<recoverylines.index('giveitem ITEM_SAPPHIRE'), 'Original Sapphire flag/item order differs')
    healpath='src/data/heal_locations.json';heal=next(h for h in src.json(healpath)['heal_locations'] if h['id']=='HEAL_LOCATION_FIVE_ISLAND')
    sea=src.text('src/seagallop.c');arrival=re.search(r'\[SEAGALLOP_FIVE_ISLAND\]\s*=\s*\{MAP\((MAP_\w+)\),\s*(0x[0-9a-fA-F]+),\s*(0x[0-9a-fA-F]+)\}',sea)
    need(arrival, 'Five Island ferry landing missing')
    clone=byname['FiveIsland']['events']['object_events'][2]
    need(clone['type']=='clone' and clone['target_map']=='MAP_FIVE_ISLAND_MEADOW', 'Original border clone differs')
    return dict(warehouseDoor=dict(map='MAP_FIVE_ISLAND_MEADOW',x=12,y=21,openCollision=1,
        flag='FLAG_UNLOCKED_ROCKET_WAREHOUSE',passwordFlag='FLAG_LEARNED_YES_NAH_CHANSEY',sourceCell=cell('FiveIsland_Meadow',12,21),metatiles=doortiles,
        onLoad=block('FiveIsland_Meadow_OnLoad'),entryScript=block('FiveIsland_Meadow_EventScript_WarehouseDoor'),
        openScript=block('FiveIsland_Meadow_EventScript_OpenWarehouseDoor'),patchScript=block(door)),
        admin1Arrows=dict(map='MAP_FIVE_ISLAND_ROCKET_WAREHOUSE',trainer='TRAINER_TEAM_ROCKET_ADMIN',
            originalCells=[cell('FiveIsland_RocketWarehouse',x,20) for x in [26,27]],metatile=arrowtile,collision=0,
            onLoad=block('FiveIsland_RocketWarehouse_OnLoad'),patchScript=block(arrows),
            defeatedScript=block('FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1')),
        spinTiles=spin,sapphireRecovery=dict(script=block(recovery),grantItem='ITEM_SAPPHIRE',endpointFlag='FLAG_RECOVERED_SAPPHIRE',
            laterCelioDeliveryAdmitted=False),
        rocketDeparture=dict(script=block('FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin2'),
            hideFlag='FLAG_HIDE_FIVE_ISLAND_ROCKETS',defeatedFlag='FLAG_DEFEATED_ROCKETS_IN_WAREHOUSE'),
        cloneObjects=[dict(map='MAP_FIVE_ISLAND',localId=3,event=clone)],
        ferryLanding=dict(map=arrival[1],x=int(arrival[2],16),y=int(arrival[3],16),facing='DIR_SOUTH',sourceRef=src.ref('src/seagallop.c','sSeag',sea.count('\n',0,arrival.start())+1)),
        healLocation=dict(declaration=heal,sourceRef=src.ref(healpath,heal['id']),blackoutX=7,blackoutY=4,blackoutFacing='DIR_NORTH',
            blackoutSource=src.ref('src/heal_location.c','SetWhiteoutRespawnWarpAndHealerNpc')),
        escapeRopeAnchor=dict(map='MAP_FIVE_ISLAND_MEADOW',entry=byname['FiveIsland_Meadow']['events']['warp_events'][0],
            x=12,y=22,facing='DIR_SOUTH',sourceRef=src.ref('src/overworld.c','UpdateEscapeWarp')),
        mapHeaders=[dict(map=w['events']['id'],allowCycling=w['events']['allow_cycling'],allowEscaping=w['events']['allow_escaping'],
            allowRunning=w['events']['allow_running'],sourceRef=w['sourceRef']) for w in worlds])

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--dependency-root',type=Path,default=ROOT)
    ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--check',action='store_true');args=ap.parse_args()
    safe(args.project_root.absolute())
    target=args.project_root.resolve();dep=args.dependency_root.resolve();ref=args.reference_root.resolve();safe(target)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader=module(dep / 'scripts/imports/source/import-cinnabar-source-preparation.py', 'five_island_reader')
    reader.PRODUCT=dep;reader.TILESETS=TILESETS;reader.PROFILE=PROFILE
    src=reader.Source(ref);worlds=[reader.decode(src,n) for n in CORE+SUPPORT]
    for n in ANCHOR:src.read(f'data/maps/{n}/map.json','reused-anchor-events')
    scripts=script_index(src,reader);scripts['profile']=PROFILE

    need(not scripts['unresolvedLabelReferences'], 'Unresolved label closure')
    links=topology(src,worlds)
    proofbytes=(dep/'content/generated/data/pokemon/canonical/catalog.json').read_bytes()
    data=declarations(src,scripts,worlds,json.loads(proofbytes))
    for p in reader.META_PATHS+['data/maps/PalletTown/scripts.inc','data/maps/PalletTown_ProfessorOaksLab/scripts.inc',
        'src/event_data.c','src/seagallop.c','src/new_game.c','src/field_specials.c',
        'src/field_player_avatar.c','src/field_tasks.c','src/fieldmap.c','src/bike.c','src/party_menu.c','src/heal_location.c',
        'src/fldeff_cut.c','src/field_control_avatar.c','src/metatile_behavior.c','src/field_fadetransition.c',
        'include/constants/field_tasks.h','include/constants/daycare.h',
        'src/data/heal_locations.json','src/data/heal_locations.constants.json.txt',
        'data/maps/map_groups.json','data/maps/VermilionCity/map.json','include/gba/defines.h','src/data/tilesets/headers.h','src/data/tilesets/graphics.h']:
        src.read(p,'rule-reference')
    # Preserve the selected source constants, not every header in the reference tree.
    headers=['flags','vars','maps','map_scripts','event_objects','event_object_movement','opponents','trainers',
        'species','moves','items','abilities','songs','battle','battle_ai','battle_setup','menu','seagallop',
        'metatile_labels','metatile_behaviors','cable_club','fame_checker','field_effects',
        'field_weather','game_stat','global','help_system','pokemon','sound','union_room','daycare','field_tasks']
    for n in headers:src.read('include/constants/'+n+'.h','script-operand-constant')
    bylabel={b['label']:b for b in scripts['blocks']}
    pickups=[];hidden=[]
    for w in worlds:
        for i,e in enumerate(w['events']['object_events'],1):
            if e['graphics_id'] == 'OBJ_EVENT_GFX_ITEM_BALL':
                pickups.append(dict(map=w['events']['id'],localId=i,event=e,script=bylabel[e['script']]))
        hidden.extend(dict(map=w['events']['id'],event=e) for e in w['events']['bg_events'] if 'item' in e)
    contract=dict(schemaVersion=1,coreMaps=CORE,supportMaps=SUPPORT,
        reusedAnchorMaps=ANCHOR,mapTopology=links,itemPickups=pickups,hiddenItems=hidden,
        excludedEncounterGates=[],
        externalBoundaries={k:[e for e in links[k] if not e['destinationInSlice']] for k in ('warps','connections')},
        tilePatches=[dict(label=b['label'],sourceRef=b['sourceRef'],changes=[dict(line=r['line'],arguments=[x.strip() for x in r['text'].strip().split(None,1)[1].split(',')]) for r in b['lines'] if r['text'].strip().startswith('setmetatile ')]) for b in scripts['blocks'] if any(r['text'].strip().startswith('setmetatile ') for r in b['lines'])])
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='five-island-import-') as temp:
        stage=Path(temp)
        for n in CORE+SUPPORT:shared.import_map(src,n,'map.'+reader.directory(n),stage/reader.directory(n))
        visible=[dict(w,events=dict(w['events'],object_events=[e for e in w['events']['object_events'] if e['graphics_id']!='0'])) for w in worlds]
        npcs=shared.npc_inventory(src,visible,stage/'npc')
        npcs['invisibleActors']=[dict(map=w['events']['id'],localId=i,event=e) for w in worlds for i,e in enumerate(w['events']['object_events'],1) if e['graphics_id']=='0']
        const=constants(src,shared,worlds,scripts)
        contract.update(field_contract(src, worlds, const, bylabel))
        data['shopStocks'] = []
        mapsymbols={}
        tokens=set(re.findall(r'\bMAP_[A-Z0-9_]+\b',json.dumps(scripts)+json.dumps(contract)+json.dumps([w['events'] for w in worlds])))
        for p in src.tree:
            if p.startswith('data/maps/') and p.endswith('/map.json'):
                n=p.split('/')[2];symbol='MAP_'+re.sub(r'([a-z])([A-Z])',r'\1_\2',n).upper()
                if symbol in tokens:mapsymbols[symbol]='map.'+reader.directory(n)
        catalog=[]
        for w in worlds:
            relative=reader.directory(w['sourceName']);p=stage/relative/'runtime-map.json';rich=p.read_bytes()
            compact=converter.encode(converter.normalize_runtime(json.loads(rich)));(stage/relative/'runtime-map.json').write_bytes(compact)
            catalog.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=relative,sourceMapSymbol=w['events']['id'],
                path=relative+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=CORE+SUPPORT)
        lockbytes=canonical(lock)
        docs=package_documents(target / 'content/generated', const, {'catalog.json':dict(schemaVersion=1,
            maps=catalog,tilesets=TILESETS,mapSymbols=mapsymbols,**links),
            'scripts.json':scripts,'npc-source.json':npcs,'contracts.json':contract,
            'geometry.json':dict(maps=reader.geometry_inventory(worlds),tilesets=TILESETS)}, check=args.check)
        for n,d in docs.items():(stage/n).write_bytes(canonical(d))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,


            topologyVersion='five-island-warehouse-v1',scriptIndexerVersion='five-island-label-closure-v1',outputs=outputs)
        manifestbytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifestbytes)
        writes=[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            safe(p)
            if args.check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))

if __name__=='__main__':main()
