#!/usr/bin/env python3
"""Exact six-species projection and reviewed 24-team trainer conversion, no admission."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from pathlib import Path
import argparse,  importlib.util, json, re, subprocess, sys, tempfile
sys.dont_write_bytecode=True
PROFILE='firered-cinnabar-catalogs-v1'
def canonical(d):return (json.dumps(d,sort_keys=True,indent=2)+'\n').encode()
def need(v,m):
    if not v:raise ValueError(m)
def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,required=True);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    p=(_PROJECT_ROOT / 'scripts/imports/source/import-cinnabar-source-preparation.py')
    spec=importlib.util.spec_from_file_location('_cinnabar_region',p);region=importlib.util.module_from_spec(spec);spec.loader.exec_module(region)
    projection=p.with_name('_cinnabar-trainer-projection.py')
    src=region.Source(a.reference_root);region.PROFILE=PROFILE
    region.PRODUCT=a.dependency_root
    worlds=[dict(events=src.json('data/maps/'+name+'/map.json')) for name in region.CORE]
    declarations=region.declarations(src,worlds,region.scripts(src,worlds))
    declaration=canonical(declarations)
    paths=['charmap.txt','include/battle.h','include/constants/battle.h','include/constants/battle_ai.h','include/constants/battle_setup.h','include/constants/trainers.h','include/constants/opponents.h','include/constants/species.h','include/constants/moves.h','include/constants/items.h','src/data/trainers.h','src/data/trainer_parties.h','src/battle_main.c','src/data/pokemon/species_info.h','src/data/pokemon/evolution.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/tmhm_learnsets.h','src/data/text/species_names.h']
    paths+=sorted({b['sourceRef']['path'] for b in declarations['battleOccurrences']})
    for path in paths:src.read(path)
    proofbytes=(a.dependency_root/'content/generated/data/pokemon/canonical/catalog.json').read_bytes();proof=json.loads(proofbytes)
    selected=[next(s for s in proof['species'] if s['sourceSpeciesId']==i) for i in [98,99,38,59,78,91]]
    def one(path,pattern):
        ms=list(re.finditer(pattern,src.text(path),re.M|re.S));need(len(ms)==1,'Ambiguous source match '+pattern);return ms[0]
    def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=src.text(path).count('\n',0,m.start())+1,endLine=src.text(path).count('\n',0,m.end())+1)
    names=[];encoded=[];machines=[];held=[];evolutions=[]
    for s in selected:
        symbol=s['id']['value'][5:];np='src/data/text/species_names.h';m=one(np,r'^    \['+symbol+r'\]\s*= _\("([^"\n]+)"\),$');name=m[1];r=ref(np,symbol,m)
        names.append(dict(id=s['id'],name=name,source=r))
        codes=[int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in name+'$']
        encoded.append(dict(symbol=symbol,name=name,bytes=codes,source=r,encodingSource=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'))))
        tp='src/data/pokemon/tmhm_learnsets.h';tm=one(tp,r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),');machines.append(dict(species=s['id'],machineSymbols=re.findall(r'TMHM\((\w+)\)',tm[1])))
        meta=next(x for x in proof['metadata'] if x['id']==s['id']);held.append(dict(species=s['id'],common=meta['itemCommon'],rare=meta['itemRare']));evolutions.append(dict(species=s['id'],sourceEvolutions=meta['sourceEvolutions']))
    pokemon=dict(schemaVersion=1,species=[dict(id=s['id'],evolutions=s['evolutions']) for s in selected[:2]],opponentSpecies=[dict(id=s['id'],evolutions=s['evolutions']) for s in selected[2:]],speciesNames=names,encodedNames=encoded,machines=machines,heldItems=held,sourceEvolutions=evolutions,requiredMoves=sorted({json.dumps(x['move'],sort_keys=True) for s in selected for x in s['levelUpMoves']}))
    pokemon['requiredMoves']=[json.loads(x) for x in pokemon['requiredMoves']]
    with tempfile.TemporaryDirectory(prefix='cinnabar-catalog-stage-') as temporary:
        tmp=Path(temporary);sp=tmp/'source'
        for path,b in src.data.items():
            target=sp/'content/raw/pokefirered/037335f4'/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
        converted=tmp/'converted';subprocess.run([sys.executable,str(projection),str(tmp/'base'),str(sp),str(converted)],input=declaration,check=True,stdout=subprocess.DEVNULL)
        trainer=(converted/'trainers.json').read_bytes()
    outputs={'trainers.json':trainer_json(a.project_root/'content/generated', json.loads(trainer), check=a.check),'pokemon.json':canonical(pokemon)}
    artifact=dict(schemaVersion=1,outputs=[dict(path=p) for p,b in sorted(outputs.items())])
    manifestdata=canonical(artifact)
    writes={'content/generated/data/regions/cinnabar/manifest.json':manifestdata}
    writes.update({'content/generated/data/regions/cinnabar/'+p:b for p,b in outputs.items()});writes.update({'content/raw/pokefirered/037335f4/'+p:b for p,b in src.data.items()})
    for path,b in writes.items():
        target=a.project_root/path;need(not any(p.is_symlink() for p in (target,*target.parents)),'Output symlink denied')
        if a.check:need(target.read_bytes()==b,'Regeneration differs: '+path)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
    print(json.dumps(dict(manifestBytes=len(manifestdata),inputCount=len(src.entries),outputs=2)))
if __name__=='__main__':main()
