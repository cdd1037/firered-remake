#!/usr/bin/env python3
"""Import pinned Quest Log captions, event budgets, map labels, and exclusion rules."""
import argparse,json,re,pathlib
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parents[3]
RAW=ROOT/'content/raw/pokefirered/037335f4'
def block(text,name):
    match=re.search(r'\b'+re.escape(name)+r'\[\][^=]*=\s*\{(.*?)\};',text,re.S)
    if not match: raise ValueError('missing '+name)
    return match.group(1)
def generate(raw=RAW, world_path=ROOT/"content/generated/maps/world.json"):
    raw=pathlib.Path(raw)
    events=(raw/'src/quest_log_events.c').read_text(); constants=(raw/'include/constants/quest_log.h').read_text()
    texts={name:json.loads('"'+value+'"') for name,value in re.findall(r'const u8 gText_QuestLog_(\w+)\[\] = _\("((?:[^"\\]|\\.)*)"\);',(raw/'src/data/text/quest_log.h').read_text())}
    ids={k:int(v) for k,v in re.findall(r'#define (QL_EVENT_\w+)\s+(\d+)\b',constants)}
    sizes={ids[k]:sum(int(v) for v in expr.replace('CMD_HEADER_SIZE','4').split('+')) for k,expr in re.findall(r'\[(QL_EVENT_\w+)\]\s*=\s*([\w +]+),?',block(events,'sQuestLogEventCmdSizes'))}
    required_texts=set(re.findall(r'gText_QuestLog_(\w+)',events))
    if set(sizes)!=set(ids.values()) or not required_texts.issubset(texts):raise ValueError('incomplete source tables')
    world=json.loads(pathlib.Path(world_path).read_text()); symbols=world['mapSymbols']; labels={}
    names={row['id']:row.get('name','') for row in json.loads((raw/'src/data/region_map/region_map_sections.json').read_text())['map_sections']}
    for path in (raw/'data/maps').glob('*/map.json'):
        row=json.loads(path.read_text())
        if row['id'] in symbols and names.get(row['region_map_section']):labels[symbols[row['id']]]=names[row['region_map_section']]
    # Runtime aliases in world entries take precedence over generic symbol spelling.
    for row in world['maps']:
        if row['sourceMapSymbol'] in symbols and symbols[row['sourceMapSymbol']] in labels:labels[row['stableId']]=labels[symbols[row['sourceMapSymbol']]]
    flags=re.findall(r'FLAG_WORLD_MAP_\w+',block(events,'sWorldMapFlags'))
    disabledbody=re.search(r'static bool8 InQuestLogDisabledLocation\(void\)\s*\{(.*?)\n\}',events,re.S).group(1)
    disabled=sorted({symbols[x] for x in re.findall(r'MAP_(?:GROUP|NUM)\((MAP_\w+)\)',disabledbody) if x in symbols})
    graphics={int(value):symbol for symbol,value in re.findall(r'#define (OBJ_EVENT_GFX_\w+)\s+(\d+)\b',(raw/'include/constants/event_objects.h').read_text()) if not symbol.startswith('OBJ_EVENT_GFX_VAR_')}
    locationnames=dict(re.findall(r'\[(QL_LOCATION_\w+)\]\s*=\s*gText_QuestLog_(\w+)',block(events,'sLocationNameTexts')))
    locationtypes=dict(re.findall(r'\[(QL_LOCATION_\w+)\]\s*=\s*(QL_DEPARTED_\w+)',block(events,'sLocationToDepartedTextId')))
    departuretexts=dict(re.findall(r'\[(QL_DEPARTED_\w+)\]\s*=\s*gText_QuestLog_(\w+)',block(events,'sDepartedLocationTexts')))
    departures=[]
    locationids={k:int(v) for k,v in re.findall(r"#define (QL_LOCATION_\w+)\s+(\d+)\b",constants)}
    gymsections=re.findall(r"MAPSEC_\w+",re.search(r"sGymCityMapSecs\[NUM_BADGES\]\s*=\s*\{(.*?)\};",events,re.S).group(1))
    mapsections={row["id"]:row["region_map_section"] for path in (raw/"data/maps").glob("*/map.json") for row in [json.loads(path.read_text())]}
    specials=(raw/'src/field_specials.c').read_text()
    pairs=re.findall(r'\[(QL_LOCATION_\w+)\]\s*=\s*\{MAP\((MAP_\w+)\),\s*MAP\((MAP_\w+)\)\}',block(specials,'sInsideOutsidePairs'))
    for location,inside,outside in pairs:
        if inside in symbols and outside in symbols:
            departures.append({'inside':symbols[inside],'outside':symbols[outside],'textKey':departuretexts[locationtypes[location]],'place':texts[locationnames[location]],'kind':locationtypes[location].removeprefix('QL_DEPARTED_'),'locationId':locationids[location],'locationSymbol':location,'regionName':names['MAPSEC_ROUTE_2'] if location.startswith('QL_LOCATION_VIRIDIAN_FOREST_') else names[mapsections[inside]],'badgeFlag':f'FLAG_BADGE{gymsections.index(mapsections[inside])+1:02d}_GET' if locationtypes[location]=='QL_DEPARTED_GYM' and mapsections[inside] in gymsections else None})
    speciesconstants={symbol:int(value) for symbol,value in re.findall(r'#define (SPECIES_\w+)\s+(\d+)\b',(raw/'include/constants/species.h').read_text())}
    specialbody=re.search(r'static bool8 IsSpeciesFromSpecialEncounter\(u16 species\)\s*\{(.*?)\n\}',events,re.S).group(1)
    excluded=[speciesconstants[x] for x in re.findall(r'case (SPECIES_\w+):',specialbody)]
    itembody=re.search(r'void TrySetObtainedItemQuestLogEvent\(u16 itemId\)\s*\{(.*?)\n\}',(raw/'src/item.c').read_text(),re.S).group(1)
    storyitems=list(dict.fromkeys(re.findall(r'itemId == (ITEM_\w+)',itembody)))
    palette_lines=(raw/'graphics/text_window/stdpal_4.pal').read_text().splitlines()
    if palette_lines[:3]!=['JASC-PAL','0100','16']:raise ValueError('invalid Quest Log window palette')
    palette=[[(int(v)>>3)*8+(int(v)>>5) for v in line.split()] for line in palette_lines[3:19]]
    with Image.open(raw/'graphics/quest_log/description_window.png') as window:
        if window.mode!='P' or window.size!=(8,24):raise ValueError('invalid Quest Log window tiles')
        pixels=[window.getpixel((x,y)) for y in range(24) for x in range(8)]
    if len(palette)!=16 or any(len(c)!=3 for c in palette) or any(p<0 or p>15 for p in pixels):raise ValueError('invalid Quest Log window colors')
    result={'windowPalette':palette,'windowPixels':pixels,'storyItems' :storyitems,'townMapGiftMap':symbols.get('MAP_PALLET_TOWN_RIVALS_HOUSE',''),'excludedWildSpecies':excluded,'departures':departures,'graphics':graphics,'texts':texts,'mapNames':labels,'eventSizes':sizes,'arrivalFlags':flags,'disabledMaps':disabled}
    return result

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=RAW)
    parser.add_argument('--world',type=pathlib.Path,default=ROOT/'content/generated/maps/world.json')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/quest-log.json')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    data=generate(args.raw,args.world)
    encoded=json.dumps(data,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n'
    if args.check:
        if not args.out.is_file() or args.out.read_text()!=encoded:
            raise SystemExit('Quest Log generated data differs: '+str(args.out))
        print('Quest Log source regeneration check passed (read-only).')
    else:
        args.out.parent.mkdir(parents=True,exist_ok=True)
        args.out.write_text(encoded)
        print(f"Imported {len(data['texts'])} captions, {len(data['mapNames'])} map names and {len(data['eventSizes'])} event sizes.")
if __name__=='__main__':main()
