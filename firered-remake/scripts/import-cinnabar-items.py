#!/usr/bin/env python3
"""Import four Cinnabar item declarations and 31 exact teaching matrices from pinned Git blobs."""
from pathlib import Path
import argparse, hashlib, importlib.util, json, re, sys
sys.dont_write_bytecode = True

SPEC = importlib.util.spec_from_file_location('saffron_import', Path(__file__).with_name('import-saffron-gym-catalogs.py'))
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
COMMIT, TREE = BASE.COMMIT, BASE.TREE
PROFILE = 'firered-cinnabar-items-source-v1'
MACHINES = BASE.MACHINES + ('TM04',)
NEW_MACHINES = ('TM14', 'TM22', 'TM38')
ITEMS = ('SECRET_KEY',) + NEW_MACHINES
MANIFEST_SHA256 = 'd7758a4ab00ce39fc790aaafe5b77b9f84ec33f2c3bdf70b6ea4e0a96caf0b65'
LOCK_SHA256 = '376c84d363b74d4255f75b7ddcba22932182e373ea8f7c21acc458263fa869d9'

def need(value, message):
    if not value:
        raise ValueError(message)

def ident(symbol):
    return dict(value='pret:' + symbol)

def fingerprint(value):
    return BASE.sha(json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode())

def convert(source, dependency):
    paths = ('include/constants/items.h', 'include/constants/species.h', 'include/constants/moves.h',
             'src/data/items.json', 'src/data/party_menu.h', 'src/data/battle_moves.h',
             'src/data/pokemon/tmhm_learnsets.h', 'src/move_descriptions.c',
             'data/maps/CinnabarIsland_Mart/scripts.inc')
    text = {p: source.text(p) for p in paths}
    def one(path, pattern):
        rows = list(re.finditer(pattern, text[path], re.M | re.S))
        need(len(rows) == 1, 'Expected one source declaration: ' + pattern)
        return rows[0]
    def number(path, symbol):
        return int(one(path, r'^#define ' + symbol + r'\s+(\d+)\b[^\n]*')[1])
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text[path].count('\n', 0, match.start()) + 1,
                    endLine=text[path].count('\n', 0, match.end()) + 1, sha256=source.entries[path]['sha256'])
    baseline = BASE.safe(dependency, 'scripts/fuchsia-gym-baseline.lock.json').read_bytes()
    need(BASE.sha(baseline) == '3775e076364c67d145c5754e88907d1bdf3de334b7e714e90a63b86cb87b396d', '113-species predecessor differs')
    owned = json.loads(baseline)['species'] + ['CHANSEY', 'SCYTHER', 'TAUROS', 'PSYDUCK', 'GOLDUCK', 'LAPRAS', 'KRABBY', 'KINGLER']
    need(len(owned) == len(set(owned)) == 115, 'Expected 115 owned species')
    def row(short, tm):
        symbol = 'SPECIES_' + short
        path = 'src/data/pokemon/tmhm_learnsets.h'
        match = one(path, r'^    \[' + symbol + r'\]\s*= TMHM_LEARNSET\((.*?)\),')
        tokens = re.findall(r'TMHM\((\w+)\)', match[1])
        need(not re.sub(r'TMHM\(\w+\)|\||\s|0', '', match[1]), 'Unhandled source compatibility syntax')
        return dict(species=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol),
                    canLearn=any(t.startswith(tm + '_') for t in tokens), source=ref(path, symbol, match))
    path = 'src/data/party_menu.h'
    mapping_match = one(path, r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};')
    mapping = re.findall(r'MOVE_\w+', mapping_match[1])
    need(len(mapping) == 58, '58 machine source mapping differs')
    refs = [ref(path, 'sTMHMMoves', mapping_match)]
    definitions, new = [], []
    for short in ITEMS:
        symbol = 'ITEM_' + short
        item = next(i for i in json.loads(text['src/data/items.json'])['items'] if i['itemId'] == symbol)
        path = 'src/data/items.json'
        match = one(path, r'^    \{\n      "english": "' + re.escape(item['english']) + r'",.*?^    \}')
        item_ref = ref(path, symbol, match)
        definition = dict(id=ident(symbol), sourceItemSymbol=symbol, sourceItemId=number('include/constants/items.h', symbol),
            name=item['english'], price=item['price'], importance=item['importance'], registrability=item['registrability'],
            pocket=item['pocket'], type=str(item['type']), holdEffect=item['holdEffect'], holdEffectParam=item['holdEffectParam'],
            fieldUseFunction=item['fieldUseFunc'], battleUsage=item['battleUsage'], battleUseFunction=item['battleUseFunc'],
            secondaryId=item['secondaryId'], canToss=item['importance'] == 0, canSell=item['price'] != 0,
            canGive=item['importance'] == 0, source=item_ref)
        declaration = item['description_english'].replace(chr(92) + 'n', chr(10))
        description, description_ref = declaration, item_ref
        if short in NEW_MACHINES:
            path = 'src/move_descriptions.c'
            match = one(path, r'^const u8 gMoveDescription_' + item['moveId'] + r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
            description, description_ref = json.loads(match[1]), ref(path, 'gMoveDescription_' + item['moveId'], match)
        metadata = dict(item=definition, description=description, declarationDescription=declaration, descriptionSource=description_ref)
        definitions.append(dict(metadata=metadata, effectBytes=[], effectSource=None))
        if short in NEW_MACHINES:
            move = mapping[int(short[2:]) - 1]
            need(move == 'MOVE_' + re.sub(r'([a-z])([A-Z])', r'\1_\2', item['moveId']).upper(), 'Item and machine move mapping disagree')
            path = 'src/data/battle_moves.h'
            match = one(path, r'^    \[' + move + r'\] =\n    \{(.*?)^    \},')
            refs.append(ref(path, move, match))
            new.append(dict(machine=metadata, move=ident(move), sourceMoveId=number('include/constants/moves.h', move),
                movePp=int(re.search(r'\.pp = (\d+),', match[1])[1]),
                compatibility=sorted([row(s, short) for s in owned], key=lambda r: r['sourceSpeciesId'])))
    path = 'data/maps/CinnabarIsland_Mart/scripts.inc'
    match = one(path, r'^CinnabarIsland_Mart_Items::\s*\n((?:\s*\.2byte ITEM_\w+\n)+)')
    stock = re.findall(r'\.2byte (ITEM_\w+)', match[1])
    need(stock == ['ITEM_ULTRA_BALL', 'ITEM_GREAT_BALL', 'ITEM_HYPER_POTION', 'ITEM_REVIVE', 'ITEM_FULL_HEAL', 'ITEM_ESCAPE_ROPE', 'ITEM_MAX_REPEL', 'ITEM_NONE'], 'Cinnabar Mart source stock differs')
    mart = dict(id='CinnabarIsland_Mart_Items', items=[ident(s) for s in stock[:-1]], source=ref(path, 'CinnabarIsland_Mart_Items', match))
    items = dict(schemaVersion=1, profile='firered-cinnabar-items-v1', sourceCommit=COMMIT, items=definitions,
                 mart=mart, rulesSources=refs, limitations=['Only Secret Key extends the inherited ordinary inventory; all predecessor item definitions retain identity.',
                 'TM14, TM22 and TM38 belong in the separate TM Case; item declarations do not grant rewards or implement field scripts.'])
    machines = dict(schemaVersion=1, profile='firered-cinnabar-machines-v1', sourceCommit=COMMIT,
        inheritedItemIds=[ident('ITEM_' + m) for m in MACHINES],
        additions=[dict(item=ident('ITEM_' + m), compatibility=[row(s, m) for s in ('KRABBY', 'KINGLER')]) for m in MACHINES],
        newMachines=new, limitations=['All 3164 inherited compatibility cells retain object identity; 56 appended cells add only Krabby and Kingler.',
        'Three new matrices contain all 115 owned species in source species-ID order; opponent-only Alakazam, Slowbro and Mr. Mime remain excluded.'])
    return items, machines

def generate(reference, dependency):
    source = BASE.Source(reference)
    items, machines = convert(source, dependency)
    lock = source.lock()
    lock['profile'] = PROFILE
    lock['gameEdition'], lock['romRevision'] = 'FIRERED', 0
    outputs = {'content/generated/cinnabar-items/items.json': BASE.canonical(items),
               'content/generated/cinnabar-items/machines.json': BASE.canonical(machines),
               'scripts/cinnabar-items.lock.json': BASE.canonical(lock)}
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, sourceTree=TREE,
        fullSourceHash=BASE.sha(''.join(p + '\0' + e['sha256'] + '\n' for p, e in sorted(source.entries.items())).encode()),
        lockSha256=BASE.sha(outputs['scripts/cinnabar-items.lock.json']),
        outputs=[dict(path=p, sha256=BASE.sha(b), byteLength=len(b)) for p, b in sorted(outputs.items()) if p.startswith('content/generated/')])
    outputs['content/generated/cinnabar-items/manifest.json'] = BASE.canonical(manifest)
    outputs.update({'content/raw/cinnabar-items/' + p: b for p, b in source.data.items()})
    return outputs, items, machines

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, required=True)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).absolute().parents[1])
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs, items, machines = generate(args.reference_root, args.dependency_root)
    for path, expected in [('content/generated/cinnabar-items/manifest.json', MANIFEST_SHA256), ('scripts/cinnabar-items.lock.json', LOCK_SHA256)]:
        need(BASE.sha(outputs[path]) == expected, 'Pinned generated package changed: ' + path)
    for path, data in outputs.items():
        target = BASE.safe(args.project_root, path)
        if args.check:
            need(target.read_bytes() == data, 'Regeneration differs: ' + path)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(items=4, shopAdditions=1, machines=31, species=115, inheritedCells=3164, appendedCells=56,
        newCells=345, manifestSha256=BASE.sha(outputs['content/generated/cinnabar-items/manifest.json']),
        lockSha256=BASE.sha(outputs['scripts/cinnabar-items.lock.json']), newMachinesFingerprint=fingerprint(machines['newMachines']),
        additionsFingerprint=fingerprint(machines['additions']), martFingerprint=fingerprint(items['mart']))))

if __name__ == '__main__':
    main()
