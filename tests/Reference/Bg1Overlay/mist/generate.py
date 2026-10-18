#!/usr/bin/env python3
import argparse,json,subprocess,sys,re
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
from source_extract import *
from validate_common import verify,compile_pair,observe
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);args=p.parse_args();root=args.reference;out=Path(__file__).resolve().parent
paths=['src/battle_anim_ice.c','src/battle_anim_mons.c','src/battle_anim.c','include/battle_anim.h','include/constants/battle.h','src/sprite.c','src/battle_main.c','src/main.c','data/battle_anim_scripts.s']
verify(root,paths)
sources={p:(root/p).read_text() for p in paths}
names={paths[0]:['AnimThrowMistBall'],paths[1]:['SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','TranslateAnimSpriteToTargetMonLocation'],paths[2]:['DestroyAnimSprite']}
functions='\n'.join(function(sources[path],name) for path,ns in names.items() for name in ns)
generated=(out/'harness.c.in').read_text().replace('@FUNCTIONS@',functions).replace('@ARRAY@',array(sources[paths[1]],'sBattlerCoords')).replace('@ENUM@',enum_containing(sources[paths[3]],'BATTLER_COORD_X_2')).replace('@DEFINES@',macros([sources[paths[3]],sources[paths[4]]],['MAX_BATTLERS_COUNT','B_SIDE_PLAYER','sTransl_Speed','sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY']))
(out/'generated.c').write_text(generated);compile_pair(out/'generated.c',out/'oracle')
script=sources['data/battle_anim_scripts.s'];m=re.search(r'^\s*createsprite gMistBallSpriteTemplate, ANIM_TARGET, 0, ([^\n]+)',script,re.M);recipe=[int(x.strip(),0) for x in m[1].split(',')];assert recipe==[0,0,0,0,30,0]
cases=[]
def run(name,double,attacker,target,a,equal=0,mode=0):
 command=[str(out/'oracle'),str(double),str(attacker),str(target),str(equal),*map(str,a),str(mode)]
 rows=observe(command)
 cases.append(dict(name=name,double=bool(double),attacker=attacker,target=target,args=a,equal_x=bool(equal),probe=bool(mode) or name.startswith(('wide-delta','s16-offset','equal-x')),divergent_coord2=bool(mode&2),rows=rows))
for double in range(2):
 for attacker in range(4):
  for target in range(4):
   run('script-recipe',double,attacker,target,recipe)
   for flag in [0,1,256,257]:run('offset-flags-'+str(flag),double,attacker,target,[11,-9,13,-7,30,flag])
for side in range(2):
 run('equal-x-side-'+str(side),0,side,side^1,[11,-9,13,-7,2,0],equal=1)
 for duration in [1,2,255]:
  run('wide-delta-duration-'+str(duration),1,side,side^1,[300,-200,-300,200,duration,0])
 run('s16-offset-wrap',1,side,side^1,[32760,32760,-32768,-32768,30,257])
 run('u16-accumulator-wrap',1,side,side^1,recipe,mode=1)
for side in range(2):
 for flag in [0,1,256,257]:run('divergent-coordinate-helper-probe',1,side,side^1,[11,-9,13,-7,30,flag],mode=2)
result=dict(optimization_parity=['O0','O2'],schema='firered-mist-ball-source-v1',commit=COMMIT,sources=identity(root,paths),source_functions=names,recipe=recipe,callback_ids={'0':'AnimThrowMistBall','1':'TranslateAnimSpriteToTargetMonLocation','2':'StartAnimLinearTranslation','3':'AnimTranslateLinear_WithFollowup','4':'DestroyAnimSprite'},stub_picture_offsets=[8,3,12,-4],normal_coord2_picture_offsets=[8,3,12,-4],divergent_probe_coord2_picture_offsets=[10,7,14,-2],cases=cases)
(out/'vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
print(json.dumps({'cases':len(cases),'rows':sum(len(c['rows']) for c in cases),'recipe_destroy_invocation':cases[0]['rows'][-1]['invocation'],'path':str(out/'vectors.json')}))
