#!/usr/bin/env python3
"""Authenticate original Good Rod service, one compact house and 22 loaded ordinary Kanto tables.

Only hash-pinned local source/map/icon codecs execute. Upstream Git blobs remain inert.
Safari tables and unloaded Kanto maps remain source-only; no runtime admission occurs here.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse
import base64
import collections
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'good-rod-service'
RESOURCE_ID = 'firered-good-rod-service-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAP = 'FuchsiaCity_House2'
MAP_ID = 'map.fuchsia-city-house-2'
DIRECTORY = 'fuchsia-city-house-2'
TILESETS = {'gTileset_Building':'primary/building', 'gTileset_Museum':'secondary/museum'}
CITY_PACKAGE = 'content/generated/fuchsia-route-region'
CITY_RESOURCE = 'fuchsia-city/runtime-map.json'
NPC_REUSE = {'OBJ_EVENT_GFX_FISHER', 'OBJ_EVENT_GFX_RED_NORMAL'}
ORDINARY_SYMBOLS = ('MAP_CELADON_CITY','MAP_CERULEAN_CAVE_1F','MAP_CERULEAN_CAVE_B1F','MAP_CERULEAN_CITY',
 'MAP_CINNABAR_ISLAND','MAP_FUCHSIA_CITY','MAP_PALLET_TOWN','MAP_ROUTE10','MAP_ROUTE11','MAP_ROUTE12','MAP_ROUTE13',
 'MAP_ROUTE21_NORTH','MAP_ROUTE21_SOUTH','MAP_ROUTE22','MAP_ROUTE23','MAP_ROUTE24','MAP_ROUTE25','MAP_ROUTE4','MAP_ROUTE6',
 'MAP_SSANNE_EXTERIOR','MAP_VERMILION_CITY','MAP_VIRIDIAN_CITY')
SOURCE_ONLY_SYMBOLS = ('MAP_ROUTE19','MAP_ROUTE20','MAP_SAFARI_ZONE_CENTER','MAP_SAFARI_ZONE_EAST',
 'MAP_SAFARI_ZONE_NORTH','MAP_SAFARI_ZONE_WEST','MAP_SEAFOAM_ISLANDS_B3F','MAP_SEAFOAM_ISLANDS_B4F')


def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name,path); value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value); return value

def declaration(src, path, symbol, macro=False):
    text=src.text(path)
    pattern=(r'^\t\.macro '+re.escape(symbol)+r'[^\n]*\n.*?^\t\.endm' if macro else
             r'^(?:static )?[^\n;]+\b'+re.escape(symbol)+r'\([^;\n]*\)\n\{.*?^\}')
    matches=list(re.finditer(pattern,text,re.M|re.S));need(len(matches)==1,'Missing/ambiguous source declaration: '+symbol)
    m=matches[0];return dict(symbol=symbol,sourceText=m[0],sourceRef=src.ref(path,symbol,text[:m.start()].count('\n')+1))

def fishing_tables(src):
    path='src/data/wild_encounters.json'; raw=src.read(path,'good-rod-source-tables')
    groups=json.loads(raw)['wild_encounter_groups']; group=next(g for g in groups if g['label']=='gWildMonHeaders')
    field=next(g for g in group['fields'] if g['type']=='fishing_mons')
    need(field['groups']['good_rod'] == [2, 3, 4] and field['encounter_rates'] == [70, 30, 60, 20, 20, 40, 40, 15, 4, 1], 'Source fishing groups differ')
    candidates=[e for e in group['encounters'] if e['base_label'].endswith('_FireRed') and 'fishing_mons' in e and
                not e['map'].startswith(tuple('MAP_'+x+'_' for x in ['ONE','TWO','THREE','FOUR','FIVE','SIX','SEVEN']))]
    need({e['map'] for e in candidates}==set(ORDINARY_SYMBOLS+SOURCE_ONLY_SYMBOLS), 'Kanto source fishing cohort differs')
    def table(e):
        stable='map.'+e['map'].removeprefix('MAP_').lower().replace('_','-');stable=re.sub(r'route(\d+)',r'route-\1',stable)
        return dict(map=dict(value=stable),sourceCommit=COMMIT,sourcePath=path,sourceSymbol=e['base_label'],
          rawEncounterRate=e['fishing_mons']['encounter_rate'],slots=[dict(index=i,species=dict(value='pret:'+m['species']),
          minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=field['encounter_rates'][i])
          for i,m in enumerate(e['fishing_mons']['mons']) if i in field['groups']['good_rod']])
    tables=sorted([table(e) for e in candidates if e['map'] in ORDINARY_SYMBOLS],key=lambda t:t['map']['value'])
    source_only=sorted([table(e) for e in candidates if e['map'] in SOURCE_ONLY_SYMBOLS],key=lambda t:t['map']['value'])
    need(len(tables)==22 and len(source_only)==8 and {s['species']['value'] for t in tables for s in t['slots']}==
         {'pret:SPECIES_MAGIKARP','pret:SPECIES_POLIWAG','pret:SPECIES_GOLDEEN','pret:SPECIES_HORSEA','pret:SPECIES_KRABBY'}, 'Good Rod species scope differs')
    species_source=src.text('src/data/pokemon/species_info.h')
    held_items=[]
    for symbol in sorted({s['species']['value'][5:] for t in tables for s in t['slots']}):
        m=re.search(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',species_source,re.M|re.S)
        need(m is not None and '.itemCommon = ITEM_NONE,' in m[1] and '.itemRare = ITEM_NONE,' in m[1], 'Good Rod species held item differs: '+symbol)
        held_items.append(dict(species=symbol,common='ITEM_NONE',rare='ITEM_NONE',sourceRef=src.ref('src/data/pokemon/species_info.h',symbol,species_source[:m.start()].count('\n')+1)))
    return dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,rod='good',tables=tables,sourceOnlyTables=source_only), held_items

def item_inventory(src, constants, stage, dep):
    codec=module(dep / 'scripts/import-lab-battle-ui.py', 'good_rod_icon_codec')
    symbol='ITEM_GOOD_ROD'; p='src/data/items.json'; original=next(i for i in src.json(p)['items'] if i['itemId']==symbol)
    def ref(path,symbol,match=None):
        text=src.text(path) if not path.endswith('.png') else ''
        return dict(path=path,symbol=symbol,startLine=1 if match is None else text.count('\n',0,match.start())+1,
                    endLine=max(1,text.count('\n')) if match is None else text.count('\n',0,match.end())+1)
    source=ref(p,symbol);secondary=original['secondaryId'];secondary=constants['values'][secondary]['value'] if isinstance(secondary,str) else secondary
    item=dict(id=dict(value='pret:'+symbol),sourceItemSymbol=symbol,sourceItemId=constants['values'][symbol]['value'],name=original['english'],
      price=original['price'],importance=original['importance'],registrability=original['registrability'],pocket=original['pocket'],type=str(original['type']),
      holdEffect=original['holdEffect'],holdEffectParam=original['holdEffectParam'],fieldUseFunction=original['fieldUseFunc'],battleUsage=original['battleUsage'],
      battleUseFunction=original['battleUseFunc'],secondaryId=secondary,canToss=original['importance']==0,canSell=original['price']!=0,canGive=original['importance']==0,source=source)
    need(item['sourceItemId']==263 and secondary==1 and item['fieldUseFunction']=='FieldUseFunc_Rod' and item['pocket']=='POCKET_KEY_ITEMS', 'Good Rod item identity differs')
    desc=original['description_english'].replace('\\n','\n');p='src/data/item_icon_table.h';match=re.search(r'^    \['+symbol+r'\]\s*=\s*\{(\w+), (\w+)\},',src.text(p),re.M)
    need(match is not None,'Missing Good Rod icon');binding=ref(p,symbol,match);paths=[]
    for token in match.groups():
        m=re.search(r'\b'+token+r'\[\]\s*= INCBIN_U32\("(.*?)"\)',src.text('src/data/graphics/items.h'));need(m is not None,'Missing Good Rod icon asset')
        paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
    image,palette=paths;w,h,idx,_=codec.indexed_png(src.read(image));rgba=codec.rgba(idx,codec.palette(src.read(palette)));need((w,h)==(24,24),'Good Rod icon size differs')
    png=codec.png(w,h,rgba);filename='item_good_rod.png';(stage/filename).write_bytes(png)
    icon=dict(item=item['id'],width=w,height=h,pngPath=filename,
              source=ref(image,match[1]),paletteSource=ref(palette,match[2]),bindingSource=binding)
    return dict(schemaVersion=1,profile=RESOURCE_ID,sourceCommit=COMMIT,items=[dict(item=item,description=desc)],icons=[icon])

def retained_city(src,current):
    manifest=(current/CITY_PACKAGE/'manifest.json').read_bytes()
    row=next(e for e in json.loads(manifest)['outputs'] if e['path']==CITY_RESOURCE);raw=(current/CITY_PACKAGE/CITY_RESOURCE).read_bytes()
    original=src.json('data/maps/FuchsiaCity/map.json');need(json.loads(raw)['events']==original,'Retained Fuchsia source events differ')
    entrances=[dict(index=i,event=e) for i,e in enumerate(original['warp_events']) if e['dest_map']=='MAP_FUCHSIA_CITY_HOUSE2']
    need([(e['index'],e['event']['x'],e['event']['y'],e['event']['dest_warp_id']) for e in entrances]==[(7,38,31,'1'),(8,39,28,'3'),(9,39,29,'3')],'Fuchsia house entrance topology differs')
    return dict(map='map.fuchsia-city',operation='reuse-existing-source-map',currentPackage=CITY_PACKAGE,
                currentResource=row,entrances=entrances,geometryChanges=[],eventChanges=[])

def script_inventory(src, reader, worlds):
    """Bounded map/event/standard-script closure, with original source references."""
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or
              any(p in (f'data/maps/{n}/scripts.inc', f'data/maps/{n}/text.inc') for n in [MAP]))]
    paths += ['data/event_scripts.s']
    for path in sorted(paths):
        text = reader.git(src.reference, 'cat-file', 'blob', src.tree[path][2]).decode()
        current, macro = None, False
        for line, raw in enumerate(text.splitlines(), 1):
            code = raw.split('@')[0].strip()
            if code.startswith('.macro'): macro, current = True, None; continue
            if code == '.endm': macro = False; continue
            if macro: continue
            found = re.fullmatch(r'(\w+)::?', code)
            if found:
                current = found[1]
                need(current not in blocks, 'Duplicate script label: ' + current)
                blocks[current] = dict(label=current, path=path, line=line, lines=[])
            elif current: blocks[current]['lines'].append(dict(line=line, text=raw))
            found = re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)', code)
            if found: aliases[found[1]] = found[2]
    roots = {label for label, b in blocks.items() if b['path'] in [f'data/maps/{n}/scripts.inc' for n in [MAP]]}
    roots.update(e['script'] for w in worlds for kind in ('object_events', 'coord_events', 'bg_events')
                 for e in w['events'][kind] if e.get('script') not in (None, '0', '0x0', 'NULL'))
    implicit = ['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo', 'Std_ReceivedItem', 'Std_PutItemAway']
    roots.update(implicit)
    pending, selected = sorted(roots), set()
    while pending:
        label = aliases.get(pending[-1], pending[-1]); pending.pop()
        if label in selected: continue
        need(label in blocks, 'Missing script label: ' + label)
        selected.add(label)
        for row in blocks[label]['lines']:
            code = re.sub(r'"[^"\n]*"', '', row['text'].split('@')[0])
            for token in re.findall(r'\b[A-Za-z_]\w*\b', code):
                token = aliases.get(token, token)
                if token in blocks and token not in selected: pending.append(token)
    result, opcodes, specials, unresolved = [], collections.Counter(), collections.Counter(), []
    for label in sorted(selected):
        b = blocks[label]
        src.read(b['path'], 'script-label-closure')
        result.append(dict(label=label, sourceRef=src.ref(b['path'], label, b['line']), lines=b['lines']))
        for row in b['lines']:
            code = row['text'].split('@')[0].strip()
            if not code or code.startswith(('.', '#')): continue
            op, _, operands = code.partition(' ')
            if '\t' in op: op, operands = code.split(None, 1)
            opcodes[op] += 1
            if op in ('special', 'specialvar'): specials[operands.split(',')[-1].strip()] += 1
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'): target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement', 'case') or op.startswith(('goto_if_', 'call_if_')): target = operands.split(',')[-1].strip()
            elif op in ('msgbox', 'giveitem_msg', 'msgreceiveditem'): target = operands.split(',')[0].strip()
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(label=label, line=row['line'], target=target))
            if op == 'trainerbattle_single':
                for target in operands.split(',')[1:]:
                    target = target.strip()
                    if aliases.get(target, target) not in blocks:
                        unresolved.append(dict(label=label, line=row['line'], target=target))
    need(not unresolved, 'Unresolved script labels: ' + repr(unresolved))
    used_aliases = {a: b for a, b in aliases.items() if b in selected}
    return dict(schemaVersion=1, executable=False, roots=sorted(roots), implicitStandardRoots=implicit,
                sourcePaths=sorted({b['sourceRef']['path'] for b in result}),
                implicitSpecials=[],
                blocks=result, aliases=used_aliases, opcodeCounts=dict(sorted(opcodes.items())),
                specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path,required=True);p.add_argument('--dependency-root',type=Path,default=ROOT)
    p.add_argument('--current-project-root',type=Path);p.add_argument('--project-root',type=Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
    target,dep,ref,current=[Path(os.path.abspath(x)) for x in [a.project_root,a.dependency_root,a.reference_root,a.current_project_root or a.dependency_root]]
    for path in [target,dep,ref,current]:safe(path)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Reference/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'good_rod_reader');reader.PRODUCT,reader.TILESETS,reader.PROFILE=dep,TILESETS,RESOURCE_ID
    util=module(dep / 'scripts/import-fossil-lab-region.py', 'good_rod_source_utils');util.MAPS=[MAP]
    src=reader.Source(ref);world=reader.decode(src,MAP);world['stableId']=MAP_ID
    retained=retained_city(src,current)
    scripts=script_inventory(src,reader,[world])
    constants=util.constants_inventory(src,dict(scripts,blocks=scripts['blocks']+[dict(lines=[dict(text='ITEM_GOOD_ROD FLAG_GOT_GOOD_ROD GOOD_ROD')])]),[world])
    for name,value in [('ITEM_GOOD_ROD',263),('FLAG_GOT_GOOD_ROD',0x244),('GOOD_ROD',1)]:need(constants['values'][name]['value']==value,'Good Rod constant differs: '+name)
    for leaf in ['scripts.inc','text.inc']:src.read(f'data/maps/{MAP}/'+leaf,'live-map-script')
    src.read('data/maps/map_groups.json','source-map-number-projection')
    links=reader.topology(src,[world])
    for entrance in retained['entrances']:
        e=entrance['event'];j=int(e['dest_warp_id']);end=world['events']['warp_events'][j]
        links['warps'].append(dict(sourceMap='MAP_FUCHSIA_CITY',sourceIndex=entrance['index'],source=e,destinationMap='MAP_FUCHSIA_CITY_HOUSE2',destinationIndex=j,destination=end,dynamic=False,reciprocalIndex=int(end['dest_warp_id'])==entrance['index']))
    tables,held_items=fishing_tables(src)
    bodies={b['label']:'\n'.join(r['text'] for r in b['lines']) for b in scripts['blocks']};prefix=MAP+'_EventScript_'
    give=bodies[prefix+'GiveGoodRod'];effects=['checkitemspace ITEM_GOOD_ROD','giveitem_msg FuchsiaCity_House2_Text_ReceivedGoodRod, ITEM_GOOD_ROD','msgbox FuchsiaCity_House2_Text_GoodRodCanCatchBetterMons','setflag FLAG_GOT_GOOD_ROD']
    need(all(x in give for x in effects) and [give.index(x) for x in effects]==sorted(give.index(x) for x in effects),'Good Rod grant order differs')
    declarations=[declaration(src,'src/item_use.c',n) for n in ['FieldUseFunc_Rod','CanFish','ItemUseOnFieldCB_Rod']]
    declarations += [declaration(src,'src/field_player_avatar.c','Fishing'+str(i)) for i in range(2,11)]
    declarations += [declaration(src,'src/wild_encounter.c',n) for n in ['ChooseWildMonIndex_Fishing','FishingWildEncounter']]
    by_symbol={d['symbol']:d['sourceText'] for d in declarations}
    need('const s16 arr2[] = {1, 3, 6};' in by_symbol['Fishing2'] and 'task->tStep += 3;' in by_symbol['Fishing7'] and 'gMain.newKeys' not in by_symbol['Fishing7'], 'Good Rod round bound or skipped reel window differs')
    macros=[declaration(src,'asm/macros/event.inc',n,True) for n in ['giveitem_msg','msgreceiveditem']]
    contract = dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, runtimeIntegrated=False, retainedCity=retained, mapTopology=links, service=dict(id='city-service.good-rod', mapId=MAP_ID, entryLabel=prefix + 'FishingGurusBrother', localObjectId=1, actor=world['events']['object_events'][0], giftItem='ITEM_GOOD_ROD', itemId=263, quantity=1, flag='FLAG_GOT_GOOD_ROD', flagValue=580, choice='MSGBOX_YESNO', successChoice='YES', capacityCheckBeforeMutation=True, noRoomPreservesFlagAndInventory=True, prerequisiteFlags=[], orderedEffects=effects, successLabel=prefix + 'GiveGoodRod', declineText=MAP + '_Text_OhThatsDisappointing', noRoomLabel=prefix + 'NoRoomForGoodRod', revisitLabel=prefix + 'AlreadyGotGoodRod'), fishing=dict(rod='good', secondaryId=1, minimumRoundDrawBound=3, slots=[2, 3, 4], weights=[60, 20, 20], reelReactionReachable=False, ordinaryTableCount=22, sourceOnlyTableCount=8, sourceOnlyReasons={'safari-zone': 'separate Safari battle/capture rules', 'route-19/route-20/seafoam': 'maps not loaded'}, heldItemDrawBound=100, heldItems=held_items), declarations=declarations, receiptMacros=macros, limitations=['Source/resource package only; current-game admission is separate.', 'Safari tables and unloaded Route19, Route20 and Seafoam tables are source-only.', 'No Super Rod, Magikarp record service, Sevii Good Rod rollout, or new shop layer.', 'NPC and player art refer to existing authenticated resources.'])
    need((world['width'],world['height'],len(world['cells']),len(world['borderBlocks']),len(world['events']['warp_events']))==(10,10,100,4,4),'Good Rod house geometry differs')
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE,shared.TILESETS=RESOURCE_ID,TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='good-rod-',dir=target) as temp:
        stage=Path(temp);shared.import_map(src,MAP,MAP_ID,stage/DIRECTORY)
        npcs=shared.npc_inventory(src,[world],stage/'npc');need({r['graphicsId'] for r in npcs['graphics']}==set(NPC_REUSE),'Good Rod actor cohort differs')
        items=item_inventory(src,constants,stage,dep)
        path=stage/DIRECTORY/'runtime-map.json';rich=path.read_bytes();compact=converter.normalize_runtime(json.loads(rich));util.compact_check(compact,world,src)
        data=converter.encode(compact);(stage/DIRECTORY/'runtime-map.json').write_bytes(data)
        entry=dict(sourceName=MAP,stableId=MAP_ID,directory=DIRECTORY,sourceMapSymbol=world['events']['id'],path=DIRECTORY+'/runtime-map.json')
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=[MAP],completeGame=False);lock_bytes=canonical(lock)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,maps=[entry],tilesets=TILESETS,mapSymbols={'MAP_FUCHSIA_CITY_HOUSE2':MAP_ID,'MAP_FUCHSIA_CITY':'map.fuchsia-city'}),
              'scripts.json':scripts,'contracts.json':contract,'npc-source.json':npcs,'items.json':items,'good-rod-tables.json':tables,
              'geometry.json':dict(maps=reader.geometry_inventory([world]),tilesets=TILESETS)}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=RESOURCE_ID,resourceId=RESOURCE_ID,sourceCommit=COMMIT,
          inputCount=len(src.entries),mapCount=1,cellCount=100,borderCellCount=4,directedWarpCount=7,objectCount=1,reusedNpcGraphicsCount=2,itemCount=1,
          ordinaryGoodRodTableCount=22,sourceOnlyTableCount=8,scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_bytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifest_bytes)
        writes=[(target / 'scripts/good-rod-service.contract.json', canonical(contract))]
        writes += [(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if path.suffix=='.json':json.loads(data)
            if a.check:need(path.is_file() and path.read_bytes()==data,'Regeneration differs: '+str(path))
        if not a.check:
            for path,data in writes:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        print(json.dumps(dict(
             inputCount=len(src.entries),outputCount=len(outputs),scriptBlockCount=len(scripts['blocks']),ordinaryTables=22,sourceOnlyTables=8,check=a.check),indent=2))
if __name__=='__main__':main()
