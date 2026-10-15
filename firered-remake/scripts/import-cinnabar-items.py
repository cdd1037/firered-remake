#!/usr/bin/env python3
"""Import four Cinnabar item declarations and 31 exact teaching matrices from pinned Git blobs."""
from item_catalog import item_definition as shared_item, inventory_item, item_json
from pathlib import Path
import argparse,  importlib.util, json, re, sys
sys.dont_write_bytecode = True

SPEC = importlib.util.spec_from_file_location('saffron_import', Path(__file__).with_name('import-saffron-gym-catalogs.py'))
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
import machine_catalog as shared_machines

COMMIT, TREE = BASE.COMMIT, BASE.TREE
PROFILE = 'firered-cinnabar-items-source-v1'
MACHINES = BASE.MACHINES + ('TM04',)
NEW_MACHINES = ('TM14', 'TM22', 'TM38')
ITEMS = ('SECRET_KEY',) + NEW_MACHINES

def need(value, message):
    if not value:
        raise ValueError(message)

def ident(symbol):
    return dict(value='pret:' + symbol)


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
                    endLine=text[path].count('\n', 0, match.end()) + 1)
    catalog = shared_machines.load_catalog(dependency)
    definitions = []
    new = shared_machines.machine_definitions(dependency, 'content/generated/cinnabar-items/machines.json')
    for short in ITEMS:
        symbol = 'ITEM_' + short
        item = next(i for i in json.loads(text['src/data/items.json'])['items'] if i['itemId'] == symbol)
        path = 'src/data/items.json'
        match = one(path, r'^    \{\n      "english": "' + re.escape(item['english']) + r'",.*?^    \}')
        item_ref = ref(path, symbol, match)
        definition = shared_item(symbol)
        declaration = item['description_english'].replace(chr(92) + 'n', chr(10))
        description = declaration
        if short in NEW_MACHINES:
            path = 'src/move_descriptions.c'
            match = one(path, r'^const u8 gMoveDescription_' + item['moveId'] + r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
            description = json.loads(match[1])
        metadata = dict(item=definition, description=description)
        definitions.append(dict(metadata=metadata, effectBytes=[]))
    path = 'data/maps/CinnabarIsland_Mart/scripts.inc'
    match = one(path, r'^CinnabarIsland_Mart_Items::\s*\n((?:\s*\.2byte ITEM_\w+\n)+)')
    stock = re.findall(r'\.2byte (ITEM_\w+)', match[1])
    need(stock == ['ITEM_ULTRA_BALL', 'ITEM_GREAT_BALL', 'ITEM_HYPER_POTION', 'ITEM_REVIVE', 'ITEM_FULL_HEAL', 'ITEM_ESCAPE_ROPE', 'ITEM_MAX_REPEL', 'ITEM_NONE'], 'Cinnabar Mart source stock differs')
    mart = dict(id='CinnabarIsland_Mart_Items', items=[ident(s) for s in stock[:-1]])
    items = dict(schemaVersion=1, profile='firered-cinnabar-items-v1', sourceCommit=COMMIT, items=definitions,
                 mart=mart, limitations=['Only Secret Key extends the inherited ordinary inventory; all predecessor item definitions retain identity.',
                 'TM14, TM22 and TM38 belong in the separate TM Case; item declarations do not grant rewards or implement field scripts.'])
    machines = dict(schemaVersion=1, profile='firered-cinnabar-machines-v1', sourceCommit=COMMIT,
        inheritedItemIds=[ident('ITEM_' + m) for m in MACHINES],
        additions=shared_machines.compatibility_additions(dependency, 'content/generated/cinnabar-items/machines.json'),
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
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        outputs=[dict(path=p) for p, b in sorted(outputs.items()) if p.startswith('content/generated/')])
    outputs['content/generated/cinnabar-items/manifest.json'] = BASE.canonical(manifest)
    outputs.update({'content/raw/pokefirered/037335f4/' + p: b for p, b in source.data.items()})
    return outputs, items, machines

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, required=True)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).absolute().parents[1])
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs, items, machines = generate(args.reference_root, args.dependency_root)
    for path, data in outputs.items():
        target = BASE.safe(args.project_root, path)
        if args.check:
            need(target.read_bytes() == data, 'Regeneration differs: ' + path)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(items=4, shopAdditions=1, machines=31, species=115, inheritedCells=3164, appendedCells=56,
        newCells=345), default=item_json))

if __name__ == '__main__':
    main()
