#!/usr/bin/env python3
"""Import southern routes and Fuchsia ordinary services; preserve Route12 reuse.

Read the retained 037335f4 raw cache by default, or an explicitly selected pinned
Git checkout. Raw mode trusts that pinned input selection and does not claim Git
verification. No upstream scripts are executed. Rebuild the shared field tilesets
and map atlases after importing; --check accepts both standalone and packed art.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import decode_geometry
from map_source import reciprocal_topology as topology
from map_runtime import publish_map_package

from shared_constants import package_documents

from pathlib import Path
import argparse, collections, importlib.util, json, os, re, struct, subprocess, tempfile
from PIL import Image

ROOT = _PROJECT_ROOT
PRODUCT = ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-fuchsia-route-region-import-v1'
PACKAGE = 'maps/regions/fuchsia-route'
CORE = ['Route12', 'Route13', 'Route14', 'Route15', 'Route15_WestEntrance_1F', 'Route15_WestEntrance_2F', 'FuchsiaCity', 'FuchsiaCity_PokemonCenter_1F', 'FuchsiaCity_Mart']
OPTIONAL = ['Route12_FishingHouse']
TILESETS = {'gTileset_General':'primary/general', 'gTileset_LavenderTown':'secondary/lavender_town', 'gTileset_FuchsiaCity':'secondary/fuchsia_city', 'gTileset_Building':'primary/building', 'gTileset_GenericBuilding2':'secondary/generic_building_2', 'gTileset_PokemonCenter':'secondary/pokemon_center', 'gTileset_GenericBuilding1':'secondary/generic_building_1', 'gTileset_Mart':'secondary/mart'}

def canonical(doc): return (json.dumps(doc, sort_keys=True, separators=(',', ':'))+'\n').encode()
def directory(name): return re.sub(r'route(\d+)', r'route-\1', re.sub(r'([a-z])([A-Z])',r'\1-\2',name).replace('_','-').lower())
def require(value, message):
    if not value: raise ValueError(message)

def safe(path):
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'Symlink path rejected: '+str(path))
    return path

def git(repo, *args):
    env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull,GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0')
    return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(repo),*args],env=env)

class LazyBlobs(dict):
    def __init__(self, owner): super().__init__(); self.owner=owner
    def __missing__(self, path): return self.owner.read(path)

class Source:
    def __init__(self, reference=None, *, raw_root=None):
        require((reference is None) != (raw_root is None), 'Select exactly one Git checkout or raw root')
        self.reference=reference
        self.raw_root=safe(Path(raw_root).absolute()) if raw_root is not None else None
        self.tree={}
        if self.raw_root is not None:
            require(self.raw_root.is_dir(), 'Missing pinned raw root: '+str(self.raw_root))
            for path in sorted(self.raw_root.rglob('*')):
                safe(path)
                if path.is_file(): self.tree[path.relative_to(self.raw_root).as_posix()]=None
        else:
            require(git(reference, 'rev-parse', 'HEAD').decode().strip() == COMMIT, 'Source HEAD differs from pinned revision')
            for row in git(reference,'ls-tree','-rz',COMMIT).split(b'\0'):
                if row:
                    fields,path=row.split(b'\t'); self.tree[path.decode()]=tuple(fields.decode().split())
        self.data=LazyBlobs(self)
    def peek(self,path):
        relative=Path(path)
        require(not relative.is_absolute() and '..' not in relative.parts and '\\' not in path,
                'Unsafe raw path: '+path)
        require(path in self.tree, 'Missing pinned source input: '+path)
        if self.raw_root is not None:
            return safe(self.raw_root/path).read_bytes()
        require(self.tree[path][:2]==('100644','blob'), 'Not a regular source blob: '+path)
        return git(self.reference,'cat-file','blob',self.tree[path][2])
    def read(self,path,role='source-dependency'):
        if path not in self.data:
            self.data[path]=self.peek(path)
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

def decode(src, name):
    return decode_geometry(src, name, tilesets=TILESETS)

def scripts(src,maps):
    # Full files are indexed to discover symbols; only selected closure files become
    # dependencies. This deliberately does not equate closure membership to admission.
    blocks={};aliases={}
    paths=[p for p in src.tree if p.endswith('.inc') and (p.startswith(('data/scripts/','data/text/')) or any(p.startswith('data/maps/'+n+'/') for n in CORE+OPTIONAL))]+['data/event_scripts.s']
    for p in sorted(paths):
        b=src.peek(p);current=None;macro=False
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
    roots.update(['EventScript_AfterWhiteOutHeal','EventScript_TryDoNormalTrainerBattle','EventScript_DoTrainerBattleFromApproach','Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo'])
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
    if unresolved:raise ValueError('Unresolved source label targets: '+repr(unresolved))
    return dict(executable=False,roots=sorted(roots),blocks=result,opcodeCounts=dict(sorted(opcodes.items())),specialCounts=dict(sorted(specials.items())),aliases=aliases,unresolvedLabelReferences=unresolved)


def load_helper(name):
    p=PRODUCT/('scripts/imports/maps' if name.startswith(('export-', 'import-map-atlases')) else 'tools/import-regional-service-maps')/name
    spec=importlib.util.spec_from_file_location('_fuchsia_'+p.stem,p);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module


def check_package(stage, output, generated, manifest):
    """Compare fresh output, resolving pruned images through the shared atlas."""
    atlas = None
    for path in sorted(stage.iterdir()):
        destination = safe(output / path.name)
        if path.suffix == '.png':
            with Image.open(path) as image:
                expected = image.convert('RGBA')
            if destination.is_file():
                with Image.open(destination) as image:
                    actual = image.convert('RGBA')
            else:
                if atlas is None:
                    atlas_module = load_helper('import-map-atlases.py')
                    atlas = atlas_module.AtlasReader(generated / 'maps/atlases')
                actual = atlas.read(PACKAGE + '/' + path.name)
            require(actual.size == expected.size and actual.tobytes() == expected.tobytes(),
                    'Map RGBA differs: ' + path.name)
        elif path.name == 'manifest.json':
            require(destination.is_file(), 'Missing package manifest: ' + str(destination))
            # Atlas pruning removes just the images that it packed. Check each
            # image above and derive the exact inventory of remaining files.
            current = dict(manifest, outputs=[row for row in manifest['outputs']
                if not row['path'].endswith('.png') or (output / row['path']).is_file()])
            require(json.loads(destination.read_bytes()) == current,
                    'Regeneration differs: ' + str(destination))
        else:
            require(destination.is_file() and destination.read_bytes() == path.read_bytes(),
                    'Regeneration differs: ' + str(destination))


def convert(root, raw=None, reference=None, check=False):
    generated = safe(root / 'content/generated')
    out = safe(generated / PACKAGE)
    raw = safe(raw or root / 'content/raw/pokefirered/037335f4')
    require(not (raw.is_relative_to(generated) or generated.is_relative_to(raw)),
            'Raw source overlaps generated output')
    if reference is not None:
        require(not (reference.is_relative_to(root) or root.is_relative_to(reference)),
                'Reference and output must be disjoint')
    src = Source(reference, raw_root=raw if reference is None else None)
    worlds = [decode(src, name) for name in CORE + OPTIONAL]
    # Load every local script/text explicitly, even files with no selected label.
    # The compact renderer must retain the complete story dispatch paths.
    for name in CORE + OPTIONAL:
        for filename in ('scripts.inc', 'text.inc'):
            src.read(f'data/maps/{name}/{filename}', 'map-story-input')
    doc = scripts(src, worlds)
    links = topology(src, worlds)
    for name in ['flags', 'vars', 'event_objects', 'trainers', 'opponents', 'items',
                 'species', 'moves', 'songs', 'map_scripts', 'field_effects', 'battle',
                 'pokemon', 'global', 'menu', 'game_stat', 'battle_ai', 'event_bg',
                 'event_object_movement', 'sound']:
        src.read('include/constants/' + name + '.h', 'rule-reference')
    src.read('tools/mapjson/mapjson.cpp', 'local-object-id-rule')
    shared = load_helper('_regional_service_shared.py')
    shared.PROFILE = PROFILE
    shared.TILESETS = TILESETS
    with tempfile.TemporaryDirectory(prefix='firered-fuchsia-route-') as temp:
        stage = Path(temp)
        catalog = []
        for world in worlds:
            name = world['sourceName']
            if name not in CORE[1:]:
                continue
            relative = directory(name)
            shared.import_map(src, name, world['stableId'], stage / relative)
            catalog.append(dict(sourceName=name, stableId=world['stableId'], directory=relative,
                                sourceMapSymbol=world['events']['id'], path=relative + '/runtime-map.json'))
        npc_worlds = [dict(events=dict(object_events=[actor for actor in world['events']['object_events']
            if actor['graphics_id'] != 'OBJ_EVENT_GFX_VAR_0'])) for world in worlds if world['sourceName'] in CORE]
        npc_worlds.append(dict(events=dict(object_events=[dict(graphics_id=name)
            for name in ['OBJ_EVENT_GFX_KABUTO', 'OBJ_EVENT_GFX_OMANYTE']])))
        npcs = shared.npc_inventory(src, npc_worlds, stage / 'npc')
        npcs['dynamicGraphics'] = [dict(map='MAP_FUCHSIA_CITY', localId=9,
            sourceGraphics='OBJ_EVENT_GFX_VAR_0', variable='VAR_OBJ_GFX_ID_0',
            flag='FLAG_GOT_DOME_FOSSIL', whenSet='OBJ_EVENT_GFX_OMANYTE', whenUnset='OBJ_EVENT_GFX_KABUTO')]
        constants = shared.constants_inventory(src, worlds, doc)
        for index, heal in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
            constants['values'][heal['id']] = dict(value=index,
                sourceRef=src.ref('src/data/heal_locations.json', heal['id']))
        reused = dict(sourceName='Route12', stableId='map.route-12', sourceMapSymbol='MAP_ROUTE12',
            sourceRef=src.ref('data/maps/Route12/map.json', 'MAP_ROUTE12'), regenerate=False)
        main_catalog = dict(schemaVersion=1, maps=catalog, reusedMaps=[reused], tilesets=TILESETS, **links)
        objects = [dict(map=world['stableId'], sourceMap=world['events']['id'], localId=index + 1,
            sourceObjectIndex=index, declaration=actor, sourceRef=world['sourceRef'])
            for world in worlds if world['sourceName'] in CORE
            for index, actor in enumerate(world['events']['object_events'])]
        admitted = {world['events']['id'] for world in worlds if world['sourceName'] in CORE}
        availability = dict(schemaVersion=1, completeGame=False, enabledServices=[],
            loadedMapSymbols=[row['sourceMapSymbol'] for row in catalog], reusedMapSymbols=['MAP_ROUTE12'],
            deferredDestinations=sorted({warp['dest_map'] for world in worlds if world['sourceName'] in CORE
                                        for warp in world['events']['warp_events']} - admitted),
            deferredConnections=['MAP_ROUTE18', 'MAP_ROUTE19'],
            notes=['Map import preserves full original cells, events, clones, water and grass. Admission belongs to the application composition.',
                   'The fishing house and other Fuchsia interiors remain separate map-package boundaries.'])
        documents = package_documents(generated, constants, {
            'fuchsia-route-region-catalog.json': main_catalog,
            'runtime-catalog.json': dict(schemaVersion=3, maps=catalog),
            'npc-source.json': npcs, 'objects.json': dict(objects=objects), 'availability.json': availability}, check=check)
        for filename, value in documents.items():
            (stage / filename).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs = [dict(path=path.name) for path in sorted(stage.iterdir()) if path.is_file()]
        manifest = dict(schemaVersion=1, outputs=outputs)
        (stage / 'manifest.json').write_bytes(canonical(manifest))
        if check:
            check_package(stage, out, generated, manifest)
        else:
            # An explicit Git source may refresh the retained cache. Offline raw
            # mode never writes its inputs or requires a reference repository.
            if reference is not None:
                for relative, data in sorted(src.data.items()):
                    target = safe(raw / relative)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(data)
            out.mkdir(parents=True, exist_ok=True)
            for path in sorted(stage.iterdir()):
                safe(out / path.name).write_bytes(path.read_bytes())
    return dict(checked=check, maps=len(catalog), reusedMaps=1, outputCount=len(outputs))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', '--project-root', dest='root', type=Path, default=ROOT)
    source = parser.add_mutually_exclusive_group()
    source.add_argument('--reference-root', type=Path, help='Pinned source Git checkout')
    source.add_argument('--raw', '--raw-root', dest='raw', type=Path,
                        help='Offline pinned raw cache; defaults to content/raw/pokefirered/037335f4')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(convert(args.root.absolute(), args.raw.absolute() if args.raw else None,
        args.reference_root.absolute() if args.reference_root else None, args.check), sort_keys=True))


if __name__ == '__main__':
    main()
