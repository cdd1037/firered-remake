#!/usr/bin/env python3
"""Exact21 existing FireRed water tables; no map/field/battle admission side effects."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,json,pathlib,re,sys
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
from pokemon_import_io import safe, canonical
from presentation_source import need as require
WATER_MAPS={'sCeladonCity_FireRed': ('map.celadon-city', 'MAP_CELADON_CITY'), 'sCeruleanCity_FireRed': ('map.cerulean-city', 'MAP_CERULEAN_CITY'), 'sFuchsiaCity_FireRed': ('map.fuchsia-city', 'MAP_FUCHSIA_CITY'), 'sPalletTown_FireRed': ('map.pallet-town', 'MAP_PALLET_TOWN'), 'sRoute10_FireRed': ('map.route-10', 'MAP_ROUTE10'), 'sRoute11_FireRed': ('map.route-11', 'MAP_ROUTE11'), 'sRoute12_FireRed': ('map.route-12', 'MAP_ROUTE12'), 'sRoute13_FireRed': ('map.route-13', 'MAP_ROUTE13'), 'sRoute22_FireRed': ('map.route-22', 'MAP_ROUTE22'), 'sRoute23_FireRed': ('map.route-23', 'MAP_ROUTE23'), 'sRoute24_FireRed': ('map.route-24', 'MAP_ROUTE24'), 'sRoute25_FireRed': ('map.route-25', 'MAP_ROUTE25'), 'sRoute4_FireRed': ('map.route-4', 'MAP_ROUTE4'), 'sRoute6_FireRed': ('map.route-6', 'MAP_ROUTE6'), 'sSafariZoneCenter_FireRed': ('map.safari-zone-center', 'MAP_SAFARI_ZONE_CENTER'), 'sSafariZoneEast_FireRed': ('map.safari-zone-east', 'MAP_SAFARI_ZONE_EAST'), 'sSafariZoneNorth_FireRed': ('map.safari-zone-north', 'MAP_SAFARI_ZONE_NORTH'), 'sSafariZoneWest_FireRed': ('map.safari-zone-west', 'MAP_SAFARI_ZONE_WEST'), 'sSSAnneExterior_FireRed': ('map.ssanne-exterior', 'MAP_SSANNE_EXTERIOR'), 'sVermilionCity_FireRed': ('map.vermilion-city', 'MAP_VERMILION_CITY'), 'sViridianCity_FireRed': ('map.viridian-city', 'MAP_VIRIDIAN_CITY')}
WEIGHTS=(60,30,5,4,1)
def build(root):
 raw_root=safe(root,'content/raw/pokefirered/037335f4')
 source=json.loads(safe(raw_root,'src/data/wild_encounters.json').read_bytes());tables=[];species=set()
 group=next(g for g in source['wild_encounter_groups'] if any(t.get('base_label') in WATER_MAPS for t in g.get('encounters',[])))
 waterfield=next(f for f in group['fields'] if f['type']=='water_mons')
 require(waterfield['encounter_rates']==list(WEIGHTS),'Source water weights differ')
 for table in group['encounters']:
  if table.get('base_label') not in WATER_MAPS:continue
  stable,symbol=WATER_MAPS[table['base_label']];require(table['map']==symbol,'Map symbol differs');w=table['water_mons'];rows=[]
  for i,row in enumerate(w['mons']):
   species.add(row['species']);rows.append(dict(index=i,species={'value':'pret:'+row['species']},minimumLevel=row['min_level'],maximumLevel=row['max_level'],weight=WEIGHTS[i]))
  require(len(rows)==5,'Source water slots differ')
  tables.append(dict(map={'value':stable},rawEncounterRate=w['encounter_rate'],slots=rows))
 tables.sort(key=lambda t:t['map']['value']);require(len(tables)==21 and species=={'SPECIES_TENTACOOL','SPECIES_PSYDUCK','SPECIES_KOFFING'},'Surf source scope differs')
 held=[];path='src/data/pokemon/species_info.h';text=safe(raw_root,path).read_text()
 for name in sorted(species):
  m=re.search(r'^    \['+name+r'\] =\s*\n    \{\n(.*?)^    \},',text,re.M|re.S);require(m is not None,'Held species missing');f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
  held.append(dict(species={'value':'pret:'+name},common=f['itemCommon'],rare=f['itemRare']))
 require(all(r['common']==r['rare']=='ITEM_NONE' for r in held),'Expected source empty held slots differ')
 doc=dict(schemaVersion=1,tables=tables,heldItems=held,heldItemDrawBound=100)
 return canonical(doc)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();b=build(a.project_root);path=safe(a.project_root,'content/generated/data/encounters/surf.json')
 if a.check:require(path.read_bytes()==b,'Water catalog bytes differ')
 else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
 print(json.dumps(dict(outputBytes=len(b))))
