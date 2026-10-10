#!/usr/bin/env python3
"""Authenticate TM34, two trainer portraits and the Gym's four additional synth cues.
Only pinned Git blobs/local inputs are read; upstream code is never executed.
"""
import argparse
import base64
import hashlib
import importlib.util
import json
import pathlib
import re
import struct
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-vermilion-gym-assets-v1'
LOCK_SHA256 = 'a6007bb7e7cb658fbb567c758bf38026aa5c2b94d875231738b5a7525cc00437'
LOCK_BYTES = 9529
MANIFEST_SHA256 = '8e903d87b0a0a7c44fa4f5ab7d3b1e4c09f92f2ab1b6d1828b2dbeab5c2f329a'
NEW_CUES = {'SE_UNLOCK', 'SE_M_CHARGE', 'SE_M_TRI_ATTACK2', 'SE_M_THUNDERBOLT'}
PORTRAITS = [('engineer-front', 'Engineer', 'engineer', 93),
             ('lt-surge-front', 'LeaderLtSurge', 'leader_lt_surge', 118)]


def sha(data): return hashlib.sha256(data).hexdigest()
def encoded(value): return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()
def pairs(rows):
    result = {}
    for key, value in rows:
        if key in result: raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result

def safe(root, relative=None):
    if '..' in pathlib.Path(root).parts: raise ValueError('Parent traversal root rejected')
    root = pathlib.Path(root).absolute()
    if relative is not None:
        if (not relative or pathlib.PurePosixPath(relative).is_absolute() or '\\' in relative
                or any(p in ('', '.', '..') for p in relative.split('/'))):
            raise ValueError('Unsafe Gym asset path')
        root /= relative
    for part in [root, *root.parents]:
        if part.is_symlink(): raise ValueError('Gym asset symlink rejected')
    return root

def read(root, path, length, digest):
    target = safe(root, path)
    if not 0 < length <= 3_000_000 or target.stat().st_size != length: raise ValueError('Input size differs: ' + path)
    data = target.read_bytes()
    if len(data) != length or sha(data) != digest: raise ValueError('Input hash differs: ' + path)
    return data

def read_lock(path=None):
    path = safe(path or ROOT / 'scripts/vermilion-gym-assets.lock.json')
    data = read(path.parent, path.name, LOCK_BYTES, LOCK_SHA256)
    lock = json.loads(data, object_pairs_hook=pairs)
    if (lock['schemaVersion'], lock['repository'], lock['commit'], lock['tree'], lock['profile'],
            lock['gameVersion'], lock['revision'], len(lock['files']), len(lock['inputs'])) != (
            1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0, 36, 4):
        raise ValueError('Gym asset lock identity differs')
    return lock

def source_hash(lock): return sha(''.join(e['path'] + '\0' + e['sha256'] + '\n' for e in lock['files']).encode())
def raw_manifest(lock):
    return encoded(dict(profile=PROFILE, sourceCommit=COMMIT, sourceTree=TREE,
                        lockSha256=LOCK_SHA256, fullSourceHash=source_hash(lock), inputCount=len(lock['files'])))

def authenticated_inputs(lock, raw, repository=None, root=ROOT):
    sources, inputs = {}, {}
    if repository:
        repository = safe(repository)
        tree = subprocess.check_output(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}']).decode().strip()
        if tree != TREE: raise ValueError('Pinned source tree differs')
    elif safe(raw, 'manifest.json').read_bytes() != raw_manifest(lock):
        raise ValueError('Gym raw manifest differs')
    for entry in lock['files']:
        path = entry['path']
        safe(raw, path)
        if path in sources: raise ValueError('Duplicate source file')
        if repository:
            row = subprocess.check_output(['git', '-C', str(repository), 'ls-tree', COMMIT, '--', path]).decode()
            if row != '100644 blob ' + entry['gitBlobSha1'] + '\t' + path + '\n':
                raise ValueError('Source must be a pinned regular Git blob')
            data = subprocess.check_output(['git', '-C', str(repository), 'cat-file', 'blob', entry['gitBlobSha1']])
        else: data = read(raw, path, entry['byteLength'], entry['sha256'])
        if (len(data), sha(data), hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest()) != (
                entry['byteLength'], entry['sha256'], entry['gitBlobSha1']):
            raise ValueError('Gym original Git blob differs: ' + path)
        sources[path] = data
    for entry in lock['inputs']:
        if entry['path'] in inputs: raise ValueError('Duplicate local input')
        inputs[entry['path']] = read(root, entry['path'], entry['byteLength'], entry['sha256'])
    return sources, inputs

def sound_closure(script, moves):
    labels = list(re.finditer(r'^(\w+)::?$', script, re.M))
    blocks = {m[1]: script[m.end():labels[i + 1].start() if i + 1 < len(labels) else len(script)]
              for i, m in enumerate(labels)}
    result = {}
    for move in sorted(moves):
        pending, seen, effects = ['Move_' + move], set(), set()
        while pending:
            label = pending.pop()
            if label in seen: continue
            if label not in blocks: raise ValueError('Unresolved animation branch: ' + label)
            seen.add(label)
            for line in blocks[label].splitlines():
                line = line.split('@', 1)[0].strip()
                if not line: continue
                tokens = line.split(None, 1)
                op = tokens[0]
                args = [a.strip() for a in tokens[1].split(',')] if len(tokens) > 1 else []
                effects.update(re.findall(r'\bSE_\w+', line))
                arities = {'call': 1, 'goto': 1, 'jumprettrue': 1, 'jumpretfalse': 1,
                           'jumpargeq': 3, 'jumpreteq': 2, 'choosetwoturnanim': 2}
                if op in arities:
                    if len(args) != arities[op]: raise ValueError('Unexpected animation branch arity')
                    pending.extend(args if op == 'choosetwoturnanim' else [args[-1]])
                elif op.startswith(('jump', 'choose')): raise ValueError('Unsupported animation branch: ' + op)
        result[move] = dict(effects=sorted(effects), labels=sorted(seen))
    return result

def build(lock, sources, inputs):
    text = {p: b.decode() for p, b in sources.items() if not p.endswith(('.png', '.mid'))}
    entries = {e['path']: e for e in lock['files']}
    rules = []
    def match(path, pattern):
        matches = list(re.finditer(pattern, text[path], re.M | re.S))
        if len(matches) != 1: raise ValueError('Nonunique source match: ' + path + ' ' + pattern)
        return matches[0]
    def ref(path, symbol, m=None):
        return dict(path=path, symbol=symbol, startLine=1 if m is None else text[path].count('\n', 0, m.start()) + 1,
                    endLine=max(1, text.get(path, '').count('\n')) if m is None else text[path].count('\n', 0, m.end()) + 1,
                    sha256=entries[path]['sha256'])
    def require(path, fragment):
        if fragment not in text[path]: raise ValueError('Source contract missing: ' + fragment)
    def constant(path, symbol):
        m = match(path, r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$')
        rules.append(ref(path, symbol, m))
        return int(m[1])
    def ident(symbol): return dict(value='pret:' + symbol)

    prior = json.loads(inputs['content/generated/ssanne-machines/ssanne-machines.json'], object_pairs_hook=pairs)
    if (prior['profile'], len(prior['machines']), len(prior['machines'][0]['compatibility'])) != (
            'firered-ssanne-machines-v1', 11, 73): raise ValueError('Historical machine scope differs')
    compatibility = []
    for original in prior['machines'][0]['compatibility']:
        symbol = original['species']['value'][5:]
        path = 'src/data/pokemon/tmhm_learnsets.h'
        m = match(path, r'^    \[' + symbol + r'\]\s*= TMHM_LEARNSET\((.*?)\),')
        tokens = re.findall(r'TMHM\((\w+)\)', m[1])
        if re.sub(r'TMHM\(\w+\)|\||\s|0', '', m[1]) or len(tokens) != len(set(tokens)):
            raise ValueError('Unsupported source TM compatibility syntax')
        source = ref(path, symbol, m)
        species_id = constant('include/constants/species.h', symbol)
        if source != original['source'] or species_id != original['sourceSpeciesId']:
            raise ValueError('Historical 73-species identity/source spans changed')
        compatibility.append(dict(species=ident(symbol), sourceSpeciesId=species_id,
                                  canLearn='TM34_SHOCK_WAVE' in tokens, source=source))
    all_items = json.loads(text['src/data/items.json'], object_pairs_hook=pairs)['items']
    chosen = [i for i in all_items if i['itemId'] == 'ITEM_TM34']
    if len(chosen) != 1: raise ValueError('TM34 item nonunique')
    i = chosen[0]
    m = match('src/data/items.json', r'^    \{\n      "english": "TM34",.*?^    \}')
    definition = dict(id=ident(i['itemId']), sourceItemSymbol=i['itemId'], sourceItemId=constant('include/constants/items.h', i['itemId']),
        name=i['english'], price=i['price'], importance=i['importance'], registrability=i['registrability'], pocket=i['pocket'],
        type=str(i['type']), holdEffect=i['holdEffect'], holdEffectParam=i['holdEffectParam'], fieldUseFunction=i['fieldUseFunc'],
        battleUsage=i['battleUsage'], battleUseFunction=i['battleUseFunc'], secondaryId=i['secondaryId'],
        canToss=i['importance'] == 0, canSell=i['price'] != 0, canGive=i['importance'] == 0, source=ref('src/data/items.json', i['itemId'], m))
    m = match('src/move_descriptions.c', r'^const u8 gMoveDescription_ShockWave\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
    machine = dict(item=definition, description=json.loads(m[1]), declarationDescription=i['description_english'].replace('\\n', '\n'),
                   descriptionSource=ref('src/move_descriptions.c', 'gMoveDescription_ShockWave', m))
    m = match('src/data/party_menu.h', r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};')
    mapping = re.findall(r'\bMOVE_\w+', m[1])
    if len(mapping) != 58 or mapping[33] != 'MOVE_SHOCK_WAVE': raise ValueError('TM34 source move map differs')
    rules.append(ref('src/data/party_menu.h', 'sTMHMMoves', m))
    m = match('src/data/battle_moves.h', r'^    \[MOVE_SHOCK_WAVE\] =\n    \{(.*?)^    \},')
    pp = int(re.search(r'\.pp = (\d+),', m[1])[1])
    move_id = constant('include/constants/moves.h', 'MOVE_SHOCK_WAVE')
    if (definition['sourceItemId'], move_id, pp) != (322, 351, 20): raise ValueError('TM34 source identity differs')
    rules.append(ref('src/data/battle_moves.h', 'MOVE_SHOCK_WAVE', m))
    tm34 = dict(machine=machine, move=ident('MOVE_SHOCK_WAVE'), sourceMoveId=move_id, movePp=pp, compatibility=compatibility)
    m = match('data/maps/VermilionCity_Gym/scripts.inc', r'^VermilionCity_Gym_EventScript_GiveTM34::.*?^\tend$')
    rules.append(ref('data/maps/VermilionCity_Gym/scripts.inc', 'VermilionCity_Gym_EventScript_GiveTM34', m))

    # The only executed helper is a separately authenticated local PNG codec, never upstream code.
    codec_path = safe(ROOT, 'scripts/import-lab-battle-ui.py')
    if codec_path.read_bytes() != inputs['scripts/import-lab-battle-ui.py']: raise ValueError('Authenticated PNG codec differs')
    spec = importlib.util.spec_from_file_location('_gym_asset_codec', codec_path)
    codec = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(codec)
    outputs, portraits = {}, []
    require('tools/gbagfx/gfx.c', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)')
    def convert(name, image_path, palette_path, size):
        width, height, indices, _ = codec.indexed_png(sources[image_path])
        if (width, height) != size or max(indices) > 15: raise ValueError('Source indexed image geometry differs')
        rgba = codec.rgba(indices, codec.palette(sources[palette_path]))
        png = codec.png(width, height, rgba)
        outputs[name + '.rgba'], outputs[name + '.png'] = rgba, png
        return rgba, png
    p = 'src/data/item_icon_table.h'
    m = match(p, r'^    \[ITEM_TM34\][ \t]*= \{(\w+), (\w+)\},')
    binding = ref(p, 'ITEM_TM34', m)
    symbols, paths = m.groups(), []
    for symbol in symbols:
        p = 'src/data/graphics/items.h'
        m = match(p, r'^const u32 ' + symbol + r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);')
        rules.append(ref(p, symbol, m))
        paths.append(m[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
    rgba, png = convert('item_tm34', paths[0], paths[1], (24, 24))
    icon = dict(item=ident('ITEM_TM34'), width=24, height=24, rgbaBase64=base64.b64encode(rgba).decode(), rgbaSha256=sha(rgba),
                pngPath='item_tm34.png', pngSha256=sha(png), source=ref(paths[0], symbols[0]),
                paletteSource=ref(paths[1], symbols[1]), bindingSource=binding)
    coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', text['src/data/trainer_graphics/front_pic_tables.h'].split('const struct CompressedSpriteSheet')[0])
    require('src/battle_controller_opponent.c', '(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40')
    require('src/battle_controller_opponent.c', 'CreateSprite(&gMultiuseSpriteTemplate,\n                                                     176,')
    for name, symbol, stem, picture in PORTRAITS:
        image_path = 'graphics/trainers/front_pics/' + stem + '_front_pic.png'
        palette_path = 'graphics/trainers/palettes/' + stem + '.pal'
        require('src/data/graphics/trainers.h', 'gTrainerFrontPic_' + symbol + '[] = INCBIN_U32("' + image_path.replace('.png', '.4bpp.lz') + '")')
        require('src/data/graphics/trainers.h', 'gTrainerPalette_' + symbol + '[] = INCBIN_U32("' + palette_path.replace('.pal', '.gbapal.lz') + '")')
        require('src/data/trainer_graphics/front_pic_tables.h', 'TRAINER_SPRITE(' + stem.upper() + ', gTrainerFrontPic_' + symbol + ', 0x800)')
        require('src/data/trainer_graphics/front_pic_tables.h', 'TRAINER_PAL(' + stem.upper() + ', gTrainerPalette_' + symbol + ')')
        if constant('include/constants/trainers.h', 'TRAINER_PIC_' + stem.upper()) != picture or coords[picture] != ('8', '1'):
            raise ValueError('Trainer picture identity/geometry differs')
        rgba, png = convert(name, image_path, palette_path, (64, 64))
        portraits.append(dict(id=name, width=64, height=64, x=144, y=8, sourceTrainerPicId=picture, sourceFrame=0,
                              sourceImage=image_path, sourcePalette=palette_path, rgbaSha256=sha(rgba), pngSha256=sha(png)))

    # Resolve precisely the four Gym parties, including source default-move insertion/oldest-slot deletion.
    require('src/pokemon.c', 'if (GiveMoveToBoxMon(boxMon, move) == MON_HAS_MAX_MOVES)\n            DeleteFirstMoveAndGiveMoveToBoxMon(boxMon, move);')
    require('src/pokemon.c', 'if (existingMove == move)\n            return MON_ALREADY_KNOWS_MOVE;')
    resolved, moves = [], set()
    for trainer in ['LEADER_LT_SURGE', 'SAILOR_DWAYNE', 'ENGINEER_BAILY', 'GENTLEMAN_TUCKER']:
        m = match('src/data/trainers.h', r'^    \[TRAINER_' + trainer + r'\] = \{.*?^    \},')
        party_type, party_name = re.search(r'\.party = (NO_ITEM_(?:CUSTOM|DEFAULT)_MOVES)\((\w+)\)', m[0]).groups()
        rules.append(ref('src/data/trainers.h', 'TRAINER_' + trainer, m))
        m = match('src/data/trainer_parties.h', r'^static const struct \w+ ' + party_name + r'\[\] = \{(.*?)^\};')
        rules.append(ref('src/data/trainer_parties.h', party_name, m))
        roster = []
        for slot in re.findall(r'    \{(.*?)    \},', m[1], re.S):
            species = re.search(r'\.species = (SPECIES_\w+)', slot)[1]
            level = int(re.search(r'\.lvl = (\d+)', slot)[1])
            if party_type == 'NO_ITEM_CUSTOM_MOVES': known = re.findall(r'\bMOVE_\w+', slot)
            else:
                pointer = match('src/data/pokemon/level_up_learnset_pointers.h', r'^    \[' + species + r'\] = (\w+),$')[1]
                learnset = match('src/data/pokemon/level_up_learnsets.h', r'^static const u16 ' + pointer + r'\[\] = \{(.*?)^\};')
                known = []
                for lvl, move in re.findall(r'LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)', learnset[1]):
                    if int(lvl) > level: break
                    if move not in known: known = (known + [move])[-4:]
            if len(known) != 4: raise ValueError('Gym party move slots differ')
            moves.update(k.removeprefix('MOVE_') for k in known)
            roster.append(dict(species=species, level=level, moves=known))
        resolved.append(dict(trainer='TRAINER_' + trainer, party=roster))
    expected = set('THUNDER_WAVE QUICK_ATTACK DOUBLE_TEAM SLAM TACKLE SCREECH SONIC_BOOM SPARK THUNDER_SHOCK SUPERSONIC SHOCK_WAVE'.split())
    if moves != expected or [len(r['party']) for r in resolved] != [3, 2, 2, 1]: raise ValueError('Gym move repertoire differs')
    closure = sound_closure(text['data/battle_anim_scripts.s'], moves)
    prior_bytes = inputs['scripts/synth-ssanne-cues.json']
    recipe = json.loads(prior_bytes, object_pairs_hook=pairs)
    prior_ids = {c['id'] for c in recipe['cues']}
    if (recipe['schemaVersion'], recipe['sourceCommit'], len(recipe['cues']), len(prior_ids)) != (1, COMMIT, 239, 239):
        raise ValueError('Historical239 synth identity differs')
    field = set(re.findall(r'\b(?:MUS|SE)_\w+', text['data/maps/VermilionCity_Gym/scripts.inc']))
    field.add(json.loads(text['data/maps/VermilionCity_Gym/map.json'])['music'])
    cut = match('src/fldeff_cut.c', r'^static void FieldMoveCallback_CutTree\(void\)\n\{.*?^\}')
    field.update(re.findall(r'PlaySE\((SE_\w+)\)', cut[0]))
    rules.append(ref('src/fldeff_cut.c', 'FieldMoveCallback_CutTree', cut))
    effects = set().union(*(set(c['effects']) for c in closure.values()))
    if field - prior_ids != {'SE_UNLOCK'} or effects - prior_ids != NEW_CUES - {'SE_UNLOCK'}:
        raise ValueError('Gym field/battle sound closure differs')
    constants = {s: int(n) for s, n in re.findall(r'^#define ((?:MUS|SE)_\w+)\s+(\d+)', text['include/constants/songs.h'], re.M)}
    table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', text['sound/song_table.inc'], re.M)
    pack = inputs['content/generated/synth-audio/firered.pack']
    additions = []
    for cue in sorted(NEW_CUES):
        source_id = constants[cue]
        address, player, secondary = struct.unpack_from('<IHH', pack, 0x084A32CC + source_id * 8 - 0x08489968)
        path = 'sound/songs/midi/' + cue.lower() + '.mid'
        if table[source_id] != (cue.lower(), str(player), str(secondary)) or not sources[path].startswith(b'MThd'):
            raise ValueError('Source cue mapping/MIDI differs')
        additions.append(dict(id=cue, sourceCommit=COMMIT, sourceSymbol=cue, sourcePath=path, sourcePlayer=player,
                              priority=pack[address - 0x08489968 + 2], sourceCounter=None, songAddress=address, species=0, looping=False))
    old = recipe['cues']
    recipe['cues'] = sorted(old + additions, key=lambda c: c['id'])
    candidate = encoded(recipe)
    for cue in old:
        record = '\n'.join('    ' + line for line in json.dumps(cue, indent=2).splitlines()).encode()
        if record not in prior_bytes or record not in candidate: raise ValueError('Historical cue record bytes changed')
    outputs['synth-manifest-candidate.json'] = candidate
    outputs['synth-extra-cues.json'] = encoded(dict(schemaVersion=1, sourceCommit=COMMIT, cues=additions))
    outputs['synth-closure.json'] = encoded(dict(schemaVersion=1, sourceCommit=COMMIT, priorCueCount=239, candidateCueCount=243,
        candidateSha256=sha(candidate), resolvedTrainers=resolved, moves=closure, fieldCues=sorted(field),
        extraCues=sorted(NEW_CUES), samplePackSha256=sha(pack), recordingsWritten=False))
    document = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'], localInputs=lock['inputs'],
                    rulesSources=rules, tm34=tm34, icon=icon, portraits=portraits, limitations=[
                    'One new machine over the unchanged 73-species S.S. Anne scope; historical eleven-machine profiles remain exact.',
                    'Two original trainer portraits only; Sailor and Gentleman are reused from the existing ship art.',
                    'Four new original-source synthesized effects; candidate manifest preserves all historical239 cue records.',
                    'No recording files, gameplay integration, native-window acceptance or complete-game acceptance is claimed.'])
    outputs['assets.json'] = encoded(document)
    outputs['manifest.json'] = encoded(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, sourceTree=TREE,
        lockSha256=LOCK_SHA256, fullSourceHash=source_hash(lock), outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(outputs.items())]))
    return outputs

def run(raw, out, repository=None, lock_path=None, root=ROOT, check=False):
    raw, out = safe(raw), safe(out)
    if raw == out or raw in out.parents or out in raw.parents: raise ValueError('Raw/generated overlap')
    if repository:
        reference = safe(repository)
        if any(p == reference or p in reference.parents or reference in p.parents for p in (raw, out)):
            raise ValueError('Reference/output overlap')
    for kind in ('raw', 'generated'):
        for protected in (ROOT / 'content' / kind).iterdir():
            if protected.name == 'vermilion-gym-assets': continue
            if any(p == protected or p in protected.parents or protected in p.parents for p in (raw, out)):
                raise ValueError('Legacy/dependency output overlap')
    lock = read_lock(lock_path)
    sources, inputs = authenticated_inputs(lock, raw, repository, root)
    outputs = build(lock, sources, inputs)
    if sha(outputs['manifest.json']) != MANIFEST_SHA256: raise ValueError('Reviewed Gym generated manifest differs')
    materials = {(out, p): b for p, b in outputs.items()}
    if repository:
        materials.update({(raw, p): b for p, b in sources.items()})
        materials[(raw, 'manifest.json')] = raw_manifest(lock)
    # Preflight every target before any writes; an untrusted failed input cannot alter output.
    targets = []
    for (parent, path), data in materials.items():
        target, stage = safe(parent, path), safe(parent, path + '.tmp')
        for part in [target.parent, *target.parent.parents]:
            if part.exists() and not part.is_dir(): raise ValueError('Non-directory target parent')
        if any(p.exists() and not p.is_file() for p in (target, stage)): raise ValueError('Non-file target')
        targets.append((target, stage, data))
    for target, stage, data in targets:
        if check:
            if target.read_bytes() != data: raise ValueError('Generated output differs: ' + str(target))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            stage.write_bytes(data)
            stage.replace(target)
    return dict(passed=True, sourceFiles=36, localInputs=4, species=73, machines=12, newPortraits=2,
                newSynthCues=4, candidateCueCount=243, manifestSha256=sha(outputs['manifest.json']),
                catalogSha256=sha(outputs['assets.json']), outputs=len(outputs), recordingsWritten=False)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/vermilion-gym-assets')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/vermilion-gym-assets')
    parser.add_argument('--lock', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.raw, args.out, args.reference_repo, args.lock, check=args.check), sort_keys=True))
