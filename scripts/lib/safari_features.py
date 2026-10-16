"""Compile live safari business contracts from pinned raw source.

No generated input, rendering, publication, or package inventories.
"""
import re


def compile_safari(src, worlds, scripts, constants):
    """Extract source values; runtime enablement belongs to explicit application handlers."""
    by_label={b['label']:b for b in scripts['blocks']}
    def script(label):
        b=by_label[label]
        return dict(label=label, sourceRef=b['sourceRef'], lines=b['lines'])
    c=src.text('src/safari_zone.c')
    enter=re.search(r'void EnterSafariMode\(void\)\n\{(.*?)\n\}',c,re.S)
    if not enter:raise ValueError('Missing EnterSafariMode source')
    balls=int(re.search(r'gNumSafariBalls = (\d+);',enter[1])[1]);steps=int(re.search(r'gSafariZoneStepCounter = (\d+);',enter[1])[1])
    entry=script('FuchsiaCity_SafariZone_Entrance_EventScript_TryEnterSafariZone')
    fees=[int(re.search(r'\b'+opcode+r' (\d+)\b','\n'.join(r['text'] for r in entry['lines']))[1]) for opcode in ('checkmoney','removemoney')]
    if fees!=[500,500] or balls!=30 or steps!=600:raise ValueError('Safari source session contract differs')
    items=[]
    for w in worlds:
        for i,o in enumerate(w['events']['object_events'],1):
            if o['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
            body=script(o['script']);item=re.search(r'(?:finditem|giveitem(?:_std|_msg)?)\s+(ITEM_\w+)', '\n'.join(x['text'] for x in body['lines']))
            if not item:raise ValueError('Missing source item pickup: '+o['script'])
            items.append(dict(map=w['stableId'],localId=i,item=item[1],flag=o['flag'],x=o['x'],y=o['y'],label=o['script']))
    return dict(
        schemaVersion=1,
        entryFee=fees[0],
        startingBalls=balls,
        startingSteps=steps,
        pickups=items,
        hiddenItems=[dict(map=w['stableId'], declaration=e) for w in worlds for e in w['events']['bg_events'] if e['type'] == 'hidden_item'],
    )

def compile_safari_encounters(src):
    path='src/data/wild_encounters.json';doc=src.json(path);weights=[20,20,10,10,10,10,5,5,4,4,1,1];tables=[];species=set()
    for area in ('Center','East','North','West'):
        symbol='sSafariZone'+area+'_FireRed';matches=[e for g in doc['wild_encounter_groups'] for e in g.get('encounters',[]) if e['base_label']==symbol]
        if len(matches)!=1 or len(matches[0]['land_mons']['mons'])!=12:raise ValueError('Missing exact FireRed Safari table')
        row=matches[0];land=row['land_mons'];mapid='map.safari-zone-'+area.lower()
        tables.append(dict(map=dict(value=mapid),rawEncounterRate=land['encounter_rate'],slots=[dict(index=i,species=dict(value='pret:'+m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=weights[i]) for i,m in enumerate(land['mons'])]))
        species.update(m['species'] for m in land['mons'])
    held=[];text=src.text('src/data/pokemon/species_info.h')
    for speciesid in sorted(species):
        m=re.search(r'^    \['+speciesid+r'\] =\s*\n    \{\n(.*?)^    \},',text,re.M|re.S)
        if not m:raise ValueError('Missing species held-item row')
        f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
        held.append(dict(species='pret:'+speciesid,common=f['itemCommon'],rare=f['itemRare']))
    return dict(schemaVersion=1, tables=tables, heldItems=held, heldItemDrawBound=100)
