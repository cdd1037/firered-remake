#!/usr/bin/env python3
"""Import exact Sevii Ruby battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY REGION OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
import argparse
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ('BLACK_BELT_HUGH', 'BLACK_BELT_SHEA', 'CAMPER_BRYCE', 'CRUSH_GIRL_JOCELYN', 'CRUSH_GIRL_SHARON', 'CRUSH_GIRL_TANYA', 'CRUSH_KIN_MIK_KIA', 'FISHERMAN_TOMMY', 'PICNICKER_CLAIRE', 'PKMN_RANGER_BETH', 'PKMN_RANGER_LOGAN', 'SWIMMER_FEMALE_ABIGAIL', 'SWIMMER_FEMALE_MARIA', 'SWIMMER_MALE_FINN', 'SWIMMER_MALE_GARRETT', 'TEAM_ROCKET_GRUNT_43', 'TEAM_ROCKET_GRUNT_44')


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency, region):
    texts, inputs, raw = {}, {}, {}
    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            inputs[path] = dict(path=path)
            raw[path] = data
            texts[path] = data.decode()
        return texts[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1)
    def number(path, symbol):
        value = one(path, r"^#define " + symbol + r"\s+([^\n]+)")[1].split("//")[0].strip()
        return int(value, 0) if re.fullmatch(r"(?:0x[0-9a-fA-F]+|\d+)", value) else 1 << int(re.fullmatch(r"\(1 << (\d+)\)", value)[1])
    def encode(name):
        return [int(one("charmap.txt", "^'" + re.escape(c) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16) for c in name]
    def identity(symbol):
        return dict(value="pret:" + symbol)
    def ai_flags(symbols):
        return sum(1 << int(one("include/constants/battle_ai.h", r"^#define " + s + r"\s+\(1 << (\d+)\)[^\n]*")[1]) for s in symbols)
    def natural_moves(species, level):
        pointer = one("src/data/pokemon/level_up_learnset_pointers.h", r"^    \[" + species + r"\] = (\w+),$")[1]
        body = one("src/data/pokemon/level_up_learnsets.h", r"^static const u16 " + pointer + r"\[\] = \{\n(.*?)^\};")[1]
        moves = []
        for lv, move in re.findall(r"LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)", body):
            if int(lv) <= level and move not in moves:
                moves = (moves + [move])[-4:]
        need(1 <= len(moves) <= 4, "Expected source natural moves")
        return moves

    trainers, resolved = [], []
    for short in TRAINERS:
        symbol, path = "TRAINER_" + short, "src/data/trainers.h"
        match = one(path, r"^    \[" + symbol + r"\] = \{\n(.*?)^    \},")
        fields = dict(re.findall(r"^        \.(\w+) = (.*),$", match[1], re.M))
        shape, party_symbol = re.fullmatch(r"((?:NO_ITEM|ITEM)_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)", fields["party"]).groups()
        custom, held = "CUSTOM" in shape, shape.startswith("ITEM_")
        flags = int(custom) | (int(held) << 1)
        party_path = "src/data/trainer_parties.h"
        party = one(party_path, r"^static const struct TrainerMon" + ("Item" if held else "NoItem") + ("Custom" if custom else "Default") + "Moves " + party_symbol + r"\[\] = \{\n(.*?)^\};")
        rows = list(re.finditer(r"    \{\n(.*?)    \},\n", party[1], re.S))
        need("".join(r[0] for r in rows) == party[1], "Unhandled trainer party syntax")
        members, movesets = [], []
        for row in rows:
            mf = dict(re.findall(r"^        \.(\w+) = (.*),$", row[1], re.M))
            need(set(mf) == {"iv", "lvl", "species"} | ({"heldItem"} if held else set()) | ({"moves"} if custom else set()), "Unexpected member fields")
            species, level = mf["species"], int(mf["lvl"])
            names_path = "src/data/text/species_names.h"
            name = one(names_path, r'^    \[' + species + r'\] = _\("([^"]+)"\),$')
            moves = mf["moves"].strip("{}").split(", ") if custom else natural_moves(species, level)
            members.append(dict(species=identity(species), sourceSpeciesId=number("include/constants/species.h", species),
                speciesName=name[1], encodedSpeciesName=encode(name[1]), level=level, sourceIv=int(mf["iv"]),
                heldItem=mf.get("heldItem", "ITEM_NONE"), customMoves=[identity(m) for m in moves] if custom else [],
                source=ref(party_path, party_symbol, party), speciesNameSource=ref(names_path, species, name)))
            movesets.append(dict(species=identity(species), level=level, moves=moves))
        name = re.fullmatch(r'_\("([^"]+)"\)', fields["trainerName"])[1]
        items = fields["items"].strip("{}").split(", ") if fields["items"] != "{}" else []
        items += ["ITEM_NONE"] * (4 - len(items))
        money = one("src/battle_main.c", r"^\s*\{" + fields["trainerClass"] + r", (\d+)\},$")
        ai = fields["aiFlags"].split(" | ")
        trainers.append(dict(id=identity(symbol), sourceTrainerId=number("include/constants/opponents.h", symbol),
            trainerClass=fields["trainerClass"], sourceTrainerClass=number("include/constants/trainers.h", fields["trainerClass"]),
            trainerPic=fields["trainerPic"], sourceTrainerPic=number("include/constants/trainers.h", fields["trainerPic"]),
            sourceTrainerName=name, encodedTrainerName=encode(name),
            encounterMusicGender=sum(number("include/constants/trainers.h", v) if not v.isdigit() else int(v) for v in fields["encounterMusic_gender"].split(" | ")),
            items=items, doubleBattle=fields["doubleBattle"] == "TRUE", partyFlags=flags,
            aiFlags=ai_flags(ai), aiFlagSymbols=ai, baseMoney=int(money[1]), party=members,
            source=ref(path, symbol, match), moneySource=ref("src/battle_main.c", fields["trainerClass"], money)))
        resolved.append(dict(trainer=symbol, party=movesets))
    dependencies = []
    def dependent(root, path):
        data = (root / path).read_bytes()
        dependencies.append(dict(path=path))
        return json.loads(data)
    canonical = dependent(dependency, 'content/generated/data/pokemon/canonical/catalog.json')
    # Reuse the region's original extractor in memory; no source snapshot is published.
    import importlib.util
    def importer(name, path):
        spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
    ruby=importer('ruby_rule_region',(_PROJECT_ROOT / 'scripts/imports/maps/import-sevii-ruby-region.py'))
    reader=ruby.module((_PROJECT_ROOT / 'scripts/imports/source/import-cinnabar-source-preparation.py'),'ruby_rule_source')
    prep=ruby.module((_PROJECT_ROOT / 'scripts/lib/prepare_first_sevii.py'),'ruby_rule_scripts')
    prep.CORE=ruby.CORE;prep.SUPPORT=ruby.SUPPORT;prep.ANCHOR=['OneIsland_PokemonCenter_1F']
    source=reader.Source(reference)
    worlds=[dict(events=source.json('data/maps/'+name+'/map.json')) for name in ruby.CORE+ruby.SUPPORT]
    declarations=json.loads(json.dumps(ruby.declarations(source,prep.script_index(source,reader),worlds,canonical),sort_keys=True))
    species = {s['id']['value'][5:]: s for s in canonical['species']}
    battles = []
    for occurrence in declarations['battleOccurrences']:
        if occurrence['rematch']: continue
        path, label = occurrence['sourceRef']['path'], occurrence['script']
        block = one(path, r'^' + label + r'::\n((?:(?!^\w+::).)*)')
        args = occurrence['arguments']; opcode = occurrence['opcode']
        one(path, r'^\t' + opcode + ' ' + re.escape(', '.join(args)) + '$')
        double = opcode == 'trainerbattle_double'; no_intro = opcode == 'trainerbattle_no_intro'
        need(opcode in ['trainerbattle_single', 'trainerbattle_double', 'trainerbattle_no_intro'], 'Unexpected battle opcode')
        post = re.search(r'^\tmsgbox (\w+)', block[1], re.M)
        mode = 'TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if no_intro else 'TRAINER_BATTLE_SINGLE'
        battle = dict(trainer=identity(args[0]), mode=number('include/constants/battle_setup.h', mode),
            rivalFlags=0, battleTypeFlags=9 if double else 8, scriptEntry=label,
            introText='' if no_intro else args[1], defeatText=args[1] if no_intro else args[2], victoryText='',
            postBattleText=post[1] if post else '', entryScript='EventScript_TryDoDoubleTrainerBattle' if double else
            'EventScript_DoNoIntroTrainerBattle' if no_intro else 'EventScript_TryDoNormalTrainerBattle',
            callback='CB2_EndTrainerBattle', source=ref(path,label,block), postBattleScript=None, musicOption=None)
        if double: battle['notEnoughMonsText'] = args[3]
        battles.append(battle)
    need(len(trainers) == 17 and len(battles) == 18, 'Expected 17 trainers and 18 first-battle scripts')
    need(sum(t['doubleBattle'] for t in trainers)==1 and sum(b['mode']==4 for b in battles)==2, 'Mik/Kia double scope')
    held = [t for t in trainers if any(m['heldItem'] != 'ITEM_NONE' for m in t['party'])]
    need(len(held)==6 and all(t['partyFlags']==2 and all(m['heldItem']=='ITEM_BLACK_BELT' for m in t['party']) for t in held), 'Exact Black Belt teams')
    for t in trainers:
        original=next(x for x in declarations['trainers'] if 'pret:'+x['trainer']==t['id']['value'])
        sets=next(x['party'] for x in resolved if 'pret:'+x['trainer']==t['id']['value'])
        need(len(t['party'])==len(original['party']), 'Region party length')
        for m, moves, source in zip(t['party'], sets, original['party']):
            need(m['species']['value']=='pret:'+source['species'] and m['level']==source['level'] and
                m['sourceIv']==source['sourceIv'] and m['heldItem']==source['heldItem'] and
                moves['moves']==source['resolvedMoves'] and bool(m['customMoves'])==source['customMoves'], 'Region trainer projection')
    # Canonical mechanics already hold every original species and all 354 moves.
    # Reuse those records and only disclose the two newly reachable level evolution edges.
    additions=['SPECIES_RAPIDASH','SPECIES_SLUGMA','SPECIES_MAGCARGO']
    metadata={m['id']['value'][5:]:m for m in declarations['pokemonMetadata']}
    edges=[]
    for name,level,target in [('SPECIES_PONYTA',40,'SPECIES_RAPIDASH'),('SPECIES_SLUGMA',38,'SPECIES_MAGCARGO')]:
        e=metadata[name]['sourceEvolutions'];need(len(e)==1 and e[0]['method']=='EVO_LEVEL' and e[0]['parameterValue']==level and e[0]['target']['value']=='pret:'+target,'Evolution edge')
        edges.append(dict(species=identity(name),sourceEvolution=e[0]))
    identities=[]
    for t in trainers:
        h=0; members=[]; defaults=next(r['party'] for r in resolved if 'pret:'+r['trainer']==t['id']['value'])
        for p,moves in zip(t['party'],defaults):
            h=(h+sum(t['encodedTrainerName'])+sum(p['encodedSpeciesName']))&0xffffffff
            pid=((0x80 if t['doubleBattle'] else 0x78 if t['encounterMusicGender']&128 else 0x88)+(h<<8))&0xffffffff
            members.append(dict(species=p['species'],level=p['level'],fixedIv=p['sourceIv']*31//255,personality=pid,
                heldItem=p['heldItem'],moves=[identity(m) for m in moves['moves']]))
        identities.append(dict(trainer=t['id'],members=members,basePrize=(8 if t['doubleBattle'] else 4)*t['baseMoney']*t['party'][-1]['level']))
    for path in ['src/pokemon.c','src/battle_main.c','src/battle_setup.c','src/battle_script_commands.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h']:
        read(path)
    rules=dict(ownedSpeciesCount=118,previousOwnedSpeciesCount=115,
        opponentSpeciesCount=135,previousOpponentSpeciesCount=133,ownedSpeciesAdded=additions,
        trainerSpecies=sorted({m['species']['value'] for t in trainers for m in t['party']}),
        trainerMoves=sorted({m for t in resolved for p in t['party'] for m in p['moves']}),identities=identities,
        battleOccurrences=declarations['battleOccurrences'],heldBlackBeltTeams=[t['id'] for t in held])
    docs={'trainers.json':dict(completeSourceRoster=False,
          trainers=trainers,battles=battles,resolvedMoves=resolved),
        'pokemon.json':dict(
          ownedSpeciesAdded=additions,evolutionEdges=edges),
        'rules.json':rules}
    return docs,raw

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','region','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    docs,raw=generate(a.reference,a.dependency,a.region)
    files={'content/generated/data/regions/sevii-ruby/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else (json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode()) for n,d in docs.items()}
    files.update({'content/raw/pokefirered/037335f4/'+n:d for n,d in raw.items()})
    lock=dict(sourceCommit=COMMIT,profile='firered-sevii-ruby-rules-v1',
        files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=17,members=sum(len(t['party']) for t in docs['trainers.json']['trainers']),
        battles=18,moves=len(docs['rules.json']['trainerMoves']),inputs=len(raw)),indent=2))
if __name__=='__main__':main()
