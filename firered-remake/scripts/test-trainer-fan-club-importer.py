#!/usr/bin/env python3
"""Verify source constants, required resources and deterministic regeneration."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import tempfile


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
    need(len(lock['files'])==90 and len(manifest['outputs'])==14,'nonempty exact input and output inventories')
    for row in lock['files']:
        need((root/'content/raw/pokefirered/037335f4'/row['path']).is_file(),'source:'+row['path'])
    for row in manifest['outputs']:
        need((package/row['path']).is_file(),'resource:'+row['path'])
    constants=json.loads((package.parent/'shared-constants/values.json').read_bytes())
    need(constants['VAR_FANCLUB_FAN_COUNTER']==0x4038 and constants['VAR_FANCLUB_LOSE_FAN_TIMER']==0x4039,
         'native helper-only source variables retained')
    expression=constants['(NUM_TRAINER_FAN_CLUB_MEMBERS - 1)']
    need(expression==7 and expression==constants['NUM_TRAINER_FAN_CLUB_MEMBERS']-1, 'exact source expression value retained')
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
        need(result.returncode!=0 and ('Regeneration differs:' in result.stderr or 'shared-constants/values.json' in result.stderr) and not list(empty.iterdir()),'missing check output rejected without mutation')
        negatives.append(dict(case='missing-output',returncode=result.returncode,error=result.stderr.splitlines()[-1]))
        alias=temp/'alias';alias.symlink_to(args.dependency_root,target_is_directory=True)
        result=run(empty,alias)
        need(result.returncode!=0 and 'Symlink rejected:' in result.stderr and not list(empty.iterdir()),'symlink dependency rejected before execution')
        negatives.append(dict(case='symlink-helper',returncode=result.returncode,error=result.stderr.splitlines()[-1]))
    report=dict(checkCount=len(checks),failures=0,checks=checks,negativeBoundaries=negatives,
        inputCount=len(lock['files']),outputCount=len(manifest['outputs']))
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(checkCount=len(checks),failures=0,negativeBoundaryCount=len(negatives))))


if __name__=='__main__':main()
