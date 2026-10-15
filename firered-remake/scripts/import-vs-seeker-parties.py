#!/usr/bin/env python3
"""Authenticate all original Vs Seeker parties through pinned inert Git blobs.

The existing reviewed Git reader and canonical species/move catalogs are dependencies.
No upstream executable runs and no substitute trainer, species, or move is permitted.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-vs-seeker-parties-v1'

def need(ok, message):
    if not ok: raise ValueError(message)

def compact(value): return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()
def output(value): return compact(value) + b'\n'
def identity(symbol): return {'value': 'pret:' + symbol}

def generate(reference, dependency, rules_root):
    helper = dependency/'scripts/import-cinnabar-source-preparation.py'
    spec = importlib.util.spec_from_file_location('vs_seeker_source_reader', helper)
    reader = importlib.util.module_from_spec(spec); spec.loader.exec_module(reader)
    src = reader.Source(reference)
    dependencies = []
    def dependent(root, path):
        raw = (root/path).read_bytes()
        dependencies.append(dict(path=path))
        return json.loads(raw)
    rules = dependent(rules_root, 'content/generated/vs-seeker/catalog.json')
    canonical = dependent(dependency, 'content/generated/canonical-pokemon/catalog.json')
    move_document = dependent(dependency, 'content/generated/metronome-moves/catalog.json')
    species = {x['id']['value'][5:]: x for x in canonical['species']}
    moves = {x['id']['value'][5:]: x for x in move_document['moves']}

    def one(path, pattern):
        found = list(re.finditer(pattern, src.text(path), re.M | re.S))
        need(len(found) == 1, 'Expected one source declaration: ' + pattern)
        return found[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=src.text(path).count('\n', 0, match.start()) + 1,
            endLine=src.text(path).count('\n', 0, match.end()) + 1)
    def number(path, symbol):
        value = one(path, r'^#define\s+' + re.escape(symbol) + r'\s+([^\n]+)')[1].split('//')[0].strip()
        if re.fullmatch(r'0x[0-9a-fA-F]+|\d+', value): return int(value, 0)
        shift = re.fullmatch(r'\(1 << (\d+)\)', value)
        need(shift, 'Unsupported source constant: ' + symbol)
        return 1 << int(shift[1])
    def encode(name):
        return [int(one('charmap.txt', "^'" + re.escape("\\'" if c == "'" else c) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16) for c in name]
    def fields(body):
        pairs = []
        for line in body.splitlines():
            m = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            need(m, 'Unhandled source field: ' + line); pairs.append(m.groups())
        need(len({p[0] for p in pairs}) == len(pairs), 'Duplicate source field')
        return dict(pairs)
    def natural(species_symbol, level):
        pointer = one('src/data/pokemon/level_up_learnset_pointers.h', r'^    \[' + species_symbol + r'\] = (\w+),$')[1]
        body = one('src/data/pokemon/level_up_learnsets.h', r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};')[1]
        # Original FireRed conditionals are preserved by selecting their named branch.
        body = re.sub(r'#if defined\(FIRERED\)\n(.*?)#elif defined\(LEAFGREEN\)\n.*?#endif', r'\1', body, flags=re.S)
        need('#' not in body, 'Unhandled conditional learnset: ' + species_symbol)
        result = []
        for lv, move in re.findall(r'LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)', body):
            if int(lv) <= level and move not in result: result = (result + [move])[-4:]
        projected = []
        for row in species[species_symbol]['levelUpMoves']:
            move = row['move']['value'][5:]
            if row['level'] <= level and move not in projected: projected = (projected + [move])[-4:]
        need(result == projected and 1 <= len(result) <= 4, 'Independent canonical/default move comparison: ' + species_symbol)
        return result

    trainers, resolved = [], []
    requirements = rules['rosterRequirements']
    need(len(requirements) == 351 and len({r['trainerSymbol'] for r in requirements}) == 351, 'Exact Vs Seeker roster scope')
    for requirement in requirements:
        symbol = requirement['trainerSymbol']; path = 'src/data/trainers.h'
        match = one(path, r'^    \[' + symbol + r'\] = \{\n(.*?)^    \},')
        f = fields(match[1]); need(set(f) == {'trainerClass', 'encounterMusic_gender', 'trainerPic', 'trainerName', 'items', 'doubleBattle', 'aiFlags', 'party'}, 'Trainer fields differ')
        shape, party_symbol = re.fullmatch(r'((?:NO_ITEM|ITEM)_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)', f['party']).groups()
        custom, held = 'CUSTOM' in shape, shape.startswith('ITEM_')
        need((shape, party_symbol, f['doubleBattle'] == 'TRUE', number('include/constants/opponents.h', symbol)) ==
             (requirement['partyMacro'], requirement['partySymbol'], requirement['doubleBattle'], requirement['sourceId']), 'Rules/party source identity differs')
        pp = 'src/data/trainer_parties.h'
        party = one(pp, r'^static const struct TrainerMon' + ('Item' if held else 'NoItem') + ('Custom' if custom else 'Default') + 'Moves ' + party_symbol + r'\[\] = \{\n(.*?)^\};')
        rows = list(re.finditer(r'    \{\n(.*?)    \},\n', party[1], re.S))
        need(''.join(r[0] for r in rows) == party[1] and 1 <= len(rows) <= 6, 'Unhandled trainer party syntax')
        members, expected = [], []
        for row in rows:
            mf = fields(row[1]); need(set(mf) == {'iv', 'lvl', 'species'} | ({'heldItem'} if held else set()) | ({'moves'} if custom else set()), 'Member fields differ')
            s, level = mf['species'], int(mf['lvl']); np = 'src/data/text/species_names.h'
            name = one(np, r'^    \[' + s + r'\] = _\("([^"\n]+)"\),$')
            natural_moves = natural(s, level)
            custom_moves = [m.strip() for m in mf['moves'].strip('{}').split(',')] if custom else []
            need(not custom or len(custom_moves) == 4, 'Four original custom slots required')
            actual_moves = custom_moves[:custom_moves.index('MOVE_NONE')] if 'MOVE_NONE' in custom_moves else custom_moves
            need(not custom or all(m == 'MOVE_NONE' for m in custom_moves[len(actual_moves):]), 'Nontrailing MOVE_NONE')
            source_id = number('include/constants/species.h', s)
            need(s in species and species[s]['sourceSpeciesId'] == source_id, 'Missing canonical species: ' + s)
            need(all(m in moves for m in (actual_moves if custom else natural_moves)), 'Missing canonical moves: ' + s)
            members.append(dict(species=identity(s), sourceSpeciesId=source_id, speciesName=name[1], encodedSpeciesName=encode(name[1]),
                level=level, sourceIv=int(mf['iv']), heldItem=mf.get('heldItem', 'ITEM_NONE'), customMoves=[identity(m) for m in custom_moves],
                source=ref(pp, party_symbol, party), speciesNameSource=ref(np, s, name)))
            expected.append([identity(m) for m in (actual_moves if custom else natural_moves)])
        name = re.fullmatch(r'_\("([^"\n]+)"\)', f['trainerName'])[1]
        items = [x.strip() for x in f['items'].strip('{}').split(',')] if f['items'] != '{}' else []
        need(len(items) <= 4, 'Trainer inventory size'); items += ['ITEM_NONE'] * (4 - len(items))
        money = one('src/battle_main.c', r'^\s*\{' + f['trainerClass'] + r', (\d+)\},$')
        ai = f['aiFlags'].split(' | ')
        trainer = dict(id=identity(symbol), sourceTrainerId=requirement['sourceId'], trainerClass=f['trainerClass'],
            sourceTrainerClass=number('include/constants/trainers.h', f['trainerClass']), trainerPic=f['trainerPic'],
            sourceTrainerPic=number('include/constants/trainers.h', f['trainerPic']), sourceTrainerName=name, encodedTrainerName=encode(name),
            encounterMusicGender=sum(number('include/constants/trainers.h', v) if not v.isdigit() else int(v) for v in f['encounterMusic_gender'].split(' | ')),
            items=items, doubleBattle=requirement['doubleBattle'], partyFlags=int(custom) | (int(held) << 1),
            aiFlags=sum(number('include/constants/battle_ai.h', a) for a in ai), aiFlagSymbols=ai, baseMoney=int(money[1]),
            party=members, source=ref(path, symbol, match), moneySource=ref('src/battle_main.c', f['trainerClass'], money))
        trainers.append(trainer); resolved.append(dict(trainer=identity(symbol), partyMoves=expected))
    trainers.sort(key=lambda t: t['sourceTrainerId']); resolved.sort(key=lambda r: next(t['sourceTrainerId'] for t in trainers if t['id'] == r['trainer']))
    # Authenticate the actual actor-script branch, including its rematch label and exact line.
    script_path = 'data/scripts/trainers.inc'; script_text = src.text(script_path)
    blocks = {m[1]: m for m in re.finditer(r'^(\w+)::\n((?:(?!^\w+::).)*)', script_text, re.M | re.S)}
    occurrences = []
    modes = {'trainerbattle_single': 'TRAINER_BATTLE_SINGLE', 'trainerbattle_double': 'TRAINER_BATTLE_DOUBLE',
             'trainerbattle_rematch': 'TRAINER_BATTLE_REMATCH', 'trainerbattle_rematch_double': 'TRAINER_BATTLE_REMATCH_DOUBLE'}
    macros = src.text('asm/macros/event.inc')
    for opcode, mode in modes.items():
        macro = re.search(r'^\s*\.macro ' + opcode + r'\b(.*?)^\s*\.endm', macros, re.M | re.S)
        need(macro and mode in macro[1], 'Trainer battle macro mode differs: ' + opcode)
    for actor in rules['actorBindings']:
        root = actor['scriptLabel']; need(root in blocks, 'Missing original actor script: ' + root)
        pending, seen, found = [root], set(), []
        while pending:
            label = pending.pop()
            if label in seen: continue
            seen.add(label); block = blocks[label]
            for match in re.finditer(r'^\t(trainerbattle_\w+) ([^\n]+)$', block[2], re.M):
                opcode = match[1]; args = [x.strip() for x in match[2].split(',')]
                need(opcode in modes and len(args) == (4 if 'double' in opcode else 3) and args[0] == actor['trainerSymbol'], 'Actor battle occurrence differs: ' + root)
                offset = block.start(2) + match.start()
                line = script_text.count('\n', 0, offset) + 1
                found.append(dict(actorScript=root, scriptEntry=label, opcode=opcode, arguments=args, trainer=identity(args[0]),
                    isRematch='rematch' in opcode, mode=number('include/constants/battle_setup.h', modes[opcode]),
                    source=dict(path=script_path, symbol=label, startLine=line, endLine=line)))
            pending += [x for x in re.findall(r'\b\w+\b', block[2]) if x in blocks and x not in seen]
        need(len(found) == 2 and sum(o['isRematch'] for o in found) == 1, 'Exactly one original/rematch branch per actor: ' + root)
        occurrences += found
    occurrences.sort(key=lambda x: (x['actorScript'], x['isRematch']))
    document = dict(sourceCommit=COMMIT, profile=PROFILE, trainers=trainers, resolvedMoves=resolved)
    occurrence_document = dict(sourceCommit=COMMIT, profile=PROFILE, occurrences=occurrences)
    lock = dict(schemaVersion=1, repository='pret/pokefirered', commit=COMMIT,  profile=PROFILE,
        reader=dict(path='scripts/import-cinnabar-source-preparation.py'), dependencies=dependencies,
        files=[src.entries[p] for p in sorted(src.entries)])
    summary = dict(trainers=len(trainers), singles=sum(not t['doubleBattle'] for t in trainers), doubles=sum(t['doubleBattle'] for t in trainers),
        partyMembers=sum(len(t['party']) for t in trainers), sourceSpecies=sorted({m['species']['value'] for t in trainers for m in t['party']}),
        moveCount=len({m['value'] for row in resolved for party in row['partyMoves'] for m in party}),
        partyFlags=sorted({t['partyFlags'] for t in trainers}), aiFlags=sorted({t['aiFlags'] for t in trainers}),
        heldItems=sorted({m['heldItem'] for t in trainers for m in t['party']}),
        missingCanonicalSpecies=[], missingCanonicalMoves=[], upstreamScriptsExecuted=False)
    return {'catalog.json': output(document), 'battle-occurrences.json': output(occurrence_document), 'manifest.json': output(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
           summary=summary))}, src

def main():
    p = argparse.ArgumentParser(); p.add_argument('--source-root', type=Path, required=True); p.add_argument('--dependency-root', type=Path, required=True)
    p.add_argument('--rules-root', type=Path, required=True); p.add_argument('--output-root', type=Path, default=Path(__file__).resolve().parents[1]); p.add_argument('--check', action='store_true')
    a = p.parse_args(); artifacts, src = generate(a.source_root, a.dependency_root, a.rules_root)
    for name, data in artifacts.items():
        path = a.output_root/'content/generated/vs-seeker-parties'/name
        if a.check: need(path.read_bytes() == data, 'Generated artifact differs: ' + str(path))
        else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    for name, raw in src.data.items():
        path = a.output_root/'content/raw/pokefirered/037335f4'/name
        if a.check: need(path.read_bytes() == raw, 'Raw source differs: ' + name)
        else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
    print(artifacts['manifest.json'].decode().strip())

if __name__ == '__main__': main()
