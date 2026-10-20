"""Text-only Xzonn catalog projections for the existing SourceTextPack groups.

No files are written, upstream code is never executed, and no rule data is
returned. Generated English owns the selected identities. Pinned English
declarations resolve those identities to Chinese declarations; displayed English
is never used as a lookup key.

Candidate values retain C/source escapes. The caller owns control validation and
normalization. Originals retain the generated consumer's actual representation.
References and coverage are import diagnostics, not runtime resources.
"""
from __future__ import annotations

import json
from pathlib import Path
import re


QUOTED = r'"(?:[^"\\]|\\.)*"'
LITERALS = rf'(?:{QUOTED}\s*)+'
HAN = re.compile(r'[\u3400-\u4dbf\u4e00-\u9fff]')
MEMO_KEYS = (
    'ATrade', 'MetInATrade', 'FatefulEncounterMet', 'Met', 'ApparentlyMet',
    'Hatched', 'FatefulEncounterHatched', 'ApparentlyFatefulEncounterHatched',
)
SOURCE_FIELDS = {
    'src/data/text/species_names.h': ['gSpeciesNames[SPECIES_*]'],
    'src/data/text/move_names.h': ['gMoveNames[MOVE_*]'],
    'src/data/text/abilities.h': [
        'gAbilityNames[ABILITY_*]', 'gAbilityDescriptionPointers[ABILITY_*] -> text declaration',
    ],
    'src/data/text/nature_names.h': ['gNatureNamePointers[NATURE_*] -> text declaration'],
    'src/data/text/trainer_class_names.h': ['gTrainerClassNames[TRAINER_CLASS_*]'],
    'src/battle_main.c': ['gTypeNames[TYPE_*]'],
    'src/data/pokemon/pokedex_entries.h': ['gPokedexEntries[NATIONAL_DEX_*].categoryName'],
    'src/data/pokemon/pokedex_text_fr.h': ['baseline .description symbol -> FireRed text declaration'],
    'src/data/items.json': ['itemId join; english and description_english only'],
    'src/move_descriptions.c': ['baseline MOVE_* / item moveId -> text declaration'],
    'src/strings.c': [
        'existing pokedex-ui symbols', 'gText_PokeSum_* memo symbols',
        'gText_EggNickname', 'gText_PokeSum_Item_None', 'gEasyChatGroupName_*',
    ],
    'src/data/region_map/region_map_sections.json': ['id join; name_zh only'],
    'src/data/trainers.h': ['gTrainers[TRAINER_*].trainerName'],
    'src/data/easy_chat/easy_chat_group_*.h': [
        'baseline word table slot -> sEasyChatWord_* text declaration',
    ],
    'src/keyboard_text.c': ['gText_EasyChatKeyboard_ABCDEFothers (alphabet:0 suffix)'],
    'src/trainer_tower_sets.c': ['baseline floor symbol / trainer slot -> .name'],
}


def _unique(rows, context):
    result = {}
    for key, value in rows:
        if key in result:
            raise ValueError(f'Duplicate {context}: {key}')
        result[key] = value
    return result


def _json(path):
    return json.loads(path.read_text(encoding='utf-8'),
                      object_pairs_hook=lambda rows: _unique(rows, str(path)))


def _unquote_raw(sequence):
    """Join adjacent literals without turning source \\n into actual LF."""
    literals = re.findall(QUOTED, sequence)
    if not literals or re.sub(QUOTED, '', sequence).strip():
        raise ValueError('Unsupported text literal expression')
    return ''.join(literal[1:-1] for literal in literals)


def _decode(value):
    """English ordinary-description representation, only for source fallback."""
    # These C catalogs use Python/JSON-compatible escaping. Keep nonstandard
    # source controls untouched for the caller rather than guessing an encoding.
    return re.sub(r'\\(["\'\\nrt])',
                  lambda m: {'n': '\n', 'r': '\r', 't': '\t'}.get(m[1], m[1]), value)


def _strip_comments(text):
    # Preserve quoted text, including // or /* inside a literal.
    return re.sub(rf'{QUOTED}|\'(?:[^\'\\]|\\.)*\'|//[^\n]*|/\*.*?\*/',
                  lambda m: ('\n' * m[0].count('\n')) if m[0].startswith(('/',))
                  else m[0], text, flags=re.S)


def _brace_end(text, start):
    depth = 0
    for match in re.finditer(rf'{QUOTED}|[{{}}]', text[start:]):
        token = match[0]
        if token == '{':
            depth += 1
        elif token == '}':
            depth -= 1
            if depth == 0:
                return start + match.end()
    raise ValueError('Unclosed text initializer')


def _table(text, symbol):
    pattern = r'\b' + re.escape(symbol) + r'(?:\s*\[[^\]]*\])*\s*=\s*\{'
    matches = list(re.finditer(pattern, text))
    if len(matches) != 1:
        raise ValueError(f'Missing or ambiguous table: {symbol}')
    start = matches[0].end() - 1
    return text[start + 1:_brace_end(text, start) - 1]


def _declarations(text):
    rows = re.findall(
        rf'\b(?:static\s+)?const\s+u8\s+(\w+)\s*\[[^\]]*\]\s*=\s*_\(\s*({LITERALS})\s*\)\s*;',
        _strip_comments(text))
    return _unique(((key, _unquote_raw(value)) for key, value in rows), 'text declaration')


def _names(text, table, prefix):
    body = _table(_strip_comments(text), table)
    rows = re.findall(
        rf'\[({re.escape(prefix)}\w+)\]\s*=\s*_\(\s*({LITERALS})\s*\)\s*,?', body)
    return _unique(((key, _unquote_raw(value)) for key, value in rows), table)


def _pointers(text, table, prefix, offset=False):
    body = _table(_strip_comments(text), table)
    subtract = r'\s*-\s*1' if offset else ''
    return _unique(re.findall(
        rf'\[({re.escape(prefix)}\w+){subtract}\]\s*=\s*(\w+)\s*,?', body), table)


def _structs(text, table, prefix):
    body = _table(_strip_comments(text), table)
    rows = []
    for match in re.finditer(r'\[(' + re.escape(prefix) + r'\w+)\]\s*=\s*\{', body):
        start = match.end() - 1
        rows.append((match[1], body[start + 1:_brace_end(body, start) - 1]))
    return _unique(rows, table)


def _field_literal(body, field):
    matches = re.findall(r'\.' + re.escape(field) + rf'\s*=\s*_\(\s*({LITERALS})\s*\)', body)
    if len(matches) != 1:
        raise ValueError(f'Missing or ambiguous text field: {field}')
    return _unquote_raw(matches[0])


def _constants(text, prefix):
    return _unique(((symbol, int(value, 0)) for symbol, value in re.findall(
        r'^#define\s+(' + re.escape(prefix) + r'\w+)\s+(0x[0-9a-fA-F]+|\d+)\b', text, re.M)),
        prefix + ' constants')


class _Source:
    def __init__(self, root):
        self.root = Path(root)
        self.cache = {}

    def text(self, relative):
        if relative not in self.cache:
            self.cache[relative] = (self.root / relative).read_text(encoding='utf-8')
        return self.cache[relative]

    def declarations(self, relative):
        return _declarations(self.text(relative))


class _Collector:
    """Small shared result builder; it never writes generated resources."""
    def __init__(self):
        self.groups, self.originals, self.references, self.coverage = {}, {}, {}, {}
        self.warnings = []

    def original(self, group, values):
        self.originals[group] = dict(values)
        self.groups[group], self.references[group] = {}, {}
        self.coverage[group] = dict(total=len(values), translated=[], unchanged=[],
                                    missing=[], empty=[], changedWithoutHan=[])

    def add(self, group, key, value, path, symbol, field=None):
        if key not in self.originals[group]:
            raise ValueError(f'Unknown catalog identity: {group}/{key}')
        ref = dict(path=path, symbol=symbol)
        if field:
            ref['field'] = field
        self.references[group][key] = ref
        report = self.coverage[group]
        if value is None:
            report['missing'].append(key)
        elif not value.strip():
            report['empty'].append(key)
        elif _decode(value) == _decode(self.originals[group][key]):
            report['unchanged'].append(key)
        elif not HAN.search(value):
            report['changedWithoutHan'].append(key)
        else:
            self.groups[group][key] = value
            report['translated'].append(key)

    def finish(self, source_fields):
        for group, report in self.coverage.items():
            bins = ('translated', 'unchanged', 'missing', 'empty', 'changedWithoutHan')
            for name in bins:
                report[name].sort()
            if sum(len(report[name]) for name in bins) != report['total']:
                raise ValueError('Incomplete catalog coverage classification: ' + group)
        return dict(groups=self.groups, originals=self.originals, references=self.references,
                    coverage=self.coverage, warnings=self.warnings, sourceFields=source_fields)


def collect_catalogs(project_root, chinese_root):
    """Return sparse Chinese candidates, exact originals, diagnostics and refs.

    Missing generated text-only artifacts are reconstructed from the exact
    existing producer's English declarations and explicitly reported. This does
    not fix or certify the runtime generated-resource prerequisites.
    """
    root = Path(project_root)
    baseline = _Source(root / 'content/raw/pokefirered/037335f4')
    zh = _Source(chinese_root)
    generated = root / 'content/generated'
    result = _Collector()
    original, add = result.original, result.add
    originals, warnings = result.originals, result.warnings

    def literal_group(group, values, path, symbols):
        original(group, values)
        declarations = zh.declarations(path)
        for key in values:
            symbol = symbols[key]
            add(group, key, declarations.get(symbol), path, symbol)

    def named_group(group, values, path, table, prefix, symbol_for_key=lambda x: x):
        original(group, values)
        names = _names(zh.text(path), table, prefix)
        for key in values:
            symbol = symbol_for_key(key)
            add(group, key, names.get(symbol), path, table, symbol)

    # Numeric species IDs are resolved exclusively through the pinned baseline.
    dex = _json(generated / 'sprites/pokemon/pokedex-menu/pokedex-menu.json')
    species = _constants(baseline.text('include/constants/species.h'), 'SPECIES_')
    species_by_id = _unique(((number, symbol) for symbol, number in species.items()),
                            'species numeric identity')
    dex_path = 'src/data/pokemon/pokedex_entries.h'
    baseline_dex = _structs(baseline.text(dex_path), 'gPokedexEntries', 'NATIONAL_DEX_')
    chinese_dex = _structs(zh.text(dex_path), 'gPokedexEntries', 'NATIONAL_DEX_')
    species_names = _names(zh.text('src/data/text/species_names.h'), 'gSpeciesNames', 'SPECIES_')
    descriptions = zh.declarations('src/data/pokemon/pokedex_text_fr.h')
    for group, field in [('species-names', 'name'), ('species-categories', 'category'),
                         ('species-descriptions', 'description')]:
        original(group, {str(row['sourceSpeciesId']): row[field] for row in dex['entries']})
    for row in dex['entries']:
        key = str(row['sourceSpeciesId'])
        symbol = species_by_id[row['sourceSpeciesId']]
        dex_symbol = 'NATIONAL_DEX_' + symbol.removeprefix('SPECIES_')
        add('species-names', key, species_names.get(symbol),
            'src/data/text/species_names.h', 'gSpeciesNames', symbol)
        category = (_field_literal(chinese_dex[dex_symbol], 'categoryName')
                    if dex_symbol in chinese_dex else None)
        add('species-categories', key, category, dex_path, dex_symbol, 'categoryName')
        desc = re.findall(r'\.description\s*=\s*(\w+)', baseline_dex[dex_symbol])
        if len(desc) != 1:
            raise ValueError('Missing baseline Pokédex description: ' + dex_symbol)
        add('species-descriptions', key, descriptions.get(desc[0]),
            'src/data/pokemon/pokedex_text_fr.h', desc[0])
    literal_group('pokedex-ui', dex['uiTexts'], 'src/strings.c',
                  {key: key for key in dex['uiTexts']})

    # The generated item catalog is the admission boundary; dummy duplicate
    # ITEM_NONE rows are never imported or silently collapsed into that catalog.
    items = _json(generated / 'data/items.json')
    item_keys = {row['id']['value']: row['name'] for row in items['items']}
    item_path = 'src/data/items.json'
    en_item_rows = _json(baseline.root / item_path)['items']
    zh_item_rows = _json(zh.root / item_path)['items']

    def selected_items(rows):
        return _unique(((row['itemId'], row) for row in rows
                        if 'pret:' + row['itemId'] in item_keys), 'selected itemId')

    en_items, zh_items = selected_items(en_item_rows), selected_items(zh_item_rows)
    original('item-names', item_keys)
    move_path = 'src/move_descriptions.c'
    en_move_desc, zh_move_desc = baseline.declarations(move_path), zh.declarations(move_path)

    def item_description(key, use_item=False):
        symbol = key.removeprefix('pret:')
        if not use_item and re.fullmatch(r'ITEM_(?:TM|HM)\d+', symbol):
            declaration = 'gMoveDescription_' + en_items[symbol]['moveId']
            return (zh_move_desc.get(declaration), move_path, declaration, None,
                    _decode(en_move_desc[declaration]))
        row = zh_items.get(symbol)
        return (None if row is None else row.get('description_english'),
                item_path, symbol, 'description_english',
                _decode(en_items[symbol]['description_english']))

    if 'descriptions' in items:
        item_originals = items['descriptions']
    else:
        warnings.append('data/items.json lacks descriptions; reconstructed text originals from the existing item producer selection.')
        item_originals = {key: item_description(key)[-1] for key in item_keys}
    original('item-descriptions', item_originals)
    for key in item_keys:
        symbol = key.removeprefix('pret:')
        row = zh_items.get(symbol)
        add('item-names', key, None if row is None else row.get('english'),
            item_path, symbol, 'english')
        value, path, declaration, field, _ = item_description(key)
        add('item-descriptions', key, value, path, declaration, field)

    machines = _json(generated / 'data/machines.json')
    machine_rows = [row['machine'] for row in machines['machines']] + [machines['current']['tmCase']]
    machine_originals = {row['item']: row['description'] for row in machine_rows}
    recipe = _json(root / 'scripts/recipes/config/machine-catalog.json')
    item_description_machines = {'pret:ITEM_' + name for name in recipe['itemDescriptionMachines']}
    original('machine-descriptions', machine_originals)
    for key in machine_originals:
        value, path, declaration, field, _ = item_description(
            key, key in item_description_machines or key == 'pret:ITEM_TM_CASE')
        add('machine-descriptions', key, value, path, declaration, field)

    moves = _json(generated / 'data/services/metronome-moves.json')['moveNames']
    named_group('move-names', {row['id']['value']: row['name'] for row in moves},
                'src/data/text/move_names.h', 'gMoveNames', 'MOVE_',
                lambda key: key.removeprefix('pret:'))

    display_path = generated / 'ui/summary-screen/display-names.json'
    ability_path = 'src/data/text/abilities.h'
    nature_path = 'src/data/text/nature_names.h'
    nature_ids = _constants(baseline.text('include/constants/pokemon.h'), 'NATURE_')
    en_nature_refs = _pointers(baseline.text(nature_path), 'gNatureNamePointers', 'NATURE_')
    en_natures = baseline.declarations(nature_path)
    if display_path.exists():
        display = _json(display_path)
    else:
        warnings.append('summary-screen/display-names.json is missing; reconstructed text originals from existing producer declarations.')
        en_strings = baseline.declarations('src/strings.c')
        display = dict(
            types={key: _decode(value) for key, value in _names(
                baseline.text('src/battle_main.c'), 'gTypeNames', 'TYPE_').items()},
            abilities={key: _decode(value) for key, value in _names(
                baseline.text(ability_path), 'gAbilityNames', 'ABILITY_').items()},
            natures={str(nature_ids[key]): _decode(en_natures[value])
                     for key, value in en_nature_refs.items()},
            eggName=_decode(en_strings['gText_EggNickname']),
            noHeldItem=_decode(en_strings['gText_PokeSum_Item_None']),
        )
    named_group('type-names', display['types'], 'src/battle_main.c', 'gTypeNames', 'TYPE_')
    named_group('ability-names', display['abilities'], ability_path, 'gAbilityNames', 'ABILITY_')
    literal_group('display-labels',
                  {'EggName': display['eggName'], 'NoHeldItem': display['noHeldItem']},
                  'src/strings.c', {'EggName': 'gText_EggNickname', 'NoHeldItem': 'gText_PokeSum_Item_None'})
    # Select canonical English pointer symbols, not a translated table's ordering.
    literal_group('nature-names', display['natures'], nature_path,
                  {str(nature_ids[key]): value for key, value in en_nature_refs.items()})

    summary = _json(generated / 'ui/summary-screen/text.json')
    move_refs = _pointers(baseline.text(move_path), 'gMoveDescriptionPointers', 'MOVE_', offset=True)
    literal_group('summary-moves', summary['moves'], move_path, move_refs)
    ability_refs = _pointers(baseline.text(ability_path), 'gAbilityDescriptionPointers', 'ABILITY_')
    literal_group('summary-abilities', summary['abilities'], ability_path, ability_refs)
    memos = dict(summary['memos'])
    for key in MEMO_KEYS:
        if key not in memos:
            warnings.append('Summary memo ' + key + ' is missing from generated; reconstructed its current producer text.')
            memos[key] = _decode(baseline.declarations('src/strings.c')['gText_PokeSum_' + key])
    literal_group('summary-memos', memos, 'src/strings.c',
                  {key: 'gText_PokeSum_' + key for key in memos})

    section_path = 'src/data/region_map/region_map_sections.json'
    sections = _unique(((row['id'], row) for row in _json(zh.root / section_path)['map_sections']),
                       'map section identity')
    original('summary-locations', summary['locations'])
    for key in summary['locations']:
        add('summary-locations', key, sections.get(key, {}).get('name_zh'), section_path, key, 'name_zh')
    fly = _json(generated / 'ui/fly-map/fly-map.json')
    fly_names = {}
    for region in fly['regions']:
        for cell in region['cells']:
            for id_key, text_key in [('sectionId', 'name'), ('dungeonSectionId', 'dungeonName')]:
                if not cell[text_key]:
                    continue
                key, value = cell[id_key], cell[text_key]
                if key in fly_names and fly_names[key] != value:
                    raise ValueError('Conflicting Fly map name: ' + key)
                fly_names[key] = value
    original('map-section-names', fly_names)
    for key in fly_names:
        add('map-section-names', key, sections.get(key, {}).get('name_zh'), section_path, key, 'name_zh')

    trainers = _json(generated / 'data/trainers.json')['trainers']
    trainer_path = 'src/data/trainers.h'
    trainer_rows = _structs(zh.text(trainer_path), 'gTrainers', 'TRAINER_')
    original('trainer-names', {row['id']['value']: row['sourceTrainerName'] for row in trainers})
    for key in originals['trainer-names']:
        symbol = key.removeprefix('pret:')
        value = _field_literal(trainer_rows[symbol], 'trainerName') if symbol in trainer_rows else None
        add('trainer-names', key, value, trainer_path, symbol, 'trainerName')
    classes_path = generated / 'data/trainer-class-names.json'
    class_path = 'src/data/text/trainer_class_names.h'
    if classes_path.exists():
        classes = _json(classes_path)
    else:
        warnings.append('data/trainer-class-names.json is missing; reconstructed existing producer text originals.')
        classes = {key: _decode(value) for key, value in _names(
            baseline.text(class_path), 'gTrainerClassNames', 'TRAINER_CLASS_').items()}
    named_group('trainer-class-names', classes, class_path, 'gTrainerClassNames', 'TRAINER_CLASS_')

    # Easy Chat words bind through baseline EC_GROUP identity and source slot.
    # The Xzonn headers currently retain English words; unchanged words remain
    # explicit gaps, rather than bloating a supposed Chinese overlay.
    chat = _json(generated / 'data/services/mail-composer/catalog.json')
    chat_names = {row['symbol']: row['name'] for row in chat['groups']}
    chat_names.update({'alphabet:' + str(row['id']): row['name'] for row in chat['alphabetical']})
    original('easy-chat-groups', chat_names)
    ec_names = _pointers(baseline.text('src/easy_chat.c'), 'sEasyChatGroupNamePointers', 'EC_GROUP_')
    zh_strings = zh.declarations('src/strings.c')
    for key in chat_names:
        if key.startswith('alphabet:'):
            if key == 'alphabet:0':
                symbol = 'gText_EasyChatKeyboard_ABCDEFothers'
                value = zh.declarations('src/keyboard_text.c').get(symbol)
                value = None if value is None else value.rsplit('}', 1)[-1]
                add('easy-chat-groups', key, value, 'src/keyboard_text.c', symbol, 'suffix')
            else:
                # A-Z are structural source bucket suffixes, not translated prose.
                add('easy-chat-groups', key, chat_names[key],
                    'src/data/easy_chat/easy_chat_words_by_letter.h', key, 'alphabet bucket suffix')
        else:
            symbol = ec_names[key]
            add('easy-chat-groups', key, zh_strings.get(symbol), 'src/strings.c', symbol)
    words = [row for row in chat['words'] if row['symbol'].startswith('EC_WORD_')]
    original('easy-chat-words', {str(row['value']): row['text'] for row in words})
    group_file = 'src/data/easy_chat/easy_chat_groups.h'
    group_source = baseline.text(group_file)
    include_paths = re.findall(r'#include "(easy_chat_group_\w+\.h)"', group_source)
    group_rows = re.findall(r'\.wordData\s*=\s*\{\.(words|valueList)\s*=\s*(\w+)\}', group_source)
    if len(include_paths) != len(group_rows):
        raise ValueError('Easy Chat table/header binding differs')
    ec_ids = _constants(baseline.text('include/constants/easy_chat.h'), 'EC_GROUP_')
    header_by_group = {}
    for group in chat['groups']:
        group_id = ec_ids[group['symbol']]
        if group['id'] != group_id:
            raise ValueError('Generated Easy Chat group identity differs: ' + group['symbol'])
        kind, table = group_rows[group_id]
        if kind == 'words':
            path = 'src/data/easy_chat/' + include_paths[group_id]
            source_rows = re.findall(r'\.text\s*=\s*(sEasyChatWord_\w+)',
                                     _table(baseline.text(path), table))
            header_by_group[group_id] = (path, source_rows, zh.declarations(path))
    for word in words:
        path, rows, values = header_by_group[word['group']]
        if word['value'] != (word['group'] << 9) | word['index']:
            raise ValueError('Generated Easy Chat word identity differs: ' + word['symbol'])
        symbol = rows[word['index']]
        add('easy-chat-words', str(word['value']), values.get(symbol), path, symbol)

    tower = _json(generated / 'data/regions/trainer-tower/catalog.json')['local']['templates']
    tower_originals = {floor['symbol'] + '/' + str(trainer['index']): trainer['name']
                       for floor in tower for trainer in floor['trainers']}
    original('trainer-tower-names', tower_originals)
    tower_path = 'src/trainer_tower_sets.c'
    tower_text = _strip_comments(zh.text(tower_path))
    for floor in tower:
        body = _table(tower_text, floor['symbol'])
        names = [_unquote_raw(value) for value in re.findall(
            rf'\.name\s*=\s*_\(\s*({LITERALS})\s*\)', body)]
        for trainer in floor['trainers']:
            index = trainer['index']
            key = floor['symbol'] + '/' + str(index)
            add('trainer-tower-names', key, names[index] if index < len(names) else None,
                tower_path, floor['symbol'], 'trainers[' + str(index) + '].name')

    return result.finish(SOURCE_FIELDS)


HELP_GLYPHS = {
    'PLUS': '+', 'LEFT_PAREN': '(', 'RIGHT_PAREN': ')',
    'BIG_MULT_X': '×', 'TRIANGLE': '△', 'CIRCLE_DOT': '⊙',
    **{f'CIRCLE_{number}': str(number) for number in range(1, 6)},
}


def _help_text(value):
    """Exactly the existing HELP producer's display text transformations."""
    value = (value.replace(r'\n', '\n').replace(r'\l', '\n').replace(r'\p', '\n')
             .replace(r'\"', '"').removesuffix('$'))
    for name, glyph in HELP_GLYPHS.items():
        value = value.replace('{' + name + '}', glyph)
    return value


def _opening_text(value):
    """Opening pages use FF; this does not introduce a new pagination format."""
    return _decode(value.replace(r'\p', '\f').replace(r'\l', r'\n')).rstrip('$')


def collect_ui_groups(project_root, chinese_root, declarations):
    """Collect eight remaining UI groups using the shared declaration scanner.

    ``declarations`` is source_text_collector.Collection, with .texts and
    .records. It owns source conditionals, duplicate declarations, and explicit
    source exclusions. This function resolves only the current consumer keys.
    HELP and opening values already use their producer's display representation;
    Teachy TV and story menu values retain source escapes.
    """
    root = Path(project_root)
    baseline = _Source(root / 'content/raw/pokefirered/037335f4')
    generated = root / 'content/generated'
    result = _Collector()
    original, add = result.original, result.add
    texts = declarations.texts
    records = declarations.records

    def declaration(group, key, symbol, transform=lambda value: value, fallback_path=''):
        value = texts.get(symbol)
        matches = records.get(symbol, [])
        paths = {record.path for record in matches}
        path = next(iter(paths)) if len(paths) == 1 else fallback_path
        add(group, key, None if value is None else transform(value), path, symbol)

    # Apply() accepts only the reachable HELP labels/questions/answers, although
    # the retained generated source contains additional unused declarations.
    help_doc = _json(generated / 'ui/help/help.json')
    help_ids = {'Help_Text_Greetings', 'Help_Text_Cancel'}
    for topic in help_doc['topics'].values():
        help_ids.update((topic['label'], topic['description']))
        for entry in topic['entries']:
            help_ids.update((entry['question'], entry['answer']))
    original('help', {key: help_doc['texts'][key] for key in sorted(help_ids)})
    for key in result.originals['help']:
        declaration('help', key, key, _help_text, 'data/text/help_system.inc')

    opening = _json(generated / 'ui/opening/opening.json')
    original('opening', {key: value for key, value in opening['texts'].items()
                         if not key.startswith('gNameChoice_')})
    name_ids = dict.fromkeys(opening['rivalNameSymbols'] + ['gNameChoice_Red', 'gNameChoice_Leaf'])
    original('opening-names', {key: opening['texts'][key] for key in name_ids})
    for group in ('opening', 'opening-names'):
        for key in result.originals[group]:
            declaration(group, key, key, _opening_text, 'data/text/new_game_intro.inc')

    teachy = _json(generated / 'data/services/teachy-tv.json')
    original('teachy-tv', {row['symbol']: row['encodedText'] for row in teachy['texts']})
    for key in result.originals['teachy-tv']:
        declaration('teachy-tv', key, key)

    quest = _json(generated / 'data/quest-log.json')
    original('quest-log', quest['texts'])
    for key in result.originals['quest-log']:
        declaration('quest-log', key, 'gText_QuestLog_' + key, _decode, 'src/data/text/quest_log.h')

    # World aliases preserve the original stable-map identities. Chinese map
    # data is not consulted; only the name_zh display field is read.
    world = _json(generated / 'maps/world.json')
    section_path = 'src/data/region_map/region_map_sections.json'
    zh_sections = _unique(((row['id'], row.get('name_zh'))
                           for row in _json(Path(chinese_root) / section_path)['map_sections']),
                          'Chinese map section')
    map_sections = {}
    for path in sorted((baseline.root / 'data/maps').glob('*/map.json')):
        row = _json(path)
        if row['id'] in map_sections:
            raise ValueError('Duplicate baseline map identity: ' + row['id'])
        map_sections[row['id']] = row['region_map_section']
    stable_sections = {stable: map_sections[symbol] for symbol, stable in world['mapSymbols'].items()
                       if symbol in map_sections}
    for row in world['maps']:
        source_symbol = row['sourceMapSymbol']
        if source_symbol in world['mapSymbols']:
            symbol_stable = world['mapSymbols'][source_symbol]
            if symbol_stable in stable_sections:
                stable_sections[row['stableId']] = stable_sections[symbol_stable]
    original('quest-log-map-names', quest['mapNames'])
    for key in result.originals['quest-log-map-names']:
        section = stable_sections.get(key)
        add('quest-log-map-names', key, zh_sections.get(section), section_path,
            section or key, 'name_zh')

    # The baseline source selects the label symbol and the inside/outside map
    # pair. Forest exits use their outside region, exactly as the existing
    # import-quest-log.py producer does; no translated gameplay rule is used.
    events = baseline.text('src/quest_log_events.c')
    location_names = _pointers(events, 'sLocationNameTexts', 'QL_LOCATION_')
    pairs = _unique(((location, (inside, outside)) for location, inside, outside in re.findall(
        r'\[(QL_LOCATION_\w+)\]\s*=\s*\{MAP\((MAP_\w+)\),\s*MAP\((MAP_\w+)\)\}',
        _table(baseline.text('src/field_specials.c'), 'sInsideOutsidePairs'))), 'Quest Log location')
    departure_originals = {}
    for departure in quest['departures']:
        symbol = departure['locationSymbol']
        departure_originals[symbol + '/place'] = departure['place']
        departure_originals[symbol + '/region'] = departure['regionName']
    original('quest-log-departures', departure_originals)
    for departure in quest['departures']:
        location = departure['locationSymbol']
        symbol = location_names[location]
        declaration('quest-log-departures', location + '/place', symbol, _decode,
                    'src/data/text/quest_log.h')
        inside, outside = pairs[location]
        map_symbol = outside if location.startswith('QL_LOCATION_VIRIDIAN_FOREST_') else inside
        section = map_sections[map_symbol]
        add('quest-log-departures', location + '/region', zh_sections.get(section),
            section_path, section, 'name_zh')

    menus = _json(generated / 'data/story.json')['Compilation']['Menus']
    menu_texts = {}
    for menu in menus.values():
        for option in menu['Options']:
            symbol, value = option['TextSymbol'], option['Text']
            if symbol in menu_texts and menu_texts[symbol] != value:
                raise ValueError('Conflicting generated story menu text: ' + symbol)
            menu_texts[symbol] = value
    original('story-menus', menu_texts)
    for key in menu_texts:
        declaration('story-menus', key, key)

    fields = {
        'data/text/help_system.inc': ['current reachable HELP text symbols only'],
        'data/text/new_game_intro.inc': ['current opening and preset-name symbols only'],
        'src/data/text/teachy_tv.h': ['current Teachy TV text symbols only'],
        'data/text/pokedude.inc': ['current Teachy TV/Pokédude text symbols only'],
        'src/data/text/quest_log.h': ['current Quest Log text and departure place symbols only'],
        section_path: ['baseline map/region identity joins; name_zh only'],
        'src/strings.c': ['existing story-menu TextSymbol only'],
    }
    return result.finish(fields)
