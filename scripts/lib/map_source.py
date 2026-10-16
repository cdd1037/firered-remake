"""Raw map geometry, source identities and topology shared by final converters."""
import json
import pathlib
import re
from map_runtime import require

def object_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result

def json_bytes(data):
    return json.loads(data, object_pairs_hook=object_pairs)

def require_safe_target(root, target):
    root, target = pathlib.Path(root), pathlib.Path(target)
    if root.is_symlink() or target.is_symlink():
        raise ValueError('Symlink input/output is not allowed: ' + str(target))
    for ancestor in target.parents:
        if ancestor.is_symlink():
            raise ValueError('Symlink ancestor is not allowed: ' + str(ancestor))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Target escapes selected directory')

def source_topology(src,worlds):
    allmaps={}
    for p in src.tree:
        if p.startswith('data/maps/') and p.endswith('/map.json'):
            n=p.split('/')[2]; symbol='MAP_'+re.sub(r'([a-z])([A-Z])',r'\1_\2',n).upper(); allmaps[symbol]=p
    active={w['events']['id'] for w in worlds}; warps=[]; connections=[]
    for w in worlds:
        e=w['events'];symbol=e['id']
        for i,x in enumerate(e['warp_events']):
            d=src.json(allmaps[x['dest_map']]); j=int(x['dest_warp_id']); back=d['warp_events'][j]
            warps.append(dict(sourceMap=symbol,sourceIndex=i,source=x,destination=back,
              destinationInSlice=x['dest_map'] in active,reciprocalMap=back['dest_map']==symbol,
              reciprocalIndex=back['dest_map']==symbol and int(back['dest_warp_id'])==i))
        opposite={'up':'down','down':'up','left':'right','right':'left'}
        for x in e['connections'] or []:
            d=src.json(allmaps[x['map']]);back=[y for y in d['connections'] or [] if y['map']==symbol and y['direction']==opposite[x['direction']]]
            require(len(back)==1 and back[0]['offset']==-x['offset'],'connection mismatch')
            connections.append(dict(sourceMap=symbol,source=x,destinationInSlice=x['map'] in active,reciprocal=True))
    return dict(warps=warps,connections=connections)

def source_directory(name): return re.sub(r'route(\d+)', r'route-\1', re.sub(r'([a-z])([A-Z])',r'\1-\2',name).replace('_','-').lower())

def decode_geometry(src, name, *, tilesets):
    events=src.json(f'data/maps/{name}/map.json');layout=next(x for x in src.json('data/layouts/layouts.json')['layouts'] if x.get('id')==events['layout'])
    attrs=[src.units('data/tilesets/'+tilesets[layout[k]]+'/metatile_attributes.bin','I') for k in ('primary_tileset','secondary_tileset')]
    names={int(v,16):n for n,v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)',src.text('include/constants/metatile_behaviors.h'),re.M)}
    blocks=src.units(layout['blockdata_filepath'],'H');border=src.units(layout['border_filepath'],'H')
    if len(blocks)!=layout['width']*layout['height']:raise ValueError('Grid size differs')
    cells=[]
    for i,b in enumerate(blocks):
        mid=b&1023;a=attrs[mid>=640][mid if mid<640 else mid-640];bh=a&511
        cells.append(dict(x=i%layout['width'],y=i//layout['width'],rawBlock=b,metatileId=mid,rawAttributes=a,collision=(b>>10)&3,elevation=b>>12,behaviorId=bh,behaviorName=names.get(bh,'UNKNOWN'),encounterType=(a>>24)&7))
    return dict(sourceName=name,stableId='map.'+source_directory(name),sourceRef=src.ref(f'data/maps/{name}/map.json',events['id']),width=layout['width'],height=layout['height'],layout=layout,events=events,cells=cells,borderBlocks=border)

def tileset_bindings(src):
    """Resolve tileset geometry directories from the pinned C declarations."""
    headers = src.text('src/data/tilesets/headers.h')
    declarations = src.text('src/data/tilesets/metatiles.h')
    result = {}
    for match in re.finditer(r'const struct Tileset (\w+) =\s*\{(.*?)\n\};', headers, re.S):
        symbol = re.search(r'\.metatileAttributes = (\w+)', match[2])
        require(symbol, 'Missing tileset attribute binding: ' + match[1])
        declaration = re.search(r'\b' + symbol[1] + r'\[\] = INCBIN_U32\("(.*?)"\)', declarations)
        require(declaration, 'Missing tileset attribute declaration: ' + symbol[1])
        path = declaration[1]
        require(path.startswith('data/tilesets/') and path.endswith('/metatile_attributes.bin')
                and '..' not in path.split('/'), 'Unsafe tileset declaration')
        result[match[1]] = path.removeprefix('data/tilesets/').removesuffix('/metatile_attributes.bin')
    require(result, 'Missing source tileset declarations')
    return result
