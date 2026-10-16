"""Pure rod/map projections from retained original FireRed encounter definitions."""
import re

RODS = [('old', 'old_rod', [0, 1], [70, 30]),
        ('good', 'good_rod', [2, 3, 4], [60, 20, 20]),
        ('super', 'super_rod', [5, 6, 7, 8, 9], [40, 40, 15, 4, 1])]


def convert(source):
    groups = [g for g in source['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders']
    if len(groups) != 1:
        raise ValueError('Missing unique wild header group')
    group = groups[0]
    fields = [f for f in group['fields'] if f['type'] == 'fishing_mons']
    if len(fields) != 1:
        raise ValueError('Missing unique fishing field')
    field = fields[0]
    for _, name, indexes, weights in RODS:
        if field['groups'][name] != indexes or [field['encounter_rates'][i] for i in indexes] != weights:
            raise ValueError('Fishing source slot group differs: ' + name)
    ordinary, safari, seen = [], [], set()
    for encounter in group['encounters']:
        if not encounter['base_label'].endswith('_FireRed') or 'fishing_mons' not in encounter:
            continue
        symbol = encounter['map']
        if symbol in seen:
            raise ValueError('Duplicate FireRed fishing map: ' + symbol)
        seen.add(symbol)
        fish = encounter['fishing_mons']
        if len(fish['mons']) != 10 or not 1 <= fish['encounter_rate'] <= 255:
            raise ValueError('Fishing source slots or rate differ: ' + symbol)
        stable = re.sub(r'route(\d+)', r'route-\1', 'map.' + symbol.removeprefix('MAP_').lower().replace('_', '-'))
        for rod, _, indexes, _ in RODS:
            slots = []
            for index in indexes:
                mon = fish['mons'][index]
                if not 1 <= mon['min_level'] <= mon['max_level'] <= 100:
                    raise ValueError('Invalid fishing source level: ' + symbol)
                slots.append(dict(index=index, species=dict(value='pret:' + mon['species']),
                                  minimumLevel=mon['min_level'], maximumLevel=mon['max_level'],
                                  weight=field['encounter_rates'][index]))
            target = safari if symbol.startswith('MAP_SAFARI_ZONE_') else ordinary
            target.append(dict(rod=rod, table=dict(map=dict(value=stable), rawEncounterRate=fish['encounter_rate'], slots=slots)))
    if not ordinary or not safari:
        raise ValueError('Empty ordinary or Safari fishing source')
    key = lambda row: (row['table']['map']['value'], [r[0] for r in RODS].index(row['rod']))
    return dict(schemaVersion=1, tables=sorted(ordinary, key=key), sourceOnlyTables=sorted(safari, key=key))
