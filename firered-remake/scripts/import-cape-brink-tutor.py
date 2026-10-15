#!/usr/bin/env python3
"""Authenticate Cape Brink and its ultimate move tutor, retaining the unchanged Two Island connection.

Pinned upstream Git blobs are inert input data. Only the reviewed local map
renderer, compact converter and constants reader execute. Teaching business
rules and current-game admission are separate from this resource package.
"""
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
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'cape-brink-tutor'
RESOURCE_ID = 'firered-cape-brink-tutor-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAPS = ['TwoIsland_CapeBrink', 'TwoIsland_CapeBrink_House']
TILESETS = {'gTileset_General':'primary/general', 'gTileset_SeviiIslands123':'secondary/sevii_islands_123',
 'gTileset_Building':'primary/building', 'gTileset_GenericBuilding2':'secondary/generic_building_2'}
ANCHOR_PACKAGE = 'content/generated/first-sevii-region'
NPC_REUSE = {
 'OBJ_EVENT_GFX_CHANNELER': 'content/generated/saffron-gym-region/npc/channeler.png',
 'OBJ_EVENT_GFX_RED_NORMAL': 'content/generated/city-services/npc/red-normal.png'}

def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def script_inventory(src, reader, worlds):
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or p.endswith('/text.inc') or any(p == f'data/maps/{name}/scripts.inc' for name in MAPS))]
    for path in sorted(paths + ['data/event_scripts.s']):
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
    roots = {label for label, b in blocks.items() if b['path'] in {f'data/maps/{name}/scripts.inc' for name in MAPS}}
    roots.update(e['script'] for world in worlds for kind in ('object_events', 'coord_events', 'bg_events')
                 for e in world['events'][kind] if e.get('script') not in (None, '0', '0x0', 'NULL'))
    implicit = ['Std_MsgboxNPC', 'Std_MsgboxSign', 'Std_MsgboxDefault', 'Std_MsgboxYesNo', 'EventScript_HiddenItemScript', 'Std_FindItem', 'Std_PutItemAway']
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
        block = blocks[label]
        src.read(block['path'], 'script-label-closure')
        result.append(dict(label=label, sourceRef=src.ref(block['path'], label, block['line']), lines=block['lines']))
        for row in block['lines']:
            code = row['text'].split('@')[0].strip()
            if not code or code.startswith(('.', '#')): continue
            parts = code.split(None, 1); op = parts[0]; operands = parts[1] if len(parts) == 2 else ''
            opcodes[op] += 1
            if op in ('special', 'specialvar'): specials[operands.split(',')[-1].strip()] += 1
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'): target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement', 'case') or op.startswith(('goto_if_', 'call_if_')): target = operands.split(',')[-1].strip()
            elif op == 'msgbox': target = operands.split(',')[0].strip()
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(label=label, line=row['line'], target=target))
    need(not unresolved, 'Unresolved script labels: ' + repr(unresolved))
    return dict(schemaVersion=1, executable=False, roots=sorted(roots), implicitStandardRoots=implicit,
                sourcePaths=sorted({b['sourceRef']['path'] for b in result}), blocks=result,
                aliases={a: b for a, b in aliases.items() if b in selected},
                opcodeCounts=dict(sorted(opcodes.items())), specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def declaration(src, path, symbol):
    value=src.text(path)
    match=re.search(r'^(?:static )?[^\n;]+\b'+re.escape(symbol)+r'\([^;\n]*\)\n\{.*?^\}',value,re.M|re.S)
    need(match is not None, 'Missing native declaration: '+symbol)
    return dict(symbol=symbol,sourceText=match[0],sourceRef=src.ref(path,symbol,value[:match.start()].count('\n')+1))


def native_inventory(src):
    groups={'src/field_specials.c':['GetLeadMonIndex','CapeBrinkGetMoveToTeachLeadPokemon','HasLearnedAllMovesFromCapeBrinkTutor'],
      'src/party_menu.c':['GetTutorMove','CanLearnTutorMove','CanMonLearnTMTutor','ChooseMonForMoveTutor','TryTutorSelectedMon','Task_LearnedMove','Task_PartyMenuReplaceMove','DisplayLearnMoveMessageAndClose'],
      'src/scrcmd.c':['ScrCmd_bufferleadmonspeciesname']}
    rows=[declaration(src,path,symbol) for path,symbols in groups.items() for symbol in symbols]
    field=src.text('src/field_specials.c');table=re.search(r'static const u8 sCapeBrinkCompatibleSpecies\[\] = \{(.*?)\};',field,re.S)
    need(re.findall('SPECIES_[A-Z]+',table[1])==['SPECIES_VENUSAUR','SPECIES_CHARIZARD','SPECIES_BLASTOISE'],'Starter species/order differs')
    rows.append(dict(symbol='sCapeBrinkCompatibleSpecies',sourceText=table[0],sourceRef=src.ref('src/field_specials.c','sCapeBrinkCompatibleSpecies',field[:table.start()].count('\n')+1)))
    src.read('include/constants/party_menu.h');src.read('src/data/battle_moves.h');src.read('src/data/pokemon/species_info.h')
    return dict(schemaVersion=1,declarations=rows,requiredNativeBindings=['bufferleadmonspeciesname','CapeBrinkGetMoveToTeachLeadPokemon','HasLearnedAllMovesFromCapeBrinkTutor'],
      reusedBindings=['ChooseMonForMoveTutor','QuestLog_CutRecording','DisableMsgBoxWalkaway','SetHiddenItemFlag'],
      sourceSemantics=dict(lead='first non-egg non-empty party slot, including fainted',friendship=255,cost=0,
        menu='fixed lead index for tutor indices 15 through 17',successfulTeach='one persistent flag per move; all-three flag after all three',
        afterPartialSuccess='FLAG_TEMP_2 until original temporary flag reset on leaving map',alreadyKnown='offer gate allows it; shared teaching rejects without consuming a flag',
        ownership='no starter-choice, original-trainer, gender, National Dex, or level check in this service'))


def retained_anchor(src, current):
    manifest=(current/ANCHOR_PACKAGE/'manifest.json').read_bytes()
    resource=next(r for r in json.loads(manifest)['outputs'] if r['path']=='two-island/runtime-map.json')
    raw=(current/ANCHOR_PACKAGE/resource['path']).read_bytes()
    original=src.json('data/maps/TwoIsland/map.json');need(json.loads(raw)['events']==original,'Two Island source events differ')
    connection=next(c for c in original['connections'] if c['map']=='MAP_TWO_ISLAND_CAPE_BRINK')
    need(connection==dict(map='MAP_TWO_ISLAND_CAPE_BRINK',direction='up',offset=24),'Two Island northern connection differs')
    return dict(sourceName='TwoIsland',map='map.two-island',sourceMap='MAP_TWO_ISLAND',operation='reuse-existing-source-map',
      currentPackage=ANCHOR_PACKAGE,currentResource=resource,
      connection=connection,geometryChanges=[],eventChanges=[])


def encounters_items(src, worlds):
    path='src/data/wild_encounters.json';g=next(g for g in src.json(path)['wild_encounter_groups'] if g['label']=='gWildMonHeaders')
    matches=[e for e in g['encounters'] if e['map']=='MAP_TWO_ISLAND_CAPE_BRINK' and e['base_label'].endswith('_FireRed')]
    need(len(matches)==1,'Cape Brink encounter table differs');row=matches[0]
    need([len(row[k]['mons']) for k in ['land_mons','water_mons','fishing_mons']]==[12,5,10],'Cape Brink encounter slot count differs')
    species=sorted({m['species'] for k in ['land_mons','water_mons','fishing_mons'] for m in row[k]['mons']})
    items=worlds[0]['events']['bg_events'];symbols={e['item'] for e in items}
    declarations=[i for i in src.json('src/data/items.json')['items'] if i['itemId'] in symbols]
    need(symbols=={'ITEM_PP_MAX','ITEM_RARE_CANDY'} and len(declarations)==2,'Cape Brink hidden items differ')
    moves=[];battle=src.text('src/data/battle_moves.h')
    for symbol in ['MOVE_FRENZY_PLANT','MOVE_BLAST_BURN','MOVE_HYDRO_CANNON']:
        m=re.search(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',battle,re.M|re.S)
        need(m is not None,'Missing ultimate move');fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
        need(fields['effect']=='EFFECT_RECHARGE' and fields['pp']=='5' and fields['power']=='150' and fields['accuracy']=='90','Ultimate move source differs')
        moves.append(dict(symbol=symbol,fields=fields,sourceRef=src.ref('src/data/battle_moves.h',symbol,battle[:m.start()].count('\n')+1)))
    return dict(schemaVersion=1,sourceCommit=COMMIT,fields=g['fields'],encounters=matches,wildSpecies=species,
      sourceRef=src.ref(path,'sTwoIslandCapeBrink_FireRed'),hiddenItems=items,itemDeclarations=declarations,
      itemSourceRef=src.ref('src/data/items.json','ITEM_PP_MAX and ITEM_RARE_CANDY'),moves=moves,
      runtimeAdmission=False,notes=['All 13 encounter species already occur in the existing owned catalog; verify exact ownership at composition.',
      'Surf/Old Rod/Good Rod/Super Rod use their existing shared rules; no new species or items are introduced here.'])


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path,required=True);p.add_argument('--dependency-root',type=Path,default=ROOT)
    p.add_argument('--current-project-root',type=Path);p.add_argument('--project-root',type=Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
    target,dep,ref,current=[Path(os.path.abspath(x)) for x in [a.project_root,a.dependency_root,a.reference_root,a.current_project_root or a.dependency_root]]
    for path in [target,dep,ref,current]:safe(path)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Reference/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'cape_brink_reader');reader.PRODUCT,reader.TILESETS,reader.PROFILE=dep,TILESETS,RESOURCE_ID
    util=module(dep / 'scripts/import-fossil-lab-region.py', 'cape_brink_utils');util.MAPS=MAPS
    src=reader.Source(ref);worlds=[reader.decode(src,n) for n in MAPS];anchor=retained_anchor(src,current)
    scripts=script_inventory(src,reader,worlds)
    extra='MOVETUTOR_FRENZY_PLANT MOVETUTOR_BLAST_BURN MOVETUTOR_HYDRO_CANNON MOVE_FRENZY_PLANT MOVE_BLAST_BURN MOVE_HYDRO_CANNON FLAG_TUTOR_FRENZY_PLANT FLAG_TUTOR_BLAST_BURN FLAG_TUTOR_HYDRO_CANNON SPECIES_VENUSAUR SPECIES_CHARIZARD SPECIES_BLASTOISE'
    constants=util.constants_inventory(src,dict(scripts,blocks=scripts['blocks']+[dict(lines=[dict(text=extra)])]),worlds)
    for name,value in [('MOVETUTOR_FRENZY_PLANT',15),('MOVETUTOR_BLAST_BURN',16),('MOVETUTOR_HYDRO_CANNON',17),('LOCALID_CAPE_BRINK_TUTOR',1)]:need(constants['values'][name]['value']==value,'Tutor constant differs: '+name)
    native=native_inventory(src);inventory=encounters_items(src,worlds);src.read('data/maps/map_groups.json')
    topology=reader.topology(src,worlds)
    topology['connections'].append(dict(sourceMap='MAP_TWO_ISLAND',reciprocal=True,destinationConnections=worlds[0]['events']['connections'],**anchor['connection']))
    need([(w['width'],w['height']) for w in worlds]==[(24,40),(11,9)],'Cape Brink geometry differs')
    need(all(w['events']['coord_events']==[] and len(w['events']['warp_events'])==1 for w in worlds),'Cape Brink event scope differs')
    need(worlds[0]['events']['object_events']==[] and len(worlds[1]['events']['object_events'])==1,'Tutor actor count differs')
    actor=worlds[1]['events']['object_events'][0];need((actor['graphics_id'],actor['x'],actor['y'],actor['script'])==('OBJ_EVENT_GFX_CHANNELER',4,4,'TwoIsland_CapeBrink_House_EventScript_StarterTutor'),'Tutor actor differs')
    choices=[dict(species=s,move=m,tutorIndex=i,flag=f) for s,m,i,f in [('SPECIES_VENUSAUR','MOVE_FRENZY_PLANT',15,'FLAG_TUTOR_FRENZY_PLANT'),('SPECIES_CHARIZARD','MOVE_BLAST_BURN',16,'FLAG_TUTOR_BLAST_BURN'),('SPECIES_BLASTOISE','MOVE_HYDRO_CANNON',17,'FLAG_TUTOR_HYDRO_CANNON')]]
    contract=dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,retainedAnchors=[anchor],topology=topology,
      tutor=dict(map=worlds[1]['stableId'],localId=1,event=actor,entryScript=actor['script'],choices=choices,
        allMovesFlag='FLAG_LEARNED_ALL_MOVES_AT_CAPE_BRINK',temporaryFlag='FLAG_TEMP_2',semantics=native['sourceSemantics']),
      maps=[dict(map=w['stableId'],sourceMap=w['events']['id'],width=w['width'],height=w['height'],layout=w['events']['layout'],events=w['events']) for w in worlds],
      limitations=['Source/content and Domain service candidate; no Current, Application, Godot, route or UI admission.',
      'Preserve Two Island quests and original northern edge; no replacement NPC or transfer point.',
      'Native opcode/special integration is enumerated in native.json; shared move-learning, encounters, hidden-item and temporary-flag owners must be reused.'])
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE,shared.TILESETS=RESOURCE_ID,TILESETS;converter=reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='cape-brink-',dir=target) as temp:
        stage=Path(temp)
        for w in worlds:shared.import_map(src,w['sourceName'],w['stableId'],stage/reader.directory(w['sourceName']))
        npcs=shared.npc_inventory(src,worlds,stage/'npc');need({r['graphicsId'] for r in npcs['graphics']}==set(NPC_REUSE),'NPC graphics cohort differs')
        for row in npcs['graphics']:
            path=NPC_REUSE[row['graphicsId']];existing=current/path;safe(existing);rendered=stage/row['texturePath']
            need(rendered.read_bytes() == existing.read_bytes(),'Reused sprite differs')
            row.update(texturePath=path,reuseExisting=True);rendered.unlink()
        (stage/'npc').rmdir()
        entries=[]
        for w in worlds:
            directory=reader.directory(w['sourceName']);path=stage/directory/'runtime-map.json';rich=path.read_bytes()
            compact=converter.normalize_runtime(json.loads(rich));util.compact_check(compact,w,src);data=converter.encode(compact)
            (stage/directory/'runtime-map.json').write_bytes(data)
            entries.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=directory,sourceMapSymbol=w['events']['id'],path=directory+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False);lock_bytes=canonical(lock)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,maps=entries,inputFiles=lock['files'],tilesets=TILESETS,mapSymbols={**{w['events']['id']:w['stableId'] for w in worlds},'MAP_TWO_ISLAND':'map.two-island'}),
          'scripts.json':scripts,'contracts.json':contract,'npc-source.json':npcs,'geometry.json':dict(maps=reader.geometry_inventory(worlds),tilesets=TILESETS),'native.json':native,'encounters-items.json':inventory}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=RESOURCE_ID,resourceId=RESOURCE_ID,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=2,cellCount=1059,borderCellCount=8,directedWarpCount=2,directedConnectionCount=2,objectCount=1,reusedNpcGraphicsCount=2,scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_bytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifest_bytes)
        writes=[(target/'scripts/cape-brink-tutor.lock.json',lock_bytes),(target/'scripts/cape-brink-tutor.contract.json',canonical(contract))]
        writes += [(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if path.suffix=='.json':json.loads(data)
            if a.check:need(path.is_file() and path.read_bytes()==data,'Regeneration differs: '+str(path))
        if not a.check:
            for path,data in writes:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        print(json.dumps(dict(inputCount=len(src.entries),outputCount=len(outputs),scriptBlockCount=len(scripts['blocks']),wildSpecies=inventory['wildSpecies'],check=a.check),indent=2))

if __name__=='__main__':main()
