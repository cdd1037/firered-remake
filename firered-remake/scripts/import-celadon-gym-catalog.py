#!/usr/bin/env python3
"""Rebuild private Celadon Gym catalog additions from pinned local Git blobs or checked raw cache.
Uses authenticated existing project image codecs; never executes source repository programs.
"""
from item_catalog import item_definition as shared_item, inventory_item, item_json
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse,json,pathlib,re,sys
import pokemon_import as pokemon
from pokemon_import import learn_rows
from pokemon_import_io import safe, unique_pairs, load_recipe, read_sources
from image_io import indexed_png, png
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
import sys
sys.path.insert(0, str(ROOT / "scripts"))
from machine_catalog import load_catalog, machine_reference, compatibility_reference, admission_view, admission_species
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-celadon-gym-pokemon-v1'
NEW_SPECIES=('Vileplume','Victreebel','Exeggcute','Tangela')
ART_SPECIES=(('Vileplume',45),('Victreebel',71),('Exeggcute',102),('Tangela',114))
TEACHING_MOVES=('MOVE_GIGA_DRAIN',)
TRAINERS=('TRAINER_LASS_KAY','TRAINER_BEAUTY_BRIDGET','TRAINER_PICNICKER_TINA','TRAINER_BEAUTY_TAMIA','TRAINER_BEAUTY_LORI','TRAINER_LASS_LISA','TRAINER_COOLTRAINER_MARY','TRAINER_LEADER_ERIKA')
def source_symbol(name):return name.upper()
def source_stem(name):return name.lower()
def canonical(data):return (json.dumps(data,sort_keys=True,separators=(',',':'),ensure_ascii=False, default=item_json)+'\n').encode()


def read_json(path):return json.loads(safe(path).read_bytes(),object_pairs_hook=unique_pairs)

def trainer_parties(lock,sources):
    # Read original roster using the common source parser, with this exact inventory check.
    result=[]
    text={p:b.decode() for p,b in sources.items() if p.endswith(('.h','.inc'))}
    for symbol in TRAINERS:
        record=re.search(r'^    \['+symbol+r'\] = \{\n(.*?)^    \},',text['src/data/trainers.h'],re.M|re.S)
        shape,party_symbol=re.search(r'\.party = (NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',record[1]).groups()
        shape_type='TrainerMonNoItemCustomMoves' if shape=='NO_ITEM_CUSTOM_MOVES' else 'TrainerMonNoItemDefaultMoves'
        party=re.search(r'^static const struct '+shape_type+' '+party_symbol+r'\[\] = \{\n(.*?)^\};',text['src/data/trainer_parties.h'],re.M|re.S)
        rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{(MOVE_\w+(?:, MOVE_\w+){3})\},\n)?    \},\n',party[1]))
        if not rows or ''.join(m[0] for m in rows)!=party[1]:raise ValueError('Unknown trainer party shape')
        mons=[]
        for row in rows:
            level=int(row[2]);moves=[]
            if shape=='NO_ITEM_CUSTOM_MOVES':moves=[x for x in row[4].split(', ') if x!='MOVE_NONE']
            else:
                pointer=re.search(r'^    \['+row[3]+r'\] = (\w+),$',text['src/data/pokemon/level_up_learnset_pointers.h'],re.M)[1]
                learn=re.search(r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};',text['src/data/pokemon/level_up_learnsets.h'],re.M|re.S)
                for m in learn_rows(text['src/data/pokemon/level_up_learnsets.h'],learn):
                    if int(m[1])<=level and m[2] not in moves:moves=(moves+[m[2]])[-4:]
            mons.append(dict(iv=int(row[1]),level=level,species=row[3],moves=moves))
        result.append(dict(trainer=symbol,party=mons,trainerSource=pokemon.reference(lock,sources,'src/data/trainers.h',symbol,record[0]),partySource=pokemon.reference(lock,sources,'src/data/trainer_parties.h',party_symbol,party[0])))
    if len(result)!=8 or sum(len(t['party']) for t in result)!=21:raise ValueError('Celadon Gym roster differs')
    return result

def metadata(lock, sources):
    text = {p: b.decode('utf-8') for p, b in sources.items() if not p.endswith(('.png', '.wav'))}
    entries = {e['path']: e for e in lock['files']}
    def one(pattern, value):
        matches = list(re.finditer(pattern, value, re.M | re.S))
        if len(matches) != 1: raise ValueError('Expected exactly one source match: ' + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text[path].count('\n', 0, match.start()) + 1,
            endLine=text[path].count('\n', 0, match.end()) + 1)
    def block(path, symbol):
        match = one(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', text[path])
        fields = {}
        for line in match[1].splitlines():
            field = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not field or field[1] in fields: raise ValueError('Unexpected source field: ' + line)
            fields[field[1]] = field[2]
        return fields, ref(path, symbol, match)
    def ident(symbol): return dict(value='pret:' + symbol)
    def constant(path, symbol): return one(r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$', text[path])
    def number(path, symbol): return int(constant(path, symbol)[1])
    def name(path, symbol):
        m = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z ♀♂-]+)"\),$', text[path])
        return dict(id=ident(symbol), name=m[1], source=ref(path, symbol, m))
    species, names, deferred, move_symbols = [], [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for name_ in NEW_SPECIES:
        symbol = 'SPECIES_' + source_symbol(name_)
        fields, source = block('src/data/pokemon/species_info.h', symbol)
        if fields['genderRatio'] not in ('PERCENT_FEMALE(25)', 'PERCENT_FEMALE(50)', 'PERCENT_FEMALE(75)', 'MON_MALE', 'MON_FEMALE', 'MON_GENDERLESS') or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW', 'GROWTH_FAST', 'GROWTH_SLOW'):
            raise ValueError('Unsupported RockTunnel species profile')
        types = one(r'^\{(TYPE_\w+), (TYPE_\w+)\}$', fields['types'])
        abilities = one(r'^\{(ABILITY_\w+), (ABILITY_\w+)\}$', fields['abilities'])
        learnpath = 'src/data/pokemon/level_up_learnsets.h'
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', text['src/data/pokemon/level_up_learnset_pointers.h'])[1]
        learn = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', text[learnpath])
        moves = []
        for m in learn_rows(text[learnpath], learn):
            moves.append(dict(level=int(m[1]), move=ident(m[2])))
            move_symbols.add(m[2])
        evolution_path = 'src/data/pokemon/evolution.h'
        rows = list(re.finditer(r'^    \[' + symbol + r'\]\s*= (\{\{.*?\}\}),$', text[evolution_path], re.M | re.S))
        evolutions = []
        if len(rows) > 1: raise ValueError('Duplicate evolution row')
        m = rows[0] if rows else None
        declarations = re.findall(r'\{(EVO_\w+),\s+([A-Z_0-9]+), (SPECIES_\w+)\}', m[1]) if m else []
        if m and (not declarations or re.sub(r'\s+', '', m[1]) != '{' + ','.join('{' + ','.join(d) + '}' for d in declarations) + '}'):
            raise ValueError('Unsupported or incomplete evolution declarations')
        expected = [('EVO_ITEM', 'ITEM_LEAF_STONE', 'SPECIES_EXEGGUTOR')] if symbol == 'SPECIES_EXEGGCUTE' else []
        if declarations != expected: raise ValueError('Celadon Gym source evolution differs')
        for method, parameter, target in declarations:
            if method == 'EVO_LEVEL' and target in {'SPECIES_' + source_symbol(n) for n in NEW_SPECIES}:
                evolutions.append(dict(level=int(parameter), target=ident(target)))
            else:
                deferred.append(dict(species=ident(symbol), method=method, parameter=parameter, target=ident(target), executable=False, reason='Evolution target is not imported in this profile; retained source metadata, not a battle level cap.'))
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol), baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)}, type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']), evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}, growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow', 'GROWTH_FAST': 'fast', 'GROWTH_SLOW': 'slow'}[fields['growthRate']], genderRatio={'PERCENT_FEMALE(25)': 63, 'PERCENT_FEMALE(50)': 127, 'PERCENT_FEMALE(75)': 191, 'MON_MALE': 0, 'MON_FEMALE': 254, 'MON_GENDERLESS': 255}[fields['genderRatio']], ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']), levelUpMoves=moves, evolutions=evolutions))
        names.append(name('src/data/text/species_names.h', symbol))
    # Source trainer parties require definitions independently of these six complete learnsets.
    move_symbols.update(TEACHING_MOVES)
    move_symbols.update(m for t in trainer_parties(lock, sources) for mon in t['party'] for m in mon['moves'])
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(ident(symbol))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    abilities = sorted({s[k] for s in species for k in ('ability1', 'ability2')} - {'ABILITY_NONE'})
    for path, symbol in [('include/constants/pokemon.h', 'EVO_ITEM'), ('include/constants/items.h', 'ITEM_LEAF_STONE'), *[('include/constants/abilities.h', s) for s in abilities]]:
        m = constant(path, symbol)
        constants.append(dict(symbol=symbol, value=int(m[1]), source=ref(path, symbol, m)))
    encoding_path = 'charmap.txt'
    english = one(r'\A.*\Z', text[encoding_path])
    encoding_source = ref(encoding_path, 'English name encoding, gender glyphs, spaces and EOS', english)
    encoded_names = []
    for row in names + move_names:
        values = []
        for char in row['name'] + '$':
            m = one(r"^'" + re.escape(char) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$", text[encoding_path])
            values.append(int(m[1], 16))
        encoded_names.append(dict(symbol=row['source']['symbol'], name=row['name'], bytes=values,
            source=row['source'], encodingSource=encoding_source))
    evolution_path = 'src/data/pokemon/evolution.h'
    overrides = []
    for symbol, target in [('SPECIES_GLOOM', 'SPECIES_VILEPLUME'), ('SPECIES_WEEPINBELL', 'SPECIES_VICTREEBEL')]:
        m = one(r'^    \[' + symbol + r'\]\s*= (\{\{.*?\}\}),$', text[evolution_path])
        if ('EVO_ITEM, ITEM_LEAF_STONE, ' + target) not in m[1]: raise ValueError('Leaf Stone source target differs')
        overrides.append(dict(species=ident(symbol), evolution=dict(level=0, target=ident(target), method='item', parameter='ITEM_LEAF_STONE')))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'], species=[dict(id=row['id'], evolutions=row['evolutions']) for row in species],
        moves=moves, speciesNames=names, moveNames=move_names, constants=constants, encodedNames=encoded_names,
        deferredEvolutions=deferred, evolutionOverrides=overrides, naturalLevelLimit=None,
        itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False, tradeEvolutionExecutorImplemented=False,
        limitations=['Four new Celadon Gym species over the unchanged 94-species catalog; 98 species total.',
          'Full source learnsets, names, stats and evolutions are metadata; missing battle effects remain unavailable.',
          'Leaf Stone evolution targets are represented, but Leaf Stone and the department store are not admitted.',
          'Exeggcute to Exeggutor remains deferred because its target is not imported.',
          'No natural level cap, move backfill, cry playback or audible-output claim.'])

def item_outputs(lock,sources,project):
    text={p:b.decode() for p,b in sources.items() if not p.endswith('.png')};entries={e['path']:e for e in lock['files']}
    rules=[]
    def match(path,pattern):
        found=list(re.finditer(pattern,text[path],re.M|re.S))
        if len(found)!=1:raise ValueError('Nonunique source match '+path+' '+pattern)
        return found[0]
    def ref(path,symbol,m=None):return dict(path=path,symbol=symbol,startLine=1 if m is None else text[path].count('\n',0,m.start())+1,endLine=max(1,text.get(path,'').count('\n')) if m is None else text[path].count('\n',0,m.end())+1)
    def ident(s):return dict(value='pret:'+s)
    def constant(path,symbol):
        m=match(path,r'^#define '+symbol+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');rules.append(ref(path,symbol,m));return int(m[1])
    target='content/generated/celadon-gym-items/celadon-gym-machines.json'
    values=[admission_view(project,target,'/machines/'+str(index)) for index in range(15)]
    symbols=[species[5:] for species in admission_species(project,target,'/machines/0')]
    i=next(x for x in json.loads(text['src/data/items.json'])['items'] if x['itemId']=='ITEM_TM19');path='src/data/items.json'
    m=match(path,r'^    \{\n      "english": "TM19",.*?^    \}');source=ref(path,'ITEM_TM19',m)
    definition=shared_item('ITEM_TM19')
    path='src/move_descriptions.c';m=match(path,r'^const u8 gMoveDescription_GigaDrain\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
    wrapped=dict(item=definition,description=json.loads(m[1]))
    path='src/data/party_menu.h';m=match(path,r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};');mapping=re.findall(r'\bMOVE_\w+\b',m[1]);rules.append(ref(path,'sTMHMMoves',m))
    if len(mapping)!=58 or mapping[18]!='MOVE_GIGA_DRAIN':raise ValueError('TM19 source move mapping differs')
    path='src/data/battle_moves.h';m=match(path,r'^    \[MOVE_GIGA_DRAIN\] =\n    \{(.*?)^    \},');rules.append(ref(path,'MOVE_GIGA_DRAIN',m))
    if not re.search(r'\.pp = 5,',m[1]):raise ValueError('Giga Drain source PP differs')
    path='src/data/item_icon_table.h';m=match(path,r'^    \[ITEM_TM19\][ \t]*= \{(\w+), (\w+)\},');binding=ref(path,'ITEM_TM19',m);icon_symbols=m.groups();paths=[]
    for symbol in icon_symbols:
        path='src/data/graphics/items.h';m=match(path,r'^const u32 '+symbol+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(path,symbol,m));paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
    image,palette=paths
    w,h,indices,_=indexed_png(sources[image]);rgba=pokemon.rgba(indices,pokemon.icon_palette(sources[palette]));image_png=png(w,h,rgba)
    if (w,h)!=(24,24):raise ValueError('TM19 icon size differs')
    icon=dict(item=ident('ITEM_TM19'),width=w,height=h,pngPath='item_tm19.png',source=ref(image,icon_symbols[0]),paletteSource=ref(palette,icon_symbols[1]),bindingSource=binding)
    for path,symbol,pattern in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','RemoveBagItem',r'^bool8 RemoveBagItem\(.*?^\}'),('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}'),('src/pokemon.c','CanMonLearnTMHM',r'^u32 CanMonLearnTMHM\([^;]*?\n\{.*?^\}'),('data/maps/CeladonCity_Gym/scripts.inc','CeladonCity_Gym_EventScript_GiveTM19',r'^CeladonCity_Gym_EventScript_GiveTM19::.*?^\tend$')]:rules.append(ref(path,symbol,match(path,pattern)))
    limitations=['Exactly one TM19 item extends the unchanged 45-item predecessor; all five shop stocks remain unchanged.','Fifteen original machines have exactly 98 source compatibility rows; inherited14x94 records are identical.','TM19 is source Giga Drain, consumed once after successful teaching. No Leaf Stone or department-store stock is introduced.','Catalog metadata alone does not activate rewards, battle effects, field moves or application profiles.']
    return {'celadon-gym-items.json':canonical(dict(schemaVersion=1,profile='firered-celadon-gym-items-v1',sourceCommit=COMMIT,inputs=lock['files'],items=[dict(metadata=wrapped, effectBytes=[])],icons=[icon],limitations=limitations)),
      'celadon-gym-machines.json':canonical(dict(schemaVersion=1,profile='firered-celadon-gym-machines-v1',sourceCommit=COMMIT,inputs=lock['files'],machines=values,icons=[icon],limitations=limitations)),'item_tm19.png':image_png}


def load_sources(stem, raw, repo):
    lock = load_recipe(ROOT / ('scripts/' + stem + '.lock.json'),
                       commit=COMMIT, profile='firered-' + stem + '-v1')
    return lock, read_sources(lock, raw, repo)


def pokemon_outputs(lock,sources,project):
    catalog = metadata(lock, sources)
    outputs = pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Four Celadon Gym species, original front/back frame zero, normal and shiny palettes.')
    outputs['battle-art.json'] = canonical(json.loads(outputs['battle-art.json']))
    # Preserve the admitted predecessor order, then append this region's additions.
    previous = tuple(species[13:] for species in admission_species(project,
        'content/generated/celadon-gym-items/celadon-gym-machines.json', '/machines/0')
        if species[13:] not in {name.upper() for name in NEW_SPECIES})
    outputs['catalog.json'] = canonical(catalog)
    outputs.update(pokemon.supplement(lock, sources, profile=PROFILE, species=ART_SPECIES,
        weight_species=previous + tuple(name.upper() for name in NEW_SPECIES), natural_level_limit=None))
    declarations=[];text=sources['src/data/pokemon/species_info.h'].decode()
    for name in NEW_SPECIES:
        symbol='SPECIES_'+name.upper();m=re.search(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',text,re.M|re.S)
        declarations.append(dict(species={'value':'pret:'+symbol},fields={r[1]:r[2] for r in re.finditer(r'^\s*\.(\w+) = (.*),$',m[1],re.M)},declaration=m[0],source=pokemon.reference(lock,sources,'src/data/pokemon/species_info.h',symbol,m[0])))
    outputs['source-species.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,species=declarations))
    return outputs

def materialize(project,target,repo=None,check=False):
    project,target=safe(project),safe(target);results=[]
    for stem in ('celadon-gym-pokemon','celadon-gym-items'):
        raw=safe(target,'content/raw/pokefirered/037335f4');out=safe(target,'content/generated/'+stem)
        if repo is not None:
            repo=safe(repo)
            if any(p==repo or p in repo.parents or repo in p.parents for p in (raw,out)):raise ValueError('Output overlaps source repository')
        for kind in ('raw','generated'):
            for directory in (project/'content'/kind).iterdir():
                if directory.name==stem:continue
                if any(p==directory or p in directory.parents or directory in p.parents for p in (raw,out)):raise ValueError('Output overlaps protected predecessor')
        lock,sources=load_sources(stem,raw,repo)
        profile='firered-'+stem+'-v1'
        outputs=pokemon_outputs(lock,sources,project) if stem.endswith('pokemon') else item_outputs(lock,sources,project)
        manifest=canonical(dict(schemaVersion=1,profile=profile,sourceCommit=COMMIT,inputCount=len(sources),upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
        outputs['manifest.json']=manifest;raw_manifest=canonical(dict(schemaVersion=1,profile=profile,sourceCommit=COMMIT,inputCount=len(sources)))
        materials={(out,p):b for p,b in outputs.items()}
        if repo is not None:materials.update({(raw,p):b for p,b in sources.items()})
        if check:
            for (root,path),data in materials.items():
                if safe(root,path).read_bytes()!=data:raise ValueError('Materialized output differs: '+path)
        else:
            targets=[]
            for (root,path),data in materials.items():
                destination=safe(root,path);stage=safe(root,path+'.tmp')
                if any(p.exists() and not p.is_file() for p in (destination,stage)):raise ValueError('Nonregular output path')
                targets.append((destination,stage,data))
            for destination,stage,data in targets:destination.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(data);stage.replace(destination)
        results.append(dict(stem=stem,inputCount=len(sources),outputCount=len(outputs)))
    return results

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project-root',type=pathlib.Path,default=ROOT);parser.add_argument('--target-root',type=pathlib.Path,default=ROOT);parser.add_argument('--reference-repo',type=pathlib.Path);parser.add_argument('--check',action='store_true');args=parser.parse_args()
    print(json.dumps(materialize(args.project_root,args.target_root,args.reference_repo,args.check),sort_keys=True, default=item_json))
