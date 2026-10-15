#!/usr/bin/env python3
"""Verify current source semantics and required fan-club resources."""
import argparse
import json
from pathlib import Path


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
    contract=json.loads((package/'contracts.json').read_bytes())
    rules=contract['rules'];source=(args.reference_root/'src/trainer_fan_club.c').read_text(encoding='utf-8')
    need(rules['counterIncrements']==[2,1,2,1] and 'sCounterIncrements[] = {2, 1, 2, 1};' in source,'counter source semantics')
    need(rules['counterThreshold']==20 and 'fanClub->timer + sCounterIncrements[a1] >= 20' in source,'counter threshold')
    need(rules['decay']['intervalHours']==12 and 'gSaveBlock2Ptr->playTimeHours - timer < 12' in source,'twelve-hour decay boundary')
    need(rules['decay']['disabledAtHours']==999 and 'gSaveBlock2Ptr->playTimeHours < 999' in source,'clock cap boundary')
    need(rules['gainScanOrder']==['FANCLUB_MEMBER'+str(i) for i in [2,4,6,1,8,7,5,3]],'gain scan order')
    need(rules['loseScanOrder']==['FANCLUB_MEMBER'+str(i) for i in [6,7,4,8,5,2,1,3]],'lose scan order')
    need(len(contract['actors'])==10 and len(contract['transition']['nonFanPositions'])==8,'complete source actor positioning')
    report=dict(checkCount=len(checks),failures=0,checks=checks,scope='Current fan-club source semantics and runtime resource completeness; historical importer helper pipeline retired')
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report))

if __name__=='__main__':main()
