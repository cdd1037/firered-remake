"""Read the currently migrated static menus from raw data, without running upstream code."""
import re

# Import selection, not runtime scene permissions. Native service menus migrate separately.
STATIC_MENUS = (
    'MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD',
    'MULTICHOICE_YES_NO_INFO',
    'MULTICHOICE_TRAINER_TOWER_MODE',
    'MULTICHOICE_TRAINER_CARD_ICON_TINT',
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


# ListMenu has a separate ID namespace from multichoice. Select only ordinary
# static lists; elevators and transaction-owning services keep their own adapters.
STATIC_LIST_MENUS = ('LISTMENU_BADGES', 'LISTMENU_BERRY_POWDER')


def extract_list_menus(raw, menu_ids=STATIC_LIST_MENUS):
    def read(path):
        return (raw / path).read_text(encoding='utf-8')

    source = read('src/field_specials.c')
    strings = read('src/strings.c')
    constants = read('include/constants/menu.h')
    charmap = {char: int(code, 16) for char, code in re.findall(
        r"^'(.)'\s*=\s*([A-F0-9]{2})\s*$", read('charmap.txt'), re.M)}
    widths = [int(v) for v in re.search(
        r'sFontNormalLatinGlyphWidths\[\]\s*=\s*\{(.*?)\}', read('src/text.c'), re.S)[1].split(',') if v.strip()]
    small_widths = [int(v) for v in re.search(r'sFontSmallLatinGlyphWidths\[\]\s*=\s*\{(.*?)\}', read('src/text.c'), re.S)[1].split(',') if v.strip()]
    if not menu_ids or len(set(menu_ids)) != len(menu_ids):
        raise ValueError('List menu import selection must be nonempty and unique')
    definitions = []
    for menu_id in menu_ids:
        number = re.search(r'^#define ' + re.escape(menu_id) + r'\s+(\d+)\s*$', constants, re.M)
        config = re.search(r'case ' + re.escape(menu_id) + r':\s*(.*?)\s*break;', source, re.S)
        labels = re.search(r'\[' + re.escape(menu_id) + r'\]\s*=\s*\{(.*?)\}', source, re.S)
        if number is None or config is None or labels is None:
            raise ValueError('Missing native list menu definition: ' + menu_id)
        values = {int(k): int(v) for k, v in re.findall(r'task->data\[(\d+)\]\s*=\s*(\d+);', config[1])}
        if not set(range(7)).issubset(values) or values[6] not in (0, 1):
            raise ValueError('Unsupported native list menu layout: ' + menu_id)
        symbols = [s.strip() for s in labels[1].split(',') if s.strip()]
        if len(symbols) != values[1] or not 1 <= values[0] <= len(symbols) or len(symbols) >= 128:
            raise ValueError('Native list menu row mismatch: ' + menu_id)
        options = []
        for symbol in symbols:
            label = re.search(r'const u8 ' + re.escape(symbol) + r'\[\]\s*=\s*_\("([^"\n]+)"\);', strings)
            if label is None:
                raise ValueError('Missing native list menu label: ' + symbol)
            plain = re.sub(r'\{CLEAR_TO 0x[0-9A-Fa-f]+\}\{FONT_SMALL\}', '', label[1])
            if any(c not in charmap or not 32 <= ord(c) <= 126 for c in plain):
                raise ValueError('Unsupported native list menu label: ' + symbol)
            options.append(dict(textSymbol=symbol, text=label[1]))
        def text_width(text):
            price = re.fullmatch(r'([^{}]+)\{CLEAR_TO 0x([0-9A-Fa-f]+)\}\{FONT_SMALL\}([^{}]+)', text)
            if price:
                return int(price[2],16)+sum(small_widths[charmap[c]] for c in price[3])
            return sum(widths[charmap[c]] for c in text)
        width = (max(text_width(o['text']) for o in options) + 9) // 8 + 1
        definitions.append(dict(id=menu_id, sourceId=int(number[1]), options=options, columnWidthTiles=width,
                                listLayout=dict(left=values[2], top=values[3], visibleRows=values[0],
                                                heightTiles=values[5], keepOpen=bool(values[6]))))
    return definitions
