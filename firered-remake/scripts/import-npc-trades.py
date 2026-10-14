#!/usr/bin/env python3
"""Authenticate the nine original FireRed NPC trades and add only Cinnabar's Lounge.

The pinned source is read as Git blobs, never executed. Runtime IDs 0/2/4/6/7/8
reuse the original maps; source evidence for the other three does not admit them.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'npc-trades'
RESOURCE_ID = 'firered-npc-trades-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
READER_SHA = '13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
FOSSIL_READER_SHA = '00c8e1105f255e0895eebc5b52a18b5d199ba36a5aedb06ea3961e861b8440c8'
MANIFEST_SHA256 = 'b8a7a2b17f7114ca74b2ec4a8a10b37bb5269cd71ef8c5439a9d3183345032cb'
LOCK_SHA256 = '08c9ee2df1df83d49a2fd0a8b6f028cbea3659e239124b2c89264db646032c28'
SEMANTIC_SHA256 = 'c259397a134f07268ae6d788ff7c7f2cb66d6d19755e07c3d525f4dec92c7414'
MAPS = ['CinnabarIsland_PokemonLab_Lounge']
LIVE_SCRIPT_MAPS = MAPS + ['UndergroundPath_NorthEntrance', 'Route11_EastEntrance_2F', 'Route2_House']
ENABLED_TRADE_IDS = [0, 2, 4, 6, 7, 8]
TRADE_MAPS = ['Route2_House', 'CeruleanCity_House3', 'UndergroundPath_NorthEntrance',
              'VermilionCity_House2', 'Route11_EastEntrance_2F', 'Route18_EastEntrance_2F',
              'CinnabarIsland_PokemonLab_Lounge', 'CinnabarIsland_PokemonLab_ExperimentRoom']
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_Lab': 'secondary/lab'}
NPC_REUSE = {
 'OBJ_EVENT_GFX_SCIENTIST': ('content/generated/silph-region/npc/scientist.png', '69a795f639fcebb3e039f751b86995993f2760bed525cf91d40a912f80167568'),
 'OBJ_EVENT_GFX_OLD_MAN_2': ('content/generated/silph-region/npc/old-man-2.png', '7c0e1db72f46cfd4c6d317c5cd386c03e1b3be70b1cf489db946e2b41fdd7da6'),
 'OBJ_EVENT_GFX_WOMAN_2': ('content/generated/silph-region/npc/woman-2.png', '18f95efa875fb0e3dcd9d7e241205cc859d9d9588426dd84c4f93bdf7279c001'),
 'OBJ_EVENT_GFX_RED_NORMAL': ('content/generated/silph-region/npc/red-normal.png', '51d410e3c642aea0682cd273737df10f41a46eaf88fb952552eabe6294c3410a'),
}
def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def compact(value): return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, digest, name):
    need(sha(path.read_bytes()) == digest, 'Reviewed helper differs: ' + str(path))
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value

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

def source_ref(src, path, symbol, start, end=None):
    src.read(path)
    return dict(path=path, symbol=symbol, startLine=start, endLine=end or start, sha256=src.entries[path]['sha256'])

def numeric(src, path):
    return {m[1]: int(m[2], 0) for m in re.finditer(r'^#define\s+(\w+)\s+(0x[\da-fA-F]+|\d+)(?:\s|$)', src.text(path), re.M)}

def import_offers(src, reader):
    path = 'src/data/ingame_trades.h'; original = src.text(path); selected = firered(original)
    ids = numeric(src, 'include/constants/trade.h')
    species = numeric(src, 'include/constants/species.h')
    items = numeric(src, 'include/constants/items.h')
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
            bindings[found[1]] = dict(tradeSymbol=found[1], map='map.' + reader.directory(name), localId=local,
                scriptLabel=block[1], completionFlag=flags[0], usesInlineSequence='special DoInGameTradeScene' in block[2],
                actorSource=source_ref(src, f'data/maps/{name}/map.json', block[1], 1, len(src.text(f'data/maps/{name}/map.json').splitlines())),
                scriptSource=source_ref(src, script_path, block[1], script[:block.start()].count('\n')+1,
                    script[:block.end()].count('\n')+1))
    offers, numeric_records = [], []
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
            source=source_ref(src,path,symbol,selected[:match.start()].count('\n')+1,selected[:match.end()].count('\n')+1), **bindings[symbol])
        offers.append(data)
        numeric_records.append(dict(tradeId=ids[symbol], receivedSpecies=species[fields['species']], requestedSpecies=species[fields['requestedSpecies']], heldItem=items[fields['heldItem']]))
    need([o['tradeId'] for o in offers] == list(range(9)) and len(bindings) == 9, 'Nine original trade definitions required')
    return offers, numeric_records

def constructor_contract(src):
    refs = []
    for path, symbol in [('src/trade_scene.c','CreateInGameTradePokemonInternal'),('src/trade_scene.c','TradeMons'),
                         ('src/trade_scene.c','GetInGameTradeMail'),('src/trade_scene.c','GetTradeSpecies'),
                         ('src/pokemon.c','CreateMon'),('src/pokemon.c','CreateBoxMon'),('src/party_menu.c','ChoosePartyMonByMenuType'),('src/party_menu_specials.c','ChoosePartyMon')]:
        text = src.text(path)
        pattern = r'^(?:static )?(?:void|u16) ' + symbol + r'\([^;]*?\)\n\{.*?^\}'
        m = re.search(pattern,text,re.M|re.S); need(m is not None, 'Constructor reference missing: '+symbol)
        refs.append(dict(**source_ref(src,path,symbol,text[:m.start()].count('\n')+1,text[:m.end()].count('\n')+1),text=m[0]))
    by = {r['symbol']:r['text'] for r in refs}
    need('level, USE_RANDOM_IVS, TRUE, inGameTrade->personality, TRUE, inGameTrade->otId' in by['CreateInGameTradePokemonInternal'], 'Fixed PID/OT and discarded-IV mode differ')
    body = by['CreateBoxMon']; random_branch=body.split('u32 iv;')[1].split('if (gSpeciesInfo')[0]
    need(random_branch.count('Random()') == 2 and 'GiveBoxMonInitialMoveset(boxMon);' in body and 'gExperienceTables[gSpeciesInfo[species].growthRate][level]' in body, 'Original construction flow differs')
    need('friendship = 70;' in by['TradeMons'] and 'SWAP(*playerMon, *partnerMon' in by['TradeMons'], 'Original exchange differs')
    sentinel_path='src/data/region_map/region_map_sections.constants.json.txt'
    sentinel=numeric(src,sentinel_path)['METLOC_IN_GAME_TRADE']; need(sentinel==254,'Real met sentinel differs')
    globals=numeric(src,'include/constants/global.h'); item_ids=numeric(src,'include/constants/items.h')
    src.read('include/constants/pokemon.h'); src.read('include/global.h')
    need(re.search(r'#if defined\(FIRERED\)\s*#define GAME_VERSION VERSION_FIRE_RED',src.text('include/config.h')) is not None,'FireRed version configuration differs')
    need('#define GAME_LANGUAGE (LANGUAGE_ENGLISH)' in src.text('include/constants/global.h') and
         'const u8 gGameVersion = GAME_VERSION;' in src.text('src/main.c') and
         'const u8 gGameLanguage = GAME_LANGUAGE;' in src.text('src/main.c'),'Constructor version/language globals differ')
    return dict(metLocation='METLOC_IN_GAME_TRADE',metLocationValue=sentinel,discardedRandomIvDraws=2,fixedPersonalityDraws=0,
        fixedOriginalTrainerIdDraws=0,receivedFriendship=70,level='offered-current-level',experience='received-species-level-threshold',
        language=globals['LANGUAGE_ENGLISH'],metGame=globals['VERSION_FIRE_RED'],pokeBall='ITEM_POKE_BALL',mailNone=item_ids['MAIL_NONE'],partyCancelMinimum=globals['PARTY_SIZE'],
        fixedIvOrder=['hp','attack','defense','speed','specialAttack','specialDefense'],
        conditionOrder=['cool','beauty','cute','smart','tough'],references=refs)

def presentation_text(src):
    path='src/strings.c'; text=src.text(path); rows=[]
    for symbol in ['gText_XWillBeSentToY','gText_ByeByeVar1','gText_XSentOverY','gText_TakeGoodCareOfX',
                   'gText_ChoosePokemon','gText_ChoosePokemonCancel','gFameCheckerText_Cancel']:
        match=re.search(r'const u8 '+symbol+r'\[\] = _\(("[^"\n]*")\);',text)
        need(match is not None,'Original presentation string missing: '+symbol)
        value=json.loads(match[1])
        rows.append(dict(symbol=symbol,text=value,source=source_ref(src,path,symbol,text[:match.start()].count('\n')+1)))
    return rows

def retained_maps(src, current):
    docs = []
    for name, relative in [
        ('CinnabarIsland_PokemonLab_Entrance', 'fossil-lab-region/cinnabar-island-pokemon-lab-entrance'),
        ('CinnabarIsland_PokemonLab_ExperimentRoom', 'fossil-lab-region/cinnabar-island-pokemon-lab-experiment-room'),
        ('UndergroundPath_NorthEntrance', 'vermilion-region/underground-path-north-entrance'),
        ('Route11_EastEntrance_2F', 'route11-region/route-11-east-entrance-2f'),
        ('Route2_House', 'route11-region/route-2-house')]:
        path='content/generated/'+relative+'/runtime-map.json'
        raw=(current/path).read_bytes(); doc=json.loads(raw); events=src.json(f'data/maps/{name}/map.json')
        need(doc['events']==events,'Retained original map events differ: '+name)
        docs.append(dict(path=path,sha256=sha(raw),byteLength=len(raw),map=doc['stableId'],sourceMap=events['id'],geometryChanges=[],eventChanges=[]))
    entrance=src.json('data/maps/CinnabarIsland_PokemonLab_Entrance/map.json')['warp_events'][3]
    need(entrance==dict(x=13,y=5,elevation=0,dest_map='MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE',dest_warp_id='0'),'Original lounge entrance differs')
    return docs

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root',type=Path,required=True)
    parser.add_argument('--dependency-root',type=Path,default=ROOT)
    parser.add_argument('--project-root',type=Path,default=ROOT)
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args(); target,dep,ref=map(lambda p:Path(os.path.abspath(p)),(args.project_root,args.dependency_root,args.reference_root))
    for p in (target,dep,ref):safe(p)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Source/output overlap')
    reader=module(dep/'scripts/import-cinnabar-source-preparation.py',READER_SHA,'npc_trade_reader')
    helper=module(dep/'scripts/import-fossil-lab-region.py',FOSSIL_READER_SHA,'npc_trade_helpers')
    reader.PRODUCT,reader.TILESETS,reader.PROFILE=dep,TILESETS,RESOURCE_ID
    helper.MAPS=TRADE_MAPS; helper.NPC_REUSE=NPC_REUSE
    src=reader.Source(ref); worlds=[reader.decode(src,n) for n in MAPS]
    offers,numeric_records=import_offers(src,reader)
    scripts=helper.script_inventory(src,reader,worlds)
    constants=helper.constants_inventory(src,scripts,worlds)
    construction=constructor_contract(src); retained=retained_maps(src,dep); presentation=presentation_text(src)
    src.read('data/maps/map_groups.json','source-map-number-projection')
    links=reader.topology(src,worlds)
    need(len(links['warps'])==1 and links['warps'][0]['reciprocalIndex'],'Lounge reciprocal warp differs')
    for name in LIVE_SCRIPT_MAPS:
        for leaf in ['scripts.inc','text.inc']:
            path=f'data/maps/{name}/'+leaf
            if path in src.tree:src.read(path,'live-map-script')
    semantic=sha(compact(offers))
    if SEMANTIC_SHA256:need(semantic==SEMANTIC_SHA256,'Pinned trade semantics differ')
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE,shared.TILESETS=RESOURCE_ID,TILESETS
    converter=reader.load_helper('_reviewed_runtime_converter.py')
    with tempfile.TemporaryDirectory(prefix='npc-trades-',dir=target) as folder:
        stage=Path(folder)
        for world in worlds:shared.import_map(src,world['sourceName'],world['stableId'],stage/reader.directory(world['sourceName']))
        npcs=helper.reuse_npcs(src,shared,worlds,stage,dep)
        source_hash=sha(''.join(p+'\0'+e['sha256']+'\n' for p,e in sorted(src.entries.items())).encode())
        entries=[]
        for world in worlds:
            relative=reader.directory(world['sourceName']);path=stage/relative/'world.json';rich=path.read_bytes()
            doc=converter.convert(rich,sha(rich),source_hash);helper.compact_check(doc,world,src)
            data=converter.encode(doc);(stage/relative/'runtime-map.json').write_bytes(data);path.unlink()
            entries.append(dict(sourceName=world['sourceName'],stableId=world['stableId'],directory=relative,sourceMapSymbol=world['events']['id'],path=relative+'/runtime-map.json',sha256=sha(data),byteLength=len(data),sourceWorldSha256=sha(rich),sourceWorldByteLength=len(rich),fullSourceHash=source_hash))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False);lock_bytes=canonical(lock)
        contracts=dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,enabledTradeIds=ENABLED_TRADE_IDS,retainedMaps=retained,
            mapTopology=links,construction=construction,numericRecords=numeric_records,
            deferredTradeIds=[i for i in range(9) if i not in ENABLED_TRADE_IDS],limitations=['Only original IDs 0/2/4/6/7/8 are enabled. Other three original records are source evidence only.',
            'Jynx original Fab Mail/message is retained as evidence; no mail store or Jynx runtime admission.',
            'No source cutscene visual/audio timing parity is claimed by this content contract.'])
        docs={'catalog.json':dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,sourceTree=reader.TREE,fullSourceHash=source_hash,maps=entries,inputFiles=lock['files'],tilesets=TILESETS),
            'trades.json':offers,'presentation-text.json':presentation,'scripts.json':scripts,'constants.json':constants,'contracts.json':contracts,'npc-source.json':npcs,
            'geometry.json':dict(maps=reader.geometry_inventory(worlds),tilesets=TILESETS)}
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix(),sha256=sha(p.read_bytes()),byteLength=p.stat().st_size) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=RESOURCE_ID,resourceId=RESOURCE_ID,sourceCommit=COMMIT,sourceTree=reader.TREE,fullSourceHash=source_hash,lockSha256=sha(lock_bytes),
            readerSha256=READER_SHA,helperSha256=FOSSIL_READER_SHA,converterSha256=reader.HELPERS['_reviewed_runtime_converter.py'],rendererSha256=reader.HELPERS['_regional_service_shared.py'],
            inputCount=len(src.entries),mapCount=1,cellCount=165,borderCellCount=4,objectCount=3,tradeCount=9,enabledTradeIds=ENABLED_TRADE_IDS,tradeSemanticSha256=semantic,
            reusedNpcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_bytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifest_bytes)
        if MANIFEST_SHA256:need(sha(manifest_bytes)==MANIFEST_SHA256,'Pinned manifest differs')
        if LOCK_SHA256:need(sha(lock_bytes)==LOCK_SHA256,'Pinned lock differs')
        writes=[(target/'scripts/npc-trades.lock.json',lock_bytes)]
        writes += [(target/'content/raw'/PACKAGE/p,b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if path.suffix=='.json':json.loads(data)
            if args.check:need(path.is_file() and path.read_bytes()==data,'Regeneration differs: '+str(path))
            else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        print(json.dumps(dict(manifestSha256=sha(manifest_bytes),manifestByteLength=len(manifest_bytes),lockSha256=sha(lock_bytes),lockByteLength=len(lock_bytes),fullSourceHash=source_hash,semanticSha256=semantic,inputCount=len(src.entries),outputCount=len(outputs),mapCount=1,tradeCount=9,scriptBlockCount=len(scripts['blocks']),check=args.check),indent=2))
if __name__=='__main__':main()
