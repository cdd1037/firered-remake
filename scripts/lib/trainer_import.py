"""Inert FireRed trainer declarations and executable regional battle bindings."""
import ast
from pathlib import Path
import re

def generate(raw, identities):
    """Read full trainer definitions from a directory or source-byte mapping."""
    raw = Path(raw) if not isinstance(raw, dict) else raw
    identities = list(identities)
    if not identities or len(identities) != len(set(identities)):
        raise ValueError('Trainer selection must be nonempty and unique')
    if any(not identity.startswith('pret:TRAINER_') for identity in identities):
        raise ValueError('Expected stable trainer IDs')
    cache = {}
    def text(path):
        if path not in cache:
            cache[path] = raw[path].decode() if isinstance(raw, dict) else (raw / path).read_text()
        return cache[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, text(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one declaration: ' + path + ' ' + pattern)
        return matches[0]
    definitions = {}
    for path in ['include/constants/opponents.h', 'include/constants/trainers.h',
                 'include/constants/species.h', 'include/constants/battle_ai.h']:
        definitions.update(re.findall(r'^#define\s+(\w+)\s+([^\n]+)', text(path), re.M))
    def number(expression):
        expression = expression.split('//')[0].strip()
        def visit(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name):
                return number(definitions[node.id])
            if isinstance(node, ast.BinOp):
                left, right = visit(node.left), visit(node.right)
                if isinstance(node.op, ast.BitOr): return left | right
                if isinstance(node.op, ast.LShift): return left << right
            raise ValueError('Unsupported trainer numeric expression: ' + expression)
        return visit(ast.parse(expression, mode='eval').body)
    def encode(name):
        return [int(one('charmap.txt', "^'" + re.escape("\\'" if c == "'" else c) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16) for c in name]
    def fields(body):
        rows = [re.fullmatch(r'\s*\.(\w+) = (.*),', line) for line in body.splitlines()]
        if any(row is None for row in rows):
            raise ValueError('Unrecognized trainer declaration syntax')
        result = {row[1]: row[2] for row in rows}
        if len(result) != len(rows):
            raise ValueError('Duplicate trainer declaration field')
        return result
    def identity(symbol): return {'value': 'pret:' + symbol}
    trainers = []
    for stable_id in identities:
        symbol = stable_id.removeprefix('pret:')
        declaration = one('src/data/trainers.h', r'^    \[' + re.escape(symbol) + r'\] = \{\n(.*?)^    \},')
        f = fields(declaration[1])
        if set(f) != {'trainerClass', 'encounterMusic_gender', 'trainerPic', 'trainerName', 'items', 'doubleBattle', 'aiFlags', 'party'}:
            raise ValueError('Unrecognized trainer fields: ' + stable_id)
        match = re.fullmatch(r'((?:NO_ITEM|ITEM)_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)', f['party'])
        if not match: raise ValueError('Unrecognized trainer party type: ' + stable_id)
        shape, party_symbol = match.groups()
        custom, held = 'CUSTOM' in shape, shape.startswith('ITEM_')
        party = one('src/data/trainer_parties.h', r'^static const struct TrainerMon' + ('Item' if held else 'NoItem') + ('Custom' if custom else 'Default') + 'Moves ' + party_symbol + r'\[\] = \{\n(.*?)^\};')
        rows = list(re.finditer(r'    \{\n(.*?)    \},\n', party[1], re.S))
        if ''.join(row[0] for row in rows) != party[1] or not 1 <= len(rows) <= 6:
            raise ValueError('Unrecognized trainer party: ' + stable_id)
        members = []
        for row in rows:
            mf = fields(row[1])
            if set(mf) != {'iv', 'lvl', 'species'} | ({'heldItem'} if held else set()) | ({'moves'} if custom else set()):
                raise ValueError('Unrecognized party fields: ' + stable_id)
            species = mf['species']
            species_name = one('src/data/text/species_names.h', r'^    \[' + species + r'\] = _\("([^"\n]+)"\),$')[1]
            moves = [move.strip() for move in mf['moves'].strip('{}').split(',')] if custom else []
            if custom and len(moves) != 4:
                raise ValueError('Custom trainer party must preserve all four move slots')
            members.append(dict(species=identity(species), sourceSpeciesId=number(species),
                speciesName=species_name, encodedSpeciesName=encode(species_name), level=int(mf['lvl']),
                sourceIv=int(mf['iv']), heldItem=mf.get('heldItem', 'ITEM_NONE'), customMoves=[identity(move) for move in moves]))
        name = re.fullmatch(r'_\("([^"\n]+)"\)', f['trainerName'])[1]
        items = [item.strip() for item in f['items'].strip('{}').split(',') if item.strip()]
        if len(items) > 4 or f['doubleBattle'] not in ('TRUE', 'FALSE'):
            raise ValueError('Unrecognized trainer items or battle mode: ' + stable_id)
        items += ['ITEM_NONE'] * (4 - len(items))
        money = int(one('src/battle_main.c', r'^\s*\{' + f['trainerClass'] + r', (\d+)\},$')[1])
        trainers.append(dict(id=identity(symbol), sourceTrainerId=number(symbol), trainerClass=f['trainerClass'],
            sourceTrainerClass=number(f['trainerClass']), trainerPic=f['trainerPic'], sourceTrainerPic=number(f['trainerPic']),
            sourceTrainerName=name, encodedTrainerName=encode(name), encounterMusicGender=number(f['encounterMusic_gender']),
            items=items, doubleBattle=f['doubleBattle'] == 'TRUE', partyFlags=(number('F_TRAINER_PARTY_CUSTOM_MOVESET') if custom else 0) | (number('F_TRAINER_PARTY_HELD_ITEM') if held else 0),
            aiFlags=number(f['aiFlags']), aiFlagSymbols=f['aiFlags'].split(' | '), baseMoney=money, party=members))
    return sorted(trainers, key=lambda trainer: trainer['sourceTrainerId'])



def convert_battles(recipe, sources):
    """Select declarations separately from their executable script occurrences."""
    def text(path):
        return sources[path].decode()

    def one(path, pattern):
        matches = list(re.finditer(pattern, text(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one source declaration: ' + path + ' ' + pattern)
        return matches[0]

    def number(path, symbol):
        match = one(path, r'^#define ' + re.escape(symbol) + r'\s+(\d+)\b[^\n]*')
        return int(match[1])

    def labels(path):
        return re.finditer(r'^(\w+)::\n((?:(?!^\w+::).)*)', text(path), re.M | re.S)

    identities = ['pret:TRAINER_' + short for short in recipe['trainers']]
    if not identities or len(set(identities)) != len(identities):
        raise ValueError('Trainer selection must be nonempty and unique')
    definitions = {row['id']['value']: row for row in generate(sources, identities)}
    trainers = [definitions[identity] for identity in identities]
    battle_type = 1 << int(one('include/constants/battle.h',
        r'^#define BATTLE_TYPE_TRAINER\s+\(1 << (\d+)\)[^\n]*')[1])
    contexts = {}
    for context in recipe.get('dialogueContexts', []):
        matches = [label for label in labels(context['script']) if label[1] == context['label']]
        if len(matches) != 1:
            raise ValueError('Missing or duplicate dialogue context')
        speeches = re.findall(r'^\tmsgbox (\w+)$', matches[0][2], re.M)
        first = context.get('introIndex', 0)
        if len(speeches) != first + 2:
            raise ValueError('Expected introduction and post-battle dialogue')
        for trainer in context['trainers']:
            contexts['TRAINER_' + trainer] = speeches[first:]

    def binding(path, label, battle):
        kind, symbol, operands = battle.groups()
        args = operands.split(', ')
        no_intro = kind == 'no_intro'
        if (no_intro and len(args) != 1) or (not no_intro and len(args) not in (2, 3, 4)):
            raise ValueError('Unsupported trainer battle operands: ' + symbol)
        if len(args) == 4 and args[3] != 'NO_MUSIC':
            raise ValueError('Unsupported trainer music option: ' + args[3])
        mode = ('TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if no_intro else
                'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args) == 4 else
                'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args) == 3 else 'TRAINER_BATTLE_SINGLE')
        post = re.search(r'^\tmsgbox (\w+)', label[2][battle.end():], re.M)
        intro, post_text = contexts.get(symbol, ('' if no_intro else args[0], post[1] if post else ''))
        return dict(trainer={'value': 'pret:' + symbol},
            mode=number('include/constants/battle_setup.h', mode), rivalFlags=0,
            battleTypeFlags=battle_type, scriptEntry=label[1], introText=intro,
            defeatText=args[0] if no_intro else args[1], victoryText='', postBattleText=post_text,
            entryScript='EventScript_DoNoIntroTrainerBattle' if no_intro else 'EventScript_TryDoNormalTrainerBattle',
            callback='CB2_EndTrainerBattle', source=dict(path=path, symbol=label[1],
                startLine=text(path).count('\n', 0, label.start()) + 1,
                endLine=text(path).count('\n', 0, label.end()) + 1),
            postBattleScript=args[2] if len(args) > 2 else None,
            musicOption=args[3] if len(args) > 3 else None)

    occurrences = {}
    for path in recipe['scripts']:
        for label in labels(path):
            for battle in re.finditer(r'^\ttrainerbattle_(single|no_intro) (TRAINER_\w+), ([^\n]+)$', label[2], re.M):
                occurrences.setdefault(battle[2], []).append((path, label, battle))
    battles = []
    for identity in identities:
        candidates = occurrences.get(identity.removeprefix('pret:'), [])
        if len(candidates) != 1:
            raise ValueError('Expected one initial trainer battle: ' + identity)
        battles.append(binding(*candidates[0]))
    if recipe.get('maps'):
        scripts = []
        for name in recipe['maps']:
            document = __import__('json').loads(text(f'data/maps/{name}/map.json'))
            scripts.extend(row['script'] for row in document['object_events']
                           if row['trainer_type'] == 'TRAINER_TYPE_NORMAL')
        if scripts != [battle['scriptEntry'] for battle in battles]:
            raise ValueError('Map trainer order and battle selection differ')
    result = dict(schemaVersion=1, trainers=trainers, battles=battles)
    if 'additionalOccurrences' in recipe:
        additional = []
        for selection in recipe['additionalOccurrences']:
            candidates = [entry for entry in occurrences.get('TRAINER_' + selection['trainer'], [])
                          if entry[0] == selection['script'] and entry[1][1] == selection['label']]
            if len(candidates) != 1:
                raise ValueError('Expected one additional battle occurrence')
            additional.append(binding(*candidates[0]))
        result['additionalOccurrences'] = additional
    if 'moves' in recipe:
        moves = recipe['moves']
        if moves == 'party':
            moves = set()
            for trainer in trainers:
                for member in trainer['party']:
                    if trainer['partyFlags'] & 1:
                        moves.update(row['value'].removeprefix('pret:') for row in member['customMoves'])
                    else:
                        species = member['species']['value'].removeprefix('pret:')
                        slots = natural_moves(text, species, member['level'])
                        moves.update(slots)
            moves = sorted(moves - {'MOVE_NONE'})
        result['moves'] = [{'value': 'pret:' + move} for move in moves]
        result['moveNames'] = [dict(id={'value': 'pret:' + move}, name=one('src/data/text/move_names.h',
            r'^    \[' + move + r'\]\s*= _\("([^"]+)"\),$')[1]) for move in moves]
    return result


class TextSources(dict):
    """Adapt an importer's existing reader without changing its source policy."""
    def __init__(self, read):
        super().__init__()
        self.read = read

    def __missing__(self, path):
        value = self.read(path)
        data = value.encode() if isinstance(value, str) else value
        self[path] = data
        return data


def selected_trainers(read, symbols):
    """Parse source symbols while preserving the caller's selected order."""
    identities = ['pret:' + symbol for symbol in symbols]
    definitions = {row['id']['value']: row for row in generate(TextSources(read), identities)}
    return [definitions[identity] for identity in identities]


def resolved_parties(read, trainers):
    """Resolve custom slots or source level-up moves for build-time consumers."""
    result = []
    for trainer in trainers:
        members = []
        for member in trainer['party']:
            species = member['species']['value'].removeprefix('pret:')
            if trainer['partyFlags'] & 1:
                moves = [row['value'].removeprefix('pret:') for row in member['customMoves']]
            else:
                moves = natural_moves(read, species, member['level'])
            members.append(dict(species=member['species'], level=member['level'], moves=moves))
        result.append(dict(trainer=trainer['id']['value'].removeprefix('pret:'), party=members))
    return result


def read_source(root, path, commit):
    """Read an explicit raw directory or a pinned object in a Git checkout."""
    import subprocess
    root = Path(root)
    if Path(path).is_absolute() or any(part in ('', '.', '..') for part in path.split('/')) or '\\' in path:
        raise ValueError('Unsafe source path: ' + path)
    if (root / '.git').exists():
        return subprocess.check_output(['git', '-C', str(root), 'show', commit + ':' + path])
    target = root / path
    if any(part.is_symlink() for part in (target, *target.parents)):
        raise ValueError('Symlink source rejected: ' + str(target))
    return target.read_bytes()


def natural_moves(read, species, level):
    """Resolve source level-up move slots without consulting generated data."""
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one learnset declaration: ' + pattern)
        return matches[0]
    pointer = one('src/data/pokemon/level_up_learnset_pointers.h', r'^    \[' + species + r'\] = (\w+),$')[1]
    from pokemon_import import learn_rows
    path = 'src/data/pokemon/level_up_learnsets.h'
    text = read(path)
    learnset = one(path, r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};')
    moves = []
    for row in learn_rows(text, learnset):
        learned, move = row[1], row[2]
        if int(learned) <= level and move not in moves:
            moves = (moves + [move])[-4:]
    if not moves:
        raise ValueError('Trainer member has no natural moves: ' + species)
    return moves


def class_names(raw):
    """Import display labels at their existing source class symbols, including facility classes."""
    path = 'src/data/text/trainer_class_names.h'
    text = raw[path].decode() if isinstance(raw, dict) else (Path(raw) / path).read_text()
    match = re.fullmatch(r'\s*const u8 gTrainerClassNames\[\]\[13\] = \{(.*?)\};\s*', text, re.S)
    if not match:
        raise ValueError('Trainer class name table declaration differs')
    names = {}
    for line in match[1].splitlines():
        if not line.strip():
            continue
        row = re.fullmatch(r'\s*\[(TRAINER_CLASS_[A-Z_]+)\]\s*= _\("([^"\\]+)"\),\s*', line)
        if row is None or row[1] in names:
            raise ValueError('Missing or duplicate trainer class name declaration')
        # The source uses the two-tile Pokémon glyph. Runtime display names are plain text.
        value = row[2].replace('{PKMN}', 'POKéMON')
        if not value.strip() or any(c in value for c in '{}\\$') or any(ord(c) < 32 for c in value):
            raise ValueError('Unsupported trainer class name control: ' + row[1])
        names[row[1]] = value
    if not names:
        raise ValueError('Trainer class name table is empty')
    return names
