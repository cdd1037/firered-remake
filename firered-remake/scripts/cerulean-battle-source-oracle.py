#!/usr/bin/env python3
"""Independent source-semantic vectors for the opt-in Cerulean battle kernel.
No production C# is imported, executed or scraped. Fails closed on source drift.
"""
import argparse, hashlib, json, pathlib, subprocess
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
ROOT=pathlib.Path(__file__).resolve().parents[1]
LOCK=ROOT/'scripts/cerulean-battle-source.lock.json'
OUTPUT=ROOT/'tests/Game.CeruleanBattle.Tests/source-vectors.json'
def generate(ref):
    lock=json.loads(LOCK.read_text())
    assert lock['commit']==COMMIT
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==COMMIT
    source={}
    for row in lock['files']:
        data=(ref/row['path']).read_bytes()
        assert hashlib.sha256(data).hexdigest()==row['sha256'],row['path']
        source[row['path']]=data.decode()
    assert 'gBattleMoveDamage = gBattleMons[gBattlerTarget].maxHP / 2;' in source['src/battle_script_commands.c']
    assert 'gBattleMons[gBattlerTarget].ability != ABILITY_SHELL_ARMOR' in source['src/battle_script_commands.c']
    assert 'gBattleMons[gActiveBattler].maxHP - gBattleMons[gActiveBattler].hp > itemEffects[paramOffset]' in source['src/battle_ai_switch_items.c']
    assert 'ShouldGetStatBadgeBoost(FLAG_BADGE05_GET, battlerIdDef)' in source['src/pokemon.c']
    vectors=[]
    for move,target in [('RECOVER','ONIX'),('SWIFT','ONIX'),('SWIFT','SHELLDER'),('WATER_SPORT','ONIX')]:
        for seed in [0,1,99,777]:
            current=seed; trace=[]
            def draw(purpose,bound=65536):
                nonlocal current
                before=current;current=(current*0x41C64E6D+0x6073)&0xffffffff;raw=current>>16
                trace.append(dict(purpose=purpose,before=before,after=current,raw=raw,bound=bound,result=raw%bound))
                return raw%bound
            while draw('wild-battle.opponent.choose-slot',4)!=0:pass
            if move=='SWIFT':
                if target!='SHELLDER':draw('wild-battle.player.critical',16)
                draw('wild-battle.player.damage',16);draw('wild-battle.player.secondary-effect',100)
            draw('wild-battle.turn-end.random-turn-number')
            vectors.append(dict(move=move,target=target,seed=seed,trace=trace))
    return dict(schemaVersion=1,sourceCommit=COMMIT,sourceLockSha256=hashlib.sha256(LOCK.read_bytes()).hexdigest(),
                semantics='source command RNG only, excludes animation/VBlank/controller timing',traceVectors=vectors,
                recover=[dict(maxHp=m,before=b,after=min(m,b+max(1,m//2))) for m,b in [(1,1),(53,1),(53,50),(54,1)]],
                potion=[dict(maxHp=m,hp=h,use=h>0 and (h<m//4 or m-h>50)) for m,h in [(43,9),(43,10),(80,30),(81,30),(80,0)]])
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--reference',type=pathlib.Path);parser.add_argument('--check',action='store_true');args=parser.parse_args()
    ref=args.reference or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip());data=generate(ref);encoded=json.dumps(data,indent=2)+'\n'
    if args.check:assert OUTPUT.read_text()==encoded,'Cerulean source vectors changed'
    else:OUTPUT.write_text(encoded)
    print(json.dumps(dict(passed=True,sourceCommit=COMMIT,vectors=len(data['traceVectors']),sourceFiles=len(json.loads(LOCK.read_text())['files']))))
if __name__=='__main__':main()
