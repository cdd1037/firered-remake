"""Read the currently migrated static menus from raw data, without running upstream code."""
import re

# Import selection, not runtime scene permissions. Native service menus migrate separately.
STATIC_MENUS = (
    'MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD',
    'MULTICHOICE_YES_NO_INFO',
    'MULTICHOICE_TRAINER_TOWER_MODE',
    'MULTICHOICE_HOF_QUIT',
    'MULTICHOICE_EGGS_QUIT',
    'MULTICHOICE_VICTORIES_QUIT',
    'MULTICHOICE_HOF_EGGS_QUIT',
    'MULTICHOICE_HOF_VICTORIES_QUIT',
    'MULTICHOICE_EGGS_VICTORIES_QUIT',
    'MULTICHOICE_HOF_EGGS_VICTORIES_QUIT',

)


def extract_menus(raw, menu_ids=STATIC_MENUS):
    def read(path):
        return (raw / path).read_text(encoding='utf-8')

    source = read('src/script_menu.c')
    strings = read('src/strings.c')
    constants = read('include/constants/menu.h')
    font = read('src/text.c')
    charmap = {char: int(code, 16) for char, code in re.findall(
        r"^'(.)'\s*=\s*([A-F0-9]{2})\s*$", read('charmap.txt'), re.M)}
    widths = [int(v) for v in re.search(
        r'sFontNormalCopy1LatinGlyphWidths\[\]\s*=\s*\{(.*?)\}', font, re.S)[1].split(',') if v.strip()]
    if not menu_ids or len(set(menu_ids)) != len(menu_ids):
        raise ValueError('Menu import selection must be nonempty and unique')
    definitions = []
    for menu_id in menu_ids:
        number = re.search(r'^#define ' + re.escape(menu_id) + r'\s+(\d+)\s*$', constants, re.M)
        binding = re.search(r'\[' + re.escape(menu_id) + r'\]\s*=\s*MULTICHOICE\((\w+)\)', source)
        if number is None or binding is None:
            raise ValueError('Missing menu constant or list binding: ' + menu_id)
        block = re.search(r'static const struct MenuAction ' + binding[1] + r'\[\]\s*=\s*\{(.*?)\};', source, re.S)
        if block is None:
            raise ValueError('Missing menu list: ' + binding[1])
        symbols = re.findall(r'\{\s*(\w+)\s*\}', block[1])
        remainder = re.sub(r'\{\s*\w+\s*\}\s*,?', '', block[1]).strip()
        if not symbols or remainder or len(symbols) >= 128:
            raise ValueError('Unsupported static menu list: ' + menu_id)
        options = []
        for symbol in symbols:
            label = re.search(r'const u8 ' + symbol + r'\[\]\s*=\s*_\("([^"\n]+)"\);', strings)
            if label is None or any(c not in charmap or not 32 <= ord(c) <= 126 for c in label[1]):
                raise ValueError('Unsupported static menu label: ' + symbol)
            options.append(dict(textSymbol=symbol, text=label[1]))
        width = max((sum(widths[charmap[c]] for c in o['text']) + 7) // 8 for o in options) + 1
        definitions.append(dict(id=menu_id, sourceId=int(number[1]), options=options, columnWidthTiles=width))
    return definitions
