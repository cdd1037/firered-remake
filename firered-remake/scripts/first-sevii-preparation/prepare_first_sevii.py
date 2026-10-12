#!/usr/bin/env python3
"""Small read-only source preparation over the reviewed Cinnabar source reader.

Writes only beside this script. No reference scripts, engine, GUI, downloads,
rendered textures, duplicated repository, or runtime admission.
"""
import argparse, base64, collections, hashlib, importlib.util, json, re, sys
from pathlib import Path
sys.dont_write_bytecode = True
OUT = Path(__file__).resolve().parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
READER_SHA = '13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
CORE = ['OneIsland','OneIsland_Harbor','OneIsland_PokemonCenter_1F',
        'TwoIsland','TwoIsland_Harbor','TwoIsland_JoyfulGameCorner',
        'ThreeIsland','ThreeIsland_Harbor','ThreeIsland_Port',
        'ThreeIsland_BondBridge','ThreeIsland_BerryForest']
SUPPORT = ['TwoIsland_PokemonCenter_1F','ThreeIsland_PokemonCenter_1F','ThreeIsland_Mart']
ANCHOR = ['CinnabarIsland','CinnabarIsland_PokemonCenter_1F']
PROFILE = 'firered-first-sevii-source-preparation-v1'
TILESETS = {'gTileset_General':'primary/general','gTileset_Building':'primary/building',
 'gTileset_SeviiIslands123':'secondary/sevii_islands_123','gTileset_IslandHarbor':'secondary/island_harbor',
 'gTileset_PokemonCenter':'secondary/pokemon_center','gTileset_GameCorner':'secondary/game_corner',
 'gTileset_BerryForest':'secondary/berry_forest','gTileset_Mart':'secondary/mart'}
def sha(b): return hashlib.sha256(b).hexdigest()
def canonical(d): return (json.dumps(d,sort_keys=True,indent=2)+'\n').encode()
def need(v,m):
    if not v: raise ValueError(m)

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
            need(back['dest_map']==symbol,'warp destination map mismatch')
            warps.append(dict(sourceMap=symbol,sourceIndex=i,source=x,destination=back,
              destinationInSlice=x['dest_map'] in active,reciprocalIndex=int(back['dest_warp_id'])==i))
        opposite={'up':'down','down':'up','left':'right','right':'left'}
        for x in e['connections'] or []:
            d=src.json(allmaps[x['map']]);back=[y for y in d['connections'] or [] if y['map']==symbol and y['direction']==opposite[x['direction']]]
            need(len(back)==1 and back[0]['offset']==-x['offset'],'connection mismatch')
            connections.append(dict(sourceMap=symbol,source=x,destinationInSlice=x['map'] in active,reciprocal=True))
    return dict(warps=warps,connections=connections)

def declarations(src,doc,worlds,proof):
    species={s['id']['value'][5:]:s for s in proof['species']};metadata={s['id']['value'][5:]:s for s in proof['metadata']}
    occurrences=[]
    for b in doc['blocks']:
        for row in b['lines']:
            t=row['text'].split('@')[0].strip()
            if not t.startswith('trainerbattle_'):continue
            op,args=t.split(None,1);args=[x.strip() for x in args.split(',')]
            # Anchor Cinnabar Center contains no new trainers; all selected trainer roots belong to the slice.
            occurrences.append(dict(trainer=args[0],opcode=op,arguments=args,script=b['label'],
              sourceRef=src.ref(b['sourceRef']['path'],b['label'],row['line']),rematch='rematch' in op))
    def one(p,pattern):
        ms=list(re.finditer(pattern,src.text(p),re.M|re.S));need(len(ms)==1,'source declaration '+pattern);return ms[0]
    trainers=[]
    for symbol in sorted({r['trainer'] for r in occurrences}):
        p='src/data/trainers.h';m=one(p,r'^    \['+symbol+r'\] = \{\n(.*?)^    \},')
        fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M));party=re.fullmatch(r'(NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',fields['party']);need(party,'party shape')
        shape,pname=party.groups();custom=shape=='NO_ITEM_CUSTOM_MOVES';pp='src/data/trainer_parties.h'
        pm=one(pp,r'^static const struct TrainerMonNoItem'+('Custom' if custom else 'Default')+'Moves '+pname+r'\[\] = \{\n(.*?)^\};')
        rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{([^}]+)\},\n)?    \},\n',pm[1]));need(''.join(r[0] for r in rows)==pm[1],'party rows')
        mons=[]
        for r in rows:
            iv,level,sp=int(r[1]),int(r[2]),r[3];natural=[]
            for move in species[sp]['levelUpMoves']:
                ms=move['move']['value'][5:]
                if move['level']<=level and ms not in natural:natural=(natural+[ms])[-4:]
            moves=[v.strip() for v in r[4].split(',')] if custom else natural
            mons.append(dict(species=sp,level=level,sourceIv=iv,heldItem='ITEM_NONE',customMoves=custom,resolvedMoves=moves))
        trainers.append(dict(trainer=symbol,fields=fields,party=mons,sourceRef=src.ref(p,symbol,src.text(p).count('\n',0,m.start())+1),partySource=src.ref(pp,pname,src.text(pp).count('\n',0,pm.start())+1)))
    symbols={w['events']['id'] for w in worlds};encounters=[];slot_tables=[]
    for g in src.json('src/data/wild_encounters.json')['wild_encounter_groups']:
        chosen=[e for e in g['encounters'] if e['map'] in symbols and e['base_label'].endswith('_FireRed')]
        if chosen:encounters+=chosen;slot_tables.append({k:v for k,v in g.items() if k!='encounters'})
    wild={m['species'] for e in encounters for tab in e.values() if isinstance(tab,dict) and 'mons' in tab for m in tab['mons']}
    wild.add('SPECIES_HYPNO');evolution=set(wild)
    while True:
        extra={e['target']['value'][5:] for s in evolution for e in metadata[s]['sourceEvolutions'] if e['method']=='EVO_LEVEL' and species[e['target']['value'][5:]]['sourceSpeciesId']<=151}
        if extra<=evolution:break
        evolution|=extra
    opponents={m['species'] for t in trainers for m in t['party']};wanted=evolution|opponents
    fullmoves=sorted({m['move']['value'][5:] for s in wanted for m in species[s]['levelUpMoves']})
    battle_moves=sorted({m for t in trainers for mon in t['party'] for m in mon['resolvedMoves']}-{'MOVE_NONE'})
    hypno=[]
    for m in species['SPECIES_HYPNO']['levelUpMoves']:
        ms=m['move']['value'][5:]
        if m['level']<=30 and ms not in hypno:hypno=(hypno+[ms])[-4:]
    for s in wanted:
        for r in [species[s]['source'],species[s]['learnsetSource']]+[m['source'] for m in species[s]['levelUpMoves']]:need(sha(src.read(r['path']))==r['sha256'],'canonical dependency differs')
    return dict(trainers=trainers,battleOccurrences=occurrences,wildEncounters=encounters,encounterSlotTables=slot_tables,
      scriptedHypno=dict(species='SPECIES_HYPNO',level=30,resolvedMoves=hypno,sourceRef=src.ref('data/maps/ThreeIsland_BerryForest/scripts.inc','ThreeIsland_BerryForest_EventScript_Lostelle',10)),
      wildSpecies=sorted(wild),preNationalDexLevelEvolutionSpecies=sorted(evolution),trainerSpecies=sorted(opponents),
      trainerMoves=battle_moves,fullNaturalMoveClosure=fullmoves,
      speciesReferences=[dict(species=s,sourceSpeciesId=species[s]['sourceSpeciesId'],source=species[s]['source'],learnsetSource=species[s]['learnsetSource'],sourceEvolutions=metadata[s]['sourceEvolutions']) for s in sorted(wanted)])

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    need(OUT!=a.project_root.resolve() and not OUT.is_relative_to(a.project_root.resolve()),'do not write into main')
    p=a.project_root/'scripts/import-cinnabar-source-preparation.py';need(sha(p.read_bytes())==READER_SHA,'reviewed reader differs')
    spec=importlib.util.spec_from_file_location('_first_sevii_reader',p);reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
    reader.TILESETS=TILESETS;reader.PROFILE=PROFILE
    src=reader.Source(a.reference_root);worlds=[reader.decode(src,n) for n in CORE+SUPPORT]
    doc=script_index(src,reader);links=topology(src,worlds)
    proofpath=a.project_root/'content/generated/canonical-pokemon/catalog.json';proofbytes=proofpath.read_bytes();need(sha(proofbytes)==reader.CANONICAL_SHA,'canonical386 differs')
    data=declarations(src,doc,worlds,json.loads(proofbytes))
    for p in ['src/seagallop.c','src/battle_setup.c','src/field_specials.c','src/renewable_hidden_items.c','src/field_control_avatar.c','src/field_player_avatar.c','src/field_fadetransition.c','src/field_effect.c','src/event_object_movement.c','src/overworld.c','src/script_menu.c','src/scrcmd.c','src/pokemon.c','src/vs_seeker.c','src/data/items.json','src/data/battle_moves.h','src/data/heal_locations.json','src/data/heal_locations.constants.json.txt','src/heal_location.c','src/event_data.c','src/new_game.c','src/data/tilesets/headers.h','asm/macros/event.inc','data/maps/map_groups.json',
      'data/maps/ThreeIsland_DunsparceTunnel/scripts.inc','data/maps/ThreeIsland_DunsparceTunnel/map.json','data/maps/VermilionCity/scripts.inc',
      'src/data/tilesets/graphics.h','src/graphics.c','src/wild_encounter.c']:
        if p in src.tree:src.read(p,'rule-reference')
    for p in src.tree:
        if p.startswith('include/constants/') and p.endswith('.h') and p.split('/')[-1] in [n+'.h' for n in ['flags','vars','maps','map_scripts','event_objects','event_object_movement','opponents','trainers','species','moves','items','abilities','songs','battle','battle_ai','battle_setup','menu','seagallop','heal_locations','metatile_labels','metatile_behaviors']]:src.read(p,'rule-reference')
    for ts in TILESETS.values():
        for p in src.tree:
            if p.startswith('data/tilesets/'+ts+'/'):src.read(p,'deferred-map-art-dependency')
    # Hash current predecessor catalogs rather than copying their large contents.
    predecessor=[];owned=set();opponents=set();moves=set()
    paths=list((a.project_root/'content/generated').glob('*pokemon/catalog.json'))
    paths += [a.project_root/'content/generated/pokemon/starter-catalog.json',a.project_root/'content/generated/old-man-tutorial/catalog.json']
    paths += [a.project_root/f'content/generated/{n}-catalogs/pokemon.json' for n in ['silph','saffron-gym','cinnabar']]
    paths += [a.project_root/'content/generated/metronome-moves/catalog.json']
    for p in sorted(set(paths)):
        if not p.is_file() or 'canonical-pokemon' in str(p):continue
        b=p.read_bytes();d=json.loads(b);s=d.get('species',[]);o=d.get('opponentSpecies',[])
        if isinstance(s,list):owned.update(v['id']['value'][5:] for v in s if isinstance(v,dict) and 'id' in v)
        if isinstance(o,list):opponents.update(v['id']['value'][5:] for v in o if isinstance(v,dict) and 'id' in v)
        ms=d.get('moves',[])
        if isinstance(ms,list):moves.update(v['id']['value'][5:] for v in ms if isinstance(v,dict) and 'id' in v)
        predecessor.append(dict(path=p.relative_to(a.project_root).as_posix(),sha256=sha(b)))
    need(len(owned)==115,'Cinnabar ownership observation changed');need(len(moves)==354,'current shared move metadata observation changed')
    delta=dict(catalogObservationOnly=True,inputs=predecessor,ownedCatalogSpeciesCount=len(owned),ownedSpecies=sorted(owned),opponentOnlySpecies=sorted(opponents-owned),
      missingWildOwnership=sorted(set(data['wildSpecies'])-owned),missingEvolutionOwnership=sorted(set(data['preNationalDexLevelEvolutionSpecies'])-owned),
      missingTrainerSpecies=sorted(set(data['trainerSpecies'])-owned-opponents),moveCatalogCount=len(moves),missingTrainerMoveMetadata=sorted(set(data['trainerMoves'])-moves),missingNaturalMoveMetadata=sorted(set(data['fullNaturalMoveClosure'])-moves),
      missingMaps=[dict(name=w['sourceName'],stableId=w['stableId']) for w in worlds])
    delta['missingFirstVisitWildOwnership']=sorted(set(data['wildSpecies'])-owned-{'SPECIES_DUNSPARCE'})
    delta['missingFirstVisitEvolutionOwnership']=sorted(set(data['preNationalDexLevelEvolutionSpecies'])-owned-{'SPECIES_DUNSPARCE'})
    delta['excludedEncounterGates']=[dict(map='MAP_THREE_ISLAND_PORT',species='SPECIES_DUNSPARCE',gate='IsNationalPokedexEnabled',
      sourcePath='data/maps/ThreeIsland_DunsparceTunnel/scripts.inc',sourceLabel='ThreeIsland_DunsparceTunnel_OnTransition',
      reason='Port Dunsparce grass is east of the tunnel, disconnected from western first-visit port; the alternate dug-out tunnel layout is selected only when National Dex is enabled.')]
    bylabel={b['label']:b for b in doc['blocks']};items=[];hidden=[]
    for w in worlds:
        for i,e in enumerate(w['events']['object_events']):
            if e['graphics_id']=='OBJ_EVENT_GFX_ITEM_BALL':items.append(dict(map=w['events']['id'],localId=i+1,event=e,script=bylabel[e['script']]))
        hidden += [dict(map=w['events']['id'],event=e) for e in w['events']['bg_events'] if 'item' in e]
    relevant={b['label']:b for b in doc['blocks'] if b['sourceRef']['path'].startswith(tuple('data/maps/'+n+'/' for n in CORE+SUPPORT))}
    tokens='\n'.join(r['text'] for b in relevant.values() for r in b['lines'])
    shoplabels=['TwoIsland_Items_ShopInitial','TwoIsland_Items_ShopExpanded1','ThreeIsland_Mart_Items']
    shops=[dict(label=n,sourceRef=bylabel[n]['sourceRef'],items=re.findall(r'\bITEM_(?!NONE\b)\w+', '\n'.join(r['text'] for r in bylabel[n]['lines']))) for n in shoplabels]
    itemset={'ITEM_METEORITE','ITEM_TRI_PASS','ITEM_TOWN_MAP','ITEM_IAPAPA_BERRY','ITEM_MOON_STONE','ITEM_FULL_RESTORE'}
    for x in items:itemset.update(re.findall(r'\bITEM_\w+','\n'.join(r['text'] for r in x['script']['lines'])))
    itemset.update(x['event']['item'] for x in hidden)
    itemset.update(s for x in shops for s in x['items'])
    itemset.discard('ITEM_NONE')
    data['itemDeclarations']=[x for x in src.json('src/data/items.json')['items'] if x['itemId'] in itemset]
    need({x['itemId'] for x in data['itemDeclarations']}==itemset,'item declarations missing')
    data['shopStocks']=shops
    data['gifts']=[dict(item=i,quantity=1,label=l,bagFull=f) for i,l,f in [
       ('ITEM_METEORITE','OneIsland_PokemonCenter_1F_EventScript_MeetCelioScene','source additem has no alternate branch'),
       ('ITEM_TRI_PASS','OneIsland_PokemonCenter_1F_EventScript_MeetCelioScene','source giveitem_msg result is not branched'),
       ('ITEM_TOWN_MAP','OneIsland_PokemonCenter_1F_EventScript_ReceiveTownMap','only when Pallet rival house scene <2'),
       ('ITEM_IAPAPA_BERRY','ThreeIsland_BerryForest_EventScript_Lostelle','continues rescue with no deferred reward'),
       ('ITEM_MOON_STONE','TwoIsland_JoyfulGameCorner_EventScript_ReceiveMoonStone','deferred reward flag; Meteorite handoff remains complete'),
       ('ITEM_FULL_RESTORE','ThreeIsland_EventScript_GiveFullRestore','receipt flag remains unset, allowing retry')]]
    data['healLocations']=[x for x in src.json('src/data/heal_locations.json')['heal_locations'] if x['id'] in ['HEAL_LOCATION_ONE_ISLAND','HEAL_LOCATION_TWO_ISLAND','HEAL_LOCATION_THREE_ISLAND']]
    data['initialSetFlags']=[s for s in re.findall(r'\bsetflag\s+(FLAG_\w+)', '\n'.join(r['text'] for r in bylabel['EventScript_ResetAllMapFlags']['lines'])) if any(x in s for x in ['ONE_ISLAND','TWO_ISLAND','THREE_ISLAND','LOSTELLE'])]
    # Reuse the reviewed constant parser, retaining only symbols in this inventory.
    shared=reader.load_helper('_regional_service_shared.py')
    const=shared.constants_inventory(src,worlds,doc)
    wanted=set(re.findall(r'\b[A-Z][A-Z0-9_]+\b',json.dumps(doc)+json.dumps([w['events'] for w in worlds])+json.dumps(data)))
    const['values']={k:v for k,v in const['values'].items() if k in wanted}
    contract=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,coreMaps=CORE,supportMaps=SUPPORT,reusedAnchorMaps=ANCHOR,
      mapTopology=links,itemPickups=items,hiddenItems=hidden,flags=sorted(set(re.findall(r'\bFLAG_\w+',tokens))),variables=sorted(set(re.findall(r'\bVAR_\w+',tokens))),
      ferryDestinations=[dict(origin=s,destination=d,destinationMap=m,x=x,y=y) for s,d,m,x,y in [
       ('SEAGALLOP_CINNABAR_ISLAND','SEAGALLOP_ONE_ISLAND','MAP_ONE_ISLAND_HARBOR',8,5),
       ('SEAGALLOP_ONE_ISLAND','SEAGALLOP_CINNABAR_ISLAND','MAP_CINNABAR_ISLAND',21,7)]],
      ferryIslandDestinations=[dict(destination='SEAGALLOP_'+n+'_ISLAND',destinationMap='MAP_'+n+'_ISLAND_HARBOR',x=8,y=5) for n in ['ONE','TWO','THREE']],
      initialTiming=dict(badge='FLAG_BADGE07_GET',requiresGameClear=False,requiresNationalDex=False,scopeEndsAtCinnabarScene=4),
      criticalBranches=json.loads((OUT/'branch_contract.json').read_text()),
      limitations=['Preparation only; none of the14 new maps is admitted in the current Cinnabar profile.',
        'Full script inventory includes postgame and multiplayer dependencies; those branches are guarded/outside this bounded route.',
        'Map data contains authentic blocks, metatile attributes and events; no rendered resources or compiled runtime profile is produced.',
        'No runtime, GUI, battle, save/load or physical audio validation was run.'])
    geometry=[]
    for w in worlds:
        geo=dict(sourceName=w['sourceName'],stableId=w['stableId'],width=w['width'],height=w['height'],layout=w['layout'],events=w['events'],sourceRef=w['sourceRef'],
          blocksBase64=base64.b64encode(src.read(w['layout']['blockdata_filepath'])).decode(),borderBase64=base64.b64encode(src.read(w['layout']['border_filepath'])).decode(),
          metatileAttributes=sorted({(c['metatileId'],c['rawAttributes']) for c in w['cells']}),behaviorCounts=dict(sorted(collections.Counter(c['behaviorName'] for c in w['cells']).items())))
        geometry.append(geo)
    # Exact primary scripts and selected ferry C source, no broad raw-source cache.
    outputs={f'critical-original-scripts/data/maps/{n}/scripts.inc':src.read(f'data/maps/{n}/scripts.inc') for n in CORE+SUPPORT+ANCHOR}
    for p in ['data/scripts/seagallop.inc','src/seagallop.c','data/maps/ThreeIsland_DunsparceTunnel/scripts.inc','data/maps/VermilionCity/scripts.inc']:outputs['critical-original-scripts/'+p]=src.read(p)
    outputs.update({'contracts.json':canonical(contract),'scripts.json':canonical(doc),'declarations.json':canonical(data),'catalog-delta.json':canonical(delta),'constants.json':canonical(const),'geometry.json':canonical(dict(maps=geometry,tilesets=TILESETS))})
    lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=CORE+SUPPORT)
    outputs['source-lock.json']=canonical(lock)
    manifest=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,tree=reader.TREE,mapCount=len(worlds),coreMapCount=len(CORE),supportMapCount=len(SUPPORT),cellCount=sum(w['width']*w['height'] for w in worlds),inputCount=len(src.entries),scriptBlockCount=len(doc['blocks']),trainerCount=len(data['trainers']),
      sourceHash=sha(''.join(p+'\0'+e['sha256']+'\n' for p,e in sorted(src.entries.items())).encode()),readerSha256=READER_SHA,preparerSha256=sha(Path(__file__).read_bytes()),branchContractSha256=sha((OUT/'branch_contract.json').read_bytes()),canonicalCatalogSha256=reader.CANONICAL_SHA,
      outputs=[dict(path=p,sha256=sha(b),byteLength=len(b)) for p,b in sorted(outputs.items())])
    outputs['manifest.json']=canonical(manifest)
    for p,b in outputs.items():
        dest=OUT/p;need(not any(x.is_symlink() for x in (dest,*dest.parents)),'symlink')
        if a.check:need(dest.read_bytes()==b,'regeneration differs '+p)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputBytes=sum(map(len,outputs.values())),outputCount=len(outputs),manifestSha256=sha(outputs['manifest.json']),catalogDelta={k:v for k,v in delta.items() if k not in ['inputs','ownedSpecies','missingMaps']}),indent=2))
if __name__=='__main__':main()
