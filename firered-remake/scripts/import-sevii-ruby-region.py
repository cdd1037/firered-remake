#!/usr/bin/env python3
"""Authenticate the bounded One Island / Mt. Ember Ruby region; execute no upstream code."""
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
PROFILE = 'firered-sevii-ruby-region-v1'
PACKAGE = 'sevii-ruby-region'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
CORE = ['OneIsland_KindleRoad', 'MtEmber_Exterior', 'MtEmber_RubyPath_1F',
        'MtEmber_RubyPath_B1F', 'MtEmber_RubyPath_B2F', 'MtEmber_RubyPath_B3F',
        'MtEmber_RubyPath_B4F', 'MtEmber_RubyPath_B5F',
        'MtEmber_RubyPath_B1F_Stairs', 'MtEmber_RubyPath_B2F_Stairs']
SUPPORT = ['OneIsland_KindleRoad_EmberSpa']
ANCHOR = ['OneIsland', 'OneIsland_PokemonCenter_1F']
TILESETS = {'gTileset_General':'primary/general',
            'gTileset_SeviiIslands123':'secondary/sevii_islands_123',
            'gTileset_MtEmber':'secondary/mt_ember'}

def canonical(v): return (json.dumps(v, sort_keys=True, indent=2)+'\n').encode()
def need(v, message):
    if not v: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink in output: '+str(path))
def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value

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
    for symbol in wanted:
        for ref in [species[symbol]['source'], species[symbol]['learnsetSource']]+[m['source'] for m in species[symbol]['levelUpMoves']]:
            src.read(ref['path'])
    item_symbols = {'ITEM_RUBY','ITEM_TRI_PASS','ITEM_RAINBOW_PASS','ITEM_HM06'}
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
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, runtimeIntegrated=False,
        trainers=trainers, battleOccurrences=occurrences, wildEncounters=encounters, encounterSlotTables=slots,
        wildSpecies=sorted(wild), nationalDexLevelEvolutionSpecies=sorted(evolution), trainerSpecies=sorted(opponents),
        trainerMoves=sorted({m for t in trainers for mon in t['party'] for m in mon['resolvedMoves']}-{'MOVE_NONE'}),
        fullNaturalMoveClosure=sorted({m['move']['value'][5:] for s in wanted for m in species[s]['levelUpMoves']}),
        pokemon=[species[s] for s in sorted(wanted)], pokemonMetadata=[metadata[s] for s in sorted(wanted)],
        itemDeclarations=items,
        limitations=['Complete original first-encounter teams, wild tables and level evolutions are inventory only.',
            'Rematch opcodes are preserved; VS Seeker higher-tier teams are not admitted.',
            'No legendary, Sapphire quest, roamer or later-island runtime admission.'])

def constants(src, shared, worlds, scripts):
    result=shared.constants_inventory(src, worlds, scripts); definitions={}
    for path in sorted(src.data):
        if path.startswith('include/'):
            text=src.text(path)
            for m in re.finditer(r'^#define[ \t]+([A-Z][A-Z0-9_]+)[ \t]+([^\n]+)',text,re.M):
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

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--dependency-root',type=Path,default=ROOT)
    ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--check',action='store_true');args=ap.parse_args()
    safe(args.project_root.absolute())
    target=args.project_root.resolve();dep=args.dependency_root.resolve();ref=args.reference_root.resolve();safe(target)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'ruby_reader')
    prep=module(dep / 'scripts/first-sevii-preparation/prepare_first_sevii.py', 'ruby_script_index')
    reader.PRODUCT=dep;reader.TILESETS=TILESETS;reader.PROFILE=PROFILE
    prep.CORE=CORE;prep.SUPPORT=SUPPORT;prep.ANCHOR=['OneIsland_PokemonCenter_1F']
    src=reader.Source(ref);worlds=[reader.decode(src,n) for n in CORE+SUPPORT]
    for n in ANCHOR:src.read(f'data/maps/{n}/map.json','reused-anchor-events')
    scripts=prep.script_index(src,reader);scripts['profile']=PROFILE
    scripts['sourcePaths']=sorted({b['sourceRef']['path'] for b in scripts['blocks']})
    need(not scripts['unresolvedLabelReferences'], 'Unresolved label closure')
    links=prep.topology(src,worlds)
    proofbytes=(dep/'content/generated/canonical-pokemon/catalog.json').read_bytes()
    data=declarations(src,scripts,worlds,json.loads(proofbytes))
    for p in reader.META_PATHS+['data/maps/PalletTown/scripts.inc','data/maps/PalletTown_ProfessorOaksLab/scripts.inc',
        'src/event_data.c','src/seagallop.c','src/new_game.c','src/field_specials.c',
        'src/data/heal_locations.json','src/data/heal_locations.constants.json.txt',
        'data/maps/map_groups.json','include/gba/defines.h','src/data/tilesets/headers.h','src/data/tilesets/graphics.h']:
        src.read(p,'rule-reference')
    # Preserve the selected source constants, not every header in the reference tree.
    headers=['flags','vars','maps','map_scripts','event_objects','event_object_movement','opponents','trainers',
        'species','moves','items','abilities','songs','battle','battle_ai','battle_setup','menu','seagallop',
        'metatile_labels','metatile_behaviors','cable_club','fame_checker','field_effects',
        'field_weather','game_stat','global','help_system','pokemon','sound','union_room']
    for n in headers:src.read('include/constants/'+n+'.h','script-operand-constant')
    branchbytes=(ROOT/'scripts/sevii-ruby-branch-contract.json').read_bytes();branches=json.loads(branchbytes)
    bylabel={b['label']:b for b in scripts['blocks']}
    for branch in branches:
        for label in branch['labels']:need(label in bylabel, 'Missing branch source '+label)
    pickups=[];hidden=[]
    for w in worlds:
        for i,e in enumerate(w['events']['object_events'],1):
            if e['graphics_id'] in ('OBJ_EVENT_GFX_ITEM_BALL','OBJ_EVENT_GFX_RUBY'):
                pickups.append(dict(map=w['events']['id'],localId=i,event=e,script=bylabel[e['script']]))
        hidden.extend(dict(map=w['events']['id'],event=e) for e in w['events']['bg_events'] if 'item' in e)
    contract=dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,coreMaps=CORE,supportMaps=SUPPORT,
        reusedAnchorMaps=ANCHOR,mapTopology=links,itemPickups=pickups,hiddenItems=hidden,criticalBranches=branches,
        excludedEncounterGates=[],
        externalBoundaries={k:[e for e in links[k] if not e['destinationInSlice']] for k in ('warps','connections')},
        tilePatches=[dict(label=b['label'],sourceRef=b['sourceRef'],changes=[dict(line=r['line'],arguments=[x.strip() for x in r['text'].strip().split(None,1)[1].split(',')]) for r in b['lines'] if r['text'].strip().startswith('setmetatile ')]) for b in scripts['blocks'] if any(r['text'].strip().startswith('setmetatile ') for r in b['lines'])],
        limitations=['Source/standalone Content only; no campaign, trainer, encounter, item or script runtime admission.',
            'Mt. Ember summit/legendary boundaries and later ferry destinations remain unadmitted.',
            'Celio dispatch contains original guarded Sapphire/link/roamer branches; closure membership is not permission.',
            'No battle, natural travel, save, native window or physical audio validation is claimed.'])
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='sevii-ruby-import-') as temp:
        stage=Path(temp)
        for n in CORE+SUPPORT:shared.import_map(src,n,'map.'+reader.directory(n),stage/reader.directory(n))
        npcs=shared.npc_inventory(src,worlds,stage/'npc')
        const=constants(src,shared,worlds,scripts)
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
        docs=package_documents(target / 'content/generated', const, {'catalog.json':dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            runtimeIntegrated=False,maps=catalog,tilesets=TILESETS,mapSymbols=mapsymbols,inputFiles=lock['files'],**links),
            'scripts.json':scripts,'npc-source.json':npcs,'contracts.json':contract,
            'declarations.json':data,'geometry.json':dict(maps=reader.geometry_inventory(worlds),tilesets=TILESETS)}, check=args.check)
        for n,d in docs.items():(stage/n).write_bytes(canonical(d))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(w['width']*w['height'] for w in worlds),
            warpCount=sum(len(w['events']['warp_events']) for w in worlds),npcGraphicsCount=len(npcs['graphics']),
            scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,trainerCount=len(data['trainers']),
            outputs=outputs)
        manifestbytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifestbytes)
        writes=[(target/'content/sources/sevii-ruby-region.lock.json',lockbytes)]+[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            safe(p)
            if args.check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))

if __name__=='__main__':main()
