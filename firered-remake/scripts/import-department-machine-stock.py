#!/usr/bin/env python3
"""Read pinned original Git blobs; reuse imported source files, emit no art or audio."""
import argparse,  json, pathlib, re, subprocess
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'

def canonical(v): return json.dumps(v, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()
def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--source', required=True); ap.add_argument('--root', required=True); args=ap.parse_args()
    root=pathlib.Path(args.root); contract=json.loads((root/'scripts/department-machine-stock.contract.json').read_text())
    raw={}; inputs=[]
    for row in contract['inputs']:
        data=subprocess.check_output(['git','-C',args.source,'show',COMMIT+':'+row['path']])
        raw[row['path']]=data.decode(); inputs.append(row)
    def ref(path,symbol,start,end): return {'path':path,'symbol':symbol,'startLine':start,'endLine':end}
    path='src/data/items.json'; text=raw[path]; all_items=json.loads(text)['items']; item=next(x for x in all_items if x['itemId']=='ITEM_TM15')
    pos=text.index('"itemId": "ITEM_TM15"'); start=text.rfind('{',0,pos); end=text.index('}',pos)
    # Existing machine metadata references start at the english field, matching the source import convention.
    start=text.index('"english"',start); item_ref=ref(path,'ITEM_TM15',text[:start].count('\n')+1,text[:end].count('\n'))
    match=re.search(r'#define ITEM_TM15\s+(\d+)',raw['include/constants/items.h']); assert match
    id=int(match.group(1)); assert id==303
    value={'id':{'value':'pret:ITEM_TM15'},'sourceItemSymbol':'ITEM_TM15','sourceItemId':id,'name':item['english'],
        'price':item['price'],'importance':item['importance'],'registrability':item['registrability'],'pocket':item['pocket'],'type':item['type'],
        'holdEffect':item['holdEffect'],'holdEffectParam':item['holdEffectParam'],'fieldUseFunction':item['fieldUseFunc'],
        'battleUsage':item['battleUsage'],'battleUseFunction':item['battleUseFunc'],'secondaryId':item['secondaryId'],
        'canToss':True,'canSell':True,'canGive':True,'source':item_ref}
    path='src/move_descriptions.c'; text=raw[path]; match=re.search(r'const u8 gMoveDescription_HyperBeam\[\] = _\("(.*?)"\);',text); assert match
    description=json.loads('"'+match.group(1)+'"'); line=text[:match.start()].count('\n')+1
    machine={'item':value,'description':description,'declarationDescription':item['description_english'].replace('\\n','\n'),
        'descriptionSource':ref(path,'gMoveDescription_HyperBeam',line,line)}
    path='src/data/pokemon/tmhm_learnsets.h'; text=raw[path]; rows=[]
    for species in contract['ownedSpecies']:
        match=re.search(r'\['+species['symbol']+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),',text,re.S); assert match, species
        start=text[:match.start()].count('\n')+1; end=start+match.group(0).count('\n')
        rows.append({'species':{'value':'pret:'+species['symbol']},'sourceSpeciesId':species['sourceSpeciesId'],
            'canLearn':'TMHM(TM15_HYPER_BEAM)' in match.group(0),'source':ref(path,species['symbol'],start,end)})
    tm15={'machine':machine,'move':{'value':'pret:MOVE_HYPER_BEAM'},'sourceMoveId':63,'movePp':5,'compatibility':rows}
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
    document={'schemaVersion':1,'sourceCommit':COMMIT,
        'stockId':'CeladonCity_DepartmentStore_2F_TMs','stockItems':['pret:'+s for s in symbols[:-1]],
        'inheritedMachineCount':41,'machineCount':42,'speciesCount':163,'inputs':inputs,
        'tm15':tm15,
        'movePower':150,'moveAccuracy':90,'moveType':'TYPE_NORMAL','moveEffect':'EFFECT_RECHARGE','iconSourceItem':'pret:ITEM_TM05'}
    out=root/'content/generated/department-machine-stock';out.mkdir(parents=True,exist_ok=True)
    target=out/'machines.json';target.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'species':len(rows),'compatible':sum(r['canLearn'] for r in rows)}))
if __name__=='__main__': main()
