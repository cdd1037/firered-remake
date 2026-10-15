#!/usr/bin/env python3
"""Exact21 existing FireRed water tables; no map/field/battle admission side effects."""
import argparse,importlib.util,json,pathlib,re,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('surf_meta',ROOT/'scripts/import-surf-pokemon.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
WEIGHTS=(60,30,5,4,1)
def build(root):
 lock,raw=meta.authenticated(root);inv={r['path']:r for r in lock['files']};source=json.loads(raw['src/data/wild_encounters.json']);tables=[];species=set()
 group=next(g for g in source['wild_encounter_groups'] if any(t.get('base_label') in meta.WATER_MAPS for t in g.get('encounters',[])))
 waterfield=next(f for f in group['fields'] if f['type']=='water_mons')
 meta.require(waterfield['encounter_rates']==list(WEIGHTS),'Source water weights differ')
 for table in group['encounters']:
  if table.get('base_label') not in meta.WATER_MAPS:continue
  stable,symbol=meta.WATER_MAPS[table['base_label']];meta.require(table['map']==symbol,'Map symbol differs');w=table['water_mons'];rows=[]
  for i,row in enumerate(w['mons']):
   species.add(row['species']);rows.append(dict(index=i,species={'value':'pret:'+row['species']},minimumLevel=row['min_level'],maximumLevel=row['max_level'],weight=WEIGHTS[i]))
  meta.require(len(rows)==5,'Source water slots differ')
  tables.append(dict(map={'value':stable},sourceMapSymbol=symbol,sourceCommit=meta.COMMIT,sourcePath='src/data/wild_encounters.json',sourceSymbol=table['base_label'],rawEncounterRate=w['encounter_rate'],slots=rows))
 tables.sort(key=lambda t:t['map']['value']);meta.require(len(tables)==21 and species=={'SPECIES_TENTACOOL','SPECIES_PSYDUCK','SPECIES_KOFFING'},'Surf source scope differs')
 held=[];path='src/data/pokemon/species_info.h';text=raw[path].decode()
 for name in sorted(species):
  m=re.search(r'^    \['+name+r'\] =\s*\n    \{\n(.*?)^    \},',text,re.M|re.S);meta.require(m is not None,'Held species missing');f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
  held.append(dict(species={'value':'pret:'+name},common=f['itemCommon'],rare=f['itemRare']))
 meta.require(all(r['common']==r['rare']=='ITEM_NONE' for r in held),'Expected source empty held slots differ')
 doc=dict(schemaVersion=1,profile='firered-surf-encounters-v1',sourceCommit=meta.COMMIT,tables=tables,heldItems=held,heldItemDrawBound=100,limitations=['Exactly17 already imported mainland maps and4 Safari maps; no route boundary is opened.','Five-slot water data is distinct from land and fishing. Dratini/Dragonair remain deferred.','Use the shared ordinary generation kernel and existing battle engines; active Safari decides Safari battle dispatch.'])
 return meta.canonical(doc)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();b=build(a.project_root);path=meta.safe(a.project_root,'content/generated/surf-encounters/catalog.json')
 if a.check:meta.require(path.read_bytes()==b,'Water catalog bytes differ')
 else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
 print(json.dumps(dict(outputBytes=len(b))))
