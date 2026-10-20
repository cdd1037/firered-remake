#!/usr/bin/env python3
"""Import the bounded Power Plant catalog from local raw files or pinned Git objects; --check is byte exact.
No reference repository code is executed. Includes the Thunder Stone ownership closure.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from game_pokemon_admissions import resolve_admissions

from item_source import ItemSource
from item_catalog import inventory_item, item_json
import argparse,  json, re
from pathlib import Path
import sys
from pokemon_import_io import safe, read_source, write_files
from machine_catalog import compatibility_additions, machine_definitions, admission_species

ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
SPECIES=['SPECIES_ELECTABUZZ','SPECIES_JOLTEON','SPECIES_ZAPDOS']
CATALOG_PATH='content/generated/data/pokemon/power-plant.json'
RAW='content/raw/pokefirered/037335f4/'
DEPENDENCIES=(
    'content/generated/data/pokemon/canonical/catalog.json',
    'data/game-pokemon.json',
)
from image_io import png as encode_rgba_png
from item_icon_exports import decode_icon_page

def encoded(v):return (json.dumps(v,separators=(',',':'),ensure_ascii=False, default=item_json)+'\n').encode()
def ident(s):return dict(value='pret:'+s)
def need(v,m):
 if not v:raise ValueError(m)
def build_power_plant_presentation(read_source_bytes,dependency_root):
 """Build source-derived art and manifests; retain raw bytes for materialization."""
 import io
 from PIL import Image
 from presentation_pixels import PixelReader

 BASE = 'content/generated/sprites/pokemon/power-plant/'
 SPECIES = [('Electabuzz', 125), ('Jolteon', 135), ('Zapdos', 145)]
 def encode(value):
     return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False, default=item_json) + '\n').encode()


 png = encode_rgba_png


 raw, inherited, out, assets = {}, {}, {}, []
 dependency_root = Path(dependency_root).absolute()

 def read(path):
     if path not in raw:
         data = read_source_bytes(path)
         need(isinstance(data, bytes), 'Source callback must return bytes: ' + path)
         raw[path] = data
     return raw[path]

 def text(path):
     return read(path).decode()

 def reuse(path):
     need(':' not in path, 'Unsafe inherited path')
     data = safe(dependency_root, path).read_bytes()
     inherited[path] = data
     return data

 def emit(identity, width, height, rgba, paths):
     need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
     out[identity + '.png'] = png(width, height, rgba)
     row = dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png')
     assets.append(row)
     return row

 class Source:
     def read(self, path):
         return read(path)
 reader = PixelReader(Source())
 def palette(path, floor=True):
     return reader.palette(path, floor)
 def indexed(path, pal, shape, floor=True, first_frame=False):
     return reader.indexed(path, pal, shape, floor=floor, first_frame=first_frame)

 sprites, icons, footprints, names = [], [], [], []
 graphics = text('src/data/graphics/pokemon.h')
 constants = text('include/constants/species.h')
 for name, species in SPECIES:
     stem, symbol = name.lower(), 'SPECIES_' + name.upper()
     need(re.search(r'^#define ' + symbol + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Species ID differs')
     elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
     elevation = int(elevation_match[1]) if elevation_match else 0
     for side in ('front', 'back'):
         coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
         match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', text(coordinates))
         need(match, 'Species coordinates missing')
         width, height, y_offset = map(int, match.groups())
         image = f'graphics/pokemon/{stem}/{side}.png'
         need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Species image binding differs')
         for variant in ('normal', 'shiny'):
             pal = f'graphics/pokemon/{stem}/{variant}.pal'
             palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
             need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Species palette binding differs')
             indices, rgba = indexed(image, pal, (64, 64))
             x, y = (144, 8 + y_offset - elevation) if side == 'front' else (40, 48 + y_offset)
             sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, renderSpeciesId=species, sourceFrameIndex=0, form=None,
                 side=side, paletteVariant=variant, x=x, y=y, sourceWidth=width, sourceHeight=height,
                 sourceYOffset=y_offset, sourceEnemyElevation=elevation,  sourceCoordinatePath=coordinates,
                 paletteExpansion='floor(rgb5 * 255 / 31)', transparentIndex=0,
                 **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
     icon_table = text('src/pokemon_icon.c')
     match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
     need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
     pal_index = int(match[1])
     image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
     need(f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Species icon image binding differs')
     indices, rgba = indexed(image, pal, (32, 64), first_frame=True)
     icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, paletteIndex=pal_index, sourceFrameIndex=0,
                       sourceFrameCount=2,  **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
     image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
     need('[' + symbol + '] = gMonFootprint_' + name + ',' in text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
     with Image.open(io.BytesIO(read(image))) as im:
         need(im.mode == '1' and im.size == (16, 16), 'Footprint source differs')
         indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
     colors = palette(pal)
     rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
     footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))
     display = re.search(r'\[' + symbol + r'\]\s*=\s*_\("([^"]+)"\)', text('src/data/text/species_names.h'))
     need(display, 'Species display name missing')
     names.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, name=display[1]))

 script_path = 'data/maps/PowerPlant/scripts.inc'
 script = text(script_path)
 zapdos = script.split('PowerPlant_EventScript_Zapdos::', 1)[1].split('\nPowerPlant_EventScript_DefeatedZapdos::', 1)[0]
 need('setwildbattle SPECIES_ZAPDOS, 50' in zapdos and 'playmoncry SPECIES_ZAPDOS, CRY_MODE_ENCOUNTER' in zapdos and
      'playbgm MUS_ENCOUNTER_GYM_LEADER, 0' in zapdos and 'special StartLegendaryBattle' in zapdos,
      'Zapdos encounter presentation source differs')
 for number in (1, 2):
     electrode = script.split(f'PowerPlant_EventScript_Electrode{number}::', 1)[1].split(f'\nPowerPlant_EventScript_FoughtElectrode{number}::', 1)[0]
     need('setwildbattle SPECIES_ELECTRODE, 34' in electrode and 'playmoncry SPECIES_ELECTRODE, CRY_MODE_ENCOUNTER' in electrode and
          '\n\tdowildbattle\n' in electrode and 'StartLegendaryBattle' not in electrode, 'Electrode encounter presentation source differs')
 need(json.loads(read('data/maps/PowerPlant/map.json'))['music'] == 'MUS_POKE_MANSION', 'Power Plant map music differs')
 setup_path = 'src/battle_setup.c'
 setup = text(setup_path)
 legendary = setup.split('void StartLegendaryBattle(void)', 1)[1].split('void StartGroudonKyogreBattle(void)', 1)[0]
 need(re.search(r'case SPECIES_ZAPDOS:\s*case SPECIES_HO_OH:\s*case SPECIES_LUGIA:\s*CreateBattleStartTask\(B_TRANSITION_BLUR, MUS_VS_LEGEND\);', legendary),
      'Zapdos source battle music binding differs')

 geometry_path = 'content/generated/battle/art/campaign-hit-art/manifest.json'
 geometry = json.loads(reuse(geometry_path))['geometry']
 geometry_rows = []
 for row in sprites:
     if row['paletteVariant'] != 'normal':
         continue
     expected = dict(species=row['sourceSpeciesId'], side=row['side'], sourceWidth=row['sourceWidth'], sourceHeight=row['sourceHeight'],
                     yOffset=row['sourceYOffset'], elevation=row['sourceEnemyElevation'] if row['side'] == 'front' else 0)
     need(expected in geometry, 'Inherited battle geometry differs')
     geometry_rows.append(expected)

 # Bag icons keep the earlier item importers' replicated-RGB5 palette convention.
 item_icons = []
 item_table, item_graphics = text('src/data/item_icon_table.h'), text('src/data/graphics/items.h')
 for symbol, item_id, inherited_png in [
     ('ITEM_THUNDER_STONE', 96, None),
     ('ITEM_TM17', 305, ('content/generated/data/items/mtmoon/mtmoon-items.json','pret:ITEM_TM05')),
     ('ITEM_TM25', 313, ('content/generated/ui/vermilion-gym-assets/assets.json','pret:ITEM_TM34'))]:
     need(re.search(r'^#define ' + symbol + r'\s+' + str(item_id) + r'\s*(?://[^\n]*)?$', text('include/constants/items.h'), re.M), 'Item ID differs')
     binding = re.search(r'^    \[' + symbol + r'\][ \t]*= \{(\w+), (\w+)\},', item_table, re.M)
     need(binding, 'Item icon binding missing')
     paths = []
     for sym in binding.groups():
         definition = re.search(r'^const u32 ' + sym + r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);', item_graphics, re.M)
         need(definition, 'Item graphics source missing')
         paths.append(definition[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
     image, pal = paths
     _, rgba = indexed(image, pal, (24, 24), floor=False)
     if inherited_png:
         descriptor,item = inherited_png
         document=json.loads(reuse(descriptor))
         icon_row=next(i for i in document.get('icons',[document.get('icon')]) if i['item']['value']==item)
         png_path=icon_row['pngPath'];png_bytes=reuse(png_path)
         im=decode_icon_page(png_bytes,icon_row)
         need(im.tobytes()==rgba,'Inherited TM icon differs')
     else:
         emitted = emit('items/' + symbol.lower(), 24, 24, rgba, paths)
         png_path = emitted['texturePath']
         png_bytes = out['items/' + symbol.lower() + '.png']
     item_icons.append(dict(item=dict(value='pret:' + symbol), sourceItemId=item_id, width=24, height=24, pngPath=png_path, inherited=bool(inherited_png)))
     if inherited_png and icon_row.get('region'):item_icons[-1]['region']=icon_row['region']

 manifest = dict(schemaVersion=1, assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, speciesNames=names, newOwnedSpecies=[s for _, s in SPECIES], itemIcons=item_icons, moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry_rows), audio=dict(   encounter=dict(sourcePath=script_path, battleMusicSourcePath=setup_path, cryMode=2, cryParameters=dict(length=140, release=225, pitch=15600, chorus=20, volume=90), zapdos=dict(species=145, level=50, cry='CRY_ZAPDOS_ENCOUNTER', introductionMusic='MUS_ENCOUNTER_GYM_LEADER', battleMusic='MUS_VS_LEGEND'), electrodes=dict(species=101, level=34, count=2, cry='CRY_ELECTRODE_ENCOUNTER', battleMusic='MUS_VS_WILD'), mapMusic='MUS_POKE_MANSION', playbackAuthority='existing committed presentation effects and typed battle context')), outputs=[dict(path=p) for p, b in sorted(out.items())])
 manifest_bytes = encode(manifest)
 out['manifest.json'] = manifest_bytes
 files = {BASE + p: b for p, b in out.items()}
 files.update({RAW + p: b for p, b in raw.items()})
 report = dict(passed=True, sourceFiles=len(raw), inheritedInputs=len(inherited), assets=len(assets), sprites=len(sprites),
     icons=len(icons), footprints=len(footprints), itemIcons=len(item_icons), inheritedItemIcons=2,
     geometry=len(geometry_rows), outputs=len(out) - 1,
      manifestBytes=len(manifest_bytes),

      nativeWindowVerified=False)
 return files, dict(manifest=manifest, report=report, itemIcons=item_icons)


def generate(reference,dependency,raw=None):
 raw_root=raw if raw is not None else ROOT/RAW
 raw={}
 def readbytes(path):
  if path not in raw:
   b=read_source(raw_root,path,commit=COMMIT,repository=reference);raw[path]=b
  return raw[path]
 shared_item = ItemSource(readbytes).definition
 def read(path):return readbytes(path).decode()
 def one(path,pattern):
  m=list(re.finditer(pattern,read(path),re.M|re.S));need(len(m)==1,'Unique source: '+pattern);return m[0]
 def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=read(path).count('\n',0,m.start())+1,endLine=read(path).count('\n',0,m.end())+1)
 def dep(path):
  b=safe(dependency,path).read_bytes()
  return json.loads(b)
 d={p:dep(p) for p in DEPENDENCIES};canonical=d['content/generated/data/pokemon/canonical/catalog.json']
 species={s['id']['value'][5:]:s for s in canonical['species']};metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
 owned={row['id']['value'][5:] for row in resolve_admissions(d['data/game-pokemon.json'])['owned']}
 need(set(SPECIES) <= owned, 'Power Plant species must be admitted by the current game')
 for s in SPECIES:
  need(not metadata[s]['sourceEvolutions'] and not species[s]['evolutions'],'Terminal added species')
 edges=[]
 for s,t in [('SPECIES_PIKACHU','SPECIES_RAICHU'),('SPECIES_EEVEE','SPECIES_JOLTEON')]:
  e=[e for e in metadata[s]['sourceEvolutions'] if e['method']=='EVO_ITEM' and e['parameter']=='ITEM_THUNDER_STONE']
  need(len(e)==1 and e[0]['parameterValue']==96 and e[0]['target']==ident(t),'Exact Thunder Stone edge');edges.append(dict(species=ident(s),sourceEvolution=e[0]))
 wildpath='src/data/wild_encounters.json';wild=json.loads(read(wildpath));entries=[e for g in wild['wild_encounter_groups'] for e in g.get('encounters',[]) if e['map']=='MAP_POWER_PLANT' and e['base_label'].endswith('_FireRed')]
 need(len(entries)==1 and set(entries[0])=={'map','base_label','land_mons'},'Only original FireRed land encounters')
 w=entries[0];weights=next(f['encounter_rates'] for g in wild['wild_encounter_groups'] for f in g.get('fields',[]) if f['type']=='land_mons')
 need(weights == [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1] and w['land_mons']['encounter_rate'] == 7, 'Original encounter weights/rate')
 land=dict(map=dict(value='map.power-plant'),rawEncounterRate=7,slots=[dict(index=i,species=ident(m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=weights[i]) for i,m in enumerate(w['land_mons']['mons'])])
 map_path='data/maps/PowerPlant/map.json';mapping=json.loads(read(map_path));scriptpath='data/maps/PowerPlant/scripts.inc';scripts=read(scriptpath);statics=[]
 for local,obj in enumerate(mapping['object_events'],1):
  block=re.search(r'^'+obj['script']+r'::\n((?:(?!^\w+::).)*)',scripts,re.M|re.S)
  if not block:continue
  m=re.search(r'^\tsetwildbattle (SPECIES_\w+), (\d+)\n',block[1],re.M)
  if m:
   kind=obj['script'].split('_')[-1];statics.append(dict(kind=kind[0].lower()+kind[1:],localId=local,species=ident(m[1]),level=int(m[2]),heldItem='ITEM_NONE',modernFatefulEncounter=False,legendary=kind=='Zapdos',source=ref(scriptpath,obj['script'],block)))
 need([(s['kind'],s['localId'],s['level']) for s in statics]==[('zapdos',6,50),('electrode2',7,34),('electrode1',8,34)],'Exactly three source static objects')
 for p in ['asm/macros/event.inc','src/pokemon.c','src/script_pokemon_util.c','src/scrcmd.c','src/battle_setup.c','src/battle_main.c','include/random.h','include/constants/pokemon.h','include/constants/species.h','include/constants/items.h','src/data/pokemon/tmhm_learnsets.h','src/data/items.json','src/data/pokemon/item_effects.h','include/constants/item_effects.h','src/move_descriptions.c']:read(p)
 need('CreateMon(&gEnemyParty[0], species, level, 32, 0, 0, OT_ID_PLAYER_ID, 0);' in read('src/script_pokemon_util.c'),'Source scripted generation')
 held=[]
 for s in sorted({r['species']['value'][5:] for r in land['slots']}|{'SPECIES_ELECTRODE','SPECIES_ZAPDOS'}):
  fields=metadata[s]
  held.append(dict(species=ident(s),common=fields['itemCommon'],rare=fields['itemRare']))
 need([(r['species']['value'],r['rare']) for r in held if r['rare']!='ITEM_NONE']==[('pret:SPECIES_MAGNETON','ITEM_MAGNET')],'FireRed held-item tables')
 cells=compatibility_additions(dependency, CATALOG_PATH, 'machines')
 itemtext=read('src/data/items.json');sourceitems=json.loads(itemtext)['items'];constants={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',read('include/constants/items.h'),re.M)}
 def item(symbol):
  i=next(i for i in sourceitems if i['itemId']==symbol);m=one('src/data/items.json',r'"itemId":\s*"'+symbol+r'"');r=ref('src/data/items.json',symbol,m)
  meta=shared_item(symbol)
  description=i['description_english'].replace('\\n','\n')
  if symbol.startswith('ITEM_TM'):
   p='src/move_descriptions.c';m=one(p,r'^const u8 gMoveDescription_'+i['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1])
  return dict(item=meta,description=description)
 items=[]
 for symbol in ['ITEM_THUNDER_STONE','ITEM_TM17','ITEM_TM25']:
  wrapped=item(symbol);meta=wrapped['item'];stone=symbol=='ITEM_THUNDER_STONE';effect=None
  if stone:
   p='src/data/pokemon/item_effects.h';m=one(p,r'^static const u8 sItemEffect_ThunderStone\[6\] = \{\n    \[4\] = ITEM4_EVO_STONE,\n\};');effect=ref(p,'sItemEffect_ThunderStone',m)
   need('#define ITEM4_EVO_STONE         0x80' in read('include/constants/item_effects.h'),'Stone effect byte')
  items.append(dict(item=meta) | dict(description=wrapped['description'], healHp=0, cureStatus=0, effectBytes=[0, 0, 0, 0, 128, 0] if stone else [], useSupport=9 if stone else 10))
 newmachines=machine_definitions(dependency, CATALOG_PATH)
 flags=read('include/constants/flags.h');ballscripts=read('data/scripts/item_ball_scripts.inc');pickups=[]
 def flagvalue(symbol):
  m=re.search(r'^#define\s+'+symbol+r'\s+([^\n]+)',flags,re.M);need(m,'Flag exists');expr=m[1].split('//')[0].strip()
  if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expr):return int(expr,0)
  a=re.fullmatch(r'\((FLAG_\w+)\s+\+\s+(\d+)\)',expr);need(a,'Flag expression');return flagvalue(a[1])+int(a[2])
 def pickup(obj,hidden,local,symbol,quantity):
  i=item(symbol)['item'];pickups.append(dict(id='power-plant:'+obj['flag'],mapSymbol='MAP_POWER_PLANT',hidden=hidden,sourceLocalId=local,x=obj['x'],y=obj['y'],elevation=obj['elevation'],script='EventScript_HiddenItemScript' if hidden else obj['script'],itemSymbol=symbol,sourceItemId=i['sourceItemId'],item=i['id'],itemName=i['name'],pocket=i['pocket'],quantity=quantity,flag=obj['flag'],sourceFlagId=flagvalue(obj['flag'])))
 for local,obj in enumerate(mapping['object_events'],1):
  m=re.search(r'^'+obj['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n',ballscripts,re.M)
  if m:pickup(obj,False,local,m[1],int(m[2] or 1))
 for obj in mapping['bg_events']:
  need(obj['type']=='hidden_item' and not obj['underfoot'],'Only ordinary hidden pickups');pickup(obj,True,None,obj['item'],obj['quantity'])
 need(len(pickups)==7 and sum(p['hidden'] for p in pickups)==2,'Five visible and two hidden pickups')
 vectors=[]
 for kind,s,level,seed in [('zapdos','SPECIES_ZAPDOS',50,42),('electrode1','SPECIES_ELECTRODE',34,0x12345678),('electrode2','SPECIES_ELECTRODE',34,0xDEADBEEF)]:
  initial=seed;draws=[]
  for _ in range(4):seed=(seed*1103515245+24691)&0xffffffff;draws.append(seed>>16)
  pid=draws[0]|draws[1]<<16;ivs=[draws[2]&31,(draws[2]>>5)&31,(draws[2]>>10)&31,draws[3]&31,(draws[3]>>5)&31,(draws[3]>>10)&31]
  vectors.append(dict(kind=kind,species=ident(s),level=level,initialSeed=initial,finalSeed=seed,rawDraws=draws,personality=pid,nature=pid%25,ivs=ivs,originalTrainerId=0x12345678))
 doc=dict(ownedSpeciesCount=152, opponentSpeciesCount=186, itemCount=123, machineCount=41, species=[dict(definition=dict(id=species[s]['id'], evolutions=species[s]['evolutions']), metadata=metadata[s]) for s in SPECIES], thunderStoneEvolutions=edges, land=land, heldItems=held, staticEncounters=statics, machines=cells, newMachines=newmachines, items=items, pickups=pickups, generationVectors=vectors, generationOrder='Static: player OT, random PID then IVs, explicit low16/high16 profile; original target-compiler Random32 operand ordering unverified. Ordinary land uses existing nature rejection generation. Legendary Zapdos skips held draw; ordinary land and Electrode consume one held draw at battle entry. No fateful metadata.')
 slots=',\n            '.join(f'new({r["index"]},new("{r["species"]["value"]}"),{r["minimumLevel"]},{r["maximumLevel"]},{r["weight"]})' for r in land['slots'])
 heldrows=',\n        '.join(f'new(new("{r["species"]["value"]}"),"{r["common"]}","{r["rare"]}")' for r in held)
 code='''using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\npublic sealed record PowerPlantHeldItemSource(SpeciesId Species, string Common, string Rare);\n/// <summary>Exact FireRed Power Plant table, generated only from pinned source.</summary>\npublic static class PowerPlantEncounterTables\n{\n'''+f'''    public static MapId Map {{ get; }} = new("map.power-plant");\n    public static EarlyLandEncounterTable Land {{ get; }} = new(Map, 7,\n        [{slots}]);\n    public static ImmutableArray<PowerPlantHeldItemSource> HeldItems {{ get; }} = [\n        {heldrows}];\n}}\n'''
 return doc,raw,{'src/Game.Domain/Encounters/PowerPlantEncounterTables.cs':code.encode()}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path,nargs='?',help='Optional pinned local Git repository');p.add_argument('--raw-root',type=Path,help='Local raw input directory when no reference repository is selected');p.add_argument('output',type=Path);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference is not None and a.raw_root is not None:p.error('Choose either REFERENCE or --raw-root')
 raw_root=a.raw_root if a.raw_root is not None else ROOT/RAW
 doc,raw,files=generate(a.reference,a.dependency_root,raw_root);files[CATALOG_PATH]=encoded(doc)
 presentation,art=build_power_plant_presentation(lambda path:read_source(raw_root,path,commit=COMMIT,repository=a.reference),a.dependency_root);files.update(presentation)
 for path,b in raw.items():files[RAW+path]=b
 if a.check:
  for path,b in files.items():need(safe(a.output,path).read_bytes()==b,'Regeneration differs: '+path)
 else:write_files(a.output,files)
 print(json.dumps(dict(checked=a.check,inputs=len(raw),presentation=art['report']),indent=2, default=item_json))
if __name__=='__main__':main()
