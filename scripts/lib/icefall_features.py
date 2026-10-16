"""Source-derived icefall features; no map/pixel or inventory publication."""
import re
import collections
from map_runtime import require as need


def ice_fall_pairs(src, worlds):
    text=src.text('src/field_tasks.c')
    match=re.search(r'static const u8 sIcefallCaveIceCoords\[\]\[2\] =\s*\{(.*?)\n\};',text,re.S)
    need(match,'Missing thin-ice coordinate table')
    coords=[(int(x),int(y)) for x,y in re.findall(r'\{\s*(\d+),\s*(\d+)\s*\}',match[1])]
    need(coords and len(set(coords))==len(coords),'Invalid thin-ice coordinates')
    world=next(w for w in worlds if w['sourceName']=='FourIsland_IcefallCave_1F')
    for x,y in coords:
        need(0<=x<world['width'] and 0<=y<world['height'] and world['cells'][y*world['width']+x]['behaviorId']==0x26,'Thin ice source geometry differs')
    return [dict(source=dict(x=x,y=y)) for x,y in coords]


def doll_collection_contract(src, worlds, bylabel):
    """Convert the source special's monotonic stat-gated flag clears to data."""
    special = 'UpdateLoreleiDollCollection'
    source = src.text('src/field_specials.c')
    match = re.search(r'^void ' + special + r'\(void\)\n\{(.*?)^\}', source, re.M | re.S)
    need(match, 'Missing doll collection source special')
    body = match[1]
    stat = re.match(r'\s*u32 (\w+) = GetGameStat\((GAME_STAT_\w+)\);', body)
    need(stat, 'Doll collection stat declaration differs')
    checks = list(re.finditer(r'if \(' + re.escape(stat[1]) + r' >= (\d+)\)\s*\{?\s*FlagClear\((FLAG_\w+)\);', body))
    # The leading threshold encloses the remaining checks. Reject additional
    # source operations rather than silently dropping new behavior.
    remainder = body[stat.end():]
    remainder = re.sub(r'if \(' + re.escape(stat[1]) + r' >= \d+\)\s*\{?\s*FlagClear\(FLAG_\w+\);', '', remainder)
    need(checks and re.fullmatch(r'\s*\}\s*', remainder), 'Unsupported doll collection source operations')
    rows = [dict(stat=stat[2], minimum=int(check[1]), flag=check[2]) for check in checks]
    need(all(row['minimum'] > 0 for row in rows) and
         [row['minimum'] for row in rows] == sorted({row['minimum'] for row in rows}) and
         len({row['flag'] for row in rows}) == len(rows), 'Doll collection thresholds differ')
    house = next(world for world in worlds if world['sourceName'] == 'FourIsland_LoreleisHouse')
    doll_flags = {event['flag'] for event in house['events']['object_events']
                  if event['script'] == 'FourIsland_LoreleisHouse_EventScript_Doll' and event['flag'] != '0'}
    need({row['flag'] for row in rows} == doll_flags, 'Doll collection flags do not match house actors')
    initial_flags = {line['text'].strip().split()[1] for line in bylabel['EventScript_ResetAllMapFlags']['lines']
                     if line['text'].strip().startswith('setflag ')}
    need(doll_flags <= initial_flags, 'New-game doll hide flags missing from script closure')
    transition = bylabel['FourIsland_LoreleisHouse_OnTransition']['lines']
    need(any(line['text'].strip() == 'special ' + special for line in transition), 'Doll collection transition differs')
    return dict(statFlagUnlockSpecial=special, statFlagUnlocks=rows)

def sticker_milestones(src):
    script = src.text('data/scripts/trainer_card.inc')
    native = src.text('src/field_specials.c')
    body = re.search(r'u16 StickerManGetBragFlags\(void\)\s*\{(.*?)\n\}', native, re.S)[1]
    rows = []
    for index, (name, branch, stat, variable, clamp) in enumerate([
        ('HOF', 'HoF', 'GAME_STAT_ENTERED_HOF', 'VAR_0x8004', False),
        ('EGG', 'Eggs', 'GAME_STAT_HATCHED_EGGS', 'VAR_0x8005', True),
        ('LINK_WIN', 'LinkWins', 'GAME_STAT_LINK_BATTLE_WINS', 'VAR_0x8006', False)]):
        block = re.search(r'FourIsland_House2_EventScript_Brag' + branch + r'::(.*?)\n\s*end', script, re.S)[1]
        upper = [int(v) for v in re.findall(r'goto_if_le ' + variable + r', (\d+),', block)]
        need(len(upper) == 3 and stat in body, 'Missing Sticker Man source thresholds/stat')
        need('result |= 1 << ' + str(index) in body, 'Sticker Man bit assignment differs')
        award = 'VAR_' + name + '_BRAG_STATE'
        need(all(re.search(r'setvar ' + award + ', ' + str(t), script) for t in range(1,5)), 'Missing source award tiers')
        rows.append(dict(id=name,stat=stat,countVariable=variable,stateVariable=award,
                         bragFlag=1 << index,clampCount=clamp,minimumCounts=[1]+[v+1 for v in upper]))
    need('if (numEggs > 0xFFFF)' in body and 'gSpecialVar_0x8005 = 0xFFFF;' in body,
         'Sticker Man egg clamp differs')
    return dict(map='map.four-island-house2', special='StickerManGetBragFlags',
                entryScript='FourIsland_House2_EventScript_StickerMan', categories=rows)
