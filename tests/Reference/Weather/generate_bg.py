#!/usr/bin/env python3
import argparse,json,hashlib,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
from source_extract import function,macros,enum_containing,identity,COMMIT,verify
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);a=p.parse_args();r=a.reference;out=Path(__file__).resolve().parent
paths=['src/battle_anim_rock.c','src/battle_anim_mons.c','include/battle_anim.h','include/gba/io_reg.h','include/gba/defines.h','include/constants/battle.h','include/palette.h']
verify(r,paths)
s={p:(r/p).read_text() for p in paths};funcs={'src/battle_anim_mons.c':['GetBattleAnimBg1Data'],'src/battle_anim_rock.c':['AnimTask_LoadSandstormBackground','AnimTask_LoadSandstormBackground_Step']}
c=(out/'bg-harness.c.in').read_text().replace('@DEFINES@',macros(list(s.values()),['BLDCNT_TGT1_BG1','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_BG1HOFS','REG_OFFSET_BG1VOFS','BG_PLTT_ID','PLTT_SIZE_4BPP','B_SIDE_PLAYER','BG_ANIM_PAL_1'])).replace('@ENUM@',enum_containing(s[paths[2]],'BG_ANIM_PRIORITY')).replace('@ARRAYS@','').replace('@FUNCTIONS@','\n'.join(function(s[p],n) for p,ns in funcs.items() for n in ns));(out/'bg.c').write_text(c)
for opt in ['O0','O2']:subprocess.run(['cc','-std=c11','-'+opt,'-fwrapv',str(out/'bg.c'),'-o',str(out/('bg-'+opt))],check=True)
cases=[]
for flag in [0,1]:
 for contest in [0,1]:
  for attacker in range(4):
   for edge in [0,1]:
    args=list(map(str,[flag,contest,attacker,attacker^1,edge]));data=[subprocess.check_output([str(out/('bg-'+opt)),*args]) for opt in ['O0','O2']];assert data[0]==data[1];rows=[json.loads(x) for x in data[0].splitlines()];assert rows[-1]['frame']==(3 if edge else 159);assert rows[-1]['alive']==0;assert rows[-1]['bg1']==[0,0];cases.append(dict(flag=flag,contest=bool(contest),attacker=attacker,cleanupProbe=bool(edge),rows=rows))
result=dict(schema='firered-sandstorm-bg-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=funcs,optimization_parity=['O0','O2'],asset_ids={'1':'sandstorm-map','2':'sandstorm-tiles','3':'flying-dirt-palette'},events={'1':'SetGpuReg(offset,value)','2':'SetAnimBgAttribute(bg,attribute,value)','5':'AnimLoadCompressedBgTilemap(bg,asset)','6':'AnimLoadCompressedBgGfx(bg,asset,offset)','7':'LoadCompressedPalette(asset,offset,size)','8':'RelocateBattleBgPal(palette,0,0)','11':'InitBattleAnimBg(bg)','12':'DestroyAnimVisualTask(task)'},cases=cases)
(out/'bg-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('bg',len(cases),sum(len(c['rows']) for c in cases))
