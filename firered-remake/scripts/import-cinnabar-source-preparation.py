#!/usr/bin/env python3
"""Prepare the exact Pallet-to-Cinnabar region source dependencies and compact resources.

No upstream scripts are executed. Reviewed map/compact converters are pinned by
SHA-256. No runtime admission or source battle/UI validation is implied.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
from pathlib import Path
import argparse, base64, collections,  importlib.util, json, os, re, struct, subprocess, sys, tempfile
sys.dont_write_bytecode=True

ROOT = Path(__file__).resolve().parents[1]
PRODUCT = ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-cinnabar-source-preparation-v1'
CORE = ['Route21_North','Route21_South','CinnabarIsland','PokemonMansion_1F','PokemonMansion_2F','PokemonMansion_3F','PokemonMansion_B1F','CinnabarIsland_PokemonCenter_1F','CinnabarIsland_Mart','CinnabarIsland_Gym']
OPTIONAL = []
TILESETS = {'gTileset_General':'primary/general','gTileset_Building':'primary/building','gTileset_PalletTown':'secondary/pallet_town','gTileset_CinnabarIsland':'secondary/cinnabar_island','gTileset_PokemonMansion':'secondary/pokemon_mansion','gTileset_CinnabarGym':'secondary/cinnabar_gym','gTileset_PokemonCenter':'secondary/pokemon_center','gTileset_Mart':'secondary/mart'}

def canonical(doc): return (json.dumps(doc, sort_keys=True, indent=2)+'\n').encode()
def directory(name): return re.sub(r'route(\d+)', r'route-\1', re.sub(r'([a-z])([A-Z])',r'\1-\2',name).replace('_','-').lower())
def git(repo, *args):
    env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull,GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0')
    return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(repo),*args],env=env)

class LazyBlobs(dict):
    def __init__(self, owner): super().__init__(); self.owner=owner
    def __missing__(self, path): return self.owner.read(path)

class Source:
    def __init__(self, reference):
        self.reference=reference
        if git(reference, 'rev-parse', 'HEAD').decode().strip() != COMMIT: raise ValueError('Source HEAD/tree differs')
        self.tree={}
        for row in git(reference,'ls-tree','-rz',COMMIT).split(b'\0'):
            if row:
                fields,path=row.split(b'\t'); self.tree[path.decode()]=tuple(fields.decode().split())
        self.data=LazyBlobs(self);self.entries={};self.roles=collections.defaultdict(set)
    def read(self,path,role='source-dependency'):
        if path not in self.tree or self.tree[path][:2]!=('100644','blob'): raise ValueError('Not a regular source blob: '+path)
        self.roles[path].add(role)
        if path not in self.data:
            b=git(self.reference,'cat-file','blob',self.tree[path][2])
            self.data[path]=b;self.entries[path]=dict(path=path)
        return self.data[path]
    def text(self,path):return self.read(path).decode()
    def json(self,path):return json.loads(self.read(path))
    def units(self,path,code):
        b=self.read(path);size=struct.calcsize(code)
        if len(b)%size:raise ValueError('Misaligned source binary')
        return list(struct.unpack('<'+str(len(b)//size)+code,b))
    def ref(self,path,symbol,line=None):
        self.read(path)
        v=dict(kind='pret',repository='pret/pokefirered',commit=COMMIT,path=path,symbol=symbol)
        if line is not None:v['line']=line
        return v
    def lock(self):
        return dict(schemaVersion=1,repository='pret/pokefirered',commit=COMMIT,profile=PROFILE,files=[dict(**self.entries[p],roles=sorted(self.roles[p])) for p in sorted(self.entries)])

def add_metatile_constants(src, constants, contract):
    # The shared inventory accepts uppercase identifiers only. These source
    # identifiers deliberately retain the original mixed-case tileset names.
    path='include/constants/metatile_labels.h'; source=src.text(path)
    for match in re.finditer(r'^#define\s+(METATILE_(?:PokemonMansion|CinnabarGym)_\w+)\s+(0x[0-9A-Fa-f]+)\s*$', source, re.M):
        name,token=match.groups();value=int(token,16)
        if name in constants['values'] and constants['values'][name]['value']!=value:
            raise ValueError('Conflicting Cinnabar metatile constant: '+name)
        constants['values'][name]=dict(value=value,sourceRef=src.ref(path,name,source[:match.start()].count('\n')+1))
    for patch in contract['tilePatches']:
        for change in patch['changes']:
            if change['metatile'] not in constants['values']:
                raise ValueError('Unresolved Cinnabar setmetatile operand: '+change['metatile'])

def decode(src,name):
    events=src.json(f'data/maps/{name}/map.json');layout=next(x for x in src.json('data/layouts/layouts.json')['layouts'] if x.get('id')==events['layout'])
    attrs=[src.units('data/tilesets/'+TILESETS[layout[k]]+'/metatile_attributes.bin','I') for k in ('primary_tileset','secondary_tileset')]
    names={int(v,16):n for n,v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)',src.text('include/constants/metatile_behaviors.h'),re.M)}
    blocks=src.units(layout['blockdata_filepath'],'H');border=src.units(layout['border_filepath'],'H')
    if len(blocks)!=layout['width']*layout['height']:raise ValueError('Grid size differs')
    cells=[]
    for i,b in enumerate(blocks):
        mid=b&1023;a=attrs[mid>=640][mid if mid<640 else mid-640];bh=a&511
        cells.append(dict(x=i%layout['width'],y=i//layout['width'],rawBlock=b,metatileId=mid,rawAttributes=a,collision=(b>>10)&3,elevation=b>>12,behaviorId=bh,behaviorName=names.get(bh,'UNKNOWN'),encounterType=(a>>24)&7))
    return dict(sourceName=name,stableId='map.'+directory(name),sourceRef=src.ref(f'data/maps/{name}/map.json',events['id']),width=layout['width'],height=layout['height'],layout=layout,events=events,cells=cells,borderBlocks=border)

def scripts(src,maps):
    # Full files are indexed to discover symbols; only selected closure files become
    # dependencies. This deliberately does not equate closure membership to admission.
    blocks={};aliases={}
    paths=[p for p in src.tree if p.endswith('.inc') and (p.startswith(('data/scripts/','data/text/')) or p.endswith('/text.inc') or any(p.startswith('data/maps/'+n+'/') for n in CORE+OPTIONAL))]+['data/event_scripts.s']
    scanned={}
    for p in paths:
        b=git(src.reference,'cat-file','blob',src.tree[p][2]);scanned[p]=b;current=None;macro=False
        for ln,line in enumerate(b.decode().splitlines(),1):
            code=line.split('@')[0].strip()
            if code.startswith('.macro'):macro=True;current=None;continue
            if code=='.endm':macro=False;continue
            if macro:continue
            m=re.fullmatch(r'(\w+)::?',code)
            if m:
                current=m[1]
                if current in blocks:raise ValueError('Duplicate source label '+current)
                blocks[current]=dict(label=current,path=p,line=ln,lines=[])
            elif current:blocks[current]['lines'].append(dict(line=ln,text=line))
            m=re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)',code)
            if m:aliases[m[1]]=m[2]
    roots={n for n,b in blocks.items() if any(b['path'].startswith('data/maps/'+m+'/') for m in CORE+OPTIONAL)}
    for m in maps:
        for k in ('object_events','coord_events','bg_events'):
            roots.update(e['script'] for e in m['events'][k] if e.get('script') not in (None,'0','0x0','NULL'))
    roots.update(['EventScript_SetCinnabarGymTrainers','EventScript_DoFallWarp'])
    roots.update(['Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo','Std_FindItem','Std_ReceivedItem','Std_PutItemAway'])
    pending=sorted(roots);selected=set()
    while pending:
        label=pending.pop()
        if label in selected:continue
        if label not in blocks:raise ValueError('Missing root '+label)
        selected.add(label)
        for row in blocks[label]['lines']:
            for tok in re.findall(r'\b[A-Za-z_]\w*\b',row['text'].split('@')[0]):
                tok=aliases.get(tok,tok)
                if tok in blocks and tok not in selected:pending.append(tok)
    result=[];opcodes=collections.Counter();specials=collections.Counter();unresolved=[]
    for label in sorted(selected):
        b=blocks[label];p=b['path'];src.read(p,'script-label-closure')
        result.append(dict(label=label,sourceRef=src.ref(p,label,b['line']),lines=b['lines']))
        for row in b['lines']:
            t=row['text'].split('@')[0].strip()
            if not t or t.startswith(('.','#')):continue
            op=t.split()[0];opcodes[op]+=1
            if op in ('special','specialvar'):specials[t.split(',')[-1].split()[-1]]+=1
            operands=t.split(None,1)[1] if ' ' in t else '';target=None
            if op in ('call','goto','goto_if_questlog','message'):target=operands.strip()
            elif op in ('map_script','map_script_2','applymovement') or op.startswith(('goto_if_','call_if_')):target=operands.split(',')[-1].strip()
            elif op=='case':target=re.split(r'[,\s]+',operands.strip())[-1]
            elif op in ('msgbox','giveitem_msg','msgreceiveditem','pokemart'):target=operands.split(',')[0].strip()
            if target and target not in ('0','0x0','NULL') and aliases.get(target,target) not in blocks:unresolved.append(dict(source=label,line=row['line'],target=target))
            if op.startswith('trainerbattle_'):
                for target in operands.split(',')[1:]:
                    target=target.strip()
                    if target not in ('0','NULL','NO_MUSIC') and target not in blocks:
                        unresolved.append(dict(source=label,line=row['line'],target=target))
    if unresolved:raise ValueError('Unresolved source label targets: '+repr(unresolved))
    return dict(executable=False,roots=sorted(roots),blocks=result,opcodeCounts=dict(sorted(opcodes.items())),specialCounts=dict(sorted(specials.items())),aliases=aliases,unresolvedLabelReferences=unresolved)

def topology(src,worlds):
    by_symbol={w['events']['id']:w['events'] for w in worlds};warps=[];connections=[]
    def target(symbol):
        if symbol in by_symbol:return by_symbol[symbol]
        for p in src.tree:
            if p.startswith('data/maps/') and p.endswith('/map.json'):
                name=p.split('/')[2]
                if re.sub(r'([a-z])([A-Z])',r'\1_\2',name).upper()==symbol.removeprefix('MAP_'):
                    by_symbol[symbol]=src.json(p);return by_symbol[symbol]
        raise ValueError('Unknown external map '+symbol)
    opposite={'up':'down','down':'up','left':'right','right':'left'}
    for w in worlds:
        for c in w['events']['connections'] or []:
            d=target(c['map']);back=[x for x in d['connections'] or [] if x['map']==w['events']['id'] and x['direction']==opposite[c['direction']]]
            connections.append(dict(sourceMap=w['events']['id'],reciprocal=bool(len(back)==1 and back[0]['offset']==-c['offset']),destinationConnections=d['connections'],**c))
        for i,e in enumerate(w['events']['warp_events']):
            if e['dest_map']=='MAP_DYNAMIC':
                raise ValueError('Unexpected dynamic warp in bounded Cinnabar maps')
                warps.append(dict(sourceMap=w['events']['id'],sourceIndex=i,source=e,dynamic=True));continue
            d=target(e['dest_map']);j=int(e['dest_warp_id']);end=d['warp_events'][j]
            if end['dest_map']!=w['events']['id']:raise ValueError('Destination map differs for Cinnabar warp')
            warps.append(dict(sourceMap=w['events']['id'],sourceIndex=i,source=e,destinationMap=d['id'],destinationIndex=j,destination=end,dynamic=False,reciprocalIndex=int(end['dest_warp_id'])==i))
    return dict(connections=connections,warps=warps)

def geometry_inventory(worlds):
    out=[]
    for w in worlds:
        rows=collections.defaultdict(list)
        for c in w['cells']:rows[c['behaviorName']].append([c['x'],c['y']])
        out.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],width=w['width'],height=w['height'],layout=w['layout'],events=w['events'],sourceRef=w['sourceRef'],behaviorCounts={k:len(v) for k,v in sorted(rows.items())},specialBehaviorCells={k:v for k,v in sorted(rows.items()) if k!='MB_NORMAL'},originalGridBase64=base64.b64encode(struct.pack('<'+str(len(w['cells']))+'H',*[x['rawBlock'] for x in w['cells']])).decode(),attributes=sorted({(x['metatileId'],x['rawAttributes']) for x in w['cells']})))
    return out

def load_helper(name):
    p=PRODUCT/('scripts' if name == 'export-runtime-maps.py' else 'tools/import-regional-service-maps')/name
    spec=importlib.util.spec_from_file_location('_cinnabar_' +p.stem,p);module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

META_PATHS = ['src/data/trainers.h','src/data/trainer_parties.h','src/data/wild_encounters.json',
    'src/data/battle_moves.h','src/data/pokemon/species_info.h','src/data/pokemon/level_up_learnsets.h',
    'src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/evolution.h','src/data/items.json',
    'src/data/pokemon/tmhm_learnsets.h','src/battle_setup.c','src/pokemon.c','src/vs_seeker.c',
    'src/field_control_avatar.c','src/metatile_behavior.c','src/field_fadetransition.c',
    'src/field_player_avatar.c','src/field_effect.c','src/event_object_movement.c','src/overworld.c',
    'src/field_specials.c','src/script_menu.c','src/scrcmd.c','asm/macros/event.inc']

def declarations(src, worlds, doc):
    """Source declarations; deliberately not a runtime-admitted trainer catalog."""
    proofbytes=(PRODUCT/'content/generated/canonical-pokemon/catalog.json').read_bytes()
    proof=json.loads(proofbytes)
    species={s['id']['value'][5:]:s for s in proof['species']}
    metadata={s['id']['value'][5:]:s for s in proof['metadata']}
    occurrences=[]
    for block in doc['blocks']:
        for row in block['lines']:
            t=row['text'].split('@')[0].strip()
            if not t.startswith('trainerbattle_'):continue
            op,args=t.split(None,1);args=[a.strip() for a in args.split(',')]
            occurrences.append(dict(trainer=args[0],opcode=op,arguments=args,
                script=block['label'],sourceRef=src.ref(block['sourceRef']['path'],block['label'],row['line']),
                admitted=False,rematch='rematch' in op))
    trainers=[]
    def one(pattern,path):
        matches=list(re.finditer(pattern,src.text(path),re.M|re.S))
        if len(matches)!=1:raise ValueError('Expected one source declaration: '+pattern)
        return matches[0]
    def ref(path,symbol,match):return src.ref(path,symbol,src.text(path).count('\n',0,match.start())+1)
    for symbol in sorted({r['trainer'] for r in occurrences}):
        path='src/data/trainers.h';m=one(r'^    \['+symbol+r'\] = \{\n(.*?)^    \},',path)
        fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
        match=re.fullmatch(r'(NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',fields['party'])
        if not match:raise ValueError('Unsupported trainer party format: '+symbol)
        shape,party_symbol=match.groups();custom=shape=='NO_ITEM_CUSTOM_MOVES'
        pp='src/data/trainer_parties.h'
        pm=one(r'^static const struct TrainerMonNoItem'+('Custom' if custom else 'Default')+'Moves '+party_symbol+r'\[\] = \{\n(.*?)^\};',pp)
        rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{([^}]+)\},\n)?    \},\n',pm[1]))
        if ''.join(r[0] for r in rows)!=pm[1]:raise ValueError('Unsupported party rows: '+symbol)
        members=[]
        for r in rows:
            iv,level,sp=int(r[1]),int(r[2]),r[3];natural=[]
            for move in species[sp]['levelUpMoves']:
                move_symbol=move['move']['value'][5:]
                if move['level']<=level and move_symbol not in natural:natural=(natural+[move_symbol])[-4:]
            moves=[v.strip() for v in r[4].split(',')] if custom else natural
            if custom and len(moves)!=4:raise ValueError('Wrong custom move count')
            members.append(dict(species=sp,level=level,sourceIv=iv,heldItem='ITEM_NONE',
                customMoves=custom,resolvedMoves=moves,learnsetSource=species[sp]['learnsetSource']))
        trainers.append(dict(trainer=symbol,fields=fields,party=members,sourceRef=ref(path,symbol,m),
            partySource=ref(pp,party_symbol,pm),battleAdmission=False))
    symbols={w['events']['id'] for w in worlds}|{'MAP_PALLET_TOWN'}
    wild=src.json('src/data/wild_encounters.json');encounters=[];slot_tables=[]
    for group in wild['wild_encounter_groups']:
        for row in group['encounters']:
            if row['map'] not in symbols or not row['base_label'].endswith('_FireRed'):continue
            encounters.append(row)
        if any(row['map'] in symbols and row['base_label'].endswith('_FireRed') for row in group['encounters']):
            slot_tables.append({k:v for k,v in group.items() if k!='encounters'})
    wild_species=sorted({m['species'] for e in encounters for kind,table in e.items() if isinstance(table,dict) and 'mons' in table for m in table['mons']})
    trainer_species=sorted({m['species'] for t in trainers for m in t['party']})
    wanted=set(wild_species+trainer_species)
    # Reachable pre-National-Dex level evolutions are a separate ownership requirement.
    evolution_species=set(wild_species)
    while True:
        extra={e['target']['value'][5:] for s in evolution_species for e in metadata[s]['sourceEvolutions']
               if e['method']=='EVO_LEVEL' and species[e['target']['value'][5:]]['sourceSpeciesId']<=151}
        if extra<=evolution_species:break
        evolution_species|=extra
    wanted|=evolution_species
    selected=[species[s] for s in sorted(wanted)]
    moves=sorted({m for t in trainers for mon in t['party'] for m in mon['resolvedMoves']} - {'MOVE_NONE'})
    move_rows=[]
    for move in moves:
        p='src/data/battle_moves.h';m=one(r'^    \['+move+r'\] =\s*\n    \{\n(.*?)^    \},',p)
        move_rows.append(dict(move=move,fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M)),sourceRef=ref(p,move,m)))
    item_symbols=set()
    for b in doc['blocks']:
        if b['sourceRef']['path'].startswith(tuple('data/maps/'+n+'/' for n in CORE)) or any(b['label']==o.get('script') for w in worlds for o in w['events']['object_events']):
            item_symbols.update(re.findall(r'\bITEM_\w+', '\n'.join(r['text'] for r in b['lines'])))
    item_symbols.update(e['item'] for w in worlds for e in w['events']['bg_events'] if 'item' in e)
    item_symbols.update(i for t in trainers for i in re.findall(r'ITEM_\w+',t['fields']['items']))
    item_symbols.discard('ITEM_NONE')
    item_rows=[row for row in src.json('src/data/items.json')['items'] if row['itemId'] in item_symbols]
    if {r['itemId'] for r in item_rows}!=item_symbols:raise ValueError('Missing item declaration')
    machines={}
    for symbol in sorted(species):
        p='src/data/pokemon/tmhm_learnsets.h';m=one(r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),',p)
        compatible=sorted(set(re.findall(r'TMHM\((\w+)\)',m[1])) & {'TM14_BLIZZARD','TM22_SOLAR_BEAM','TM38_FIRE_BLAST'})
        if compatible:machines[symbol]=compatible
    if not machines:raise ValueError('Empty new machine compatibility')
    return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,
        trainers=trainers,battleOccurrences=occurrences,wildEncounters=encounters,encounterSlotTables=slot_tables,
        wildSource=src.ref('src/data/wild_encounters.json','Selected FireRed encounter tables'),
        wildSpecies=wild_species,trainerSpecies=trainer_species,preNationalDexLevelEvolutionSpecies=sorted(evolution_species),
        pokemon=selected,pokemonMetadata=[metadata[s] for s in sorted(wanted)],
        abilities=sorted({s[k] for s in selected for k in ('ability1','ability2')} - {'ABILITY_NONE'}),
        trainerMoves=move_rows,fullNaturalMoveClosure=sorted({m['move']['value'][5:] for s in selected for m in s['levelUpMoves']}),
        itemDeclarations=item_rows,itemSource=src.ref('src/data/items.json','Selected items'),
        newMachineCompatibility=machines,machineSource=src.ref('src/data/pokemon/tmhm_learnsets.h','TM14/TM22/TM38 source compatibility; ownership admission remains separate'),

        limitations=['Source data only; no field/battle/ownership/presentation admission.',
            'Trainer rematch entry declarations retained; VS Seeker rematch roster expansion is deferred.',
            'FireRed tables only; no LeafGreen species substitution.',
            'Gym and post-Blaine Bill scripts are inventoried, not enabled by this package.'])

def item_icons(src,stage):
    path=PRODUCT/'scripts/import-lab-battle-ui.py'
    spec=importlib.util.spec_from_file_location('_cinnabar_item_codec',path);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
    binding_path='src/data/item_icon_table.h';bindings=src.text(binding_path)
    graphic_path='src/data/graphics/items.h';graphics=src.text(graphic_path);records=[]
    for item in ['ITEM_SECRET_KEY','ITEM_TM14','ITEM_TM22','ITEM_TM38']:
        matches=list(re.finditer(r'^    \['+item+r'\][ \t]*= \{(\w+), (\w+)\},',bindings,re.M))
        if len(matches)!=1:raise ValueError('Item icon binding missing: '+item)
        binding=matches[0];paths=[]
        for symbol in binding.groups():
            matches=list(re.finditer(r'^const u32 '+symbol+r'\[\][ \t]*= INCBIN_U32\("([^"\n]+)"\);',graphics,re.M))
            if len(matches)!=1:raise ValueError('Item icon symbol missing: '+symbol)
            paths.append(matches[0][1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
        image,palette=paths;w,h,indices,_=codec.indexed_png(src.read(image));rgba=codec.rgba(indices,codec.palette(src.read(palette)))
        if (w,h)!=(24,24):raise ValueError('Unexpected item dimensions')
        png=codec.png(w,h,rgba);relative='items/'+item.lower()+'.png';p=stage/relative;p.parent.mkdir(exist_ok=True);p.write_bytes(png)
        records.append(dict(item=item,width=w,height=h,texturePath=relative,
            sourceRef=src.ref(image,binding[1]),paletteRef=src.ref(palette,binding[2]),
            bindingRef=src.ref(binding_path,item,bindings.count('\n',0,binding.start())+1)))
    return dict(schemaVersion=1,items=records,runtimeIntegrated=False)

def contracts(src,worlds,doc):
    blocks={b['label']:b for b in doc['blocks']}
    def block(n):return blocks[n]
    patches=[]
    for b in doc['blocks']:
        rows=[]
        for r in b['lines']:
            t=r['text'].strip()
            if not t.startswith('setmetatile '):continue
            args=[s.strip() for s in t.split(None,1)[1].split(',')]
            rows.append(dict(x=int(args[0]),y=int(args[1]),metatile=args[2],collision=int(args[3]),sourceLine=r['line']))
        if rows:patches.append(dict(label=b['label'],sourceRef=b['sourceRef'],changes=rows))
    falls=[];items=[];hidden=[]
    for w in worlds:
        for i,e in enumerate(w['events']['warp_events']):
            c=w['cells'][e['y']*w['width']+e['x']]
            if c['behaviorName']=='MB_FALL_WARP':falls.append(dict(map=w['stableId'],sourceWarpIndex=i,source=e,cell=c))
        for i,o in enumerate(w['events']['object_events']):
            if o['graphics_id']=='OBJ_EVENT_GFX_ITEM_BALL':items.append(dict(map=w['stableId'],localId=i+1,event=o,script=block(o['script'])))
        for e in w['events']['bg_events']:
            if 'item' in e:hidden.append(dict(map=w['stableId'],event=e))
    if len(falls)!=5 or len(items)!=12:raise ValueError('Mansion fall/pickup scope differs')
    city=next(w for w in worlds if w['sourceName']=='CinnabarIsland')
    door=[e for e in city['events']['coord_events'] if e['script']=='CinnabarIsland_EventScript_GymDoorLocked']
    if len(door)!=1 or door[0]['var']!='VAR_TEMP_1' or door[0]['var_value']!='0':raise ValueError('Gym door trigger differs')
    return dict(schemaVersion=1, profile=PROFILE, runtimeIntegrated=False, arrivalMaps=CORE[:3], mansionMaps=CORE[3:7], supportMaps=CORE[7:9], laterGymMap=CORE[9], routeConnectionChain=['MAP_PALLET_TOWN', 'MAP_ROUTE21_NORTH', 'MAP_ROUTE21_SOUTH', 'MAP_CINNABAR_ISLAND'], sourcePreconditions=['FLAG_BADGE05_GET', 'PartyHasMonWithSurf()', 'IsPlayerFacingSurfableFishableWater()'], noSeafoamPrerequisite=True, secretKeyPickup=next((i for i in items if i['event']['script'].endswith('ItemSecretKey'))), gymDoorTrigger=door[0], gymUnlock=block('CinnabarIsland_EventScript_CheckUnlockGym'), gymUnlocked=block('CinnabarIsland_EventScript_UnlockGym'), mansionSwitch=block('PokemonMansion_EventScript_SecretSwitch'), mansionReset=block('PokemonMansion_EventScript_ResetSwitch'), mansionSwitchFlag='FLAG_POKEMON_MANSION_SWITCH_STATE', tilePatches=patches, fallWarps=falls, itemPickups=items, hiddenItems=hidden, blaineVictory=block('CinnabarIsland_Gym_EventScript_DefeatedBlaine'), tm38Reward=block('CinnabarIsland_Gym_EventScript_GiveTM38'), tm38NoRoom=block('CinnabarIsland_Gym_EventScript_NoRoomForTM38'), gymTrainerFlags=block('EventScript_SetCinnabarGymTrainers'), quizAnswers=['YES', 'NO', 'NO', 'NO', 'YES', 'NO'], martStock=block('CinnabarIsland_Mart_Items'), billDecline=block('CinnabarIsland_EventScript_DeclineSailToOneIsland'), billAcceptanceBoundary=block('CinnabarIsland_EventScript_SailToOneIsland'), limitations=['Source map/script closure is not executable admission.', 'Mansion fall handling must honor EventScript_DoFallWarp and destination facing/landing, not ordinary reverse warps.', 'Map border connections to Route20 and doors to Lab/Center2F are retained as external boundaries.', 'The pinned source gym unlocks from the pickup hide flag, not an ITEM_SECRET_KEY bag test.', 'Blaine victory starts the Bill scene; do not suppress it or substitute its story flags.', 'Map PNGs show source default tiles; a source-authenticated dynamic tile atlas/render path is still required for switch/gym patches.', 'No physical audio output, native UI, or original battle behavior was validated.'])

def main():
    global ROOT,PRODUCT
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--dependency-root',type=Path,required=True)
    ap.add_argument('--check',action='store_true');a=ap.parse_args()
    ROOT=a.project_root.absolute();PRODUCT=a.dependency_root.absolute();reference=a.reference_root.absolute()
    if ROOT==reference or ROOT.is_relative_to(reference) or reference.is_relative_to(ROOT):raise ValueError('Reference/output overlap')
    out=ROOT/'content/generated/cinnabar-source-preparation';raw=ROOT/'content/raw/pokefirered/037335f4'
    lockpath=ROOT/'content/sources/cinnabar-source-preparation.lock.json'
    for target in (ROOT,out,raw,lockpath):
        if any(p.is_symlink() for p in (target,*target.parents)):raise ValueError('Output parent symlink denied')
    src=Source(reference);worlds=[decode(src,n) for n in CORE];doc=scripts(src,worlds);links=topology(src,worlds)
    # These behavior-triggered script roots are not map-event pointers.
    if 'EventScript_DoFallWarp' not in {b['label'] for b in doc['blocks']}:
        raise ValueError('Fall-warp behavior script is absent from closure')
    doc['profile']=PROFILE;doc['sourcePaths']=sorted({b['sourceRef']['path'] for b in doc['blocks']})
    for name in ['flags','vars','event_objects','trainers','opponents','items','species','moves','abilities','songs','map_scripts','field_effects','battle','pokemon','global','menu','game_stat','battle_ai','event_bg','event_object_movement','sound','fame_checker','metatile_labels','battle_setup','seagallop']:
        p='include/constants/'+name+'.h'
        if p in src.tree:src.read(p,'rule-reference')
    for p in META_PATHS:src.read(p,'rule-reference')
    contract=contracts(src,worlds,doc);data=declarations(src,worlds,doc)
    shared=load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=load_helper('export-runtime-maps.py')
    # Temporary staging is outside the output tree, so --check creates no product files.
    with tempfile.TemporaryDirectory(prefix='cinnabar-source-stage-') as temp:
        stage=Path(temp)
        for n in CORE:shared.import_map(src,n,'map.'+directory(n),stage/directory(n))
        npcs=shared.npc_inventory(src,[dict(events=dict(object_events=[o for o in w['events']['object_events'] if o['graphics_id']!='0'])) for w in worlds],stage/'npc')
        item_art=item_icons(src,stage)
        constants=shared.constants_inventory(src,worlds,doc)
        add_metatile_constants(src,constants,contract)
        catalog=[]
        for w in worlds:
            d=directory(w['sourceName']);p=stage/d/'runtime-map.json';b=p.read_bytes()
            compact=converter.encode(converter.normalize_runtime(json.loads(b)));(stage/d/'runtime-map.json').write_bytes(compact)
            catalog.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=d,
                sourceMapSymbol=w['events']['id'],path=d+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=CORE)
        lockdata=canonical(lock)
        docs=package_documents(out.parent, constants, {'catalog.json':dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,maps=catalog,tilesets=TILESETS,inputFiles=lock['files'],**links),
              'scripts.json':doc,'npc-source.json':npcs,'contracts.json':contract,'declarations.json':data,
              'geometry.json':dict(maps=geometry_inventory(worlds)),'item-art.json':item_art}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=len(CORE),cellCount=sum(w['width']*w['height'] for w in worlds),
            warpCount=sum(len(w['events']['warp_events']) for w in worlds),npcGraphicsCount=len(npcs['graphics']),
            scriptBlockCount=len(doc['blocks']),unresolvedLabelCount=0,trainerCount=len(data['trainers']),outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=[(lockpath,lockdata)]+[(raw/p,b) for p,b in sorted(src.data.items())]+[(out/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            if any(x.is_symlink() for x in (p,*p.parents)):raise ValueError('Output symlink denied')
            if a.check:
                if not p.is_file() or p.read_bytes()!=b:raise ValueError('Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputs=len(outputs),falls=len(contract['fallWarps']))))

if __name__=='__main__':main()

