#!/usr/bin/env python3
"""Verify candidate source/resource identity and fail-closed importer boundaries."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def sha(data): return hashlib.sha256(data).hexdigest()


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root',type=Path,required=True)
    parser.add_argument('--dependency-root',type=Path,required=True)
    parser.add_argument('--reference-root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    root=args.project_root.resolve();package=root/'content/generated/trainer-fan-club'
    checks=[]
    def need(value,name):
        if not value:raise AssertionError(name)
        checks.append(name)
    lock=json.loads((root/'scripts/trainer-fan-club.lock.json').read_bytes())
    manifest=json.loads((package/'manifest.json').read_bytes())
    need(len(lock['files'])==90 and len(manifest['outputs'])==15,'nonempty exact input and output inventories')
    for row in lock['files']:
        raw=(root/'content/raw/trainer-fan-club'/row['path']).read_bytes()
        need(sha(raw)==row['sha256'] and len(raw)==row['byteLength'] and
             hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==row['gitBlobSha1'],'source:'+row['path'])
    for row in manifest['outputs']:
        raw=(package/row['path']).read_bytes()
        need(sha(raw)==row['sha256'] and len(raw)==row['byteLength'],'resource:'+row['path'])
    need(sha((root/'scripts/trainer-fan-club.lock.json').read_bytes())==manifest['lockSha256'],'manifest source lock binding')
    full=sha(''.join(e['path']+'\0'+e['sha256']+'\n' for e in lock['files']).encode())
    need(full==manifest['fullSourceHash'],'full source identity')
    constants=json.loads((package/'constants.json').read_bytes())['values']
    need(constants['VAR_FANCLUB_FAN_COUNTER']['value']==0x4038 and constants['VAR_FANCLUB_LOSE_FAN_TIMER']['value']==0x4039,
         'native helper-only source variables retained')
    expression=constants['(NUM_TRAINER_FAN_CLUB_MEMBERS - 1)']
    need(expression['value']==7 and expression['derivation']['literal']==1 and
         expression['derivation']['symbol']=='NUM_TRAINER_FAN_CLUB_MEMBERS','exact source expression retained')
    source=(root/'content/raw/trainer-fan-club/src/trainer_fan_club.c').read_bytes()
    need(sha(source)=='a0676e06e8b41041e8e7a093e4e9376e75baf00ae90bed44878e5aad6cb6524f','source pure-rule identity')
    def run(target,dep=None):
        return subprocess.run([sys.executable,str(root/'scripts/import-trainer-fan-club.py'),
            '--project-root',str(target),'--dependency-root',str(dep or args.dependency_root),
            '--current-project-root',str(args.dependency_root),'--reference-root',str(args.reference_root),'--check'],
            capture_output=True,text=True)
    result=run(root)
    need(result.returncode==0,'deterministic regeneration --check')
    negatives=[]
    with tempfile.TemporaryDirectory(prefix='fan-club-negative-',dir=root/'evidence') as temp:
        temp=Path(temp)
        empty=temp/'empty';empty.mkdir()
        result=run(empty)
        need(result.returncode!=0 and 'Regeneration differs:' in result.stderr and not list(empty.iterdir()),'missing check output rejected without mutation')
        negatives.append(dict(case='missing-output',returncode=result.returncode,error=result.stderr.splitlines()[-1]))
        damaged=temp/'damaged'
        for path in ['scripts/trainer-fan-club.lock.json','scripts/trainer-fan-club.contract.json']:
            target=damaged/path;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/path,target)
        shutil.copytree(root/'content/raw/trainer-fan-club',damaged/'content/raw/trainer-fan-club')
        shutil.copytree(package,damaged/'content/generated/trainer-fan-club')
        victim=damaged/'content/generated/trainer-fan-club/saffron-city-pokemon-trainer-fan-club/runtime-map.json'
        damaged_bytes=victim.read_bytes()+b' ';victim.write_bytes(damaged_bytes)
        result=run(damaged)
        need(result.returncode!=0 and str(victim) in result.stderr and victim.read_bytes()==damaged_bytes,'tampered output rejected without rewriting')
        negatives.append(dict(case='tampered-output',returncode=result.returncode,error=result.stderr.splitlines()[-1]))
        dep=temp/'dependency';(dep/'scripts').mkdir(parents=True)
        (dep/'scripts/import-cinnabar-source-preparation.py').write_bytes((args.dependency_root/'scripts/import-cinnabar-source-preparation.py').read_bytes()+b'\n')
        result=run(empty,dep)
        need(result.returncode!=0 and 'Reviewed dependency differs:' in result.stderr and not list(empty.iterdir()),'changed executable dependency rejected before execution')
        negatives.append(dict(case='changed-helper',returncode=result.returncode,error=result.stderr.splitlines()[-1]))
        alias=temp/'alias';alias.symlink_to(args.dependency_root,target_is_directory=True)
        result=run(empty,alias)
        need(result.returncode!=0 and 'Symlink rejected:' in result.stderr and not list(empty.iterdir()),'symlink dependency rejected before execution')
        negatives.append(dict(case='symlink-helper',returncode=result.returncode,error=result.stderr.splitlines()[-1]))
    report=dict(checkCount=len(checks),failures=0,checks=checks,negativeBoundaries=negatives,
        inputCount=len(lock['files']),outputCount=len(manifest['outputs']),
        manifestSha256=sha((package/'manifest.json').read_bytes()),fullSourceHash=full)
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(checkCount=len(checks),failures=0,negativeBoundaryCount=len(negatives))))


if __name__=='__main__':main()
