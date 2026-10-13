#!/usr/bin/env python3
"""Import exact Four Island battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ('TEAM_ROCKET_GRUNT_45',)

def sha(data):
    return hashlib.sha256(data).hexdigest()

def fingerprint(value):
    return sha(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode())

def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
    texts, inputs, raw = {}, {}, {}
    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            inputs[path] = dict(path=path, byteLength=len(data), gitBlobSha1=hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest(), sha256=sha(data))
            raw[path] = data
            texts[path] = data.decode()
        return texts[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1, sha256=inputs[path]["sha256"])
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
    canonical_path = 'content/generated/canonical-pokemon/catalog.json'
    data = (dependency / canonical_path).read_bytes()
    need(sha(data) == 'c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc', 'Canonical dependency identity')
    canonical = json.loads(data)
    additions = ['SPECIES_SEEL','SPECIES_DEWGONG','SPECIES_WOOPER','SPECIES_QUAGSIRE','SPECIES_SWINUB','SPECIES_PILOSWINE','SPECIES_DELIBIRD']
    species = {s['id']['value'][5:]:s for s in canonical['species']}
    metadata = {s['id']['value'][5:]:s for s in canonical['metadata']}
    edges = []
    for name in additions:
        original = species[name]
        for source in [original['source'], original['learnsetSource'], metadata[name]['learnsetPointerSource']] + [m['source'] for m in original['levelUpMoves']]:
            read(source['path']); need(inputs[source['path']]['sha256'] == source['sha256'], 'Canonical source identity')
        raw_edges = metadata[name]['sourceEvolutions']
        expected = {'SPECIES_SEEL':(34,'SPECIES_DEWGONG'),'SPECIES_WOOPER':(20,'SPECIES_QUAGSIRE'),'SPECIES_SWINUB':(33,'SPECIES_PILOSWINE')}.get(name)
        if expected:
            level,target = expected
            need(len(raw_edges)==1 and raw_edges[0]['method']=='EVO_LEVEL' and raw_edges[0]['parameterValue']==level and raw_edges[0]['target']['value']=='pret:'+target, 'Evolution source')
            edges.append(dict(species=identity(name),sourceEvolution=raw_edges[0]))
        else: need(not raw_edges, 'Unexpected future evolution')
    path='data/maps/FourIsland_IcefallCave_Back/scripts.inc'
    label='FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene'
    block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
    battle_line=one(path,r'^\ttrainerbattle_no_intro (TRAINER_\w+), (\w+)$')
    mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT')
    tail=block[1].split(battle_line[0],1)[1]
    post=re.search(r'^\tmsgbox (\w+)',tail,re.M)[1]
    battle=dict(trainer=identity(battle_line[1]),mode=mode,rivalFlags=0,battleTypeFlags=8,scriptEntry=label,
        introText='',defeatText=battle_line[2],victoryText='',postBattleText=post,entryScript='EventScript_DoNoIntroTrainerBattle',
        callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None)
    trainer=trainers[0]
    need(mode==3 and trainer['sourceTrainerId']==539 and trainer['partyFlags']==0 and not trainer['doubleBattle'] and trainer['aiFlags']==1 and trainer['items']==['ITEM_NONE']*4, 'Exact Grunt45 declaration')
    need([(p['species']['value'],p['level'],p['sourceIv'],p['heldItem']) for p in trainer['party']]==[('pret:SPECIES_ZUBAT',38,0,'ITEM_NONE')]*2+[('pret:SPECIES_GOLBAT',38,0,'ITEM_NONE')], 'Grunt45 party')
    h=0; members=[]
    for p,moves in zip(trainer['party'],resolved[0]['party']):
        h=(h+sum(trainer['encodedTrainerName'])+sum(p['encodedSpeciesName']))&0xffffffff
        members.append(dict(species=p['species'],level=p['level'],fixedIv=0,personality=(0x88+(h<<8))&0xffffffff,
            heldItem=p['heldItem'],moves=[identity(m) for m in moves['moves']]))
    for path in ['src/pokemon.c','src/battle_setup.c','src/battle_script_commands.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h']:
        read(path)
    need(inputs['src/data/pokemon/evolution.h']['sha256']==edges[0]['sourceEvolution']['source']['sha256'],'Evolution hash')
    docs={
        'trainers.json':dict(sourceCommit=COMMIT,profile='firered-four-island-trainers-v1',completeSourceRoster=False,
            trainers=trainers,battles=[battle],resolvedMoves=resolved),
        'pokemon.json':dict(sourceCommit=COMMIT,profile='firered-four-island-owned-pokemon-v1',canonicalCatalogSha256=sha(data),
            ownedSpeciesAdded=additions,evolutionEdges=edges),
        'rules.json':dict(sourceCommit=COMMIT,profile='firered-four-island-rules-v1',ownedSpeciesCount=125,previousOwnedSpeciesCount=118,
            opponentSpeciesCount=141,previousOpponentSpeciesCount=135,ownedSpeciesAdded=additions,
            trainerSpecies=sorted({p['species']['value'] for p in trainer['party']}),
            trainerMoves=sorted({m for p in resolved[0]['party'] for m in p['moves']}),
            identities=[dict(trainer=trainer['id'],members=members,basePrize=4*trainer['baseMoney']*38)],
            naturalLearnsets={s:species[s]['levelUpMoves'] for s in additions},
            limitations=['Additive source catalogs do not grant campaign, combat, capture, storage, save or field admission.',
                'Only the single mode3 Grunt45 battle is admitted. Lorelei is a scene ally, not an opponent or double partner.']),
        'source-input-hashes.json':dict(sourceCommit=COMMIT,sourceTree='7515cd4067ea55f8cdbc73624478afd2736c58b9',
            files=[inputs[k] for k in sorted(inputs)],dependencyInputs=[dict(path=canonical_path,byteLength=len(data),sha256=sha(data))])}
    return docs

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','output']: p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    docs=generate(a.reference,a.dependency)
    files={'content/generated/four-island-rules/'+n:(json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode() for n,d in docs.items()}
    lock=dict(sourceCommit=COMMIT,sourceTree='7515cd4067ea55f8cdbc73624478afd2736c58b9',profile='firered-four-island-rules-v1',
        files=[dict(path=k,byteLength=len(v),sha256=sha(v)) for k,v in sorted(files.items())])
    files['scripts/four-island-rules.lock.json']=(json.dumps(lock,indent=2)+'\n').encode()
    for name,data in files.items():
        dest=a.output/name
        if a.check: need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else: dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=1,battles=1,owned=125,opponents=141,hashes={n:sha(v) for n,v in files.items()}),indent=2))
if __name__=='__main__': main()
