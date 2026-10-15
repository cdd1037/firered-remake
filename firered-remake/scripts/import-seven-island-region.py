#!/usr/bin/env python3
"""Reproduce 18 pinned Seven Island maps and source field contracts; no upstream execution."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse, ast, collections,  importlib.util, json, re, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PROFILE = 'firered-seven-island-region-v1'
PACKAGE = 'seven-island-region'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
CORE = ['SevenIsland', 'SevenIsland_Harbor', 'SevenIsland_House_Room1', 'SevenIsland_Mart',
    'SevenIsland_PokemonCenter_1F', 'SevenIsland_SevaultCanyon', 'SevenIsland_SevaultCanyon_Entrance',
    'SevenIsland_SevaultCanyon_House', 'SevenIsland_SevaultCanyon_TanobyKey', 'SevenIsland_TanobyRuins',
    *['SevenIsland_TanobyRuins_'+c+'Chamber' for c in ['Monean','Liptoo','Weepth','Dilford','Scufib','Rixy','Viapois']],
    'SevenIsland_TrainerTower']
SUPPORT = []
ANCHOR = []
TILESETS = {'gTileset_General':'primary/general', 'gTileset_Building':'primary/building',
    'gTileset_SeviiIslands67':'secondary/sevii_islands_67', 'gTileset_IslandHarbor':'secondary/island_harbor',
    'gTileset_PokemonCenter':'secondary/pokemon_center', 'gTileset_Mart':'secondary/mart',
    'gTileset_SeafoamIslands':'secondary/seafoam_islands', 'gTileset_GenericBuilding2':'secondary/generic_building_2',
    'gTileset_TanobyRuins':'secondary/tanoby_ruins', 'gTileset_DiglettsCave':'secondary/digletts_cave'}

def canonical(v): return (json.dumps(v, sort_keys=True, indent=2)+'\n').encode()
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
                  'Std_FindItem','Std_ReceivedItem','Std_PutItemAway'])
    roots.add('EventScript_HiddenItemScript')
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


def constants(src, shared, worlds, scripts):
    result=shared.constants_inventory(src, worlds, scripts); definitions={}
    for path in sorted(src.data):
        if path.startswith('include/'):
            text=src.text(path)
            for m in re.finditer(r'^#define[ \t]+([A-Z][A-Z0-9_]+)[ \t]+([^\n]+)',text,re.M):
                definitions[m[1]]=(m[2].split('//')[0].strip(),path,text.count('\n',0,m.start())+1)
    for i, layout in enumerate(src.json('data/layouts/layouts.json')['layouts'], 1):
        if 'id' in layout:
            result['values'][layout['id']]=dict(value=i,sourceRef=src.ref('data/layouts/layouts.json',layout['id']))
    need('int i = 1;' in src.text('tools/mapjson/mapjson.cpp'), 'Layout enum generator differs')
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
    for i in range(1,10):resolve('FLAG_TEMP_'+str(i))
    healpath='src/data/heal_locations.json';template=src.text('src/data/heal_locations.constants.json.txt')
    need('HEAL_LOCATION_NONE,' in template and '{{ heal_location.id }},' in template, 'Heal enum differs')
    for i,h in enumerate(src.json(healpath)['heal_locations'],1):
        result['values'][h['id']]=dict(value=i,sourceRef=src.ref(healpath,h['id']))
    for alias, target in scripts['aliases'].items():
        if alias in operands and target in definitions:
            resolve(target)
            result['values'][alias]=result['values'][target]
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


def function(src, path, symbol):
    text=src.text(path); m=re.search(r'^(?:static )?\w[^\n;]*\b'+symbol+r'\([^;\n]*\)\n\{',text,re.M)
    need(m, 'Missing source function '+symbol)
    depth=1; end=m.end()
    while depth:
        need(end<len(text),'Unclosed source function');depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return dict(sourceRef=src.ref(path,symbol,text.count('\n',0,m.start())+1),text=text[m.start():end])

def field_contract(src, worlds, const, scripts, links):
    maps={w['events']['id']:w for w in worlds}; blocks={b['label']:b for b in scripts['blocks']}
    def cell(symbol,x,y):
        w=maps[symbol];need(0<=x<w['width'] and 0<=y<w['height'],'Anchor outside map');return w['cells'][y*w['width']+x]
    renewable=src.text('src/renewable_hidden_items.c'); groups=[]
    for match in re.finditer(r'    \{\n        \.mapGroup = MAP_GROUP\((MAP_\w+)\),(.*?)\n    \},',renewable,re.S):
        if match[1] not in maps:continue
        row=dict(map=match[1],sourceRef=src.ref('src/renewable_hidden_items.c','sRenewableHiddenItems',renewable.count('\n',0,match.start())+1))
        for rarity in ['rare','uncommon','common']:
            section=re.search(r'\.'+rarity+r' = \{(.*?)\}',match[2],re.S);need(section,'Missing renewable rarity')
            row[rarity]=re.findall(r'HIDDEN_ID\((FLAG_\w+)\)',section[1])
        groups.append(row)
    need(len(groups)==2 and sum(len(g[k]) for g in groups for k in ['rare','uncommon','common'])==6,'Renewable source group scope differs')
    flags={f:(g['map'],rarity,i) for g in groups for rarity in ['rare','uncommon','common'] for i,f in enumerate(g[rarity])}
    visible=[];hidden=[];clones=[]
    for w in worlds:
        symbol=w['events']['id']
        for i,e in enumerate(w['events']['object_events'],1):
            if e.get('type')=='clone':
                target=maps[e['target_map']];pairs=[(j,v) for j,v in enumerate(target['events']['object_events'],1) if v.get('local_id')==e['target_local_id']]
                need(len(pairs)==1,'Clone target differs');j,target_event=pairs[0]
                clones.append(dict(map=symbol,localId=i,declaration=e,targetMap=e['target_map'],targetLocalId=j,target=target_event))
            if e['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
            block=blocks[e['script']];tokens=re.findall(r'^\s*finditem (ITEM_\w+)(?:,\s*(\d+))?', '\n'.join(r['text'] for r in block['lines']),re.M)
            need(len(tokens)==1,'Visible pickup script differs')
            visible.append(dict(map=symbol,localId=i,event=e,script=block,item=tokens[0][0],quantity=int(tokens[0][1] or '1'),renewable=False))
        for e in w['events']['bg_events']:
            if 'item' not in e:continue
            row=dict(map=symbol,event=e,item=e['item'],quantity=e['quantity'],renewable=e['flag'] in flags)
            if row['renewable']:
                group,rarity,index=flags[e['flag']];need(group==symbol,'Renewable flag mapped to another map');row.update(renewalRarity=rarity,renewalIndex=index)
            hidden.append(row)
    need(len(visible)==4 and len(hidden)==9 and len(clones)==1,'Pickup or clone source scope differs')
    key='MAP_SEVEN_ISLAND_SEVAULT_CANYON_TANOBY_KEY';w=maps[key]
    expected=[(7,2),(4,4),(10,4),(5,6),(9,6),(5,8),(9,8)]
    ordered=sorted(w['events']['coord_events'], key=lambda e:int(e['var'].rsplit('_',1)[1]))
    need([(e['x'],e['y']) for e in ordered]==expected,'Switch coordinates differ')
    switches=[]
    for i,e in enumerate(ordered,1):
        c=cell(key,e['x'],e['y']);need(c['behaviorId']==0x20 and e['var_value']=='99','Strength switch behavior/player guard differs')
        switches.append(dict(index=i,sourceEventIndex=w['events']['coord_events'].index(e),event=e,sourceCell=c,temporaryVariable='VAR_TEMP_'+str(i),pressedValue=100,script=blocks[e['script']]))
    boulders=[dict(localId=i,event=e,solvedX=expected[i-1][0],solvedY=expected[i-1][1]) for i,e in enumerate(w['events']['object_events'],1)]
    need(len(boulders)==7 and all(b['event']['graphics_id']=='OBJ_EVENT_GFX_PUSHABLE_BOULDER' for b in boulders),'Seven source boulders required')
    activate=function(src,'src/field_control_avatar.c','HandleBoulderActivateVictoryRoadSwitch')
    need('MB_STRENGTH_BUTTON' in activate['text'] and 'events[i].x + MAP_OFFSET == x' in activate['text'] and 'events[i].y + MAP_OFFSET == y' in activate['text'] and 'events[i].var' not in activate['text'],'Boulder hook must dispatch by coordinate without player var99 guard')
    prefix='SevenIsland_SevaultCanyon_TanobyKey_'
    puzzle=dict(map=key,switches=switches,boulders=boulders,solvedFlag='FLAG_SYS_UNLOCKED_TANOBY_RUINS',
        countVariable='VAR_TEMP_8',requiredCount=7,pressedValue=100,strengthBehaviorId=0x20,
        trigger=activate,finishPush=function(src,'src/field_player_avatar.c','DoBoulderFinish'),
        onTransition=blocks[prefix+'OnTransition'],solvedPositions=blocks[prefix+'EventScript_MoveBouldersToSolvedPos'],
        complete=blocks[prefix+'EventScript_PuzzleSolved'],press=blocks[prefix+'EventScript_SwitchPressed'])
    heal=next(h for h in src.json('src/data/heal_locations.json')['heal_locations'] if h['id']=='HEAL_LOCATION_SEVEN_ISLAND')
    need(heal['x']==12 and heal['y']==4 and const['values'][heal['id']]['value']==19,'Seven Island heal anchor differs')
    sea=src.text('src/seagallop.c');need('[SEAGALLOP_SEVEN_ISLAND]    = {MAP(MAP_SEVEN_ISLAND_HARBOR), 0x08, 0x05}' in sea,'Ferry anchor differs')
    external={k:[e for e in links[k] if not e['destinationInSlice']] for k in ['warps','connections']}
    need(len(external['warps'])==3 and not external['connections'] and {e['source']['dest_map'] for e in external['warps']}=={'MAP_TRAINER_TOWER_LOBBY','MAP_SEVEN_ISLAND_HOUSE_ROOM2','MAP_SEVEN_ISLAND_POKEMON_CENTER_2F'},'Unsupported boundary scope differs')
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,maps=CORE,mapTopology=links,externalBoundaries=external,
        itemPickups=visible,hiddenItems=hidden,clones=clones,tanobyKey=puzzle,
        renewableHiddenItems=dict(stepThreshold=1500,counterVariable='VAR_RENEWABLE_ITEM_STEP_COUNTER',groups=groups,
            originalGlobalGroupCount=15,initiallyUnavailable=True,resetAllGroupsBeforeSampling=True,
            increment=function(src,'src/renewable_hidden_items.c','IncrementRenewableHiddenItemStepCounter'),
            regenerate=function(src,'src/renewable_hidden_items.c','TryRegenerateRenewableHiddenItems'),
            sample=function(src,'src/renewable_hidden_items.c','SampleRenewableItemFlags')),
        ferryLanding=dict(map='MAP_SEVEN_ISLAND_HARBOR',x=8,y=5,facing='DIR_SOUTH',sourceRef=src.ref('src/seagallop.c','sSeag')),
        healLocation=dict(declaration=heal,sourceId=19,blackoutX=7,blackoutY=4,blackoutFacing='DIR_NORTH',sourceRef=src.ref('src/data/heal_locations.json',heal['id'])),
        mapHeaders=[dict(map=w['events']['id'],allowCycling=w['events']['allow_cycling'],allowEscaping=w['events']['allow_escaping'],allowRunning=w['events']['allow_running'],sourceRef=w['sourceRef']) for w in worlds],
        limitations=['Complete map/source field data only; application runtime admission is separate.',
            'Trainer Tower lobby/modes, HouseRoom2 e-Reader battles and Center2F remain explicit unsupported boundaries.',
            'Source closure includes guarded unsupported branches; it does not authorize runtime execution.',
            'Six renewable hidden pickups must use existing global 1500-step renewal rules, never permanent exhaustion.'])

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',required=True,type=Path)
    ap.add_argument('--dependency-root',type=Path,default=ROOT);ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--check',action='store_true');args=ap.parse_args()
    target=args.project_root.absolute();safe(target);target=target.resolve();dep=args.dependency_root.resolve();ref=args.reference_root.resolve()
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Source/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'seven_island_reader')
    reader.PRODUCT=dep;reader.TILESETS=TILESETS;reader.PROFILE=PROFILE
    src=reader.Source(ref);worlds=[reader.decode(src,n) for n in CORE]
    scripts=script_index(src,reader);scripts['profile']=PROFILE;scripts['sourcePaths']=sorted({b['sourceRef']['path'] for b in scripts['blocks']})
    links=topology(src,worlds)
    for p in ['src/seagallop.c','src/field_control_avatar.c','src/field_player_avatar.c','src/renewable_hidden_items.c',
              'src/heal_location.c','src/overworld.c','src/wild_encounter.c','src/data/heal_locations.json','src/data/heal_locations.constants.json.txt',
              'data/maps/map_groups.json','data/maps/VermilionCity/map.json','include/gba/defines.h','asm/macros/event.inc','tools/mapjson/mapjson.cpp']:
        src.read(p,'field-rule-reference')
    headers=['flags','vars','maps','map_scripts','event_objects','event_object_movement','opponents','trainers','species','moves','items','abilities','songs',
        'battle','battle_ai','battle_setup','menu','seagallop','metatile_labels','metatile_behaviors','cable_club','fame_checker','field_effects','field_weather',
        'game_stat','global','weather','help_system','pokemon','sound','union_room','daycare','field_tasks']
    for n in headers:src.read('include/constants/'+n+'.h','script-operand-constant')
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='seven-island-import-') as temp:
        stage=Path(temp)
        for n in CORE:shared.import_map(src,n,'map.'+reader.directory(n),stage/reader.directory(n))
        invisible=[dict(map=w['events']['id'],localId=i,event=e) for w in worlds for i,e in enumerate(w['events']['object_events'],1) if e['graphics_id']=='0']
        need(len(invisible)==2 and all(e['event']['movement_type']=='MOVEMENT_TYPE_INVISIBLE' for e in invisible), 'Invisible journal actor scope differs')
        npc_worlds=[dict(events=dict(object_events=[e for e in w['events']['object_events'] if e['graphics_id']!='0'])) for w in worlds]
        npcs=shared.npc_inventory(src,npc_worlds,stage/'npc');npcs['invisibleObjects']=invisible
        const=constants(src,shared,worlds,scripts)
        contract=field_contract(src,worlds,const,scripts,links)
        npcs['clones']=contract['clones']
        wanted={p['item'] for k in ['itemPickups','hiddenItems'] for p in contract[k]}
        stock=next(b for b in scripts['blocks'] if b['label']=='SevenIsland_Mart_Items')
        shop=re.findall(r'\bITEM_(?!NONE\b)\w+', '\n'.join(r['text'] for r in stock['lines']));wanted.update(shop)
        items=dict(items=[x for x in src.json('src/data/items.json')['items'] if x['itemId'] in wanted],shop=dict(label=stock['label'],items=shop,sourceRef=stock['sourceRef']))
        need({x['itemId'] for x in items['items']}==wanted,'Item source declarations differ')
        symbols={}
        tokens=set(re.findall(r'\bMAP_[A-Z0-9_]+\b',json.dumps(scripts)+json.dumps(contract)+json.dumps([w['events'] for w in worlds])))
        for p in src.tree:
            if p.startswith('data/maps/') and p.endswith('/map.json'):
                n=p.split('/')[2];symbol='MAP_'+re.sub(r'([a-z])([A-Z])',r'\1_\2',n).upper()
                if symbol in tokens:symbols[symbol]='map.'+reader.directory(n)
        catalog=[]
        for w in worlds:
            relative=reader.directory(w['sourceName']);p=stage/relative/'runtime-map.json';rich=p.read_bytes()
            compact=converter.encode(converter.normalize_runtime(json.loads(rich)));(stage/relative/'runtime-map.json').write_bytes(compact)
            catalog.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=relative,sourceMapSymbol=w['events']['id'],path=relative+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=CORE);lockbytes=canonical(lock)
        docs=package_documents(target / 'content/generated', const, {'catalog.json':dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
                runtimeIntegrated=False,maps=catalog,tilesets=TILESETS,mapSymbols=symbols,inputFiles=lock['files'],**links),
            'scripts.json':scripts,'npc-source.json':npcs,'contracts.json':contract,'items.json':items,
            'geometry.json':dict(maps=reader.geometry_inventory(worlds),tilesets=TILESETS)}, check=args.check)
        for n,d in docs.items():(stage/n).write_bytes(canonical(d))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(w['width']*w['height'] for w in worlds),warpCount=len(links['warps']),
            connectionCount=len(links['connections']),objectCount=sum(len(w['events']['object_events']) for w in worlds),npcGraphicsCount=len(npcs['graphics']),
            scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifestbytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifestbytes)
        writes=[(target/'content/sources/seven-island-region.lock.json',lockbytes)]+[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            safe(p)
            if args.check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))

if __name__=='__main__':main()
