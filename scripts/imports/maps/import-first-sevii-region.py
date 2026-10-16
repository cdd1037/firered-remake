#!/usr/bin/env python3
"""Regenerate pinned first-Sevii source, map pixels and compact data; execute no upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
from layout_variants import import_layout_variant
import argparse, ast, importlib.util, io, json, re, sys, tempfile
from PIL import Image
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PROFILE = 'firered-first-sevii-region-v1'
PACKAGE = 'maps/regions/first-sevii'
def canonical(v): return (json.dumps(v, sort_keys=True, separators=(',', ':'))+'\n').encode()
def need(v,m):
    if not v: raise ValueError(m)
def module(path, name):
    spec=importlib.util.spec_from_file_location(name,path); value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
def safe(path): need(not any(p.is_symlink() for p in [path,*path.parents]), 'Symlink in output: '+str(path))
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    source=ap.add_mutually_exclusive_group(required=True)
    source.add_argument('--reference-root',type=Path,help='Pinned source Git checkout')
    source.add_argument('--raw-root',type=Path,help='Offline cache of the pinned source files; no Git checkout or network required')
    ap.add_argument('--dependency-root',type=Path,default=ROOT)
    ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--check',action='store_true');a=ap.parse_args()
    target=a.project_root.absolute();dep=a.dependency_root.absolute()
    ref=a.reference_root.absolute() if a.reference_root else None
    raw=a.raw_root.absolute() if a.raw_root else None
    if ref is not None:need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    if raw is not None:
        generated=target/'content/generated'
        need(not(raw.is_relative_to(generated) or generated.is_relative_to(raw)), 'Raw source overlaps generated output')
    safe(target)
    prep=module(ROOT / 'scripts/lib/prepare_first_sevii.py', 'first_sevii_preparer')
    reader=module(dep / 'scripts/imports/source/import-cinnabar-source-preparation.py', 'first_sevii_reader')
    reader.PRODUCT=dep;reader.TILESETS=prep.TILESETS;reader.PROFILE=PROFILE
    with tempfile.TemporaryDirectory(prefix='first-sevii-import-') as temp:
        tmp=Path(temp)
        src=reader.Source(ref,raw_root=raw)
        canonical_pokemon=json.loads((dep/'content/generated/data/pokemon/canonical/catalog.json').read_bytes())
        original,worlds=prep.prepare(src,reader,canonical_pokemon)
        names=prep.CORE+prep.SUPPORT
        for n in prep.ANCHOR+['VermilionCity']:src.read(f'data/maps/{n}/map.json','reused-anchor-events')
        for n in ['cable_club','fame_checker','field_effects','field_weather','game_stat','global','help_system','pokemon','sound','union_room']:
            src.read('include/constants/'+n+'.h','script-operand-constant')
        src.read('include/gba/defines.h','script-operand-constant')
        scripts=original['scripts.json'];scripts['profile']=PROFILE

        shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=prep.TILESETS
        converter=reader.load_helper('export-runtime-maps.py')
        stage=tmp/'generated';stage.mkdir()
        for name in names:shared.import_map(src,name,'map.'+reader.directory(name),stage/reader.directory(name))
        variants=[]
        for base in worlds:
            for symbol in prep.LAYOUT_VARIANTS.get(base['sourceName'], []):
                layout=next(row for row in src.json('data/layouts/layouts.json')['layouts'] if row.get('id')==symbol)
                relative=reader.directory(layout['name'].removesuffix('_Layout'))
                import_layout_variant(src,reader,shared,base,symbol,stage/relative)
                variants.append(dict(layoutSymbol=symbol,sourceName=base['sourceName'],stableId=base['stableId'],
                    sourceMapSymbol=base['events']['id'],directory=relative,path=relative+'/runtime-map.json'))
        npcs=shared.npc_inventory(src,worlds,stage/'npc')
        constants=shared.constants_inventory(src,worlds,scripts)
        # Preserve the source arithmetic used by script operands and generated heal IDs.
        definitions={}
        for sourcepath in sorted(src.data):
            if sourcepath.startswith('include/'):
                text=src.text(sourcepath)
                for m in re.finditer(r'^#define[ \t]+([A-Z][A-Z0-9_]+)[ \t]+([^\n]+)',text,re.M):
                    definitions[m[1]]=(m[2].split('//')[0].strip(),sourcepath,text.count('\n',0,m.start())+1)
        macrotext=src.text('asm/macros/event.inc')
        for m in re.finditer(r'^\s*([A-Z][A-Z0-9_]+)\s*=\s*([0-9]+)\s*$',macrotext,re.M):
            definitions[m[1]]=(m[2],'asm/macros/event.inc',macrotext.count('\n',0,m.start())+1)
        pending=set()
        def resolve(symbol):
            if symbol in constants['values']:return constants['values'][symbol]['value']
            need(symbol not in pending and symbol in definitions,'Unresolved constant '+symbol)
            pending.add(symbol);expression,path,line=definitions[symbol]
            def number(node):
                if isinstance(node,ast.Constant) and type(node.value) is int:return node.value
                if isinstance(node,ast.Name):return resolve(node.id)
                if isinstance(node,ast.BinOp):
                    x,y=number(node.left),number(node.right)
                    if isinstance(node.op,ast.Add):return x+y
                    if isinstance(node.op,ast.Sub):return x-y
                    if isinstance(node.op,ast.LShift) and 0<=y<32:return x<<y
                    if isinstance(node.op,ast.BitOr):return x|y
                raise ValueError('Unsupported source expression '+symbol+': '+expression)
            value=number(ast.parse(expression,mode='eval').body);need(0<=value<=0x7fffffff,'Constant out of range')
            constants['values'][symbol]=dict(value=value,sourceRef=src.ref(path,symbol,line));pending.remove(symbol);return value
        operands=set(re.findall(r'\b[A-Z][A-Z0-9_]+\b','\n'.join(r['text'].split('@')[0] for b in scripts['blocks'] for r in b['lines'] if not r['text'].strip().startswith('.string'))))
        for symbol in sorted(operands & definitions.keys()):resolve(symbol)
        healpath='src/data/heal_locations.json';template=src.text('src/data/heal_locations.constants.json.txt')
        need('HEAL_LOCATION_NONE,' in template and '{{ heal_location.id }},' in template,'Heal enum template differs')
        for i,h in enumerate(src.json(healpath)['heal_locations'],1):
            constants['values'][h['id']]=dict(value=i,sourceRef=src.ref(healpath,h['id']),generatorSource=src.ref('src/data/heal_locations.constants.json.txt','ordered enum'))
        for i,layout in enumerate(src.json('data/layouts/layouts.json')['layouts'],1):
            if layout.get('id') in operands:
                constants['values'][layout['id']]=dict(value=i,sourceRef=src.ref('data/layouts/layouts.json',layout['id']))
        constants['nonNumericOperandTokens']=sorted(operands-constants['values'].keys()-{s for s in operands if s.startswith('MAP_')})
        need(constants['nonNumericOperandTokens']==['STR_VAR_1','STR_VAR_2','STR_VAR_3'],'Unexpected unresolved operand constant')
        # The shared parser handles uppercase constants; preserve mixed-case source metatiles too.
        path='include/constants/metatile_labels.h';text=src.text(path)
        for match in re.finditer(r'^#define\s+(METATILE_\w+)\s+(0x[0-9A-Fa-f]+)\s*$',text,re.M):
            symbol,token=match.groups()
            constants['values'][symbol]=dict(value=int(token,16),sourceRef=src.ref(path,symbol,text.count('\n',0,match.start())+1))
        contracts=original['contracts.json'];contracts['profile']=PROFILE
        contracts['limitations']=['Source evidence and standalone content loading only; application admission is separate.',
            'Original external map edges and postgame/multiplayer branches are retained without admitting them.',
            'Dunsparce grass remains disconnected on the first visit; the alternate tunnel requires National Dex.',
            'No GUI, battle, save/load, ferry animation or physical audio validation is claimed.']
        contracts['layoutVariants']=variants
        contracts['externalBoundaries']={k:[e for e in contracts['mapTopology'][k] if not e['destinationInSlice']] for k in ('warps','connections')}
        contracts['tilePatches']=[dict(label=b['label'],sourceRef=b['sourceRef'],changes=[dict(line=r['line'],arguments=[x.strip() for x in r['text'].strip().split(None,1)[1].split(',')]) for r in b['lines'] if r['text'].strip().startswith('setmetatile ')]) for b in scripts['blocks'] if any(r['text'].strip().startswith('setmetatile ') for r in b['lines'])]
        map_tokens=set(re.findall(r'\bMAP_[A-Z0-9_]+\b',json.dumps(scripts)+json.dumps(contracts)+json.dumps([w['events'] for w in worlds])))
        map_symbols={}
        for p in src.tree:
            if p.startswith('data/maps/') and p.endswith('/map.json'):
                name=p.split('/')[2];symbol='MAP_'+re.sub(r'([a-z])([A-Z])',r'\1_\2',name).upper()
                if symbol in map_tokens:map_symbols[symbol]='map.'+reader.directory(name)
        catalog=[]
        for w in worlds:
            relative=reader.directory(w['sourceName']);p=stage/relative/'runtime-map.json';rich=p.read_bytes()
            compact=converter.encode(converter.normalize_runtime(json.loads(rich)));(stage/relative/'runtime-map.json').write_bytes(compact)
            catalog.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=relative,sourceMapSymbol=w['events']['id'],path=relative+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=names)
        lockdata=canonical(lock)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,maps=catalog,layoutVariants=variants,tilesets=prep.TILESETS,mapSymbols=map_symbols,**contracts['mapTopology']),
              'scripts.json':scripts,'npc-source.json':npcs,'contracts.json':contracts,
              'geometry.json':original['geometry.json']}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=([(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())] if ref is not None else [])+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        atlas=None
        for p,b in writes:
            safe(p)
            if a.check:
                if p.suffix=='.png' and p.is_relative_to(target/'content/generated'):
                    with Image.open(io.BytesIO(b)) as expected:expected=expected.convert('RGBA')
                    if p.is_file():
                        with Image.open(p) as actual:actual=actual.convert('RGBA')
                    else:
                        if atlas is None:
                            atlas_module=module(ROOT/'scripts/imports/maps/import-map-atlases.py','first_sevii_atlas_reader')
                            atlas=atlas_module.AtlasReader(target/'content/generated/maps/atlases')
                        actual=atlas.read(p.relative_to(target/'content/generated').as_posix())
                    need(actual.size==expected.size and actual.tobytes()==expected.tobytes(),'Map RGBA differs: '+str(p))
                elif p.name=='manifest.json' and p.is_file() and p.read_bytes()!=b:
                    # The shared atlas publisher removes packed PNG entries from
                    # package manifests. Each omitted image is checked above.
                    packed=dict(manifest,outputs=[row for row in outputs if not row['path'].endswith('.png')])
                    need(p.read_bytes()==canonical(packed),'Regeneration differs: '+str(p))
                else:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))
if __name__=='__main__':main()
