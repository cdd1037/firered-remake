"""Shared source item icons and ordinary visible/hidden pickup conversion.

Region-specific rewards, machine admissions and supported uses stay with their
callers. Inputs are already-read source bytes; no filesystem or Git fallback.
"""
import json
import re

from image_io import indexed_png, png
from item_icon_exports import item_icon_path
from presentation_pixels import PixelReader, palette, rgba


def unique_pairs(rows):
    result = {}
    for key, value in rows:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


class ItemSource:
    def __init__(self, data):
        self.read = data if callable(data) else data.__getitem__
        self._definitions = None

    def definition(self, symbol):
        """Resolve business fields using this source reader, never the project cache."""
        from item_catalog import definitions_from_source
        if self._definitions is None:
            self._definitions = definitions_from_source(self.read)
        symbol = symbol.removeprefix('pret:')
        try:
            return self._definitions[symbol]
        except KeyError as error:
            raise ValueError('Unknown item: ' + symbol) from error

    def text(self, path):
        return self.read(path).decode('utf-8')

    def match(self, path, pattern):
        matches = list(re.finditer(pattern, self.text(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Nonunique source match: ' + path + ' ' + pattern)
        return matches[0]

    def ref(self, path, symbol, match=None):
        text = self.text(path)
        return dict(path=path, symbol=symbol,
                    startLine=1 if match is None else text.count('\n', 0, match.start()) + 1,
                    endLine=max(1, text.count('\n')) if match is None else text.count('\n', 0, match.end()) + 1)

    def icon_sources(self, symbol):
        binding = self.match('src/data/item_icon_table.h',
            r'^    \[' + re.escape(symbol) + r'\][ \t]*= \{(\w+), (\w+)\},')
        paths = []
        for asset in binding.groups():
            graphic = self.match('src/data/graphics/items.h',
                r'^const u32 ' + re.escape(asset) + r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);')
            paths.append(graphic[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
        return paths

    def icon(self, symbol, output_path=None):
        paths = self.icon_sources(symbol)
        image, colors = paths
        width, height, indices, _ = indexed_png(self.read(image))
        if (width, height) != (24, 24):
            raise ValueError('Item icon dimensions differ: ' + symbol)
        pixels = rgba(indices, palette(self.read(colors)))
        path = output_path or item_icon_path(image, colors)
        return dict(item=dict(value='pret:' + symbol), width=width, height=height,
                    pngPath=path), png(width, height, pixels)

    def pickups(self, maps, *, hidden_maps=None):
        result = []
        for name in maps:
            path = 'data/maps/' + name + '/map.json'
            mapping = json.loads(self.text(path), object_pairs_hook=unique_pairs)
            for local_id, event in enumerate(mapping['object_events'], 1):
                if event.get('graphics_id') != 'OBJ_EVENT_GFX_ITEM_BALL':
                    continue
                script = 'data/scripts/item_ball_scripts.inc'
                match = self.match(script, r'^' + re.escape(event['script']) + r'::\n\tfinditem (ITEM_\w+)\n\tend$')
                result.append(dict(map=name, kind='visible', item=dict(value='pret:' + match[1]),
                    quantity=1, localObjectId=local_id, x=event['x'], y=event['y'], elevation=event['elevation'],
                    flag=event['flag'], underfoot=False, script=event['script'], source=self.ref(path, event['flag']),
                    scriptSource=self.ref(script, event['script'], match)))
            if hidden_maps is not None and name not in hidden_maps:
                continue
            for event in mapping['bg_events']:
                if event['type'] != 'hidden_item':
                    continue
                result.append(dict(map=name, kind='hidden', item=dict(value='pret:' + event['item']),
                    quantity=event['quantity'], localObjectId=None, x=event['x'], y=event['y'],
                    elevation=event['elevation'], flag=event['flag'], underfoot=event['underfoot'],
                    script=None, source=self.ref(path, event['flag']), scriptSource=None))
        return result


def selected_item_art(source, selection, emit):
    """Build selected regional item descriptions and exact 24px source icons."""
    items = []
    icons = ItemSource(source.read)
    pixels = PixelReader(source)
    data = json.loads(source.read('src/data/items.json'))['items']
    for name in selection:
        symbol = 'ITEM_' + name
        paths = icons.icon_sources(symbol)
        _, image = pixels.indexed(*paths, (24, 24))
        row = next(row for row in data if row['itemId'] == symbol)
        items.append(dict(item='pret:' + symbol, name=row['english'],
            description=row['description_english'].replace('\\n', '\n'),
            pocket=row['pocket'], fieldUseFunction=row['fieldUseFunc'],
            **emit('item-' + name.lower().replace('_', '-'), 24, 24, image, paths)))
    return items
