#!/usr/bin/env python3
"""Import the pinned Old Man tutorial's inert source data. Never build/run the upstream tree.
The lock and expected generated hash are reviewed constants; this importer cannot update either.
"""
import argparse
import hashlib
import json
import os
import pathlib
import re
import struct
import subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-old-man-tutorial-v1'
LOCK_SHA256 = '29e5547cef4583511bee5916e4f35d8889320b3ecc4919304f1887c656e415ba'
GENERATED_SHA256 = 'e2114f5049580c8e19a4ec101d6bd523adb1056d665f3cda4a9d8534d112b80f'


def sha(b):
    return hashlib.sha256(b).hexdigest()


def import_content(repo, raw, out):
    lock_bytes = (ROOT / 'scripts/old-man-tutorial.lock.json').read_bytes()
    if sha(lock_bytes) != LOCK_SHA256:
        raise ValueError('Old Man source lock changed; independent review required')
    lock = json.loads(lock_bytes)
    if lock['commit'] != COMMIT or lock['profile'] != PROFILE:
        raise ValueError('Unexpected source profile')
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        data = subprocess.check_output(['git', '-C', str(repo), 'show', COMMIT + ':' + path])
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        if len(data) != entry['bytes'] or sha(data) != entry['sha256'] or blob != entry['gitBlobSha1']:
            raise ValueError('Source bytes differ: ' + path)
        verified[path] = data
    sources = {p: b.decode('utf-8') for p, b in verified.items() if not p.endswith('.png')}
    meta = {e['path']: e for e in lock['files']}

    def one(pattern, value):
        matches = list(re.finditer(pattern, value, re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected exactly one source match: ' + pattern)
        return matches[0]

    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=sources[path].count('\n', 0, match.start()) + 1,
                    endLine=sources[path].count('\n', 0, match.end()) + 1, sha256=meta[path]['sha256'])

    def block(path, symbol):
        match = one(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', sources[path])
        fields = {}
        for line in match[1].splitlines():
            field = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not field or field[1] in fields:
                raise ValueError('Unexpected source field: ' + line)
            fields[field[1]] = field[2]
        return fields, ref(path, symbol, match)

    def ident(symbol):
        return dict(value='pret:' + symbol)

    def number(path, symbol):
        return int(one(r'^#define ' + symbol + r'\s+(\d+)\s*$', sources[path])[1])

    def name(path, symbol):
        m = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z -]+)"\),$', sources[path])
        return dict(id=ident(symbol), name=m[1], source=ref(path, symbol, m))

    species, species_names, move_symbols = [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for species_symbol in ['SPECIES_WEEDLE', 'SPECIES_KAKUNA', 'SPECIES_BEEDRILL']:
        f, species_ref = block('src/data/pokemon/species_info.h', species_symbol)
        if f['genderRatio'] != 'PERCENT_FEMALE(50)' or f['growthRate'] != 'GROWTH_MEDIUM_FAST':
            raise ValueError('Unsupported Weedle-family profile')
        if species_symbol == 'SPECIES_WEEDLE' and (f['itemCommon'] != 'ITEM_NONE' or f['itemRare'] != 'ITEM_NONE'):
            raise ValueError('Weedle must draw held-item RNG with two NONE source items')
        base = {k: int(f['base' + s]) for k, s in zip(stat_keys, stat_fields)}
        yields = {k: int(f['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}
        types = one(r'^\{(TYPE_\w+), (TYPE_\w+)\}$', f['types'])
        abilities = one(r'^\{(ABILITY_\w+), (ABILITY_\w+)\}$', f['abilities'])
        learnpath = 'src/data/pokemon/level_up_learnsets.h'
        pointer = one(r'^    \[' + species_symbol + r'\] = (\w+),$', sources['src/data/pokemon/level_up_learnset_pointers.h'])[1]
        learn = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', sources[learnpath])
        entries = []
        for m in re.finditer(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', sources[learnpath]):
            if learn.start() < m.start() < learn.end():
                entries.append(dict(level=int(m[1]), move=ident(m[2]), source=ref(learnpath, pointer, m)))
                move_symbols.add(m[2])
        if not entries or not learn[1].strip().endswith('LEVEL_UP_END'):
            raise ValueError('Missing complete learnset')
        evolutions = []
        for m in re.finditer(r'^    \[' + species_symbol + r'\]\s*= \{\{EVO_LEVEL,\s+(\d+), (SPECIES_\w+)\}\},$', sources['src/data/pokemon/evolution.h'], re.M):
            if m[2] not in ['SPECIES_KAKUNA', 'SPECIES_BEEDRILL']:
                raise ValueError('Evolution target beyond complete family')
            evolutions.append(dict(level=int(m[1]), target=ident(m[2]), source=ref('src/data/pokemon/evolution.h', species_symbol, m)))
        species.append(dict(id=ident(species_symbol), sourceSpeciesId=number('include/constants/species.h', species_symbol),
                       baseStats=base, type1=types[1], type2=types[2], catchRate=int(f['catchRate']),
                       experienceYield=int(f['expYield']), evYield=yields, growthRate='mediumFast', genderRatio=127,
                       ability1=abilities[1], ability2=abilities[2], friendship=int(f['friendship']), levelUpMoves=entries,
                       evolutions=evolutions, source=species_ref, learnsetSource=ref(learnpath, pointer, learn)))
        species_names.append(name('src/data/text/species_names.h', species_symbol))
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(dict(id=ident(symbol), sourceMoveId=number('include/constants/moves.h', symbol),
                          pp=int(f['pp']), power=int(f['power']), type=f['type'], accuracy=int(f['accuracy']),
                          effect=f['effect'], secondaryEffectChance=int(f['secondaryEffectChance']), target=f['target'],
                          priority=int(f['priority']), flags=[] if f['flags'] == '0' else f['flags'].split(' | '),
                          battleEffectImplemented=False, source=source))
        move_names.append(name('src/data/text/move_names.h', symbol))
    textpath = 'src/battle_message.c'
    texts = []
    for symbol in ['sText_WildPkmnAppearedPause', 'gText_WhatWillOldManDo', 'sText_OldManUsedItem', 'sText_GotchaPkmnCaught2']:
        m = one(r'^(?:static )?const u8 ' + symbol + r'\[\] = _\("([^"\n]+)"\);$', sources[textpath])
        # Decode line breaks only; retain source engine control tokens (including \p).
        texts.append(dict(symbol=symbol, text=m[1].replace('\\n', '\n'), source=ref(textpath, symbol, m)))
    item = next(i for i in json.loads(sources['src/data/items.json'])['items'] if i['itemId'] == 'ITEM_TEACHY_TV')
    item_match = one(r'    \{\n      "english": "TEACHY TV",.*?^    \}', sources['src/data/items.json'])
    teachy_tv = dict(sourceItemId=number('include/constants/items.h', 'ITEM_TEACHY_TV'), name=item['english'],
                     price=item['price'], holdEffect=item['holdEffect'], holdEffectParam=item['holdEffectParam'],
                     description=item['description_english'].replace('\\n', '\n'), importance=item['importance'],
                     registrability=item['registrability'], pocket=item['pocket'], type=item['type'],
                     fieldUseFunc=item['fieldUseFunc'], battleUsage=item['battleUsage'], battleUseFunc=item['battleUseFunc'],
                     secondaryId=item['secondaryId'], source=ref('src/data/items.json', 'ITEM_TEACHY_TV', item_match))
    assets = []
    for p, b in verified.items():
        if p.startswith('graphics/'):
            w, h = struct.unpack('>II', b[16:24]) if p.endswith('.png') else (0, 0)
            assets.append(dict(path=p, bytes=len(b), sha256=sha(b), width=w, height=h))
    animpath = 'src/data/trainer_graphics/back_pic_anims.h'
    anim = one(r'^static const union AnimCmd sAnimCmd_OldMan_1\[\] = \{\n(.*?)^\};', sources[animpath])
    frames = [dict(frame=int(m[1]), ticks=int(m[2])) for m in re.finditer(r'ANIMCMD_FRAME\((\d+), (\d+)\)', anim[1])]
    # Store exact, bounded source blocks for presentation; no claim that metadata executes animations.
    layouts = []
    for path, symbol, pattern in [
        ('src/data/trainer_graphics/back_pic_tables.h', 'gTrainerBackPicCoords', r'^const struct MonCoords gTrainerBackPicCoords\[\] = \{.*?^\};'),
        ('src/data.c', 'gTrainerBackPicTable_OldMan', r'^const struct SpriteFrameImage gTrainerBackPicTable_OldMan\[\] =\n\{.*?^\};'),
        ('src/data/pokemon_graphics/front_pic_coordinates.h', 'SPECIES_WEEDLE', r'^    \[SPECIES_WEEDLE\] =\n    \{.*?^    \},'),
        ('src/battle_bg.c', 'B_WIN_OAK_OLD_MAN', r'^    \[B_WIN_OAK_OLD_MAN\] = \{.*?^    \},'),
        ('src/battle_message.c', 'B_WIN_OAK_OLD_MAN', r'^    \[B_WIN_OAK_OLD_MAN\] = \{.*?^    \}')]:
        m = one(pattern, sources[path]); layouts.append(dict(symbol=symbol, text=m[0], source=ref(path, symbol, m)))
    doc = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'],
               limitations=['Only the non-cancellable Old Man Lv5 demonstration is executable.',
                            'Weedle, Kakuna and Beedrill complete source learnsets/evolutions are metadata; only Lv5 Weedle is used by the tutorial.',
                            'Presentation metadata does not claim full original rendering/audio or VBlank frame RNG equivalence.',
                            'Teachy TV reward definition does not enable its five Pokedude tutorials.'],
               species=species, moves=moves, speciesNames=species_names, moveNames=move_names,
               texts=texts, teachyTv=teachy_tv, assets=assets, oldManThrowFrames=frames,
               oldManThrowSource=ref(animpath, 'sAnimCmd_OldMan_1', anim), layouts=layouts)
    data = (json.dumps(doc, indent=2, ensure_ascii=False) + '\n').encode()
    if sha(data) != GENERATED_SHA256:
        raise ValueError('Generated bytes differ from independently reviewed artifact')
    # No files are materialized until the complete closure verifies.
    for p, b in verified.items():
        dest = raw / p; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(b)
    out.mkdir(parents=True, exist_ok=True)
    (out / 'catalog.json').write_bytes(data)
    print(json.dumps(dict(profile=PROFILE, inputCount=len(verified), generatedSha256=sha(data), output=str(out / 'catalog.json'))))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=pathlib.Path, default=pathlib.Path(os.environ.get('FIRERED_ORIGINAL_REFERENCE','/workspace/shared/firered-reference-build/pokefirered')))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/old-man-tutorial')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/old-man-tutorial')
    args = parser.parse_args()
    import_content(args.repo, args.raw, args.out)
