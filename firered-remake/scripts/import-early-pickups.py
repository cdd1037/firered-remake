#!/usr/bin/env python3
"""Import eight early field pickups from pinned local source bytes; never execute pret."""
import argparse
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-early-field-pickups-v1'
SCOPE = [('ViridianCity', 'object_events', 17, 5),
         ('ViridianForest', 'object_events', 5, 41),
         ('ViridianForest', 'object_events', 40, 21),
         ('ViridianForest', 'object_events', 21, 34),
         ('ViridianForest', 'object_events', 49, 60),
         ('ViridianForest', 'bg_events', 3, 22),
         ('ViridianForest', 'bg_events', 28, 57),
         ('PewterCity', 'bg_events', 6, 3)]
HELPERS = ['Std_FindItem', 'EventScript_BufferPocketNameTryFanfare', 'EventScript_BufferItemsPocket',
           'EventScript_BufferPokeBallsPocket', 'EventScript_PlayFanfareObtainedItem', 'EventScript_PickUpItem',
           'EventScript_FoundItem', 'EventScript_NoRoomToPickUpItem', 'EventScript_HiddenItemScript',
           'EventScript_TryPickUpHiddenItem', 'EventScript_PickedUpHiddenItem', 'EventScript_FoundSingleItem',
           'EventScript_NoRoomForHiddenItem']
TEXTS = ['Text_ObtainedTheX', 'Text_PutItemAway', 'Text_FoundOneItem', 'Text_TooBadBagFull']


def unique(pairs):
    result = {}
    for k, v in pairs:
        if k in result:
            raise ValueError('Duplicate JSON member: ' + k)
        result[k] = v
    return result


def safe_file(root, path):
    p = pathlib.PurePosixPath(path)
    if p.is_absolute() or str(p) != path or any(s in ('', '.', '..') for s in path.split('/')) or '\\' in path:
        raise ValueError('Unsafe source/output path: ' + path)
    target = pathlib.Path(root).absolute() / path
    for part in [target, *target.parents]:
        if part.is_symlink():
            raise ValueError('Symlink is unsupported: ' + str(part))
    return target


def read_lock(path=None):
    path = pathlib.Path(path or ROOT / 'content/early-pickups-source-lock.json')
    data = safe_file(path.parent, path.name).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique)
    if (lock['commit'], lock['profile'], lock['pickupCount'], len(lock['files'])) != (COMMIT, PROFILE, 8, 23):
        raise ValueError('Source scope differs')
    return lock


def materialize(repo, raw):
    collected = []
    for entry in read_lock()['files']:
        data = subprocess.check_output(['git', '-C', str(repo), 'show', COMMIT + ':' + entry['path']])
        collected.append((safe_file(raw, entry['path']), data))
    for path, data in collected:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


class Source:
    def __init__(self, raw, lock_path=None):
        self.lock = read_lock(lock_path)
        self.entries = {e['path']: e for e in self.lock['files']}
        self.texts = {}
        for path, entry in self.entries.items():
            data = safe_file(raw, path).read_bytes()
            self.texts[path] = data.decode('utf-8')

    def one(self, path, pattern):
        found = list(re.finditer(pattern, self.texts[path], re.M))
        if len(found) != 1:
            raise ValueError('Expected one source record: ' + path + ': ' + pattern)
        return found[0]

    def reference(self, path, symbol, match):
        text = self.texts[path]
        return dict(path=path, symbol=symbol, startLine=text.count('\n', 0, match.start()) + 1,
                    endLine=text.count('\n', 0, match.end() - 1) + 1)

    def constant(self, path, symbol):
        m = self.one(path, r'^#define\s+' + re.escape(symbol) + r'\s+([^\n]+)')
        value = m[1].split('//')[0].strip()
        if re.fullmatch(r'0x[0-9a-fA-F]+|\d+', value):
            result = int(value, 0)
        elif re.fullmatch(r'\(FLAG_HIDDEN_ITEMS_START\s*\+\s*\d+\)', value):
            result = self.constant(path, 'FLAG_HIDDEN_ITEMS_START')['value'] + int(re.search(r'\+\s*(\d+)', value)[1])
        else:
            raise ValueError('Unsupported constant expression: ' + symbol)
        return dict(value=result, source=self.reference(path, symbol, m))

    def block(self, path, symbol):
        m = self.one(path, r'^' + re.escape(symbol) + r'::?\n(?:(?!^\w+::?).*\n)*')
        rows = []
        for offset, line in enumerate(m[0].splitlines()):
            if offset > 0 and line.strip():
                rows.append(dict(line=self.texts[path].count('\n', 0, m.start()) + offset + 1, text=line))
        return dict(symbol=symbol, source=self.reference(path, symbol, m), lines=rows)

    def function(self, path, symbol):
        m = self.one(path, r'^(?:static )?[^\n;{}]+\b' + re.escape(symbol) + r'\([^;{}]*\)\n\{[\s\S]*?^\}')
        return dict(symbol=symbol, source=self.reference(path, symbol, m), text=m[0])


def build(source):
    constants = {}
    def constant(path, symbol):
        constants[symbol] = source.constant(path, symbol)
        return constants[symbol]['value']
    pickups, scripts = [], []
    for map_name, array_name, x, y in SCOPE:
        path = 'data/maps/' + map_name + '/map.json'
        map_data = json.loads(source.texts[path], object_pairs_hook=unique)
        matches = [(i, e) for i, e in enumerate(map_data[array_name]) if (e['x'], e['y']) == (x, y)]
        if len(matches) != 1:
            raise ValueError('Missing or duplicate scoped event')
        index, event = matches[0]
        hidden = array_name == 'bg_events'
        if hidden:
            if event['type'] != 'hidden_item' or event['underfoot'] is not False:
                raise ValueError('Unsupported hidden event shape')
            symbol, quantity, script = event['item'], event['quantity'], 'EventScript_HiddenItemScript'
        else:
            if event['graphics_id'] != 'OBJ_EVENT_GFX_ITEM_BALL':
                raise ValueError('Expected item-ball graphics')
            script = event['script']
            block = source.block('data/scripts/item_ball_scripts.inc', script)
            rows = [r['text'].strip() for r in block['lines']]
            if len(rows) != 2 or rows[1] != 'end':
                raise ValueError('Unsupported item-ball script shape')
            m = re.fullmatch(r'finditem (ITEM_\w+)(?:, (\d+))?', rows[0])
            if not m:
                raise ValueError('Expected finditem macro invocation')
            symbol, quantity = m[1], int(m[2] or '1')
            scripts.append(block)
        if symbol not in ('ITEM_POTION', 'ITEM_ANTIDOTE', 'ITEM_POKE_BALL') or quantity != 1:
            raise ValueError('Unsupported pickup item or quantity')
        item_id = constant('include/constants/items.h', symbol)
        item_rows = [i for i in json.loads(source.texts['src/data/items.json'], object_pairs_hook=unique)['items'] if i.get('itemId') == symbol]
        if len(item_rows) != 1:
            raise ValueError('Ambiguous item metadata')
        item = item_rows[0]
        if item['pocket'] not in ('POCKET_ITEMS', 'POCKET_POKE_BALLS'):
            raise ValueError('Unsupported pickup pocket')
        flag_id = constant('include/constants/flags.h', event['flag'])
        # The exact event's unique flag anchors its original JSON object and line range.
        m = source.one(path, r'^    \{\n(?:(?!^    \}).*\n)*?      "flag": "' + event['flag'] + r'"[^\n]*\n(?:(?!^    \}).*\n)*?    \}')
        event_ref = source.reference(path, map_data['id'] + '.' + array_name + '[' + str(index) + ']', m)
        item_m = source.one('src/data/items.json', r'^    \{\n(?:(?!^    \}).*\n)*?      "itemId": "' + symbol + r'",\n(?:(?!^    \}).*\n)*?    \}')
        pickups.append(dict(id=event['flag'], mapSymbol=map_data['id'], kind='hiddenItem' if hidden else 'itemBall',
            sourceEventIndex=index, sourceLocalId=None if hidden else index + 1, x=x, y=y, elevation=event['elevation'],
            script=script, itemSymbol=symbol, sourceItemId=item_id, itemId='pret:' + symbol, itemName=item['english'],
            pocket=item['pocket'], quantity=quantity, flag=event['flag'], sourceFlagId=flag_id,
            underfoot=event.get('underfoot', False), eventSource=event_ref,
            itemSource=source.reference('src/data/items.json', symbol, item_m),
            scriptSource=source.block('data/scripts/obtain_item.inc' if hidden else 'data/scripts/item_ball_scripts.inc', script)['source']))
    if len({p['flag'] for p in pickups}) != 8:
        raise ValueError('Pickup flags must be independent')
    macro_path = 'asm/macros/event.inc'
    macro = source.one(macro_path, r'^\s*\.macro finditem item:req, amount=1\n([\s\S]*?)^\s*\.endm')
    expected = ['setorcopyvar VAR_0x8000, \\item', 'setorcopyvar VAR_0x8001, \\amount', 'callstd STD_FIND_ITEM']
    if [s.strip() for s in macro[1].splitlines() if s.strip()] != expected:
        raise ValueError('finditem expansion differs')
    std = source.one(macro_path, r'^\s*STD_FIND_ITEM = (\d+)$')
    constants['STD_FIND_ITEM'] = dict(value=int(std[1]), source=source.reference(macro_path, 'STD_FIND_ITEM', std))
    table = source.one('data/event_scripts.s', r'^gStdScripts::\n((?:\s*\.4byte[^\n]+\n)+)')
    entries = re.findall(r'\.4byte (\w+)', table[1])
    if entries[int(std[1])] != 'Std_FindItem':
        raise ValueError('STD_FIND_ITEM table binding differs')
    for path, symbols in {
        'include/constants/global.h': ['POCKET_ITEMS', 'POCKET_KEY_ITEMS', 'POCKET_POKE_BALLS', 'POCKET_TM_CASE', 'POCKET_BERRY_POUCH', 'BAG_ITEMS_COUNT', 'BAG_POKEBALLS_COUNT'],
        'include/constants/menu.h': ['STDSTRING_ITEMS_POCKET', 'STDSTRING_KEY_ITEMS_POCKET', 'STDSTRING_POKEBALLS_POCKET', 'STDSTRING_TM_CASE', 'STDSTRING_BERRY_POUCH'],
        'include/constants/vars.h': ['VAR_0x8000', 'VAR_0x8001', 'VAR_0x8004', 'VAR_0x8005', 'VAR_0x8006', 'VAR_0x8007', 'VAR_0x8008', 'VAR_RESULT', 'VAR_LAST_TALKED', 'NPC_TEXT_COLOR_NEUTRAL'],
        'include/constants/songs.h': ['MUS_LEVEL_UP'],
        'include/constants/flags.h': ['FLAG_HIDDEN_ITEMS_START'],
        'include/gba/defines.h': ['TRUE', 'FALSE']}.items():
        for symbol in symbols:
            constant(path, symbol)
    blocks = scripts + [source.block('data/scripts/obtain_item.inc', h) for h in HELPERS]
    texts = []
    for symbol in TEXTS:
        block = source.block('data/text/obtain_item.inc', symbol)
        text = ''.join(json.loads(re.fullmatch(r'\s*\.string (".*")', r['text'])[1]) for r in block['lines'])
        if not text.endswith('$'):
            raise ValueError('Missing source text terminator')
        texts.append(dict(symbol=symbol, text=text, source=block['source']))
    for symbol, std_name in [('gText_ItemsPocket', 'STDSTRING_ITEMS_POCKET'), ('gText_PokeBallsPocket', 'STDSTRING_POKEBALLS_POCKET')]:
        m = source.one('src/strings.c', r'^const u8 ' + symbol + r'\[\] = _\((".*")\);')
        binding = source.one('src/script_menu.c', r'^\s*\[' + std_name + r'\]\s*= ' + symbol + r',')
        texts.append(dict(symbol=symbol, text=json.loads(m[1]), source=source.reference('src/strings.c', symbol, m)))
    native = [source.function(path, symbol) for path, symbol in [
        ('src/field_specials.c', 'SetHiddenItemFlag'), ('src/field_specials.c', 'GetHiddenItemAttr'),
        ('src/field_specials.c', 'BufferTMHMMoveName'), ('src/scrcmd.c', 'ScrCmd_additem'),
        ('src/scrcmd.c', 'ScrCmd_checkitemspace'), ('src/scrcmd.c', 'ScrCmd_checkitemtype'),
        ('src/scrcmd.c', 'ScrCmd_removeobject'), ('src/event_object_movement.c', 'RemoveObjectEventByLocalIdAndMap'),
        ('src/item.c', 'AddBagItem'), ('src/item.c', 'CheckBagHasSpace'),
        ('src/field_control_avatar.c', 'GetInteractedBackgroundEventScript')]]
    blocked = [dict(target=t, reason=r) for t, r in [
        ('EventScript_BufferKeyItemsPocket', 'Only ordinary Items and Poke Balls pockets are in this pickup profile.'),
        ('EventScript_BufferTMCase', 'No TM or HM pickups are in this profile.'),
        ('EventScript_BufferBerryPouch', 'No berry pickups are in this profile.'),
        ('EventScript_FoundTMHM', 'BufferTMHMMoveName returns FALSE for the three bounded item IDs.'),
        ('EventScript_TryPickUpHiddenCoins', 'All three hidden item IDs are nonzero; hidden coins are out of scope.'),
        ('EventScript_FoundMultipleItems', 'All eight source quantities are exactly one.')]]
    dependencies = sorted(set(r['text'].strip().split()[0] for b in blocks for r in b['lines']))
    return dict(schemaVersion=1, sourceCommit=COMMIT, profile=PROFILE,
        inputFiles=list(source.entries.values()), pickups=pickups, constants=constants, scriptBlocks=blocks,
        texts=texts, findItemMacro=dict(source=source.reference(macro_path, 'finditem', macro), defaultQuantity=1,
        instructions=expected, standardScript='Std_FindItem', standardScriptId=int(std[1]),
        bindingSource=source.reference('data/event_scripts.s', 'gStdScripts', table)),
        nativeDependencies=native, requiredOpcodes=dependencies, excludedBranches=blocked,
        limitations=['Eight authenticated pickup definitions only; this artifact does not enable runtime handlers.',
            'Whole original source libraries retain unrelated unsupported branches. They must not authorize other map pickups.',
            'No new map, item catalog, art, ROM, upstream execution, natural walking, save/reload or GUI certification.'])


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw).absolute(), pathlib.Path(out).absolute()
    if raw == out or raw in out.parents or out in raw.parents:
        raise ValueError('Raw and output directories must be disjoint')
    doc = build(Source(raw, lock_path))
    data = (json.dumps(doc, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    manifest = dict(schemaVersion=1, sourceCommit=COMMIT, profile=PROFILE,  inputCount=len(doc['inputFiles']), pickupCount=len(doc['pickups']),
        outputs=[dict(path='early-pickups.json')])
    outputs = {'early-pickups.json': data, 'manifest.json': (json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').encode('utf-8')}
    # Validate every input and every destination before touching previous successful outputs.
    targets = [(safe_file(out, p), b) for p, b in outputs.items()]
    for path, b in targets:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=pathlib.Path, help='Read fixed Git objects into raw first')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/early-pickups')
    args = parser.parse_args()
    if args.repo:
        materialize(args.repo, args.raw)
    print(json.dumps(import_catalog(args.raw, args.out), ensure_ascii=False))
