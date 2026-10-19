#!/usr/bin/env python3
"""Extract pinned source callbacks, compile authored bounded harness, emit numerical vectors."""
import tempfile
import argparse,sys,json,subprocess,hashlib
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from source_extract import *
from validate_common import verify,compile_pair,observe
HERE=Path(__file__).resolve().parent
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--build-dir',type=Path);p.add_argument('--output',type=Path,default=(HERE.parents[2]/ 'Game.Presentation.Tests/Fixtures')/'overlay-task-source-vectors.json');p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a=p.parse_args();root=a.reference;out=(a.build_dir or Path(tempfile.mkdtemp(prefix='overlay-task-reference-'))).resolve();out.mkdir(parents=True,exist_ok=True)
paths=['src/battle_anim_effects_2.c','src/battle_anim_ice.c','include/battle_anim.h','include/gba/io_reg.h','include/gba/defines.h','include/constants/battle.h','include/palette.h','src/battle_anim_mons.c']
verify(root,paths)
sources={p:(root/p).read_text() for p in paths};names={paths[0]:['AnimTask_ScaryFace','AnimTask_ScaryFace_Step'],paths[1]:['AnimTask_HazeScrollingFog','AnimTask_HazeScrollingFog_Step','AnimTask_MistBallFog','AnimTask_MistBallFog_Step']}
functions=function(sources['src/battle_anim_mons.c'],'GetBattleAnimBg1Data', signed_byte=False)+'\n'+'\n'.join(function(sources[path],name, signed_byte=False) for path,ns in names.items() for name in ns)
template=(HERE/'harness.c.in').read_text();generated=template.replace('@DEFINES@',macros([sources[x] for x in paths[3:]],['BLDCNT_TGT1_BG1','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_BG1HOFS','REG_OFFSET_BG1VOFS','BG_PLTT_ID','PLTT_SIZE_4BPP','B_SIDE_PLAYER','B_SIDE_OPPONENT','BG_ANIM_PAL_1'], require_separator=True)).replace('@ENUM@',enum_containing(sources[paths[2]],'BG_ANIM_PRIORITY')).replace('@ARRAYS@','\n'.join(array(sources[paths[1]],n) for n in ['sHazeBlendAmounts','sMistBlendAmounts'])).replace('@FUNCTIONS@',functions)
(out/'generated.c').write_text(generated);compile_pair(out/'generated.c',out/'oracle')
cases=[]
for family,name in enumerate(['scary-face','haze-scrolling-fog','mist-ball-fog']):
 for contest in [0,1]:
  for attacker in range(4):
   for target in range(4):
    rows=observe([str(out/'oracle'),str(family),str(contest),str(attacker),str(target),'0'])
    cases.append(dict(name=name,contest=bool(contest),attacker=attacker,target=target,initial_bg1=[123,456],initial_attributes=[7]*7,task_id=3,rows=rows))
for family,name in enumerate(['scary-face','haze-scrolling-fog','mist-ball-fog']):
 rows=observe([str(out/'oracle'),str(family),'0','0','1','1'])
 cases.append(dict(name=name+'-cleanup-edge',contest=False,attacker=0,target=1,task_id=3,rows=rows))
def representative(c):
    # Each task's complete phases and cleanup edge; target-side material
    # selection, plus the explicit contest branch.
    return c['name'].endswith('-cleanup-edge') or (c['contest'], c['attacker'], c['target']) in [(False, 0, 0), (False, 0, 1), (True, 1, 1)]

if not a.full:
    cases = [c for c in cases if representative(c)]
result=dict(profile='full' if a.full else 'representative',optimization_parity=['O0','O2'],schema='firered-bg1-overlay-source-v1',commit=COMMIT,sources=identity(root,paths),source_functions={**names,'src/battle_anim_mons.c':['GetBattleAnimBg1Data']},compiler_flags=['-std=c11','-O0','-fwrapv'],tables={n:[int(x) for x in __import__('re').findall(r'\b\d+\b',array(sources[paths[1]],n).split('=')[1])] for n in ['sHazeBlendAmounts','sMistBlendAmounts']},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n')
print(json.dumps({'cases':len(cases),'rows':sum(len(c['rows']) for c in cases),'lifetimes':{c['name']:c['rows'][-1]['frame'] for c in cases},'path':str(a.output)}))
