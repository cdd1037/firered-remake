#!/usr/bin/env python3
"""Focused independent integer translation of pinned Muk CHECK_BAD_MOVE/CHECK_VIABILITY.
Reads and hashes original source text, never executes reference scripts or product code.
Target is healthy Rattata, known Run Away; Sludge is the only damaging move and cannot KO.
"""
import hashlib, json, pathlib, sys
HERE=pathlib.Path(__file__).resolve().parent
REF=pathlib.Path(sys.argv[1])
FILES={
 'data/battle_ai_scripts.s':'0971513157b28ba27ce272acaa2388afa7362a302711f737cad6c3598ca1b389',
 'src/battle_ai_switch_items.c':'dad37604fd92c7540f82a360b14db083f5eb1a91878763e6992666ab2f6e947d'}
for path,sha in FILES.items():
    assert hashlib.sha256((REF/path).read_bytes()).hexdigest()==sha,path
asm=(REF/'data/battle_ai_scripts.s').read_text()
assert 'if_effect EFFECT_MINIMIZE, AI_CV_EvasionUp' in asm
assert 'if_effect EFFECT_DEFENSE_UP_2, AI_CV_DefenseUp' in asm
assert 'if_effect EFFECT_TOXIC, AI_CV_Toxic' in asm

def vector(seed,capped,toxic):
    state=seed;draws=[]
    def draw(bound):
        nonlocal state
        state=(state*1103515245+24691)&0xffffffff
        raw=state>>16;draws.append({'raw':raw,'bound':bound,'result':raw%bound,'after':state})
        return raw%bound
    simulated=[100-draw(16) for _ in range(4)]
    scores=[90 if capped else 100,100,90 if capped else 100,90 if toxic else 100]
    # AI_CV_EvasionUp at 100% HP, no seed/root/curse.
    if draw(256)>=100:scores[0]+=3
    if capped and draw(256)>=128:scores[0]-=1
    if toxic and draw(256)>=50:scores[0]+=3
    # AI_CV_DefenseUp at 100% HP and target MOVE_NONE.
    if capped:
        if draw(256)>=100:scores[2]-=1
    elif draw(256)>=128:scores[2]+=2
    if draw(256)>=200 and draw(256)>=60:scores[2]-=2
    best=[0]
    for i in range(1,4):
        if scores[i]>scores[best[0]]:best=[i]
        if scores[i]==scores[best[0]]:best.append(i)
    chosen=best[draw(len(best))]
    return dict(seed=seed,capped=capped,toxic=toxic,scores=scores,simulated=simulated,chosen=chosen,draws=draws,finalSeed=state)
result={'source':'037335f4c725d7c9aecdac87066f2002b4bd7e14','vectors':[vector(s,c,t) for s in [1,17,0x12345678] for c,t in [(False,False),(True,False),(False,True)]]}
if len(sys.argv)>2 and sys.argv[2]=='--emit': print(json.dumps(result,indent=2))
else:
    assert json.loads((HERE/'source-vectors.json').read_text())==result,'source-derived vectors differ'
    print(json.dumps({'passed':True,'vectors':len(result['vectors']),'sourceFiles':len(FILES)}))
