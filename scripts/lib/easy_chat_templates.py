"""Source-owned Easy Chat templates shared by gameplay and presentation imports."""
import re


def require(condition, message):
    if not condition:
        raise ValueError(message)


def block(source, name):
    match = re.search(r'(?:static )?const [^\n]*\b' + re.escape(name) + r'\[\][^=]*=\s*\{(.*?)^\};', source, re.M | re.S)
    require(match is not None, 'Missing Easy Chat table: ' + name)
    return match[1]


def constants(src):
    return {k: int(v, 0) for k, v in re.findall(r'^#define\s+(\w+)\s+(0x[\da-fA-F]+|\d+)\b',
        src.text('include/constants/easy_chat.h'), re.M)}


def templates(src):
    source = src.text('src/easy_chat_2.c')
    entries = re.findall(r'\{([^{}]+)\}', block(source, 'sEasyChatScreenTemplates'))
    types = constants(src)
    require('if (sEasyChatScreen->numWords > 9)' in source and 'sEasyChatScreen->numWords = 9;' in source,
            'Easy Chat word buffer limit differs')
    cancel = re.search(r'void GetEasyChatConfirmCancelText\([^)]*\)\s*\{(.*?)\n\}', source, re.S)
    require(cancel is not None, 'Missing Easy Chat cancellation text')
    result = []
    for identifier, type_symbol in (('profile', 'EASY_CHAT_TYPE_PROFILE'), ('mail', 'EASY_CHAT_TYPE_MAIL')):
        rows = [dict(re.findall(r'\.(\w+)\s*=\s*(\w+)', row)) for row in entries]
        rows = [row for row in rows if row.get('type') == type_symbol]
        require(len(rows) == 1, 'Missing or duplicate Easy Chat template: ' + identifier)
        row = rows[0]
        columns, count_rows, frame = (int(row[k]) for k in ('numColumns', 'numRows', 'frameId'))
        require(columns > 0 and count_rows > 0, 'Invalid Easy Chat template geometry')
        branch = re.search((r'case EASY_CHAT_TYPE_MAIL:' if identifier == 'mail' else r'default:') + r'(.*?)break;', cancel[1], re.S)
        require(branch is not None, 'Missing Easy Chat cancellation branch')
        cancel_symbols = re.findall(r'\*str[12]\s*=\s*(gText_\w+)', branch[1])
        title = row.get('titleText')
        result.append(dict(id=identifier, type=types[type_symbol], columns=columns, rows=count_rows,
            wordCount=min(columns * count_rows, 9), frameId=frame, titleTextSymbol=None if title == 'NULL' else title,
            instructionTextSymbols=[row['instructionsText1'], row['instructionsText2']],
            confirmTextSymbols=[row['confirmText1'], row['confirmText2']], cancelTextSymbols=cancel_symbols))
    return result


def profile(src):
    ec = constants(src)
    words = {symbol: (ec[group] << 9) | int(index, 0) for symbol, group, index in re.findall(
        r'^#define\s+(EC_WORD_\w+)\s+\((EC_GROUP_\w+) << 9\) \| (0x[\da-fA-F]+)',
        src.text('include/constants/easy_chat.h'), re.M)}
    def phrase(path, name):
        body = block(src.text(path), name)
        tokens = [s.strip() for s in body.split(',') if s.strip()]
        require(len(tokens) == 4 and all(t in words for t in tokens), 'Invalid Easy Chat profile phrase: ' + name)
        return [words[t] for t in tokens]
    return dict(type=ec['EASY_CHAT_TYPE_PROFILE'],
        defaultWords=phrase('src/easy_chat.c', 'sDefaultProfileWords'),
        specialPhrase=phrase('src/easy_chat_2.c', 'sECPhrase_MysteryEventIsExciting'))


def layout(src):
    source = src.text('src/easy_chat_3.c')
    def rows(name):
        return [{k: int(v) for k, v in re.findall(r'\.(\w+)\s*=\s*(\d+)', row)}
                for row in re.findall(r'\{([^{}]+)\}', block(source, name))]
    frames = rows('sPhraseFrameDimensions')
    windows = rows('sEasyChatWindowTemplates')
    require(len(windows) == 3 and windows[0]['width'] * 8 == 128, 'Easy Chat title window differs')
    clear = re.search(r'sText_Clear17\[\]\s*=\s*_\("\{CLEAR (\d+)\}"\)', source)
    require(clear is not None and 'y += 2;' in source and 'i * 16, TEXT_SKIP_DRAW' in source,
            'Easy Chat text printer layout differs')
    result = []
    for template in templates(src):
        frame = frames[template['frameId']]
        require(set(frame) == {'left', 'top', 'width', 'height'}, 'Invalid Easy Chat phrase frame')
        title = windows[0]
        result.append(dict(id=template['id'], **{'frame' + k.title(): v for k, v in frame.items()},
            fieldX=frame['left'] * 8 + int(clear[1]), fieldY=frame['top'] * 8 + 2,
            titleX=title['tilemapLeft'] * 8, titleY=title['tilemapTop'] * 8 + 2, titleWidth=title['width'] * 8,
            backgroundIds={mode: 'easy-chat-' + ('' if template['id'] == 'mail' else template['id'] + '-') + mode
                           for mode in ('fields', 'groups', 'words')}))
    return result
