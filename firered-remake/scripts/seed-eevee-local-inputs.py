#!/usr/bin/env python3
"""Materialize exact pinned Git blobs. No upstream code or network is used."""
import argparse,hashlib,json,pathlib,subprocess
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
ROOT=pathlib.Path(__file__).resolve().parents[1]
PATHS=['sound/cry_tables.inc','src/pokemon.c','charmap.txt','include/constants/abilities.h','include/constants/items.h','include/constants/moves.h','include/constants/pokemon.h','include/constants/species.h','src/data/battle_moves.h','src/data/items.json','src/data/pokemon/evolution.h','src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/species_info.h','src/data/text/move_names.h','src/data/text/species_names.h','src/data/pokemon/tmhm_learnsets.h','src/data/graphics/pokemon.h','src/pokemon_icon.c','src/data/pokemon_graphics/front_pic_coordinates.h','src/data/pokemon_graphics/back_pic_coordinates.h','src/data/pokemon_graphics/enemy_mon_elevation.h','src/data/pokemon_graphics/footprint_table.h','src/data/pokemon/pokedex_entries.h','src/data/pokemon/pokedex_text_fr.h','graphics/pokemon/icon_palettes/icon_palette_2.pal','graphics/pokedex/kanto_dex_bgpals.pal']+[f'graphics/pokemon/eevee/{s}' for s in ('front.png','back.png','normal.pal','shiny.pal','icon.png','footprint.png')]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=pathlib.Path,required=True);a=p.parse_args();rows=[]
for rel in sorted(PATHS):
 b=subprocess.run(['git','-C',str(a.reference_root),'show',COMMIT+':'+rel],check=True,capture_output=True).stdout
 blob=subprocess.run(['git','-C',str(a.reference_root),'rev-parse',COMMIT+':'+rel],check=True,capture_output=True,text=True).stdout.strip()
 assert hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest()==blob
 dest=ROOT/'content/raw/eevee-pokemon'/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 rows.append(dict(path=rel,byteLength=len(b),sha256=hashlib.sha256(b).hexdigest(),gitBlobSha1=blob))
lock=dict(schemaVersion=1,profile='firered-eevee-source-v1',repository='pret/pokefirered',commit=COMMIT,files=rows)
b=(json.dumps(lock,sort_keys=True,indent=2)+'\n').encode();(ROOT/'scripts/eevee-pokemon.lock.json').write_bytes(b);print(len(rows),len(b),hashlib.sha256(b).hexdigest())
