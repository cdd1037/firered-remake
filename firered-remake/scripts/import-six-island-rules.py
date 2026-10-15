#!/usr/bin/env python3
"""Import exact Six Island battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ('AROMA_LADY_ROSE','JUGGLER_EDWARD','SWIMMER_MALE_SAMIR','SWIMMER_FEMALE_DENISE','TWINS_MIU_MIA','HIKER_EARL','RUIN_MANIAC_STANLY','RUIN_MANIAC_FOSTER','RUIN_MANIAC_LARRY','HIKER_DARYL','POKEMANIAC_HECTOR')


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
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
    canonical_path = 'content/generated/canonical-pokemon/catalog.json'
    data = (dependency / canonical_path).read_bytes()
    canonical = json.loads(data)
    additions = ['SPECIES_SENTRET','SPECIES_FURRET','SPECIES_NATU','SPECIES_XATU','SPECIES_YANMA','SPECIES_WOBBUFFET','SPECIES_BELLOSSOM']
    opponent_additions = ['SPECIES_SUNKERN','SPECIES_SUNFLORA','SPECIES_CHINCHOU','SPECIES_LANTURN','SPECIES_SUDOWOODO']
    species = {s['id']['value'][5:]:s for s in canonical['species']}
    metadata = {s['id']['value'][5:]:s for s in canonical['metadata']}
    edges = []
    for name in additions+opponent_additions+['SPECIES_GLOOM']:
        original = species[name]
        for sr in [original['source'],original['learnsetSource'],metadata[name]['learnsetPointerSource'],metadata[name]['dexSource']]+[m['source'] for m in original['levelUpMoves']]+[e['source'] for e in metadata[name]['sourceEvolutions']]:
            read(sr['path'])
    for name,method,parameter,target in [('SPECIES_SENTRET','EVO_LEVEL',15,'SPECIES_FURRET'),('SPECIES_NATU','EVO_LEVEL',25,'SPECIES_XATU'),('SPECIES_GLOOM','EVO_ITEM',93,'SPECIES_BELLOSSOM')]:
        matches=[e for e in metadata[name]['sourceEvolutions'] if e['method']==method and e['parameterValue']==parameter and e['target']['value']=='pret:'+target]
        need(len(matches)==1,'Evolution edge: '+name)
        edges.append(dict(species=identity(name),sourceEvolution=matches[0]))
    battles=[]; actors=[]
    for name in ['SixIsland_WaterPath','SixIsland_RuinValley']:
        mapping=json.loads(read('data/maps/'+name+'/map.json'))
        for local_id,actor in enumerate(mapping['object_events'],1):
            if actor['trainer_type']!='TRAINER_TYPE_NORMAL': continue
            label=actor['script'];path='data/scripts/trainers.inc'
            block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
            command=re.search(r'^\t(trainerbattle_single|trainerbattle_double) ([^\n]+)$',block[1],re.M)
            need(command is not None,'First-battle command: '+label)
            args=command[2].split(', ');double=command[1]=='trainerbattle_double'
            need(args[0][8:] in TRAINERS,'Source actor trainer scope')
            post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
            battle=dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE'),
                rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',
                postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',
                callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None)
            if double:battle['notEnoughMonsText']=args[3]
            battles.append(battle)
            actors.append(dict(map=mapping['id'],localId=local_id,x=actor['x'],y=actor['y'],movementType=actor['movement_type'],sight=actor['trainer_sight_or_berry_tree_id'],script=label,trainer=identity(args[0])))
    need(len(trainers)==11 and len(battles)==12 and len(actors)==12,'11 trainer IDs/12 source actors')
    need(sum(t['doubleBattle'] for t in trainers)==1 and sum(b['mode']==4 for b in battles)==2,'Original Miu/Mia double shape')
    identities=[]
    for trainer,sets in zip(trainers,resolved):
        h=0;members=[]
        for member,moves in zip(trainer['party'],sets['party']):
            h=(h+sum(trainer['encodedTrainerName'])+sum(member['encodedSpeciesName']))&0xffffffff
            pid=((0x80 if trainer['doubleBattle'] else 0x78 if trainer['encounterMusicGender']&128 else 0x88)+(h<<8))&0xffffffff
            members.append(dict(species=member['species'],level=member['level'],fixedIv=member['sourceIv']*31//255,personality=pid,heldItem=member['heldItem'],moves=[identity(m) for m in moves['moves']]))
        identities.append(dict(trainer=trainer['id'],members=members,basePrize=(8 if trainer['doubleBattle'] else 4)*trainer['baseMoney']*trainer['party'][-1]['level']))
    for path in ['src/pokemon.c','src/battle_main.c','src/battle_setup.c','src/battle_script_commands.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h']:read(path)
    # Counts are recomputed from an actual authenticated predecessor snapshot, never inferred from scope prose.
    baseline_path=Path(__file__).with_name('six-island-predecessor.json')
    baseline=json.loads(baseline_path.read_text())
    owned=set(baseline['ownedSpecies']); opponents=set(baseline['opponentSpecies'])
    need(len(owned)==125 and len(opponents)==141 and owned<=opponents,'Authenticated predecessor scope')
    new_owned=owned|{'pret:'+s for s in additions};new_opponents=opponents|new_owned|{'pret:'+s for s in opponent_additions}
    need(len(new_owned)==132 and len(new_opponents)==153,'Deduplicated exact successor scope')
    docs={
      'trainers.json':dict(sourceCommit=COMMIT,profile='firered-six-island-trainers-v1',completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved),
      'pokemon.json':dict(sourceCommit=COMMIT,profile='firered-six-island-owned-pokemon-v1',ownedSpeciesAdded=additions,opponentSpeciesAdded=opponent_additions,evolutionEdges=edges),
      'rules.json':dict(sourceCommit=COMMIT,profile='firered-six-island-rules-v1',ownedSpeciesCount=len(new_owned),previousOwnedSpeciesCount=len(owned),opponentSpeciesCount=len(new_opponents),previousOpponentSpeciesCount=len(opponents),ownedSpeciesAdded=additions,opponentSpeciesAdded=opponent_additions,trainerSpecies=sorted({p['species']['value'] for t in trainers for p in t['party']}),trainerMoves=sorted({m for r in resolved for p in r['party'] for m in p['moves']}),identities=identities,trainerActors=actors,naturalLearnsets={s:species[s]['levelUpMoves'] for s in additions},limitations=['Source catalogs alone do not grant runtime route/capture/save/battle admission.','Miu/Mia is one double battle and shared defeat identity, exposed by two actor scripts.','Vs Seeker/rematch teams are not admitted.','Qwilfish is retained only in source Super Rod declarations; no new rod or ownership admission.','Sun Stone eligibility reads canonical Gloom evolution metadata without replacing its inherited species definition.','Dragon Scale held-item trade triggering and Dream Mail reading/writing/attachment remain explicitly unsupported.']),
      'source-input-hashes.json':dict(sourceCommit=COMMIT,files=[inputs[k] for k in sorted(inputs)],dependencyInputs=[dict(path=canonical_path)])}
    return docs

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference,a.dependency)
    files={'content/generated/six-island-rules/'+n:(json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode() for n,d in docs.items()}
    lock=dict(sourceCommit=COMMIT,profile='firered-six-island-rules-v1',files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=11,battles=12,owned=docs['rules.json']['ownedSpeciesCount'],opponents=docs['rules.json']['opponentSpeciesCount']),indent=2))
if __name__=='__main__':main()
