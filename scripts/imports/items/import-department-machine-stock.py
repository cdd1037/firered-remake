#!/usr/bin/env python3
"""Read pinned original Git blobs; reuse imported source files, emit no art or audio."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  json, pathlib, re, subprocess
from machine_catalog import admission_rows, admission_view
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'

def canonical(v): return json.dumps(v, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()
def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--source', required=True); ap.add_argument('--root', required=True); args=ap.parse_args()
    root=pathlib.Path(args.root); contract=json.loads((root/'scripts/recipes/items/department-machine-stock.contract.json').read_text())
    raw={}; inputs=[]
    for row in contract['inputs']:
        data=subprocess.check_output(['git','-C',args.source,'show',COMMIT+':'+row['path']])
        raw[row['path']]=data.decode(); inputs.append({key:row[key] for key in ('path','localPath')})
    all_items=json.loads(raw['src/data/items.json'])['items']
    relative='content/generated/data/machine-learning/department-machine-stock.json'
    tm15=admission_view(root, relative, '/tm15')
    rows=admission_rows(root, relative, '/tm15')
    assert tm15['item']=='pret:ITEM_TM15' and len(rows)==163
    assert re.search(r'#define MOVE_HYPER_BEAM\s+63\b',raw['include/constants/moves.h'])
    battle=raw['src/data/battle_moves.h']; match=re.search(r'\[MOVE_HYPER_BEAM\]\s*=\s*\{(.*?)\},',battle,re.S); assert match
    for key,expected in [('effect','EFFECT_RECHARGE'),('power','150'),('type','TYPE_NORMAL'),('accuracy','90'),('pp','5')]:
        assert re.search(r'\.'+key+r'\s*=\s*'+expected+r'\s*,',match.group(1))
    script=raw['data/maps/CeladonCity_DepartmentStore_2F/scripts.inc']; block=script.split('CeladonCity_DepartmentStore_2F_TMs::\n')[1]
    symbols=re.findall(r'\.2byte (ITEM_\w+)',block); assert symbols==['ITEM_TM05','ITEM_TM15','ITEM_TM28','ITEM_TM31','ITEM_TM43','ITEM_TM45','ITEM_NONE']
    prices={x['itemId']:x['price'] for x in all_items}; assert [prices[s] for s in symbols[:-1]]==[1000,7500,2000,3000,3000,3000]
    icons=raw['src/data/item_icon_table.h']
    for tm in ['TM05','TM15']:
        assert re.search(r'\[ITEM_'+tm+r'\]\s*=\s*\{gItemIcon_TMHM, gItemIconPalette_NormalTMHM\}',icons)
    document={'schemaVersion':1,
        'stockId':'CeladonCity_DepartmentStore_2F_TMs','stockItems':['pret:'+s for s in symbols[:-1]],
        'inheritedMachineCount':41,'machineCount':42,'speciesCount':163,
        'tm15':tm15,
        'movePower':150,'moveAccuracy':90,'moveType':'TYPE_NORMAL','moveEffect':'EFFECT_RECHARGE','iconSourceItem':'pret:ITEM_TM05'}
    out=root/'content/generated/data/machine-learning/department-machine-stock.json';out.parent.mkdir(parents=True,exist_ok=True)
    target=out;target.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'species':len(rows),'compatible':sum(r['canLearn'] for r in rows)}))
if __name__=='__main__': main()
