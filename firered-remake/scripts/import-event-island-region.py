#!/usr/bin/env python3
"""Reproduce 24 pinned event-island maps and source field contracts; no upstream execution."""
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
PROFILE = 'firered-event-island-region-v1'
PACKAGE = 'event-island-region'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
CORE = ['BirthIsland_Exterior', 'BirthIsland_Harbor', 'NavelRock_1F', 'NavelRock_B1F', 'NavelRock_Base', 'NavelRock_BasePath_B10F', 'NavelRock_BasePath_B11F', 'NavelRock_BasePath_B1F', 'NavelRock_BasePath_B2F', 'NavelRock_BasePath_B3F', 'NavelRock_BasePath_B4F', 'NavelRock_BasePath_B5F', 'NavelRock_BasePath_B6F', 'NavelRock_BasePath_B7F', 'NavelRock_BasePath_B8F', 'NavelRock_BasePath_B9F', 'NavelRock_Exterior', 'NavelRock_Fork', 'NavelRock_Harbor', 'NavelRock_Summit', 'NavelRock_SummitPath_2F', 'NavelRock_SummitPath_3F', 'NavelRock_SummitPath_4F', 'NavelRock_SummitPath_5F']
SUPPORT = []
ANCHOR = ['VermilionCity']
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_NavelRock': 'secondary/navel_rock', 'gTileset_IslandHarbor': 'secondary/island_harbor', 'gTileset_SeviiIslands67': 'secondary/sevii_islands_67', 'gTileset_SeviiIslands123': 'secondary/sevii_islands_123'}

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
    roots={n for n,b in blocks.items() if any(b['path']==f'data/maps/{m}/scripts.inc' for m in CORE+SUPPORT)}
    for name in CORE+SUPPORT:
        m=src.json(f'data/maps/{name}/map.json')
        roots.update(e['script'] for k in ('object_events','coord_events','bg_events') for e in m[k]
                     if e.get('script') not in (None,'0','0x0','NULL'))
    roots.update(['VermilionCity_EventScript_FerrySailor', 'VermilionCity_EventScript_CheckTicketLeft', 'VermilionCity_EventScript_CheckTicketRight', 'VermilionCity_EventScript_ExitedTicketCheck', 'EventScript_ResetLegendaries'])
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
    for symbol in ['OBJ_EVENT_ID_CAMERA','OBJ_EVENT_ID_PLAYER']:resolve(symbol)
    healpath='src/data/heal_locations.json';template=src.text('src/data/heal_locations.constants.json.txt')
    need('HEAL_LOCATION_NONE,' in template and '{{ heal_location.id }},' in template, 'Heal enum differs')
    for i,h in enumerate(src.json(healpath)['heal_locations'],1):
        result['values'][h['id']]=dict(value=i,sourceRef=src.ref(healpath,h['id']))
    for alias, target in scripts['aliases'].items():
        if alias in operands and target in definitions:
            resolve(target)
            result['values'][alias]=result['values'][target]
    result['nonNumericOperandTokens']=sorted(operands-result['values'].keys()-{s for s in operands if s.startswith('MAP_')})
    need(set(result['nonNumericOperandTokens']) <= {'STR_VAR_1','STR_VAR_2','STR_VAR_3','MAP_NUM','MAP_GROUP'}, 'Unresolved operand constants '+str(result['nonNumericOperandTokens']))
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

def field_contract(src, worlds, const, links, npcs):
    maps={w['events']['id']:w for w in worlds}
    need(len(worlds)==24 and sum(len(w['events']['object_events']) for w in worlds)==8, 'Map/object scope differs')
    need(len(links['warps'])==44 and not links['connections'] and all(r['destinationInSlice'] and r['reciprocalIndex'] for r in links['warps']), 'Event island topology differs')
    coord=[dict(map=w['events']['id'],event=e,sourceRef=w['sourceRef']) for w in worlds for e in w['events']['coord_events']]
    need(len(coord)==1 and coord[0]['event']==dict(type='trigger',x=9,y=12,elevation=3,var='VAR_TEMP_1',var_value='0',script='NavelRock_Summit_EventScript_HoOh'), 'Ho-Oh coordinate trigger differs')
    hidden=[dict(map=w['events']['id'],event=e,item=e['item'],quantity=e['quantity'],renewable=False,requiresItemfinder=True,sourceRef=w['sourceRef']) for w in worlds for e in w['events']['bg_events'] if 'item' in e]
    need(len(hidden)==1 and hidden[0]['event']['underfoot'] and hidden[0]['item']=='ITEM_SACRED_ASH' and (hidden[0]['event']['x'],hidden[0]['event']['y'])==(9,11), 'Sacred Ash source differs')
    encounters=src.json('src/data/wild_encounters.json')
    active=set(maps)
    headers=[e for group in encounters['wild_encounter_groups'] for e in group.get('encounters',[]) if e.get('map') in active]
    need(not headers,'Event maps unexpectedly have normal random encounter headers')
    sea=src.text('src/seagallop.c');landings=[]
    for name in ['NAVEL_ROCK','BIRTH_ISLAND']:
        need(re.search(r'\[SEAGALLOP_'+name+r'\]\s*=\s*\{MAP\(MAP_'+name+r'_HARBOR\),\s+0x08, 0x05\}',sea),'Ferry landing differs')
        landings.append(dict(map='MAP_'+name+'_HARBOR',x=8,y=5,facing='DIR_SOUTH',sourceRef=src.ref('src/seagallop.c','sSeag')))
    control=src.text('src/field_control_avatar.c');finder=src.text('src/itemfinder.c')
    need('if (GetHiddenItemAttr(bgEvent->bgUnion.hiddenItem, HIDDEN_ITEM_UNDERFOOT) == TRUE)\n            return NULL;' in control,
        'Underfoot item must refuse facing interaction')
    need('GetHiddenItemAttr(events->bgEvents[i].bgUnion.hiddenItem, HIDDEN_ITEM_UNDERFOOT) == TRUE' in finder,
        'Itemfinder underfoot prerequisite differs')
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,maps=CORE,mapTopology=links,
        externalBoundaries=dict(warps=[],connections=[]),hiddenItems=hidden,coordinateTriggers=coord,
        ferryLandings=landings,randomEncounterHeaders=headers,
        mapHeaders=[dict(map=w['events']['id'],sourceRef=w['sourceRef'],**{k:w['events'][k] for k in ['music','weather','map_type','allow_cycling','allow_escaping','allow_running','requires_flash','show_map_name','floor_number','battle_scene']}) for w in worlds],
        dynamicGraphics=npcs['dynamicGraphics'],
        nativeCamera=dict(localId='LOCALID_CAMERA',sourceId=const['values']['LOCALID_CAMERA']['value'],initialMapObject=False),
        sacredAshBoundary=dict(underfoot=True,requiresItemfinder=True,runtimePickupAdmitted=False,
            reason='Source underfoot pickup requires Itemfinder; never expose as an ordinary facing pickup.',
            facingSourceRef=src.ref('src/field_control_avatar.c','GetInteractedBackgroundEventScript'),
            itemfinderSourceRef=src.ref('src/itemfinder.c','ItemUseOnFieldCB_Itemfinder')),
        limitations=['Source map/resource declarations only; application admission is separate.',
            'Ferry travel uses source specials, not synthetic map warps.',
            'Native camera is spawned by script specials and is not a ninth initial map object.',
            'Birth Island overworld uses DEOXYS_N through VAR_OBJ_GFX_ID_0; FireRed battle form is separate.'])

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',required=True,type=Path)
    ap.add_argument('--dependency-root',type=Path,default=ROOT);ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--check',action='store_true');args=ap.parse_args()
    target=args.project_root.absolute();safe(target);target=target.resolve();dep=args.dependency_root.resolve();ref=args.reference_root.resolve()
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Source/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'event_island_reader')
    reader.PRODUCT=dep;reader.TILESETS=TILESETS;reader.PROFILE=PROFILE
    src=reader.Source(ref);worlds=[reader.decode(src,n) for n in CORE]
    scripts=script_index(src,reader);scripts['profile']=PROFILE;scripts['sourcePaths']=sorted({b['sourceRef']['path'] for b in scripts['blocks']})
    need(len(scripts['blocks'])==125,'Exact event-island script closure differs')
    links=topology(src,worlds)
    for p in ['src/seagallop.c','src/field_control_avatar.c','src/field_player_avatar.c','src/item_use.c','src/itemfinder.c',
              'src/field_specials.c','src/field_effect.c','src/post_battle_event_funcs.c','src/event_object_movement.c',
              'src/wild_encounter.c','src/data/wild_encounters.json','src/data/items.json',
              'src/data/heal_locations.json','src/data/heal_locations.constants.json.txt',
              'data/maps/map_groups.json','data/maps/VermilionCity/map.json','include/gba/defines.h','asm/macros/event.inc','tools/mapjson/mapjson.cpp']:
        src.read(p,'field-rule-reference')
    headers=['flags','vars','maps','map_scripts','event_objects','event_object_movement','opponents','trainers','species','moves','items','abilities','songs',
        'battle','battle_ai','battle_setup','menu','seagallop','metatile_labels','metatile_behaviors','field_effects','field_weather',
        'game_stat','global','weather','help_system','pokemon','sound']
    for n in headers:src.read('include/constants/'+n+'.h','script-operand-constant')
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='event-island-import-') as temp:
        stage=Path(temp)
        for n in CORE:shared.import_map(src,n,'map.'+reader.directory(n),stage/reader.directory(n))
        npc_worlds=[dict(events=dict(object_events=[e for e in w['events']['object_events'] if e['graphics_id']!='OBJ_EVENT_GFX_VAR_0'])) for w in worlds]
        npc_worlds.append(dict(events=dict(object_events=[dict(graphics_id='OBJ_EVENT_GFX_DEOXYS_N')])))
        npcs=shared.npc_inventory(src,npc_worlds,stage/'npc')
        npcs['dynamicGraphics']=[dict(map='MAP_BIRTH_ISLAND_EXTERIOR',localId=2,sourceGraphics='OBJ_EVENT_GFX_VAR_0',variable='VAR_OBJ_GFX_ID_0',graphicsId='OBJ_EVENT_GFX_DEOXYS_N',script='BirthIsland_Exterior_OnTransition')]
        npcs['invisibleObjects']=[]
        const=constants(src,shared,worlds,scripts)
        contract=field_contract(src,worlds,const,links,npcs)
        wanted={'ITEM_SACRED_ASH','ITEM_MYSTIC_TICKET','ITEM_AURORA_TICKET','ITEM_ITEMFINDER'}
        items=dict(items=[x for x in src.json('src/data/items.json')['items'] if x['itemId'] in wanted])
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
        writes=[(target/'content/sources/event-island-region.lock.json',lockbytes)]+[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            safe(p)
            if args.check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))

if __name__=='__main__':main()
