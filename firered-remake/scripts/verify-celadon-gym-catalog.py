#!/usr/bin/env python3
"""Cold reconstruction, source/provenance, immutable predecessor and negative-path checks. No GUI claim."""
import argparse, importlib.util, json, pathlib, re, tempfile

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project-root',type=pathlib.Path,required=True);parser.add_argument('--reference-repo',type=pathlib.Path,required=True);parser.add_argument('--evidence',type=pathlib.Path);args=parser.parse_args()
    script=pathlib.Path(__file__).with_name('import-celadon-gym-catalog.py');spec=importlib.util.spec_from_file_location('celadon_gym_verification',script);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
    project=m.safe(args.project_root);repo=m.safe(args.reference_repo);checks=0
    def check(value,message):
        nonlocal checks
        checks+=1
        if not value:raise AssertionError(message)
    def reject(action,message):
        try:action()
        except (ValueError,FileNotFoundError,NotADirectoryError):check(True,message)
        else:check(False,message)
    recipe=m.configure(project);before={d['path']:(project/d['path']).read_bytes() for d in recipe['dependencies']}
    with tempfile.TemporaryDirectory(prefix='celadon-gym-catalog-') as temporary:
        temp=pathlib.Path(temporary);first=temp/'first';second=temp/'second'
        a=m.materialize(project,first,repo);b=m.materialize(project,second,repo);check(a==b,'Cold source manifests differ')
        files=[p.relative_to(first).as_posix() for p in first.rglob('*') if p.is_file()]
        for path in files:check((first/path).read_bytes()==(second/path).read_bytes(),'Cold file differs '+path)
        check(len(files)>140,'Cold generation inventory unexpectedly small')
        check(m.materialize(project,first,None,True)==a,'Offline checked rebuild differs')
        for stem in ('celadon-gym-pokemon','celadon-gym-items'):
            lock=m.read_json(m.ROOT/'scripts'/(stem+'.lock.json'))
            for row in lock['files']:
                raw=(first/'content/raw/pokefirered/037335f4'/row['path']).read_bytes()
                check(raw==m.git(repo,'show',m.COMMIT+':'+row['path']),'Source differs from pinned Git')
        p=first/'content/generated/celadon-gym-pokemon';catalog=m.read_json(p/'catalog.json');full=m.read_json(p/'source-species.json');supplement=m.read_json(p/'supplement.json')
        check(len(catalog['species'])==4 and len(catalog['moves'])==29,'Catalog scope differs')
        lock=m.read_json(m.ROOT/'scripts/celadon-gym-pokemon.lock.json')
        sources={row['path']:(first/'content/raw/pokefirered/037335f4'/row['path']).read_bytes() for row in lock['files']}
        trainers=m.trainer_parties(lock,sources)
        check(len(trainers)==8 and sum(len(t['party']) for t in trainers)==21 and len({move for t in trainers for mon in t['party'] for move in mon['moves']})==17,'Trainer closure differs')
        prior_moves={row['value'] for d in recipe['dependencies'] if d['path'].endswith('/catalog.json') for row in m.read_json(project/d['path']).get('moves',[])}
        check({x['value'] for x in catalog['moves']}-prior_moves=={'pret:MOVE_BARRAGE','pret:MOVE_INGRAIN','pret:MOVE_MEGA_DRAIN','pret:MOVE_TICKLE','pret:MOVE_UPROAR'},'Five metadata additions differ')
        rawtext=(first/'content/raw/pokefirered/037335f4/src/data/pokemon/species_info.h').read_text()
        for row in full['species']:
            symbol=row['species']['value'][5:];source=re.search(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',rawtext,re.M|re.S)
            fields={v[1]:v[2] for v in re.finditer(r'^\s*\.(\w+) = (.*),$',source[1],re.M)}
            check(row['fields']==fields and row['declaration']==source[0] and len(fields)==26,'Full original declaration omitted a field')
        check(len(supplement['weights'])==98 and len(supplement['icons'])==4 and len(supplement['footprints'])==4,'Source supplement scope')
        check(all(not c['audioBytesImported'] and not c['playbackVerified'] for c in supplement['cries']),'Unverified cry playback claim')
        check(not any(x.endswith(('.wav','.ogg','.qoa')) for x in files),'Imported recorded audio')
        prior=m.read_json(project/'content/generated/celadon-approach-items/celadon-approach-machines.json');machines=m.read_json(first/'content/generated/celadon-gym-items/celadon-gym-machines.json')['machines']
        check(len(machines)==15 and all(len(x['compatibility'])==98 for x in machines),'Machine scope differs')
        for old,new in zip(prior['machines'],machines[:14]):
            expected=dict(new,compatibility=new['compatibility'][:94]);check(old==expected,'Inherited machine metadata changed')
            for cell,new_cell in zip(old['compatibility'],new['compatibility'][:94]):check(cell==new_cell,'Inherited machine row changed')
        check(sum(x['canLearn'] for x in machines[-1]['compatibility'])==25,'TM19 source species compatibility count')
        tm_source=(first/'content/raw/pokefirered/037335f4/src/data/pokemon/tmhm_learnsets.h').read_text()
        for index,machine in enumerate(machines):
            token=machine['machine']['item']['value'].removeprefix('pret:ITEM_')+'_'+machine['move']['value'][10:]
            for cell in (machine['compatibility'] if index==14 else machine['compatibility'][94:]):
                symbol=cell['species']['value'][5:];block=re.search(r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),',tm_source,re.M|re.S)
                actual=token in re.findall(r'TMHM\((\w+)\)',block[1])
                check(cell['canLearn']==actual,'New compatibility differs from original table')

        for d in recipe['dependencies']:check((project/d['path']).read_bytes()==before[d['path']],'Predecessor/helper mutated')
        # Negative tests use local copies only; no authenticated predecessor mutation.
        asset=first/'content/generated/celadon-gym-pokemon/catalog.json';original=asset.read_bytes();asset.write_bytes(original+b'\n')
        reject(lambda:m.materialize(project,first,None,True),'Generated tamper was accepted');asset.write_bytes(original)
        path=first/'content/raw/pokefirered/037335f4/src/data/pokemon/species_info.h';saved=temp/'saved-source';path.rename(saved);path.symlink_to(saved)
        reject(lambda:m.materialize(project,first,None),'Source symlink was accepted');path.unlink();saved.rename(path)
        reject(lambda:m.safe(temp,'../escape'),'Traversal was accepted')
        reject(lambda:m.safe(temp,'/absolute'),'Absolute output was accepted')
        reject(lambda:m.safe(temp,'a\\b'),'Backslash output was accepted')
        reject(lambda:m.materialize(project,repo,repo),'Reference overlap was accepted')
        check(m.materialize(project,first,None,True)==a,'Negative tests damaged pristine source material')
    check(checks>1750,'Verification suite unexpectedly small')
    report=dict(checks=checks,pinnedCommit=m.COMMIT,coldRebuilds=2,legacyMachineCells=14*94,newSpecies=4,metadataMoves=29,newMetadataMoves=5,tm19CompatibleSpecies=25,appPlayable=False,guiVerified=False,audioVerified=False)
    if args.evidence:args.evidence.parent.mkdir(parents=True,exist_ok=True);args.evidence.write_bytes(m.canonical(report))
    print(json.dumps(report,sort_keys=True))
if __name__=='__main__':main()
