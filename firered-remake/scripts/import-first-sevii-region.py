#!/usr/bin/env python3
"""Regenerate pinned first-Sevii source, map pixels and compact data; execute no upstream code."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse, ast, contextlib,  importlib.util, io, json, re, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PROFILE = 'firered-first-sevii-region-v1'
PACKAGE = 'first-sevii-region'
def canonical(v): return (json.dumps(v, sort_keys=True, separators=(',', ':'))+'\n').encode()
def need(v,m):
    if not v: raise ValueError(m)
def module(path, name):
    spec=importlib.util.spec_from_file_location(name,path); value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
def safe(path): need(not any(p.is_symlink() for p in [path,*path.parents]), 'Symlink in output: '+str(path))
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--dependency-root',type=Path,default=ROOT)
    ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--check',action='store_true');a=ap.parse_args()
    target=a.project_root.absolute();dep=a.dependency_root.absolute();ref=a.reference_root.absolute()
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    safe(target)
    prep=module(ROOT / 'scripts/first-sevii-preparation/prepare_first_sevii.py', 'first_sevii_preparer')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'first_sevii_reader')
    reader.PRODUCT=dep;reader.TILESETS=prep.TILESETS;reader.PROFILE=PROFILE
    with tempfile.TemporaryDirectory(prefix='first-sevii-import-') as temp:
        tmp=Path(temp);prepared=tmp/'prepared';prepared.mkdir()
        prep.OUT=prepared
        argv=sys.argv
        try:
            sys.argv=[str(prep.__file__),'--reference-root',str(ref),'--project-root',str(dep)]
            with contextlib.redirect_stdout(io.StringIO()):prep.main()
        finally:sys.argv=argv
        original={p.name:json.loads(p.read_bytes()) for p in prepared.glob('*.json')}
        src=reader.Source(ref)
        for entry in original['source-lock.json']['files']:
            for role in entry['roles']:src.read(entry['path'],role)
        names=prep.CORE+prep.SUPPORT
        worlds=[reader.decode(src,n) for n in names]
        for n in prep.ANCHOR+['VermilionCity']:src.read(f'data/maps/{n}/map.json','reused-anchor-events')
        for n in ['cable_club','fame_checker','field_effects','field_weather','game_stat','global','help_system','pokemon','sound','union_room']:
            src.read('include/constants/'+n+'.h','script-operand-constant')
        src.read('include/gba/defines.h','script-operand-constant')
        scripts=original['scripts.json'];scripts['profile']=PROFILE
        scripts['sourcePaths']=sorted({b['sourceRef']['path'] for b in scripts['blocks']})
        shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=prep.TILESETS
        converter=reader.load_helper('export-runtime-maps.py')
        stage=tmp/'generated';stage.mkdir()
        for name in names:shared.import_map(src,name,'map.'+reader.directory(name),stage/reader.directory(name))
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
        contracts['excludedEncounterGates']=original['catalog-delta.json']['excludedEncounterGates']
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
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,profile=PROFILE,sourceCommit=reader.COMMIT,runtimeIntegrated=False,maps=catalog,tilesets=prep.TILESETS,mapSymbols=map_symbols,**contracts['mapTopology']),
              'scripts.json':scripts,'npc-source.json':npcs,'contracts.json':contracts,
              'geometry.json':original['geometry.json'],
              'catalog-delta.json':original['catalog-delta.json']}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=reader.COMMIT,inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(w['width']*w['height'] for w in worlds),warpCount=sum(len(w['events']['warp_events']) for w in worlds),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,trainerCount=len(original['declarations.json']['trainers']),outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            safe(p)
            if a.check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))
if __name__=='__main__':main()
