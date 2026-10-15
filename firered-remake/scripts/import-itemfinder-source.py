#!/usr/bin/env python3
"""Read pinned Git blobs and generate the bounded Itemfinder/Sacred Ash source contract."""
import argparse,  io, json, re, subprocess
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def generate(reference):
    raw={}
    def read(path):
        if path not in raw: raw[path]=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference)
        return raw[path].decode()
    for path in ['src/itemfinder.c','src/fieldmap.c','src/prof_pc.c','src/item_use.c','src/party_menu.c','src/pokemon.c','src/item_menu.c','src/data/pokemon/item_effects.h','src/data/items.json','src/strings.c','include/constants/items.h','include/constants/flags.h','data/scripts/itemfinder.inc','data/scripts/obtain_item.inc','data/text/itemfinder.inc','data/maps/Route11_EastEntrance_2F/scripts.inc','data/maps/Route11_EastEntrance_2F/map.json']:
        read(path)
    def number(path,symbol):
        value=re.search(r'^#define '+symbol+r'\s+([^\n]+)',read(path),re.M).group(1).split('//')[0].strip()
        if value.startswith('('):
            s,n=re.fullmatch(r'\((\w+) \+ (0x[0-9A-Fa-f]+|\d+)\)',value).groups();return number(path,s)+int(n,0)
        return int(value,0)
    underfoot=[]
    for name in ['PokemonTower_7F','ViridianCity_Gym','Route12','Route16','NavelRock_Summit']:
        path='data/maps/'+name+'/map.json';data=json.loads(read(path))
        for e in data['bg_events']:
            if e.get('underfoot'):
                underfoot.append(dict(map=data['id'],x=e['x'],y=e['y'],elevation=e['elevation'],item=e['item'],sourceItemId=number('include/constants/items.h',e['item']),flag=e['flag'],sourceFlagId=number('include/constants/flags.h',e['flag']),quantity=1))
    assert len(underfoot)==5
    source=read('src/itemfinder.c')
    for anchor in ['dx >= -7','dx <=  7','dy >= -5','dy <=  5','gSpecialVar_0x8006 = 1;','if (dx == 0 && dy == 0)','dx2 + dy2 == dx3 + dy3 && (dy2 > dy3 || (dy2 == dy3 && tItemY < dy))']:
        assert anchor in source,anchor
    texts={}
    for name in ['gText_NopeTheresNoResponse','gText_ItemfinderResponding','gText_ItemfinderShakingWildly','gText_WontHaveEffect']:
        s=re.search(r'const u8 '+name+r'\[\] = _\("(.*?)"\);',read('src/strings.c')).group(1)
        texts[name]=s.replace('\\n','\n').replace('\\p','\n\n').replace('{PAUSE_UNTIL_PRESS}','')
    for path,name in [('data/text/itemfinder.inc','Text_DugUpItemFromGround'),('data/scripts/obtain_item.inc','Text_PutItemAway'),('data/scripts/obtain_item.inc','Text_TooBadBagFull')]:
        m=re.search(r'^'+name+r'::\n((?:\s*\.string .*\n)+)',read(path),re.M)
        if m: texts[name]=''.join(re.findall(r'\.string "(.*?)"',m[1])).replace('\\n','\n').replace('\\p','\n\n').rstrip('$')
    # Shared obtain-item text is sourced in its own text file in this revision.
    for name in ['Text_PutItemAway','Text_TooBadBagFull']:
        if name not in texts:
            path='data/text/obtain_item.inc';m=re.search(r'^'+name+r'::\n((?:\s*\.string .*\n)+)',read(path),re.M)
            assert m,name
            texts[name]=''.join(re.findall(r'\.string "(.*?)"',m[1])).replace('\\n','\n').replace('\\p','\n\n').rstrip('$')
    items=json.loads(read('src/data/items.json'))['items'];item=next(x for x in items if x['itemId']=='ITEM_ITEMFINDER')
    assert item['registrability']==1 and item['fieldUseFunc']=='ItemUseOutOfBattle_Itemfinder' and item['battleUsage']==0
    icons={};iconrows=[]
    for short in ['itemfinder','sacred_ash','macho_brace','leftovers']:
        iconpath='graphics/items/icons/'+short+'.png';palpath='graphics/items/icon_palettes/'+short+'.pal'
        raw[iconpath]=subprocess.check_output(['git','show',COMMIT+':'+iconpath],cwd=reference)
        palette=[tuple(map(int,l.split())) for l in read(palpath).splitlines()[3:]]
        icon=Image.open(io.BytesIO(raw[iconpath]));assert icon.mode=='P' and icon.size==(24,24)
        rgba=bytes(c for i in icon.get_flattened_data() for c in (*palette[i],0 if i==0 else 255))
        icons[short+'.rgba']=rgba
        iconrows.append(dict(item='pret:ITEM_'+short.upper(),file=short+'.rgba'))
    doc=dict(schemaVersion=1,sourceCommit=COMMIT,itemfinderSourceId=261,requiredCaught=30,underfoot=underfoot,texts=texts,itemfinderDescription=item['description_english'].replace('\\n','\n'),icons=iconrows,inputs=[dict(path=p) for p,b in sorted(raw.items())],limitations=['Uses source range, nearest ordering and loaded direct map connections; missing connected map data is not fabricated.','Native Itemfinder arrow/star animation and timed dings are represented by source direction/ding metadata, not pixel timing.'])
    return doc,raw,icons
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('output',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    doc,raw,icons=generate(a.reference);outputs={'itemfinder-source.json':(json.dumps(doc,ensure_ascii=False,indent=2)+'\n').encode()}
    outputs.update(icons)
    outputs.update({'source/'+k:v for k,v in raw.items()})
    lock=json.loads(Path(__file__).with_name('itemfinder-source.lock.json').read_text())
    assert doc['inputs'] == lock['inputs'], 'Locked Itemfinder source contract changed'
    for path,data in outputs.items():
        f=a.output/path
        if a.check: assert f.read_bytes()==data,path
        else: f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(data)
    print(json.dumps(dict(passed=True,sourceCommit=COMMIT,underfoot=len(doc['underfoot']),inputs=len(raw))))
