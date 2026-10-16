"""Import a source alternate layout while preserving its map identity and events."""
from pathlib import Path

from map_runtime import compact_world, encode


class LayoutSource:
    """Read-only layout selection; underlying cached source bytes stay unchanged."""
    def __init__(self, source, name, layout_symbol):
        self.source = source
        self.map_path = f'data/maps/{name}/map.json'
        self.layout_symbol = layout_symbol

    def __getattr__(self, name):
        return getattr(self.source, name)

    def json(self, path):
        value = self.source.json(path)
        return dict(value, layout=self.layout_symbol) if path == self.map_path else value


def import_layout_variant(source, reader, renderer, base, layout_symbol, output):
    """Use the ordinary renderer/codec for an explicit SetMapLayoutIndex target."""
    layouts = [row for row in source.json('data/layouts/layouts.json')['layouts']
               if row.get('id') == layout_symbol]
    if len(layouts) != 1:
        raise ValueError('Missing or duplicate alternate layout: ' + layout_symbol)
    layout = layouts[0]
    if any(layout[key] != base['layout'][key]
           for key in ('width', 'height', 'border_width', 'border_height')):
        raise ValueError('Alternate layout dimensions differ: ' + layout_symbol)
    selected = LayoutSource(source, base['sourceName'], layout_symbol)
    world = reader.decode(selected, base['sourceName'])
    rich = renderer.import_map(selected, base['sourceName'], base['stableId'], output)
    # Selection changes geometry and art, never the persistent event header.
    world['events'] = base['events']
    rich['events'] = base['events']
    (Path(output) / 'runtime-map.json').write_bytes(encode(compact_world(rich, source.data)))
    return world, dict(layoutSymbol=layout_symbol, mapId=base['stableId'],
        sourceLayout=layout, sourceRef=source.ref('data/layouts/layouts.json', layout_symbol),
        changes=[dict(x=old['x'], y=old['y'], beforeRawBlock=old['rawBlock'], afterRawBlock=new['rawBlock'])
                 for old, new in zip(base['cells'], world['cells'], strict=True)
                 if old['rawBlock'] != new['rawBlock']])
