#!/usr/bin/env python3
"""Import the three source event species, 117 TM/HM cells and six inventory definitions.
Only reads pinned Git blobs. Never builds or executes reference code. --check is byte-exact.
"""
import argparse,  json, re, subprocess
from pathlib import Path
from machine_catalog import compatibility_additions
ROOT=Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
SPECIES=['SPECIES_HO_OH','SPECIES_LUGIA','SPECIES_DEOXYS']
def need(v,m):
    if not v: raise ValueError(m)
def generate(reference,dependency):
    inputs={}; raw={}; dependencies=[]
    def read(path):
        if path not in raw:
            b=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference);raw[path]=b
            inputs[path]=dict(path=path)
        return raw[path].decode()
    def one(path,pattern):
        m=list(re.finditer(pattern,read(path),re.M|re.S));need(len(m)==1,'Unique source: '+pattern);return m[0]
    def ref(path,symbol,m):
        return dict(path=path,symbol=symbol,startLine=read(path).count('\n',0,m.start())+1,endLine=read(path).count('\n',0,m.end())+1)
    def ident(s): return dict(value='pret:'+s)
    def dep(path):
        b=(dependency/path).read_bytes()
        dependencies.append(dict(path=path));return json.loads(b)
    canonical=dep('content/generated/canonical-pokemon/catalog.json')
    species={s['id']['value'][5:]:s for s in canonical['species']};metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
    form=canonical['generationContract']['formStatOverrides'][0]
    need(form['species']==ident('SPECIES_DEOXYS') and form['baseStats']==dict(hp=50,attack=180,defense=20,speed=150,specialAttack=180,specialDefense=20),'FireRed Attack forme')
    for s in SPECIES:
        for r in [species[s]['source'],species[s]['learnsetSource'],metadata[s]['dexSource'],metadata[s]['learnsetPointerSource']]:
            read(r['path'])
        need(species[s]['ability1']=='ABILITY_PRESSURE' and species[s]['ability2']=='ABILITY_NONE' and metadata[s]['sourceEvolutions']==[],'Terminal Pressure event species')
    for r in [form['source'],form['ruleSource']]:
        read(r['path'])
    need(species['SPECIES_DEOXYS']['sourceSpeciesId']==410 and metadata['SPECIES_DEOXYS']['nationalDexNumber']==386,'Internal ID versus national dex')
    encounters=[]
    for s,kind,mapname,level in [('SPECIES_HO_OH','hoOh','NavelRock_Summit',70),('SPECIES_LUGIA','lugia','NavelRock_Base',70),('SPECIES_DEOXYS','deoxys','BirthIsland_Exterior',30)]:
        p='data/maps/'+mapname+'/scripts.inc';m=one(p,r'^\tseteventmon '+s+', '+str(level)+r'\n')
        encounters.append(dict(kind=kind,map='MAP_'+re.sub(r'([a-z])([A-Z])',r'\1_\2',mapname).upper(),species=ident(s),level=level,heldItem='ITEM_NONE',modernFatefulEncounter=True,source=ref(p,'seteventmon '+s,m)))
    for p in ['asm/macros/event.inc','src/pokemon.c','src/battle_setup.c','src/battle_main.c','include/random.h','include/constants/pokemon.h','include/constants/species.h','include/constants/items.h','src/data/items.json']:read(p)
    need('CreateEventMon(&gEnemyParty[0], species, level, USE_RANDOM_IVS, FALSE, 0, OT_ID_PLAYER_ID, 0);' in read('src/pokemon.c') and 'SetMonData(mon, MON_DATA_MODERN_FATEFUL_ENCOUNTER, &isModernFatefulEncounter);' in read('src/pokemon.c'),'Event generation source')
    cells=compatibility_additions(ROOT, 'content/generated/event-island-species/catalog.json', 'machines')
    need(len(cells)==39, 'Exact admitted machine scope')
    dependencies.extend(dict(path=p) for p in ['scripts/import-config/machine-admissions.json', 'scripts/import-config/machine-catalog.json'])
    text=read('src/data/items.json');source_items=json.loads(text)['items'];items=[]
    constants={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',read('include/constants/items.h'),re.M)}
    for symbol in ['ITEM_MYSTIC_TICKET','ITEM_AURORA_TICKET','ITEM_SACRED_ASH','ITEM_ITEMFINDER','ITEM_MACHO_BRACE','ITEM_LEFTOVERS']:
        i=next(i for i in source_items if i['itemId']==symbol);m=one('src/data/items.json',r'"itemId":\s*"'+symbol+r'"');r=ref('src/data/items.json',symbol,m)
        meta=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=constants[symbol],name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['importance']==0,canGive=i['importance']==0,source=r)
        items.append({k:meta[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}|dict(description=i['description_english'].replace('\\n','\n'),healHp=0,cureStatus=0,effectBytes=[],useSupport=10,effectSource=None,sourceMetadata=meta))
    vectors=[]
    for s,level,seed in [('SPECIES_HO_OH',70,42),('SPECIES_LUGIA',70,0x12345678),('SPECIES_DEOXYS',30,0xDEADBEEF)]:
        start=seed;draws=[]
        for _ in range(4):seed=(seed*1103515245+24691)&0xffffffff;draws.append(seed>>16)
        pid=draws[0]|draws[1]<<16;ivs=[draws[2]&31,(draws[2]>>5)&31,(draws[2]>>10)&31,draws[3]&31,(draws[3]>>5)&31,(draws[3]>>10)&31]
        vectors.append(dict(species=ident(s),level=level,initialSeed=start,finalSeed=seed,rawDraws=draws,personality=pid,nature=pid%25,ivs=ivs,originalTrainerId=0x12345678))
    doc=dict(sourceCommit=COMMIT,profile='firered-event-island-species-v1',ownedSpeciesCount=149,opponentSpeciesCount=183,species=[dict(definition=species[s],metadata=metadata[s]) for s in SPECIES],formOverride=form,encounters=encounters,machines=cells,items=items,generationVectors=vectors,generationOrder='Explicit low16 then high16 profile; original target-compiler Random32 evaluation order remains unverified.',inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=dependencies)
    return doc,raw

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path);p.add_argument('output',type=Path);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args()
    d,raw=generate(a.reference,a.dependency_root);files={'content/generated/event-island-species/catalog.json':(json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode()}
    for path,data in raw.items():files['content/raw/pokefirered/037335f4/'+path]=data
    lock=dict(sourceCommit=COMMIT,profile=d['profile'],files=[dict(path=k) for k,v in sorted(files.items())])
    for path,b in files.items():
        dest=a.output/path
        if a.check:need(dest.read_bytes()==b,'Regeneration differs: '+path)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
    print(json.dumps(dict(checked=a.check,inputs=len(raw)),indent=2))
if __name__=='__main__':main()
