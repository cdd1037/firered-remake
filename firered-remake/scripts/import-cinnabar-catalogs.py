#!/usr/bin/env python3
"""Exact six-species projection and reviewed 24-team trainer conversion, no admission."""
from pathlib import Path
import argparse, hashlib, importlib.util, json, re, subprocess, sys, tempfile
sys.dont_write_bytecode=True
PROFILE='firered-cinnabar-catalogs-v1'
REGION_IMPORTER_SHA='13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
PROJECTION_SHA='31d70e22805ce5678e046d799614b4976989a30b42404b79bfa32318f7426ed8'
MANIFEST_SHA='da358852c02d2883c24e2aa17163120f28655712ea33c5cee6968e9631f7f007'
LOCK_SHA='c3c583fec333419d459fef92b1b77ab9d8028f3ac450371e9a6202838cd92171'
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(d):return (json.dumps(d,sort_keys=True,indent=2)+'\n').encode()
def need(v,m):
    if not v:raise ValueError(m)
def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,required=True);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    p=Path(__file__).with_name('import-cinnabar-source-preparation.py');need(sha(p.read_bytes())==REGION_IMPORTER_SHA,'Region importer differs')
    spec=importlib.util.spec_from_file_location('_cinnabar_region',p);region=importlib.util.module_from_spec(spec);spec.loader.exec_module(region)
    projection=p.with_name('_cinnabar-trainer-projection.py');need(sha(projection.read_bytes())==PROJECTION_SHA,'Trainer projection differs')
    src=region.Source(a.reference_root);src_profile=region.PROFILE;region.PROFILE=PROFILE
    source_root=a.project_root/'content/generated/cinnabar-source-preparation'
    manifest_bytes=(source_root/'manifest.json').read_bytes();need(sha(manifest_bytes)==region.MANIFEST_SHA256,'Region manifest differs')
    manifest=json.loads(manifest_bytes);entry=next(e for e in manifest['outputs'] if e['path']=='declarations.json');declaration=(source_root/'declarations.json').read_bytes();need(sha(declaration)==entry['sha256'],'Region declarations differ')
    paths=['charmap.txt','include/battle.h','include/constants/battle.h','include/constants/battle_ai.h','include/constants/battle_setup.h','include/constants/trainers.h','include/constants/opponents.h','include/constants/species.h','include/constants/moves.h','include/constants/items.h','src/data/trainers.h','src/data/trainer_parties.h','src/battle_main.c','src/data/pokemon/species_info.h','src/data/pokemon/evolution.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/tmhm_learnsets.h','src/data/text/species_names.h']
    declarations=json.loads(declaration)
    paths+=sorted({b['sourceRef']['path'] for b in declarations['battleOccurrences']})
    for path in paths:src.read(path)
    proofbytes=(a.dependency_root/'content/generated/canonical-pokemon/catalog.json').read_bytes();need(sha(proofbytes)==region.CANONICAL_SHA,'Canonical386 catalog differs');proof=json.loads(proofbytes)
    selected=[next(s for s in proof['species'] if s['sourceSpeciesId']==i) for i in [98,99,38,59,78,91]]
    def one(path,pattern):
        ms=list(re.finditer(pattern,src.text(path),re.M|re.S));need(len(ms)==1,'Ambiguous source match '+pattern);return ms[0]
    def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=src.text(path).count('\n',0,m.start())+1,endLine=src.text(path).count('\n',0,m.end())+1,sha256=src.entries[path]['sha256'])
    names=[];encoded=[];machines=[];held=[];evolutions=[]
    for s in selected:
        for r in [s['source'],s['learnsetSource']]+[x['source'] for x in s['levelUpMoves']]:need(sha(src.read(r['path']))==r['sha256'],'Canonical/source mismatch')
        symbol=s['id']['value'][5:];np='src/data/text/species_names.h';m=one(np,r'^    \['+symbol+r'\]\s*= _\("([^"\n]+)"\),$');name=m[1];r=ref(np,symbol,m)
        names.append(dict(id=s['id'],name=name,source=r))
        codes=[int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in name+'$']
        encoded.append(dict(symbol=symbol,name=name,bytes=codes,source=r,encodingSource=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'))))
        tp='src/data/pokemon/tmhm_learnsets.h';tm=one(tp,r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),');machines.append(dict(species=s['id'],machineSymbols=re.findall(r'TMHM\((\w+)\)',tm[1]),source=ref(tp,symbol,tm)))
        meta=next(x for x in proof['metadata'] if x['id']==s['id']);held.append(dict(species=s['id'],common=meta['originalFields']['itemCommon'],rare=meta['originalFields']['itemRare'],source=s['source']));evolutions.append(dict(species=s['id'],sourceEvolutions=meta['sourceEvolutions']))
    pokemon=dict(schemaVersion=1,profile='firered-cinnabar-pokemon-v1',sourceCommit=region.COMMIT,species=selected[:2],opponentSpecies=selected[2:],speciesNames=names,encodedNames=encoded,machines=machines,heldItems=held,sourceEvolutions=evolutions,requiredMoves=sorted({json.dumps(x['move'],sort_keys=True) for s in selected for x in s['levelUpMoves']}),canonicalCatalogSha256=region.CANONICAL_SHA,limitations=['Krabby/Kingler are candidate ownership additions; the four other species are opponents only.','Source metadata does not grant campaign admission, field progression, battle execution or presentation.'])
    pokemon['requiredMoves']=[json.loads(x) for x in pokemon['requiredMoves']]
    with tempfile.TemporaryDirectory(prefix='cinnabar-catalog-stage-') as temporary:
        tmp=Path(temporary);sp=tmp/'source';dest=sp/'content/generated/cinnabar-source-preparation/declarations.json';dest.parent.mkdir(parents=True);dest.write_bytes(declaration)
        for path,b in src.data.items():
            target=sp/'content/raw/cinnabar-source-preparation'/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
        converted=tmp/'converted';subprocess.run([sys.executable,str(projection),str(tmp/'base'),str(sp),str(converted)],check=True,stdout=subprocess.DEVNULL)
        trainer=(converted/'trainers.json').read_bytes()
    lock=src.lock();lock.update(gameEdition='FIRERED',romRevision=0);lockdata=canonical(lock)
    outputs={'trainers.json':trainer,'pokemon.json':canonical(pokemon)}
    artifact=dict(schemaVersion=1,profile=PROFILE,sourceCommit=region.COMMIT,sourceTree=region.TREE,fullSourceHash=sha(''.join(p+'\0'+e['sha256']+'\n' for p,e in sorted(src.entries.items())).encode()),lockSha256=sha(lockdata),regionManifestSha256=region.MANIFEST_SHA256,trainerProjectionSha256=PROJECTION_SHA,outputs=[dict(path=p,sha256=sha(b),byteLength=len(b)) for p,b in sorted(outputs.items())])
    manifestdata=canonical(artifact)
    if MANIFEST_SHA:need(sha(manifestdata)==MANIFEST_SHA,'Catalog manifest differs')
    if LOCK_SHA:need(sha(lockdata)==LOCK_SHA,'Catalog lock differs')
    writes={'scripts/cinnabar-catalogs.lock.json':lockdata,'content/generated/cinnabar-catalogs/manifest.json':manifestdata}
    writes.update({'content/generated/cinnabar-catalogs/'+p:b for p,b in outputs.items()});writes.update({'content/raw/cinnabar-catalogs/'+p:b for p,b in src.data.items()})
    for path,b in writes.items():
        target=a.project_root/path;need(not any(p.is_symlink() for p in (target,*target.parents)),'Output symlink denied')
        if a.check:need(target.read_bytes()==b,'Regeneration differs: '+path)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
    print(json.dumps(dict(manifestSha256=sha(manifestdata),manifestBytes=len(manifestdata),lockSha256=sha(lockdata),lockBytes=len(lockdata),inputCount=len(src.entries),pokemonSha256=sha(outputs['pokemon.json']),trainersSha256=sha(trainer),outputs=2)))
if __name__=='__main__':main()
