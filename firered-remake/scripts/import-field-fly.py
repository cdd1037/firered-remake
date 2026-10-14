#!/usr/bin/env python3
"""Import a bounded original Fly contract and HM02 delta. No upstream code is run.
REFERENCE DEPENDENCY_ROOT OUTPUT_ROOT [--check]. Reuses exact existing raw files;
only missing text inputs and the two small generated documents are materialized.
"""
import argparse, hashlib, json, re, subprocess
from pathlib import Path

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/field-fly/'
RAW = 'content/raw/field-fly/'
DEPENDENCY = 'content/generated/game-corner-prizes/machines.json'
DEPENDENCY_HASH = 'c30c6f9ef42bef171aa4b618438c4646c32a99816ccd2893f4d12fcde118e2bf'
def sha(data): return hashlib.sha256(data).hexdigest()
def encode(value): return (json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode()
def fingerprint(value): return sha(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode())
def need(value, reason):
    if not value: raise ValueError(reason)

def generate(reference, dependency):
    need(subprocess.check_output(['git','-C',str(reference),'rev-parse',COMMIT+'^{tree}']).decode().strip()==TREE, 'Pinned source tree')
    # Fixed authored locations make rebuilds stable when unrelated importers add
    # another copy of the same source filename elsewhere in content/raw.
    locked=json.loads(Path(__file__).with_name('field-fly.contract.json').read_text())
    locations={row['path']:row for row in locked['inputs']}
    raw, inputs, cache = {}, {}, {}
    def read(path):
        if path in cache: return cache[path]
        data=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+path])
        # Reuse exact previously authenticated bytes; no regional resource trees.
        need(path in locations,'Unlisted input '+path);locked_input=locations[path]
        need(sha(data)==locked_input['sha256'] and len(data)==locked_input['byteLength'],'Pinned input '+path)
        local=locked_input['localPath'];shared=locked_input['shared']
        if shared: need((dependency/local).read_bytes()==data,'Shared source bytes '+path)
        if not shared: raw[local]=data
        inputs[path]=dict(path=path,localPath=local,shared=shared,byteLength=len(data),sha256=sha(data),
            gitBlobSha1=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest())
        cache[path]=data; return data
    def text(path): return read(path).decode()
    def source(path,symbol,start,end=None):
        return dict(path=path,symbol=symbol,startLine=start,endLine=start if end is None else end,sha256=sha(read(path)))
    def match(path,pattern):
        found=list(re.finditer(pattern,text(path),re.M|re.S));need(len(found)==1,'Exact declaration '+path+': '+pattern);return found[0]
    def number(path,symbol):
        if symbol=='MAX_TRAINERS_COUNT': path='include/constants/opponents.h'
        expression=match(path,r'^#define '+symbol+r'[ \t]+([^\n]+)')[1].split('//')[0].strip()
        if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expression): return int(expression,0)
        need(re.fullmatch(r'\(\w+(?: [+-] \w+)+\)',expression),'Unsupported constant '+symbol+': '+expression)
        tokens=expression[1:-1].split();total=0;sign=1
        for token in tokens:
            if token in ['+','-']: sign=1 if token=='+' else -1
            else: total+=sign*(int(token,0) if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',token) else number(path,token))
        return total
    def stable(symbol): return 'map.'+re.sub(r'ROUTE(\d+)',r'ROUTE_\1',symbol.removeprefix('MAP_')).lower().replace('_','-')
    dep=(dependency/DEPENDENCY).read_bytes();need(sha(dep)==DEPENDENCY_HASH,'Exact current 174 species dependency')
    owners=json.loads(dep)['additions'][0]['compatibility'];need(len(owners)==174,'Current owner count')
    region_path='src/region_map.c'; region=text(region_path)
    sections=json.loads(read('src/data/region_map/region_map_sections.json'))['map_sections']
    ids=[s['id'] for s in sections]; kanto=ids.index('MAPSEC_PALLET_TOWN'); sevii=ids.index('MAPSEC_ONE_ISLAND')
    regions=[dict(sectionId=s,region=0) for s in ids[kanto:sevii]]
    for group,number_ in [('SEVII123',1),('SEVII45',2),('SEVII67',3)]:
        block=match(region_path,r'\[REGIONMAP_'+group+r' - 1\]\s*=\s*\{(.*?)\}')[1]
        regions.extend(dict(sectionId=s,region=number_) for s in re.findall(r'MAPSEC_\w+',block) if s!='MAPSEC_NONE')
    groups={s['sectionId']:s['region'] for s in regions}
    heals=json.loads(read('src/data/heal_locations.json'))['heal_locations'];destinations=[]
    flags=text('include/constants/flags.h')
    map_names={}
    map_groups=json.loads(read('data/maps/map_groups.json'))
    for group in map_groups['group_order']:
        for name in map_groups[group]: map_names[name.replace('_','').upper()]=name
    # Map names are derivable from the source group list without reading every map.
    for heal in heals:
        entry=match(region_path,r'\[(MAPSEC_\w+)\s*- KANTO_MAPSEC_START\]\s*=\s*\{MAP\('+heal['map']+r'\),\s*'+heal['id']+r'\}')
        section=entry[1]
        m=match(region_path,r'case '+section+r':\s*(?:if \(!GetRegionMapPermission\(MAPPERM_HAS_FLY_DESTINATIONS\)\)\s*return MAPSECTYPE_NONE;\s*)?return FlagGet\((FLAG_\w+)\) \? MAPSECTYPE_VISITED : MAPSECTYPE_NOT_VISITED;')
        flag=m[1]; origin=heal['respawn_map'] if section in ['MAPSEC_ROUTE_4_POKECENTER','MAPSEC_ROUTE_10_POKECENTER'] else heal['map']
        name=map_names[origin.removeprefix('MAP_').replace('_','')]; script='data/maps/'+name+'/scripts.inc'; body=text(script)
        flag_line=body[:body.index('setworldmapflag '+flag)].count('\n')+1
        transition=body[:body.index('setworldmapflag '+flag)].split('::')[-2].splitlines()[-1]
        need('MAP_SCRIPT_ON_TRANSITION, '+transition in body,'Visited flag set on entry: '+name)
        destinations.append(dict(sectionId=section,region=groups[section],mapSymbol=heal['map'],mapId=dict(value=stable(heal['map'])),
            healLocation=heal['id'],healLocationId=heals.index(heal)+1,x=heal['x'],y=heal['y'],visitedFlag=flag,
            visitedFlagId=number('include/constants/flags.h',flag),visitMapSymbol=origin,visitSource=source(script,transition,flag_line)))
    need(len(destinations)==20 and len({d['sectionId'] for d in destinations})==20,'20 original Fly destinations')
    path='src/data/items.json'; items=text(path); item=next(x for x in json.loads(items)['items'] if x['itemId']=='ITEM_HM02')
    pos=items.index('"itemId": "ITEM_HM02"'); start=items.index('"english"',items.rfind('{',0,pos));end=items.index('}',pos)
    value=dict(id=dict(value='pret:ITEM_HM02'),sourceItemSymbol='ITEM_HM02',sourceItemId=number('include/constants/items.h','ITEM_HM02'),
        name=item['english'],price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=item['type'],
        holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],
        battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=False,canSell=False,canGive=False,
        source=source(path,'ITEM_HM02',items[:start].count('\n')+1,items[:end].count('\n')))
    path='src/move_descriptions.c'; m=match(path,r'const u8 gMoveDescription_Fly\[\] = _\("(.*?)"\);'); line=text(path)[:m.start()].count('\n')+1
    machine=dict(item=value,description=json.loads('"'+m[1]+'"'),declarationDescription=item['description_english'].replace('\\n','\n'),descriptionSource=source(path,'gMoveDescription_Fly',line))
    compatibility=[];path='src/data/pokemon/tmhm_learnsets.h'
    for owner in owners:
        symbol=owner['species']['value'][5:];m=match(path,r'\['+symbol+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),');line=text(path)[:m.start()].count('\n')+1
        compatibility.append(dict(species=owner['species'],sourceSpeciesId=owner['sourceSpeciesId'],canLearn='TMHM(HM02_FLY)' in m[0],source=source(path,symbol,line,line+m[0].count('\n'))))
    hm02=dict(machine=machine,move=dict(value='pret:MOVE_FLY'),sourceMoveId=number('include/constants/moves.h','MOVE_FLY'),movePp=15,compatibility=compatibility)
    move=match('src/data/battle_moves.h',r'\[MOVE_FLY\]\s*=\s*\{(.*?)\},')[1]
    need(re.search(r'\.pp\s*=\s*15,',move),'Fly PP15')
    for item_symbol in ['ITEM_HM02','ITEM_TM40']:
        need(re.search(r'\['+item_symbol+r'\]\s*=\s*\{gItemIcon_TMHM, gItemIconPalette_FlyingTMHM\}',text('src/data/item_icon_table.h')),'HM02 icon reuse')
    need(re.findall(r'MOVE_\w+',match('src/data/party_menu.h',r'sTMHMMoves\[\]\s*=\s*\{(.*?)\};')[1])[51]=='MOVE_FLY','Original HM02 slot')
    house='data/maps/Route16_House/scripts.inc';gift=text(house)
    need('goto_if_set FLAG_GOT_HM02' in gift and 'checkitemspace ITEM_HM02' in gift and 'giveitem_msg Route16_House_Text_ReceivedHM02FromGirl, ITEM_HM02' in gift and 'FLAG_BADGE' not in gift,'Original one-time gift')
    # Authenticate bounded predicate vectors without running upstream C.
    for path in ['src/party_menu.c','src/overworld.c','src/field_effect.c','include/constants/map_types.h','include/constants/party_menu.h']: read(path)
    need(number('include/constants/party_menu.h','FIELD_MOVE_FLY')==2,'Thunder Badge field index')
    party=text('src/party_menu.c')
    need('FlagGet(FLAG_BADGE01_GET + fieldMove)' in party and 'MenuHelpers_IsLinkActive() == TRUE || InUnionRoom() == TRUE' in party,'Field move admission')
    callback=match('src/party_menu.c',r'static bool8 SetUpFieldMove_Fly\(void\)\s*\{(.*?)\n\}')[1]
    need('Overworld_MapTypeAllowsTeleportAndFly(gMapHeader.mapType)' in callback,'Original map-type callback')
    types=match('src/overworld.c',r'bool8 Overworld_MapTypeAllowsTeleportAndFly\(u8 mapType\)\s*\{(.*?)\n\}')[1]
    need(re.findall(r'mapType == (MAP_TYPE_\w+)',types)==['MAP_TYPE_ROUTE','MAP_TYPE_TOWN','MAP_TYPE_OCEAN_ROUTE','MAP_TYPE_CITY'],'Original allowed map types')
    actions=match('src/party_menu.c',r'static void SetPartyMonFieldSelectionActions\(struct Pokemon \*mons, u8 slotId\)\s*\{(.*?)\n\}')[1]
    need('MON_DATA_MOVE1' in actions and 'MON_DATA_HP' not in actions and 'MON_DATA_PP' not in actions,'No HP or PP field-menu gate')
    need('actionType = ACTIONS_SWITCH;' in party and 'if (GetMonData(mon, MON_DATA_IS_EGG))' in party,'Eggs excluded from field action menu')
    need('SetWarpDestination(warp->mapGroup, warp->mapNum, -1, warp->x, warp->y);' in text('src/overworld.c'),'Coordinate exterior warp')
    need('SetMainCallback2(CB2_ReturnToPartyMenuFromFlyMap);' in region,'Cancel returns to party')
    field=dict(sourceCommit=COMMIT,destinations=destinations,sectionRegions=regions)
    contract=dict(schemaVersion=1,sourceCommit=COMMIT,sourceTree=TREE,upstreamScriptsExecuted=False,romDownloaded=False,
        dependencies=[dict(path=DEPENDENCY,byteLength=len(dep),sha256=DEPENDENCY_HASH)],inputs=sorted(inputs.values(),key=lambda p:p['path']),
        fieldSemanticSha256=fingerprint(field),hm02SemanticSha256=fingerprint(hm02),speciesCount=174,compatibleSpeciesCount=sum(c['canLearn'] for c in compatibility),
        iconAlias=dict(item='pret:ITEM_HM02',sourceItem='pret:ITEM_TM40'),giftFlagId=number('include/constants/flags.h','FLAG_GOT_HM02'),
        outputs=[dict(path=BASE+'field.json',byteLength=len(encode(field)),sha256=sha(encode(field))),dict(path=BASE+'hm02.json',byteLength=len(encode(hm02)),sha256=sha(encode(hm02)))])
    return {**raw,BASE+'field.json':encode(field),BASE+'hm02.json':encode(hm02),'scripts/field-fly.contract.json':encode(contract)},contract

def main():
    p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('dependency',type=Path);p.add_argument('output',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    outputs,contract=generate(a.reference,a.dependency)
    for path,data in outputs.items():
        target=a.output/path
        if a.check: need(target.read_bytes()==data,'Reproducibility: '+path)
        else: target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(files=len(outputs),sourceInputs=len(contract['inputs']),compatible=contract['compatibleSpeciesCount'],
        fieldHash=contract['fieldSemanticSha256'],hm02Hash=contract['hm02SemanticSha256'],contractHash=sha(encode(contract)),outputs=contract['outputs'])))
if __name__=='__main__': main()
